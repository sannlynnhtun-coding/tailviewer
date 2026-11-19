#if NET10_0_OR_GREATER && PROFILE_IMPORT_CONTRACTS
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

        [Test]
        public async Task RepeatedImportAssignsStableIdentitiesToLegacySourcesWithoutIds()
        {
            var xml = XDocument.Load(_source);
            xml.Root.Element("datasources").Element("datasource").Attribute("id").Remove();
            xml.Save(_source);
            Assert.That((await Import()).Status, Is.EqualTo(ProfileImportStatus.Imported));
            var first = new ApplicationSettings(_target);
            first.Restore();
            Assert.That((await Import(ProfileImportChoice.ReplaceExisting)).Status, Is.EqualTo(ProfileImportStatus.Imported));
            var second = new ApplicationSettings(_target);
            second.Restore();
            Assert.That(second.DataSources.Single().Id, Is.EqualTo(first.DataSources.Single().Id));
        }

        [Test]
        public async Task DependentBookmarksAreBackedUpAndRepeatedImportsDoNotDuplicateThem()
        {
            var sourceBookmarks = Path.Combine(Path.GetDirectoryName(_source), "bookmarks.xml");
            File.WriteAllText(sourceBookmarks, "<xml future='keep'><bookmark datasource='" + SourceId + "' index='17' label='keep'/></xml>");
            var original = File.ReadAllBytes(sourceBookmarks);
            var first = await Import();
            Assert.That(first.Status, Is.EqualTo(ProfileImportStatus.Imported));
            var backup = Path.Combine(Path.GetDirectoryName(first.BackupFile), "bookmarks.xml");
            Assert.That(Hash(File.ReadAllBytes(backup)), Is.EqualTo(Hash(original)));
            Assert.That((await Import(ProfileImportChoice.ReplaceExisting)).Status, Is.EqualTo(ProfileImportStatus.Imported));
            var marks = new BookmarkCollection(Path.Combine(Path.GetDirectoryName(_target), "bookmarks.xml"));
            marks.Restore();
            Assert.That(marks.Count, Is.EqualTo(1));
            Assert.That(marks[0].DataSourceId.Value.ToString(), Is.EqualTo(SourceId));
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
