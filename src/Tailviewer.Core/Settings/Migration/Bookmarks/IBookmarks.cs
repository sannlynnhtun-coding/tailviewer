using System.Collections.Generic;

namespace Tailviewer.Settings.Bookmarks
{
	/// <summary>Persistent bookmark operations.</summary>
	public interface IBookmarks
	{
		/// <summary>Adds a bookmark record.</summary>
		void Add(BookmarkSettings bookmark);
		/// <summary>Removes the supplied bookmark records.</summary>
		void Remove(IEnumerable<BookmarkSettings> removed);

		/// <summary>A snapshot of configured bookmark records.</summary>
		IEnumerable<BookmarkSettings> All { get; }

		/// <summary>Saves the current bookmark collection.</summary>
		bool Save();
		/// <summary>Queues a bookmark snapshot for saving.</summary>
		void SaveAsync();
	}
}