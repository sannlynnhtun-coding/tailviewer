using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Tailviewer.Api;
using Tailviewer.Archiver.Applications;
using Tailviewer.Archiver.Plugins;

namespace Tailviewer.Archiver.Tests.Migration
{
	[TestFixture]
	public sealed class ArchiverRuntimeTest
	{
		[Test]
		public void TestModernAssemblyHasNoLegacyRuntimeReferences()
		{
			var forbidden = new[] { "Metrolib", "PE", "SharpRemote", "PresentationCore", "PresentationFramework", "WindowsBase", "System.Drawing", "System.Drawing.Common" };
			Assert.That(typeof(PluginPacker).Assembly.GetReferencedAssemblies().Select(x => x.Name).Intersect(forbidden), Is.Empty);
		}

		[Test]
		public void TestPackModernApiWithoutBundlingRuntime()
		{
			using var bytes = new MemoryStream();
			using (var packer = PluginPacker.Create(bytes, true))
				packer.AddFile("Tailviewer.Api.dll", typeof(ILogSource).Assembly.Location);
			bytes.Position = 0;
			using var archive = PluginArchive.OpenRead(bytes);
			Assert.That(archive.Index.Assemblies.Select(x => x.AssemblyName), Is.EqualTo(new[] { "Tailviewer.Api" }));
		}

		[Test]
		public void TestEncodedIconBytesArePreserved()
		{
			using var icon = typeof(ArchiverRuntimeTest).Assembly.GetManifestResourceStream("MetadataTest.Icon.png");
			using var expected = new MemoryStream();
			icon.CopyTo(expected);
			expected.Position = 0;
			using var bytes = new MemoryStream();
			using (var packer = PluginPacker.Create(bytes, true))
				packer.SetIcon(expected);
			bytes.Position = 0;
			using var archive = PluginArchive.OpenRead(bytes);
			using var actual = new MemoryStream();
			using (var input = archive.ReadIcon())
				input.CopyTo(actual);
			Assert.That(actual.ToArray(), Is.EqualTo(expected.ToArray()));
		}

		[Test]
		public void TestMalformedIconIsRejected()
		{
			using var bytes = new MemoryStream();
			using var packer = PluginPacker.Create(bytes, true);
			using var icon = new MemoryStream(new byte[] { 1, 2, 3 });
			Assert.Throws<PackException>(() => packer.SetIcon(icon));
		}

		[Test]
		public void TestMalformedAssemblyMetadataIsRejected()
		{
			Assert.Throws<BadImageFormatException>(() => AssemblyDescription.FromRawData(new byte[] { 1, 2, 3 }));
		}

		[Test]
		public void TestLegacyPublishFailsExplicitlyUntilTransportMigration()
		{
			Assert.That(new Publish().Run(new PublishOptions()), Is.EqualTo(ExitCode.ConnectionError));
		}
	}
}
