#if NET10_0_OR_GREATER
using System.IO;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;
using Tailviewer.Settings;

namespace Tailviewer.Core.Tests.Migration
{
    [TestFixture]
    public sealed class WindowSettingsMigrationTest
    {
        [Test]
        public void OriginalDefaultsStayUnspecifiedWithoutInventingWindowDimensions()
        {
            var settings = new MainWindowSettings();
            Assert.That(settings.Left, Is.Zero);
            Assert.That(settings.Top, Is.Zero);
            Assert.That(settings.Width, Is.Zero);
            Assert.That(settings.Height, Is.Zero);
            Assert.That(settings.State, Is.EqualTo(WindowDisplayState.Normal));
            Assert.That(settings.IsLeftSidePanelVisible, Is.True);
            Assert.That(settings.AlwaysOnTop, Is.False);
        }

        [Test]
        [SetCulture("de-DE")]
        public void PlacementAndPanelsRoundTripWithLegacyNames()
        {
            var settings = new MainWindowSettings { Left = -120.5, Top = 42.25, Width = 900, Height = 600, State = WindowDisplayState.Maximized, SelectedMainPanel = "settings", SelectedSidePanel = "bookmarks", AlwaysOnTop = true, IsLeftSidePanelVisible = false };
            var saved = Save(settings);
            Assert.That((string)saved.Attribute("left"), Is.EqualTo("-120.5"));
            Assert.That((string)saved.Attribute("previouswindowstate"), Is.EqualTo("Normal"));
            var restored = Restore(saved);
            Assert.That(restored.Left, Is.EqualTo(-120.5));
            Assert.That(restored.Top, Is.EqualTo(42.25));
            Assert.That(restored.Width, Is.EqualTo(900));
            Assert.That(restored.Height, Is.EqualTo(600));
            Assert.That(restored.State, Is.EqualTo(WindowDisplayState.Maximized));
            Assert.That(restored.SelectedMainPanel, Is.EqualTo("settings"));
            Assert.That(restored.SelectedSidePanel, Is.EqualTo("bookmarks"));
            Assert.That(restored.AlwaysOnTop, Is.True);
            Assert.That(restored.IsLeftSidePanelVisible, Is.False);
        }

        [Test]
        public void CloneKeepsPreMinimizeStateThroughSaveAndRestore()
        {
            var settings = new MainWindowSettings { State = WindowDisplayState.Maximized, Width = 900, Height = 600 };
            settings.State = WindowDisplayState.Minimized;
            var clone = settings.Clone();
            Assert.That(clone.RestoredState, Is.EqualTo(WindowDisplayState.Maximized));
            Assert.That(Restore(Save(clone)).RestoredState, Is.EqualTo(WindowDisplayState.Maximized));
            clone.Width = 400;
            Assert.That(settings.Width, Is.EqualTo(900));
        }

        [Test]
        public void InvalidGeometryRestoresSafeDefaults()
        {
            var restored = Restore(XElement.Parse("<mainwindow width='-1' height='NaN' left='Infinity' top='invalid' state='99'/>"));
            Assert.That(restored.Width, Is.Zero);
            Assert.That(restored.Height, Is.Zero);
            Assert.That(restored.Left, Is.Zero);
            Assert.That(restored.Top, Is.Zero);
            Assert.That(restored.State, Is.EqualTo(WindowDisplayState.Normal));
        }

        [Test]
        public void StartupNeverRestoresAnUnusableMinimizedState()
        {
            var restored = Restore(XElement.Parse("<mainwindow state='Minimized' previouswindowstate='Minimized'/>"));
            Assert.That(restored.RestoredState, Is.EqualTo(WindowDisplayState.Normal));
        }

        [TestCase("99")]
        [TestCase("invalid")]
        public void InvalidPreviousStateNeverEscapesIntoWindowState(string previous)
        {
            var restored = Restore(new XElement("mainwindow", new XAttribute("state", "Minimized"), new XAttribute("previouswindowstate", previous)));
            Assert.That(restored.RestoredState, Is.EqualTo(WindowDisplayState.Normal));
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        public void NegativeWindowSizeIsRejected(double width, double height)
        {
            Assert.That(() => new WindowSize(width, height), Throws.InstanceOf<System.ArgumentException>());
        }

        [Test]
        public void PointBeyondRightEdgeIsOutside()
        {
            Assert.That(new Desktop.Rectangle(0, 0, 10, 10).Contains(new Desktop.Point(15, 5)), Is.False);
        }

        [Test]
        public void CrossingRectanglesOverlapEvenWithoutContainedCorners()
        {
            var horizontal = new Desktop.Rectangle(-10, -2, 20, 4);
            var vertical = new Desktop.Rectangle(-2, -10, 4, 20);
            Assert.That(horizontal.Overlap(vertical), Is.True);
            Assert.That(horizontal.Cut(vertical), Has.Count.EqualTo(2));
        }

        [Test]
        public void OversizedWindowUsesLargestAvailableWorkingArea()
        {
            var small = new Desktop.Screen(new Desktop.Point(0, 0), new WindowSize(100, 100));
            var large = new Desktop.Screen(new Desktop.Point(100, 0), new WindowSize(500, 500));
            var desktop = new Desktop(new[] { small, large });
            var fitted = desktop.ClipToBoundaries(new Desktop.Window(new Desktop.Point(0, 0), new WindowSize(1000, 1000)));
            Assert.That(fitted.Width, Is.EqualTo(500));
            Assert.That(fitted.Height, Is.EqualTo(500));
            Assert.That(fitted.Left, Is.EqualTo(100));
        }

        private static MainWindowSettings Restore(XElement element)
        {
            using var reader = element.CreateReader();
            reader.MoveToContent();
            var settings = new MainWindowSettings();
            settings.Restore(reader);
            return settings;
        }

        private static XElement Save(MainWindowSettings settings)
        {
            using var text = new StringWriter();
            using (var writer = XmlWriter.Create(text))
            {
                writer.WriteStartElement("mainwindow");
                settings.Save(writer);
                writer.WriteEndElement();
            }
            return XElement.Parse(text.ToString());
        }
    }
}
#endif
