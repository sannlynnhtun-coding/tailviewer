#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NUnit.Framework;

namespace Tailviewer.Core.Tests.Migration
{
	[TestFixture]
	public sealed class FixtureManifestTest
	{
		[Test]
		public void TestRecordedFixtures()
		{
			var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (root != null && !File.Exists(Path.Combine(root.FullName, "Tailviewer.Avalonia.slnx")))
				root = root.Parent;
			Assert.That(root, Is.Not.Null, "Fixture audit requires the repository checkout");
			var manifestPath = Path.Combine(root.FullName, "src/Tailviewer.Core.Tests/Migration/Fixtures/manifest.json");
			using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
			Assert.That(manifest.RootElement.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
			var ids = new System.Collections.Generic.HashSet<string>();
			foreach (var fixture in manifest.RootElement.GetProperty("fixtures").EnumerateArray())
			{
				var id = fixture.GetProperty("id").GetString();
				Assert.That(ids.Add(id), Is.True, "Duplicate fixture ID: " + id);
				byte[] bytes;
				if (fixture.TryGetProperty("path", out var path))
				{
					var absolutePath = Path.GetFullPath(Path.Combine(root.FullName, path.GetString()));
					Assert.That(absolutePath.StartsWith(root.FullName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
					bytes = File.ReadAllBytes(absolutePath);
					if (fixture.TryGetProperty("normalization", out var normalization))
					{
						Assert.That(normalization.GetString(), Is.EqualTo("lf"), id);
						bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n"));
					}
				}
				else
				{
					bytes = Convert.FromBase64String(fixture.GetProperty("base64").GetString());
				}
				Assert.That(bytes.Length, Is.EqualTo(fixture.GetProperty("sizeBytes").GetInt32()), id);
				Assert.That(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), Is.EqualTo(fixture.GetProperty("sha256").GetString()), id);
				if (fixture.TryGetProperty("expectedLines", out var expected))
				{
					using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, true);
					foreach (var line in expected.EnumerateArray())
						Assert.That(reader.ReadLine(), Is.EqualTo(line.GetString()), id);
					Assert.That(reader.ReadLine(), Is.Null, id);
				}
				if (fixture.TryGetProperty("expectedAttributes", out var attributes))
				{
					var xml = new System.Xml.XmlDocument { XmlResolver = null };
					xml.Load(new MemoryStream(bytes));
					foreach (var attribute in attributes.EnumerateObject())
						Assert.That(xml.SelectSingleNode(attribute.Name)?.Value, Is.EqualTo(attribute.Value.GetString()), id);
				}
			}
			Assert.That(ids.Count, Is.GreaterThan(0));
		}
	}
}
#endif
