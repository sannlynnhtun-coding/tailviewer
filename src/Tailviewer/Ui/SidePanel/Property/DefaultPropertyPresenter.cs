using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Tailviewer.Api;

namespace Tailviewer.Ui.SidePanel.Property
{
	public sealed class DefaultPropertyPresenter
		: IPropertyPresenter
	{
		private readonly string _displayName;
		private readonly IReadOnlyPropertyDescriptor _property;
		private object _value;

		public DefaultPropertyPresenter(string displayName, IReadOnlyPropertyDescriptor property)
		{
			_displayName = displayName;
			_property = property;
		}

		#region Implementation of INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		#endregion

		#region Implementation of IPropertyPresenter

		public string DisplayName => _displayName;

		public object Value
		{
			get
			{
				return _value;
			}
			private set
			{
				if (value == _value)
					return;

				_value = value;
				EmitPropertyChanged();
			}
		}

		public void Update(object newValue)
		{
			Value = _property == Core.Properties.Size && newValue is long bytes
				? (object)Metrolib.Size.FromBytes(bytes) : newValue;
		}

		public event Action<object> OnValueChanged;

		#endregion

		private void EmitPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private void EmitValueChanged(object obj)
		{
			OnValueChanged?.Invoke(obj);
		}
	}
}