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
		private XElement _rawXml;
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
						writer.WriteStartElement("xml");

						Save(writer);

						writer.WriteEndElement();
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
				Log.ErrorFormat("Caught unexpected exception: {0}", e);
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
				using (FileStream stream = File.OpenRead(_fileName))
				using (XmlReader reader = XmlReader.Create(stream))
				{
#if NET10_0_OR_GREATER
					var element = XElement.Load(reader, LoadOptions.PreserveWhitespace);
					if (element.Name != "xml") throw new XmlException("Unexpected bookmark root element.");
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
					_rawXml = element;
					_restoreFailed = false;
#else
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

#endif
					Clear();
					Capacity = bookmarks.Count;
					AddRange(bookmarks);
				}
			}
			catch (Exception e)
			{
#if NET10_0_OR_GREATER
				_restoreFailed = true;
#endif
				Log.ErrorFormat("Caught unexpected exception: {0}", e);
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

		private void Save(XmlWriter writer)
		{
#if NET10_0_OR_GREATER
			var element = _rawXml == null ? new XElement("xml") : new XElement(_rawXml);
			var replacements = new Queue<XElement>(this.Select(bookmark =>
			{
				var document = new XDocument();
				using (var content = document.CreateWriter())
				{
					content.WriteStartElement("bookmark");
					bookmark.Save(content);
					content.WriteEndElement();
				}
				return document.Root;
			}));
			foreach (var child in element.Elements("bookmark").ToList())
			{
				if (replacements.Count > 0) child.ReplaceWith(replacements.Dequeue());
				else child.Remove();
			}
			element.Add(replacements);
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
			clone._rawXml = _rawXml == null ? null : new XElement(_rawXml);
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
