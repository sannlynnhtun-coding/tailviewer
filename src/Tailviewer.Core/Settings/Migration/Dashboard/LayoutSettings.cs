using System.Collections.Generic;
using System.Xml;
#if NET10_0_OR_GREATER
using System.Linq;
using System.Xml.Linq;
#endif
using Tailviewer.Settings.Dashboard.Widgets;

namespace Tailviewer.Settings.Dashboard
{
	/// <summary>Legacy dashboard layout envelope.</summary>
	public sealed class LayoutSettings
		: List<WidgetSettings>
	{
#if NET10_0_OR_GREATER
		private XElement _rawXml;
#endif
		/// <summary>Restores the legacy configuration envelope.</summary>
		public void Restore(XmlReader reader)
		{
#if NET10_0_OR_GREATER
			var element = SettingsXml.ReadElement(reader);
			var widgets = new List<WidgetSettings>();
			foreach (var child in element.Elements("widget"))
			{
				var widget = new WidgetSettings();
				using (var content = child.CreateReader())
				{
					content.MoveToContent();
					widget.Restore(content);
				}
				widgets.Add(widget);
			}
			Clear();
			AddRange(widgets);
			_rawXml = element;
#else
			var widgets = new List<WidgetSettings>();
			XmlReader subtree = reader.ReadSubtree();

			while (subtree.Read())
			{
				switch (subtree.Name)
				{
					//case "layout": // "this"
					//	for (int i = 0; i < subtree.AttributeCount; ++i)
					//	{
					//		subtree.MoveToAttribute(i);
					//		switch (subtree.Name)
					//		{
					//		}
					//	}
					//	break;

					case "widget":
						var widget = new WidgetSettings();
						widget.Restore(reader);
						widgets.Add(widget);
						break;
				}
			}

			Clear();
			Capacity = widgets.Count;
			foreach (WidgetSettings widget in widgets)
			{
				Add(widget);
			}
#endif
		}

		/// <summary>Writes the legacy configuration envelope.</summary>
		public void Save(XmlWriter writer)
		{
#if NET10_0_OR_GREATER
			var element = _rawXml == null ? new XElement("layout") : new XElement(_rawXml);
			SettingsXml.ReplaceChildren(element, "widget",
				this.Select(widget => SettingsXml.SaveElement("widget", widget.Save)));
			SettingsXml.WriteContent(element, writer);
#else
			//writer.WriteAttributeGuid("selecteditem", SelectedItem);

			foreach (WidgetSettings widget in this)
			{
				writer.WriteStartElement("widget");
				widget.Save(writer);
				writer.WriteEndElement();
			}
#endif
		}
	}
}
