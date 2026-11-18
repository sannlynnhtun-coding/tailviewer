using System.IO;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NUnit.Framework;
using Tailviewer.Archiver.Plugins.Description;
using Tailviewer.Ui.Plugins;

namespace Tailviewer.Tests.Ui
{
	[TestFixture]
	[Apartment(ApartmentState.STA)]
	public sealed class InstalledPluginIconTest
	{
		[Test]
		public void TestMissingIcon()
		{
			Assert.That(new InstalledPluginViewModel(new PluginDescription()).Icon, Is.Null);
		}

		[Test]
		public void TestEncodedIconIsDecodedOnceAndDetachedFromStream()
		{
			var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 10, 20, 30, 255 }, 4);
			var encoder = new PngBitmapEncoder();
			encoder.Frames.Add(BitmapFrame.Create(bitmap));
			byte[] bytes;
			using (var buffer = new MemoryStream())
			{
				encoder.Save(buffer);
				bytes = buffer.ToArray();
			}
			var description = new PluginDescription();
			description.Icon = bytes;
			var viewModel = new InstalledPluginViewModel(description);
			var image = (BitmapSource)viewModel.Icon;
			Assert.That(image.PixelWidth, Is.EqualTo(1));
			Assert.That(image.PixelHeight, Is.EqualTo(1));
			Assert.That(image.IsFrozen, Is.True);
			Assert.That(viewModel.Icon, Is.SameAs(image));
		}

		[TestCase(0)]
		[TestCase(3)]
		public void TestMalformedIconKeepsPluginMetadataUsable(int length)
		{
			var description = new PluginDescription { Name = "Still usable", Icon = new byte[length] };
			var viewModel = new InstalledPluginViewModel(description);
			Assert.That(viewModel.Icon, Is.Null);
			Assert.That(viewModel.Name, Is.EqualTo("Still usable"));
		}
	}
}
