#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;
using Tailviewer.Settings.Dashboard;

namespace Tailviewer.Core.Tests.Migration
{
    [TestFixture]
    public sealed class DashboardSettingsMigrationTest
    {
        [Test]
        public void OpaqueWidgetAndLayoutMetadataSurviveTwoRoundTrips()
        {
            var xml = "<layout xmlns:p='urn:dashboard' future='keep'><widget kind='p:future'><p:config><![CDATA[a < b]]></p:config></widget>" +
                "<future><widget kind='nested'/></future></layout>";
            for (var i = 0; i < 2; ++i)
            {
                var layout = new LayoutSettings();
                Read(layout, xml);
                Assert.That(layout.Count, Is.EqualTo(1));
                var saved = Save(layout);
                Assert.That((string)saved.Attribute("future"), Is.EqualTo("keep"));
                Assert.That((string)saved.Element("widget").Attribute("kind"), Is.EqualTo("p:future"));
                Assert.That(saved.GetNamespaceOfPrefix("p").NamespaceName, Is.EqualTo("urn:dashboard"));
                Assert.That(saved.DescendantNodes().OfType<XCData>().Single().Value, Is.EqualTo("a < b"));
                Assert.That(saved.Element("future").Element("widget"), Is.Not.Null);
                xml = saved.ToString();
            }
        }

        [Test]
        public void ClearingKnownWidgetsRetainsOtherLayoutData()
        {
            var layout = new LayoutSettings();
            Read(layout, "<layout future='keep'><widget/><opaque/></layout>");
            layout.Clear();
            var saved = Save(layout);
            Assert.That(saved.Elements("widget"), Is.Empty);
            Assert.That(saved.Element("opaque"), Is.Not.Null);
            Assert.That((string)saved.Attribute("future"), Is.EqualTo("keep"));
        }

        [Test]
        public void FailedRestoreKeepsTheLastLayoutAndOpaqueMetadata()
        {
            var layout = new LayoutSettings();
            Read(layout, "<layout future='keep'><widget kind='keep'/></layout>");
            var widget = layout[0];
            var before = Save(layout).ToString();
            Assert.Catch(() => Read(layout, "<layout><widget/><unfinished>"));
            Assert.That(layout[0], Is.SameAs(widget));
            Assert.That(Save(layout).ToString(), Is.EqualTo(before));
        }

        private static void Read(LayoutSettings layout, string xml)
        {
            using (var reader = XmlReader.Create(new StringReader(xml)))
            {
                reader.MoveToContent();
                layout.Restore(reader);
            }
        }

        private static XElement Save(LayoutSettings layout)
        {
            var document = new XDocument();
            using (var writer = document.CreateWriter())
            {
                writer.WriteStartElement("layout");
                layout.Save(writer);
                writer.WriteEndElement();
            }
            return document.Root;
        }
    }
}
#endif
