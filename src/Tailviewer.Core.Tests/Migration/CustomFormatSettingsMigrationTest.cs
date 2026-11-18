#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;
using Tailviewer.Settings.CustomFormats;

namespace Tailviewer.Core.Tests.Migration
{
    [TestFixture]
    public sealed class CustomFormatSettingsMigrationTest
    {
        [Test]
        public void PluginIdentityIsPlainDataAndKnownValuesRoundTrip()
        {
            var format = new CustomLogFileFormat("missing.plugin", "custom", "{message}", Encoding.UTF8);
            var xml = Save("customformat", format.Save);
            var restored = new CustomLogFileFormat();
            Restore(xml, restored.Restore);
            Assert.That(restored.PluginId, Is.EqualTo("missing.plugin"));
            Assert.That(restored.Name, Is.EqualTo("custom"));
            Assert.That(restored.Format, Is.EqualTo("{message}"));
            Assert.That(restored.Encoding, Is.EqualTo(Encoding.UTF8));
            Assert.That(typeof(CustomLogFileFormat).GetProperty("PluginId").PropertyType, Is.EqualTo(typeof(string)));
            Assert.That(Array.Exists(typeof(CustomLogFileFormat).Assembly.GetReferencedAssemblies(), a => a.Name == "archive"), Is.False);
            Assert.That(restored, Is.Not.EqualTo(new CustomLogFileFormat("Missing.Plugin", "custom", "{message}", Encoding.UTF8)));
        }

        [Test]
        public void MissingPluginAndOpaqueXmlSurviveTwoRoundTrips()
        {
            var xml = XElement.Parse("<customformat xmlns:p='urn:plugin' plugin='missing.plugin' name='custom' format='{message}' encoding='utf-8' p:version='7'><p:opaque><![CDATA[a < b]]></p:opaque></customformat>");
            for (var i = 0; i < 2; ++i)
            {
                var format = new CustomLogFileFormat();
                Restore(xml, format.Restore);
                xml = Save("customformat", format.Save);
                Assert.That((string)xml.Attribute(XName.Get("version", "urn:plugin")), Is.EqualTo("7"));
                Assert.That((string)xml.Element(XName.Get("opaque", "urn:plugin")), Is.EqualTo("a < b"));
                Assert.That(format.PluginId, Is.EqualTo("missing.plugin"));
            }
        }

        [Test]
        public void UnavailableEncodingKeepsItsOriginalWireName()
        {
            var format = new CustomLogFileFormat();
            Restore(XElement.Parse("<customformat plugin='missing.plugin' encoding='future-encoding'/>") , format.Restore);
            Assert.That(format.Encoding, Is.Null);
            var other = new CustomLogFileFormat();
            Restore(XElement.Parse("<customformat plugin='missing.plugin' encoding='other-future-encoding'/>") , other.Restore);
            Assert.That(format, Is.Not.EqualTo(other));
            Assert.That((string)Save("customformat", format.Save).Attribute("encoding"), Is.EqualTo("future-encoding"));
        }

        [Test]
        public void ContainerDoesNotActivateKnownNamesInsideOpaqueExtensions()
        {
            var formats = new CustomFormatsSettings();
            Restore(XElement.Parse("<customformats><future><customformat plugin='do-not-activate' name='hidden'/></future><customformat plugin='missing.plugin' name='visible'/></customformats>"), formats.Restore);
            Assert.That(formats.Count, Is.EqualTo(1));
            Assert.That(formats[0].Name, Is.EqualTo("visible"));
        }

        [Test]
        public void ContainerCloneDoesNotShareMutableFormatModels()
        {
            var formats = new CustomFormatsSettings { new CustomLogFileFormat("missing.plugin", "original", "{message}", null) };
            var clone = formats.Clone();
            Restore(XElement.Parse("<customformat plugin='missing.plugin' name='changed'/>") , clone[0].Restore);
            Assert.That(formats[0].Name, Is.EqualTo("original"));
        }

