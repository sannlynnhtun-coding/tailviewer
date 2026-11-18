using Avalonia.Controls;
using Tailviewer.Settings;

namespace Tailviewer.Avalonia.Settings;

public static class WindowSettingsAdapter
{
    public static WindowState ToAvaloniaState(this WindowDisplayState value) => value switch
    {
        WindowDisplayState.Normal => WindowState.Normal,
        WindowDisplayState.Minimized => WindowState.Minimized,
        WindowDisplayState.Maximized => WindowState.Maximized,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static WindowDisplayState ToSettingsState(this WindowState value) => value switch
    {
        WindowState.Normal => WindowDisplayState.Normal,
        WindowState.Minimized => WindowDisplayState.Minimized,
        WindowState.Maximized => WindowDisplayState.Maximized,
        _ => throw new ArgumentOutOfRangeException(nameof(value), "This state is not part of the legacy window settings contract.")
    };
}
