using System.Xml;
#if NET10_0_OR_GREATER
using System.Xml.Linq;
#endif

namespace Tailviewer.Settings.Dashboard.Widgets
{
	/// <summary>Legacy dashboard widget configuration envelope.</summary>
	public sealed class WidgetSettings
	{
#if NET10_0_OR_GREATER
		private XElement _rawXml;
#endif
		/// <summary>Restores the legacy configuration envelope.</summary>
		public void Restore(XmlReader reader)
		{
#if NET10_0_OR_GREATER
			_rawXml = SettingsXml.ReadElement(reader);
#endif
		}

		/// <summary>Writes the legacy configuration envelope.</summary>
		public void Save(XmlWriter writer)
		{
#if NET10_0_OR_GREATER
			if (_rawXml != null) SettingsXml.WriteContent(_rawXml, writer);
#endif
		}
	}
}
