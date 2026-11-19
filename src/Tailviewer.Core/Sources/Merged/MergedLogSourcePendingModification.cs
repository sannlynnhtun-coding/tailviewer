using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System;
using Tailviewer.Api;

// ReSharper disable once CheckNamespace
namespace Tailviewer.Core
{
	internal readonly struct MergedLogSourcePendingModification
	{
		public readonly ILogSource LogSource;
		public readonly LogSourceModification Modification;

		public MergedLogSourcePendingModification(ILogSource logSource, LogSourceModification modification)
		{
			LogSource = logSource;
			Modification = modification;
		}

		public override string ToString()
		{
			return $"{Modification} ({LogSource})";
		}

		/// <summary>
		///     Returns an optimized list of changes which causes identical behaviour as the input list, but which may
		///     execute faster.
		/// </summary>
		/// <remarks>
		///     This method looks at the list of modifications and identifies resets. All modifications to the SAME source
		///     prior to this reset will be ignored and not forwarded.
		/// </remarks>
		/// <remarks>
		///     Tail invalidations trim earlier appends to the still-valid prefix, avoiding duplicate reads of rebuilt lines.
		/// </remarks>
		/// <param name="pendingModifications"></param>
		/// <returns></returns>
		[Pure]
		public static IReadOnlyList<MergedLogSourcePendingModification> Optimize(
			IEnumerable<MergedLogSourcePendingModification> pendingModifications)
		{
			var modifications = new List<MergedLogSourcePendingModification>();

			foreach(var modification in pendingModifications)
			{
				var logFile = modification.LogSource;
				var section = modification.Modification;
				if (section.IsReset())
				{
					modifications.RemoveAll(x => x.LogSource == logFile);
				}
				else if (section.IsRemoved(out var removed) && removed.Count > 0)
				{
					for (var index = modifications.Count - 1; index >= 0; --index)
					{
						var previous = modifications[index];
						if (previous.LogSource != logFile || !previous.Modification.IsAppended(out var appended)) continue;
						var keepCount = Math.Min(appended.Count, removed.Index.Value - appended.Index.Value);
						if (keepCount <= 0) modifications.RemoveAt(index);
						else if (keepCount < appended.Count)
							modifications[index] = new MergedLogSourcePendingModification(logFile, LogSourceModification.Appended(appended.Index, keepCount));
					}
				}

				modifications.Add(modification);
			}

			return modifications;
		}
	}
}
