using System;
using System.IO;
using System.Reflection;

namespace Tailviewer.Core
{
	/// <summary>
	///     Extension methods for the <see cref="Assembly" /> class.
	/// </summary>
	public static class AssemblyExtensions
	{
		/// <summary>
		///     Returns the folder this assembly is located in.
		/// </summary>
		/// <param name="assembly"></param>
		/// <returns></returns>
		public static string GetFolder(this Assembly assembly)
		{
			return Path.GetDirectoryName(assembly.Location);
		}
	}
}
