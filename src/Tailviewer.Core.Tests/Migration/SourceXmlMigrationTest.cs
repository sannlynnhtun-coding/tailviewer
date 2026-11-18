#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;
using Tailviewer.Api;
using Tailviewer.Settings;

namespace Tailviewer.Core.Tests.Migration
{
    [TestFixture]
    public sealed class SourceXmlMigrationTest
    {
        private const string First = "11111111-1111-1111-1111-111111111111";
        private const string Second = "22222222-2222-2222-2222-222222222222";
        private const string Third = "33333333-3333-3333-3333-333333333333";
        private static DataSourceId Id(string value) => new DataSourceId(Guid.Parse(value));
        private static QuickFilterId FilterId(string value) => new QuickFilterId(Guid.Parse(value));
        private static string Source(string id, string extra = "") => "<datasource id='" + id + "' " + extra + "/>";

        [Test]
        public void OnlyDirectReferenceChildrenAreApplied()
        {
            var source = new DataSource();
            Read(source, "<datasource id='" + First + "'><activatedquickfilters><quickfilter id='" + Second +
                "'/><future><quickfilter id='" + Third + "'/></future></activatedquickfilters>" +
                "<excludeddatasources><datasource id='" + Second + "'/><future><datasource id='" + Third +
                "'/></future></excludeddatasources></datasource>");
            Assert.That(source.ActivatedQuickFilters, Is.EqualTo(new[] { FilterId(Second) }));
            Assert.That(source.ExcludedDataSources, Is.EquivalentTo(new[] { Id(Second) }));
        }

        [Test]
        public void RecordExtensionsAndLegacySearchAliasSurviveEditsAndTwoRestarts()
        {
            var source = new DataSource();
            Read(source, "<datasource xmlns:p='urn:source' id='" + First +
                "' searchterm='before' stringfilter='old' p:kind='p:future'><p:configuration><![CDATA[a < b]]></p:configuration></datasource>");
            var clone = source.Clone();
            clone.File = "missing.log";
            clone.SearchTerm = "after";
            Assert.That(source.File, Is.Null);
            for (var i = 0; i < 2; ++i)
            {
                var saved = Save("datasource", clone.Save);
                Assert.That((string)saved.Attribute("id"), Is.EqualTo(First));
                Assert.That((string)saved.Attribute("file"), Is.EqualTo("missing.log"));
                Assert.That(saved.Attribute("stringfilter"), Is.Null);
                Assert.That((string)saved.Attribute(XName.Get("kind", "urn:source")), Is.EqualTo("p:future"));
                Assert.That(saved.GetNamespaceOfPrefix("p").NamespaceName, Is.EqualTo("urn:source"));
                Assert.That(saved.DescendantNodes().OfType<XCData>().Single().Value, Is.EqualTo("a < b"));
                clone = new DataSource();
                Read(clone, saved.ToString());
                Assert.That(clone.SearchTerm, Is.EqualTo("after"));
            }
        }

        [Test]
        public void ReferenceMetadataFollowsIdentityAndClearKeepsOpaqueData()
        {
            var source = new DataSource();
            Read(source, "<datasource id='" + First + "'><activatedquickfilters future='keep'><quickfilter id='" + Second +
                "' future='second'/><opaque/><quickfilter id='" + Third + "' future='third'><payload/></quickfilter>" +
                "</activatedquickfilters><excludeddatasources future='excluded'><opaque/><datasource id='" + Second +
                "' future='reference'/></excludeddatasources></datasource>");
            source.ActivatedQuickFilters.Reverse();
            var saved = Save("datasource", source.Save);
            var filters = saved.Element("activatedquickfilters").Elements("quickfilter").ToList();
            Assert.That((string)filters[0].Attribute("id"), Is.EqualTo(Third));
            Assert.That((string)filters[0].Attribute("future"), Is.EqualTo("third"));
            Assert.That(filters[0].Element("payload"), Is.Not.Null);
            source.ActivatedQuickFilters.Clear();
            source.ExcludedDataSources.Clear();
            saved = Save("datasource", source.Save);
            Assert.That(saved.Element("activatedquickfilters").Elements("quickfilter"), Is.Empty);
            Assert.That((string)saved.Element("activatedquickfilters").Attribute("future"), Is.EqualTo("keep"));
            Assert.That(saved.Element("activatedquickfilters").Element("opaque"), Is.Not.Null);
            Assert.That(saved.Element("excludeddatasources").Elements("datasource"), Is.Empty);
            Assert.That((string)saved.Element("excludeddatasources").Attribute("future"), Is.EqualTo("excluded"));
            Assert.That(saved.Element("excludeddatasources").Element("opaque"), Is.Not.Null);
        }

