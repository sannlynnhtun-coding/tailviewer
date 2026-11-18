using log4net;
#if !NET10_0_OR_GREATER
using Metrolib;
#endif
using System;
using System.Diagnostics.Contracts;
using System.Reflection;
#if NET10_0_OR_GREATER
using Color = System.Drawing.Color;
using Colors = System.Drawing.Color;
#else
using System.Windows.Media;
#endif
using System.Xml;
using Tailviewer.Core;

namespace Tailviewer.Settings
{
	/// <summary>Global log viewer preferences and per-level colors.</summary>
	public sealed class LogViewerSettings
		: ILogViewerSettings
		, ICloneable
	{
		/// <summary>The original default LinesScrolledPerWheelTick value.</summary>
		public const int DefaultLinesScrolledPerWheelTick = 2;
		/// <summary>The original default FontSize value.</summary>
		public const int DefaultFontSize = 12;
		/// <summary>The original default TabWidth value.</summary>
		public const int DefaultTabWidth = 4;

		/// <summary>The original Other color palette.</summary>
		public static LogLevelSettings DefaultOther => new LogLevelSettings
		{
			ForegroundColor = Colors.Black,
			BackgroundColor = Colors.Transparent
		};

		/// <summary>The original Trace color palette.</summary>
		public static LogLevelSettings DefaultTrace => new LogLevelSettings
		{
			ForegroundColor = Color.FromArgb(255, 128, 128, 128),
			BackgroundColor = Colors.Transparent
		};

		/// <summary>The original Debug color palette.</summary>
		public static LogLevelSettings DefaultDebug => new LogLevelSettings
		{
			ForegroundColor = Color.FromArgb(255, 128, 128, 128),
			BackgroundColor = Colors.Transparent
		};

		/// <summary>The original Info color palette.</summary>
		public static LogLevelSettings DefaultInfo => new LogLevelSettings
		{
			ForegroundColor = Colors.Black,
			BackgroundColor = Colors.Transparent
		};

		/// <summary>The original Warning color palette.</summary>
		public static LogLevelSettings DefaultWarning => new LogLevelSettings
		{
			ForegroundColor = Colors.White,
			BackgroundColor = Color.FromArgb(255, 255, 195, 0)
		};

		/// <summary>The original Error color palette.</summary>
		public static LogLevelSettings DefaultError => new LogLevelSettings
		{
			ForegroundColor = Colors.White,
			BackgroundColor = Color.FromArgb(255, 232, 17, 35)
		};

		/// <summary>The original Fatal color palette.</summary>
		public static LogLevelSettings DefaultFatal => new LogLevelSettings
		{
			ForegroundColor = Colors.White,
			BackgroundColor = Color.FromArgb(255, 232, 17, 35)
		};

		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private int _linesScrolledPerWheelTick;
		private int _fontSize;
		private int _tabWidth;
		private LogLevelSettings _other;
		private LogLevelSettings _trace;
		private LogLevelSettings _debug;
		private LogLevelSettings _info;
		private LogLevelSettings _warning;
		private LogLevelSettings _error;
		private LogLevelSettings _fatal;

		/// <summary>Creates settings with the original defaults.</summary>
		public LogViewerSettings()
		{
			_linesScrolledPerWheelTick = DefaultLinesScrolledPerWheelTick;
			_fontSize = DefaultFontSize;
			_tabWidth = DefaultTabWidth;
			_other = DefaultOther;
			_trace = DefaultTrace;
			_debug = DefaultDebug;
			_info = DefaultInfo;
			_warning = DefaultWarning;
			_error = DefaultError;
			_fatal = DefaultFatal;
		}

		/// <inheritdoc />
		public int LinesScrolledPerWheelTick
		{
			get { return _linesScrolledPerWheelTick; }
			set { _linesScrolledPerWheelTick = value; }
		}

		/// <inheritdoc />
		public int FontSize
		{
			get { return _fontSize; }
			set { _fontSize = value; }
		}

		/// <inheritdoc />
		public int TabWidth
		{
			get { return _tabWidth; }
			set { _tabWidth = value; }
		}

		/// <inheritdoc />
		public LogLevelSettings Other
		{
			get { return _other; }
		}

		/// <inheritdoc />
		public LogLevelSettings Trace
		{
			get { return _trace; }
		}

		/// <inheritdoc />
		public LogLevelSettings Debug
		{
			get { return _debug; }
		}

		/// <inheritdoc />
		public LogLevelSettings Info
		{
			get { return _info; }
		}

		/// <inheritdoc />
		public LogLevelSettings Warning
		{
			get { return _warning; }
		}

		/// <inheritdoc />
		public LogLevelSettings Error
		{
			get { return _error; }
		}

		/// <inheritdoc />
		public LogLevelSettings Fatal
		{
			get { return _fatal; }
		}

		/// <summary>Writes the existing XML attributes and level elements.</summary>
		public void Save(XmlWriter writer)
		{
			writer.WriteAttributeInt("linesscrolledperwheeltick", _linesScrolledPerWheelTick);
			writer.WriteAttributeInt("fontsize", _fontSize);
			writer.WriteAttributeInt("tabwidth", _tabWidth);

			writer.WriteStartElement("other");
			_other.Save(writer);
			writer.WriteEndElement();

			writer.WriteStartElement("trace");
			_trace.Save(writer);
			writer.WriteEndElement();

			writer.WriteStartElement("debug");
			_debug.Save(writer);
			writer.WriteEndElement();

			writer.WriteStartElement("info");
			_info.Save(writer);
			writer.WriteEndElement();

			writer.WriteStartElement("warning");
			_warning.Save(writer);
			writer.WriteEndElement();

			writer.WriteStartElement("error");
			_error.Save(writer);
			writer.WriteEndElement();

			writer.WriteStartElement("fatal");
			_fatal.Save(writer);
			writer.WriteEndElement();
		}

		/// <summary>Restores values and retains defaults for invalid values.</summary>
		public void Restore(XmlReader reader)
		{
			reader.ReadAttributeAsInt("linesscrolledperwheeltick", Log, DefaultLinesScrolledPerWheelTick, out _linesScrolledPerWheelTick);
			if (_linesScrolledPerWheelTick < 1)
			{
				Log.WarnFormat("Attribute 'linesscrolledperwheeltick' must be 1 or greater, restoring value to default...");
				_linesScrolledPerWheelTick = DefaultLinesScrolledPerWheelTick;
			}

			reader.ReadAttributeAsInt("fontsize", Log, DefaultFontSize, out _fontSize);
			if (_fontSize < 1)
			{
				Log.WarnFormat("Attribute 'fontsize' must be 1 or greater, restoring value to default...");
				_fontSize = DefaultFontSize;
			}

			reader.ReadAttributeAsInt("tabwidth", Log, DefaultTabWidth, out _tabWidth);
			if (_tabWidth < 1)
			{
				Log.WarnFormat("Attribute 'tabwidth' must be 1 or greater, restoring value to default...");
				_tabWidth = DefaultTabWidth;
			}

			reader.MoveToElement();
			var subtree = reader.ReadSubtree();
			while (subtree.Read())
			{
#if NET10_0_OR_GREATER
				if (subtree.NodeType != XmlNodeType.Element || subtree.Depth != 1 || subtree.NamespaceURI.Length != 0) continue;
#endif
				switch (subtree.Name)
				{
					case "other":
						_other.Restore(subtree);
						break;

					case "trace":
						_trace.Restore(subtree);
						break;

					case "debug":
						_debug.Restore(subtree);
						break;

					case "info":
						_info.Restore(subtree);
						break;

					case "warning":
						_warning.Restore(subtree);
						break;

					case "error":
						_error.Restore(subtree);
						break;

					case "fatal":
						_fatal.Restore(subtree);
						break;
				}
			}
		}

		/// <summary>Creates an independent settings copy.</summary>
		[Pure]
		public LogViewerSettings Clone()
		{
			return new LogViewerSettings
			{
				LinesScrolledPerWheelTick = LinesScrolledPerWheelTick,
				FontSize = FontSize,
				TabWidth = TabWidth,
				_other = Other.Clone(),
				_trace = Trace.Clone(),
				_debug = Debug.Clone(),
				_info = Info.Clone(),
				_warning = Warning.Clone(),
				_error = Error.Clone(),
				_fatal = Fatal.Clone()
			};
		}

		object ICloneable.Clone()
		{
			return Clone();
		}
	}
}
