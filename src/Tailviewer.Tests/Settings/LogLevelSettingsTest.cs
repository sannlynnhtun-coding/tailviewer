using System.Collections.Generic;
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
	public sealed class LogLevelSettingsTest
	{
		private static uint Argb(Color value) => ((uint)value.A << 24) | ((uint)value.R << 16) | ((uint)value.G << 8) | value.B;

		public static IReadOnlyList<Color> SomeColors => new[]
		{
			Colors.Black,
			Colors.Magenta,
			Colors.Teal
		};
		
		[Pure]
		private static LogLevelSettings Restore(string file)
		{
			using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(file)))
			{
				using (var reader = XmlReader.Create(stream))
				{
					reader.MoveToContent();

					var settings = new LogLevelSettings();
					settings.Restore(reader);
					return settings;
				}
			}
		}

		[Pure]
		private static string Save(LogLevelSettings logViewerSettings)
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
		public void TestConstruction()
		{
			var settings = new LogLevelSettings();
			Argb(settings.ForegroundColor).Should().Be(Argb(Colors.Black));
			Argb(settings.BackgroundColor).Should().Be(Argb(Colors.Transparent));
		}

		[Test]
		public void TestRoundtrip([ValueSource(nameof(SomeColors))] Color foregroundColor,
		                          [ValueSource(nameof(SomeColors))] Color backgroundColor)
		{
			var settings = new LogLevelSettings
			{
				ForegroundColor = foregroundColor,
				BackgroundColor = backgroundColor
			};
			var actualSettings = Restore(Save(settings));
			Argb(actualSettings.ForegroundColor).Should().Be(Argb(foregroundColor));
			Argb(actualSettings.BackgroundColor).Should().Be(Argb(backgroundColor));
		}
	}
}