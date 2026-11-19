#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using NUnit.Framework;
using Tailviewer.Settings;

namespace Tailviewer.Core.Tests.Migration
{
    [TestFixture]
    public sealed class ProfileStoreWriteTest
    {
        private string _folder;
        private string _path;
        private string _foreign;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "tailviewer-store-write-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            _path = Path.Combine(_folder, "profile.xml");
            _foreign = Path.Combine(_folder, ".profile.xml.abandoned.tmp");
            File.WriteAllText(_path, "<xml value='old'/>");
            File.WriteAllText(_foreign, "<xml><unfinished>");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.SetAttributes(_path, FileAttributes.Normal);
            Directory.Delete(_folder, true);
        }

        [TestCase("serialize")]
        [TestCase("validate")]
        [TestCase("syntax")]
        [TestCase("root")]
        [TestCase("size")]
        [TestCase("depth")]
        [TestCase("dtd")]
        public void RejectedCandidateNeverChangesTheDestinationOrForeignTempFile(string failure)
        {
            var before = File.ReadAllBytes(_path);
            var validated = false;
            Assert.Catch(() => ProfileStore.Write(_path, writer =>
            {
                if (failure == "syntax") { writer.WriteRaw("<xml><unfinished>"); return; }
                if (failure == "depth")
                {
                    writer.WriteRaw("<xml>" + string.Concat(Enumerable.Repeat("<nested>", 100)) +
                        string.Concat(Enumerable.Repeat("</nested>", 100)) + "</xml>");
                    return;
                }
                if (failure == "dtd") writer.WriteDocType("xml", null, null, "<!ELEMENT xml ANY>");
                writer.WriteStartElement(failure == "root" ? "other" : "xml");
                writer.WriteAttributeString("value", "new");
                if (failure == "serialize") throw new IOException("Simulated candidate write failure");
                if (failure == "size") writer.WriteString(new string('x', ProfileStore.MaxFileBytes));
                writer.WriteEndElement();
            }, document =>
            {
                validated = true;
                var candidate = Directory.GetFiles(_folder, ".profile.xml.*.tmp").Single(path => path != _foreign);
                Assert.That(Path.GetDirectoryName(candidate), Is.EqualTo(_folder));
                Assert.That((string)ProfileStore.Read(candidate).Root.Attribute("value"), Is.EqualTo("new"));
                Assert.That(File.ReadAllBytes(_path), Is.EqualTo(before));
                if (failure == "validate") throw new InvalidDataException("Simulated semantic validation failure");
            }));
            Assert.That(validated, Is.EqualTo(failure == "validate"));
            Assert.That(File.ReadAllBytes(_path), Is.EqualTo(before));
            Assert.That(File.ReadAllText(_foreign), Is.EqualTo("<xml><unfinished>"));
            Assert.That(Directory.GetFiles(_folder).OrderBy(path => path), Is.EqualTo(new[] { _path, _foreign }.OrderBy(path => path)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReplaceFailureKeepsPreviousBytesAndCleansOnlyItsOwnCandidate(bool readOnly)
        {
            var before = File.ReadAllBytes(_path);
            if (readOnly) File.SetAttributes(_path, FileAttributes.ReadOnly);
            using (var locked = readOnly ? null : File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.Catch(() => ProfileStore.Write(_path, writer => WriteValue(writer, "new"), document => { }));
            Assert.That(File.ReadAllBytes(_path), Is.EqualTo(before));
            Assert.That(Directory.GetFiles(_folder), Has.Length.EqualTo(2));
        }

        [Test]
        public void FirstSavePublishesAValidatedCandidateAndCreatesItsFolder()
        {
            var path = Path.Combine(_folder, "new", "profile.xml");
            var validated = false;
            ProfileStore.Write(path, writer => WriteValue(writer, "new"), document =>
            {
                Assert.That(File.Exists(path), Is.False);
                Assert.That((string)document.Root.Attribute("value"), Is.EqualTo("new"));
                validated = true;
            });
            Assert.That(validated, Is.True);
            Assert.That((string)ProfileStore.Read(path).Root.Attribute("value"), Is.EqualTo("new"));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(path)), Is.EqualTo(new[] { path }));
        }

        [Test]
        public void ByteLimitBoundaryRemainsReadable()
        {
            File.WriteAllText(_path, "<xml>" + new string('x', ProfileStore.MaxFileBytes - 11) + "</xml>", new UTF8Encoding(false));
            Assert.That(new FileInfo(_path).Length, Is.EqualTo(ProfileStore.MaxFileBytes));
            Assert.That(ProfileStore.Read(_path).Root.Value.Length, Is.EqualTo(ProfileStore.MaxFileBytes - 11));
        }

        [Test]
        public void Utf16ProfileAndUnicodeOpaqueDataSurviveUtf8Publication()
        {
            const string text = "မြန်မာ & log";
            var original = new XDocument(new XDeclaration("1.0", "utf-16", null),
                new XElement("xml", new XAttribute("label", text), new XElement("opaque", new XCData(text))));
            File.WriteAllText(_path, original.Declaration + original.ToString(), new UnicodeEncoding(false, true, true));
            var document = ProfileStore.Read(_path);
            ProfileStore.Write(_path, writer => document.Save(writer), candidate =>
                Assert.That(XNode.DeepEquals(XDocument.Parse(candidate.ToString()).Root, original.Root), Is.True));
            Assert.That(XNode.DeepEquals(XDocument.Parse(ProfileStore.Read(_path).ToString()).Root, original.Root), Is.True);
        }

        [Test]
        public void OpaqueTextCarriageReturnsSurvivePublication()
        {
            File.WriteAllText(_path, "<xml><opaque xml:space='preserve'>a&#xD;b&#xA;c&#xD;&#xA;d</opaque></xml>");
            var document = ProfileStore.Read(_path);
            var expected = document.Root.Element("opaque").Value;
            ProfileStore.Write(_path, writer => document.Save(writer), candidate =>
                Assert.That(candidate.Root.Element("opaque").Value, Is.EqualTo(expected)));
            Assert.That(ProfileStore.Read(_path).Root.Element("opaque").Value, Is.EqualTo(expected));
        }

        [Test]
        public void ConcurrentWritersPublishWholeValidatedDocuments()
        {
            using (var ready = new CountdownEvent(2))
            using (var release = new ManualResetEventSlim())
            {
                var tasks = new[] { "first", "second" }.Select(value => Task.Run(() =>
                {
                    try
                    {
                        ProfileStore.Write(_path, writer => WriteValue(writer, value), document =>
                        {
                            Assert.That((string)document.Root.Attribute("value"), Is.EqualTo(value));
                            ready.Signal();
                            if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
                        });
                        return true;
                    }
                    catch (IOException) { return false; }
                })).ToArray();
                try
                {
                    Assert.That(ready.Wait(TimeSpan.FromSeconds(5)), Is.True);
                    Assert.That((string)ProfileStore.Read(_path).Root.Attribute("value"), Is.EqualTo("old"));
                }
                finally
                {
                    release.Set();
                    Assert.That(Task.WaitAll(tasks, TimeSpan.FromSeconds(5)), Is.True);
                }
                Assert.That(tasks.Any(task => task.Result), Is.True);
                Assert.That((string)ProfileStore.Read(_path).Root.Attribute("value"), Is.AnyOf("first", "second"));
                Assert.That(Directory.GetFiles(_folder), Has.Length.EqualTo(2));
            }
        }

        private static void WriteValue(XmlWriter writer, string value)
        {
            writer.WriteStartElement("xml");
            writer.WriteAttributeString("value", value);
            writer.WriteEndElement();
        }
    }
}
#endif
