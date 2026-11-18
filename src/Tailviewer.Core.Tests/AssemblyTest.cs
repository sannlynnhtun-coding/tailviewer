using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace Tailviewer.Core.Tests
{
	[TestFixture]
	public sealed class AssemblyTest
	{
#if !NETFRAMEWORK
		[Test]
		public void TestPortableReferences()
		{
			var references = typeof(FilteredLogSource).Assembly.GetReferencedAssemblies().Select(x => x.Name);
			references.Should().NotContain(new[] { "Metrolib", "PresentationCore", "PresentationFramework", "WindowsBase", "System.Windows.Forms", "System.Drawing.Common", "archive" },
				"because the portable libraries must not depend on WPF or Metrolib");
		}
#endif

		[Test]
		public void TestNamespaces()
		{
			var exportedTypes = typeof(FilteredLogSource).Assembly.ExportedTypes;
			exportedTypes.Should().NotBeEmpty("because the Tailviewer.Core assembly should export some types");

			var failingTypes = new List<Type>();
			var expectedNamespaces = new[]
			{
				"Tailviewer.Core",
#if NET10_0_OR_GREATER
				// Shared legacy settings retain their serialized/public namespaces during migration.
				"Tailviewer.Settings", "Tailviewer.Settings.CustomFormats", "Tailviewer.Settings.Bookmarks",
				"Tailviewer.Settings.Dashboard", "Tailviewer.Settings.Dashboard.Widgets"
#endif
			};
			foreach (var type in exportedTypes)
			{
				if (!expectedNamespaces.Contains(type.Namespace))
					failingTypes.Add(type);
			}

			if (failingTypes.Any())
			{
				Assert.Fail("The following types are outside the allowed Core/settings namespaces ({0}):\r\n{1}",
				            string.Join(", ", expectedNamespaces),
				            string.Join("\r\n", failingTypes.Select(x => x.FullName)));
			}
		}
	}
}
