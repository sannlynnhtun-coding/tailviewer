using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Tailviewer.Settings;

namespace Tailviewer.Ui.LogView.DeltaTimes
{
	internal sealed partial class DeltaTimeFormatter
		: AbstractLogEntryValueFormatter
	{
		private readonly TimeSpan? _delta;

		public DeltaTimeFormatter(TimeSpan? delta)
			: this(delta, TextSettings.Default)
		{}

		public DeltaTimeFormatter(TimeSpan? delta, TextSettings textSettings)
			: base(textSettings)
		{
			_delta = delta;
		}

		public override string ToString(IFormatProvider provider)
		{
			return Format(_delta, provider);
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
