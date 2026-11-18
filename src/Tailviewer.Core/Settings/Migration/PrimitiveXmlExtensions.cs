using System;
using System.Globalization;
using System.Xml;

namespace Tailviewer.Settings
{
    // Preserve the old Metrolib wire values without bringing its UI assembly into Core.
    internal static class PrimitiveXmlExtensions
    {
        public static bool ReadContentAsBool(this XmlReader reader) => bool.TryParse(reader.ReadContentAsString(), out var value) && value;
        public static T ReadContentAsEnum<T>(this XmlReader reader) => (T)Enum.Parse(typeof(T), reader.ReadContentAsString());
        public static Guid ReadContentAsGuid(this XmlReader reader) => Guid.Parse(reader.ReadContentAsString());
        public static DateTime ReadContentAsDateTime2(this XmlReader reader) => new DateTime(long.Parse(reader.ReadContentAsString(), CultureInfo.InvariantCulture));
        public static double ReadContentAsDouble2(this XmlReader reader) => double.Parse(reader.ReadContentAsString(), CultureInfo.InvariantCulture);
        public static void WriteAttributeBool(this XmlWriter writer, string name, bool value) => writer.WriteAttributeString(name, XmlConvert.ToString(value));
        public static void WriteAttributeInt(this XmlWriter writer, string name, int value) => writer.WriteAttributeString(name, value.ToString(CultureInfo.InvariantCulture));
        public static void WriteAttributeGuid(this XmlWriter writer, string name, Guid value) => writer.WriteAttributeString(name, value.ToString());
        public static void WriteAttributeDateTime(this XmlWriter writer, string name, DateTime value) => writer.WriteAttributeString(name, value.Ticks.ToString(CultureInfo.InvariantCulture));
        public static void WriteAttributeDouble(this XmlWriter writer, string name, double value) => writer.WriteAttributeString(name, value.ToString(CultureInfo.InvariantCulture));
        public static void WriteAttributeEnum<T>(this XmlWriter writer, string name, T value) => writer.WriteAttributeString(name, value.ToString());
    }
}
