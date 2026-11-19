#if NET10_0_OR_GREATER
namespace Tailviewer.Settings
{
    /// <summary>The user's decision when importing over an existing profile.</summary>
    public enum ProfileImportChoice
    {
        /// <summary>Require a decision before changing an existing target.</summary>
        RequireChoice,
        /// <summary>Keep the current profile without importing.</summary>
        KeepExisting,
        /// <summary>Explicitly replace the current profile.</summary>
        ReplaceExisting
    }

    /// <summary>The outcome of an import attempt.</summary>
    public enum ProfileImportStatus
    {
        /// <summary>The converted profile was published.</summary>
        Imported,
        /// <summary>The caller must ask how to handle an existing target.</summary>
        Conflict,
        /// <summary>The user kept the current profile.</summary>
        Kept,
        /// <summary>The operation was cancelled before publication.</summary>
        Cancelled,
        /// <summary>The source, backup or publication could not be validated.</summary>
        Failed
    }

    /// <summary>Import outcome and evidence of an immutable verified source backup.</summary>
    public sealed class ProfileImportResult
    {
        internal ProfileImportResult(ProfileImportStatus status, string backupFile, string sourceHash, string backupHash, string error)
        {
            Status = status;
            BackupFile = backupFile;
            SourceSha256 = sourceHash;
            BackupSha256 = backupHash;
            ErrorCode = error;
        }
        /// <summary>The import outcome.</summary>
        public ProfileImportStatus Status { get; }
        /// <summary>The verified original backup, or null when verification did not complete.</summary>
        public string BackupFile { get; }
        /// <summary>The original byte snapshot's SHA-256, if read successfully.</summary>
        public string SourceSha256 { get; }
        /// <summary>The independently reopened backup's SHA-256, if verified.</summary>
        public string BackupSha256 { get; }
        /// <summary>A fixed error code without configuration or credential contents.</summary>
        public string ErrorCode { get; }
        /// <summary>Whether a backup was reopened and its byte hash matched the source.</summary>
        public bool HasVerifiedBackup => BackupFile != null && SourceSha256 != null && SourceSha256 == BackupSha256;
    }
}
#endif
