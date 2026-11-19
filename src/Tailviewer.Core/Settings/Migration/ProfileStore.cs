#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Tailviewer.Settings
{
    internal static class ProfileStore
    {
        internal const int MaxFileBytes = 16 * 1024 * 1024;
        internal const int MaxDepth = 64;

        internal static void Write(string path, Action<XmlWriter> serialize, Action<XDocument> validate)
        {
            ArgumentNullException.ThrowIfNull(serialize);
            ArgumentNullException.ThrowIfNull(validate);
            path = Path.GetFullPath(path);
            var folder = Path.GetDirectoryName(path);
            Directory.CreateDirectory(folder);
            var candidate = Path.Combine(folder, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            var ownsCandidate = false;
            try
            {
                using (var stream = new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    ownsCandidate = true;
                    using (var writer = XmlWriter.Create(stream, new XmlWriterSettings
                    {
                        Encoding = new UTF8Encoding(false),
                        Indent = true,
                        IndentChars = "  ",
                        NewLineChars = "\r\n",
                        NewLineHandling = NewLineHandling.Entitize,
                        CloseOutput = false
                    })) serialize(writer);
                    if (stream.Length > MaxFileBytes) throw new XmlException("Profile exceeds the file-size limit.");
                    stream.Flush(true);
                }
                validate(Read(candidate));
                if (File.Exists(path)) File.Replace(candidate, path, null);
                else File.Move(candidate, path);
                ownsCandidate = false;
            }
            finally
            {
                // Never sweep abandoned/foreign candidates or fall back to truncating the destination.
                if (ownsCandidate) File.Delete(candidate);
            }
        }

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
