using System;
using System.Xml;
using System.IO;

namespace Tailviewer.Settings
{
	/// <summary>Persisted export destination, using the legacy XML attribute and fallback folder.</summary>
	public sealed class ExportSettings
		: IExportSettings
		, ICloneable
	{
		private string _exportFolder;

		/// <summary>Creates an independent settings copy.</summary>
		public ExportSettings Clone()
		{
			return new ExportSettings
			{
				_exportFolder = _exportFolder
			};
		}

		object ICloneable.Clone()
		{
			return Clone();
		}

		/// <summary>Writes the exportfolder attribute.</summary>
		public void Save(XmlWriter writer)
		{
			writer.WriteAttributeString("exportfolder", _exportFolder);
		}

		/// <summary>Restores the exportfolder attribute, falling back when empty.</summary>
		public void Restore(XmlReader reader)
		{
			for (int i = 0; i < reader.AttributeCount; ++i)
			{
				reader.MoveToAttribute(i);
				switch (reader.Name)
				{
					case "exportfolder":
						_exportFolder = reader.ReadContentAsString();
						
						break;
				}
			}

			if (string.IsNullOrEmpty(_exportFolder))
			{
#if NET10_0_OR_GREATER
				_exportFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Tailviewer", "Export");
#else
				_exportFolder = Constants.ExportDirectory;
#endif
			}
		}

		/// <inheritdoc />
		public string ExportFolder
		{
			get { return _exportFolder; }
			set { _exportFolder = value; }
		}
	}
}