using System.Globalization;
using NUnit.Framework;
using Tailviewer.Ui.LogView.DeltaTimes;

namespace Tailviewer.Avalonia.Tests;

[TestFixture]
public sealed class DeltaTimeFormatterTest
{
    [TestCase(0, "0 ms")]
    [TestCase(1, "+1 ms")]
    [TestCase(-1, "-1 ms")]
    [TestCase(999, "+999 ms")]
    [TestCase(1000, "+1 sec")]
    [TestCase(-2000, "-2 sec")]
    [TestCase(60000, "+1 min")]
    [TestCase(-120000, "-2 min")]
    [TestCase(3600000, "+1 hour")]
    [TestCase(-3600000, "-1 hour")]
    [TestCase(7200000, "+2 hours")]
    [TestCase(-7200000, "-2 hours")]
    [TestCase(86400000, "+1 day")]
    [TestCase(-86400000, "-1 day")]
    [TestCase(172800000, "+2 days")]
    [TestCase(-172800000, "-2 days")]
    public void PreservesLegacySignedMagnitudeAndUnits(int milliseconds, string expected) =>
        Assert.That(DeltaTimeFormatter.Format(TimeSpan.FromMilliseconds(milliseconds), CultureInfo.InvariantCulture), Is.EqualTo(expected));

    [Test]
    public void MissingTimestampHasNoDeltaLabel() =>
        Assert.That(DeltaTimeFormatter.Format(null, CultureInfo.InvariantCulture), Is.Empty);
}
