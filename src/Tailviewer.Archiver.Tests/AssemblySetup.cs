using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace Tailviewer.Archiver.Tests
{
	[SetUpFixture]
	public sealed class AssemblySetup
	{
		public static string WorkDirectory { get; private set; }
		private string _previousDirectory;

		[OneTimeSetUp]
		public void SetUp()
		{
			_previousDirectory = Directory.GetCurrentDirectory();
			WorkDirectory = Path.Combine(AssemblyDirectory, "Run-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(WorkDirectory);
			Directory.SetCurrentDirectory(WorkDirectory);
		}

		[OneTimeTearDown]
		public void TearDown()
		{
			Directory.SetCurrentDirectory(_previousDirectory);
		}

		public static string AssemblyDirectory
		{
			get
			{
				return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			}
		}
	}
}