using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using log4net;
using Tailviewer.Api;
using Tailviewer.BusinessLogic.LogFileFormats;
using Tailviewer.Settings;
using Tailviewer.Settings.CustomFormats;

namespace Tailviewer.Ui.Settings.CustomFormats
{
	public sealed class CustomFormatViewModel
		: INotifyPropertyChanged
	{
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private readonly IServiceContainer _serviceContainer;
		private readonly IApplicationSettings _settings;
		private readonly ILogFileFormatRegistry _logFileFormatRegistry;
		private readonly ICustomLogFileFormatCreatorPlugin _plugin;
		private readonly IEnumerable<EncodingViewModel> _encodings;
		private CustomLogFileFormat _customFormat;
		private string _name;
		private string _format;
		private EncodingViewModel _encoding;
		private string _errorMessage;

		public CustomFormatViewModel(IServiceContainer serviceContainer,
		                             IApplicationSettings settings,
		                             ICustomLogFileFormatCreatorPlugin plugin,
		                             IEnumerable<EncodingViewModel> encodings,
		                             CustomLogFileFormat customFormat)
		{
			_serviceContainer = serviceContainer;
			_settings = settings;
			_logFileFormatRegistry = serviceContainer.Retrieve<ILogFileFormatRegistry>();
			_plugin = plugin;
			_encodings = encodings;
			_customFormat = customFormat;
			_name = customFormat.Name;
			_encoding = Encodings.FirstOrDefault(x => Equals(x.Encoding, customFormat.Encoding));
			if (customFormat.Encoding == null && !string.IsNullOrEmpty(customFormat.EncodingName))
			{
				_encoding = new EncodingViewModel(null, "Unavailable: " + customFormat.EncodingName);
				_encodings = encodings.Concat(new[] { _encoding }).ToArray();
			}
			_format = customFormat.Format;
		}

		public string Name
		{
			get { return _name; }
			set
			{
				if (value == _name)
					return;

				_name = value;
				EmitPropertyChanged();

				TryCreateFormat();
				Save();
			}
		}

		public string Format
		{
			get { return _format; }
			set
			{
				if (value == _format)
					return;

				_format = value;
				EmitPropertyChanged();

				TryCreateFormat();
				Save();
			}
		}

		public EncodingViewModel Encoding
		{
			get { return _encoding; }
			set
			{
				if (value == _encoding)
					return;

				_encoding = value;
				EmitPropertyChanged();

				TryCreateFormat(encodingChanged: true);
				Save();
			}
		}

		public IEnumerable<EncodingViewModel> Encodings => _encodings;

		public string ErrorMessage
		{
			get { return _errorMessage; }
			private set
			{
				if (value == _errorMessage)
					return;

				_errorMessage = value;
				EmitPropertyChanged();
			}
		}

		public ICommand RemoveCommand { get; set; }

		public CustomLogFileFormat CustomFormat => _customFormat;

		public event PropertyChangedEventHandler PropertyChanged;

		private void TryCreateFormat(bool encodingChanged = false)
		{
			if (!_settings.CustomFormats.Any(item => ReferenceEquals(item, _customFormat))) return;
			try
			{
				var newCustomFormat = encodingChanged
					? _customFormat.WithDefinition(_name, _format, _encoding?.Encoding)
					: _customFormat.WithDefinition(_name, _format);
				if (_plugin.TryCreate(_serviceContainer, newCustomFormat, out var newLogFileFormat))
				{
					if (newLogFileFormat == null)
					{
						Log.WarnFormat("Plugin '{0}' claimed to have created a new custom log file format, but didn't!", _plugin);
					}
				}

				ReplaceCustomFormat(newCustomFormat, newLogFileFormat);
			}
			catch (Exception e)
			{
				Log.WarnFormat("Caught exception while parsing format '{0}': {1}", _format, e);
				_logFileFormatRegistry.Remove(_customFormat);
			}
		}

		private void ReplaceCustomFormat(CustomLogFileFormat newCustomFormat, ILogFileFormat newLogFileFormat)
		{
			var formats = _settings.CustomFormats;
			var index = -1;
			for (var i = 0; i < formats.Count; ++i)
				if (ReferenceEquals(formats[i], _customFormat)) { index = i; break; }
			if (index < 0) return;
			_logFileFormatRegistry.Replace(_customFormat, newCustomFormat, newLogFileFormat);
			formats[index] = newCustomFormat;
			_customFormat = newCustomFormat;
		}

		private void Save()
		{
			_settings.SaveAsync();
		}

		private void EmitPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
