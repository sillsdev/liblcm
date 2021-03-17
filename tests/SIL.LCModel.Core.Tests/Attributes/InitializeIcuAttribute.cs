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

		public const int CustomIcuVersion = 54;

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
				Console.WriteLine($"InitializeIcuAttribute: ERROR: failed when calling Wrapper.Init() with {e.GetType()}: {e.Message}");
			}

			EnsureIcuDataEnvironmentVariableIsSet();

			try
			{
				Text.CustomIcu.InitIcuDataDir();
			}
			catch (Exception e)
			{
				Console.WriteLine($"InitializeIcuAttribute: ERROR: failed with {e.GetType()}: {e.Message}");
			}
			}

		private void EnsureIcuDataEnvironmentVariableIsSet()
		{
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
					dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
						"SIL", $"Icu{CustomIcuVersion}", $"icudt{CustomIcuVersion}l");
					if (!Directory.Exists(dir))
					{
						dir = null;
						using (var userKey = Registry.CurrentUser.OpenSubKey(@"Software\SIL"))
						using (var machineKey = Registry.LocalMachine.OpenSubKey(@"Software\SIL"))
					{
							var icuDirValueName = $"Icu{CustomIcuVersion}DataDir";
						if (userKey?.GetValue(icuDirValueName) != null)
							dir = userKey.GetValue(icuDirValueName, null) as string;
						else if (machineKey?.GetValue(icuDirValueName) != null)
							dir = machineKey.GetValue(icuDirValueName, null) as string;
					}
				}
			}
			}
			else if (Path.IsPathRooted(IcuDataPath))
			{
				dir = IcuDataPath;
			}
			else
			{
				var uriBase = new Uri(Assembly.GetExecutingAssembly().CodeBase);
				var codeDir = Path.GetDirectoryName(Uri.UnescapeDataString(uriBase.AbsolutePath));
				if (codeDir != null)
					dir = Path.Combine(codeDir, IcuDataPath);
			}

			if (string.IsNullOrEmpty(dir))
			{
				Console.WriteLine("InitializeIcuAttribute: ERROR: can't determine directory for ICU_DATA.");
				return;
			}

			// dir should point to the directory that contains nfc_fw.nrm and nfkc_fw.nrm
			// (i.e. icudt54l).
			if (!File.Exists(Path.Combine(dir, "nfc_fw.nrm")) ||
				!File.Exists(Path.Combine(dir, "nfkc_fw.nrm")))
			{
				if (Directory.Exists(Path.Combine(dir, $"icudt{CustomIcuVersion}l")))
					dir = Path.Combine(dir, $"icudt{CustomIcuVersion}l");
			}

			if (!File.Exists(Path.Combine(dir, "nfc_fw.nrm")) ||
				!File.Exists(Path.Combine(dir, "nfkc_fw.nrm")))
			{
				Console.WriteLine($"InitializeIcuAttribute: ERROR: can't find files nfc_fw.nrm and/or nfkc_fw.nrm in {dir}");
				return;
			}

			Console.WriteLine($"InitializeIcuAttribute: Setting ICU_DATA to {dir}");
			Environment.SetEnvironmentVariable("ICU_DATA", dir);
		}

		public override void AfterTest(TestDetails testDetails)
		{
			Wrapper.Cleanup();
			Environment.SetEnvironmentVariable("PATH", PreTestPathEnvironment);
			base.AfterTest(testDetails);
		}
	}
}
