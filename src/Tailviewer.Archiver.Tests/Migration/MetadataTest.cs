using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Tailviewer.Archiver.Plugins;
using Tailviewer.Archiver.Plugins.Description;

namespace Tailviewer.Archiver.Tests.Migration
{
	[TestFixture]
	public sealed class MetadataTest
	{
		[Test]
		public void TestIconContractUsesEncodedBytes()
		{
			Assert.That(typeof(IPluginDescription).GetProperty("Icon").PropertyType, Is.EqualTo(typeof(byte[])));
			Assert.That(typeof(PluginDescription).GetProperty("Icon").PropertyType, Is.EqualTo(typeof(byte[])));
		}

		[Test]
		public void TestMetadataDoesNotReferenceWpf()
		{
			var forbidden = new[] { "PresentationCore", "PresentationFramework", "WindowsBase", "Metrolib" };
			var references = typeof(PluginDescription).Assembly.GetReferencedAssemblies().Select(x => x.Name);
			Assert.That(references.Intersect(forbidden), Is.Empty);
			foreach (var type in new[] { typeof(IPluginDescription), typeof(PluginDescription), typeof(IPluginImplementationDescription), typeof(IChange), typeof(PluginId) })
				foreach (var property in type.GetProperties())
					Assert.That(property.PropertyType.Namespace, Does.Not.StartWith("System.Windows"), type.Name + "." + property.Name);
		}

		[Test]
		public void TestIconAndMetadataRoundTrip()
		{
			using var icon = typeof(MetadataTest).Assembly.GetManifestResourceStream("MetadataTest.Icon.png");
			using var original = new MemoryStream();
			icon.CopyTo(original);
			var bytes = original.ToArray();
			using var zipBytes = new MemoryStream();
			using (var archive = new ZipArchive(zipBytes, ZipArchiveMode.Create, true))
			using (var output = archive.CreateEntry("icon.png").Open())
				output.Write(bytes);

			zipBytes.Position = 0;
			using var zip = new ZipArchive(zipBytes, ZipArchiveMode.Read);
			byte[] loaded;
			using (var input = zip.GetEntry("icon.png").Open())
			using (var buffer = new MemoryStream())
			{
				input.CopyTo(buffer);
				loaded = buffer.ToArray();
			}

			var description = new PluginDescription
			{
				Id = new PluginId("Example.Logs"), Name = "Logs", Author = "Example",
				Description = "Log parser", FilePath = "example.tvp", Version = new Version(1, 2, 3),
				Website = new Uri("https://example.invalid/"), TailviewerApiVersion = new Version(2, 0),
				SerializableTypes = new Dictionary<string, string> { { "settings", "Example.Settings, Example" } },
				Changes = Array.Empty<IChange>()
			};
			description.Icon = loaded;
			var restored = JsonSerializer.Deserialize<PluginDescription>(JsonSerializer.Serialize(description));
			Assert.Multiple(() =>
			{
				Assert.That(restored.Id, Is.EqualTo(description.Id));
				Assert.That(restored.Name, Is.EqualTo(description.Name));
				Assert.That(restored.Author, Is.EqualTo(description.Author));
				Assert.That(restored.Description, Is.EqualTo(description.Description));
				Assert.That(restored.FilePath, Is.EqualTo(description.FilePath));
				Assert.That(restored.Version, Is.EqualTo(description.Version));
				Assert.That(restored.Website, Is.EqualTo(description.Website));
				Assert.That(restored.TailviewerApiVersion, Is.EqualTo(description.TailviewerApiVersion));
				Assert.That(restored.SerializableTypes, Is.EquivalentTo(description.SerializableTypes));
				Assert.That(restored.PluginImplementations, Is.Empty);
				Assert.That(restored.Changes, Is.Empty);
				Assert.That(restored.Icon, Is.EqualTo(bytes));
			});
		}

		[Test]
		public void TestMissingIconAndMetadataDefaults()
		{
			var description = new PluginDescription();
			Assert.Multiple(() =>
			{
				Assert.That(description.Icon, Is.Null);
				Assert.That(description.PluginImplementations, Is.Empty);
				Assert.That(description.SerializableTypes, Is.Empty);
				Assert.That(description.ToString(), Is.EqualTo("<Unknown>"));
				Assert.That(new PluginDescription { Name = "Logs", Version = new Version(1, 2), Author = "Example" }.ToString(),
					Is.EqualTo("Name: Logs, Version: 1.2, Author: Example"));
			});
		}
	}
}
