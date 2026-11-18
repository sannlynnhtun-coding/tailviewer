using FluentAssertions;
using NUnit.Framework;
using Tailviewer.Archiver.Repository;

namespace Tailviewer.Archiver.Tests.Repository
{
	[TestFixture]
	public sealed class RemotePublishDisabledExceptionTest
	{
		[Test]
		public void TestExceptionMetadata()
		{
			var inner = new System.InvalidOperationException("cause");
			var exception = new RemotePublishDisabledException("disabled", inner);
			exception.Message.Should().Be("disabled");
			exception.InnerException.Should().BeSameAs(inner);
			new RemotePublishDisabledException().Message.Should().Be("Remote publishing is disabled in this repository");
		}
	}
}
