#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Text;
using System.Security.AccessControl;
using System.Xml;
using System.Xml.Linq;

namespace Tailviewer.Settings
{
    internal static class ProfileStore
    {
        internal const int MaxFileBytes = 16 * 1024 * 1024;
        internal const int MaxDepth = 64;

        internal static void Write(string path, Action<XmlWriter> serialize, Action<XDocument> validate)
        {
            ArgumentNullException.ThrowIfNull(serialize);
            ArgumentNullException.ThrowIfNull(validate);
            path = Path.GetFullPath(path);
            var folder = Path.GetDirectoryName(path);
            Directory.CreateDirectory(folder);
            var candidate = Path.Combine(folder, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            var ownsCandidate = false;
            var replacing = File.Exists(path);
            if (replacing && new FileInfo(path).Length > MaxFileBytes) throw new XmlException("Existing profile exceeds the file-size limit.");
            try
            {
                using (new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    ownsCandidate = true;
                if (replacing)
                {
                    File.Copy(path, candidate, overwrite: true);
                    if (OperatingSystem.IsWindows())
                    {
                        var security = new FileSecurity();
                        security.SetSecurityDescriptorBinaryForm(new FileInfo(path).GetAccessControl(AccessControlSections.Access)
                            .GetSecurityDescriptorBinaryForm(), AccessControlSections.Access);
                        new FileInfo(candidate).SetAccessControl(security);
                    }
                    File.SetCreationTimeUtc(candidate, File.GetCreationTimeUtc(path));
                }
                using (var stream = new FileStream(candidate, FileMode.Truncate, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    using (var writer = XmlWriter.Create(stream, new XmlWriterSettings
                    {
                        Encoding = new UTF8Encoding(false),
                        Indent = true,
                        IndentChars = "  ",
                        NewLineChars = "\r\n",
                        NewLineHandling = NewLineHandling.Entitize,
                        CloseOutput = false
                    })) serialize(writer);
                    if (stream.Length > MaxFileBytes) throw new XmlException("Profile exceeds the file-size limit.");
                    stream.Flush(true);
                }
                validate(Read(candidate));
                // ReplaceFile can remove the destination on native error 1176; publish with one rename instead.
                File.Move(candidate, path, overwrite: replacing);
                ownsCandidate = false;
            }
            finally
            {
                // Never sweep abandoned/foreign candidates or fall back to truncating the destination.
                if (ownsCandidate)
                {
                    var attributes = File.GetAttributes(candidate);
                    if ((attributes & FileAttributes.ReadOnly) != 0)
                        File.SetAttributes(candidate, attributes & ~FileAttributes.ReadOnly);
                    File.Delete(candidate);
                }
            }
        }

        internal static XDocument Read(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                if (stream.Length > MaxFileBytes) throw new XmlException("Profile exceeds the file-size limit.");
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = MaxFileBytes,
                    MaxCharactersFromEntities = MaxFileBytes,
                    CloseInput = false
                };
                // Check depth before allocating/cloning an XML tree with opaque plugin payloads.
                using (var reader = XmlReader.Create(stream, settings))
                    while (reader.Read())
                        if (reader.Depth > MaxDepth) throw new XmlException("Profile exceeds the XML depth limit.");
                stream.Position = 0;
                using (var reader = XmlReader.Create(stream, settings))
                {
                    var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
                    if (document.Root == null || document.Root.Name != "xml") throw new XmlException("Unexpected profile root.");
                    return document;
                }
            }
        }
    }
}
#endif
