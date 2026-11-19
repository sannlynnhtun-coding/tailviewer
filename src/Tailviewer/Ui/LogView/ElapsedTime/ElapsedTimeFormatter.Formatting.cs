using System;
using System.Diagnostics.Contracts;

namespace Tailviewer.Ui.LogView.ElapsedTime
{
	public sealed partial class ElapsedTimeFormatter
	{
		[Pure]
		public static string ToString(TimeSpan? value, IFormatProvider provider)
		{
			if (value != null)
				return ToString(value.Value, provider);
			return string.Empty;
		}

		public static string ToString(TimeSpan value, IFormatProvider provider)
		{
			if (value >= TimeSpan.FromDays(1))
				return string.Format("{0:%d}d {0:hh\\:mm\\:ss\\.fff}", value);
			return value.ToString(@"hh\:mm\:ss\.fff", provider);
		}
	}
}
