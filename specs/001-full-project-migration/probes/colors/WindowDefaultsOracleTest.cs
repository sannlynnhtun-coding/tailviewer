using System.IO;
using System.Threading;
using System.Xml;
using NUnit.Framework;
using System.Xml.Linq;
using Tailviewer.Settings;

[TestFixture]
public sealed class WindowDefaultsOracleTest
{
    [TestCase(System.Windows.WindowState.Normal)]
    [TestCase(System.Windows.WindowState.Maximized)]
    [TestCase(System.Windows.WindowState.Minimized)]
    public void WindowPlacementReadsAndWritesTheLegacyWireValues(System.Windows.WindowState state)
    {
        var original = new Metrolib.WindowSettings { Left = -120.5, Top = 42.25, Width = 900, Height = 600, State = state };
        using var text = new StringWriter();
        using (var writer = XmlWriter.Create(text))
        {
            writer.WriteStartElement("mainwindow");
            original.Save(writer);
            writer.WriteEndElement();
        }
        var modern = new MainWindowSettings();
        using (var reader = XElement.Parse(text.ToString()).CreateReader())
        {
            reader.MoveToContent();
            modern.Restore(reader);
        }
        Assert.That(modern.Left, Is.EqualTo(original.Left));
        Assert.That(modern.Top, Is.EqualTo(original.Top));
        Assert.That(modern.Width, Is.EqualTo(original.Width));
        Assert.That(modern.Height, Is.EqualTo(original.Height));
        Assert.That(modern.State.ToString(), Is.EqualTo(state.ToString()));
        using var saved = new StringWriter();
        using (var writer = XmlWriter.Create(saved))
        {
            writer.WriteStartElement("mainwindow");
            modern.Save(writer);
            writer.WriteEndElement();
        }
        var legacy = new Metrolib.WindowSettings();
        using (var reader = XElement.Parse(saved.ToString()).CreateReader())
        {
            reader.MoveToContent();
            legacy.Restore(reader);
        }
        Assert.That(legacy.Left, Is.EqualTo(original.Left));
        Assert.That(legacy.Top, Is.EqualTo(original.Top));
        Assert.That(legacy.Width, Is.EqualTo(original.Width));
        Assert.That(legacy.Height, Is.EqualTo(original.Height));
        Assert.That(legacy.State, Is.EqualTo(state));
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void RecordLegacyDefaultRestoreToWindow()
    {
        var window = new System.Windows.Window();
        new Metrolib.WindowSettings().RestoreTo(window);
        TestContext.Progress.WriteLine($"RESTORE Left={window.Left} Top={window.Top} Width={window.Width} Height={window.Height} State={window.WindowState}");
        window.Close();
    }

    [Test]
    public void RecordLegacyWindowDefaultsAndXml()
    {
        var settings = new Metrolib.WindowSettings();
        TestContext.Progress.WriteLine($"LEGACY Left={settings.Left} Top={settings.Top} Width={settings.Width} Height={settings.Height} State={settings.State}");
        using var text = new StringWriter();
        using (var writer = XmlWriter.Create(text))
        {
            writer.WriteStartElement("window");
            settings.Save(writer);
            writer.WriteEndElement();
        }
        TestContext.Progress.WriteLine(text.ToString());
        Assert.That(settings.State, Is.EqualTo(System.Windows.WindowState.Normal));
    }
}
