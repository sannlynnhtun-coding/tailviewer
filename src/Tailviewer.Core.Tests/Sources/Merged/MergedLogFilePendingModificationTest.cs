using FluentAssertions;
using Moq;
using NUnit.Framework;
using Tailviewer.Api;

namespace Tailviewer.Core.Tests.Sources.Merged
{
	[TestFixture]
	public sealed class MergedLogFilePendingModificationTest
	{
		[Test]
		public void InvalidatingOneSourcesPendingAppendsPreservesAnotherSourcesAppends()
		{
			var first = new Mock<ILogSource>().Object;
			var second = new Mock<ILogSource>().Object;
			var other = new MergedLogSourcePendingModification(second, LogSourceModification.Appended(0, 1));
			var remove = new MergedLogSourcePendingModification(first, LogSourceModification.Removed(0, 2));
			var replacement = new MergedLogSourcePendingModification(first, LogSourceModification.Appended(0, 1));
			MergedLogSourcePendingModification.Optimize(new[]
			{
				new MergedLogSourcePendingModification(first, LogSourceModification.Appended(0, 2)), other, remove, replacement
			}).Should().Equal(new[] { other, remove, replacement });
		}

		[Test]
		public void EmptyInvalidationDoesNotDiscardPendingAppends()
		{
			var source = new Mock<ILogSource>().Object;
			var input = new[]
			{
				new MergedLogSourcePendingModification(source, LogSourceModification.Appended(0, 2)),
				new MergedLogSourcePendingModification(source, LogSourceModification.Removed(0, 0))
			};
			MergedLogSourcePendingModification.Optimize(input).Should().Equal(input);
		}

		[Test]
		public void TestOptimizeEmpty()
		{
			var input = new MergedLogSourcePendingModification[0];
			MergedLogSourcePendingModification.Optimize(input).Should().BeEmpty();
		}

		[Test]
		public void TestOptimizeOneSourceAppendReset()
		{
			var source = new Mock<ILogSource>();
			var input = new[]
			{
				new MergedLogSourcePendingModification(source.Object, LogSourceModification.Appended(1, 2)),
				new MergedLogSourcePendingModification(source.Object, LogSourceModification.Reset())
			};
			MergedLogSourcePendingModification.Optimize(input).Should().Equal(new object[]
			{
				new MergedLogSourcePendingModification(source.Object, LogSourceModification.Reset())
			});
		}

		[Test]
		public void TestOptimizeOneSourceAppendResetTwice()
		{
			var source = new Mock<ILogSource>();
			var input = new[]
			{
				new MergedLogSourcePendingModification(source.Object, LogSourceModification.Appended(1, 2)),
				new MergedLogSourcePendingModification(source.Object, LogSourceModification.Reset()),
				new MergedLogSourcePendingModification(source.Object, LogSourceModification.Appended(0, 100)),
				new MergedLogSourcePendingModification(source.Object, LogSourceModification.Reset())
			};
			MergedLogSourcePendingModification.Optimize(input).Should().Equal(new object[]
			{
				new MergedLogSourcePendingModification(source.Object, LogSourceModification.Reset())
			});
		}

		[Test]
		public void TestOptimizeTwoSourcesAppendReset()
		{
			var source1 = new Mock<ILogSource>();
			var source2 = new Mock<ILogSource>();
			var input = new[]
			{
				new MergedLogSourcePendingModification(source1.Object, LogSourceModification.Appended(1, 2)),
				new MergedLogSourcePendingModification(source2.Object, LogSourceModification.Reset())
			};
			MergedLogSourcePendingModification.Optimize(input).Should().Equal(new object[]
			{
				new MergedLogSourcePendingModification(source1.Object, LogSourceModification.Appended(1, 2)),
				new MergedLogSourcePendingModification(source2.Object, LogSourceModification.Reset())
			});
		}

		[Test]
		public void TestOptimizeTwoSourcesAppendReset2()
		{
			var source1 = new Mock<ILogSource>();
			var source2 = new Mock<ILogSource>();
			var input = new[]
			{
				new MergedLogSourcePendingModification(source1.Object, LogSourceModification.Appended(1, 2)),
				new MergedLogSourcePendingModification(source2.Object, LogSourceModification.Appended(0, 42)),
				new MergedLogSourcePendingModification(source2.Object, LogSourceModification.Reset())
			};
			MergedLogSourcePendingModification.Optimize(input).Should().Equal(new object[]
			{
				new MergedLogSourcePendingModification(source1.Object, LogSourceModification.Appended(1, 2)),
				new MergedLogSourcePendingModification(source2.Object, LogSourceModification.Reset())
			});
		}
	}
}
