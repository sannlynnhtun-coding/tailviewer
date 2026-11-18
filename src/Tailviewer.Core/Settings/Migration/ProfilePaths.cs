#if NET10_0_OR_GREATER
using System;
using System.IO;

namespace Tailviewer.Settings
{
    internal static class ProfilePaths
    {
        internal static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tailviewer.Avalonia");
        internal static string BookmarksFile => Path.Combine(Folder, "bookmarks.xml");
    }
}
#endif
