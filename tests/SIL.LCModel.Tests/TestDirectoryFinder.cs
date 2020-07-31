// Copyright (c) 2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.IO;
using System.Reflection;

namespace SIL.LCModel
{
	public static class TestDirectoryFinder
	{
		public static string CodeDirectory
		{
			get
			{
				Uri uriBase = new Uri(Assembly.GetCallingAssembly().CodeBase);
				return Path.GetDirectoryName(Uri.UnescapeDataString(uriBase.AbsolutePath));
			}
		}

		public static string RootDirectory
		{
			get
			{
				// we'll assume the executing assembly is <root>/artifacts/Debug/SIL.LCModel.Tests.dll,
				string dir = CodeDirectory;
				dir = Path.GetDirectoryName(dir);		// strip the parent directory name (net461)
				dir = Path.GetDirectoryName(dir);       // strip the parent directory name (Debug)
				dir = Path.GetDirectoryName(dir);       // strip the parent directory again (artifacts)
				return dir;
			}
		}

		public static string ProjectsDirectory => Path.Combine(CodeDirectory, "Projects");

		internal static string TestDataDirectory => Path.Combine(RootDirectory, "tests", "SIL.LCModel.Tests", "TestData");

		public static string TemplateDirectory => Path.Combine(CodeDirectory, "Templates");

		internal static ILcmDirectories LcmDirectories { get; } = new TestLcmDirectories(ProjectsDirectory);
	}
}
