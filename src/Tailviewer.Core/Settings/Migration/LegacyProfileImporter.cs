#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Tailviewer.Settings
{
    /// <summary>Imports a legacy XML profile without writing to its original file.</summary>
    public sealed class LegacyProfileImporter
    {
        /// <summary>Backs up, validates and imports a selected profile after an explicit conflict decision.</summary>
        public async Task<ProfileImportResult> ImportAsync(string sourceFile, string targetFile, string backupFolder,
            ProfileImportChoice choice = ProfileImportChoice.RequireChoice, CancellationToken cancellationToken = default)
        {
            string sourceHash = null, backupHash = null, verifiedBackup = null;
            var error = "InvalidPaths";
            ProfileImportResult Result(ProfileImportStatus status, string code = null) =>
                new ProfileImportResult(status, verifiedBackup, sourceHash, backupHash, code);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Enum.IsDefined(choice)) return Result(ProfileImportStatus.Failed, error);
                sourceFile = Path.GetFullPath(sourceFile);
                targetFile = Path.GetFullPath(targetFile);
                backupFolder = Path.GetFullPath(backupFolder);
                var sourceFolder = Path.GetDirectoryName(sourceFile);
                var targetBookmarks = Path.Combine(Path.GetDirectoryName(targetFile), "bookmarks.xml");
                var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                if (string.Equals(sourceFolder, Path.GetDirectoryName(targetFile), comparison) ||
                    string.Equals(targetFile, targetBookmarks, comparison) ||
                    string.Equals(Path.GetFileName(targetFile), ".tailviewer-import.xml", comparison) ||
                    Directory.Exists(targetFile) || Directory.Exists(targetBookmarks) ||
                    string.Equals(sourceFile, targetFile, comparison) ||
                    backupFolder.StartsWith(Path.TrimEndingDirectorySeparator(sourceFolder) + Path.DirectorySeparatorChar, comparison) ||
                    string.Equals(sourceFolder, backupFolder, comparison))
                    return Result(ProfileImportStatus.Failed, error);
                RejectLinks(sourceFile);
                RejectLinks(targetFile);
                RejectLinks(targetBookmarks);
                RejectLinks(backupFolder);
                if (choice == ProfileImportChoice.KeepExisting) return Result(ProfileImportStatus.Kept);
                if ((File.Exists(targetFile) || File.Exists(targetBookmarks)) && choice == ProfileImportChoice.RequireChoice)
                    return Result(ProfileImportStatus.Conflict);
                var sourceBookmarks = Path.Combine(sourceFolder, "bookmarks.xml");
                RejectLinks(sourceBookmarks);
                error = "SourceReadFailed";
                // Hold the original against in-place writes and rename/delete aliases until publication completes.
                using (var source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read,
                    4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
                using (var bookmarks = File.Exists(sourceBookmarks)
                    ? new FileStream(sourceBookmarks, FileMode.Open, FileAccess.Read, FileShare.Read,
                        4096, FileOptions.Asynchronous | FileOptions.SequentialScan) : null)
                {
                    if (source.Length > ProfileStore.MaxFileBytes) return Result(ProfileImportStatus.Failed, "SourceTooLarge");
                    var bytes = new byte[checked((int)source.Length)];
                    await source.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
                    sourceHash = Convert.ToHexString(SHA256.HashData(bytes));
                    string bookmarkHash = null;
                    if (bookmarks != null)
                    {
                        if (bookmarks.Length > ProfileStore.MaxFileBytes) return Result(ProfileImportStatus.Failed, "SourceTooLarge");
                        bookmarkHash = Convert.ToHexString(await SHA256.HashDataAsync(bookmarks, cancellationToken).ConfigureAwait(false));
                    }
                    error = "BackupFailed";
                    Directory.CreateDirectory(backupFolder);
                    var backupDirectory = Path.Combine(backupFolder, bookmarkHash == null ? sourceHash :
                        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceHash + "/" + bookmarkHash))));
                    var backup = Path.Combine(backupDirectory, "settings.xml");
                    RejectLinks(backupDirectory);
                    if (!Directory.Exists(backupDirectory))
                    {
                        ProfileStore.CreatePrivateDirectory(backupDirectory);
                        File.Copy(sourceFile, backup, overwrite: false);
                        if (bookmarks != null) File.Copy(sourceBookmarks, Path.Combine(backupDirectory, "bookmarks.xml"), overwrite: false);
                    }
                    // Existing backup bytes are immutable: verify/reuse or fail, never overwrite them.
                    cancellationToken.ThrowIfCancellationRequested();
                    using (var file = new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.Read,
                        4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
                    {
                        if (file.Length != bytes.Length) return Result(ProfileImportStatus.Failed, error);
                        backupHash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken).ConfigureAwait(false));
                        if (backupHash != sourceHash) return Result(ProfileImportStatus.Failed, error);
                        var bookmarkDocument = new XDocument(new XElement("xml"));
                        if (bookmarks != null)
                        {
                            var bookmarkBackup = Path.Combine(backupDirectory, "bookmarks.xml");
                            RejectLinks(bookmarkBackup);
                            using (var copy = new FileStream(bookmarkBackup, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                if (copy.Length != bookmarks.Length ||
                                    Convert.ToHexString(await SHA256.HashDataAsync(copy, cancellationToken).ConfigureAwait(false)) != bookmarkHash)
                                    return Result(ProfileImportStatus.Failed, error);
                                verifiedBackup = backup;
                                error = "InvalidBookmarks";
                                bookmarkDocument = ProfileStore.Read(bookmarkBackup);
                                Bookmarks.Bookmarks.FromDocument(targetBookmarks, bookmarkDocument);
                            }
                        }
                        verifiedBackup = backup;
                        error = "InvalidProfile";
                        var document = ProfileStore.Read(backup);
                        AssignMissingIds(document, "datasources", "datasource", "legacy-source", sourceHash);
                        AssignMissingIds(document, "quickfilters", "quickfilter", "legacy-filter", sourceHash);
                        var model = ApplicationSettings.FromDocument(targetFile, document);
                        if (model.DataSources.GroupBy(item => item.Id).Any(group => group.Count() > 1) ||
                            model.QuickFilters.GroupBy(item => item.Id).Any(group => group.Count() > 1))
                            return Result(ProfileImportStatus.Failed, "DuplicateIdentity");
                        cancellationToken.ThrowIfCancellationRequested();
                        if ((File.Exists(targetFile) || File.Exists(targetBookmarks)) && choice == ProfileImportChoice.RequireChoice)
                            return Result(ProfileImportStatus.Conflict);
                        error = "PublishFailed";
                        if (!ProfileBundleStore.Publish(targetFile, model.BuildDocument(), bookmarkDocument,
                            choice == ProfileImportChoice.ReplaceExisting, cancellationToken))
                            return Result(ProfileImportStatus.Conflict);
                        return Result(ProfileImportStatus.Imported);
                    }
                }
            }
            catch (OperationCanceledException) { return Result(ProfileImportStatus.Cancelled); }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException ||
                failure is ArgumentException || failure is System.Xml.XmlException || failure is FormatException || failure is OverflowException)
            {
                return Result(ProfileImportStatus.Failed, error);
            }
        }

        private static void AssignMissingIds(XDocument document, string container, string item, string domain, string sourceHash)
        {
            var elements = document.Root.Element(container)?.Elements(item).ToList();
            if (elements == null) return;
            for (var index = 0; index < elements.Count; ++index)
                if (elements[index].Attribute("id") == null)
                {
                    // The immutable backup, identity domain and position make retries reproducible.
                    var identity = SHA256.HashData(Encoding.UTF8.GetBytes("Tailviewer/" + domain + "/" + sourceHash + "/" +
                        index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                    elements[index].SetAttributeValue("id", new Guid(identity.AsSpan(0, 16)));
                }
        }

        internal static void RejectLinks(string path)
        {
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Use a resolved profile path.");
            for (var folder = new DirectoryInfo(Path.GetDirectoryName(path)); folder != null; folder = folder.Parent)
                if (folder.Exists && (folder.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Use resolved profile directories.");
            if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Use a resolved backup directory.");
        }
    }
}
#endif
