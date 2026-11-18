using System;
using System.Diagnostics.Contracts;
using System.Xml;
#if NET10_0_OR_GREATER
using System.Xml.Linq;
using Tailviewer.Settings;
#endif
using Tailviewer.Api;

// ReSharper disable once CheckNamespace
namespace Tailviewer.Core
{
	/// <summary>
	///     The configuration of an application-wide quick filter.
	/// </summary>
	public sealed class QuickFilterSettings
		: ICloneable
		, ISerializableType
	{
#if NET10_0_OR_GREATER
		private XElement _rawXml;
#endif

		/// <summary>
		///     The id of this quick filter.
		///     Is used to define for each data source which quick filter is active or not.
		/// </summary>
		public QuickFilterId Id;

		/// <summary>
		///     True when the case of the filter value doesn't matter.
		/// </summary>
		public bool IgnoreCase;

		/// <summary>
		///     When set to false, then a line will only be shown if it matches the filter.
		///     When set to true, then only those lines NOT matching the filter will be shown.
		/// </summary>
		public bool IsInverted;

		/// <summary>
		///     How <see cref="Value" /> is to be intepreted.
		/// </summary>
		public FilterMatchType MatchType;

		/// <summary>
		///     The actual filter value, <see cref="MatchType" /> defines how it is interpreted.
		/// </summary>
		public string Value;

		/// <summary>
		///     Initializes this quick filter.
		/// </summary>
		public QuickFilterSettings()
		{
			Id = QuickFilterId.CreateNew();
			IgnoreCase = true;
			IsInverted = false;
		}

		object ICloneable.Clone()
		{
			return Clone();
		}

		/// <summary>
		///     Restores this filter from the given xml reader.
		/// </summary>
		/// <param name="reader"></param>
		/// <returns></returns>
		public bool Restore(XmlReader reader)
		{
#if NET10_0_OR_GREATER
			var element = SettingsXml.ReadElement(reader);
			var candidate = Clone();
			using (var content = element.CreateReader())
			{
				content.MoveToContent();
				if (!candidate.RestoreKnown(content)) return false;
			}
			candidate._rawXml = element;
			CopyFrom(candidate);
			return true;
#else
			return RestoreKnown(reader);
#endif
		}

#if NET10_0_OR_GREATER
		private void CopyFrom(QuickFilterSettings other)
		{
			Id = other.Id;
			IgnoreCase = other.IgnoreCase;
			IsInverted = other.IsInverted;
			MatchType = other.MatchType;
			Value = other.Value;
			_rawXml = other._rawXml == null ? null : new XElement(other._rawXml);
		}
#endif

		private bool RestoreKnown(XmlReader reader)
		{
			var count = reader.AttributeCount;
			for (var i = 0; i < count; ++i)
			{
				reader.MoveToAttribute(i);

				switch (reader.Name)
				{
					case "id":
						Id = reader.ReadContentAsQuickFilterId();
						break;

					case "type":
						MatchType = (FilterMatchType)Enum.Parse(typeof(FilterMatchType), reader.Value);
						break;

					case "value":
						Value = reader.Value;
						break;

					case "ignorecase":
						IgnoreCase = string.Equals(reader.Value, "true", StringComparison.InvariantCultureIgnoreCase);
						break;

					case "isinclude":
						IsInverted = string.Equals(reader.Value, "true", StringComparison.InvariantCultureIgnoreCase);
						break;
				}
			}

			if (Id == QuickFilterId.Empty)
				return false;

			return true;
		}

		/// <summary>
		///     Saves the contents of this object into the given writer.
		/// </summary>
		/// <param name="writer"></param>
		public void Save(XmlWriter writer)
		{
#if NET10_0_OR_GREATER
			var element = _rawXml == null ? new XElement("quickfilter") : new XElement(_rawXml);
			element.SetAttributeValue("id", Id.Value);
			element.SetAttributeValue("type", MatchType.ToString());
			element.SetAttributeValue("value", Value ?? "");
			element.SetAttributeValue("ignorecase", XmlConvert.ToString(IgnoreCase));
			element.SetAttributeValue("isinclude", XmlConvert.ToString(IsInverted));
			SettingsXml.WriteContent(element, writer);
#else
			writer.WriteAttribute("id", Id);
			writer.WriteAttributeString("type", MatchType.ToString());
			writer.WriteAttributeString("value", Value);
			writer.WriteAttributeString("ignorecase", XmlConvert.ToString(IgnoreCase));
			writer.WriteAttributeString("isinclude", XmlConvert.ToString(IsInverted));
#endif
		}

		/// <summary>
		///     Creates a deep clone of this object.
		/// </summary>
		/// <returns></returns>
		public QuickFilterSettings Clone()
		{
			return new QuickFilterSettings
			{
#if NET10_0_OR_GREATER
				_rawXml = _rawXml == null ? null : new XElement(_rawXml),
#endif
				Id = Id,
				IgnoreCase = IgnoreCase,
				IsInverted = IsInverted,
				MatchType = MatchType,
				Value = Value
			};
		}

		/// <summary>
		///     Tests if this filter and the given one would produce the same result
		///     for the same data.
		/// </summary>
		/// <param name="other"></param>
		/// <returns>True when there is no doubt that the two filters perform identical, false otherwise</returns>
		public bool IsEquivalent(QuickFilterSettings other)
		{
			if (ReferenceEquals(other, objB: null))
				return false;

			// We won't need to include the id because it doesn't have
			// any influence on the outcome of a filter operation.

			if (IgnoreCase != other.IgnoreCase)
				return false;

			if (IsInverted != other.IsInverted)
				return false;

			if (MatchType != other.MatchType)
				return false;

			if (!Equals(Value, other.Value))
				return false;

			return true;
		}

		/// <summary>
		///     Creates a new <see cref="ILogEntryFilter" /> which behaves just like this quick filter is
		///     configured. Any further changes to this object will NOT influence the returned filter:
		///     This method must be called and the newly returned filter be used instead.
		/// </summary>
		/// <returns></returns>
		[Pure]
		public ILogEntryFilter CreateFilter()
		{
			return Filter.Create(Value, MatchType, IgnoreCase, IsInverted);
		}

		/// <inheritdoc />
		public void Serialize(IWriter writer)
		{
			writer.WriteAttribute("Id", Id);
			writer.WriteAttributeEnum("Type", MatchType);
			writer.WriteAttribute("Value", Value);
			writer.WriteAttribute("IgnoreCase", IgnoreCase);
			writer.WriteAttribute("IsInverted", IsInverted);
		}

		/// <inheritdoc />
		public void Deserialize(IReader reader)
		{
			reader.TryReadAttribute("Id", out Id);
			reader.TryReadAttributeEnum("Type", out MatchType);
			reader.TryReadAttribute("Value", out Value);
			reader.TryReadAttribute("IgnoreCase", out IgnoreCase);
			reader.TryReadAttribute("IsInverted", out IsInverted);
		}
	}
}
