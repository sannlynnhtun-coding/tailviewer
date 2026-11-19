#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NUnit.Framework;
using Tailviewer.Settings;
using BookmarkCollection = Tailviewer.Settings.Bookmarks.Bookmarks;

namespace Tailviewer.Core.Tests.Migration
{
    [TestFixture]
    public sealed class ProfileImportTest
    {
        private const string SourceId = "11111111-1111-1111-1111-111111111111";
        private const string FilterId = "22222222-2222-2222-2222-222222222222";
        private string _folder, _source, _target, _backups;
        private readonly LegacyProfileImporter _importer = new LegacyProfileImporter();

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "tailviewer-import-" + Guid.NewGuid().ToString("N"));
            _source = Path.Combine(_folder, "legacy", "settings.xml");
            _target = Path.Combine(_folder, "modern", "settings.xml");
            _backups = Path.Combine(_folder, "backups");
            Directory.CreateDirectory(Path.GetDirectoryName(_source));
            File.WriteAllText(_source, "<?future keep?><xml xmlns:p='urn:future' future='keep'><logviewer fontsize='18'/>" +
                "<datasources selecteditem='" + SourceId + "'><datasource id='" + SourceId + "' file='missing.log' p:kind='p:future'>" +
                "<p:data><![CDATA[a < b]]></p:data></datasource></datasources><quickfilters><quickfilter id='" + FilterId +
                "' type='SubstringFilter' value='မြန်မာ &amp; log'/></quickfilters><autoupdate checkforupdates='true'/>" +
                "<p:plugin future='keep'/></xml>");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var file in Directory.EnumerateFiles(_folder, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_folder, true);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task OriginalAndBackupHashesMatchAndUnknownValuesSurviveTwoRestarts(bool readOnly)
        {
            var original = File.ReadAllBytes(_source);
            if (readOnly) File.SetAttributes(_source, FileAttributes.ReadOnly);
            var result = await Import();
            Assert.That(result.Status, Is.EqualTo(ProfileImportStatus.Imported));
            Assert.That(result.HasVerifiedBackup, Is.True);
            Assert.That(result.SourceSha256, Is.EqualTo(Hash(original)));
            Assert.That(result.BackupSha256, Is.EqualTo(Hash(File.ReadAllBytes(result.BackupFile))));
            Assert.That(File.ReadAllBytes(result.BackupFile), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(_source), Is.EqualTo(original));
            for (var i = 0; i < 2; ++i)
            {
                var settings = new ApplicationSettings(_target);
                settings.Restore();
                Assert.That(settings.LogViewer.FontSize, Is.EqualTo(18));
                Assert.That(settings.AutoUpdate.CheckForUpdates, Is.True);
                Assert.That(settings.DataSources.Single().Id.Value.ToString(), Is.EqualTo(SourceId));
                Assert.That(settings.QuickFilters.Single().Value, Is.EqualTo("မြန်မာ & log"));
                Assert.That(settings.Save(), Is.True);
                var xml = XDocument.Load(_target).Root;
                Assert.That((string)xml.Attribute("future"), Is.EqualTo("keep"));
                Assert.That(xml.Element(XName.Get("plugin", "urn:future")), Is.Not.Null);
                Assert.That(xml.DescendantNodes().OfType<XCData>().Single().Value, Is.EqualTo("a < b"));
            }
            Assert.That(File.ReadAllBytes(_source), Is.EqualTo(original));
        }

        [Test]
        public async Task ExistingTargetRequiresAnExplicitDecision()
        {
            var before = CreateTarget();
            var result = await Import();
            Assert.That(result.Status, Is.EqualTo(ProfileImportStatus.Conflict));
            Assert.That(result.HasVerifiedBackup, Is.False);
            Assert.That(Directory.Exists(_backups), Is.False);
            Assert.That(File.ReadAllBytes(_target), Is.EqualTo(before));
        }