        [Test]
        public void ContainerReorderAndClearKeepOnlySurvivingRecordMetadata()
        {
            var sources = new DataSourceSettings();
            Read(sources, "<datasources future='keep' selecteditem='" + Second + "'>" + Source(First, "future='first'") +
                "<future>" + Source(Third) + "</future>" + Source(Second, "future='second'") + "</datasources>");
            Assert.That(sources.Count, Is.EqualTo(2));
            Assert.That(sources.SelectedItem, Is.EqualTo(Id(Second)));
            var clone = sources.Clone();
            clone.MoveBefore(clone[1], clone[0]);
            var saved = Save("datasources", clone.Save);
            var records = saved.Elements("datasource").ToList();
            Assert.That((string)records[0].Attribute("future"), Is.EqualTo("second"));
            Assert.That((string)records[1].Attribute("future"), Is.EqualTo("first"));
            Assert.That(sources[0].Id, Is.EqualTo(Id(First)));
            clone.Clear();
            saved = Save("datasources", clone.Save);
            Assert.That(saved.Elements("datasource"), Is.Empty);
            Assert.That(saved.Element("future").Element("datasource"), Is.Not.Null);
            Assert.That((string)saved.Attribute("future"), Is.EqualTo("keep"));
        }

        [Test]
        public void OpaqueReferencesAndDuplicateContainerExtensionsAreRetained()
        {
            var source = new DataSource();
            Read(source, "<datasource id='" + First + "'><activatedquickfilters future='first'><opaque/></activatedquickfilters>" +
                "<activatedquickfilters future='second'><quickfilter id='" + Second + "'/><quickfilter future='no-id'/>" +
                "<p:quickfilter xmlns:p='urn:future' id='" + Third + "'/></activatedquickfilters>" +
                "<future><activatedquickfilters><quickfilter id='" + Third + "'/></activatedquickfilters></future></datasource>");
            Assert.That(source.ActivatedQuickFilters, Is.EqualTo(new[] { FilterId(Second) }));
            var saved = Save("datasource", source.Save);
            var containers = saved.Elements("activatedquickfilters").ToList();
            Assert.That(containers.Count, Is.EqualTo(2));
            Assert.That((string)containers[0].Attribute("future"), Is.EqualTo("first"));
            Assert.That(containers[0].Element("opaque"), Is.Not.Null);
            Assert.That((string)containers[1].Elements("quickfilter").Single(item => item.Attribute("id") == null).Attribute("future"), Is.EqualTo("no-id"));
            Assert.That(containers[1].Element(XName.Get("quickfilter", "urn:future")), Is.Not.Null);
            Assert.That(saved.Element("future").Element("activatedquickfilters"), Is.Not.Null);
        }

        [Test]
        public void SuccessfulRecordRestoreRetainsCollectionsHeldByCallers()
        {
            var source = new DataSource();
            var filters = source.ActivatedQuickFilters;
            var excluded = source.ExcludedDataSources;
            var selected = source.SelectedLogLines;
            Read(source, "<datasource id='" + First + "'><activatedquickfilters><quickfilter id='" + Second +
                "'/></activatedquickfilters><excludeddatasources><datasource id='" + Third + "'/></excludeddatasources></datasource>");
            Assert.That(source.ActivatedQuickFilters, Is.SameAs(filters));
            Assert.That(source.ExcludedDataSources, Is.SameAs(excluded));
            Assert.That(source.SelectedLogLines, Is.SameAs(selected));
            Assert.That(filters, Is.EqualTo(new[] { FilterId(Second) }));
            Assert.That(excluded, Is.EquivalentTo(new[] { Id(Third) }));
        }

