#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace Tailviewer.Settings
{
    internal static class ProfileBundleStore
    {
        private const string JournalName = ".tailviewer-import.xml";

        internal static T WithLock<T>(string path, Func<T> action)
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(path));
            var key = OperatingSystem.IsWindows() ? folder.ToUpperInvariant() : folder;
            using (var mutex = new Mutex(false, "Tailviewer.Profile." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))))
            {
                try { mutex.WaitOne(); }
                catch (AbandonedMutexException) { /* Ownership transfers to this recovery attempt. */ }
                try
                {
                    Recover(folder);
                    return action();
                }
                finally { mutex.ReleaseMutex(); }
            }
        }

        internal static bool Publish(string settingsPath, XDocument settings, XDocument bookmarks,
            bool replace, CancellationToken cancellationToken, Action<int> published = null)
        {
            return WithLock(settingsPath, () =>
            {
                var folder = Path.GetDirectoryName(Path.GetFullPath(settingsPath));
                var names = new[] { Path.GetFileName(settingsPath), "bookmarks.xml" };
                var documents = new[] { settings, bookmarks };
                if (!replace && names.Any(name => File.Exists(Path.Combine(folder, name)))) return false;
                Directory.CreateDirectory(folder);
                var stageName = "." + Guid.NewGuid().ToString("N") + ".import";
                var stage = Path.Combine(folder, stageName);
                ProfileStore.CreatePrivateDirectory(stage);
                var journal = new XDocument(new XElement("xml", new XAttribute("stage", stageName)));
                // Prepare every candidate and durable rollback copy before publishing the journal.
                for (var index = 0; index < names.Length; ++index)
                {
                    var name = names[index];
                    var target = Path.Combine(folder, name);
                    LegacyProfileImporter.RejectLinks(target);
                    var candidate = Path.Combine(stage, index + ".after");
                    var document = documents[index];
                    ProfileStore.WriteCore(candidate, writer => document.Save(writer), parsed => Validate(index, target, parsed));
                    var entry = new XElement("file", new XAttribute("name", name), new XAttribute("after", Hash(candidate)));
                    if (File.Exists(target))
                    {
                        var before = Path.Combine(stage, index + ".before");
                        Copy(target, before);
                        entry.SetAttributeValue("before", Hash(before));
                    }
                    journal.Root.Add(entry);
                }
                cancellationToken.ThrowIfCancellationRequested();
                var journalPath = Path.Combine(folder, JournalName);
                ProfileStore.WriteCore(journalPath, writer => journal.Save(writer), _ => { }, allowReplace: false);
                try
                {
                    for (var index = 0; index < names.Length; ++index)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var target = Path.Combine(folder, names[index]);
                        ProfileStore.WriteCore(target, writer => documents[index].Save(writer), parsed => Validate(index, target, parsed), replace);
                        published?.Invoke(index);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    // Deleting the durable undo journal is the commit point for the whole pair.
                    File.Delete(journalPath);
                    return true;
                }
                catch
                {
                    Recover(folder);
                    throw;
                }
                // Keep the private recovery directory; never sweep unrelated files after a crash.
            });
        }

        private static void Validate(int index, string path, XDocument document)
        {
            if (index == 0) ApplicationSettings.FromDocument(path, document);
            else Bookmarks.Bookmarks.FromDocument(path, document);
        }

        private static void Recover(string folder)
        {
            var journalPath = Path.Combine(folder, JournalName);
            if (!File.Exists(journalPath)) return;
            LegacyProfileImporter.RejectLinks(journalPath);
            var journal = ProfileStore.ReadCore(journalPath);
            var stageName = (string)journal.Root.Attribute("stage");
            if (stageName == null || stageName.Length != 40 || stageName[0] != '.' || !stageName.EndsWith(".import", StringComparison.Ordinal) ||
                !Guid.TryParseExact(stageName.Substring(1, 32), "N", out _)) throw new XmlException("Invalid import recovery directory.");
            var stage = Path.Combine(folder, stageName);
            LegacyProfileImporter.RejectLinks(stage);
            var entries = journal.Root.Elements("file").ToList();
            if (entries.Count != 2 || (string)entries[1].Attribute("name") != "bookmarks.xml")
                throw new XmlException("Invalid import recovery entries.");
            var name = (string)entries[0].Attribute("name");
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (string.IsNullOrEmpty(name) || name != Path.GetFileName(name) || string.Equals(name, "bookmarks.xml", comparison) ||
                string.Equals(name, JournalName, comparison) || name.Contains(':') || name == "." || name == "..")
                throw new XmlException("Invalid import recovery target.");
            // Verify every undo copy and target before changing either member.
            for (var index = 0; index < entries.Count; ++index)
            {
                var target = Path.Combine(folder, (string)entries[index].Attribute("name"));
                LegacyProfileImporter.RejectLinks(target);
                var before = (string)entries[index].Attribute("before");
                if (!IsHash((string)entries[index].Attribute("after")) || (before != null && !IsHash(before)))
                    throw new XmlException("Invalid import recovery digest.");
                if (before != null && Hash(Path.Combine(stage, index + ".before")) != before)
                    throw new IOException("Import recovery copy failed verification.");
                if (File.Exists(target))
                {
                    var current = Hash(target);
                    if (current != before && current != (string)entries[index].Attribute("after"))
                        throw new IOException("Import destination changed outside the transaction.");
                }
            }
            for (var index = 0; index < entries.Count; ++index)
            {
                var target = Path.Combine(folder, (string)entries[index].Attribute("name"));
                var before = (string)entries[index].Attribute("before");
                if (File.Exists(target) && Hash(target) == before) continue;
                if (before == null) File.Delete(target);
                else
                {
                    var candidate = Path.Combine(stage, Guid.NewGuid().ToString("N") + ".restore");
                    Copy(Path.Combine(stage, index + ".before"), candidate);
                    File.Move(candidate, target, overwrite: true);
                }
            }
            File.Delete(journalPath);
        }

        private static string Hash(string path)
        {
            LegacyProfileImporter.RejectLinks(path);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > ProfileStore.MaxFileBytes) throw new IOException("Import recovery file exceeds the size limit.");
                return Convert.ToHexString(SHA256.HashData(stream));
            }
        }

        private static bool IsHash(string value) => value != null && value.Length == 64 &&
            value.All(character => (character >= '0' && character <= '9') || (character >= 'A' && character <= 'F'));

        private static void Copy(string source, string target)
        {
            // Bound and lock the source through copying, including alternate streams on Windows.
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (input.Length > ProfileStore.MaxFileBytes) throw new IOException("Import recovery file exceeds the size limit.");
                File.Copy(source, target, overwrite: false);
                if (OperatingSystem.IsWindows())
                {
                    var security = new FileSecurity();
                    security.SetSecurityDescriptorBinaryForm(new FileInfo(source).GetAccessControl(AccessControlSections.Access)
                        .GetSecurityDescriptorBinaryForm(), AccessControlSections.Access);
                    new FileInfo(target).SetAccessControl(security);
                }
                File.SetCreationTimeUtc(target, File.GetCreationTimeUtc(source));
                if ((File.GetAttributes(target) & FileAttributes.ReadOnly) == 0)
                    using (var output = new FileStream(target, FileMode.Open, FileAccess.Write, FileShare.None)) output.Flush(true);
            }
        }
    }
}
#endif
