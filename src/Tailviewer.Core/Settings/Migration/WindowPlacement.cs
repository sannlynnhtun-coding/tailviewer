using System;
using System.Globalization;
using System.Xml;

namespace Tailviewer.Settings
{
    /// <summary>The legacy window states, independent of any UI framework.</summary>
    public enum WindowDisplayState
    {
        /// <summary>A restored window.</summary>
        Normal,
        /// <summary>A minimized window.</summary>
        Minimized,
        /// <summary>A maximized window.</summary>
        Maximized
    }

    /// <summary>A size in the coordinate units selected by the UI adapter.</summary>
    public readonly struct WindowSize : IEquatable<WindowSize>
    {
        /// <summary>The width.</summary>
        public double Width { get; }
        /// <summary>The height.</summary>
        public double Height { get; }
        /// <summary>Creates a nonnegative size.</summary>
        public WindowSize(double width, double height)
        {
            if (width < 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
        }
        /// <inheritdoc />
        public bool Equals(WindowSize other) => Width.Equals(other.Width) && Height.Equals(other.Height);
        /// <inheritdoc />
        public override bool Equals(object obj) => obj is WindowSize other && Equals(other);
        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Width, Height);
    }

    internal sealed class WindowPlacement
    {
        public double Left, Top, Width, Height;
        public WindowDisplayState State;
        public WindowPlacement Clone() => (WindowPlacement)MemberwiseClone();
        public void Save(XmlWriter writer)
        {
            writer.WriteAttributeDouble("top", Top);
            writer.WriteAttributeDouble("left", Left);
            writer.WriteAttributeDouble("width", Width);
            writer.WriteAttributeDouble("height", Height);
            writer.WriteAttributeEnum("state", State);
        }
        public void Restore(XmlReader reader)
        {
            Top = ReadDouble(reader, "top", Top, false);
            Left = ReadDouble(reader, "left", Left, false);
            Width = ReadDouble(reader, "width", Width, true);
            Height = ReadDouble(reader, "height", Height, true);
            var text = reader.GetAttribute("state");
            if (Enum.TryParse<WindowDisplayState>(text, out var state) && Enum.IsDefined(state)) State = state;
        }
        private static double ReadDouble(XmlReader reader, string name, double fallback, bool size)
        {
            var text = reader.GetAttribute(name);
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) && (!size || value >= 0) ? value : fallback;
        }
    }
}
