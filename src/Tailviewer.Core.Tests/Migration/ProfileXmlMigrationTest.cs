#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using NUnit.Framework;
using Tailviewer.Settings;

namespace Tailviewer.Core.Tests.Migration
{
    [TestFixture]
    public sealed class ProfileXmlMigrationTest
    {
        private string _folder;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "tailviewer-profile-xml-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            _path = Path.Combine(_folder, "settings.xml");
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_folder, true);

        [TestCase("<xml><logviewer fontsize='99'/><datasources><datasource id='bad'/></datasources></xml>")]
        [TestCase("<other><logviewer fontsize='99'/></other>")]
        [TestCase("<xml><logviewer fontsize='99'/><unfinished>")]
        [TestCase("<xml><datasources><datasource/></datasources><quickfilters><quickfilter id='bad'/></quickfilters></xml>")]
        [TestCase("<xml/><second/>")]
        [TestCase("<xml><logviewer fontsize='99'/><logviewer fontsize='7'/></xml>")]
        public void FailedRestoreKeepsSnapshotAndBlocksOverwriteUntilRecovery(string invalid)
        {
            const string valid = "<xml future='keep'><logviewer fontsize='18'/></xml>";
            File.WriteAllText(_path, valid);
            var settings = new ApplicationSettings(_path);
            settings.Restore();
            var viewer = settings.LogViewer;
            var sources = settings.DataSources;
            File.WriteAllText(_path, invalid);
            settings.Restore(out var patched);
            Assert.That(patched, Is.False);
            Assert.That(settings.LogViewer, Is.SameAs(viewer));
            Assert.That(settings.DataSources, Is.SameAs(sources));
            Assert.That(settings.LogViewer.FontSize, Is.EqualTo(18));
            Assert.That(settings.Save(), Is.False);
            Assert.That(settings.Clone().Save(), Is.False);
            var count = settings.NumSaved;
            settings.SaveAsync();
            Assert.That(settings.NumSaved, Is.EqualTo(count));
            Assert.That(File.ReadAllText(_path), Is.EqualTo(invalid));
            File.WriteAllText(_path, valid);
            settings.Restore();
            Assert.That(settings.Save(), Is.True);
        }

        [TestCase("mainwindow")]
        [TestCase("autoupdate")]
        [TestCase("export")]
        [TestCase("logviewer")]
        [TestCase("logfile")]
        public void ScalarSectionExtensionsSurviveCloneAndTwoRestarts(string name)
        {
            File.WriteAllText(_path, "<xml><" + name + " future='keep'><opaque/></" + name + "></xml>");
            for (var i = 0; i < 2; ++i)
            {
                var settings = new ApplicationSettings(_path);
                settings.Restore();
                var clone = settings.Clone();
                var previousSize = settings.LogViewer.FontSize;
                clone.LogViewer.FontSize = previousSize + 1;
                Assert.That(settings.LogViewer.FontSize, Is.EqualTo(previousSize));
                Assert.That(clone.Save(), Is.True);
                var section = XDocument.Load(_path).Root.Element(name);
                Assert.That((string)section.Attribute("future"), Is.EqualTo("keep"));
                Assert.That(section.Element("opaque"), Is.Not.Null);
            }
        }

        [Test]
        public void NestedLevelNamesRemainOpaqueAndLevelExtensionsSurvive()
        {
            File.WriteAllText(_path, "<xml><logviewer><info foregroundcolor='Blue' future='keep'><payload/></info>" +
                "<future><info foregroundcolor='Red'/></future></logviewer></xml>");
            var settings = new ApplicationSettings(_path);
            settings.Restore();
            Assert.That(settings.LogViewer.Info.ForegroundColor.ToArgb(), Is.EqualTo(System.Drawing.Color.Blue.ToArgb()));
            settings.LogViewer.FontSize = 19;
            Assert.That(settings.Save(), Is.True);
            var viewer = XDocument.Load(_path).Root.Element("logviewer");
            Assert.That((string)viewer.Element("info").Attribute("future"), Is.EqualTo("keep"));
            Assert.That(viewer.Element("info").Element("payload"), Is.Not.Null);
            Assert.That((string)viewer.Element("future").Element("info").Attribute("foregroundcolor"), Is.EqualTo("Red"));
        }

        [Test]
        public void DisabledSavesAndClonesDoNotWrite()
        {
            var settings = new ApplicationSettings(_path) { AllowSave = false };
            Assert.That(settings.Save(), Is.False);
            Assert.That(settings.Clone().AllowSave, Is.False);
            Assert.That(settings.Clone().Save(), Is.False);
            settings.SaveAsync();
            Assert.That(settings.NumSaved, Is.Zero);
            Assert.That(File.Exists(_path), Is.False);
        }

        [Test]
        public void AsyncSaveOwnsItsSnapshotAndPreservesDocumentNodes()
        {
            File.WriteAllText(_path, "<?profile future?><xml xmlns:p='urn:future' future='keep'><logviewer fontsize='18'/>" +
                "<future kind='p:rule'/></xml><!--after-->");
            var settings = new ApplicationSettings(_path);
            settings.Restore();
            settings.SaveAsync();
            settings.LogViewer.FontSize = 99;
            var pending = (Task)typeof(ApplicationSettings).GetField("_saveTask", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Assert.That(pending.Wait(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(settings.NumSaved, Is.EqualTo(1));
            var document = XDocument.Load(_path);
            Assert.That((int)document.Root.Element("logviewer").Attribute("fontsize"), Is.EqualTo(18));
            Assert.That((string)document.Root.Attribute("future"), Is.EqualTo("keep"));
            Assert.That(document.Nodes().OfType<XProcessingInstruction>().Single().Data, Is.EqualTo("future"));
            Assert.That(document.Nodes().OfType<XComment>().Single().Value, Is.EqualTo("after"));
            Assert.That(document.Root.GetNamespaceOfPrefix("p").NamespaceName, Is.EqualTo("urn:future"));
        }

        [Test]
        public void ClearingOwnedRecordsDoesNotResurrectRawRecords()
        {
            File.WriteAllText(_path, "<xml><datasources future='keep'><opaque/><datasource id='11111111-1111-1111-1111-111111111111'/></datasources>" +
                "<quickfilters future='filters'><quickfilter id='22222222-2222-2222-2222-222222222222' type='SubstringFilter'/>" +
                "<timefilter mode='Interval' start='17' end='19'/></quickfilters></xml>");
            var settings = new ApplicationSettings(_path);
            settings.Restore();
            settings.DataSources.Clear();
            settings.QuickFilters.Clear();
            settings.QuickFilters.TimeFilter.Minimum = null;
            settings.QuickFilters.TimeFilter.Maximum = null;
            Assert.That(settings.Save(), Is.True);
            var root = XDocument.Load(_path).Root;
            Assert.That(root.Element("datasources").Elements("datasource"), Is.Empty);
            Assert.That(root.Element("datasources").Element("opaque"), Is.Not.Null);
            Assert.That(root.Element("quickfilters").Elements("quickfilter"), Is.Empty);
            Assert.That(root.Element("quickfilters").Element("timefilter").Attribute("start"), Is.Null);
            Assert.That(root.Element("quickfilters").Element("timefilter").Attribute("end"), Is.Null);
        }
    }
}
#endif
