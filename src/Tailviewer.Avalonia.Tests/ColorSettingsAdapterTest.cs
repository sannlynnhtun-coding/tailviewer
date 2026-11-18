using NUnit.Framework;
using Tailviewer.Avalonia.Settings;

namespace Tailviewer.Avalonia.Tests;

[TestFixture]
public sealed class ColorSettingsAdapterTest
{
    [TestCase(0, 255, 255, 255)]
    [TestCase(128, 1, 2, 3)]
    [TestCase(255, 232, 17, 35)]
    public void ConversionPreservesAllChannels(int a, int r, int g, int b)
    {
        var original = System.Drawing.Color.FromArgb(a, r, g, b);
        var converted = original.ToAvaloniaColor();
        Assert.That(converted.A, Is.EqualTo(a));
        Assert.That(converted.R, Is.EqualTo(r));
        Assert.That(converted.G, Is.EqualTo(g));
        Assert.That(converted.B, Is.EqualTo(b));
        Assert.That(converted.ToSettingsColor().ToArgb(), Is.EqualTo(original.ToArgb()));
    }
}
