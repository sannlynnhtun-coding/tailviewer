using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
#if NET10_0_OR_GREATER
using System.Xml.Linq;
#endif
#if !NET10_0_OR_GREATER
using Metrolib;
#endif
using log4net;
using Tailviewer.Api;
using Tailviewer.Core;
using Tailviewer.Settings.CustomFormats;

namespace Tailviewer.Settings
{
	/// <summary>Ordered persisted source configurations and folder defaults.</summary>
	public sealed class DataSourceSettings
		: List<DataSource>
		, IDataSourcesSettings
		, ICloneable
	{
		private static readonly ILog Log =
			LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

#if NET10_0_OR_GREATER
		private XElement _rawXml;
#endif
		private string _pattern;
		private bool _isPinned;

		/// <summary>The selected configured source identity.</summary>
		public DataSourceId SelectedItem { get; set; }

		/// <summary>The patterns used when adding a folder source.</summary>
		public string FolderDataSourcePattern
		{
			get { return _pattern; }
			set { _pattern = value; }
		}

		/// <summary>Whether newly added folders include subdirectories.</summary>
		public bool FolderDataSourceRecursive { get; set; }

		/// <summary>Whether the source panel stays pinned.</summary>
		public bool IsPinned
		{
			get { return _isPinned; }
			set
			{
				_isPinned = value;
			}
		}

		object ICloneable.Clone()
		{
			return Clone();
		}

		/// <summary>Restores configuration from the existing XML representation.</summary>
		public void Restore(XmlReader reader, out bool neededPatching)
		{
#if NET10_0_OR_GREATER
			var element = SettingsXml.ReadElement(reader);
			var candidate = Clone();
			var interpreted = new XElement("datasources", element.Attributes(), element.Elements("datasource"));
			using (var content = interpreted.CreateReader())
			{
				content.MoveToContent();
				candidate.RestoreKnown(content, out neededPatching);
			}
			Clear();
			AddRange(candidate);
			SelectedItem = candidate.SelectedItem;
			FolderDataSourceRecursive = candidate.FolderDataSourceRecursive;
			FolderDataSourcePattern = candidate.FolderDataSourcePattern;
			IsPinned = candidate.IsPinned;
			_rawXml = element;
#else
			RestoreKnown(reader, out neededPatching);
#endif
		}

		private void RestoreKnown(XmlReader reader, out bool neededPatching)
		{
			neededPatching = false;
			var dataSources = new List<DataSource>();
			var subtree = reader.ReadSubtree();
			var selectedItem = DataSourceId.Empty;

			// Defaults, will be overwritten if present in the xml document
			FolderDataSourceRecursive = true;
			FolderDataSourcePattern = "*.txt;*.log";

			while (subtree.Read())
				switch (subtree.Name)
				{
					case "datasources": // "this"
						for (var i = 0; i < subtree.AttributeCount; ++i)
						{
							subtree.MoveToAttribute(i);
							switch (subtree.Name)
							{
								case "selecteditem":
									selectedItem = subtree.ReadContentAsDataSourceId();
									break;

								case "folderdatasourcerecursive":
									FolderDataSourceRecursive = subtree.ReadContentAsBoolean();
									break;

								case "folderdatasourcepattern":
									FolderDataSourcePattern = subtree.ReadContentAsString();
									break;

								case "ispinned":
									IsPinned = subtree.ReadContentAsBoolean();
									break;
							}
						}
						break;

					case "datasource":
						var dataSource = new DataSource();
						bool sourceNeedsPatching;
						dataSource.Restore(subtree, out sourceNeedsPatching);
						dataSources.Add(dataSource);
						neededPatching |= sourceNeedsPatching;
						break;
				}

			Clear();
			Capacity = dataSources.Count;
			SelectedItem = DataSourceId.Empty;
			foreach (var source in dataSources)
			{
				Add(source);

				if (source.Id == selectedItem)
					SelectedItem = selectedItem;
			}

			if (SelectedItem != selectedItem || selectedItem == DataSourceId.Empty)
			{
				Log.WarnFormat("Selected item '{0}' not found in data-sources, ignoring it...", selectedItem);
				if (Count > 0)
					SelectedItem = this[0].Id;
			}
		}

		/// <summary>Writes the existing XML attribute names and child elements.</summary>
		public void Save(XmlWriter writer)
		{
#if NET10_0_OR_GREATER
			var known = SettingsXml.SaveElement("datasources", SaveKnown);
			var element = _rawXml == null ? new XElement("datasources") : new XElement(_rawXml);
			foreach (var attribute in known.Attributes()) element.SetAttributeValue(attribute.Name, attribute.Value);
			SettingsXml.ReplaceChildren(element, "datasource", known.Elements("datasource"));
			SettingsXml.WriteContent(element, writer);
#else
			SaveKnown(writer);
#endif
		}

		private void SaveKnown(XmlWriter writer)
		{
			writer.WriteAttribute("selecteditem", SelectedItem);
			writer.WriteAttributeBool("ispinned", _isPinned);
			writer.WriteAttributeBool("folderdatasourcerecursive", FolderDataSourceRecursive);
			writer.WriteAttributeString("folderdatasourcepattern", _pattern);

			foreach (var dataSource in this)
			{
				writer.WriteStartElement("datasource");
				dataSource.Save(writer);
				writer.WriteEndElement();
			}
		}

		/// <summary>Creates a copy with independent source configurations.</summary>
		public DataSourceSettings Clone()
		{
			var clone = new DataSourceSettings();
			clone.AddRange(this.Select(x => x.Clone()));
			clone.SelectedItem = SelectedItem;
			clone.FolderDataSourceRecursive = FolderDataSourceRecursive;
			clone.FolderDataSourcePattern = FolderDataSourcePattern;
			clone.IsPinned = IsPinned;
#if NET10_0_OR_GREATER
			clone._rawXml = _rawXml == null ? null : new XElement(_rawXml);
#endif
			return clone;
		}

		/// <summary>
		///     Moves the element <paramref name="dataSource" /> to appear *anchor*
		///     <paramref name="anchor" />. Does nothing if this constraint doesn't
		///     exist of if either are not part of this list.
		/// </summary>
		/// <param name="dataSource"></param>
		/// <param name="anchor"></param>
		public void MoveBefore(DataSource dataSource, DataSource anchor)
		{
			int dataSourceIndex = IndexOf(dataSource);
			int anchorIndex = IndexOf(anchor);
			if (dataSourceIndex != -1 && anchorIndex != -1)
			{
				// The required constraint already is true: dataSource is before 'anchor'
				if (dataSourceIndex < anchorIndex)
					return;

				RemoveAt(dataSourceIndex);
				Insert(anchorIndex, dataSource);
			}
		}
	}
}
