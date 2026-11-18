using Avalonia.Controls;
using NUnit.Framework;
using Tailviewer.Avalonia.Settings;
using Tailviewer.Settings;

namespace Tailviewer.Avalonia.Tests;

[TestFixture]
public sealed class WindowSettingsAdapterTest
{
    [TestCase(WindowDisplayState.Normal, WindowState.Normal)]
    [TestCase(WindowDisplayState.Minimized, WindowState.Minimized)]
    [TestCase(WindowDisplayState.Maximized, WindowState.Maximized)]
    public void StatesRoundTripExactly(WindowDisplayState saved, WindowState native)
    {
        Assert.That(saved.ToAvaloniaState(), Is.EqualTo(native));
        Assert.That(native.ToSettingsState(), Is.EqualTo(saved));
    }

    [Test]
    public void MinimizedSessionRestoresTheVisiblePreMinimizeState()
    {
        var settings = new MainWindowSettings { State = WindowDisplayState.Maximized };
        settings.State = WindowDisplayState.Minimized;
        Assert.That(settings.Clone().RestoredState.ToAvaloniaState(), Is.EqualTo(WindowState.Maximized));
    }

    [Test]
    public void UnknownStatesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((WindowDisplayState)99).ToAvaloniaState());
        Assert.Throws<ArgumentOutOfRangeException>(() => ((WindowState)99).ToSettingsState());
    }
}
