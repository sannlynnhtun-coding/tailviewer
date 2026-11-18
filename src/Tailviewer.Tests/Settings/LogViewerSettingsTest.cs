using System.Diagnostics.Contracts;
using System.IO;
using System.Text;
#if NET10_0_OR_GREATER
using Color = System.Drawing.Color;
using Colors = System.Drawing.Color;
#else
using System.Windows.Media;
#endif
using System.Xml;
using FluentAssertions;
using NUnit.Framework;
using Tailviewer.Settings;

namespace Tailviewer.Tests.Settings
{
	[TestFixture]
	public sealed class LogViewerSettingsTest
	{
		private static uint Argb(Color value) => ((uint)value.A << 24) | ((uint)value.R << 16) | ((uint)value.G << 8) | value.B;

		[Pure]
		private static LogViewerSettings Restore(string file)
		{
			using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(file)))
			{
				using (var reader = XmlReader.Create(stream))
				{
					reader.MoveToContent();

					var settings = new LogViewerSettings();
					settings.Restore(reader);
					return settings;
				}
			}
		}

		[Pure]
		private static string Save(LogViewerSettings logViewerSettings)
		{
			using (var stream = new MemoryStream())
			{
				using (var writer = XmlWriter.Create(stream))
				{
					writer.WriteStartDocument();
					writer.WriteStartElement("Test");
					logViewerSettings.Save(writer);
					writer.WriteEndElement();
					writer.WriteEndDocument();
				}

				return Encoding.UTF8.GetString(stream.ToArray());
			}
		}

		[Test]
		[Description("Verifies that all properties are initialized to sensible default values")]
		public void TestConstruction()
		{
			var settings = new LogViewerSettings();
			settings.LinesScrolledPerWheelTick.Should().Be(2);
			settings.FontSize.Should().Be(12);
			settings.TabWidth.Should().Be(4);

			Argb(settings.Other.ForegroundColor).Should().Be(Argb(Colors.Black));
			Argb(settings.Other.BackgroundColor).Should().Be(Argb(Colors.Transparent));

			Argb(settings.Trace.ForegroundColor).Should().Be(Argb(Color.FromArgb(255, 128, 128, 128)));
			Argb(settings.Trace.BackgroundColor).Should().Be(Argb(Colors.Transparent));

			Argb(settings.Debug.ForegroundColor).Should().Be(Argb(Color.FromArgb(255, 128, 128, 128)));
			Argb(settings.Debug.BackgroundColor).Should().Be(Argb(Colors.Transparent));

			Argb(settings.Info.ForegroundColor).Should().Be(Argb(Colors.Black));
			Argb(settings.Info.BackgroundColor).Should().Be(Argb(Colors.Transparent));

			Argb(settings.Warning.ForegroundColor).Should().Be(Argb(Colors.White));
			Argb(settings.Warning.BackgroundColor).Should().Be(Argb(Color.FromArgb(255, 255, 195, 0)));

			Argb(settings.Error.ForegroundColor).Should().Be(Argb(Colors.White));
			Argb(settings.Error.BackgroundColor).Should().Be(Argb(Color.FromArgb(255, 232, 17, 35)));

			Argb(settings.Fatal.ForegroundColor).Should().Be(Argb(Colors.White));
			Argb(settings.Fatal.BackgroundColor).Should().Be(Argb(Color.FromArgb(255, 232, 17, 35)));
		}

		[Test]
		public void TestClone([Values(1, 2)] int linesScrolledPerWheelTick,
		                      [Values(1, 2)] int fontSize,
		                      [Values(1, 2)] int tabWidth)
		{
			var settings = new LogViewerSettings
			{
				LinesScrolledPerWheelTick = linesScrolledPerWheelTick,
				FontSize = fontSize,
				TabWidth = tabWidth,
				Other = { BackgroundColor = Colors.Red, ForegroundColor = Colors.Firebrick },
				Trace = { BackgroundColor = Colors.Magenta, ForegroundColor = Colors.Teal },
				Debug = { BackgroundColor = Colors.Aqua, ForegroundColor = Colors.DodgerBlue },
				Info = { BackgroundColor = Colors.DarkOrange, ForegroundColor = Colors.AliceBlue },
				Warning = { BackgroundColor = Colors.Aquamarine, ForegroundColor = Colors.BlanchedAlmond },
				Error = { BackgroundColor = Colors.BlueViolet, ForegroundColor = Colors.CadetBlue },
				Fatal = { BackgroundColor = Colors.Coral, ForegroundColor = Colors.HotPink }
			};

			var actualSettings = settings.Clone();
			actualSettings.LinesScrolledPerWheelTick.Should().Be(linesScrolledPerWheelTick);
			actualSettings.FontSize.Should().Be(fontSize);
			actualSettings.TabWidth.Should().Be(tabWidth);
			Argb(actualSettings.Other.BackgroundColor).Should().Be(Argb(Colors.Red));
			Argb(actualSettings.Other.ForegroundColor).Should().Be(Argb(Colors.Firebrick));
			Argb(actualSettings.Trace.BackgroundColor).Should().Be(Argb(Colors.Magenta));
			Argb(actualSettings.Trace.ForegroundColor).Should().Be(Argb(Colors.Teal));
			Argb(actualSettings.Debug.BackgroundColor).Should().Be(Argb(Colors.Aqua));
			Argb(actualSettings.Debug.ForegroundColor).Should().Be(Argb(Colors.DodgerBlue));
			Argb(actualSettings.Info.BackgroundColor).Should().Be(Argb(Colors.DarkOrange));
			Argb(actualSettings.Info.ForegroundColor).Should().Be(Argb(Colors.AliceBlue));
			Argb(actualSettings.Warning.BackgroundColor).Should().Be(Argb(Colors.Aquamarine));
			Argb(actualSettings.Warning.ForegroundColor).Should().Be(Argb(Colors.BlanchedAlmond));
			Argb(actualSettings.Error.BackgroundColor).Should().Be(Argb(Colors.BlueViolet));
			Argb(actualSettings.Error.ForegroundColor).Should().Be(Argb(Colors.CadetBlue));
			Argb(actualSettings.Fatal.BackgroundColor).Should().Be(Argb(Colors.Coral));
			Argb(actualSettings.Fatal.ForegroundColor).Should().Be(Argb(Colors.HotPink));
		}

		[Test]
		public void TestRestoreFromEmpty()
		{
			string empty;
			using (var stream = new MemoryStream())
			{
				using (var writer = XmlWriter.Create(stream))
				{
					writer.WriteStartDocument();
					writer.WriteStartElement("Test");
					writer.WriteEndElement();
					writer.WriteEndDocument();
				}

				empty =  Encoding.UTF8.GetString(stream.ToArray());
			}

			var settings = Restore(empty);
			const string reason =
				"because when the attribute doesn't appear in the xml content, then its default value shall be restored";
			settings.LinesScrolledPerWheelTick.Should().Be(2, reason);
			settings.FontSize.Should().Be(12, reason);
			settings.TabWidth.Should().Be(4, reason);

			Argb(settings.Other.ForegroundColor).Should().Be(Argb(Colors.Black));
			Argb(settings.Other.BackgroundColor).Should().Be(Argb(Colors.Transparent));

			Argb(settings.Trace.ForegroundColor).Should().Be(Argb(Color.FromArgb(255, 128, 128, 128)), reason);
			Argb(settings.Trace.BackgroundColor).Should().Be(Argb(Colors.Transparent), reason);

			Argb(settings.Debug.ForegroundColor).Should().Be(Argb(Color.FromArgb(255, 128, 128, 128)));
			Argb(settings.Debug.BackgroundColor).Should().Be(Argb(Colors.Transparent));

			Argb(settings.Info.ForegroundColor).Should().Be(Argb(Colors.Black));
			Argb(settings.Info.BackgroundColor).Should().Be(Argb(Colors.Transparent));

			Argb(settings.Warning.ForegroundColor).Should().Be(Argb(Colors.White));
			Argb(settings.Warning.BackgroundColor).Should().Be(Argb(Color.FromArgb(255, 255, 195, 0)));

			Argb(settings.Error.ForegroundColor).Should().Be(Argb(Colors.White));
			Argb(settings.Error.BackgroundColor).Should().Be(Argb(Color.FromArgb(255, 232, 17, 35)));

			Argb(settings.Fatal.ForegroundColor).Should().Be(Argb(Colors.White));
			Argb(settings.Fatal.BackgroundColor).Should().Be(Argb(Color.FromArgb(255, 232, 17, 35)));
		}

		[Test]
		[Description("Verifies that upon restoration, invalid values are replaced with defaults")]
		public void TestRestoreFromInvalidValues([Values(-5, -2, -1, 0)] int linesScrolledPerWheelTick,
		                                         [Values(-5, -1, 0)] int fontSize,
		                                         [Values(-1, 0)] int tabWidth)
		{
			var file = Save(new LogViewerSettings
			{
				LinesScrolledPerWheelTick = linesScrolledPerWheelTick,
				FontSize = fontSize,
				TabWidth = tabWidth,
				Other = { BackgroundColor = Colors.Red, ForegroundColor = Colors.Firebrick },
				Trace = { BackgroundColor = Colors.Magenta, ForegroundColor = Colors.Teal },
				Debug = { BackgroundColor = Colors.Aqua, ForegroundColor = Colors.DodgerBlue },
				Info = { BackgroundColor = Colors.DarkOrange, ForegroundColor = Colors.AliceBlue },
				Warning = { BackgroundColor = Colors.Aquamarine, ForegroundColor = Colors.BlanchedAlmond },
				Error = { BackgroundColor = Colors.BlueViolet, ForegroundColor = Colors.CadetBlue },
				Fatal = { BackgroundColor = Colors.Coral, ForegroundColor = Colors.HotPink }
			});

			var actualSettings = Restore(file);
			actualSettings.LinesScrolledPerWheelTick.Should().Be(2, "because restore should simply discard invalid values and restore them to their defaults");
			actualSettings.FontSize.Should().Be(12);
			actualSettings.TabWidth.Should().Be(4);
			Argb(actualSettings.Other.BackgroundColor).Should().Be(Argb(Colors.Red));
			Argb(actualSettings.Other.ForegroundColor).Should().Be(Argb(Colors.Firebrick));
			Argb(actualSettings.Trace.BackgroundColor).Should().Be(Argb(Colors.Magenta));
			Argb(actualSettings.Trace.ForegroundColor).Should().Be(Argb(Colors.Teal));
			Argb(actualSettings.Debug.BackgroundColor).Should().Be(Argb(Colors.Aqua));
			Argb(actualSettings.Debug.ForegroundColor).Should().Be(Argb(Colors.DodgerBlue));
			Argb(actualSettings.Info.BackgroundColor).Should().Be(Argb(Colors.DarkOrange));
			Argb(actualSettings.Info.ForegroundColor).Should().Be(Argb(Colors.AliceBlue));
			Argb(actualSettings.Warning.BackgroundColor).Should().Be(Argb(Colors.Aquamarine));
			Argb(actualSettings.Warning.ForegroundColor).Should().Be(Argb(Colors.BlanchedAlmond));
			Argb(actualSettings.Error.BackgroundColor).Should().Be(Argb(Colors.BlueViolet));
			Argb(actualSettings.Error.ForegroundColor).Should().Be(Argb(Colors.CadetBlue));
			Argb(actualSettings.Fatal.BackgroundColor).Should().Be(Argb(Colors.Coral));
			Argb(actualSettings.Fatal.ForegroundColor).Should().Be(Argb(Colors.HotPink));
		}

		[Test]
		public void TestRoundtrip([Values(1, 2)] int linesScrolledPerWheelTick,
		                          [Values(1, 20)] int fontSize,
		                          [Values(1, 2)] int tabWidth)
		{
			var settings = new LogViewerSettings
			{
				LinesScrolledPerWheelTick = linesScrolledPerWheelTick,
				FontSize = fontSize,
				TabWidth = tabWidth,
				Other = { BackgroundColor = Colors.Red, ForegroundColor = Colors.Firebrick },
				Trace = { BackgroundColor = Colors.Magenta, ForegroundColor = Colors.Teal },
				Debug = { BackgroundColor = Colors.Aqua, ForegroundColor = Colors.DodgerBlue },
				Info = { BackgroundColor = Colors.DarkOrange, ForegroundColor = Colors.AliceBlue },
				Warning = { BackgroundColor = Colors.Aquamarine, ForegroundColor = Colors.BlanchedAlmond },
				Error = { BackgroundColor = Colors.BlueViolet, ForegroundColor = Colors.CadetBlue },
				Fatal = { BackgroundColor = Colors.Coral, ForegroundColor = Colors.HotPink }
			};

			var actualSettings = Restore(Save(settings));
			const string reason = "because all values should roundtrip perfectly";
			actualSettings.LinesScrolledPerWheelTick.Should().Be(linesScrolledPerWheelTick, reason);
			actualSettings.FontSize.Should().Be(fontSize, reason);
			actualSettings.TabWidth.Should().Be(tabWidth);
			Argb(actualSettings.Other.BackgroundColor).Should().Be(Argb(Colors.Red));
			Argb(actualSettings.Other.ForegroundColor).Should().Be(Argb(Colors.Firebrick));
			Argb(actualSettings.Trace.BackgroundColor).Should().Be(Argb(Colors.Magenta));
			Argb(actualSettings.Trace.ForegroundColor).Should().Be(Argb(Colors.Teal));
			Argb(actualSettings.Debug.BackgroundColor).Should().Be(Argb(Colors.Aqua));
			Argb(actualSettings.Debug.ForegroundColor).Should().Be(Argb(Colors.DodgerBlue));
			Argb(actualSettings.Info.BackgroundColor).Should().Be(Argb(Colors.DarkOrange));
			Argb(actualSettings.Info.ForegroundColor).Should().Be(Argb(Colors.AliceBlue));
			Argb(actualSettings.Warning.BackgroundColor).Should().Be(Argb(Colors.Aquamarine));
			Argb(actualSettings.Warning.ForegroundColor).Should().Be(Argb(Colors.BlanchedAlmond));
			Argb(actualSettings.Error.BackgroundColor).Should().Be(Argb(Colors.BlueViolet));
			Argb(actualSettings.Error.ForegroundColor).Should().Be(Argb(Colors.CadetBlue));
			Argb(actualSettings.Fatal.BackgroundColor).Should().Be(Argb(Colors.Coral));
			Argb(actualSettings.Fatal.ForegroundColor).Should().Be(Argb(Colors.HotPink));
		}
	}
}