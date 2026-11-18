#if NET10_0_OR_GREATER
using WindowState = Tailviewer.Settings.WindowDisplayState;
#else
using System.Windows;
#endif
using System.Xml;

namespace Tailviewer.Settings
{
	/// <summary>Persisted window placement and panel preferences.</summary>
	public interface IMainWindowSettings
	{
		/// <summary>
		///     The height of the window.
		/// </summary>
		double Height { get; set; }

		/// <summary>
		///     The left coordinate of the window's position.
		/// </summary>
		double Left { get; set; }

		/// <summary>
		///     The state of the window (i.e. normal, minimized, maximized, etc...).
		/// </summary>
		WindowState State { get; set; }

		/// <summary>
		///     The top coordinate of the window's position.
		/// </summary>
		double Top { get; set; }

		/// <summary>
		///     The width of the window.
		/// </summary>
		double Width { get; set; }

		/// <summary>
		///     Whether or not the window is always on top of other windows.
		/// </summary>
		bool AlwaysOnTop { get; set; }

		/// <summary>The selected side panel.</summary>
		string SelectedSidePanel { get; set; }

		/// <summary>
		///    When set to true, then the left-side panel is collapsed to
		///    reduce space requirements.
		/// </summary>
		bool IsLeftSidePanelVisible { get; set; }

		/// <summary>The selected main panel.</summary>
		string SelectedMainPanel { get; set; }

		/// <summary>Writes the saved window placement and panel attributes.</summary>
		void Save(XmlWriter writer);
		/// <summary>Restores the saved window placement and panels.</summary>
		void Restore(XmlReader reader);
#if !NET10_0_OR_GREATER
		void UpdateFrom(Window window);
#endif
		/// <summary>Keeps window bounds visible on the supplied desktop.</summary>
		void ClipToBounds(Desktop desktop);
#if !NET10_0_OR_GREATER
		/// <summary>Restores the saved window placement and panels.</summary>
		void RestoreTo(Window window);
#else
		/// <summary>The visible state to restore after a minimized session.</summary>
		WindowDisplayState RestoredState { get; }
#endif
	}
}
