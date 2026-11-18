using System;
using System.Collections.Generic;
using System.Linq;
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

        internal static XElement SaveElement(string name, Action<XmlWriter> save)
        {
            var document = new XDocument();
            using (var writer = document.CreateWriter())
            {
                writer.WriteStartElement(name);
                save(writer);
                writer.WriteEndElement();
            }
            return document.Root;
        }

        internal static void ReplaceChildren(XElement element, string name, IEnumerable<XElement> children)
        {
            var replacements = new Queue<XElement>(children);
            foreach (var child in element.Elements(name).ToList())
            {
                if (replacements.Count > 0) child.ReplaceWith(replacements.Dequeue());
                else child.Remove();
            }
            element.Add(replacements);
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
