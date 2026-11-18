using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using log4net;
#if NET10_0_OR_GREATER
using Size = Tailviewer.Settings.WindowSize;
#else
using Metrolib;
using Size = System.Windows.Size;
#endif

namespace Tailviewer.Settings
{
	/// <summary>Screen geometry and window fitting supplied by a UI adapter.</summary>
	public sealed class Desktop
	{
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private readonly IReadOnlyCollection<Screen> _screens;

#if !NET10_0_OR_GREATER
		/// <summary>The current screens for the legacy Windows desktop host.</summary>
		public static Desktop Current
		{
			get
			{
				return new Desktop(System.Windows.Forms.Screen.AllScreens.Select(x => new Screen(x)).ToList());
			}
		}

#endif

		/// <summary>The screen geometry used to keep a window visible.</summary>
		public Desktop(IReadOnlyCollection<Screen> screens)
		{
			_screens = screens;
		}

		/// <summary>
		///     Clips the given settings so the window fits into the current desktop.
		/// </summary>
		/// <remarks>
		///     The current implementation tries to **first** move the window to an available
		///     screen and only starts to reduce the width / height of the window if either monitor
		///     has a lower resolution than the current width / height requires.
		/// </remarks>
		/// <param name="window"></param>
		/// <returns>
		///     A new object with corrected position and / or area or the existing object in case no modification was
		///     necessary.
		/// </returns>
		[Pure]
		public Window ClipToBoundaries(Window window)
		{
			// If we didn't detect any monitors then somethings going on, but whatever it may be, we cannot move on...
			if (_screens.Count == 0)
			{
				Log.WarnFormat("Unable to fit window into current desktop area: Not a single monitor was detected!");
				return window;
			}

			// We'll first check if the window fits into the current virtual desktop area.
			// If it consists of more than one screen, then we'll try to figure out if there's a portion
			// of the window that's hidden, and if there is, only then will we try to move / resize the
			// window to become visible again.
			if (Contains(window))
				return window;

			Log.InfoFormat("The window doesn't fit into the current desktop area anymore, trying to find a more suitable location...");

			// We'll try to place the window on the closest screen to its current position.
			// This is incredibly useful if the window is only partially hidden.
			var screensSortedByDistance = SortScreensByDistanceToWindow(window, _screens);
			foreach (var screen in screensSortedByDistance)
			{
				if (screen.TryMoveWindowIntoScreen(window, out var newWindow))
				{
					Log.InfoFormat("Found new window location: {0}", newWindow);
					return newWindow;
				}
			}

			Log.InfoFormat("The window's size doesn't fit into any of the available monitor(s): Resizing the window so it will fit");

			// Moving the window alone doesn't cut it, we'll have to reduce the windows size to accomodate
			// the apparent change in screens.
			// We'll ditch our closest screen approach and instead try to move the window to the screen with
			// the greatest screen area.
			var screensSortedByArea = SortScreensByArea(_screens);
			var modifiedSettings = screensSortedByArea[0].FitWindowInto(window);
			Log.InfoFormat("Found new window location and size: {0}", modifiedSettings);
			return modifiedSettings;
		}

		/// <summary>Whether the point or rectangle is contained.</summary>
		[Pure]
		public bool Contains(Rectangle window)
		{
			// We'll cut away from the given window until all screens have performed a cut.
			IReadOnlyList<Rectangle> windowPortions = new List<Rectangle> {window};
			foreach (var screen in _screens)
			{
				windowPortions = screen.CutAway(windowPortions);
			}

			// If the screens cut away every single portion of the window then we can conclude
			// that everything is showing up as expected (even if the window is placed
			// among multiple screen boundaries).
			return windowPortions.Count == 0;
		}

		[Pure]
		private static IReadOnlyCollection<Screen> SortScreensByDistanceToWindow(
			Rectangle window,
			IReadOnlyCollection<Screen> screens)
		{
			var centerPoint = new Point(window.Left + window.Width / 2, window.Top + window.Height / 2);
			var sorted = screens.OrderBy(x => x.Bounds.Center.SquaredDistanceTo(centerPoint)).ToList();
			return sorted;
		}

