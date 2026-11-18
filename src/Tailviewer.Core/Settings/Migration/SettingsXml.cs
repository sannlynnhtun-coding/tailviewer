using System.Xml;
using System.Xml.Linq;

namespace Tailviewer.Settings
{
    internal static class SettingsXml
    {
        internal static XElement ReadElement(XmlReader reader)
        {
            reader.MoveToContent();
            var namespaces = (reader as IXmlNamespaceResolver)?.GetNamespacesInScope(XmlNamespaceScope.All);
            XElement element;
            using (var subtree = reader.ReadSubtree())
                element = XElement.Load(subtree, LoadOptions.PreserveWhitespace);
            // Opaque attribute values may contain QNames using an ancestor's prefix.
            if (namespaces != null)
                foreach (var pair in namespaces)
                    if (pair.Key.Length > 0 && pair.Key != "xml" && pair.Key != "xmlns" && element.GetNamespaceOfPrefix(pair.Key) == null)
                        element.SetAttributeValue(XNamespace.Xmlns + pair.Key, pair.Value);
            return element;
        }

        internal static void WriteContent(XElement element, XmlWriter writer)
        {
            using (var attributes = element.CreateReader())
            {
                attributes.MoveToContent();
                writer.WriteAttributes(attributes, false);
            }
            foreach (var node in element.Nodes()) node.WriteTo(writer);
        }
    }
}
