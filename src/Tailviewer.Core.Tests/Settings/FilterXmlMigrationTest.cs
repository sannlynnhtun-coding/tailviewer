#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;

namespace Tailviewer.Core.Tests.Settings
{
    [TestFixture]
    public sealed class FilterXmlMigrationTest
    {
        private const string First = "11111111-1111-1111-1111-111111111111";
        private const string Second = "22222222-2222-2222-2222-222222222222";
        private static string Record(string id, string extra = "") =>
            "<quickfilter id='" + id + "' type='SubstringFilter' value='log' " + extra + "/>";

        [Test]
        public void NestedKnownNamesRemainOpaque()
        {
            var settings = new QuickFiltersSettings();
            Restore(settings.Restore, "<quickfilters>" + Record(First) +
                "<timefilter mode='Interval' start='17'/><future>" + Record(Second) +
                "<timefilter mode='Interval' start='99'/></future></quickfilters>");
            Assert.That(settings.Count, Is.EqualTo(1));
            Assert.That(settings.TimeFilter.Minimum, Is.EqualTo(new DateTime(17)));
            var saved = Save("quickfilters", settings.Save);
            Assert.That(saved.Element("future").Element("quickfilter"), Is.Not.Null);
        }

        [Test]
        public void RecordMetadataSurvivesCloneEditsAndTwoRestarts()
        {
            var settings = new QuickFilterSettings();
            Restore(reader => { settings.Restore(reader); },
                "<quickfilter xmlns:p='urn:filter' id='" + First +
                "' type='SubstringFilter' value='before' p:kind='p:future'><p:data><![CDATA[a < b]]></p:data></quickfilter>");
            var clone = settings.Clone();
            clone.Value = "after";
            Assert.That(settings.Value, Is.EqualTo("before"));
            for (var i = 0; i < 2; ++i)
            {
                var saved = Save("quickfilter", clone.Save);
                Assert.That((string)saved.Attribute("id"), Is.EqualTo(First));
                Assert.That((string)saved.Attribute("value"), Is.EqualTo("after"));
                Assert.That((string)saved.Attribute(XName.Get("kind", "urn:filter")), Is.EqualTo("p:future"));
                Assert.That(saved.GetNamespaceOfPrefix("p").NamespaceName, Is.EqualTo("urn:filter"));
                Assert.That(saved.DescendantNodes().OfType<XCData>().Single().Value, Is.EqualTo("a < b"));
                clone = new QuickFilterSettings();
                Restore(reader => { clone.Restore(reader); }, saved.ToString());
            }
        }

        [Test]
        public void ClearingOptionalTimeBoundsDoesNotResurrectThem()
        {
            var settings = new TimeFilterSettings();
            Restore(reader => { settings.Restore(reader); },
                "<timefilter mode='Interval' start='17' end='19' future='keep'><opaque/></timefilter>");
            var clone = settings.Clone();
            clone.Minimum = null;
            clone.Maximum = null;
            Assert.That(settings.Minimum, Is.EqualTo(new DateTime(17)));
            var saved = Save("timefilter", clone.Save);
            Assert.That(saved.Attribute("start"), Is.Null);
            Assert.That(saved.Attribute("end"), Is.Null);
            Assert.That((string)saved.Attribute("future"), Is.EqualTo("keep"));
            Assert.That(saved.Element("opaque"), Is.Not.Null);
        }

        [Test]
        public void ClearingRecordsRetainsContainerAndTimeExtensions()
        {
            var settings = new QuickFiltersSettings();
            Restore(settings.Restore, "<quickfilters future='keep'><opaque/>" + Record(First) +
                "<timefilter future='time'><payload/></timefilter></quickfilters>");
            var clone = settings.Clone();
            clone.Clear();
            var saved = Save("quickfilters", clone.Save);
            Assert.That(settings.Count, Is.EqualTo(1));
            Assert.That(saved.Elements("quickfilter"), Is.Empty);
            Assert.That((string)saved.Attribute("future"), Is.EqualTo("keep"));
            Assert.That(saved.Element("opaque"), Is.Not.Null);
            Assert.That((string)saved.Element("timefilter").Attribute("future"), Is.EqualTo("time"));
            Assert.That(saved.Element("timefilter").Element("payload"), Is.Not.Null);
        }

