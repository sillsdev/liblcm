// Copyright (c) 2003-2021 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Security.Permissions;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;
using SIL.Extensions;
using SIL.PlatformUtilities;

namespace SIL.LCModel.Utils
{
	/// <summary/>
	public static class MiscUtils
	{
		/// <summary>
		/// Universal points-per-inch factor
		/// </summary>
		public const int kdzmpInch = 72000;

		private static readonly Regex kXmlCharEntity = new Regex(@"&#x([0-9a-f]{1,4});", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		/// <summary>
		/// Like string.IndexOf, returns the place where the subsequence occurs (or -1).
		/// Throws if source or target is null or target is empty, or idxStart is negative.
		/// </summary>
		public static int IndexOfSubArray(this byte[] source, byte[] target, int idxStart = 0)
		{
			byte first = target[0];
			int targetLength = target.Length;
			if (targetLength == 1)
				return Array.IndexOf(source, first, idxStart); // probably more efficient, and code below won't work.
			int lastStartPosition = source.Length - targetLength;
			for (int i = idxStart; i <= lastStartPosition; i++)
			{
				if (source[i] != first)
					continue;
				for (int j = 1; j < targetLength; j++)
					if (source[i + j] != target[j])
						break;
					else if (j == targetLength - 1)
						return i;
			}
			return -1;
		}

		/// <summary>
		/// Modify the input so that numbers will sort alphabetically.
		/// Specifically, any sequence of digits is replaced by a sequence of 10 digits by adding leading zeros.
		/// This currently ignores existing leading zeros, which means that 1.01 will sort the same as 1.1,
		/// since both map to 000000001.0000000001.
		/// I haven't yet tried to handle negative numbers and haven't figured out whether they will work
		/// without special attention.
		/// </summary>
		public static string NumbersAlphabeticKey(string input)
		{
			var output = new StringBuilder();
			int ich = 0;
			while (ich < input.Length)
			{
				var ch = input[ich];
				if (ch < '0' || ch > '9')
				{
					output.Append(ch);
					ich++;
					continue;
				}
				int ichLim = ich + 1;
				while (ichLim < input.Length && input[ichLim] >= '0' && input[ichLim] <= '9')
					ichLim++;
				for (int i = 0; i < 10 - (ichLim - ich); i++)
					output.Append('0');
				for (int i = ich; i < ichLim; i++)
					output.Append(input[i]);
				ich = ichLim;
			}
			return output.ToString();
		}

		/// <summary>
		/// Return the subarray from start for count items.
		/// </summary>
		public static byte[] SubArray(this byte[] source, int start, int count)
		{
			int realCount = Math.Min(count, source.Length - start);
			var result = new byte[realCount];
			Array.Copy(source, start, result, 0, realCount);
			return result;
		}

		/// <summary>
		/// Replace the indicated range of the input with the replacement. Will throw if not a valid
		/// sub-range of the input.
		/// </summary>
		public static byte[] ReplaceSubArray(this byte[] input, int start, int length, byte[] replacement)
		{
			var result = new byte[input.Length + replacement.Length - length];
			Array.Copy(input, 0, result, 0, start);
			Array.Copy(replacement, 0, result, start, replacement.Length);
			Array.Copy(input, start + length, result, start + replacement.Length, input.Length - start - length);
			return result;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// This acts like a compareTo method when you are dealing with hex numbers as strings
		/// </summary>
		/// <param name="sx">first hex number to compare</param>
		/// <param name="sy">second hex number to compare</param>
		/// <returns>Less than zero if sx is less than sy; 0 if equal; etc.</returns>
		/// ------------------------------------------------------------------------------------
		public static int CompareHex(string sx, string sy)
		{
			if (sx != null && sx.StartsWith("0x", true, CultureInfo.InvariantCulture))
				sx = sx.Substring(2);
			if (sy != null && sy.StartsWith("0x", true, CultureInfo.InvariantCulture))
				sy = sy.Substring(2);
			int x = (sx == String.Empty) ? 0 :
				int.Parse(sx, NumberStyles.HexNumber, null);
			int y = (sy == String.Empty) ? 0 :
				int.Parse(sy, NumberStyles.HexNumber, null);
			return x.CompareTo(y);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets a guid from a string.
		/// </summary>
		/// <param name="sGuid"></param>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public static Guid GetGuidFromObjData(string sGuid)
		{
			char[] guidChars = sGuid.ToCharArray();

			int a = (guidChars[1] << 16) | guidChars[0];
			short b = (short)guidChars[2];
			short c = (short)guidChars[3];
			byte[] d = new byte[8];

			d[0] = (byte)((short)guidChars[4] & 0x00FF);
			d[1] = (byte)((short)guidChars[4] >> 8);

			d[2] = (byte)((short)guidChars[5] & 0x00FF);
			d[3] = (byte)((short)guidChars[5] >> 8);

			d[4] = (byte)((short)guidChars[6] & 0x00FF);
			d[5] = (byte)((short)guidChars[6] >> 8);

			d[6] = (byte)((short)guidChars[7] & 0x00FF);
			d[7] = (byte)((short)guidChars[7] >> 8);

			return new Guid(a, b, c, d);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets a string from a guid that is the binary representation of the guid
		/// </summary>
		/// <param name="guid"></param>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public static string GetObjDataFromGuid(Guid guid)
		{
			byte[] rgGuid = guid.ToByteArray();
			char[] guidChars = new char[8];
			for (int i = 0; i < 8; i++)
				guidChars[i] = (char)(rgGuid[i * 2 + 1] << 8 | rgGuid[i * 2]);

			return new string(guidChars);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Return the local server name.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string LocalServerName
		{
			get {return Environment.MachineName + "\\SILFW";}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Checks to see if the current user has admin priviledges.  This will return 'true' if
		/// they are an administator.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static bool IsUserAdmin
		{
			get
			{
				using (WindowsIdentity idWindow = WindowsIdentity.GetCurrent())
				{
					WindowsPrincipal winPrince = new WindowsPrincipal(idWindow);
					return winPrince.IsInRole(WindowsBuiltInRole.Administrator);
				}
			}
		}

		/// <summary>
		/// Returns <c>true</c> if we're running on Windows, otherwise <c>false</c>.
		/// </summary>
		[Obsolete("Use Platform.IsWindows in SIL.Core instead")]
		public static bool IsWindows => Platform.IsWindows;

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Returns <c>true</c> if we're running on Unix, otherwise <c>false</c>.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Obsolete("Use Platform.IsUnix in SIL.Core instead")]
		public static bool IsUnix => Platform.IsUnix;

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Returns <c>true</c> if we're running on MacOSX, otherwise <c>false</c>.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Obsolete("Use Platform.IsMac in SIL.Core instead")]
		public static bool IsMac => Platform.IsMac;

		/// <summary>
		/// Returns <c>true</c> if we're running on XP, otherwise <c>false</c>.
		/// </summary>
		public static bool IsWinXp
		{
			get
			{
				if (Environment.OSVersion.Platform != PlatformID.Win32NT)
					return false;

				if (Environment.OSVersion.Version.Major != 5)
					return false;

				if (Environment.OSVersion.Version.Minor != 1)
					return false;

				return true;
			}
		}

		/// <summary>
		/// Returns <c>true</c> if we're running on Vista, Windows7 or newer, otherwise <c>false</c>.
		/// </summary>
		public static bool IsWinVistaOrNewer
		{
			get
			{
				if (Environment.OSVersion.Platform != PlatformID.Win32NT)
					return false;

				if (Environment.OSVersion.Version.Major < 6)
					return false;

				return true;
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Returns <c>true</c> if we're running on Mono , otherwise <c>false</c>.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Obsolete("Use Platform.IsMono in SIL.Core instead")]
		public static bool IsMono => Platform.IsMono;

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Returns <c>true</c> if we're running on .Net , otherwise <c>false</c>.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Obsolete("Use Platform.IsDotNet in SIL.Core instead")]
		public static bool IsDotNet => Platform.IsDotNet;

		/// ------------------------------------------------------------------------------------
		/// <summary>When passed as a parameter to <see cref="FilterForFileName"/>, this
		/// determines how rigorous filtering is to be.</summary>
		/// ------------------------------------------------------------------------------------
		public enum FilenameFilterStrength
		{
			/// <summary> changes only chars that Windows prohibits in file names </summary>
			kFilterBackup,
			/// <summary> changes a few more chars, as needed for MSDE and/or SQL </summary>
			kFilterMSDE,
			/// <summary> changes even more chars for creating project names </summary>
			kFilterProjName,
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Produce a version of the given name that can be used as a file name. This is done
		/// by replacing invalid characters with underscores '_'. In some cases MSDE and/or
		/// SQL have more stringent requirements for database names.
		/// </summary>
		/// <param name="sName">Name to be filtered</param>
		/// <param name="strength">How rigorous filtering is to be</param>
		/// <returns>the filtered name</returns>
		/// ------------------------------------------------------------------------------------
		public static string FilterForFileName(string sName, FilenameFilterStrength strength)
		{
			return StringUtils.FilterForFileName(sName, GetInvalidProjectNameChars(strength), strength);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the list of characters that are not allowed for project names.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string GetInvalidProjectNameChars(FilenameFilterStrength strength)
		{
			// These are always invalid.
			string invalidChars = new string(Path.GetInvalidFileNameChars());
			// On Unix there are more characters valid in file names, but we
			// want the result to be identical on both platforms, so we have
			// to add those characters
			invalidChars = AddMissingChars(invalidChars, "?|<>\\*:\"");

			switch (strength)
			{
				case FilenameFilterStrength.kFilterProjName:
					// This is to avoid problems with restoring from backup.
					invalidChars = AddMissingChars(invalidChars, "()");
					// This is to avoid problems during Send/Receive
					invalidChars = AddMissingChars(invalidChars, "#");
					goto case FilenameFilterStrength.kFilterMSDE;
				case FilenameFilterStrength.kFilterMSDE:
					// JohnT: I don't think this case is used any more.
					// In some MSDE SQL commands, we have used single quotes or square brackets to
					//   delimit a multi-word, Unicode database name.
					//   REVIEW: Perhaps a creative SQL guru can reduce these restrictions.
					invalidChars = AddMissingChars(invalidChars, "[];");
					goto case FilenameFilterStrength.kFilterBackup;
				case FilenameFilterStrength.kFilterBackup:
				default:
					break;
			}

			return invalidChars;
		}

		static string AddMissingChars(string input, string more)
		{
			var result = input;
			foreach (var c in more)
			{
				if (result.IndexOf(c) == -1)
					result = result + c;
			}
			return result;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the name of the folder, removing the file name.
		/// </summary>
		/// <param name="fileSpec">The full file specification</param>
		/// <returns>string containing the name of the folder or string.Empty if it does not
		/// exist</returns>
		/// ------------------------------------------------------------------------------------
		public static string GetFolderName(string fileSpec)
		{
			try
			{
				if (Directory.Exists(fileSpec))
					return fileSpec; // fileSpec is a valid folder that does not contain a file name.

				string directoryName = Path.GetDirectoryName(fileSpec);
				if (Directory.Exists(directoryName))
					return directoryName;
			}
			catch
			{
				// Ignore any errors we get and just return string.Empty below
			}
			return string.Empty; // unable to determine valid folder name.
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the high-order word
		/// </summary>
		/// <param name="wParam"></param>
		/// <returns>High-order word</returns>
		/// ------------------------------------------------------------------------------------
		public static int HiWord(IntPtr wParam)
		{
			return (int)(wParam.ToInt32() & 0xFFFF0000) / 0x10000;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the low-order word
		/// </summary>
		/// <param name="wParam"></param>
		/// <returns>Low-order word</returns>
		/// ------------------------------------------------------------------------------------
		public static int LoWord(IntPtr wParam)
		{
			return wParam.ToInt32() & 0x0000FFFF;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Returns the physical memory in bytes.
		/// </summary>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public static ulong GetPhysicalMemoryBytes()
		{
			if (Platform.IsMono)
			{
				using (var pc = new PerformanceCounter("Mono Memory", "Total Physical Memory"))
				{
					return (ulong) pc.RawValue;
				}
			}

			using (var waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset))
			{
				var helper = new ManagementObjectHelper(waitHandle);
				ThreadPool.QueueUserWorkItem(helper.GetPhysicalMemoryBytes);
				waitHandle.WaitOne();
				return helper.Memory;
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Returns the disk drive statistics.
		/// </summary>
		/// <param name="size">Disk size of drive on which Fw program resides</param>
		/// <param name="free">Free space on drive on which Fw program resides</param>
		/// <returns>The total number of disk drives</returns>
		/// ------------------------------------------------------------------------------------
		public static int GetDiskDriveStats(out ulong size, out ulong free)
		{
			size = 0;
			free = 0;
			DriveInfo[] allDrives = DriveInfo.GetDrives();
			Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
			string drive = Path.GetPathRoot(assembly.Location);
			foreach (DriveInfo d in allDrives)
			{
				if (d.Name != drive)
					continue;
				free = (ulong)d.TotalFreeSpace;
				size = (ulong)d.TotalSize;
				break;
			}
			return allDrives.Length;
		}

		#region ManagementObjectHelper class
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// For some reason the class ManagementObjectSearcher doesn't work to well on a STA
		/// thread: it doesn't release all its resources in the Dispose() method. This causes
		/// some tests to fail. Therefore we use ManagementObjectSearcher on a separate (MTA)
		/// thread.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private class ManagementObjectHelper
		{
			private readonly EventWaitHandle m_waitHandle;

			/// --------------------------------------------------------------------------------
			/// <summary>
			/// Initializes a new instance of the ManagementObjectHelper class.
			/// </summary>
			/// <param name="waitHandle">The wait handle.</param>
			/// --------------------------------------------------------------------------------
			public ManagementObjectHelper(EventWaitHandle waitHandle)
			{
				m_waitHandle = waitHandle;
			}

			/// --------------------------------------------------------------------------------
			/// <summary>
			/// Gets the memory.
			/// </summary>
			/// <value>The memory.</value>
			/// --------------------------------------------------------------------------------
			public ulong Memory { get; private set; }

			/// --------------------------------------------------------------------------------
			/// <summary>
			/// Gets the physical memory in bytes.
			/// </summary>
			/// <param name="stateInfo">The state info.</param>
			/// --------------------------------------------------------------------------------
			public void GetPhysicalMemoryBytes(object stateInfo)
			{
				Memory = 0;
				try
				{
					using (var searcher = new ManagementObjectSearcher("select * from Win32_PhysicalMemory"))
					{
						using (var objColl = searcher.Get())
						{
							foreach (ManagementObject mem in objColl.Cast<ManagementObject>())
							{
								Memory += (ulong) mem.GetPropertyValue("Capacity");
								mem.Dispose();
							}
						}
					}
				}
				catch
				{
					// Don't die just because we can't read the system memory
				}
				m_waitHandle.Set();
			}

		}
		#endregion

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Since String.IndexOfAny does not have an override which takes a string array as its
		/// first input parameter, we provide this functionality here.
		/// </summary>
		/// <param name="s"></param>
		/// <param name="rgs"></param>
		/// <param name="iMatched"></param>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public static int IndexOfAnyString(this string s, string[] rgs, out int iMatched)
		{
			return IndexOfAnyString(s, rgs, out iMatched, StringComparison.CurrentCulture);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Since String.IndexOfAny does not have an override which takes a string array as its
		/// first input parameter, we provide this functionality here.
		/// </summary>
		/// <param name="s"></param>
		/// <param name="rgs"></param>
		/// <param name="iMatched"></param>
		/// <param name="sc">culture rule to use</param>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public static int IndexOfAnyString(this string s, string[] rgs, out int iMatched, StringComparison sc)
		{
			iMatched = -1;
			if (s == null || rgs == null || rgs.Length == 0)
				return -1;
			int ichRet = -1;
			for (int i = 0; i < rgs.Length; ++i)
			{
				int ich = s.IndexOf(rgs[i], sc);
				if (ich != -1)
				{
					if (ichRet == -1 || ich < ichRet)
					{
						ichRet = ich;
						iMatched = i;
					}
					else if (ich == ichRet && rgs[i].Length > rgs[iMatched].Length)
					{
						// save the longest match at this location
						iMatched = i;
					}
				}
			}
			return ichRet;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Since String.IndexOfAny does not have an override which takes a string array as its
		/// first input parameter, we provide this functionality here.
		/// </summary>
		/// <param name="s"></param>
		/// <param name="rgs"></param>
		/// <param name="ichStart"></param>
		/// <param name="iMatched"></param>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public static int IndexOfAnyString(this string s, string[] rgs, int ichStart, out int iMatched)
		{
			iMatched = -1;
			if (s == null || rgs == null || rgs.Length == 0)
				return -1;
			int ichRet = -1;
			for (int i = 0; i < rgs.Length; ++i)
			{
				int ich = s.IndexOf(rgs[i], ichStart);
				if (ich != -1)
				{
					if (ichRet == -1 || ich < ichRet)
					{
						ichRet = ich;
						iMatched = i;
					}
					else if (ich == ichRet && rgs[i].Length > rgs[iMatched].Length)
					{
						// save the longest match at this location
						iMatched = i;
					}
				}
			}
			return ichRet;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the current UI culture in the windows format (i.e. ID contains dashes). If
		/// the culture is "en-US" then "en" is just returned because we don't care about
		/// that distinction. (I hope that assumption doesn't come back to haunt me.)
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string CurrentUICulture
		{
			get
			{
				CultureInfo ci = Thread.CurrentThread.CurrentUICulture;
				return (ci.Name == "en-US" ? "en" : ci.Name);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Return the closest CultureInfo object for the given writing system.  If none exist,
		/// return null.
		/// </summary>
		/// <param name="sWs"></param>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public static CultureInfo GetCultureForWs(string sWs)
		{
			CultureInfo ci = null;
			int idx = sWs.Length;
			while (ci == null && idx > 0)
			{
				if (idx < sWs.Length)
					sWs = sWs.Remove(idx);
				try
				{
					ci = CultureInfo.GetCultureInfo(sWs);

					// if the CultureInfo doesn't exist, don't return junk.
					if (ci.EnglishName.Equals(string.Format("Unknown Language ({0})", sWs)))
					{
						ci = null;
						idx = sWs.LastIndexOf('-');
					}
				}
				catch
				{
					ci = null;
					idx = sWs.LastIndexOf('-');
				}
			}
			return ci;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Check whether the given writing system has a valid .Net CultureInfo associated with
		/// it.
		/// </summary>
		/// <param name="sWs"></param>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public static bool WsHasValidCulture(string sWs)
		{
			try
			{
				CultureInfo.GetCultureInfo(sWs);
				return true;
			}
			catch (ArgumentException)
			{
				return false;
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get a collection of strings containing all the installed UI language possibilities.
		/// </summary>
		/// <param name="baseConfigPath">from FwUtils.DirectoryFinder</param>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public static IEnumerable<string> GetAdditionalUILanguages(string baseConfigPath)
		{
			var additionalWs = new List<string>();
			if (Directory.Exists(baseConfigPath))
			{
				var rgsFiles = Directory.GetFiles(baseConfigPath, "strings-*.xml");
				foreach (var file in rgsFiles)
				{
					var locale = Path.GetFileNameWithoutExtension(file);
					//var i = locale.LastIndexOf('-'); // allows hyphen in locale name
					var i = locale.IndexOf('-'); // allows regional variant like 'zh-CN'
					if (i < 0)
						continue;
					locale = locale.Substring(i + 1);
					if (WsHasValidCulture(locale))
						additionalWs.Add(locale);
				}
			}
			return additionalWs;
		}

		////////////////////////////////////////////////////////////////////////////////////////
		//private static string CultureTypeNames(CultureTypes ct)
		//{
		//	StringBuilder bldr = new StringBuilder();
		//	if (0 != (ct & CultureTypes.NeutralCultures))
		//		bldr.Append("+Neutral");
		//	if (0 != (ct & CultureTypes.SpecificCultures))
		//		bldr.Append("+Specific");
		//	if (0 != (ct & CultureTypes.InstalledWin32Cultures))
		//		bldr.Append("+InstalledWin32");
		//	if (0 != (ct & CultureTypes.UserCustomCulture))
		//		bldr.Append("+UserCustom");
		//	if (0 != (ct & CultureTypes.ReplacementCultures))
		//		bldr.Append("+Replacement");
		//	if (0 != (ct & CultureTypes.FrameworkCultures))
		//		bldr.Append("+Framework");
		//	if (0 != (ct & CultureTypes.WindowsOnlyCultures))
		//		bldr.Append("+WindowsOnly");
		//	return bldr.ToString();
		//}
		////////////////////////////////////////////////////////////////////////////////////////

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Removes invalid characters as per the XML spec from the specified string,
		/// plus LF and CR.
		/// </summary>
		/// <param name="text">The text (this can be either raw XML text or text as already
		/// interpreted by an XML reader).</param>
		/// ------------------------------------------------------------------------------------
		public static string CleanupXmlString(string text)
		{
			for (int i = 0; i < text.Length; i++)
			{
				int hexValue = text[i];
				if ((hexValue >= 0x20 || hexValue == 0x9) && hexValue != 0xFFFE && hexValue != 0xFFFF
					&& hexValue != '&')
					continue;

				if (hexValue == '&')
				{
					Match match = kXmlCharEntity.Match(text, i, Math.Min(text.Length - i, 8));
					if (match.Success)
					{
						Debug.Assert(match.Groups.Count == 2);
						hexValue = int.Parse(match.Groups[1].Value, NumberStyles.HexNumber);
						// We want to filter out undesirable characters. LF (0xA) and CR (0xD)
						// are valid XML characters, but we don't want them.
						if ((hexValue < 0x20 && hexValue != 0x9) || hexValue == 0xFFFE || hexValue == 0xFFFF)
						{
							text = text.Replace(match.Groups[0].Value, string.Empty);
							i--;
						}
					}
				}
				else
				{
					text = text.Remove(i, 1);
					i--;
				}
			}
			return text;
		}

		/// <returns>
		/// True if input is made of only one or more upper or lowercase English letters,
		/// otherwise false.
		/// </returns>
		public static bool IsAlpha(string input)
		{
			if (input == null)
				return false;
			return Regex.IsMatch(input, @"^[A-Za-z]+\z");
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Extension method to determines whether the current user can write to the specified
		/// registry key.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static bool CanWriteKey(this RegistryKey key)
		{
			if (key == null)
				return false;
			try
			{
				(new RegistryPermission(RegistryPermissionAccess.Write, key.Name)).Demand();
				return true;
			}
			catch
			{
				// Ignore any errors and return false.
			}
			return false;
		}

		/// <summary>
		/// Allow special case unittests to pretend they are not unit tests.
		/// </summary>
		private static bool? runningTests;

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// <c>true</c> if running tests, otherwise <c>false</c>
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static bool RunningTests
		{
			get
			{
				if (!runningTests.HasValue)
				{
					var commandLine = Environment.CommandLine;
					if (commandLine.Contains("MonoDevelop.NUnit.External.ExternalTestRunner"))
						return true;

					// If the real application is ever installed in a path that includes nunit or
					// jetbrains, then this will return true and the app. won't run properly. But
					// what are the chances of that?...
					var appPath = Path.GetFullPath(Environment.GetCommandLineArgs()[0]).ToLowerInvariant();
					runningTests = (appPath.IndexOf("nunit") != -1 || appPath.IndexOf("jetbrains") != -1);
				}
				return (bool)runningTests;
			}
			set
			{
				runningTests = value;
			}
		}

		/// <summary>
		/// Run program with arguments, calling errorHandler() if there was an error such as
		/// the program not being found to run.
		/// </summary>
		/// <returns>launched Process or null</returns>
		[Obsolete("Use ProcessExtensions.RunProcess in SIL.Core instead")]
		public static Process RunProcess(string program, string arguments,
			Action<Exception> errorHandler)
		{
			var process = new Process();
			return process.RunProcess(program, arguments, errorHandler);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Determines whether a list contains the collection represented by another list.
		/// </summary>
		/// <typeparam name="T">The type of item contained in the lists</typeparam>
		/// <param name="list1">The potential super-sequence.</param>
		/// <param name="list2">The potential sub-sequence.</param>
		/// ------------------------------------------------------------------------------------
		public static bool ContainsCollection<T>(this IEnumerable<T> list1, IEnumerable<T> list2)
		{
			int intersectingItems = list1.Intersect(list2).Count();
			if (intersectingItems == list2.Count())
			{
				return true;
			}
			return false;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the standard sans serif font.  This is preferable to using a fixed name because
		/// it may depend on the system.
		/// </summary>
		/// <remarks>
		/// Arial is not normally found on Linux systems, and when it is found, it isn't changed
		/// to anything useful when localization is in effect.  Chinese text in Arial will
		/// display as boxes on Linux, but is properly handled on Windows.  With the generic
		/// sans-serif font, Chinese text displays okay on Linux, and a standard Linux
		/// sans-serif font is used for displaying English (or other European languages).  See
		/// FWNX-947 for more messy details.
		/// </remarks>
		/// ------------------------------------------------------------------------------------
		public static string StandardSansSerif => Platform.IsUnix ? "sans-serif" : "Arial";

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the standard serif font.  This is preferable to using a fixed name because
		/// it may depend on the system.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string StandardSerif => Platform.IsUnix ? "serif" : "Times New Roman";
	}
}
