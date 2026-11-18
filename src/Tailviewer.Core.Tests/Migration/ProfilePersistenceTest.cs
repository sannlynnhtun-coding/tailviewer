#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using NUnit.Framework;
using Tailviewer.Settings;

namespace Tailviewer.Core.Tests.Migration
{
    [TestFixture]
    public sealed class ProfilePersistenceTest
    {
        private string _folder;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "profile-contract-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            _path = Path.Combine(_folder, "settings.xml");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.SetAttributes(_path, FileAttributes.Normal);
            Directory.Delete(_folder, true);
        }

        [Test]
        public void MissingProfileUsesExistingDefaultsWithoutCreatingFile()
        {
            var settings = new ApplicationSettings(_path);
            settings.Restore();
            Assert.Multiple(() =>
            {
                Assert.That(settings.LogViewer.FontSize, Is.EqualTo(12));
                Assert.That(settings.LogViewer.TabWidth, Is.EqualTo(4));
                Assert.That(settings.LogViewer.LinesScrolledPerWheelTick, Is.EqualTo(2));
                Assert.That(settings.LogFile.DefaultEncoding, Is.Null);
                Assert.That(settings.AutoUpdate.CheckForUpdates, Is.False);
                Assert.That(settings.AutoUpdate.AutomaticallyInstallUpdates, Is.False);
                Assert.That(settings.DataSources.Count, Is.Zero);
                Assert.That(settings.QuickFilters.Count, Is.Zero);
                Assert.That(File.Exists(_path), Is.False);
            });
        }

        [Test]
        public void InvalidKnownValuesRestoreExistingDefaults()
        {
            File.WriteAllText(_path, "<xml><logviewer fontsize='0' tabwidth='invalid' linesscrolledperwheeltick='-1'/></xml>");
            var settings = new ApplicationSettings(_path);
            settings.Restore();
            Assert.Multiple(() =>
            {
                Assert.That(settings.LogViewer.FontSize, Is.EqualTo(12));
                Assert.That(settings.LogViewer.TabWidth, Is.EqualTo(4));
                Assert.That(settings.LogViewer.LinesScrolledPerWheelTick, Is.EqualTo(2));
            });
        }

        [Test]
        public void UnknownAttributesAndPluginNodesSurviveTwoRestarts()
        {
            const string raw = "<futureplugin xmlns='urn:future' id='missing'><raw flag='keep'><![CDATA[a < b]]></raw></futureplugin>";
            File.WriteAllText(_path, "<xml future='keep'><logviewer fontsize='18' extra='keep'><futurelevel value='opaque'/></logviewer>" + raw + "</xml>");
            for (var restart = 0; restart < 2; ++restart)
            {
                var settings = new ApplicationSettings(_path);
                settings.Restore();
                Assert.That(settings.LogViewer.FontSize, Is.EqualTo(restart == 0 ? 18 : 19));
                settings.LogViewer.FontSize = 19 + restart;
                Assert.That(settings.Save(), Is.True);
                var document = XDocument.Load(_path);
                Assert.Multiple(() =>
                {
                    Assert.That((string)document.Root.Attribute("future"), Is.EqualTo("keep"));
                    Assert.That((string)document.Root.Element("logviewer").Attribute("extra"), Is.EqualTo("keep"));
                    Assert.That(document.Root.Element("logviewer").Element("futurelevel"), Is.Not.Null);
                    Assert.That(XNode.DeepEquals(document.Root.Element(XName.Get("futureplugin", "urn:future")), XElement.Parse(raw)), Is.True);
                });
            }
        }

        [Test]
        public void KnownNamesInsideUnknownNodesDoNotChangeSettings()
        {
            File.WriteAllText(_path, "<xml><future><logviewer fontsize='99'/></future></xml>");
            var settings = new ApplicationSettings(_path);
            settings.Restore();
            Assert.That(settings.LogViewer.FontSize, Is.EqualTo(12));
        }

        [Test]
        public void MalformedRestoreKeepsLastValidInMemoryValuesAndOriginalBytes()
        {
            File.WriteAllText(_path, "<xml><logviewer fontsize='18' tabwidth='8'/></xml>");
            var settings = new ApplicationSettings(_path);
            settings.Restore();
            File.WriteAllText(_path, "<xml><logviewer fontsize='42' tabwidth='2'/><unfinished>");
            var before = File.ReadAllBytes(_path);
            Assert.That(() => settings.Restore(), Throws.Nothing);
            Assert.Multiple(() =>
            {
                Assert.That(settings.LogViewer.FontSize, Is.EqualTo(18));
                Assert.That(settings.LogViewer.TabWidth, Is.EqualTo(8));
                Assert.That(File.ReadAllBytes(_path), Is.EqualTo(before));
            });
        }

        [Test]
        public void ReadOnlySaveReportsFailureAndPreservesLastValidProfile()
        {
            File.WriteAllText(_path, "<xml><logviewer fontsize='18'/></xml>");
            var before = File.ReadAllBytes(_path);
            File.SetAttributes(_path, FileAttributes.ReadOnly);
            var settings = new ApplicationSettings(_path);
            settings.LogViewer.FontSize = 42;
            Assert.That(settings.Save(), Is.False);
            Assert.That(File.ReadAllBytes(_path), Is.EqualTo(before));
        }

        [Test]
        public void FailedSaveAndAbandonedCandidateKeepLastValidProfile()
        {
            File.WriteAllText(_path, "<xml><logviewer fontsize='18'/></xml>");
            var before = File.ReadAllBytes(_path);
            File.WriteAllText(_path + ".tmp", "<xml><unfinished>");
            var settings = new ApplicationSettings(_path);
            settings.LogViewer.FontSize = 42;
            using (File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.That(settings.Save(), Is.False);
            Assert.That(File.ReadAllBytes(_path), Is.EqualTo(before));
            settings = new ApplicationSettings(_path);
            settings.Restore();
            Assert.That(settings.LogViewer.FontSize, Is.EqualTo(18));
        }

        [Test]
        public void DefaultModernProfileDoesNotTargetLegacySettings()
        {
            // Observe the selected path without writing into the actual user's profile.
            var field = typeof(ApplicationSettings).GetField("_fileName", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            var selected = Path.GetFullPath((string)field.GetValue(ApplicationSettings.Create()));
            var legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tailviewer", "settings.xml");
            Assert.That(selected, Is.Not.EqualTo(legacy).IgnoreCase);
        }
    }
}
#endif
