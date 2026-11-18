using System.Reflection;
#if NET10_0_OR_GREATER
using Color = System.Drawing.Color;
using Colors = System.Drawing.Color;
#else
using System.Windows.Media;
#endif
using System.Xml;
using log4net;
using Tailviewer.Core;

namespace Tailviewer.Settings
{
	/// <summary>
	///     The (display) settings concerning a particular log level.
	/// </summary>
	public sealed class LogLevelSettings
	{
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private Color _foregroundColor;
		private Color _backgroundColor;
#if NET10_0_OR_GREATER
		private string _foregroundXml;
		private string _backgroundXml;
#endif

		/// <summary>Creates black text on a transparent background.</summary>
		public LogLevelSettings()
		{
			_foregroundColor = Colors.Black;
			_backgroundColor = Colors.Transparent;
		}

		/// <summary>
		///     The color of the foreground (e.g. the text) of a line with that log level.
		/// </summary>
		public Color ForegroundColor
		{
			get { return _foregroundColor; }
			set { _foregroundColor = value;
#if NET10_0_OR_GREATER
				_foregroundXml = null;
#endif
			}
		}

		/// <summary>
		///     The color of the background of a line with that log level.
		/// </summary>
		public Color BackgroundColor
		{
			get { return _backgroundColor; }
			set { _backgroundColor = value;
#if NET10_0_OR_GREATER
				_backgroundXml = null;
#endif
			}
		}

		/// <summary>Creates an independent settings copy.</summary>
		public LogLevelSettings Clone()
		{
			return new LogLevelSettings
			{
				ForegroundColor = _foregroundColor,
				BackgroundColor = _backgroundColor,
#if NET10_0_OR_GREATER
				_foregroundXml = _foregroundXml,
				_backgroundXml = _backgroundXml,
#endif
			};
		}

		/// <summary>Writes the existing XML attributes and level elements.</summary>
		public void Save(XmlWriter writer)
		{
#if NET10_0_OR_GREATER
			if (_foregroundXml != null) writer.WriteAttributeString("foregroundcolor", _foregroundXml);
			else
#endif
			writer.WriteAttributeColor("foregroundcolor", _foregroundColor);
#if NET10_0_OR_GREATER
			if (_backgroundXml != null) writer.WriteAttributeString("backgroundcolor", _backgroundXml);
			else
#endif
			writer.WriteAttributeColor("backgroundcolor", _backgroundColor);
		}

		/// <summary>Restores values and retains defaults for invalid values.</summary>
		public void Restore(XmlReader reader)
		{
#if NET10_0_OR_GREATER
			_foregroundXml = reader.GetAttribute("foregroundcolor");
			_backgroundXml = reader.GetAttribute("backgroundcolor");
#endif
			reader.ReadAttributeAsColor("foregroundcolor", Log, _foregroundColor, out _foregroundColor);
			reader.ReadAttributeAsColor("backgroundcolor", Log, _backgroundColor, out _backgroundColor);
		}
	}
}
