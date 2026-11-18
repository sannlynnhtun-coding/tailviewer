#if NET10_0_OR_GREATER
using System.Drawing;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;
using Tailviewer.Settings;

namespace Tailviewer.Core.Tests.Migration
{
    [TestFixture]
    public sealed class ColorSettingsMigrationTest
    {
        [TestCase("#GGG")]
        [TestCase("#12")]
        [TestCase("unknown-color")]
        [TestCase("sc#NaN,0,0")]
        [TestCase("sc#1,2")]
        [TestCase("ContextColor file:///unavailable-profile.icc 1,0,0,0")]
        public void UnsupportedColorKeepsSafeDefaultAndOriginalText(string value)
        {
            var settings = Restore(value);
            Assert.That(settings.ForegroundColor.ToArgb(), Is.EqualTo(Color.Black.ToArgb()));
            Assert.That((string)Save(settings.Clone()).Attribute("foregroundcolor"), Is.EqualTo(value));
        }

        [Test]
        public void UserColorChangeReplacesOnlyThatWireValue()
        {
            var settings = Restore("sc#0.5,0.5,0.003,1");
            settings.ForegroundColor = Color.FromArgb(128, 1, 2, 3);
            var saved = Save(settings);
            Assert.That((string)saved.Attribute("foregroundcolor"), Is.EqualTo("#80010203"));
            Assert.That((string)saved.Attribute("backgroundcolor"), Is.EqualTo("Transparent"));
            var restored = Restore((string)saved.Attribute("foregroundcolor"));
            Assert.That(restored.ForegroundColor.ToArgb(), Is.EqualTo(settings.ForegroundColor.ToArgb()));
        }

        private static LogLevelSettings Restore(string value)
        {
            using var reader = new XElement("level", new XAttribute("foregroundcolor", value), new XAttribute("backgroundcolor", "Transparent")).CreateReader();
            reader.MoveToContent();
            var settings = new LogLevelSettings();
            settings.Restore(reader);
            return settings;
        }

        private static XElement Save(LogLevelSettings settings)
        {
            using var text = new StringWriter();
            using (var writer = XmlWriter.Create(text))
            {
                writer.WriteStartElement("level");
                settings.Save(writer);
                writer.WriteEndElement();
            }
            return XElement.Parse(text.ToString());
        }
    }
}
#endif
