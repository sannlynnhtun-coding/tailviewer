using System;
using System.Reflection;
using System.Text;
using System.Xml;
#if NET10_0_OR_GREATER
using System.Xml.Linq;
#endif
using log4net;
using Tailviewer.Api;
#if NET10_0_OR_GREATER
using PluginId = System.String;
#else
using Tailviewer.Archiver.Plugins;
#endif

namespace Tailviewer.Settings.CustomFormats
{
	/// <summary>A plugin-owned format definition using its stable persisted identity.</summary>
	public sealed class CustomLogFileFormat
		: ICustomLogFileFormat
	{
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		private PluginId _pluginId;
		private string _name;
		private string _format;
		private Encoding _encoding;
		private string _encodingName;
#if NET10_0_OR_GREATER
		private XElement _rawXml;
#endif

		/// <summary>Creates an empty format definition for restoration.</summary>
		public CustomLogFileFormat()
		{}

		/// <summary>Creates a format definition for the specified plugin identity.</summary>
		public CustomLogFileFormat(PluginId pluginId, string name, string format, Encoding encoding)
		{
			_pluginId = pluginId;
			_name = name;
			_format = format;
			_encoding = encoding;
		}

		/// <summary>
		///     The id of the plugin which is able to interpret this <see cref="Format"/>.
		/// </summary>
		public PluginId PluginId => _pluginId;

		/// <summary>Writes the existing custom-format XML attributes.</summary>
		public void Save(XmlWriter writer)
		{
#if NET10_0_OR_GREATER
			var element = _rawXml == null ? new XElement("customformat") : new XElement(_rawXml);
			element.SetAttributeValue("plugin", PluginId ?? string.Empty);
			element.SetAttributeValue("name", Name ?? string.Empty);
			element.SetAttributeValue("format", Format ?? string.Empty);
			element.SetAttributeValue("encoding", EncodingName);
			SettingsXml.WriteContent(element, writer);
#else
			writer.WriteAttributeString("plugin", PluginId?.ToString());
			writer.WriteAttributeString("name", Name);
			writer.WriteAttributeString("format", Format);
			writer.WriteAttributeString("encoding", EncodingName);
#endif
		}

		/// <summary>Creates an independent format definition, retaining opaque extension data.</summary>
		public CustomLogFileFormat Clone()
		{
			var clone = new CustomLogFileFormat(_pluginId, _name, _format, _encoding);
			clone._encodingName = _encodingName;
#if NET10_0_OR_GREATER
			clone._rawXml = _rawXml == null ? null : new XElement(_rawXml);
#endif
			return clone;
		}

		/// <summary>Edits known definition fields without discarding unavailable plugin data.</summary>
		public CustomLogFileFormat WithDefinition(string name, string format)
		{
			var changed = Clone();
			changed._name = name;
			changed._format = format;
			return changed;
		}

		/// <summary>Edits a definition and explicitly replaces or clears its encoding selection.</summary>
		public CustomLogFileFormat WithDefinition(string name, string format, Encoding encoding)
		{
			var changed = WithDefinition(name, format);
			changed._encoding = encoding;
			changed._encodingName = encoding?.WebName ?? string.Empty;
#if NET10_0_OR_GREATER
			changed._rawXml?.SetAttributeValue("encoding", encoding?.WebName ?? string.Empty);
#endif
			return changed;
		}

		#region Equality members

		private bool Equals(CustomLogFileFormat other)
		{
			return Equals(PluginId, other.PluginId) && string.Equals(Name, other.Name) && string.Equals(Format, other.Format) && Equals(Encoding, other.Encoding) && string.Equals(EncodingName, other.EncodingName, StringComparison.Ordinal);
		}

		/// <inheritdoc />
		public override bool Equals(object obj)
		{
			return ReferenceEquals(this, obj) || obj is CustomLogFileFormat other && Equals(other);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = (PluginId != null ? PluginId.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Format != null ? Format.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Encoding != null ? Encoding.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ EncodingName.GetHashCode();
				return hashCode;
			}
		}

		/// <inheritdoc />
		public static bool operator ==(CustomLogFileFormat left, CustomLogFileFormat right)
		{
			return Equals(left, right);
		}

		/// <inheritdoc />
		public static bool operator !=(CustomLogFileFormat left, CustomLogFileFormat right)
		{
			return !Equals(left, right);
		}

		#endregion

		/// <summary>Restores the existing custom-format XML attributes.</summary>
		public void Restore(XmlReader reader)
		{
#if NET10_0_OR_GREATER
			var element = SettingsXml.ReadElement(reader);
			var restored = new CustomLogFileFormat();
			using (var attributes = element.CreateReader())
			{
				attributes.MoveToContent();
				restored.RestoreKnown(attributes);
			}
			_pluginId = restored._pluginId;
			_name = restored._name;
			_format = restored._format;
			_encoding = restored._encoding;
			_encodingName = restored._encodingName;
			_rawXml = element;
		}

		private void RestoreKnown(XmlReader reader)
		{
#endif
			var count = reader.AttributeCount;
			for (var i = 0; i < count; ++i)
			{
				reader.MoveToAttribute(i);
				switch (reader.Name)
				{
					case "plugin":
#if NET10_0_OR_GREATER
						_pluginId = reader.ReadContentAsString();
#else
						_pluginId = new PluginId(reader.ReadContentAsString());
#endif
						break;

					case "name":
						_name = reader.ReadContentAsString();
						break;

					case "format":
						_format = reader.ReadContentAsString();
						break;

					case "encoding":
						var encodingName = reader.ReadContentAsString();
						_encodingName = encodingName;
						_encoding = null;
						if (!string.IsNullOrEmpty(encodingName))
							try
							{
#if NET10_0_OR_GREATER
								_encoding = SettingsEncoding.Resolve(encodingName);
#else
								_encoding = Encoding.GetEncoding(encodingName);
#endif
							}
							catch (Exception e)
							{
								Log.WarnFormat("Caught exception while trying to get encoding '{0}':\r\n{1}",
								               encodingName,
								               e);
							}

						break;
				}
			}
		}

		#region Implementation of ICustomLogFileFormat

		/// <inheritdoc />
		public string Name => _name;

		/// <inheritdoc />
		public string Format => _format;

		/// <summary>The resolved or unavailable encoding name; empty means automatic detection.</summary>
		public string EncodingName => _encoding?.WebName ?? _encodingName ?? string.Empty;

		/// <inheritdoc />
		public Encoding Encoding => _encoding;

		#endregion
	}
}