        [Test]
        public async Task ExistingTargetBookmarksRequireAnExplicitDecision()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_target));
            var targetBookmarks = Path.Combine(Path.GetDirectoryName(_target), "bookmarks.xml");
            File.WriteAllText(targetBookmarks, "<xml current='keep'/>");
            var before = File.ReadAllBytes(targetBookmarks);
            var result = await Import();
            Assert.That(result.Status, Is.EqualTo(ProfileImportStatus.Conflict));
            Assert.That(result.HasVerifiedBackup, Is.False);
            Assert.That(Directory.Exists(_backups), Is.False);
            Assert.That(File.Exists(_target), Is.False);
            Assert.That(File.ReadAllBytes(targetBookmarks), Is.EqualTo(before));
        }

        [TestCase(ProfileImportChoice.KeepExisting)]
        [TestCase(ProfileImportChoice.ReplaceExisting)]
        public async Task BookmarkDestinationCannotAlsoBeSettingsDestination(ProfileImportChoice choice)
        {
            var before = File.ReadAllBytes(_source);
            var destination = Path.Combine(Path.GetDirectoryName(_target), "bookmarks.xml");
            var result = await _importer.ImportAsync(_source, destination, _backups, choice);
            Assert.That(result.Status, Is.EqualTo(ProfileImportStatus.Failed));
            Assert.That(result.ErrorCode, Is.EqualTo("InvalidPaths"));
            Assert.That(File.ReadAllBytes(_source), Is.EqualTo(before));
            Assert.That(File.Exists(destination), Is.False);
            Assert.That(Directory.Exists(_backups), Is.False);
        }

        [Test]
        public async Task KeepExistingDoesNotWriteOrCreateBackup()
        {
            var before = CreateTarget();
            var result = await Import(ProfileImportChoice.KeepExisting);
            Assert.That(result.Status, Is.EqualTo(ProfileImportStatus.Kept));
            Assert.That(File.ReadAllBytes(_target), Is.EqualTo(before));
            Assert.That(Directory.Exists(_backups), Is.False);
        }

        [Test]
        public async Task CancellationKeepsCurrentTargetAndDoesNotClaimBackup()
        {
            var before = CreateTarget();
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var result = await Import(ProfileImportChoice.ReplaceExisting, cancellation.Token);
                Assert.That(result.Status, Is.EqualTo(ProfileImportStatus.Cancelled));
                Assert.That(result.HasVerifiedBackup, Is.False);
            }
            Assert.That(File.ReadAllBytes(_target), Is.EqualTo(before));
            Assert.That(Directory.Exists(_backups), Is.False);
        }

        [Test]
        public async Task CorruptExistingBackupIsNeverOverwrittenOrReportedVerified()
        {
            var before = CreateTarget();
            var source = File.ReadAllBytes(_source);
            var backup = Path.Combine(_backups, Hash(source), "settings.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            var corrupt = (byte[])source.Clone();
            corrupt[0] ^= 1;
            File.WriteAllBytes(backup, corrupt);
            var result = await Import(ProfileImportChoice.ReplaceExisting);
            Assert.That(result.Status, Is.EqualTo(ProfileImportStatus.Failed));
            Assert.That(result.ErrorCode, Is.EqualTo("BackupFailed"));
            Assert.That(result.HasVerifiedBackup, Is.False);
            Assert.That(File.ReadAllBytes(backup), Is.EqualTo(corrupt));
            Assert.That(File.ReadAllBytes(_target), Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(_source), Is.EqualTo(source));
        }

        [Test]
        public async Task BackupFailurePreventsPublication()
        {
            var before = CreateTarget();
            File.WriteAllText(_backups, "not a directory");
            var result = await Import(ProfileImportChoice.ReplaceExisting);
            Assert.That(result.Status, Is.EqualTo(ProfileImportStatus.Failed));
            Assert.That(result.ErrorCode, Is.EqualTo("BackupFailed"));
            Assert.That(result.HasVerifiedBackup, Is.False);
            Assert.That(File.ReadAllBytes(_target), Is.EqualTo(before));
        }

        [TestCase("<xml><unfinished>")]
        [TestCase("<xml><datasources><datasource id='bad'/></datasources></xml>")]
        [TestCase("<!DOCTYPE xml [<!ELEMENT xml ANY>]><xml/>")]
        public async Task InvalidProfileRetainsItsVerifiedRawBackupAndCurrentTarget(string invalid)
        {
            var before = CreateTarget();
            File.WriteAllText(_source, invalid);
            var original = File.ReadAllBytes(_source);
            var result = await Import(ProfileImportChoice.ReplaceExisting);
            Assert.That(result.Status, Is.EqualTo(ProfileImportStatus.Failed));
            Assert.That(result.ErrorCode, Is.EqualTo("InvalidProfile"));
            Assert.That(result.HasVerifiedBackup, Is.True);
            Assert.That(File.ReadAllBytes(result.BackupFile), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(_target), Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(_source), Is.EqualTo(original));
        }

        [Test]
        public async Task SourceCanNeverBeTheWritableDestination()
        {
            var original = File.ReadAllBytes(_source);
            var result = await _importer.ImportAsync(_source, _source, _backups, ProfileImportChoice.ReplaceExisting);
            Assert.That(result.Status, Is.EqualTo(ProfileImportStatus.Failed));
            Assert.That(File.ReadAllBytes(_source), Is.EqualTo(original));
            Assert.That(Directory.Exists(_backups), Is.False);
        }

        [TestCase("datasources", "datasource", SourceId)]
        [TestCase("quickfilters", "quickfilter", FilterId)]
        public async Task DuplicateIdentitiesDoNotReplaceAValidTarget(string container, string record, string id)
        {
            var before = CreateTarget();
            var xml = XDocument.Load(_source);
            xml.Root.Element(container).Add(new XElement(xml.Root.Element(container).Element(record)));
            xml.Save(_source);
            var result = await Import(ProfileImportChoice.ReplaceExisting);
            Assert.That(result.Status, Is.EqualTo(ProfileImportStatus.Failed));
            Assert.That(result.ErrorCode, Is.EqualTo("DuplicateIdentity"));
            Assert.That(File.ReadAllBytes(_target), Is.EqualTo(before));
        }

        [Test]
        public async Task CyclicSourceGroupsDoNotReplaceTheCurrentProfile()
        {
            var before = CreateTarget();
            var document = XDocument.Load(_source);
            document.Root.Element("datasources").Element("datasource").SetAttributeValue("parentid", SourceId);
            document.Save(_source);
            var original = File.ReadAllBytes(_source);
            var result = await Import(ProfileImportChoice.ReplaceExisting);
            Assert.That(result.Status, Is.EqualTo(ProfileImportStatus.Failed));
            Assert.That(result.ErrorCode, Is.EqualTo("InvalidProfile"));
            Assert.That(result.HasVerifiedBackup, Is.True);
            Assert.That(File.ReadAllBytes(_source), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(_target), Is.EqualTo(before));
        }

        [Test]
        public async Task RepeatedImportPreservesExistingIdentitiesWithoutDuplicatingSources()
        {
            var first = await Import();
            var second = await Import(ProfileImportChoice.ReplaceExisting);
            Assert.That(first.Status, Is.EqualTo(ProfileImportStatus.Imported));
            Assert.That(second.Status, Is.EqualTo(ProfileImportStatus.Imported));
            Assert.That(second.BackupFile, Is.EqualTo(first.BackupFile));
            var settings = new ApplicationSettings(_target);
            settings.Restore();
            Assert.That(settings.DataSources.Count, Is.EqualTo(1));
            Assert.That(settings.DataSources[0].Id.Value.ToString(), Is.EqualTo(SourceId));
        }

        [TestCase(1)]
        [TestCase(2)]
        public async Task RepeatedImportAssignsStableIdentitiesToLegacySourcesWithoutIds(int missingCount)
        {
            var xml = XDocument.Load(_source);
            var sources = xml.Root.Element("datasources");
            for (var index = 0; index < missingCount; ++index)
            {
                var missing = new XElement(sources.Element("datasource"));
                missing.Attribute("id").Remove();
                sources.Add(missing);
            }
            xml.Save(_source);
            Assert.That((await Import()).Status, Is.EqualTo(ProfileImportStatus.Imported));
            var first = new ApplicationSettings(_target);
            first.Restore();
            Assert.That((await Import(ProfileImportChoice.ReplaceExisting)).Status, Is.EqualTo(ProfileImportStatus.Imported));
            var second = new ApplicationSettings(_target);
            second.Restore();
            Assert.That(second.DataSources.Select(item => item.Id), Is.EqualTo(first.DataSources.Select(item => item.Id)));
            Assert.That(second.DataSources.Select(item => item.Id).Distinct().Count(), Is.EqualTo(missingCount + 1));
            Assert.That(second.DataSources[0].Id.Value.ToString(), Is.EqualTo(SourceId));
        }

        [Test]
        public async Task RepeatedImportAssignsStableDistinctIdentitiesToLegacyFiltersWithoutIds()
        {
            var xml = XDocument.Load(_source);
            var filters = xml.Root.Element("quickfilters");
            for (var index = 0; index < 2; ++index)
            {
                var missing = new XElement(filters.Element("quickfilter"));
                missing.Attribute("id").Remove();
                filters.Add(missing);
            }
            xml.Save(_source);
            Assert.That((await Import()).Status, Is.EqualTo(ProfileImportStatus.Imported));
            var first = new ApplicationSettings(_target);
            first.Restore();
            Assert.That((await Import(ProfileImportChoice.ReplaceExisting)).Status, Is.EqualTo(ProfileImportStatus.Imported));
            var second = new ApplicationSettings(_target);
            second.Restore();
            Assert.That(second.QuickFilters.Select(item => item.Id), Is.EqualTo(first.QuickFilters.Select(item => item.Id)));
            Assert.That(second.QuickFilters.Select(item => item.Id).Distinct().Count(), Is.EqualTo(3));
            Assert.That(second.QuickFilters[0].Id.Value.ToString(), Is.EqualTo(FilterId));
        }

        [Test]
        public async Task InvalidBookmarksKeepVerifiedRawBundleAndCurrentTarget()
        {
            var before = CreateTarget();
            var sourceBookmarks = Path.Combine(Path.GetDirectoryName(_source), "bookmarks.xml");
            File.WriteAllText(sourceBookmarks, "<xml><bookmark datasource='invalid' index='17'/></xml>");
            var original = File.ReadAllBytes(sourceBookmarks);
            var result = await Import(ProfileImportChoice.ReplaceExisting);
            Assert.That(result.Status, Is.EqualTo(ProfileImportStatus.Failed));
            Assert.That(result.ErrorCode, Is.EqualTo("InvalidBookmarks"));
            Assert.That(result.HasVerifiedBackup, Is.True);
            Assert.That(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(result.BackupFile), "bookmarks.xml")), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(sourceBookmarks), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(_target), Is.EqualTo(before));
        }

        [Test]
        public async Task CorruptBookmarkBackupPreventsPublicationAndVerifiedClaim()
        {
            var before = CreateTarget();
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(_source), "bookmarks.xml"),
                "<xml><bookmark datasource='" + SourceId + "' index='17'/></xml>");
            var first = await Import(ProfileImportChoice.ReplaceExisting);
            Assert.That(first.HasVerifiedBackup, Is.True);
            before = File.ReadAllBytes(_target);
            var backup = Path.Combine(Path.GetDirectoryName(first.BackupFile), "bookmarks.xml");
            var bytes = File.ReadAllBytes(backup);
            bytes[0] ^= 1;
            File.WriteAllBytes(backup, bytes);
            var second = await Import(ProfileImportChoice.ReplaceExisting);
            Assert.That(second.Status, Is.EqualTo(ProfileImportStatus.Failed));
            Assert.That(second.ErrorCode, Is.EqualTo("BackupFailed"));
            Assert.That(second.HasVerifiedBackup, Is.False);
            Assert.That(File.ReadAllBytes(backup), Is.EqualTo(bytes));
            Assert.That(File.ReadAllBytes(_target), Is.EqualTo(before));
        }

        [Test]
        public async Task ReplacingWithProfileWithoutBookmarksClearsPreviousBookmarkSnapshot()
        {
            CreateTarget();
            var targetBookmarks = Path.Combine(Path.GetDirectoryName(_target), "bookmarks.xml");
            File.WriteAllText(targetBookmarks, "<xml><bookmark datasource='" + SourceId + "' index='17'/></xml>");
            Assert.That((await Import(ProfileImportChoice.ReplaceExisting)).Status, Is.EqualTo(ProfileImportStatus.Imported));
            var marks = new BookmarkCollection(targetBookmarks);
            marks.Restore();
            Assert.That(marks.Count, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task LockedSecondDestinationRollsBackTheWholeBundle(bool existingSettings)
        {
            byte[] before = existingSettings ? CreateTarget() : null;
            Directory.CreateDirectory(Path.GetDirectoryName(_target));
            var targetBookmarks = Path.Combine(Path.GetDirectoryName(_target), "bookmarks.xml");
            File.WriteAllText(targetBookmarks, "<xml current='keep'/>");
            var originalBookmarks = File.ReadAllBytes(targetBookmarks);
            using (var held = new FileStream(targetBookmarks, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var result = await Import(ProfileImportChoice.ReplaceExisting);
                Assert.That(result.Status, Is.EqualTo(ProfileImportStatus.Failed));
                Assert.That(result.ErrorCode, Is.EqualTo("PublishFailed"));
                Assert.That(File.Exists(_target), Is.EqualTo(existingSettings));
                if (existingSettings) Assert.That(File.ReadAllBytes(_target), Is.EqualTo(before));
                Assert.That(File.ReadAllBytes(targetBookmarks), Is.EqualTo(originalBookmarks));
            }
            Assert.That((await Import(ProfileImportChoice.ReplaceExisting)).Status, Is.EqualTo(ProfileImportStatus.Imported));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void CancellationBeforeCommitRestoresBothOriginalByteSnapshots(int cancelAfterMember)
        {
            var before = CreateTarget();
            var marks = Path.Combine(Path.GetDirectoryName(_target), "bookmarks.xml");
            File.WriteAllText(marks, "<xml previous='keep'/>");
            var oldMarks = File.ReadAllBytes(marks);
            using (var cancellation = new CancellationTokenSource())
                Assert.Throws<OperationCanceledException>(() => ProfileBundleStore.Publish(_target,
                    XDocument.Parse("<xml><logviewer fontsize='18'/></xml>"), XDocument.Parse("<xml/>"), true,
                    cancellation.Token, index => { if (index == cancelAfterMember) cancellation.Cancel(); }));
            Assert.That(File.ReadAllBytes(_target), Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(marks), Is.EqualTo(oldMarks));
        }

        [Test]
        public void InterruptedBundleIsRolledBackBeforeReadingEitherMember()
        {
            var before = CreateTarget();
            var marks = Path.Combine(Path.GetDirectoryName(_target), "bookmarks.xml");
            File.WriteAllText(marks, "<xml previous='keep'/>");
            var oldMarks = File.ReadAllBytes(marks);
            CreateInterruptedBundle(marks);
            var restored = new ApplicationSettings(_target);
            restored.Restore();
            Assert.That(restored.LogViewer.FontSize, Is.EqualTo(27));
            Assert.That(File.ReadAllBytes(_target), Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(marks), Is.EqualTo(oldMarks));
            Assert.That(File.Exists(Path.Combine(Path.GetDirectoryName(_target), ".tailviewer-import.xml")), Is.False);
        }

        [Test]
        [TestCase("stage")]
        [TestCase("target")]
        [TestCase("hash")]
        [TestCase("duplicate")]
        public void InvalidRecoveryJournalDoesNotChangeEitherMember(string defect)
        {
            CreateTarget();
            var marks = Path.Combine(Path.GetDirectoryName(_target), "bookmarks.xml");
            File.WriteAllText(marks, "<xml previous='keep'/>");
            CreateInterruptedBundle(marks);
            var journalPath = Path.Combine(Path.GetDirectoryName(_target), ".tailviewer-import.xml");
            var journal = XDocument.Load(journalPath);
            if (defect == "stage") journal.Root.SetAttributeValue("stage", "../outside");
            if (defect == "target") journal.Root.Element("file").SetAttributeValue("name", "../settings.xml");
            if (defect == "hash") journal.Root.Element("file").SetAttributeValue("after", "invalid");
            if (defect == "duplicate") journal.Root.Add(new XElement(journal.Root.Element("file")));
            journal.Save(journalPath);
            var before = File.ReadAllBytes(_target);
            var beforeMarks = File.ReadAllBytes(marks);
            Assert.Throws<System.Xml.XmlException>(() => ProfileStore.Read(_target));
            Assert.That(File.ReadAllBytes(_target), Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(marks), Is.EqualTo(beforeMarks));
        }

        [Test]
        public void CorruptUndoCopyBlocksRecoveryWithoutChangingEitherMember()
        {
            CreateTarget();
            var marks = Path.Combine(Path.GetDirectoryName(_target), "bookmarks.xml");
            File.WriteAllText(marks, "<xml previous='keep'/>");
            var stage = CreateInterruptedBundle(marks);
            File.WriteAllText(Path.Combine(stage, "1.before"), "corrupt");
            var current = File.ReadAllBytes(_target);
            var currentMarks = File.ReadAllBytes(marks);
            Assert.Throws<IOException>(() => ProfileStore.Read(_target));
            Assert.That(File.ReadAllBytes(_target), Is.EqualTo(current));
            Assert.That(File.ReadAllBytes(marks), Is.EqualTo(currentMarks));
        }

        private string CreateInterruptedBundle(string marks)
        {
            var folder = Path.GetDirectoryName(_target);
            var stageName = "." + Guid.NewGuid().ToString("N") + ".import";
            var stage = Path.Combine(folder, stageName);
            Directory.CreateDirectory(stage);
            File.Copy(_target, Path.Combine(stage, "0.before"));
            File.Copy(marks, Path.Combine(stage, "1.before"));
            var settingsBefore = Hash(File.ReadAllBytes(_target));
            var bookmarksBefore = Hash(File.ReadAllBytes(marks));
            File.WriteAllText(_target, "<xml><logviewer fontsize='18'/></xml>");
            new XDocument(new XElement("xml", new XAttribute("stage", stageName),
                new XElement("file", new XAttribute("name", Path.GetFileName(_target)),
                    new XAttribute("before", settingsBefore), new XAttribute("after", Hash(File.ReadAllBytes(_target)))),
                new XElement("file", new XAttribute("name", "bookmarks.xml"),
                    new XAttribute("before", bookmarksBefore), new XAttribute("after", Hash(File.ReadAllBytes(marks))))))
                .Save(Path.Combine(folder, ".tailviewer-import.xml"));
            return stage;
        }

        [Test]
        public async Task DependentBookmarksAreBackedUpAndRepeatedImportsDoNotDuplicateThem()
        {
            var sourceBookmarks = Path.Combine(Path.GetDirectoryName(_source), "bookmarks.xml");
            File.WriteAllText(sourceBookmarks, "<?future keep?><xml future='keep'><bookmark datasource='" + SourceId + "' index='17' label='keep'/></xml>");
            var original = File.ReadAllBytes(sourceBookmarks);
            var first = await Import();
            Assert.That(first.Status, Is.EqualTo(ProfileImportStatus.Imported));
            var backup = Path.Combine(Path.GetDirectoryName(first.BackupFile), "bookmarks.xml");
            Assert.That(Hash(File.ReadAllBytes(backup)), Is.EqualTo(Hash(original)));
            Assert.That((await Import(ProfileImportChoice.ReplaceExisting)).Status, Is.EqualTo(ProfileImportStatus.Imported));
            var path = Path.Combine(Path.GetDirectoryName(_target), "bookmarks.xml");
            for (var restart = 0; restart < 2; ++restart)
            {
                var marks = new BookmarkCollection(path);
                marks.Restore();
                Assert.That(marks.Count, Is.EqualTo(1));
                Assert.That(marks[0].DataSourceId.Value.ToString(), Is.EqualTo(SourceId));
                Assert.That(marks.Save(), Is.True);
                var persisted = XDocument.Load(path);
                Assert.That((string)persisted.Root.Attribute("future"), Is.EqualTo("keep"));
                Assert.That((string)persisted.Root.Element("bookmark").Attribute("label"), Is.EqualTo("keep"));
                Assert.That(persisted.Nodes().OfType<XProcessingInstruction>().Single().Data, Is.EqualTo("keep"));
            }
            Assert.That(File.ReadAllBytes(sourceBookmarks), Is.EqualTo(original));
        }

        private Task<ProfileImportResult> Import(ProfileImportChoice choice = ProfileImportChoice.RequireChoice,
            CancellationToken cancellation = default) => _importer.ImportAsync(_source, _target, _backups, choice, cancellation);
        private byte[] CreateTarget()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_target));
            File.WriteAllText(_target, "<xml current='keep'><logviewer fontsize='27'/></xml>");
            return File.ReadAllBytes(_target);
        }
        private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data));
    }
}
#endif
