using Tailviewer.Api;

using System;
using System.Threading;

// ReSharper disable once CheckNamespace
namespace Tailviewer.Core
{
	/// <summary>
	/// 
	/// </summary>
	public static class FileLogSourceFactory
	{
		/// <summary>Creates a metadata/derived-column view; the caller retains ownership of the input source.</summary>
		public static ILogSource CreateMetadataView(ITaskScheduler scheduler, ILogSource source)
		{
			return new LogSourceColumnAdorner(new LogSourcePropertyAdorner(scheduler, source, TimeSpan.FromMilliseconds(100)));
		}

		/// <summary>Creates a multiline view; the caller retains ownership of the input source.</summary>
		public static ILogSource CreateMultiLineView(ITaskScheduler scheduler, ILogSource source)
		{
			return new MultiLineLogSource(scheduler, source, TimeSpan.Zero);
		}

		/// <summary>Owns a raw text source and adds built-in parsing and original-position columns without buffering the whole file.</summary>
		public static ILogSource ParseText(ILogSource source)
		{
			return ParseText(source, null);
		}

		/// <summary>Creates an owned parsed source and notifies derived caches when the parser's read epoch resets.</summary>
		public static ILogSource ParseText(ILogSource source, Action reset)
		{
			return new LogSourceColumnAdorner(new GenericTextLogSource(source, () => new GenericTextLogEntryParser(), reset));
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="services"></param>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public static ILogSource OpenRead(IServiceContainer services, string fileName)
		{
			return new FileLogSource(services, fileName);
		}
	}
}