		[Pure]
		private static IReadOnlyList<Screen> SortScreensByArea(IReadOnlyCollection<Screen> screens)
		{
			var sorted = screens.OrderByDescending(x => x.WorkingArea.Area).ToList();
			return sorted;
		}

		/// <summary>A point in the selected coordinate units.</summary>
		public struct Point
		{
			/// <summary>The horizontal coordinate.</summary>
			public readonly double X;
			/// <summary>The vertical coordinate.</summary>
			public readonly double Y;

			/// <summary>A point in the selected coordinate units.</summary>
			public Point(double x, double y)
			{
				X = x;
				Y = y;
			}

			/// <summary>Returns the distance to another point.</summary>
			[Pure]
			public double DistanceTo(Point other)
			{
				return Math.Sqrt(SquaredDistanceTo(other));
			}

			/// <summary>Returns squared distance to another point.</summary>
			[Pure]
			public double SquaredDistanceTo(Point other)
			{
				var dX = X - other.X;
				var dY = Y - other.Y;
				return dX * dX + dY * dY;
			}

			#region Overrides of ValueType

			/// <inheritdoc />
			public override string ToString()
			{
				return $"{X}, {Y}";
			}

			#endregion
		}

		/// <summary>A rectangle in the selected coordinate units.</summary>
		public class Rectangle
		{
			#region Equality members

			/// <summary>Compares rectangle coordinates and size.</summary>
			protected bool Equals(Rectangle other)
			{
				return _size.Equals(other._size) && _topLeft.Equals(other._topLeft);
			}

			/// <inheritdoc />
			public override bool Equals(object obj)
			{
				if (ReferenceEquals(null, obj)) return false;
				if (ReferenceEquals(this, obj)) return true;
				if (obj.GetType() != this.GetType()) return false;
				return Equals((Rectangle) obj);
			}

			/// <inheritdoc />
			public override int GetHashCode()
			{
				unchecked
				{
					return (_size.GetHashCode() * 397) ^ _topLeft.GetHashCode();
				}
			}

			/// <inheritdoc />
			public static bool operator ==(Rectangle left, Rectangle right)
			{
				return Equals(left, right);
			}

			/// <inheritdoc />
			public static bool operator !=(Rectangle left, Rectangle right)
			{
				return !Equals(left, right);
			}

			#endregion

			private readonly Size _size;
			private readonly Point _topLeft;

			/// <summary>A rectangle in the selected coordinate units.</summary>
			public Rectangle()
			{}
			/// <summary>A rectangle in the selected coordinate units.</summary>
			public Rectangle(double left, double top, double width, double height)
				: this(new Point(left, top), new Size(width, height))
			{}

			/// <summary>A rectangle in the selected coordinate units.</summary>
			public Rectangle(Point topLeft, Size size)
			{
				_topLeft = topLeft;
				_size = size;
			}

			/// <summary>The top coordinate.</summary>
			public double Top
			{
				get { return _topLeft.Y; }
			}

			/// <summary>The left coordinate.</summary>
			public double Left
			{
				get { return _topLeft.X; }
			}

			/// <summary>The bottom coordinate.</summary>
			public double Bottom
			{
				get { return _topLeft.Y + _size.Height; }
			}

			/// <summary>The right coordinate.</summary>
			public double Right
			{
				get { return _topLeft.X + _size.Width; }
			}

			/// <summary>The upper-left corner.</summary>
			public Point TopLeft
			{
				get { return _topLeft; }
			}

			/// <summary>The upper-right corner.</summary>
			public Point TopRight
			{
				get { return new Point(Right, Top); }
			}

			/// <summary>The lower-right corner.</summary>
			public Point BottomRight
			{
				get { return new Point(Right, Bottom); }
			}

			/// <summary>The lower-left corner.</summary>
			public Point BottomLeft
			{
				get { return new Point(Left, Bottom); }
			}

			/// <summary>The rectangle center.</summary>
			public Point Center
			{
				get { return new Point(_topLeft.X + _size.Width / 2, _topLeft.Y + _size.Height / 2); }
			}

