using System;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Text;
using System.Xml;
using log4net;
using Tailviewer.Core;

namespace Tailviewer.Settings
{
	/// <summary>Persisted default log encoding; null retains automatic detection.</summary>
	public sealed class LogFileSettings
		: ILogFileSettings
	{
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		#region Implementation of ILogFileSettings

		/// <inheritdoc />
		public Encoding DefaultEncoding
		{
			get { return _defaultEncoding; }
			set { _defaultEncoding = value; _encodingName = null; _encodingUnavailable = false; }
		}

		private Encoding _defaultEncoding;
		private string _encodingName;
		private bool _encodingUnavailable;

		/// <summary>The persisted encoding name, including an unavailable selection.</summary>
		public string EncodingName => _encodingName ?? DefaultEncoding?.WebName ?? string.Empty;

		/// <summary>Whether the saved selection resolved, independently of a retained live fallback.</summary>
		public bool IsEncodingAvailable => !_encodingUnavailable;

		#endregion

		/// <summary>Creates an independent settings copy.</summary>
		[Pure]
		public LogFileSettings Clone()
		{
			return new LogFileSettings
			{
				_defaultEncoding = _defaultEncoding,
				_encodingName = _encodingName,
				_encodingUnavailable = _encodingUnavailable
			};
		}

		/// <summary>Writes the legacy defaultencoding attribute.</summary>
		public void Save(XmlWriter writer)
		{
			writer.WriteAttributeString("defaultencoding", EncodingName);
		}

		/// <summary>Restores a known encoding, retaining the current value when invalid.</summary>
		public void Restore(XmlReader reader)
		{
			for (int i = 0; i < reader.AttributeCount; ++i)
			{
				reader.MoveToAttribute(i);
				switch (reader.Name)
				{
					case "defaultencoding":
						var encodingName = reader.ReadContentAsString();
						_encodingName = encodingName;
						_encodingUnavailable = false;
						if (string.IsNullOrEmpty(encodingName)) _defaultEncoding = null;
						if (!string.IsNullOrEmpty(encodingName))
						{
							try
							{
#if NET10_0_OR_GREATER
								_defaultEncoding = SettingsEncoding.Resolve(encodingName);
#else
								_defaultEncoding = Encoding.GetEncoding(encodingName);
#endif
							}
							catch (Exception e)
							{
								_encodingUnavailable = true;
								Log.WarnFormat("Caught exception while trying to get encoding '{0}':\r\n{1}",
								               encodingName,
								               e);
							}
						}
						break;
				}
			}
		}
	}
}
