using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
#if NET10_0_OR_GREATER
using System.Xml.Linq;
#endif
using log4net;

namespace Tailviewer.Settings.Bookmarks
{
	/// <summary>The persisted bookmark collection.</summary>
	public sealed class Bookmarks
		: List<BookmarkSettings>
		, IBookmarks
		, ICloneable
	{
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private static Task _saveTask;
#if NET10_0_OR_GREATER
		private XDocument _rawXml;
		private bool _restoreFailed;
#endif

		private readonly string _fileName;
		private readonly string _fileFolder;

		static Bookmarks()
		{
			_saveTask = Task.FromResult(42);
		}

		/// <summary>Creates bookmarks in the profile directory for this application.</summary>
		public static Bookmarks Create()
		{
#if NET10_0_OR_GREATER
			string fileName = ProfilePaths.BookmarksFile;
#else
			string fileName = Path.Combine(Constants.AppDataLocalFolder, "bookmarks.xml");
#endif
			return new Bookmarks(fileName);
		}

		/// <summary>Creates a bookmark collection at the supplied file path.</summary>
		public Bookmarks(string fileName)
		{
			if (Path.IsPathRooted(fileName))
			{
				_fileName = fileName;
				_fileFolder = Path.GetDirectoryName(_fileName);
			}
			else
			{
				_fileFolder = Directory.GetCurrentDirectory();
				_fileName = Path.Combine(_fileFolder, fileName);
			}
		}

		/// <summary>Saves the bookmark collection, reporting failure without throwing.</summary>
		public bool Save()
		{
#if NET10_0_OR_GREATER
			if (_restoreFailed) return false;
#endif
			try
			{
				using (var stream = new MemoryStream())
				{
					var settings = new XmlWriterSettings
					{
						Indent = true,
						IndentChars = "  ",
						NewLineChars = "\r\n",
						NewLineHandling = NewLineHandling.Replace
					};
					using (XmlWriter writer = XmlWriter.Create(stream, settings))
					{
#if NET10_0_OR_GREATER
						BuildDocument().Save(writer);
#else
						writer.WriteStartElement("xml");

						Save(writer);

						writer.WriteEndElement();
#endif
					}

					if (!Directory.Exists(_fileFolder))
						Directory.CreateDirectory(_fileFolder);

					using (var file =
						new FileStream(_fileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
					{
						var length = (int) stream.Position;
						file.Write(stream.GetBuffer(), 0, length);
						file.SetLength(length);
					}

					return true;
				}
			}
			catch (Exception e)
			{
#if NET10_0_OR_GREATER
				Log.ErrorFormat("Bookmark persistence failed ({0}); existing data retained.", e.GetType().Name);
#else
				Log.ErrorFormat("Caught unexpected exception: {0}", e);
#endif
				return false;
			}
		}

		/// <summary>Queues a snapshot for saving.</summary>
		public void SaveAsync()
		{
			var config = Clone();
			_saveTask = _saveTask.ContinueWith(unused => config.Save());
		}

		/// <summary>Restores the persisted bookmark collection.</summary>
		public void Restore()
		{
			if (!File.Exists(_fileName))
				return;

			try
			{
#if NET10_0_OR_GREATER
				var document = ProfileStore.Read(_fileName);
				var element = document.Root;

				var bookmarks = new List<BookmarkSettings>();
				foreach (var child in element.Elements("bookmark"))
				{
					var bookmark = new BookmarkSettings();
					using (var content = child.CreateReader())
					{
						content.MoveToContent();
						bookmark.Restore(content);
					}
					bookmarks.Add(bookmark);
				}
				_rawXml = document;
				_restoreFailed = false;

				Clear();
				Capacity = bookmarks.Count;
				AddRange(bookmarks);
#else
				using (FileStream stream = File.OpenRead(_fileName))
				using (XmlReader reader = XmlReader.Create(stream))
				{
					var bookmarks = new List<BookmarkSettings>();

					while (reader.Read())
					{
						switch (reader.Name)
						{
							case "bookmark":
								var bookmark = new BookmarkSettings();
								bookmark.Restore(reader);
								bookmarks.Add(bookmark);
								break;
						}
					}

					Clear();
					Capacity = bookmarks.Count;
					AddRange(bookmarks);

				}
#endif
			}
			catch (Exception e)
			{
#if NET10_0_OR_GREATER
				_restoreFailed = true;
				Log.ErrorFormat("Bookmark persistence failed ({0}); existing data retained.", e.GetType().Name);
#else
				Log.ErrorFormat("Caught unexpected exception: {0}", e);
#endif
			}
		}

		/// <summary>Removes the supplied bookmark records.</summary>
		public void Remove(IEnumerable<BookmarkSettings> removed)
		{
			foreach(var bookmark in removed.ToList())
			{
				var index = FindIndex(item => ReferenceEquals(item, bookmark));
				if (index >= 0) RemoveAt(index);
				else Remove(bookmark);
			}
		}

		/// <summary>Returns a snapshot of the configured bookmarks.</summary>
		public IEnumerable<BookmarkSettings> All
		{
			get { return this.ToList(); }
		}

#if NET10_0_OR_GREATER
		private XDocument BuildDocument()
		{
			var document = _rawXml == null ? new XDocument() : new XDocument(_rawXml);
			var root = SettingsXml.SaveElement("xml", Save);
			if (document.Root == null) document.Add(root);
			else document.Root.ReplaceWith(root);
			return document;
		}
#endif

		private void Save(XmlWriter writer)
		{
#if NET10_0_OR_GREATER
			var element = _rawXml == null ? new XElement("xml") : new XElement(_rawXml.Root);
			SettingsXml.ReplaceChildren(element, "bookmark",
				this.Select(bookmark => SettingsXml.SaveElement("bookmark", bookmark.Save)));
			SettingsXml.WriteContent(element, writer);
#else
			foreach (BookmarkSettings bookmark in this)
			{
				writer.WriteStartElement("bookmark");
				bookmark.Save(writer);
				writer.WriteEndElement();
			}
#endif
		}

#region Implementation of ICloneable

		/// <summary>Creates an independent snapshot with the same destination.</summary>
		public Bookmarks Clone()
		{
			var clone = new Bookmarks(_fileName);
			clone.AddRange(this.Select(x => x.Clone()));
#if NET10_0_OR_GREATER
			clone._rawXml = _rawXml == null ? null : new XDocument(_rawXml);
			clone._restoreFailed = _restoreFailed;
#endif
			return clone;
		}

		object ICloneable.Clone()
		{
			return Clone();
		}

#endregion
	}
}