			/// <summary>The rectangle area.</summary>
			public double Area
			{
				get { return _size.Width * _size.Height; }
			}

			/// <summary>The window width.</summary>
			public double Width
			{
				get { return _size.Width; }
			}

			/// <summary>The window height.</summary>
			public double Height
			{
				get { return _size.Height; }
			}

			/// <summary>The rectangle size.</summary>
			public Size Size
			{
				get { return _size; }
			}

			#region Overrides of Object

			/// <inheritdoc />
			public override string ToString()
			{
				return $"(({TopLeft}) ({Size}))";
			}

			#endregion

			/// <summary>
			///     Tests if this screen fully contains the given window.
			///     If the window is only partially contained (e.g. is bigger than this screen) or if
			///     the window is completely outside this screen, then false is returned.
			/// </summary>
			/// <param name="settings"></param>
			/// <returns></returns>
			[Pure]
			public bool Contains(Rectangle settings)
			{
				return !(settings.Left < Left ||
				         settings.Top < Top ||
				         settings.Right > Right ||
				         settings.Bottom > Bottom);
			}

			/// <summary>
			/// Returns the shape (split up into zero or more rectangles) that results if one were
			/// to cut away the portion of this rectangle, which intersects with the given one.
			/// </summary>
			/// <param name="other"></param>
			/// <returns></returns>
			[Pure]
			public IReadOnlyList<Rectangle> Cut(Rectangle other)
			{
				// If the other rectangle fully contains this one then nothing
				// remains after the cut...
				if (other.Contains(this))
					return new Rectangle[0];

				// If on the other hand, the two rectangles don't overlap even one bit,
				// then nothing changes due to the cut and we can simply return this rectangle
				if (!other.Overlap(this))
					return new[]{this};

				// We want to find out the area of this rectangle, which is left when the overlapping area with the other
				// rectangle is cut away. This can be achieved in the following way:
				// We first cut this rectangle into pieces along the four (infinite) lines of the other rectangle.
				// Now we should have a list of up to 9 rectangles which are either **completely** outside or
				// completely inside the screen, i.e. there's no intersections anymore.
				// All that is left to do now is to remove those rectangles which are part of the screen and we're
				// left with those hat are outside of the screen.
				// This algorithm accounts for all types of intersections:
				// - Intersection along one edge of the screen resulting in half of the window to be cut
				// - Intersection along two edges of the screen, resulting in a "corner" to be cut, yielding an L-shape
				// - Full intersection in which the screen cuts away a hole inside the window.
				//var pass1 = CutOnce(new[] {this});
				//var pass2 = CutOnce(pass1);
				var pass1 = new[] {this}.SelectMany(rectangle => rectangle.CutHorizontal(other.Top)).ToList();
				var pass2 = pass1.SelectMany(rectangle => rectangle.CutHorizontal(other.Bottom)).ToList();
				var pass3 = pass2.SelectMany(rectangle => rectangle.CutVertical(other.Left)).ToList();
				var pass4 = pass3.SelectMany(rectangle => rectangle.CutVertical(other.Right)).ToList();
				// The result is almost there, after we've cut up this rectangle, we want to remove
				// those portions that are completely inside the screen, leaving only those parts which do not.
				var finalResult = pass4.Where(x => !other.Contains(x)).ToList();
				return finalResult;
			}

			[Pure]
			private IEnumerable<Rectangle> CutHorizontal(double y)
			{
				if (y <= Top || y >= Bottom)
					return new[] {this};

				return new[]
				{
					new Rectangle(Left, Top, Width, y - Top),
					new Rectangle(Left, y, Width, Bottom - y),
				};
			}

			[Pure]
			private IEnumerable<Rectangle> CutVertical(double x)
			{
				if (x <= Left || x >= Right)
					return new[] {this};

				return new[]
				{
					new Rectangle(Left, Top, x - Left, Height),
					new Rectangle(x, Top, Right - x, Height)
				};
			}