        [Test]
        public void ReorderingRecordsMovesTheirOwnMetadata()
        {
            var settings = new QuickFiltersSettings();
            Restore(settings.Restore, "<quickfilters>" + Record(First, "future='first'") +
                Record(Second, "future='second'") + "</quickfilters>");
            settings.Reverse();
            var saved = Save("quickfilters", settings.Save).Elements("quickfilter").ToList();
            Assert.That((string)saved[0].Attribute("id"), Is.EqualTo(Second));
            Assert.That((string)saved[0].Attribute("future"), Is.EqualTo("second"));
            Assert.That((string)saved[1].Attribute("future"), Is.EqualTo("first"));
        }

        [TestCase("<timefilter mode='Interval' start='bad'/>")]
        [TestCase("<quickfilter id='bad' type='SubstringFilter'/>")]
        [TestCase("<quickfilter id='00000000-0000-0000-0000-000000000000' type='SubstringFilter'/>")]
        [TestCase("<quickfilter id='22222222-2222-2222-2222-222222222222' type='FutureFilter'/>")]
        public void FailedRestoreKeepsTheLastCollectionAndTimeObject(string invalid)
        {
            var settings = new QuickFiltersSettings();
            Restore(settings.Restore, "<quickfilters future='keep'>" + Record(First) +
                "<timefilter mode='Interval' start='17'/></quickfilters>");
            var originalRecord = settings[0];
            var originalTime = settings.TimeFilter;
            Assert.Catch(() => Restore(settings.Restore, "<quickfilters><timefilter start='99'/>" + invalid + "</quickfilters>"));
            Assert.That(settings[0], Is.SameAs(originalRecord));
            Assert.That(settings.TimeFilter, Is.SameAs(originalTime));
            Assert.That(originalTime.Minimum, Is.EqualTo(new DateTime(17)));
            Assert.That((string)Save("quickfilters", settings.Save).Attribute("future"), Is.EqualTo("keep"));
        }

        [Test]
        public void FailedLeafRestoresDoNotPartiallyMutateKnownOrOpaqueState()
        {
            var filter = new QuickFilterSettings { Value = "before" };
            Restore(reader => { filter.Restore(reader); },
                "<quickfilter id='" + First + "' value='before' future='keep'/>");
            var before = Save("quickfilter", filter.Save).ToString();
            Assert.Catch(() => Restore(reader => { filter.Restore(reader); },
                "<quickfilter value='changed' type='FutureFilter'/>"));
            Assert.That(Save("quickfilter", filter.Save).ToString(), Is.EqualTo(before));

            var time = new TimeFilterSettings();
            Restore(reader => { time.Restore(reader); }, "<timefilter mode='Interval' start='17' future='keep'/>");
            before = Save("timefilter", time.Save).ToString();
            Assert.Catch(() => Restore(reader => { time.Restore(reader); },
                "<timefilter mode='Everything' start='bad'/>"));
            Assert.That(Save("timefilter", time.Save).ToString(), Is.EqualTo(before));
        }

        [Test]
        public void SuccessfulRestoreRetainsTheTimeObjectUsedByCallers()
        {
            var settings = new QuickFiltersSettings();
            var time = settings.TimeFilter;
            Restore(settings.Restore, "<quickfilters><timefilter mode='Interval' start='17'/></quickfilters>");
            Assert.That(settings.TimeFilter, Is.SameAs(time));
            Assert.That(time.Minimum, Is.EqualTo(new DateTime(17)));
        }

        private static void Restore(Action<XmlReader> restore, string xml)
        {
            using (var reader = XmlReader.Create(new StringReader(xml)))
            {
                reader.MoveToContent();
                restore(reader);
            }
        }

        private static XElement Save(string name, Action<XmlWriter> save)
        {
            var document = new XDocument();
            using (var writer = document.CreateWriter())
            {
                writer.WriteStartElement(name);
                save(writer);
                writer.WriteEndElement();
            }
            return document.Root;
        }
    }
}
#endif
