using System.Collections.Generic;
using System.Linq;
#if NET10_0_OR_GREATER
using System.Xml.Linq;
#endif
using System.Xml;

namespace Tailviewer.Settings.CustomFormats
{
	internal sealed class CustomFormatsSettings
		: List<CustomLogFileFormat>
		, ICustomFormatsSettings
	{
#if NET10_0_OR_GREATER
		private XElement _rawXml;
#endif

		public CustomFormatsSettings()
		{ }

		private CustomFormatsSettings(CustomFormatsSettings customFormatsSettings)
			: base(customFormatsSettings.Select(x => x.Clone()))
		{
#if NET10_0_OR_GREATER
			_rawXml = customFormatsSettings._rawXml == null ? null : new XElement(customFormatsSettings._rawXml);
#endif
		}

		public CustomFormatsSettings Clone()
		{
			return new CustomFormatsSettings(this);
		}

		public void Save(XmlWriter writer)
		{
#if NET10_0_OR_GREATER
			var element = _rawXml == null ? new XElement("customformats") : new XElement(_rawXml);
			var replacements = new Queue<XElement>(this.Select(item =>
			{
				var document = new XDocument();
				using (var content = document.CreateWriter())
				{
					content.WriteStartElement("customformat");
					item.Save(content);
					content.WriteEndElement();
				}
				return document.Root;
			}));
			foreach (var child in element.Elements("customformat").ToList())
			{
				if (replacements.Count > 0) child.ReplaceWith(replacements.Dequeue());
				else child.Remove();
			}
			element.Add(replacements);
			SettingsXml.WriteContent(element, writer);
#else
			foreach (var customLogFileFormat in this)
			{
				writer.WriteStartElement("customformat");
				customLogFileFormat.Save(writer);
				writer.WriteEndElement();
			}
#endif
		}

		public void Restore(XmlReader reader)
		{
#if NET10_0_OR_GREATER
			var element = SettingsXml.ReadElement(reader);
			var formats = new List<CustomLogFileFormat>();
			foreach (var child in element.Elements("customformat"))
			{
				var format = new CustomLogFileFormat();
				using (var content = child.CreateReader())
				{
					content.MoveToContent();
					format.Restore(content);
				}
				formats.Add(format);
			}
			Clear();
			AddRange(formats);
			_rawXml = element;
#else
			var customFormats = new List<CustomLogFileFormat>();
			var subtree = reader.ReadSubtree();
			while (subtree.Read())
				switch (subtree.Name)
				{
					case "customformat":
						var dataSource = new CustomLogFileFormat();
						dataSource.Restore(subtree);
						customFormats.Add(dataSource);
						break;
				}

			Clear();
			Capacity = customFormats.Count;
			foreach (var source in customFormats)
			{
				Add(source);
			}
#endif
		}
	}
}
