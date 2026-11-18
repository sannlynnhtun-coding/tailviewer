using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace Tailviewer.Api.Tests
{
	[TestFixture]
	public sealed class AssemblyTest
	{
#if !NETFRAMEWORK
        [Test]
        public void TestPortableReferences()
        {
            var references = typeof(Tailviewer.Api.ILogSource).Assembly.GetReferencedAssemblies().Select(x => x.Name);
            references.Should().NotContain(new[] { "Metrolib", "PresentationCore", "PresentationFramework", "WindowsBase" },
                "because the portable libraries must not depend on WPF or Metrolib");
        }
#endif

		[Test]
		public void TestNamespaces()
		{
			var exportedTypes = typeof(ILogSource).Assembly.ExportedTypes;
			exportedTypes.Should().NotBeEmpty("because the Tailviewer.Api assembly should export some types");

			var failingTypes = new List<Type>();
			var expectedNamespace = "Tailviewer.Api";
			foreach (var type in exportedTypes)
			{
				if (type.Namespace != expectedNamespace)
					failingTypes.Add(type);
			}

			if (failingTypes.Any())
			{
				Assert.Fail("The following types should be located directly in the {0} namespace but are not:\r\n{1}",
				            expectedNamespace,
				            string.Join("\r\n", failingTypes.Select(x => x.FullName)));
			}
		}
	}
}
