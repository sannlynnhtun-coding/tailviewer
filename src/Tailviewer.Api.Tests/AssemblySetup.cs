using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace Tailviewer.Api.Tests
{
	[SetUpFixture]
	[Parallelizable(ParallelScope.Fixtures)]
	public sealed class AssemblySetup
	{
		[OneTimeSetUp]
		public void SetUp()
		{
			Directory.SetCurrentDirectory(AssemblyDirectory);
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