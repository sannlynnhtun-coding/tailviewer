#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Tailviewer.Settings;
using BookmarkCollection = Tailviewer.Settings.Bookmarks.Bookmarks;

namespace Tailviewer.Core.Tests.Migration
{
    [TestFixture]
    public sealed class ProfileStoreBoundaryTest
    {
        private string _folder;
        private string _path;
        private const string Id = "11111111-1111-1111-1111-111111111111";

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "tailviewer-profile-boundary-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            _path = Path.Combine(_folder, "profile.xml");
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_folder, true);

        [TestCase(false, "size")]
        [TestCase(true, "size")]
        [TestCase(false, "depth")]
        [TestCase(true, "depth")]
        [TestCase(false, "dtd")]
        [TestCase(true, "dtd")]
        public void UnsafeInputKeepsTheLastValidModelAndBlocksSaving(bool bookmarks, string kind)
        {
            File.WriteAllText(_path, Document(17, ""));
            var profile = new ApplicationSettings(_path);
            var marks = new BookmarkCollection(_path);
            Action restore = bookmarks ? (Action)marks.Restore : profile.Restore;
            Func<bool> save = bookmarks ? (Func<bool>)marks.Save : profile.Save;
            restore();
            string payload;
            if (kind == "size") payload = "<opaque>" + new string('x', 16 * 1024 * 1024) + "</opaque>";
            else if (kind == "depth") payload = string.Concat(Enumerable.Repeat("<nested>", 100)) + string.Concat(Enumerable.Repeat("</nested>", 100));
            else payload = "<opaque>&external;</opaque>";
            var invalid = Document(99, payload);
            if (kind == "dtd")
            {
                var secret = Path.Combine(_folder, "external.txt");
                File.WriteAllText(secret, "external content must not be loaded");
                invalid = "<!DOCTYPE xml [<!ENTITY external SYSTEM '" + new Uri(secret).AbsoluteUri + "'>]>" + invalid;
            }
            File.WriteAllText(_path, invalid);
            restore();
            if (bookmarks) Assert.That(marks[0].Index.Value, Is.EqualTo(17));
            else Assert.That(profile.LogViewer.FontSize, Is.EqualTo(17));
            Assert.That(save(), Is.False);
            Assert.That(File.ReadAllText(_path), Is.EqualTo(invalid));
        }

        [Test]
        public void BookmarkFilePreservesDocumentLevelExtensions()
        {
            File.WriteAllText(_path, "<?future keep?>" + Document(17, "<opaque/>") + "<!--after-->");
            var marks = new BookmarkCollection(_path);
            marks.Restore();
            Assert.That(marks.Clone().Save(), Is.True);
            var document = XDocument.Load(_path);
            Assert.That(document.Nodes().OfType<XProcessingInstruction>().Single().Data, Is.EqualTo("keep"));
            Assert.That(document.Nodes().OfType<XComment>().Single().Value, Is.EqualTo("after"));
            Assert.That(document.Root.Element("opaque"), Is.Not.Null);
        }

        private static string Document(int value, string payload) => "<xml><logviewer fontsize='" + value +
            "'/><bookmark datasource='" + Id + "' index='" + value + "'/>" + payload + "</xml>";
    }
}
#endif
