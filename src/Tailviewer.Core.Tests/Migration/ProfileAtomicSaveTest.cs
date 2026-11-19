#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Xml.Linq;
using NUnit.Framework;
using Tailviewer.Api;
using Tailviewer.Settings;
using BookmarkCollection = Tailviewer.Settings.Bookmarks.Bookmarks;

namespace Tailviewer.Core.Tests.Migration
{
    [TestFixture]
    public sealed class ProfileAtomicSaveTest
    {
        private string _folder;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "tailviewer-atomic-profile-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            _path = Path.Combine(_folder, "profile.xml");
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_folder, true);

        [TestCase(false)]
        [TestCase(true)]
        public void PublishingNewSnapshotKeepsExistingReaderCoherent(bool bookmarks)
        {
            const string before = "<xml><logviewer fontsize='17'/><bookmark datasource='11111111-1111-1111-1111-111111111111' index='17'/></xml>";
            File.WriteAllText(_path, before);
            var profile = new ApplicationSettings(_path);
            profile.Restore();
            profile.LogViewer.FontSize = 19;
            var marks = new BookmarkCollection(_path);
            marks.Restore();
            marks[0].Index = new LogLineIndex(19);
            Func<bool> save = bookmarks ? (Func<bool>)marks.Save : profile.Save;
            using (var oldReader = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                Assert.That(save(), Is.True);
                using (var text = new StreamReader(oldReader)) Assert.That(text.ReadToEnd(), Is.EqualTo(before));
            }
            var current = XDocument.Load(_path).Root;
            Assert.That((int)current.Element(bookmarks ? "bookmark" : "logviewer").Attribute(bookmarks ? "index" : "fontsize"), Is.EqualTo(19));
            Assert.That(Directory.GetFiles(_folder), Has.Length.EqualTo(1));
        }
    }
}
#endif
