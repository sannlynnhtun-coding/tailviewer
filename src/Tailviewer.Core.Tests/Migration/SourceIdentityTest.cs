#if NET10_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;
using Tailviewer.Api;
using Tailviewer.Settings;

namespace Tailviewer.Core.Tests.Migration
{
    [TestFixture]
    public sealed class SourceIdentityTest
    {
        private static readonly DataSourceId Group = new DataSourceId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        private static readonly DataSourceId OtherGroup = new DataSourceId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        private static readonly DataSourceId Child = new DataSourceId(Guid.Parse("33333333-3333-3333-3333-333333333333"));

        [Test]
        public void MissingFilesAndUnknownConfigurationSurviveSnapshotAndReopen()
        {
            var settings = new DataSourceSettings();
            var xml = XElement.Parse("<datasources selecteditem='" + Child.Value + "' future='keep'><datasource id='" +
                Child.Value + "' file='unavailable/file.log' xmlns:p='urn:future'><p:plugin keep='yes'/></datasource></datasources>");
            using (var reader = xml.CreateReader())
            {
                reader.MoveToContent();
                settings.Restore(reader, out _);
            }
            var first = new SourceConfiguration(settings);
            var reopened = new SourceConfiguration(first.Snapshot());
            Assert.That(reopened.Snapshot().Single().Id, Is.EqualTo(Child));
            Assert.That(reopened.Snapshot().Single().File, Is.EqualTo("unavailable/file.log"));
            Assert.That(reopened.Snapshot().SelectedItem, Is.EqualTo(Child));
            var persisted = SettingsXml.SaveElement("datasources", reopened.Snapshot().Save);
            Assert.That((string)persisted.Attribute("future"), Is.EqualTo("keep"));
            Assert.That(persisted.Element("datasource").Element(XName.Get("plugin", "urn:future")), Is.Not.Null);
        }

        [Test]
        public void MembershipUsesIdentitiesDespiteDuplicateDisplayNames()
        {
            var settings = new DataSourceSettings
            {
                Source(Group), Source(OtherGroup), Source(Child, OtherGroup)
            };
            foreach (var source in settings) source.DisplayName = "same-name";
            var configuration = new SourceConfiguration(settings);
            Assert.That(configuration.GetMembers(Group), Is.Empty);
            Assert.That(configuration.GetMembers(OtherGroup).Select(source => source.Id), Is.EqualTo(new[] { Child }));
        }

        [Test]
        public void MembershipPreservesConfiguredOrderAndReturnsIndependentCopies()
        {
            var settings = new DataSourceSettings { Source(Group), Source(Child, Group), Source(OtherGroup, Group) };
            var configuration = new SourceConfiguration(settings);
            settings[1].ParentId = DataSourceId.Empty;
            var members = configuration.GetMembers(Group);
            Assert.That(members.Select(source => source.Id), Is.EqualTo(new[] { Child, OtherGroup }));
            members[0].Id = Group;
            Assert.That(configuration.GetMembers(Group).Select(source => source.Id), Is.EqualTo(new[] { Child, OtherGroup }));
        }

        [Test]
        public void MissingParentRetainsTheUnresolvedSource()
        {
            var configuration = new SourceConfiguration(new DataSourceSettings { Source(Child, Group) });
            Assert.That(configuration.Snapshot().Single().ParentId, Is.EqualTo(Group));
            Assert.That(configuration.GetMembers(Group).Single().Id, Is.EqualTo(Child));
        }

        [TestCase("duplicate")]
        [TestCase("empty")]
        [TestCase("self-cycle")]
        [TestCase("two-cycle")]
        public void InvalidIdentityGraphsAreRejectedBeforeActivation(string defect)
        {
            var settings = new DataSourceSettings { Source(Group), Source(Child, Group) };
            if (defect == "duplicate") settings.Add(Source(Child, Group));
            if (defect == "empty") settings[1].Id = DataSourceId.Empty;
            if (defect == "self-cycle") settings[0].ParentId = Group;
            if (defect == "two-cycle") settings[0].ParentId = Child;
            Assert.Throws<XmlException>(() => new SourceConfiguration(settings));
        }

        [Test]
        public async Task LateReadCannotPublishAfterSourceGenerationChanges()
        {
            var configuration = new SourceConfiguration(new DataSourceSettings { Source(Child) });
            var oldGeneration = configuration.GetGeneration(Child);
            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var published = new List<string>();
            async Task<bool> ReadAndPublish()
            {
                var text = await completion.Task;
                return configuration.TryPublish(Child, oldGeneration, () => published.Add(text));
            }
            var pending = ReadAndPublish();
            var current = configuration.Invalidate(Child);
            Assert.That(configuration.TryPublish(Child, current, () => published.Add("new")), Is.True);
            completion.SetResult("old");
            Assert.That(await pending, Is.False);
            Assert.That(published, Is.EqualTo(new[] { "new" }));
            Assert.That(configuration.Snapshot().Single().Id, Is.EqualTo(Child));
        }

        [Test]
        public void InvalidatingOneSourceDoesNotDiscardAnotherSourcesPendingRead()
        {
            var configuration = new SourceConfiguration(new DataSourceSettings { Source(Group), Source(Child) });
            var generation = configuration.GetGeneration(Child);
            configuration.Invalidate(Group);
            var published = false;
            Assert.That(configuration.TryPublish(Child, generation, () => published = true), Is.True);
            Assert.That(published, Is.True);
        }

        private static DataSource Source(DataSourceId id, DataSourceId parent = default) =>
            new DataSource { Id = id, ParentId = parent, File = "unavailable.log" };
    }
}
#endif