        [TestCase("<datasource file='changed' id='bad'/>")]
        [TestCase("<datasource file='changed'><broken")]
        [TestCase("<datasource file='changed'><activatedquickfilters><quickfilter id='bad'/></activatedquickfilters></datasource>")]
        public void InvalidRecordRestoreLeavesAllLiveStateUnchanged(string invalid)
        {
            var source = new DataSource();
            Read(source, "<datasource id='" + First + "' file='before' future='keep'><activatedquickfilters><quickfilter id='" + Second + "'/></activatedquickfilters></datasource>");
            var list = source.ActivatedQuickFilters;
            var before = Save("datasource", source.Save).ToString();
            Assert.Catch(() => Read(source, invalid));
            Assert.That(source.ActivatedQuickFilters, Is.SameAs(list));
            Assert.That(Save("datasource", source.Save).ToString(), Is.EqualTo(before));
        }

        [Test]
        public void InvalidCollectionRestoreKeepsSelectionDefaultsAndRecords()
        {
            var sources = new DataSourceSettings();
            Read(sources, "<datasources future='keep' selecteditem='" + First +
                "' folderdatasourcepattern='before' folderdatasourcerecursive='false'>" + Source(First) + "</datasources>");
            var record = sources[0];
            var before = Save("datasources", sources.Save).ToString();
            Assert.Catch(() => Read(sources, "<datasources folderdatasourcepattern='changed'>" + Source(Second) + Source("bad") + "</datasources>"));
            Assert.That(sources[0], Is.SameAs(record));
            Assert.That(Save("datasources", sources.Save).ToString(), Is.EqualTo(before));
        }

        [Test]
        public void EmptyRestoreDoesNotRetainAStaleSelection()
        {
            var sources = new DataSourceSettings { SelectedItem = Id(First) };
            Read(sources, "<datasources/>");
            Assert.That(sources.SelectedItem, Is.EqualTo(DataSourceId.Empty));
            Assert.That(sources.FolderDataSourceRecursive, Is.True);
            Assert.That(sources.FolderDataSourcePattern, Is.EqualTo("*.txt;*.log"));
        }

        [Test]
        public void MissingSourceIdentityIsPatchedOnceAndRetainedOnRestart()
        {
            var sources = new DataSourceSettings();
            Assert.That(Read(sources, "<datasources><datasource file='missing.log' future='keep'/></datasources>"), Is.True);
            var id = sources[0].Id;
            Assert.That(id, Is.Not.EqualTo(DataSourceId.Empty));
            var saved = Save("datasources", sources.Save);
            Assert.That((string)saved.Element("datasource").Attribute("future"), Is.EqualTo("keep"));
            Assert.That(Read(sources, saved.ToString()), Is.False);
            Assert.That(sources[0].Id, Is.EqualTo(id));
        }

        [Test]
        public void NewSourceWritesOnlyOneActivatedFilterContainer()
        {
            var source = new DataSource { Id = Id(First) };
            source.ActivatedQuickFilters.Add(FilterId(Second));
            var saved = Save("datasource", source.Save);
            Assert.That(saved.Elements("activatedquickfilters").Count(), Is.EqualTo(1));
        }

        private static bool Read(DataSource source, string xml)
        {
            using (var reader = XmlReader.Create(new StringReader(xml)))
            {
                reader.MoveToContent();
                source.Restore(reader, out var patched);
                return patched;
            }
        }

        private static bool Read(DataSourceSettings sources, string xml)
        {
            using (var reader = XmlReader.Create(new StringReader(xml)))
            {
                reader.MoveToContent();
                sources.Restore(reader, out var patched);
                return patched;
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
