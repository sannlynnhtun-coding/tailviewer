using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Tailviewer.Settings;

namespace Tailviewer.Ui.LogView.ElapsedTime
{
	public sealed partial class ElapsedTimeFormatter
		: AbstractLogEntryValueFormatter
	{
		public const int CharacterWidth = 11;
		private readonly TimeSpan? _value;

		public ElapsedTimeFormatter(TimeSpan? value, TextSettings textSettings)
			: base(textSettings)
		{
			_value = value;
		}

		public override string ToString(IFormatProvider provider)
		{
			return ToString(_value, provider);
		}

		protected override FormattedText CreateFormattedText(string text,
		                                                     CultureInfo culture,
		                                                     TextSettings textSettings)
		{
			return new FormattedText(text,
			                         culture,
			                         FlowDirection.LeftToRight,
			                         textSettings.Typeface,
			                         textSettings.FontSize,
			                         TextBrushes.LineNumberForegroundBrush,
			                         1.25);
		}
	}
}
