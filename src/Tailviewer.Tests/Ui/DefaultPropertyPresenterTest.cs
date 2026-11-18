using FluentAssertions;
using NUnit.Framework;
using Tailviewer.Core;
using Tailviewer.Ui.SidePanel.Property;

namespace Tailviewer.Tests.Ui
{
    [TestFixture]
    public sealed class DefaultPropertyPresenterTest
    {
        [Test]
        public void TestSizeFormatting()
        {
            var presenter = new DefaultPropertyPresenter("Size", Core.Properties.Size);
            const long bytes = 5L * 1024 * 1024 * 1024;
            presenter.Update(bytes);
            presenter.Value.Should().Be(Metrolib.Size.FromBytes(bytes),
                "because the WPF side panel must keep human-readable sizes after Core switches to bytes");
            presenter.Update(null);
            presenter.Value.Should().BeNull("because an unknown size must remain unknown");
        }

        [Test]
        public void TestOtherPropertyUnchanged()
        {
            var presenter = new DefaultPropertyPresenter("Name", Core.Properties.Name);
            presenter.Update("test.log");
            presenter.Value.Should().Be("test.log", "because formatting applies only to the size property");
        }
    }
}
