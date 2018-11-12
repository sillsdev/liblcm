// Copyright (c) 2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;
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
			var baseDir = args?.Length > 0 ? args[0] : CodeDir;
			SetIcuDataDirectory(baseDir, "IcuData");
			CustomIcu.InitIcuDataDir();
			Console.WriteLine(Wrapper.IcuVersion);
			Console.WriteLine(Character.GetCharType('\xF171'));
			Console.WriteLine(CustomIcu.HaveCustomIcuLibrary);
			Wrapper.Cleanup();
		}

		private static void SetIcuDataDirectory(string baseDir, string icuDataPath)
		{
			string dir = null;
			if (string.IsNullOrEmpty(icuDataPath))
			{
				var environDataPath = Environment.GetEnvironmentVariable("ICU_DATA");
				if (!string.IsNullOrEmpty(environDataPath))
				{
					dir = environDataPath;
				}
				else
				{
					using (var userKey = Registry.CurrentUser.OpenSubKey(@"Software\SIL"))
					using (var machineKey = Registry.LocalMachine.OpenSubKey(@"Software\SIL"))
					{
						const string icuDirValueName = "Icu54DataDir";
						if (userKey?.GetValue(icuDirValueName) != null)
							dir = userKey.GetValue(icuDirValueName, null) as string;
						else if (machineKey?.GetValue(icuDirValueName) != null)
							dir = machineKey.GetValue(icuDirValueName, null) as string;
					}
				}
			}
			else if (Path.IsPathRooted(icuDataPath))
			{
				dir = icuDataPath;
			}
			else
			{
				var codeDir = baseDir;
				if (codeDir != null)
					dir = Path.Combine(codeDir, icuDataPath);
			}

			if (!string.IsNullOrEmpty(dir))
				Environment.SetEnvironmentVariable("ICU_DATA", dir);
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
