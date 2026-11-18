#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using NUnit.Framework;
using Tailviewer.Api;
using BookmarkCollection = Tailviewer.Settings.Bookmarks.Bookmarks;

namespace Tailviewer.Core.Tests.Migration
{
    [TestFixture]
    public sealed class BookmarkPersistenceMigrationTest
    {
        private string _folder;
        private string _path;
        private const string Id = "11111111-1111-1111-1111-111111111111";

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "tailviewer-bookmark-migration-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            _path = Path.Combine(_folder, "bookmarks.xml");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.SetAttributes(_path, FileAttributes.Normal);
            Directory.Delete(_folder, true);
        }

        [Test]
        public void OpaqueDataAndMissingSourceIdentitySurviveCloneAndTwoRestarts()
        {
            File.WriteAllText(_path, "<xml xmlns:p='urn:bookmark' future='keep'><bookmark datasource='" + Id + "' index='17' p:note='keep'><p:opaque><![CDATA[a < b]]></p:opaque></bookmark><future><bookmark datasource='" + Id + "' index='999'/></future></xml>");
            for (var i = 0; i < 2; ++i)
            {
                var bookmarks = new BookmarkCollection(_path);
                bookmarks.Restore();
                Assert.That(bookmarks.Count, Is.EqualTo(1));
                Assert.That(bookmarks[0].DataSourceId, Is.EqualTo(new DataSourceId(Guid.Parse(Id))));
                var clone = bookmarks.Clone();
                clone[0].Index = new LogLineIndex(18 + i);
                Assert.That(bookmarks[0].Index, Is.EqualTo(new LogLineIndex(17 + i)));
                Assert.That(clone.Save(), Is.True);
                var xml = XDocument.Load(_path).Root;
                Assert.That((string)xml.Attribute("future"), Is.EqualTo("keep"));
                var record = xml.Element("bookmark");
                Assert.That((string)record.Attribute(XName.Get("note", "urn:bookmark")), Is.EqualTo("keep"));
                Assert.That((string)record.Element(XName.Get("opaque", "urn:bookmark")), Is.EqualTo("a < b"));
                Assert.That(xml.Element("future").Element("bookmark"), Is.Not.Null);
            }
        }

        [Test]
        public void ClearingKnownBookmarksKeepsContainerExtensions()
        {
            File.WriteAllText(_path, "<xml future='keep'><opaque/><bookmark datasource='" + Id + "' index='17'/></xml>");
            var bookmarks = new BookmarkCollection(_path);
            bookmarks.Restore();
            bookmarks.Clear();
            Assert.That(bookmarks.Save(), Is.True);
            var root = XDocument.Load(_path).Root;
            Assert.That(root.Elements("bookmark"), Is.Empty);
            Assert.That(root.Element("opaque"), Is.Not.Null);
            Assert.That((string)root.Attribute("future"), Is.EqualTo("keep"));
        }

        [Test]
        public void FailedRestoreDoesNotPermitOverwritingTheUnreadableSource()
        {
            File.WriteAllText(_path, "<xml><bookmark datasource='" + Id + "' index='17'/></xml>");
            var bookmarks = new BookmarkCollection(_path);
            bookmarks.Restore();
            File.WriteAllText(_path, "<xml><bookmark");
            var before = File.ReadAllBytes(_path);
            bookmarks.Restore();
            Assert.That(bookmarks.Count, Is.EqualTo(1));
            Assert.That(bookmarks.Save(), Is.False);
            Assert.That(bookmarks.Clone().Save(), Is.False);
            Assert.That(File.ReadAllBytes(_path), Is.EqualTo(before));
        }

        [TestCase("<other><bookmark/></other>")]
        [TestCase("<xml><bookmark/></xml><trailing/>")]
        public void InvalidDocumentRetainsStateAndSuccessfulRestoreUnlocksSaving(string invalid)
        {
            var valid = "<xml><bookmark datasource='" + Id + "' index='17'/></xml>";
            File.WriteAllText(_path, valid);
            var bookmarks = new BookmarkCollection(_path);
            bookmarks.Restore();
            File.WriteAllText(_path, invalid);
            bookmarks.Restore();
            Assert.That(bookmarks[0].Index, Is.EqualTo(new LogLineIndex(17)));
            Assert.That(bookmarks.Save(), Is.False);
            Assert.That(File.ReadAllText(_path), Is.EqualTo(invalid));
            File.WriteAllText(_path, valid);
            bookmarks.Restore();
            Assert.That(bookmarks.Save(), Is.True);
        }

        [Test]
        public void ReadOnlySaveKeepsExistingBytes()
        {
            File.WriteAllText(_path, "<xml><bookmark datasource='" + Id + "' index='17'/></xml>");
            var bookmarks = new BookmarkCollection(_path);
            bookmarks.Restore();
            var before = File.ReadAllBytes(_path);
            File.SetAttributes(_path, FileAttributes.ReadOnly);
            bookmarks.Clear();
            Assert.That(bookmarks.Save(), Is.False);
            Assert.That(File.ReadAllBytes(_path), Is.EqualTo(before));
        }

        [Test]
        public void RemovingAnEquivalentRecordKeepsTheUnselectedOpaqueSibling()
        {
            File.WriteAllText(_path, "<xml><bookmark datasource='" + Id + "' index='17' future='first'/><bookmark datasource='" + Id + "' index='17' future='second'/></xml>");
            var bookmarks = new BookmarkCollection(_path);
            bookmarks.Restore();
            var first = bookmarks[0];
            bookmarks.Remove(new[] { bookmarks[1] });
            Assert.That(bookmarks[0], Is.SameAs(first));
        }

        [Test]
        public void AsyncSaveUsesAnIndependentSnapshotWithOpaqueData()
        {
            File.WriteAllText(_path, "<xml future='keep'><bookmark datasource='" + Id + "' index='17'/></xml>");
            var bookmarks = new BookmarkCollection(_path);
            bookmarks.Restore();
            bookmarks.SaveAsync();
            bookmarks.Clear();
            var pending = (Task)typeof(BookmarkCollection).GetField("_saveTask", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Assert.That(pending.Wait(TimeSpan.FromSeconds(5)), Is.True);
            var root = XDocument.Load(_path).Root;
            Assert.That(root.Elements("bookmark").Count(), Is.EqualTo(1));
            Assert.That((string)root.Attribute("future"), Is.EqualTo("keep"));
        }

        [Test]
        public void DefaultModernPathDoesNotWriteIntoTheLegacyProfile()
        {
            var path = (string)typeof(BookmarkCollection).GetField("_fileName", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(BookmarkCollection.Create());
            var legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tailviewer", "bookmarks.xml");
            Assert.That(Path.GetFullPath(path), Is.Not.EqualTo(legacy).IgnoreCase);
        }
    }
}
#endif
