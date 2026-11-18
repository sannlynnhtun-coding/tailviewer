using System;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Xml;
#if NET10_0_OR_GREATER
using System.Xml.Linq;
using Tailviewer.Settings;
#endif
using Tailviewer.Api;
using Tailviewer.Core.Filters.ExpressionEngine;

// ReSharper disable once CheckNamespace
namespace Tailviewer.Core
{
	/// <summary>
	///     The configuration of a filter which allows filtering
	///     log events by their timestamp.
	/// </summary>
	public sealed class TimeFilterSettings
		: ICloneable
	{
#if NET10_0_OR_GREATER
		private XElement _rawXml;
#endif

		/// <summary>
		/// 
		/// </summary>
		public TimeFilterMode Mode;

		/// <summary>
		///     One of the pre-defined special ranges (such as today).
		/// </summary>
		public SpecialDateTimeInterval SpecialInterval;

		/// <summary>
		/// </summary>
		public DateTime? Maximum;

		/// <summary>
		/// </summary>
		public DateTime? Minimum;

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		[Pure]
		public TimeFilterSettings Clone()
		{
			return new TimeFilterSettings
			{
#if NET10_0_OR_GREATER
				_rawXml = _rawXml == null ? null : new XElement(_rawXml),
#endif
				Mode = Mode,
				SpecialInterval = SpecialInterval,
				Minimum = Minimum,
				Maximum = Maximum
			};
		}

		object ICloneable.Clone()
		{
			return Clone();
		}

		/// <summary>
		///     Restores this filter from the given xml reader.
		/// </summary>
		/// <param name="reader"></param>
		/// <returns></returns>
		public bool Restore(XmlReader reader)
		{
#if NET10_0_OR_GREATER
			var element = SettingsXml.ReadElement(reader);
			var candidate = Clone();
			using (var content = element.CreateReader())
			{
				content.MoveToContent();
				if (!candidate.RestoreKnown(content)) return false;
			}
			candidate._rawXml = element;
			CopyFrom(candidate);
			return true;
#else
			return RestoreKnown(reader);
#endif
		}

#if NET10_0_OR_GREATER
		internal void CopyFrom(TimeFilterSettings other)
		{
			Mode = other.Mode;
			SpecialInterval = other.SpecialInterval;
			Minimum = other.Minimum;
			Maximum = other.Maximum;
			_rawXml = other._rawXml == null ? null : new XElement(other._rawXml);
		}
#endif

		private bool RestoreKnown(XmlReader reader)
		{
			var count = reader.AttributeCount;
			for (var i = 0; i < count; ++i)
			{
				reader.MoveToAttribute(i);

				switch (reader.Name)
				{
					case "mode":
						if (Enum.TryParse(reader.Value, ignoreCase: true, result: out TimeFilterMode mode))
							Mode = mode;
						break;

					case "range":
						if (Enum.TryParse(reader.Value, ignoreCase: true, result: out SpecialDateTimeInterval range))
							SpecialInterval = range;
						break;

					case "start":
						Minimum = new DateTime(long.Parse(reader.Value, CultureInfo.InvariantCulture));
						break;

					case "end":
						Maximum = new DateTime(long.Parse(reader.Value, CultureInfo.InvariantCulture));
						break;
				}
			}

			return true;
		}

		/// <summary>
		///     Saves the contents of this object into the given writer.
		/// </summary>
		/// <param name="writer"></param>
		public void Save(XmlWriter writer)
		{
#if NET10_0_OR_GREATER
			var element = _rawXml == null ? new XElement("timefilter") : new XElement(_rawXml);
			element.SetAttributeValue("mode", Mode.ToString());
			element.SetAttributeValue("range", SpecialInterval.ToString());
			element.SetAttributeValue("start", Minimum?.Ticks.ToString(CultureInfo.InvariantCulture));
			element.SetAttributeValue("end", Maximum?.Ticks.ToString(CultureInfo.InvariantCulture));
			SettingsXml.WriteContent(element, writer);
#else
			writer.WriteAttributeString("mode", Mode.ToString());
			writer.WriteAttributeString("range", SpecialInterval.ToString());
			if (Minimum != null)
				writer.WriteAttributeString("start", Minimum.Value.Ticks.ToString(CultureInfo.InvariantCulture));
			if (Maximum != null)
				writer.WriteAttributeString("end", Maximum.Value.Ticks.ToString(CultureInfo.InvariantCulture));
#endif
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		[Pure]
		public ILogEntryFilter CreateFilter()
		{
			// 
			IExpression<IInterval<DateTime?>> interval = TryCreateInterval();
			if (interval == null)
				return null;

			var expression = new ContainsTimestampExpression(interval, new TimestampVariable());
			return new FilterExpression(expression);
		}

		private IExpression<IInterval<DateTime?>> TryCreateInterval()
		{
			switch (Mode)
			{
				case TimeFilterMode.Everything:
					return null;

				case TimeFilterMode.SpecialInterval:
					return new DateTimeIntervalLiteral(SpecialInterval);

				case TimeFilterMode.Interval:
					return new DateTimeInterval(Minimum, Maximum);

				default:
					return null;
			}
		}
	}
}
