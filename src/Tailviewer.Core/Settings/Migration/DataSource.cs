using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
#if NET10_0_OR_GREATER
using System.Linq;
using System.Xml.Linq;
#endif
#if !NET10_0_OR_GREATER
using Metrolib;
#endif
using log4net;
using Tailviewer.Api;
using Tailviewer.Core;

namespace Tailviewer.Settings
{
	/// <summary>Persisted source configuration with the original XML names and source identity.</summary>
	public sealed class DataSource
		: ICloneable
	{
		private static readonly ILog Log =
			LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private readonly List<QuickFilterId> _activatedQuickFilters;
#if NET10_0_OR_GREATER
		private XElement _rawXml;
#endif

		/// <summary>Whether entries use log-level colors.</summary>
		public bool ColorByLevel;
		/// <summary>Whether empty lines are hidden.</summary>
		public bool HideEmptyLines;
		/// <summary>Whether multiline entries display on a single line.</summary>
		public bool IsSingleLine;

		/// <summary>The configured file path, including unavailable paths.</summary>
		public string File;
		/// <summary>Whether new entries are followed.</summary>
		public bool FollowTail;
		/// <summary>Whether line numbers are visible.</summary>
		public bool ShowLineNumbers;
		/// <summary>Whether timestamp differences are visible.</summary>
		public bool ShowDeltaTimes;
		/// <summary>Whether elapsed time is visible.</summary>
		public bool ShowElapsedTime;
		/// <summary>Whether the source tree item is expanded.</summary>
		public bool IsExpanded;

		/// <summary>
		///    The list of data source Id's which are excluded from being displayed.
		/// </summary>
		/// <remarks>
		///    Only relevant for merged data sources.
		/// </remarks>
		public HashSet<DataSourceId> ExcludedDataSources;

		/// <summary>
		///     Uniquely identifies this data source amongst all others.
		/// </summary>
		public DataSourceId Id;

		/// <summary>The last viewed timestamp.</summary>
		public DateTime LastViewed;
		/// <summary>The enabled log levels.</summary>
		public LevelFlags LevelFilter;
		/// <summary>The configured source order.</summary>
		public int Order;

		/// <summary>
		///     Uniquely identifies this data-source's parent, if any.
		///     Set to <see cref="Guid.Empty" /> if this data source has no parent.
		/// </summary>
		public DataSourceId ParentId;

		/// <summary>
		///     The path to the folder which is watched over for log files.
		/// </summary>
		public string LogFileFolderPath;

		/// <summary>
		///     The regular expression used to select the log files to display.
		///     Only used in conjunction with <see cref="LogFileFolderPath"/>.
		/// </summary>
		public string LogFileSearchPattern;

		/// <summary>
		///     Used in conjunction with <see cref="LogFileFolderPath"/>.
		/// </summary>
		public bool Recursive;

		/// <summary>Selected original entry indices.</summary>
		public HashSet<LogLineIndex> SelectedLogLines;
		/// <summary>The saved search text.</summary>
		public string SearchTerm;
		/// <summary>The first visible original entry index.</summary>
		public LogLineIndex VisibleLogLine;
		/// <summary>The saved horizontal scroll offset.</summary>
		public double HorizontalOffset;

		/// <summary>
		///     A supplied by and probably only relevant to the user.
		/// </summary>
		public string UserDefinedName;

		/// <summary>How merged-source names are displayed.</summary>
		public DataSourceDisplayMode MergedDataSourceDisplayMode;

		/// <summary>The active quick-filter identities.</summary>
		public List<QuickFilterId> ActivatedQuickFilters => _activatedQuickFilters;

		/// <summary>
		///     A user defined name for this data source.
		/// </summary>
		public string DisplayName;

		/// <summary>
		/// 
		/// </summary>
		public CustomDataSourceId CustomDataSourceId;

		/// <summary>
		/// 
		/// </summary>
		public ICustomDataSourceConfiguration CustomDataSourceConfiguration;

		/// <summary>Creates a source configuration with the existing defaults.</summary>
		public DataSource()
		{
			Order = -1;

			_activatedQuickFilters = new List<QuickFilterId>();

			LevelFilter = LevelFlags.All;
			ColorByLevel = true;
			ShowLineNumbers = true;
			IsExpanded = true;
			SelectedLogLines = new HashSet<LogLineIndex>();
			VisibleLogLine = LogLineIndex.Invalid;
			ExcludedDataSources = new HashSet<DataSourceId>();
		}

		/// <summary>Creates a source configuration with the existing defaults.</summary>
		public DataSource(string file)
			: this()
		{
			File = file;
		}


		/// <inheritdoc />
		public override string ToString()
		{
			if (CustomDataSourceConfiguration != null)
				return string.Format("Custom ({0})", CustomDataSourceConfiguration);

			if (File == null)
				return string.Format("Merged ({0})", Id);

			return string.Format("{0} ({1})", File, Id);
		}

		/// <summary>Writes the existing XML attribute names and child elements.</summary>
		public void Save(XmlWriter writer)
		{
#if NET10_0_OR_GREATER
			var known = SettingsXml.SaveElement("datasource", SaveKnown);
			var element = _rawXml == null ? new XElement("datasource") : new XElement(_rawXml);
			foreach (var attribute in known.Attributes()) element.SetAttributeValue(attribute.Name, attribute.Value);
			element.Attribute("stringfilter")?.Remove();
			UpdateReferences(element, known, "activatedquickfilters", "quickfilter");
			UpdateReferences(element, known, "excludeddatasources", "datasource");
			SettingsXml.WriteContent(element, writer);
#else
			SaveKnown(writer);
#endif
		}

#if NET10_0_OR_GREATER
		private static void UpdateReferences(XElement element, XElement known, string name, string itemName)
		{
			var generated = known.Element(name);
			var containers = element.Elements(name).ToList();
			if (containers.Count == 0)
			{
				if (generated != null) element.Add(new XElement(generated));
				return;
			}
			foreach (var container in containers)
			{
				var slots = container.Elements(itemName).Where(item => item.Attribute("id") != null).ToList();
				var originals = slots.GroupBy(item => Guid.Parse((string)item.Attribute("id")))
					.ToDictionary(group => group.Key, group => new Queue<XElement>(group));
				var replacements = new Queue<XElement>();
				if (generated != null)
					foreach (var item in generated.Elements(itemName))
					{
						var id = Guid.Parse((string)item.Attribute("id"));
						var replacement = originals.TryGetValue(id, out var matches) && matches.Count > 0
							? new XElement(matches.Dequeue()) : new XElement(item);
						replacement.SetAttributeValue("id", item.Attribute("id").Value);
						replacements.Enqueue(replacement);
					}
				foreach (var slot in slots)
				{
					if (replacements.Count > 0) slot.ReplaceWith(replacements.Dequeue());
					else slot.Remove();
				}
				container.Add(replacements);
			}
		}
#endif

		private void SaveKnown(XmlWriter writer)
		{
			writer.WriteAttributeString("file", File);
			writer.WriteAttributeBool("followtail", FollowTail);
			writer.WriteAttributeBool("showlinenumbers", ShowLineNumbers);
			writer.WriteAttributeBool("showdeltatimes", ShowDeltaTimes);
			writer.WriteAttributeBool("showelapsedtime", ShowElapsedTime);
			writer.WriteAttributeString("searchterm", SearchTerm);
			writer.WriteAttributeEnum("levelfilter", LevelFilter);
			writer.WriteAttributeBool("colorbylevel", ColorByLevel);
			writer.WriteAttributeBool("hideemptylines", HideEmptyLines);
			writer.WriteAttributeBool("singleline", IsSingleLine);
			writer.WriteAttributeBool("expanded", IsExpanded);
			writer.WriteAttributeInt("visibleentryindex", (int) VisibleLogLine);
			writer.WriteAttribute("id", Id);
			writer.WriteAttribute("parentid", ParentId);
			writer.WriteAttributeString("logfilefolderpath", LogFileFolderPath);
			writer.WriteAttributeString("logfileregex", LogFileSearchPattern);
			writer.WriteAttributeBool("recursive", Recursive);
			writer.WriteAttributeDateTime("lastviewed", LastViewed);
			writer.WriteAttributeDouble("horizontaloffset", HorizontalOffset);
			writer.WriteAttributeEnum("mergeddatasourcedisplaymode", MergedDataSourceDisplayMode);
			writer.WriteAttributeString("displayname", DisplayName);

			writer.WriteStartElement("activatedquickfilters");
			foreach (QuickFilterId guid in ActivatedQuickFilters)
			{
				writer.WriteStartElement("quickfilter");
				writer.WriteAttribute("id", guid);
				writer.WriteEndElement();
			}
			writer.WriteEndElement();

			if (ExcludedDataSources.Count > 0)
			{
				writer.WriteStartElement("excludeddatasources");
				foreach (DataSourceId id in ExcludedDataSources)
				{
					writer.WriteStartElement("datasource");
					writer.WriteAttribute("id", id);
					writer.WriteEndElement();
				}
				writer.WriteEndElement();
			}
		}

		/// <summary>Restores configuration from the existing XML representation.</summary>
		public void Restore(XmlReader reader, out bool neededPatching)
		{
#if NET10_0_OR_GREATER
			var element = SettingsXml.ReadElement(reader);
			var candidate = Clone();
			using (var content = element.CreateReader())
			{
				content.MoveToContent();
				candidate.RestoreAttributes(content, out neededPatching);
			}
			foreach (var child in element.Elements())
			{
				if (child.Name == "activatedquickfilters")
				{
					var references = child.Elements("quickfilter").Where(item => item.Attribute("id") != null)
						.Select(item => new QuickFilterId(Guid.Parse((string)item.Attribute("id")))).ToList();
					candidate.ActivatedQuickFilters.Clear();
					candidate.ActivatedQuickFilters.AddRange(references);
				}
				else if (child.Name == "excludeddatasources")
				{
					var references = child.Elements("datasource").Where(item => item.Attribute("id") != null)
						.Select(item => new DataSourceId(Guid.Parse((string)item.Attribute("id")))).ToList();
					candidate.ExcludedDataSources.Clear();
					candidate.ExcludedDataSources.UnionWith(references);
				}
			}
			candidate._rawXml = element;
			CopyFrom(candidate);
#else
			RestoreAttributes(reader, out neededPatching);

			reader.MoveToContent();

			XmlReader subtree = reader.ReadSubtree();
			while (subtree.Read())
			{
				switch (subtree.Name)
				{
					case "activatedquickfilters":
						IEnumerable<QuickFilterId> filters = ReadActivatedQuickFilters(reader);
						ActivatedQuickFilters.Clear();
						ActivatedQuickFilters.AddRange(filters);
						break;

					case "excludeddatasources":
						var excluded = ReadExcludedDataSources(reader);
						ExcludedDataSources.Clear();
						ExcludedDataSources.UnionWith(excluded);
						break;
				}
			}
#endif
		}

		private void RestoreAttributes(XmlReader reader, out bool neededPatching)
		{
			int count = reader.AttributeCount;
			for (int i = 0; i < count; ++i)
			{
				reader.MoveToAttribute(i);
				switch (reader.Name)
				{
					case "file":
						File = reader.Value;
						break;

					case "followtail":
						FollowTail = reader.ReadContentAsBool();
						break;

					case "showlinenumbers":
						ShowLineNumbers = reader.ReadContentAsBool();
						break;

					case "showdeltatimes":
						ShowDeltaTimes = reader.ReadContentAsBool();
						break;

					case "showelapsedtime":
						ShowElapsedTime = reader.ReadContentAsBool();
						break;

					case "stringfilter":
					case "searchterm":
						SearchTerm = reader.Value;
						break;

					case "levelfilter":
						LevelFilter = reader.ReadContentAsEnum<LevelFlags>();
						break;

					case "colorbylevel":
						ColorByLevel = reader.ReadContentAsBool();
						break;

					case "hideemptylines":
						HideEmptyLines = reader.ReadContentAsBool();
						break;

					case "singleline":
						IsSingleLine = reader.ReadContentAsBool();
						break;

					case "expanded":
						IsExpanded = reader.ReadContentAsBool();
						break;

					case "visibleentryindex":
						VisibleLogLine = reader.ReadContentAsInt();
						break;

					case "id":
						Id = reader.ReadContentAsDataSourceId();
						break;

					case "parentid":
						ParentId = reader.ReadContentAsDataSourceId();
						break;

					case "logfilefolderpath":
						LogFileFolderPath = reader.ReadContentAsString();
						break;

					case "logfileregex":
						LogFileSearchPattern = reader.ReadContentAsString();
						break;

					case "recursive":
						Recursive = reader.ReadContentAsBoolean();
						break;

					case "lastviewed":
						LastViewed = reader.ReadContentAsDateTime2();
						break;

					case "horizontaloffset":
						HorizontalOffset = reader.ReadContentAsDouble2();
						break;

					case "mergeddatasourcedisplaymode":
						MergedDataSourceDisplayMode = reader.ReadContentAsEnum<DataSourceDisplayMode>();
						break;

					case "displayname":
						DisplayName = reader.ReadContentAsString();
						break;
				}
			}

			if (Id == DataSourceId.Empty)
			{
				Id = DataSourceId.CreateNew();
				Log.InfoFormat("Data Source '{0}' doesn't have an ID yet, setting it to: {1}",
				               File,
				               Id
					);
				neededPatching = true;
			}
			else
			{
				neededPatching = false;
			}

		}

		private IEnumerable<QuickFilterId> ReadActivatedQuickFilters(XmlReader reader)
		{
			var guids = new List<QuickFilterId>();
			XmlReader subtree = reader.ReadSubtree();

			while (subtree.Read())
			{
				switch (subtree.Name)
				{
					case "quickfilter":
						int count = reader.AttributeCount;
						for (int i = 0; i < count; ++i)
						{
							reader.MoveToAttribute(i);
							switch (reader.Name)
							{
								case "id":
									guids.Add(reader.ReadContentAsQuickFilterId());
									break;
							}
						}
						break;
				}
			}

			return guids;
		}

		private IEnumerable<DataSourceId> ReadExcludedDataSources(XmlReader reader)
		{
			var guids = new List<DataSourceId>();
			XmlReader subtree = reader.ReadSubtree();

			while (subtree.Read())
			{
				switch (subtree.Name)
				{
					case "datasource":
						int count = reader.AttributeCount;
						for (int i = 0; i < count; ++i)
						{
							reader.MoveToAttribute(i);
							switch (reader.Name)
							{
								case "id":
									guids.Add(reader.ReadContentAsDataSourceId());
									break;
							}
						}
						break;
				}
			}

			return guids;
		}

		/// <summary>Creates a copy with independent mutable collections and plugin configuration.</summary>
		public DataSource Clone()
		{
			var clone = new DataSource();
			clone.CopyFrom(this);
			return clone;
		}

		private void CopyFrom(DataSource other)
		{
			var configuration = (ICustomDataSourceConfiguration)other.CustomDataSourceConfiguration?.Clone();
#if NET10_0_OR_GREATER
			var rawXml = other._rawXml == null ? null : new XElement(other._rawXml);
#endif
			ColorByLevel = other.ColorByLevel;
			CustomDataSourceId = other.CustomDataSourceId;
			CustomDataSourceConfiguration = configuration;
			UserDefinedName = other.UserDefinedName;
			File = other.File;
			FollowTail = other.FollowTail;
			HideEmptyLines = other.HideEmptyLines;
			HorizontalOffset = other.HorizontalOffset;
			Id = other.Id;
			IsSingleLine = other.IsSingleLine;
			LastViewed = other.LastViewed;
			LevelFilter = other.LevelFilter;
			Order = other.Order;
			LogFileFolderPath = other.LogFileFolderPath;
			LogFileSearchPattern = other.LogFileSearchPattern;
			Recursive = other.Recursive;
			ParentId = other.ParentId;
			SearchTerm = other.SearchTerm;
			ShowLineNumbers = other.ShowLineNumbers;
			ShowDeltaTimes = other.ShowDeltaTimes;
			ShowElapsedTime = other.ShowElapsedTime;
			VisibleLogLine = other.VisibleLogLine;
			IsExpanded = other.IsExpanded;
			MergedDataSourceDisplayMode = other.MergedDataSourceDisplayMode;
			DisplayName = other.DisplayName;
			ActivatedQuickFilters.Clear();
			ActivatedQuickFilters.AddRange(other.ActivatedQuickFilters);
			SelectedLogLines.Clear();
			SelectedLogLines.UnionWith(other.SelectedLogLines);
			ExcludedDataSources.Clear();
			ExcludedDataSources.UnionWith(other.ExcludedDataSources);
#if NET10_0_OR_GREATER
			_rawXml = rawXml;
#endif
		}

		object ICloneable.Clone()
		{
			return Clone();
		}
	}
}
