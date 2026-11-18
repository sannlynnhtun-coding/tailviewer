#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;
using Tailviewer.Api;
using Tailviewer.Settings;
using Tailviewer.Settings.Bookmarks;

namespace Tailviewer.Core.Tests.Migration
{
    [TestFixture]
    public sealed class SourceSettingsMigrationTest
    {
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(42)]
        public void BookmarkRetainsOriginalSourceAndEntryIdentity(int index)
        {
            var source = new DataSourceId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
            var original = new BookmarkSettings(source, new LogLineIndex(index));
            var xml = Save("bookmark", original.Save);
            Assert.That((string)xml.Attribute("datasource"), Is.EqualTo(source.ToString()));
            Assert.That((int)xml.Attribute("index"), Is.EqualTo(index));
            var restored = new BookmarkSettings();
            using var reader = xml.CreateReader();
            reader.MoveToContent();
            restored.Restore(reader);
            Assert.That(restored, Is.EqualTo(original));
            var clone = restored.Clone();
            clone.Index = new LogLineIndex(100);
            Assert.That(restored.Index, Is.EqualTo(new LogLineIndex(index)));
        }

        [Test]
        [SetCulture("de-DE")]
        public void LegacyTicksAndInvariantOffsetSurviveLocalizedCulture()
        {
            var time = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            var original = new DataSource("missing.log") { Id = DataSourceId.CreateNew(), LastViewed = time, HorizontalOffset = 1.25 };
            var xml = Save("datasource", original.Save);
            Assert.That((string)xml.Attribute("lastviewed"), Is.EqualTo("637135310450000000"));
            Assert.That((string)xml.Attribute("horizontaloffset"), Is.EqualTo("1.25"));
            var restored = new DataSource();
            using var reader = xml.CreateReader();
            reader.MoveToContent();
            restored.Restore(reader, out var patched);
            Assert.That(patched, Is.False);
            Assert.That(restored.LastViewed.Ticks, Is.EqualTo(time.Ticks));
            Assert.That(restored.HorizontalOffset, Is.EqualTo(1.25));
        }

        [TestCase("True", true)]
        [TestCase("false", false)]
        [TestCase("1", false)]
        [TestCase("invalid", false)]
        public void LegacyBooleanReaderKeepsItsOriginalSemantics(string text, bool expected)
        {
            using var reader = XElement.Parse("<datasource followtail='" + text + "'/>").CreateReader();
            reader.MoveToContent();
            var restored = new DataSource();
            restored.Restore(reader, out _);
            Assert.That(restored.FollowTail, Is.EqualTo(expected));
        }

        [Test]
        public void SourceCloneRetainsPluginConfigurationWithoutSharingMutableData()
        {
            var original = new DataSource
            {
                CustomDataSourceId = new CustomDataSourceId("probe.source"),
                CustomDataSourceConfiguration = new Configuration { Value = "keep" },
                UserDefinedName = "custom source"
            };
            var clone = original.Clone();
            Assert.Multiple(() =>
            {
                Assert.That(clone.CustomDataSourceId, Is.EqualTo(original.CustomDataSourceId));
                Assert.That(clone.CustomDataSourceConfiguration, Is.TypeOf<Configuration>());
                Assert.That(clone.CustomDataSourceConfiguration, Is.Not.SameAs(original.CustomDataSourceConfiguration));
                Assert.That(clone.UserDefinedName, Is.EqualTo("custom source"));
            });
            ((Configuration)clone.CustomDataSourceConfiguration).Value = "changed";
            Assert.That(((Configuration)original.CustomDataSourceConfiguration).Value, Is.EqualTo("keep"));
        }

        private sealed class Configuration : ICustomDataSourceConfiguration
        {
            public string Value;
            public object Clone() => new Configuration { Value = Value };
            public void Restore(XmlReader reader) => Value = reader.GetAttribute("value");
            public void Store(XmlWriter writer) => writer.WriteAttributeString("value", Value);
        }

        private static XElement Save(string name, Action<XmlWriter> save)
        {
            using var text = new StringWriter();
            using (var writer = XmlWriter.Create(text))
            {
                writer.WriteStartElement(name);
                save(writer);
                writer.WriteEndElement();
            }
            return XElement.Parse(text.ToString());
        }
    }
}
#endif
