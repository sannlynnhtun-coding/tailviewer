#if NET10_0_OR_GREATER
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Tailviewer.Settings
{
    internal static class ProfileStore
    {
        internal const int MaxFileBytes = 16 * 1024 * 1024;
        internal const int MaxDepth = 64;

        internal static XDocument Read(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                if (stream.Length > MaxFileBytes) throw new XmlException("Profile exceeds the file-size limit.");
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = MaxFileBytes,
                    MaxCharactersFromEntities = MaxFileBytes,
                    CloseInput = false
                };
                // Check depth before allocating/cloning an XML tree with opaque plugin payloads.
                using (var reader = XmlReader.Create(stream, settings))
                    while (reader.Read())
                        if (reader.Depth > MaxDepth) throw new XmlException("Profile exceeds the XML depth limit.");
                stream.Position = 0;
                using (var reader = XmlReader.Create(stream, settings))
                {
                    var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
                    if (document.Root == null || document.Root.Name != "xml") throw new XmlException("Unexpected profile root.");
                    return document;
                }
            }
        }
    }
}
#endif
