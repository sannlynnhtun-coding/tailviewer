using Avalonia.Media;

namespace Tailviewer.Avalonia.Settings;

public static class ColorSettingsAdapter
{
    public static Color ToAvaloniaColor(this System.Drawing.Color value) => Color.FromArgb(value.A, value.R, value.G, value.B);
    public static System.Drawing.Color ToSettingsColor(this Color value) => System.Drawing.Color.FromArgb(value.A, value.R, value.G, value.B);
}
