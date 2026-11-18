using System;
using System.Xml;
#if NET10_0_OR_GREATER
using System.Xml.Linq;
#endif
#if !NET10_0_OR_GREATER
using Metrolib;
#endif
using Tailviewer.Api;

namespace Tailviewer.Settings.Bookmarks
{
	/// <summary>A persisted bookmark referencing its source identity and original entry index.</summary>
	public sealed class BookmarkSettings
		: ICloneable
	{
#if NET10_0_OR_GREATER
		private XElement _rawXml;
#endif
		/// <summary>The bookmarked source identity.</summary>
		public DataSourceId DataSourceId;
		/// <summary>The bookmarked original entry index.</summary>
		public LogLineIndex Index;

		/// <summary>Creates a bookmark referring to its source and entry.</summary>
		public BookmarkSettings()
		{}

		/// <summary>Creates a bookmark referring to its source and entry.</summary>
		public BookmarkSettings(DataSourceId dataSourceId, LogLineIndex index)
		{
			DataSourceId = dataSourceId;
			Index = index;
		}

		/// <summary>Writes the existing XML attribute names and child elements.</summary>
		public void Save(XmlWriter writer)
		{
#if NET10_0_OR_GREATER
			var element = _rawXml == null ? new XElement("bookmark") : new XElement(_rawXml);
			element.SetAttributeValue("datasource", DataSourceId.Value);
			element.SetAttributeValue("index", Index.Value);
			SettingsXml.WriteContent(element, writer);
#else
			writer.WriteAttributeGuid("datasource", DataSourceId.Value);
			writer.WriteAttributeInt("index", Index.Value);
#endif
		}

		/// <summary>Restores configuration from the existing XML representation.</summary>
		public void Restore(XmlReader reader)
		{
#if NET10_0_OR_GREATER
			var element = SettingsXml.ReadElement(reader);
			var candidate = new BookmarkSettings();
			using (var content = element.CreateReader())
			{
				content.MoveToContent();
				candidate.RestoreKnown(content);
			}
			DataSourceId = candidate.DataSourceId;
			Index = candidate.Index;
			_rawXml = element;
#else
			RestoreKnown(reader);
#endif
		}

		private void RestoreKnown(XmlReader reader)
		{
			for (var i = 0; i < reader.AttributeCount; ++i)
			{
				reader.MoveToAttribute(i);
				switch (reader.Name)
				{
					case "datasource":
						DataSourceId = new DataSourceId(reader.ReadContentAsGuid());
						break;

					case "index":
						Index = reader.ReadContentAsInt();
						break;
				}
			}
		}

		#region Equality members

		private bool Equals(BookmarkSettings other)
		{
			return DataSourceId.Equals(other.DataSourceId) && Index.Equals(other.Index);
		}

		/// <inheritdoc />
		public override bool Equals(object obj)
		{
			if (ReferenceEquals(objA: null, objB: obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj is BookmarkSettings && Equals((BookmarkSettings) obj);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			unchecked
			{
				return (DataSourceId.GetHashCode() * 397) ^ Index.GetHashCode();
			}
		}

		/// <summary>Creates a copy with the same source and entry identity.</summary>
		public BookmarkSettings Clone()
		{
			var clone = new BookmarkSettings(DataSourceId, Index);
#if NET10_0_OR_GREATER
			clone._rawXml = _rawXml == null ? null : new XElement(_rawXml);
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
