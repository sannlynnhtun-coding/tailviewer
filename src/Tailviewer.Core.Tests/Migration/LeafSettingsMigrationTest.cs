#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;
using Tailviewer.Settings;

namespace Tailviewer.Core.Tests.Migration
{
    [TestFixture]
    public sealed class LeafSettingsMigrationTest
    {
        [TestCase("A")]
        [TestCase(@"C:\logs & exports")]
        [TestCase("မြန်မာ")]
        public void ExportFolderKeepsLegacyAttributeAndClone(string folder)
        {
            var settings = new ExportSettings { ExportFolder = folder };
            var document = Save("export", settings.Save);
            Assert.That((string)document.Attribute("exportfolder"), Is.EqualTo(folder));
            var restored = new ExportSettings();
            Restore(document, restored.Restore);
            Assert.That(restored.ExportFolder, Is.EqualTo(folder));
            var clone = restored.Clone();
            clone.ExportFolder = "changed";
            Assert.That(restored.ExportFolder, Is.EqualTo(folder));
        }

        [TestCase("<export/>")]
        [TestCase("<export exportfolder=''/>")]
        public void MissingExportFolderUsesOriginalDocumentsFallback(string xml)
        {
            var settings = new ExportSettings();
            Assert.That(settings.ExportFolder, Is.Null);
            Restore(XElement.Parse(xml), settings.Restore);
            Assert.That(settings.ExportFolder, Is.EqualTo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Tailviewer", "Export")));
        }

        [TestCase("utf-8")]
        [TestCase("utf-16")]
        [TestCase("utf-32")]
        public void EncodingKeepsLegacyNameAndIndependentClone(string name)
        {
            var settings = new LogFileSettings { DefaultEncoding = Encoding.GetEncoding(name) };
            var element = Save("logfile", settings.Save);
            Assert.That((string)element.Attribute("defaultencoding"), Is.EqualTo(name));
            var restored = new LogFileSettings();
            Restore(element, restored.Restore);
            Assert.That(restored.DefaultEncoding.WebName, Is.EqualTo(name));
            var clone = restored.Clone();
            clone.DefaultEncoding = Encoding.ASCII;
            Assert.That(restored.DefaultEncoding.WebName, Is.EqualTo(name));
        }

        [Test]
        public void NullEncodingKeepsAutomaticDetection()
        {
            var settings = new LogFileSettings();
            Assert.That(settings.DefaultEncoding, Is.Null);
            var element = Save("logfile", settings.Save);
            Assert.That((string)element.Attribute("defaultencoding"), Is.EqualTo(string.Empty));
            Restore(element, settings.Restore);
            Assert.That(settings.DefaultEncoding, Is.Null);
        }

        [Test]
        public void InvalidEncodingDoesNotDiscardCurrentSelection()
        {
            var settings = new LogFileSettings { DefaultEncoding = Encoding.Unicode };
            Restore(XElement.Parse("<logfile defaultencoding='not-a-real-encoding'/>") , settings.Restore);
            Assert.That(settings.DefaultEncoding, Is.EqualTo(Encoding.Unicode));
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

        private static void Restore(XElement element, Action<XmlReader> restore)
        {
            using var reader = element.CreateReader();
            reader.MoveToContent();
            restore(reader);
        }
    }
}
#endif
