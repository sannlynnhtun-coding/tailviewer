#if NET10_0_OR_GREATER && PROFILE_PERSISTENCE_CONTRACTS
using System.IO;
using System.Text;
using System.Xml.Linq;
using Moq;
using NUnit.Framework;
using Tailviewer.Api;
using Tailviewer.BusinessLogic.LogFileFormats;
using Tailviewer.Settings;
using Tailviewer.Settings.CustomFormats;
using Tailviewer.Ui.Settings;
using Tailviewer.Ui.Settings.CustomFormats;

namespace Tailviewer.Core.Tests.Migration
{
    [TestFixture]
    public sealed class CustomFormatEditingTest
    {
        private CustomFormatsSettings _formats;
        private Mock<IApplicationSettings> _settings;
        private EncodingViewModel _automatic;

        private CustomFormatViewModel Create()
        {
            var original = new CustomLogFileFormat();
            using (var reader = XElement.Parse("<customformat plugin='missing.plugin' name='original' format='{old}' encoding='future-encoding' future='keep'/>").CreateReader())
            {
                reader.MoveToContent();
                original.Restore(reader);
            }
            _formats = new CustomFormatsSettings { original };
            _settings = new Mock<IApplicationSettings>();
            _settings.SetupGet(x => x.CustomFormats).Returns(_formats);
            var container = new ServiceContainer();
            container.RegisterInstance(new Mock<ILogFileFormatRegistry>().Object);
            var plugin = new Mock<ICustomLogFileFormatCreatorPlugin>();
            ILogFileFormat logFormat = new Mock<ILogFileFormat>().Object;
            plugin.Setup(x => x.TryCreate(It.IsAny<IServiceContainer>(), It.IsAny<ICustomLogFileFormat>(), out logFormat)).Returns(true);
            _automatic = new EncodingViewModel(null, "Automatic");
            return new CustomFormatViewModel(container, _settings.Object, plugin.Object, new[] { _automatic, new EncodingViewModel(Encoding.UTF8) }, original);
        }

        [Test]
        public void FormatEditDoesNotEraseAnUnavailableEncoding()
        {
            var viewModel = Create();
            viewModel.Format = "{changed}";
            Assert.That(_formats[0].EncodingName, Is.EqualTo("future-encoding"));
            Assert.That(_formats[0].Format, Is.EqualTo("{changed}"));
            Assert.That(_formats.Count, Is.EqualTo(1));
        }

        [Test]
        public void ExplicitAutomaticSelectionClearsTheUnavailableEncoding()
        {
            var viewModel = Create();
            Assert.That(viewModel.Encoding.ToString(), Does.Contain("future-encoding"));
            viewModel.Encoding = _automatic;
            Assert.That(_formats[0].EncodingName, Is.EqualTo(string.Empty));
            _settings.Verify(x => x.SaveAsync(), Times.Once);
        }

        [Test]
        public void EditingOneOfTwoEquivalentDefinitionsPreservesTheOtherRecord()
        {
            var viewModel = Create();
            var other = new CustomLogFileFormat();
            using (var reader = XElement.Parse("<customformat plugin='missing.plugin' name='original' format='{old}' encoding='future-encoding' future='other'/>").CreateReader())
            {
                reader.MoveToContent();
                other.Restore(reader);
            }
            _formats.Insert(0, other);
            viewModel.Format = "{changed}";
            Assert.That(_formats.Count, Is.EqualTo(2));
            Assert.That(_formats[0], Is.SameAs(other));
            Assert.That(_formats[1].Format, Is.EqualTo("{changed}"));
        }

        [Test]
        public void StaleEditorDoesNotRecreateARemovedDefinition()
        {
            var viewModel = Create();
            _formats.Clear();
            viewModel.Format = "{changed}";
            Assert.That(_formats, Is.Empty);
        }

        [Test]
        public void NameEditIsPersistedWithoutDiscardingOpaqueData()
        {
            var viewModel = Create();
            viewModel.Name = "renamed";
            Assert.That(_formats[0].Name, Is.EqualTo("renamed"));
            using var text = new StringWriter();
            using (var writer = System.Xml.XmlWriter.Create(text))
            {
                writer.WriteStartElement("customformat");
                _formats[0].Save(writer);
                writer.WriteEndElement();
            }
            Assert.That((string)XElement.Parse(text.ToString()).Attribute("future"), Is.EqualTo("keep"));
            _settings.Verify(x => x.SaveAsync(), Times.Once);
        }
    }
}
#endif
