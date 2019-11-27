// Copyright (c) 2018 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Icu;
using NUnit.Framework;
using SIL.IO;
using SIL.LCModel.Core.Attributes;

namespace SIL.LCModel.Core.Text
{
#if NETCOREAPP2_1
	[Ignore("Platform is not supported in NUnit for .NET Core 2")]
#else
	[Platform(Exclude = "Linux",
		Reason = "These tests require ICU4C installed from NuGet packages which isn't available on Linux")]
#endif
	[TestFixture]
	public class CustomIcuFallbackTests
	{
		private string _tmpDir;
		private string _pathEnvironmentVariable;
		private const string DefaultIcuLibraryVersionMajor = "62";
		private const string CustomIcuLibraryVersionMajor = "54";
		private List<string> _dirsToDelete;
		private string _preTestDataDir;
		private string _preTestDataDirEnv;

		private static void CopyFile(string srcPath, string dstDir)
		{
			var fileName = Path.GetFileName(srcPath);
			File.Copy(srcPath, Path.Combine(dstDir, fileName));
		}

		private static string GetArchSubdir(string arch)
		{
			return Path.Combine("lib", $"win-{arch}");
		}

		internal static string OutputDirectory => Path.GetDirectoryName(
			new Uri(
#if NET40
				typeof(CustomIcuFallbackTests).Assembly.CodeBase
#else
				typeof(CustomIcuFallbackTests).GetTypeInfo().Assembly.CodeBase
#endif
				)
			.LocalPath);

		private static string GetIcuDirectory(string arch)
		{
			return Path.Combine(OutputDirectory, GetArchSubdir(arch));
		}

		private string RunTestHelper(string workDir, string exeDir = null)
		{
			if (string.IsNullOrEmpty(exeDir))
				exeDir = _tmpDir;

			using (var process = new Process())
			{
				process.StartInfo.RedirectStandardError = true;
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.WorkingDirectory = workDir;
				var filename = Path.Combine(exeDir, "TestHelper.exe");
				if (!File.Exists(filename))
				{
					// netcore
					process.StartInfo.Arguments = $"{Path.Combine(exeDir, "TestHelper.dll")}";
					filename = "dotnet";
				}

				process.StartInfo.FileName = filename;

				process.Start();
				var output = process.StandardOutput.ReadToEnd();
				process.WaitForExit();
				if (process.ExitCode != 0)
				{
					Console.WriteLine(process.StandardError.ReadToEnd());
				}
				return output.TrimEnd('\r', '\n');
			}
		}

		private static void CopyIcuFiles(string targetDir, string icuVersion)
		{
			foreach (var arch in new[] { "x64", "x86"})
			{
				var fullTargetDir = Path.Combine(targetDir, GetArchSubdir(arch));
				var icuDirectory = GetIcuDirectory(arch);
				CopyFile(Path.Combine(icuDirectory, $"icudt{icuVersion}.dll"), fullTargetDir);
				CopyFile(Path.Combine(icuDirectory, $"icuin{icuVersion}.dll"), fullTargetDir);
				CopyFile(Path.Combine(icuDirectory, $"icuuc{icuVersion}.dll"), fullTargetDir);
			}
		}

		private static void CopyTestFiles(string sourceDir, string targetDir)
		{
			var testHelper = Path.Combine(sourceDir, "TestHelper.exe");
			if (!File.Exists(testHelper))
				testHelper = Path.Combine(sourceDir, "TestHelper.dll");
			CopyFile(testHelper, targetDir);
			var targetIcuDataDir = Path.Combine(targetDir, "IcuData");
			Directory.CreateDirectory(targetIcuDataDir);
			DirectoryHelper.Copy(Path.Combine(sourceDir, "IcuData"), targetIcuDataDir);
			Environment.SetEnvironmentVariable("ICU_DATA", targetIcuDataDir);

			foreach (var file in new[]
			{
				"SIL.LCModel.Core",
				"SIL.LCModel.Utils",
				"icu.net",
				"SIL.Core.Desktop",
				"Microsoft.Extensions.DependencyModel"
			})
			{
				var sourceFile = Path.Combine(sourceDir, $"{file}.dll");
				if (File.Exists(sourceFile))
					CopyFile(sourceFile, targetDir);
			}
		}

		[SetUp]
		public void Setup()
		{
			_tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			_dirsToDelete.Add(_tmpDir);
			Directory.CreateDirectory(_tmpDir);
			foreach (var arch in new[] {"x64", "x86"})
			{
				Directory.CreateDirectory(Path.Combine(_tmpDir, GetArchSubdir(arch)));
			}

			CopyTestFiles(OutputDirectory, _tmpDir);

			_pathEnvironmentVariable = Environment.GetEnvironmentVariable("PATH");
		}

		[TearDown]
		public void TearDown()
		{
			Wrapper.Cleanup();
			Wrapper.ConfineIcuVersions(Wrapper.MinSupportedIcuVersion, Wrapper.MaxSupportedIcuVersion);
			Environment.SetEnvironmentVariable("PATH", _pathEnvironmentVariable);
		}

		[TestFixtureSetUp]
		public void FixtureSetUp()
		{
			// Undo the PATH that got set by the InitializeIcu attribute
			Environment.SetEnvironmentVariable("PATH", InitializeIcuAttribute.PreTestPathEnvironment);
			_dirsToDelete = new List<string>();
			_preTestDataDir = Wrapper.DataDirectory;
			_preTestDataDirEnv = Environment.GetEnvironmentVariable("ICU_DATA");
		}

		[TestFixtureTearDown]
		public void FixtureTearDown()
		{
			Thread.Sleep(500);
			foreach (var dir in _dirsToDelete)
			{
				new DirectoryInfo(dir).Delete(true);
			}
			Environment.SetEnvironmentVariable("ICU_DATA", _preTestDataDirEnv);
			Wrapper.DataDirectory = _preTestDataDir;
		}

		[Test]
		public void InitIcuDataDir_CustomIcuVersion()
		{
			CopyIcuFiles(_tmpDir, CustomIcuLibraryVersionMajor);
			Assert.That(RunTestHelper(_tmpDir), Is.EqualTo($"{CustomIcuLibraryVersionMajor}.1{Environment.NewLine}NON_SPACING_MARK{Environment.NewLine}True"));
		}

		[Test]
		public void InitIcuDataDir_FallbackDefaultIcuVersion()
		{
			// NOTE: if this test fails, check that you don't have icuuc54.dll or icuuc62.dll somewhere,
			// e.g. in C:\Program Files (x86)\Common Files\SIL
			CopyIcuFiles(_tmpDir, DefaultIcuLibraryVersionMajor);
			Assert.That(RunTestHelper(_tmpDir), Is.EqualTo($"{DefaultIcuLibraryVersionMajor}.1{Environment.NewLine}PRIVATE_USE_CHAR{Environment.NewLine}False"));
		}

		[Test]
		public void InitIcuDataDir_NoIcuLibrary()
		{
			Assert.That(RunTestHelper(_tmpDir), Is.Empty);
		}
	}
}