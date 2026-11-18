using System;
using System.Globalization;
#if NET10_0_OR_GREATER
using Color = System.Drawing.Color;
using Colors = System.Drawing.Color;
#else
using System.Windows.Media;
#endif
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
#if NET10_0_OR_GREATER
					value = Parse(content);
#else
					value = (Color) ColorConverter.ConvertFromString(content ?? string.Empty);
#endif
				}
				catch (Exception e)
				{
					log.WarnFormat("Cannot parse value '{0}' as a color, restoring default value for attribute '{1}' instead:\r\n{2}",
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
#if NET10_0_OR_GREATER
			var colorVlaue = "#" + unchecked((uint)color.ToArgb()).ToString("X8", CultureInfo.InvariantCulture);
#else
			var colorVlaue = color.ToString();
#endif
			writer.WriteAttributeString(localName, colorVlaue);
		}
#if NET10_0_OR_GREATER
        internal static Color Parse(string text)
        {
            text = (text ?? string.Empty).Trim();
            if (text.StartsWith("#", StringComparison.Ordinal))
            {
                var hex = text.Substring(1);
                if (hex.Length == 3 || hex.Length == 4)
                    hex = string.Concat(System.Linq.Enumerable.Select(hex, c => new string(c, 2)));
                if (hex.Length == 6) hex = "FF" + hex;
                if (hex.Length == 8 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
                    return Color.FromArgb(unchecked((int)argb));
            }
            else if (text.StartsWith("sc#", StringComparison.Ordinal))
            {
                var parts = text.Substring(3).Split(',');
                if (parts.Length != 3 && parts.Length != 4) throw new FormatException("Invalid scRGB component count.");
                var values = Array.ConvertAll(parts, p => float.Parse(p, CultureInfo.InvariantCulture));
                if (Array.Exists(values, v => !float.IsFinite(v))) throw new FormatException("Invalid scRGB component.");
                var offset = parts.Length == 4 ? 1 : 0;
                var alpha = offset == 0 ? 255 : (int)(Math.Clamp(values[0], 0f, 1f) * 255f + 0.5f);
                return Color.FromArgb(alpha, ToSrgb(values[offset]), ToSrgb(values[offset + 1]), ToSrgb(values[offset + 2]));
            }
            else
            {
                var named = Color.FromName(text);
                if (named.IsKnownColor && !named.IsSystemColor) return named;
            }
            throw new FormatException("Invalid settings color.");
        }

        private static int ToSrgb(float value)
        {
            if (value <= 0) return 0;
            if (value <= 0.0031308f) return (int)(255f * value * 12.92f + 0.5f);
            if (value < 1) return (int)(255f * (1.055f * Math.Pow(value, 1.0 / 2.4) - 0.055f) + 0.5f);
            return 255;
        }
#endif

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
