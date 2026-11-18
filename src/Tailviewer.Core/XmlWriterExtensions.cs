using System.Xml;
using Tailviewer.Api;

namespace Tailviewer.Core
{
	/// <summary>
	///     Extension methods for the <see cref="XmlWriter" /> class.
	/// </summary>
	public static class XmlWriterExtensions
	{
		/// <summary>
		///     Writes the given id into an attribute with the given name.
		/// </summary>
		/// <param name="writer"></param>
		/// <param name="localName"></param>
		/// <param name="id"></param>
		public static void WriteAttribute(this XmlWriter writer, string localName, DataSourceId id)
		{
			writer.WriteAttributeString(localName, id.Value.ToString());
		}

		/// <summary>
		///     Writes the given id into an attribute with the given name.
		/// </summary>
		/// <param name="writer"></param>
		/// <param name="localName"></param>
		/// <param name="id"></param>
		public static void WriteAttribute(this XmlWriter writer, string localName, QuickFilterId id)
		{
			writer.WriteAttributeString(localName, id.Value.ToString());
		}

	}
}