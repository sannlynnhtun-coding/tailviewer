using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using FluentAssertions;
using NUnit.Framework;

namespace Tailviewer.Core.Tests.Settings
{
    [TestFixture]
    public sealed class SettingsPersistenceTest
    {
        [TestCase("true", true)]
        [TestCase("TRUE", true)]
        [TestCase("false", false)]
        [TestCase("invalid", false)]
        public void TestLegacyQuickFilter(string boolean, bool expected)
        {
            var id = Guid.NewGuid();
            var xml = $"<filter id='{id}' type='SubstringFilter' value='မြန်မာ &amp; log' ignorecase='{boolean}' isinclude='true'/>";
            var settings = new QuickFilterSettings();
            using (var reader = XmlReader.Create(new StringReader(xml)))
            {
                reader.MoveToContent();
                settings.Restore(reader).Should().BeTrue("because the legacy filter has a valid ID");
            }

            settings.Id.Value.Should().Be(id, "because existing filter associations must survive migration");
            settings.IgnoreCase.Should().Be(expected, "because legacy booleans were case insensitive and defaulted to false");
            settings.IsInverted.Should().BeTrue("because the legacy isinclude attribute stores inversion");
            settings.Value.Should().Be("မြန်မာ & log", "because XML escaping and Unicode must survive migration");

            var restored = new QuickFilterSettings();
            using (var reader = XmlReader.Create(new StringReader(Save(settings.Save))))
            {
                reader.MoveToContent();
                restored.Restore(reader).Should().BeTrue();
            }
            restored.IsEquivalent(settings).Should().BeTrue("because saving must preserve filter behavior");
            restored.Id.Should().Be(settings.Id);
        }

        [Test]
        public void TestLegacyTimeFilterTicks()
        {
            var minimum = new DateTime(2021, 2, 17, 3, 49, 21);
            var maximum = minimum.AddTicks(1234567);
            var settings = new TimeFilterSettings();
            var xml = $"<filter mode='Interval' start='{minimum.Ticks}' end='{maximum.Ticks}'/>";
            using (var reader = XmlReader.Create(new StringReader(xml)))
            {
                reader.MoveToContent();
                settings.Restore(reader).Should().BeTrue();
            }

            settings.Minimum.Should().Be(minimum, "because old settings store DateTime ticks, not ISO timestamps");
            settings.Maximum.Should().Be(maximum, "because sub-millisecond precision must survive migration");
            var saved = Save(settings.Save);
            saved.Should().Contain("start=\"" + minimum.Ticks.ToString(CultureInfo.InvariantCulture) + "\"");
            saved.Should().Contain("end=\"" + maximum.Ticks.ToString(CultureInfo.InvariantCulture) + "\"");
        }

        [Test]
        public void TestInvalidTimeFilterStillThrows()
        {
            using (var reader = XmlReader.Create(new StringReader("<filter start='invalid'/>")))
            {
                reader.MoveToContent();
                Action restore = () => new TimeFilterSettings().Restore(reader);
                restore.Should().Throw<FormatException>("because invalid persisted ticks must not silently change the filter");
            }
        }

        private static string Save(Action<XmlWriter> save)
        {
            var text = new StringBuilder();
            using (var writer = XmlWriter.Create(text, new XmlWriterSettings { OmitXmlDeclaration = true }))
            {
                writer.WriteStartElement("filter");
                save(writer);
                writer.WriteEndElement();
            }
            return text.ToString();
        }
    }
}
