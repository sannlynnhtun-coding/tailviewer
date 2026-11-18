using System;
using System.Windows.Media;
using System.Xml;
using log4net;

namespace Tailviewer.Settings
{
	internal static class ColorXmlExtensions
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="reader"></param>
		/// <param name="attributeName"></param>
		/// <param name="log"></param>
		/// <param name="defaultValue"></param>
		/// <param name="value"></param>
		public static void ReadAttributeAsColor(this XmlReader reader, string attributeName, ILog log, Color defaultValue, out Color value)
		{
			if (!TryMoveToAttribute(reader, attributeName, log))
			{
				value = defaultValue;
			}
			else
			{
				var content = reader.ReadContentAsString();
				try
				{
					value = (Color) ColorConverter.ConvertFromString(content ?? string.Empty);
				}
				catch (Exception e)
				{
					log.WarnFormat("Cannot parse value '{0}' as an integer, restoring default value for attribute '{1}' instead:\r\n{2}",
					               content,
					               attributeName,
					               e);
					value = defaultValue;
				}
			}
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="writer"></param>
		/// <param name="localName"></param>
		/// <param name="color"></param>
		public static void WriteAttributeColor(this XmlWriter writer, string localName, Color color)
		{
			var colorVlaue = color.ToString();
			writer.WriteAttributeString(localName, colorVlaue);
		}

		private static bool TryMoveToAttribute(this XmlReader reader, string attributeName, ILog log)
		{
			if (!reader.MoveToAttribute(attributeName))
			{
				log.InfoFormat("Cannot find attribute '{0}', restoring default value instead", attributeName);
				return false;
			}

			return true;
		}
	}
}