        [Test]
        public void ClearingKnownFormatsKeepsContainerExtensionData()
        {
            var formats = new CustomFormatsSettings();
            Restore(XElement.Parse("<customformats future='keep'><opaque value='keep'/><customformat plugin='missing.plugin' name='visible'/></customformats>"), formats.Restore);
            formats.Clear();
            var xml = Save("customformats", formats.Save);
            Assert.That(xml.Elements("customformat"), Is.Empty);
            Assert.That((string)xml.Attribute("future"), Is.EqualTo("keep"));
            Assert.That((string)xml.Element("opaque").Attribute("value"), Is.EqualTo("keep"));
        }

        [Test]
        public void NonemptyContainerPreservesOrderAndExtensionsWhenDefinitionIsEdited()
        {
            var formats = new CustomFormatsSettings();
            Restore(XElement.Parse("<customformats xmlns:p='urn:plugin'><customformat plugin='first' name='before' p:extra='keep'><opaque type='p:Schema'/></customformat><future/><customformat plugin='second' name='other'/></customformats>"), formats.Restore);
            var original = formats[0];
            formats[0] = original.WithDefinition("after", "{changed}", Encoding.Unicode);
            var saved = Save("customformats", formats.Save);
            var restored = new CustomFormatsSettings();
            Restore(saved, restored.Restore);
            Assert.That(restored.Count, Is.EqualTo(2));
            Assert.That(restored[0].PluginId, Is.EqualTo("first"));
            Assert.That(restored[0].Name, Is.EqualTo("after"));
            Assert.That(restored[0].Format, Is.EqualTo("{changed}"));
            Assert.That(restored[0].Encoding, Is.EqualTo(Encoding.Unicode));
            Assert.That(restored[1].PluginId, Is.EqualTo("second"));
            Assert.That(original.Name, Is.EqualTo("before"));
            var child = saved.Element("customformat");
            Assert.That((string)child.Attribute(XName.Get("extra", "urn:plugin")), Is.EqualTo("keep"));
            Assert.That(saved.Element("future"), Is.Not.Null);
            var detached = Save("customformat", restored[0].Save);
            Assert.That(detached.GetNamespaceOfPrefix("p")?.NamespaceName, Is.EqualTo("urn:plugin"));
            Assert.That((string)detached.Element("opaque").Attribute("type"), Is.EqualTo("p:Schema"));
        }

        [Test]
        public void DefinitionEditsRetainUnavailableEncodingUntilExplicitSelection()
        {
            var format = new CustomLogFileFormat();
            Restore(XElement.Parse("<customformat plugin='missing.plugin' encoding='future-encoding'/>") , format.Restore);
            var edited = format.WithDefinition("renamed", "{changed}");
            Assert.That((string)Save("customformat", edited.Save).Attribute("encoding"), Is.EqualTo("future-encoding"));
            Assert.That((string)Save("customformat", edited.WithDefinition("renamed", "{changed}", null).Save).Attribute("encoding"), Is.EqualTo(string.Empty));
            Assert.That(format.EncodingName, Is.EqualTo("future-encoding"));
        }

        [Test]
        public void LegacyWindowsCodePageIsResolvedWithoutDiscardingItsSelection()
        {
            var format = new CustomLogFileFormat();
            Restore(XElement.Parse("<customformat plugin='missing.plugin' encoding='windows-1252'/>") , format.Restore);
            Assert.That(format.Encoding?.CodePage, Is.EqualTo(1252));
            var logSettings = new Tailviewer.Settings.LogFileSettings();
            Restore(XElement.Parse("<logfile defaultencoding='windows-1252'/>") , logSettings.Restore);
            Assert.That(logSettings.DefaultEncoding?.CodePage, Is.EqualTo(1252));
        }

        private static void Restore(XElement element, Action<XmlReader> restore)
        {
            using var reader = element.CreateReader();
            reader.MoveToContent();
            restore(reader);
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
