using Tailviewer.Api;

// ReSharper disable once CheckNamespace
namespace Tailviewer.Core
{
	/// <summary>
	/// 
	/// </summary>
	public static class FileLogSourceFactory
	{
		/// <summary>Owns a raw text source and adds built-in parsing and original-position columns without buffering the whole file.</summary>
		public static ILogSource ParseText(ILogSource source)
		{
			return new LogSourceColumnAdorner(new GenericTextLogSource(source, new GenericTextLogEntryParser()));
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
