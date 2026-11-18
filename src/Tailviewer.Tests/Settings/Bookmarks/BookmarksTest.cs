using System;
using System.IO;
using FluentAssertions;
using NUnit.Framework;
using Tailviewer.Api;
using Tailviewer.Settings.Bookmarks;

namespace Tailviewer.Tests.Settings.Bookmarks
{
	[TestFixture]
	public sealed class BookmarksTest
	{
		private string _folder;
		[SetUp]
		public void SetUp()
		{
			_folder = Path.Combine(Path.GetTempPath(), "tailviewer-bookmarks-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_folder);
		}
		[TearDown]
		public void TearDown() => Directory.Delete(_folder, true);

		[Test]
		public void TestCloneEmpty()
		{
			var bookmarks = new Tailviewer.Settings.Bookmarks.Bookmarks(Path.Combine(_folder, "bookmarks.xml"));
			var clone = bookmarks.Clone();
			clone.Should().NotBeNull();
			clone.Should().NotBeSameAs(bookmarks);
			clone.Should().BeEmpty();
		}

		[Test]
		public void TestCloneOneBookmark()
		{
			var bookmarks = new Tailviewer.Settings.Bookmarks.Bookmarks(Path.Combine(_folder, "bookmarks.xml"));
			bookmarks.Add(new BookmarkSettings(DataSourceId.CreateNew(), new LogLineIndex(42)));
			var clone = bookmarks.Clone();
			clone.Should().NotBeNull();
			clone.Should().NotBeSameAs(bookmarks);
			clone.Should().HaveCount(1);
			clone[0].Should().Be(bookmarks[0]);
			clone[0].Should().NotBeSameAs(bookmarks[0]);
		}

		[Test]
		public void TestRestoreNoSuchFile()
		{
			var filePath = Path.Combine(_folder, "not-created.xml");
			var bookmarks = new Tailviewer.Settings.Bookmarks.Bookmarks(filePath);
			new Action(() => bookmarks.Restore()).Should().NotThrow();
			bookmarks.Should().BeEmpty();
		}

		[Test]
		public void TestRoundtrip()
		{
			var filePath = Path.Combine(_folder, "bookmarks.xml");

			var bookmarks = new Tailviewer.Settings.Bookmarks.Bookmarks(filePath);
			bookmarks.Add(new BookmarkSettings(DataSourceId.CreateNew(), new LogLineIndex(101)));
			bookmarks.Save();

			var actualBookmarks = new Tailviewer.Settings.Bookmarks.Bookmarks(filePath);
			actualBookmarks.Restore();
			actualBookmarks.Should().HaveCount(1);
			actualBookmarks[0].DataSourceId.Should().Be(bookmarks[0].DataSourceId);
			actualBookmarks[0].Index.Should().Be(bookmarks[0].Index);
		}
	}
}
