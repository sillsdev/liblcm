// Copyright (c) 2018-2022 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Reflection;
using SIL.LCModel.Core.Text;

namespace Icu.Tests
{
	internal class TestHelper
	{
		public static void Main(string[] args)
		{
			// The only purpose of this TestHelper app is to output the ICU version
			// so that we can run unit tests that test loading of our custom ICU
			// or fallback to default ICU
			string baseDir;
			if (Environment.CommandLine.StartsWith("dotnet"))
			{
				// in a netcore build we execute dotnet with the assembly as the first argument
				baseDir = args?.Length > 1 ? args[1] : CodeDir;
			}
			else
			{
				// The first argument is the directory to use as IcuData
				baseDir = args?.Length > 0 ? args[0] : CodeDir;
			}
			if (baseDir != null)
			{
				Environment.SetEnvironmentVariable("ICU_DATA", Path.Combine(baseDir, "IcuData"));
			}

			CustomIcu.InitIcuDataDir();
			Console.WriteLine(Wrapper.IcuVersion);
			Console.WriteLine(Character.GetCharType('\xF171'));
			Console.WriteLine(CustomIcu.HaveCustomIcuLibrary);
			Wrapper.Cleanup();
		}

		private static string CodeDir
		{
			get
			{
				var uriBase = new Uri(Assembly.GetExecutingAssembly().CodeBase);
				return Path.GetDirectoryName(Uri.UnescapeDataString(uriBase.AbsolutePath));
			}
		}
	}
}
