using System;
using System.Diagnostics.Contracts;
using System.Reflection;
#if NET10_0_OR_GREATER
using WindowState = Tailviewer.Settings.WindowDisplayState;
#else
using System.Windows;
#endif
using System.Xml;
using log4net;
#if NET10_0_OR_GREATER
using WindowSettings = Tailviewer.Settings.WindowPlacement;
#else
using Metrolib;
#endif

namespace Tailviewer.Settings
{
	/// <summary>Persisted window placement and selected panels.</summary>
	public sealed class MainWindowSettings
		: IMainWindowSettings
		, ICloneable
	{
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private WindowState _previousWindowState = WindowState.Normal;

		/// <summary>The window height.</summary>
		public double Height
		{
			get { return _window.Height; }
			set { _window.Height = value; }
		}

		/// <summary>The left coordinate.</summary>
		public double Left
		{
			get { return _window.Left; }
			set { _window.Left = value; }
		}

		/// <summary>The saved window state.</summary>
		public WindowState State
		{
			get { return _window.State; }
			set
			{
				if (value == _window.State)
					return;

				_previousWindowState = _window.State;
				_window.State = value;
			}
		}

		/// <summary>The top coordinate.</summary>
		public double Top
		{
			get { return _window.Top; }
			set { _window.Top = value; }
		}

		/// <summary>The window width.</summary>
		public double Width
		{
			get { return _window.Width; }
			set { _window.Width = value; }
		}

		/// <summary>Whether the window stays above others.</summary>
		public bool AlwaysOnTop { get; set; }

		/// <summary>The selected side panel.</summary>
		public string SelectedSidePanel { get; set; }

		/// <summary>Whether the source side panel is visible.</summary>
		public bool IsLeftSidePanelVisible { get; set; }

		/// <summary>The selected main panel.</summary>
		public string SelectedMainPanel { get; set; }

		/// <summary>Persisted window placement and selected panels.</summary>
		public MainWindowSettings()
		{
			_window = new WindowSettings();
			IsLeftSidePanelVisible = true;
		}

		private MainWindowSettings(MainWindowSettings other)
		{
			_window = other._window.Clone();
			_previousWindowState = other._previousWindowState;
			SelectedMainPanel = other.SelectedMainPanel;
			SelectedSidePanel = other.SelectedSidePanel;
			AlwaysOnTop = other.AlwaysOnTop;
			IsLeftSidePanelVisible = other.IsLeftSidePanelVisible;
		}

		private WindowSettings _window;

		object ICloneable.Clone()
		{
			return Clone();
		}

		/// <summary>Writes legacy window attributes.</summary>
		public void Save(XmlWriter writer)
		{
			writer.WriteAttributeString("selectedmainpanel", SelectedMainPanel);
			writer.WriteAttributeString("selectedsidepanel", SelectedSidePanel);
			writer.WriteAttributeBool("alwaysontop", AlwaysOnTop);
			writer.WriteAttributeBool("isleftsidepanelvisible", IsLeftSidePanelVisible);
			writer.WriteAttributeEnum("previouswindowstate", _previousWindowState);
			_window.Save(writer);
		}

		/// <summary>Restores legacy window attributes.</summary>
		public void Restore(XmlReader reader)
		{
			for (var i = 0; i < reader.AttributeCount; ++i)
			{
				reader.MoveToAttribute(i);
				switch (reader.Name)
				{
					case "alwaysontop":
						AlwaysOnTop = reader.ReadContentAsBool();
						break;

					case "isleftsidepanelvisible":
						IsLeftSidePanelVisible = reader.ReadContentAsBool();
						break;

					case "selectedmainpanel":
						SelectedMainPanel = reader.ReadContentAsString();
						break;

					case "selectedsidepanel":
						SelectedSidePanel = reader.ReadContentAsString();
						break;

					case "previouswindowstate":
						#if NET10_0_OR_GREATER
						if (!Enum.TryParse<WindowState>(reader.Value, out _previousWindowState) || !Enum.IsDefined(_previousWindowState))
							_previousWindowState = WindowState.Normal;
#else
						_previousWindowState = reader.ReadContentAsEnum<WindowState>();
#endif
						break;
				}
			}

			_window.Restore(reader);
		}

#if !NET10_0_OR_GREATER
		/// <summary>The configured UpdateFrom value.</summary>
		public void UpdateFrom(Window window)
		{
			_window.UpdateFrom(window);

			// For some reason, when the window is minimized, the width and height
			// are set to small values such as 160/28 whereas the ActualWidth and ActualHeight
			// values are sensible. Don't know why that is, but we want to store the sensible values!
			if (window.Width < window.ActualWidth)
				_window.Width = window.ActualWidth;
			if (window.Height < window.ActualHeight)
				_window.Height = window.ActualHeight;

			Log.DebugFormat("Updated main window settings to : Width={0} Height={1}", Width, Height);
		}

#endif

		/// <summary>Keeps the configured window visible on the supplied desktop.</summary>
		public void ClipToBounds(Desktop desktop)
		{
			try
			{
				#if NET10_0_OR_GREATER
				var currentRectangle = new Desktop.Window(new Desktop.Point(Left, Top), new WindowSize(Width, Height));
#else
				var currentRectangle = new Desktop.Window(_window);
#endif
				var newRectangle = desktop.ClipToBoundaries(currentRectangle);
				if (newRectangle != currentRectangle)
				{
					_window.Left = newRectangle.Left;
					_window.Top = newRectangle.Top;
					_window.Width = newRectangle.Width;
					_window.Height = newRectangle.Height;
					if (newRectangle.IsMaximized)
						_window.State = WindowState.Maximized;
				}
			}
			catch (Exception e)
			{
				Log.ErrorFormat("Caught unexpected exception while trying to clip window to desktop bounds: {0}", e);
			}
		}

#if !NET10_0_OR_GREATER
		/// <summary>The configured RestoreTo value.</summary>
		public void RestoreTo(Window window)
		{
			// https://github.com/Kittyfisto/Tailviewer/issues/250
			// When we start the application, a user NEVER wants the app
			// to start-up minimized. Therefore we start with the previous state
			// just in case the app was minimized before it was shut-down.
			if (_window.State == WindowState.Minimized)
				_window.State = _previousWindowState;

			_window.RestoreTo(window);
			window.Topmost = AlwaysOnTop;
		}

#else
		/// <summary>The visible state to restore when the saved window was minimized.</summary>
		public WindowDisplayState RestoredState => State == WindowDisplayState.Minimized ? (_previousWindowState == WindowDisplayState.Minimized ? WindowDisplayState.Normal : _previousWindowState) : State;
#endif

		/// <summary>Creates an independent settings copy.</summary>
		[Pure]
		public MainWindowSettings Clone()
		{
			return new MainWindowSettings(this);
		}
	}
}
