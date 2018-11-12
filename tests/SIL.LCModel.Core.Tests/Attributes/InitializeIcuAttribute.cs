// Copyright (c) 2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.IO;
using System.Reflection;
using Icu;
using Microsoft.Win32;
using NUnit.Framework;

namespace SIL.LCModel.Core.Attributes
{
	[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class)]
	public class InitializeIcuAttribute : TestActionAttribute
	{
		/// <summary/>
		public override ActionTargets Targets => ActionTargets.Suite;

		public string IcuDataPath { get; set; }

		public int IcuVersion { get; set; }

		public static string PreTestPathEnvironment { get; private set; }

		public override void BeforeTest(TestDetails testDetails)
		{
			base.BeforeTest(testDetails);

			PreTestPathEnvironment = Environment.GetEnvironmentVariable("PATH");

			if (IcuVersion > 0)
				Wrapper.ConfineIcuVersions(IcuVersion);

			try
			{
				Wrapper.Init();
			}
			catch (Exception e)
			{
				Console.WriteLine($"InitializeIcuAttribute failed when calling Wrapper.Init() with {e.GetType()}: {e.Message}");
			}

			string dir = null;
			if (string.IsNullOrEmpty(IcuDataPath))
			{
				var environDataPath = Environment.GetEnvironmentVariable("ICU_DATA");
				if (!string.IsNullOrEmpty(environDataPath))
				{
					dir = environDataPath;
				}
				else
				{
					using (RegistryKey userKey = Registry.CurrentUser.OpenSubKey(@"Software\SIL"))
					using (RegistryKey machineKey = Registry.LocalMachine.OpenSubKey(@"Software\SIL"))
					{
						const string icuDirValueName = "Icu54DataDir";
						if (userKey?.GetValue(icuDirValueName) != null)
							dir = userKey.GetValue(icuDirValueName, null) as string;
						else if (machineKey?.GetValue(icuDirValueName) != null)
							dir = machineKey.GetValue(icuDirValueName, null) as string;
					}
				}
			}
			else if (Path.IsPathRooted(IcuDataPath))
			{
				dir = IcuDataPath;
			}
			else
			{
				Uri uriBase = new Uri(Assembly.GetExecutingAssembly().CodeBase);
				string codeDir = Path.GetDirectoryName(Uri.UnescapeDataString(uriBase.AbsolutePath));
				if (codeDir != null)
					dir = Path.Combine(codeDir, IcuDataPath);
			}

			if (!string.IsNullOrEmpty(dir))
				Environment.SetEnvironmentVariable("ICU_DATA", dir);

			try
			{
				Text.CustomIcu.InitIcuDataDir();
			}
			catch (Exception e)
			{
				Console.WriteLine($"InitializeIcuAttribute failed with {e.GetType()}: {e.Message}");
			}
		}

		public override void AfterTest(TestDetails testDetails)
		{
			Wrapper.Cleanup();
			Environment.SetEnvironmentVariable("PATH", PreTestPathEnvironment);
			base.AfterTest(testDetails);
		}
	}
}