			/// <summary>Whether the two rectangles overlap.</summary>
			[Pure]
			public bool Overlap(Rectangle rectangle)
			{
				return Left <= rectangle.Right && Right >= rectangle.Left &&
				       Top <= rectangle.Bottom && Bottom >= rectangle.Top;
			}

			/// <summary>Whether the point or rectangle is contained.</summary>
			[Pure]
			public bool Contains(Point topLeft)
			{
				return !(topLeft.X < Left || topLeft.X > Right ||
				       topLeft.Y < Top || topLeft.Y > Bottom);
			}
		}

		/// <summary>Window bounds and maximized state.</summary>
		public sealed class Window
			: Rectangle
		{
			/// <summary>Window bounds and maximized state.</summary>
			public Window(Point topLeft, Size size)
				: base(topLeft, size)
			{ }

#if !NET10_0_OR_GREATER
			/// <summary>Window bounds and maximized state.</summary>
			public Window(WindowSettings settings)
				: this(new Point(settings.Left, settings.Top), new Size(settings.Width, settings.Height))
			{ }

#endif

			/// <summary>Whether the fitted window should be maximized.</summary>
			public bool IsMaximized;
		}

		/// <summary>
		///     Represents one screen connected to the current system.
		/// </summary>
		public sealed class Screen
		{
			/// <summary>The complete screen bounds.</summary>
			public readonly Rectangle Bounds;
			/// <summary>The usable screen bounds.</summary>
			public readonly Rectangle WorkingArea;

			/// <summary>Screen bounds and its usable working area.</summary>
			public Screen(Point topLeft, Size size)
			{
				Bounds = new Rectangle(topLeft, size);
				WorkingArea = new Rectangle(topLeft, size);
			}

#if !NET10_0_OR_GREATER
			/// <summary>Screen bounds and its usable working area.</summary>
			public Screen(System.Windows.Forms.Screen screen)
			{
				Bounds = new Rectangle(new Point(screen.Bounds.X, screen.Bounds.Y),
				                       new Size(screen.Bounds.Width, screen.Bounds.Height));
				WorkingArea = new Rectangle(new Point(screen.WorkingArea.X, screen.WorkingArea.Y),
				                            new Size(screen.WorkingArea.Width, screen.WorkingArea.Height));
			}
#else
			/// <summary>Creates a screen with explicit bounds and usable working area in the same units.</summary>
			public Screen(Rectangle bounds, Rectangle workingArea) { Bounds = bounds; WorkingArea = workingArea; }
#endif

			/// <summary>
			///    Tries to bring the given window into view.
			/// </summary>
			/// <param name="window"></param>
			/// <param name="movedWindow"></param>
			/// <returns></returns>
			public bool TryMoveWindowIntoScreen(Window window, out Window movedWindow)
			{
				movedWindow = null;

				if (window.Width > WorkingArea.Size.Width)
					return false;
				if (window.Height > WorkingArea.Size.Height)
					return false;

				// The window will fit, we'll just have to move it into view.
				// Let's place it so its centered on the screen...
				var halfWidth = window.Width / 2;
				var halfHeight = window.Height / 2;
				var center = WorkingArea.Center;
				var topLeft = new Point(center.X - halfWidth, center.Y - halfHeight);
				movedWindow = new Window(topLeft, window.Size);
				return true;
			}

			/// <summary>Fits a window to the working area.</summary>
			[Pure]
			public Window FitWindowInto(Window settings)
			{
				var newSettings = new Window(WorkingArea.TopLeft, WorkingArea.Size);
				newSettings.IsMaximized = true;
				return newSettings;
			}

			/// <summary>Removes areas covered by this screen.</summary>
			[Pure]
			public IReadOnlyList<Rectangle> CutAway(IReadOnlyList<Rectangle> windowPortions)
			{
				var result = new List<Rectangle>();
				foreach (var window in windowPortions)
				{
					// We do NOT use the working area on purpose because it's not unusual for the taskbar
					// to be clipped to the side, effectively hiding small parts of the window.
					result.AddRange(window.Cut(Bounds));
				}

				return result;
			}
		}
	}
}
