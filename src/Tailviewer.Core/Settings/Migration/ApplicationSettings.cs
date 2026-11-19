using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
#if NET10_0_OR_GREATER
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
#endif
using log4net;
using Tailviewer.Core;
using Tailviewer.Settings.CustomFormats;

namespace Tailviewer.Settings
{
	/// <summary>The application profile with its existing XML sections and defaults.</summary>
	public sealed class ApplicationSettings
		: IApplicationSettings
		, ICloneable
	{
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private static Task _saveTask;
#if NET10_0_OR_GREATER
		private XDocument _rawXml;
		private bool _restoreFailed;
#endif

		private AutoUpdateSettings _autoUpdate;

		private DataSourceSettings _dataSources;
		private readonly string _fileFolder;
		private readonly string _fileName;
		private MainWindowSettings _mainWindow;
		private QuickFiltersSettings _quickFilters;
		private ExportSettings _export;
		private LogViewerSettings _logViewer;
		private LogFileSettings _logFile;
		private CustomFormatsSettings _customFormats;
		private int _numSaved;

		private bool _allowSave;

		/// <summary>Whether profile saves are enabled.</summary>
		public bool AllowSave
		{
			get { return _allowSave; }
			set { _allowSave = value; }
		}

		/// <summary>
		///    How often <see cref=" SaveAsync"/> was called.
		/// </summary>
		public int NumSaved
		{
			get { return _numSaved; }
		}

		static ApplicationSettings()
		{
			_saveTask = Task.FromResult(42);
		}

		private ApplicationSettings(ApplicationSettings other)
		{
			_fileName = other._fileName;
			_fileFolder = other._fileFolder;
			_autoUpdate = other._autoUpdate.Clone();
			_mainWindow = other._mainWindow.Clone();
			_dataSources = other._dataSources.Clone();
			_quickFilters = other._quickFilters.Clone();
			_export = other._export.Clone();
			_logViewer = other._logViewer.Clone();
			_logFile = other._logFile.Clone();
			_customFormats = other._customFormats.Clone();
#if NET10_0_OR_GREATER
			_allowSave = other._allowSave;
			_rawXml = other._rawXml == null ? null : new XDocument(other._rawXml);
			_restoreFailed = other._restoreFailed;
#else
			_allowSave = true;
#endif
		}

		/// <summary>Creates a profile at the supplied file path.</summary>
		public ApplicationSettings(string fileName)
		{
			_fileName = Path.GetFullPath(fileName);
			_fileFolder = Path.GetDirectoryName(_fileName);

			_autoUpdate = new AutoUpdateSettings();
			_mainWindow = new MainWindowSettings();
			_dataSources = new DataSourceSettings();
			_quickFilters = new QuickFiltersSettings();
			_logViewer = new LogViewerSettings();
			_logFile = new LogFileSettings();
			_export = new ExportSettings();
			_customFormats = new CustomFormatsSettings();
			_allowSave = true;
		}

		/// <summary>The AutoUpdate configuration.</summary>
		public IAutoUpdateSettings AutoUpdate => _autoUpdate;

		/// <summary>The MainWindow configuration.</summary>
		public IMainWindowSettings MainWindow => _mainWindow;

		/// <summary>The DataSources configuration.</summary>
		public IDataSourcesSettings DataSources => _dataSources;

		/// <summary>The LogViewer configuration.</summary>
		public ILogViewerSettings LogViewer => _logViewer;

		/// <summary>The LogFile configuration.</summary>
		public ILogFileSettings LogFile => _logFile;

		/// <summary>The QuickFilters configuration.</summary>
		public QuickFiltersSettings QuickFilters => _quickFilters;

		/// <summary>The Export configuration.</summary>
		public IExportSettings Export => _export;

		/// <summary>The CustomFormats configuration.</summary>
		public ICustomFormatsSettings CustomFormats => _customFormats;

		/// <summary>Creates the default profile for this application.</summary>
		public static ApplicationSettings Create()
		{
#if NET10_0_OR_GREATER
			string fileName = ProfilePaths.SettingsFile;
#else
			string fileName = Path.Combine(Constants.AppDataLocalFolder, "settings");
			fileName += ".xml";
#endif
			return new ApplicationSettings(fileName);
		}

		/// <summary>Queues an independent profile snapshot for saving.</summary>
		public void SaveAsync()
		{
			if (!AllowSave
#if NET10_0_OR_GREATER
				|| _restoreFailed
#endif
			)
			{
				Log.DebugFormat("Saving not allow, skipping...");
				return;
			}

			var config = Clone();
			_saveTask = _saveTask.ContinueWith(unused => config.Save());
			++_numSaved;
		}

		/// <summary>Saves the profile, reporting IO or serialization failure.</summary>
		public bool Save()
		{
#if NET10_0_OR_GREATER
			if (!AllowSave || _restoreFailed) return false;
#endif
			try
			{
#if NET10_0_OR_GREATER
				ProfileStore.Write(_fileName, writer => BuildDocument().Save(writer),
					document => new ApplicationSettings(_fileName).RestoreDocument(document, out _));
				return true;
#else
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
						WriteKnown(writer);
						writer.WriteEndElement();

					}

					if (!Directory.Exists(_fileFolder))
						Directory.CreateDirectory(_fileFolder);

					using (var file = new FileStream(_fileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
					{
						var length = (int) stream.Position;
						file.Write(stream.GetBuffer(), 0, length);
						file.SetLength(length);
					}

					return true;
				}
#endif
			}
			catch (Exception e)
			{
#if NET10_0_OR_GREATER
				Log.ErrorFormat("Could not save profile ({0}); serialization or file access failed.", e.GetType().Name);
#else
				Log.ErrorFormat("Caught unexpected exception: {0}", e);
#endif
				return false;
			}
		}

		/// <summary>Restores the profile from its configured file.</summary>
		public void Restore()
		{
			bool unused;
			Restore(out unused);
		}

		/// <summary>
		/// </summary>
		/// <remarks>On the modern target, successful restore replaces section snapshots. Bind consumers after restoring.</remarks>
		/// <param name="neededPatching">Whether or not certain values need to be changed (for example due to upgrades to the format - it is advised that the current settings be saved again if this is set to true)</param>
		public void Restore(out bool neededPatching)
		{
			neededPatching = false;
			if (!File.Exists(_fileName))
				return;

			try
			{
#if NET10_0_OR_GREATER
				RestoreDocument(ProfileStore.Read(_fileName), out neededPatching);
#else
				using (FileStream stream = File.OpenRead(_fileName))
				using (XmlReader reader = XmlReader.Create(stream))
				{
					while (reader.Read())
					{
						RestoreSection(reader, out var sectionPatched);
						neededPatching |= sectionPatched;
					}

				}
#endif
			}
			catch (Exception e)
			{
#if NET10_0_OR_GREATER
				neededPatching = false;
				_restoreFailed = true;
				Log.ErrorFormat("Could not restore profile ({0}); previous settings retained.", e.GetType().Name);
#else
				Log.ErrorFormat("Caught unexpected exception: {0}", e);
#endif
			}
		}

#if NET10_0_OR_GREATER
		private void RestoreDocument(XDocument document, out bool neededPatching)
		{
			var candidate = Clone();
			var seen = new HashSet<XName>();
			var patched = false;
			foreach (var child in document.Root.Elements())
			{
				if (child.Name.Namespace != XNamespace.None) continue;
				using (var content = child.CreateReader())
				{
					content.MoveToContent();
					if (!candidate.RestoreSection(content, out var sectionPatched)) continue;
					if (!seen.Add(child.Name)) throw new XmlException("Duplicate profile section.");
					patched |= sectionPatched;
				}
			}
			// Restore publishes new section snapshots; consumers bind after loading a profile.
			_autoUpdate = candidate._autoUpdate;
			_dataSources = candidate._dataSources;
			_mainWindow = candidate._mainWindow;
			_quickFilters = candidate._quickFilters;
			_export = candidate._export;
			_logViewer = candidate._logViewer;
			_logFile = candidate._logFile;
			_customFormats = candidate._customFormats;
			_rawXml = document;
			_restoreFailed = false;
			neededPatching = patched;
		}
#endif

		private bool RestoreSection(XmlReader reader, out bool neededPatching)
		{
			neededPatching = false;
			switch (reader.Name)
			{
				case "mainwindow":
					_mainWindow.Restore(reader);
					return true;
				case "datasources":
					_dataSources.Restore(reader, out neededPatching);
					return true;
				case "quickfilters":
					_quickFilters.Restore(reader);
					return true;
				case "autoupdate":
					_autoUpdate.Restore(reader);
					return true;
				case "export":
					_export.Restore(reader);
					return true;
				case "logviewer":
					_logViewer.Restore(reader);
					return true;
				case "logfile":
					_logFile.Restore(reader);
					return true;
				case "customformats":
					_customFormats.Restore(reader);
					return true;
				default:
					return false;
			}
		}

		private void WriteKnown(XmlWriter writer)
		{
			writer.WriteStartElement("mainwindow");
			_mainWindow.Save(writer);
			writer.WriteEndElement();

			writer.WriteStartElement("datasources");
			_dataSources.Save(writer);
			writer.WriteEndElement();

			writer.WriteStartElement("quickfilters");
			_quickFilters.Save(writer);
			writer.WriteEndElement();

			writer.WriteStartElement("autoupdate");
			_autoUpdate.Save(writer);
			writer.WriteEndElement();

			writer.WriteStartElement("export");
			_export.Save(writer);
			writer.WriteEndElement();

			writer.WriteStartElement("logviewer");
			_logViewer.Save(writer);
			writer.WriteEndElement();

			writer.WriteStartElement("logfile");
			_logFile.Save(writer);
			writer.WriteEndElement();

			writer.WriteStartElement("customformats");
			_customFormats.Save(writer);
			writer.WriteEndElement();
		}

#if NET10_0_OR_GREATER
		private XDocument BuildDocument()
		{
			var known = SettingsXml.SaveElement("xml", WriteKnown);
			var document = _rawXml == null ? new XDocument(new XElement("xml")) : new XDocument(_rawXml);
			foreach (var section in known.Elements())
			{
				if (section.Name == "datasources" || section.Name == "quickfilters" || section.Name == "customformats")
				{
					SettingsXml.ReplaceChildren(document.Root, section.Name.LocalName, new[] { section });
				}
				else
				{
					var current = document.Root.Element(section.Name);
					if (current == null) document.Root.Add(new XElement(section));
					else MergeScalarSection(current, section);
				}
			}
			return document;
		}

		private static void MergeScalarSection(XElement current, XElement known)
		{
			foreach (var attribute in known.Attributes()) current.SetAttributeValue(attribute.Name, attribute.Value);
			foreach (var child in known.Elements())
			{
				var matches = current.Elements(child.Name).ToList();
				if (matches.Count == 0) current.Add(new XElement(child));
				else foreach (var match in matches) MergeScalarSection(match, child);
			}
		}
#endif

		/// <summary>Creates an independent snapshot with the same destination.</summary>
		public ApplicationSettings Clone()
		{
			return new ApplicationSettings(this);
		}

		object ICloneable.Clone()
		{
			return Clone();
		}
	}
}
