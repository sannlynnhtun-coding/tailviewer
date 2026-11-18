using System.IO;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;
using Tailviewer.Settings;

[TestFixture]
public sealed class ColorOracleTest
{
    [TestCase("#123")]
    [TestCase("#1234")]
    [TestCase("#123456")]
    [TestCase("#80123456")]
    [TestCase("Transparent")]
    [TestCase("red")]
    [TestCase("AliceBlue")]
    [TestCase("sc#0.5,0.5,0.003,1")]
    [TestCase("sc#1,0.2,0.0031308,0")]
    [TestCase("sc#0.5,0.5,0.5")]
    public void ModernChannelsMatchWpfAndRetainOriginalWireText(string text)
    {
        var expected = (Color)ColorConverter.ConvertFromString(text);
        using var reader = new XElement("level", new XAttribute("foregroundcolor", text)).CreateReader();
        reader.MoveToContent();
        var settings = new LogLevelSettings();
        settings.Restore(reader);
        Assert.That(settings.ForegroundColor.A, Is.EqualTo(expected.A));
        Assert.That(settings.ForegroundColor.R, Is.EqualTo(expected.R));
        Assert.That(settings.ForegroundColor.G, Is.EqualTo(expected.G));
        Assert.That(settings.ForegroundColor.B, Is.EqualTo(expected.B));
        using var output = new StringWriter();
        using (var writer = XmlWriter.Create(output))
        {
            writer.WriteStartElement("level");
            settings.Clone().Save(writer);
            writer.WriteEndElement();
        }
        Assert.That((string)XElement.Parse(output.ToString()).Attribute("foregroundcolor"), Is.EqualTo(text));
    }
}
