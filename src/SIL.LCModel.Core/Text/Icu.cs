// Copyright (c) 2003-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using SIL.LCModel.Core.Properties;
using SIL.LCModel.Utils;
using SIL.Reporting;

namespace SIL.LCModel.Core.Text
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Wrapper for ICU methods
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public static class Icu
	{
		/// <summary>
		/// The ICU major version
		/// </summary>
		public const string Version = "54";

		private const string IcuucDllName = "icuuc" + Version + ".dll";
		private const string IcuinDllName = "icuin" + Version + ".dll";

		private const string VersionSuffix = "_" + Version;

		#region Public Properties
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the currently supported Unicode version for the current version of ICU.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string UnicodeVersion
		{
			get
			{
				var arg = new byte[4];
				u_getUnicodeVersion(arg);
				return string.Format("{0}.{1}", arg[0], arg[1]);
			}
		}
		#endregion

		#region Public wrappers around the ICU methods

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the directory where the ICU data is located from ICU_DATA environment variable.
		/// Will not return null.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string DefaultDataDirectory
		{
			get
			{
				// We use the ICU_DATA environment variable instead of directly reading a registry
				// value. This allows COMInterfaces.dll to be independent of WinForms.
				// ENHANCE: store data directory somewhere else other than registry (user.config
				// file?) and use that.
				var dir = Environment.GetEnvironmentVariable("ICU_DATA");
				if (string.IsNullOrEmpty(dir))
				{
					dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SIL",
						$"Icu{Version}");
				}
				return dir;
			}
		}

		///-------------------------------------------------------------------------------------
		/// <summary>
		/// Retrieve the ICU Directory from the ICU_DATA environment variable. As of ICU50, this
		/// is the same directory we want for the data directory, so just return that.
		/// </summary>
		///-------------------------------------------------------------------------------------
		public static string DefaultDirectory
		{
			get { return DefaultDataDirectory; }
		}

		#region Static methods to test codepoints' inclusion in various ranges
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Pair of codepoints (strings containing a hexidecimal value) representing a range.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private class CharacterRange
		{
			public string Start;
			public string End;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Determines whether the given codepoint is in any of the given ranges.
		/// </summary>
		/// <param name="codepoint">A string containing a hexidecimal value</param>
		/// <param name="rangesToCheck">One or more ranges of characters to check</param>
		/// ------------------------------------------------------------------------------------
		private static bool IsInRange(string codepoint, CharacterRange[] rangesToCheck)
		{
			foreach (CharacterRange range in rangesToCheck)
				if (MiscUtils.CompareHex(range.Start, codepoint) <= 0 && MiscUtils.CompareHex(range.End, codepoint) >= 0)
					return true;
			return false;
		}

		private static readonly CharacterRange[] s_validCodepointRanges =
			{
				new CharacterRange {Start = "0000", End = "10FFFD"}
			};

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// <c>true</c> when the codepoint is in the range of valid characters.
		/// </summary>
		/// <param name="codepoint">A string containing a hexidecimal value</param>
		/// ------------------------------------------------------------------------------------
		public static bool IsValidCodepoint(string codepoint)
		{
			return IsInRange(codepoint, s_validCodepointRanges);
		}

		// List of the ranges that are acceptable
		private static readonly CharacterRange[] s_puaRanges =
			{
				new CharacterRange { Start = "E000", End = "F8FF" },
				new CharacterRange { Start = "F0000", End = "FFFFD" },
				new CharacterRange { Start = "100000", End = "10FFFD" }
			};

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// <c>true</c> when the codepoint is in the Private Use range.
		/// </summary>
		/// <param name="codepoint">A string containing a hexidecimal value</param>
		/// ------------------------------------------------------------------------------------
		public static bool IsPrivateUse(string codepoint)
		{
			return IsInRange(codepoint, s_puaRanges);
		}

		// List of the ranges that are set aside for custom private-use characters
		// The actual ranges of PUA characters in the Unicode standard are E000-F8FF,
		// F0000-FFFFD, and 100000-10FFFD (see IsPrivateUse above).  We don't use the
		// full range because
		// 1) Microsoft has used codepoints from F000-F0FF
		// 2) NRSI wants to reserve F100-F8FF for its own purposes
		// 3) NRSI may prefer us to use plane 15 (F0000-FFFFD), not plane 16 (100000-10FFFD),
		//    but that's not clear, so we're adding plane 16 as of September 15, 2005.  If
		//    there's really a reason why plane 16 was not included here, remove it and
		//    document the reason!
		private static readonly CharacterRange[] s_customPuaRanges =
			{
				new CharacterRange { Start = "E000", End = "EFFF" },
				new CharacterRange { Start = "F0000", End = "FFFFD" },
				new CharacterRange { Start = "100000", End = "10FFFD"}
			};

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// <c>true</c> when the codepoint is in the custom Private Use range.
		/// </summary>
		/// <param name="codepoint">A string containing a hexidecimal value</param>
		/// ------------------------------------------------------------------------------------
		public static bool IsCustomUse(string codepoint)
		{
			return IsInRange(codepoint, s_customPuaRanges);
		}

		// List of the ranges that are set aside for surrogates
		private static readonly CharacterRange[] s_surrogateRanges =
			{
				new CharacterRange {Start = "D800", End = "DFFF"}
			};

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// <c>true</c> when the codepoint is in the surrogate ranges.
		/// </summary>
		/// <param name="codepoint">A string containing a hexidecimal value</param>
		/// ------------------------------------------------------------------------------------
		public static bool IsSurrogate(string codepoint)
		{
			return IsInRange(codepoint, s_surrogateRanges);
		}
		#endregion

		/// <summary>
		/// Makes sure the icu directory is set, and any other initialization
		/// which must be done before we use ICU is done.
		/// </summary>
		public static void InitIcuDataDir()
		{
			// Add the architecture specific path to the native icu dlls for windows into the PATH
			// this is needed for code that accesses the libraries directly instead of through icudotnet
			if (MiscUtils.IsWindows)
			{
				var arch = Environment.Is64BitProcess ? "x64" : "x86";
				var callingAssemblyFolder = Uri.UnescapeDataString(new UriBuilder(Assembly.GetExecutingAssembly().CodeBase).Path);
				// ReSharper disable once AssignNullToNotNullAttribute -- If FlexExe returns null we have bigger problems
				var icuPath = Path.Combine(Path.GetDirectoryName(callingAssemblyFolder), "lib", arch);
				// Append icu dll location to PATH, such as .../lib/x64, to help C# and C++ code find icu.
				Environment.SetEnvironmentVariable("PATH",
					Environment.GetEnvironmentVariable("PATH") + Path.PathSeparator + icuPath);
			}

			string szDir = DataDirectory;
			if (string.IsNullOrEmpty(szDir))
			{
				szDir = DefaultDataDirectory;
				DataDirectory = szDir;
			}
			// ICU docs say to do this after the directory is set, but before others are called.
			// And it can be called n times with little hit.
			UErrorCode errorCode;
			u_Init(out errorCode);

			string overrideDataPath = Path.Combine(szDir, "UnicodeDataOverrides.txt");
			if (!File.Exists(overrideDataPath))
			{
				// See if we can get the 'original' one in the data directory.
				overrideDataPath = Path.Combine(szDir, Path.Combine("data", "UnicodeDataOverrides.txt"));
			}
			bool result = SilIcuInit(overrideDataPath);
			if (!result)
			{
				// This provides a bit of extra info, especially if it fails on a no-gui build machine.
				Debug.Fail("SilIcuInit returned false. It was trying to load from " + overrideDataPath + ". The file " +
					(File.Exists(overrideDataPath) ? "exists." : "does not exist."));
				ErrorReport.ReportNonFatalMessageWithStackTrace(Resources.ksIcuInitFailed, overrideDataPath);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Cleans up the ICU files that could be locked
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void Cleanup()
		{
			u_Cleanup();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the current data directory.
		/// </summary>
		/// <returns>the pathname</returns>
		/// ------------------------------------------------------------------------------------
		public static string DataDirectory
		{
			get
			{
				IntPtr resPtr = u_GetDataDirectory();
				return Marshal.PtrToStringAnsi(resPtr);
			}
			set
			{
				// Remove a trailing backslash if it exists.
				if (value.Length > 0 && value[value.Length - 1] == '\\')
					value = value.Substring(0, value.Length - 1);
				u_SetDataDirectory(value);
			}
		}
		#endregion

		#region ICU methods that are not exposed directly

		/// <summary>SIL-specific initialization. Note that we do not currently define the kIcuVersion extension for this method.</summary>
		[DllImport(IcuucDllName, EntryPoint = "SilIcuInit",
			 CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		private static extern bool SilIcuInit(
			[MarshalAs(UnmanagedType.LPStr)]string pathname);

		/// <summary>get the name of an ICU code point</summary>
		[DllImport(IcuucDllName, EntryPoint = "u_init" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern void u_Init(out UErrorCode errorCode);

		/// <summary>Clean up the ICU files that could be locked</summary>
		[DllImport(IcuucDllName, EntryPoint = "u_cleanup" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern void u_Cleanup();

		/// <summary>Return the ICU data directory</summary>
		[DllImport(IcuucDllName, EntryPoint = "u_getDataDirectory" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr u_GetDataDirectory();

		/// <summary>Set the ICU data directory</summary>
		[DllImport(IcuucDllName, EntryPoint = "u_setDataDirectory" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern void u_SetDataDirectory(
			[MarshalAs(UnmanagedType.LPStr)]string directory);

		/// <summary>get the name of an ICU code point</summary>
		[DllImport(IcuucDllName, EntryPoint = "u_charName" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern int u_CharName(
			int code,
			UCharNameChoice nameChoice,
			IntPtr buffer,
			int bufferLength,
			out UErrorCode errorCode);

		#endregion

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Return the path to an ICU file that is locked. This checks the standard ICU
		/// files that we modify during writing system modifications. An optional locale
		/// may be included, in which case this file is also checked in addition to the others.
		/// If the return is null, it means none of the files are locked.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string CheckIcuLocked(string locale)
		{
			string sIcuDir = DefaultDataDirectory;
			var files = new List<string>
							{
								"root.res",
								"res_index.res",
								"unorm.icu",
								"uprops.icu",
								"ubidi.icu",
								"ucase.icu",
								"unames.icu",
								Path.Combine("coll", "res_index.res")
							};
			if (locale != null)
			{
				string resourceFile = Path.ChangeExtension(locale, ".res");
				files.Add(resourceFile);
				files.Add(Path.Combine("coll", resourceFile));
			}
			foreach (string file in files)
			{
				string sFile = Path.Combine(sIcuDir, file);
				if (File.Exists(sFile))
				{
					// This is a kludgy way to test for memory-mapped files.
					// Hopefully someone else can come up with a better way that doesn't
					// modify files in the process. Everything I tried, including reading
					// the mapped file in various modes, and renaming files failed to catch
					// the lock. Only by deleting or writing to the file would it actually
					// catch the lock.
					File.Copy(sFile, sFile + "xxxxx", true);
					try
					{
						File.Delete(sFile);
						File.Move(sFile + "xxxxx", sFile);
					}
					catch (Exception)
					{
						File.Delete(sFile + "xxxxx");
						return sFile;
					}
				}
			}
			return null;
		}

		#region character information

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// get the numeric value for the Unicode digit
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[DllImport(IcuucDllName, EntryPoint = "u_digit" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern int u_digit(
			int characterCode,
			byte radix);

		/// <summary></summary>
		public static int u_Digit(int characterCode, byte radix)
		{
			return u_digit(characterCode, radix);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// gets any of a variety of integer property values for the Unicode digit
		/// </summary>
		/// <param name="characterCode">The codepoint to look up</param>
		/// <param name="choice">The property value to look up</param>
		/// <remarks>DO NOT expose this method directly. Instead, make a specific implementation
		/// for each property needed. This not only makes it easier to use, but more importantly
		/// it prevents accidental use of the UCHAR_GENERAL_CATEGORY, which returns an
		/// enumeration that doesn't match the enumeration in FwKernel: LgGeneralCharCategory
		/// </remarks>
		/// ------------------------------------------------------------------------------------
		[DllImport(IcuucDllName, EntryPoint = "u_getIntPropertyValue" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern int u_getIntPropertyValue(
			int characterCode,
			UProperty choice);

		[DllImport(IcuucDllName, EntryPoint = "u_getUnicodeVersion" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern void u_getUnicodeVersion(byte[] versionInfo);

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Determines whether the specified character code is alphabetic, based on the
		/// Icu.UProperty.UCHAR_ALPHABETIC property.
		/// </summary>
		/// <param name="characterCode">The character code.</param>
		/// ------------------------------------------------------------------------------------
		public static bool IsAlphabetic(int characterCode)
		{
			return u_getIntPropertyValue(characterCode, UProperty.UCHAR_ALPHABETIC) != 0;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Determines whether the specified character code is ideographic, based on the
		/// Icu.UProperty.UCHAR_IDEOGRAPHIC property.
		/// </summary>
		/// <param name="characterCode">The character code.</param>
		/// ------------------------------------------------------------------------------------
		public static bool IsIdeographic(int characterCode)
		{
			return u_getIntPropertyValue(characterCode, UProperty.UCHAR_IDEOGRAPHIC) != 0;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Determines whether the specified character code is a diacritic, based on the
		/// Icu.UProperty.UCHAR_DIACRITIC property.
		/// </summary>
		/// <param name="characterCode">The character code.</param>
		/// ------------------------------------------------------------------------------------
		public static bool IsDiacritic(int characterCode)
		{
			return u_getIntPropertyValue(characterCode, UProperty.UCHAR_DIACRITIC) != 0;
		}

		/// <summary>
		/// Get the general character type.
		/// </summary>
		/// <param name="characterCode"></param>
		/// <returns></returns>
		[DllImport(IcuucDllName, EntryPoint = "u_charType" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern int u_charType(int characterCode);

		/// ------------------------------------------------------------------------------------
		/// <summary>
		///	Determines whether the specified code point is a symbol character
		/// </summary>
		/// <param name="characterCode">the code point to be tested</param>
		/// ------------------------------------------------------------------------------------
		public static bool IsSymbol(int characterCode)
		{
			var nAns = u_charType(characterCode);
			return nAns == (int)UCharCategory.U_MATH_SYMBOL ||
				nAns == (int)UCharCategory.U_CURRENCY_SYMBOL ||
				nAns == (int)UCharCategory.U_MODIFIER_SYMBOL ||
				nAns == (int)UCharCategory.U_OTHER_SYMBOL;
		}

		/// <summary>
		/// Determines whether the specified character is a separator.
		/// </summary>
		public static bool IsSeparator(int characterCode)
		{
			switch (GetCharType(characterCode))
			{
				case UCharCategory.U_SPACE_SEPARATOR:
				case UCharCategory.U_LINE_SEPARATOR:
				case UCharCategory.U_PARAGRAPH_SEPARATOR:
					return true;
				default:
					return false;
			}
		}

		/// <summary>
		/// Determines whether the specified character is a letter.
		/// </summary>
		public static bool IsLetter(int characterCode)
		{
			switch (GetCharType(characterCode))
			{
				case UCharCategory.U_UPPERCASE_LETTER:
				case UCharCategory.U_LOWERCASE_LETTER:
				case UCharCategory.U_TITLECASE_LETTER:
				case UCharCategory.U_MODIFIER_LETTER:
				case UCharCategory.U_OTHER_LETTER:
					return true;
				default:
					return false;
			}
		}

		/// <summary>
		/// Determines whether the specified character is a mark.
		/// </summary>
		public static bool IsMark(int characterCode)
		{
			switch (GetCharType(characterCode))
			{
				case UCharCategory.U_NON_SPACING_MARK:
				case UCharCategory.U_ENCLOSING_MARK:
				case UCharCategory.U_COMBINING_SPACING_MARK:
					return true;
				default:
					return false;
			}
		}

		///<summary>
		/// Get the general character category value for the given code point.
		///</summary>
		///<param name="ch">the code point to be checked</param>
		///<returns></returns>
		public static UCharCategory GetCharType(int ch)
		{
			return (UCharCategory)u_charType(ch);
		}

		[DllImport(IcuucDllName, EntryPoint = "u_charDirection" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern int u_charDirection(int characterCode);

		/// <summary>
		/// Gets the bidirectional category for te specified character.
		/// </summary>
		public static UCharDirection GetCharDirectionType(int ch)
		{
			return (UCharDirection) u_charDirection(ch);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Determines whether the specified character code is numeric, based on the
		/// Icu.UProperty.UCHAR_NUMERIC_TYPE property.
		/// </summary>
		/// <param name="characterCode">The character code.</param>
		/// ------------------------------------------------------------------------------------
		public static bool IsNumeric(int characterCode)
		{
			return u_getIntPropertyValue(characterCode, UProperty.UCHAR_NUMERIC_TYPE) != 0;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the decomposition type information of the given character.
		/// </summary>
		/// <param name="characterCode">The character code.</param>
		/// ------------------------------------------------------------------------------------
		public static UcdProperty GetDecompositionTypeInfo(int characterCode)
		{
			return UcdProperty.GetInstance((UDecompositionType)u_getIntPropertyValue(characterCode, UProperty.UCHAR_DECOMPOSITION_TYPE));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the numeric type information of the given character.
		/// </summary>
		/// <param name="characterCode">The character code.</param>
		/// ------------------------------------------------------------------------------------
		public static UcdProperty GetNumericTypeInfo(int characterCode)
		{
			return UcdProperty.GetInstance((UNumericType)u_getIntPropertyValue(characterCode, UProperty.UCHAR_NUMERIC_TYPE));
		}

		/// <summary>
		/// Gets the general category information of the specified character.
		/// </summary>
		public static UcdProperty GetGeneralCategoryInfo(int characterCode)
		{
			return UcdProperty.GetInstance(GetCharType(characterCode));
		}

		/// <summary>
		/// Gets the bidi class information of the specified character.
		/// </summary>
		public static UcdProperty GetBidiClassInfo(int characterCode)
		{
			return UcdProperty.GetInstance(GetCharDirectionType(characterCode));
		}

		/// <summary>
		/// Gets the combining class information of the specified character.
		/// </summary>
		public static UcdProperty GetCombiningClassInfo(int characterCode)
		{
			return UcdProperty.GetInstance(GetCombiningClass(characterCode));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		///Get the numeric value for a Unicode code point as defined in the Unicode Character Database.
		///A "double" return type is necessary because some numeric values are fractions, negative, or too large for int32_t.
		///For characters without any numeric values in the Unicode Character Database,
		///this function will return U_NO_NUMERIC_VALUE.
		///
		///Similar to java.lang.Character.getNumericValue(), but u_getNumericValue() also supports negative values,
		///large values, and fractions, while Java's getNumericValue() returns values 10..35 for ASCII letters.
		///</summary>
		///<remarks>
		///  See also:
		///      U_NO_NUMERIC_VALUE
		///  Stable:
		///      ICU 2.2
		/// http://oss.software.ibm.com/icu/apiref/uchar_8h.html#a477
		/// </remarks>
		///<param name="characterCode">Code point to get the numeric value for</param>
		///<returns>Numeric value of c, or U_NO_NUMERIC_VALUE if none is defined.</returns>
		/// ------------------------------------------------------------------------------------
		[DllImport(IcuucDllName, EntryPoint = "u_getNumericValue" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern double u_getNumericValue(
			int characterCode);
		/// <summary></summary>
		public static double u_GetNumericValue(int characterCode)
		{
			return u_getNumericValue(characterCode);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		///	Determines whether the specified code point is a punctuation character.
		/// </summary>
		/// <param name="characterCode">the code point to be tested</param>
		/// ------------------------------------------------------------------------------------
		[DllImport(IcuucDllName, EntryPoint = "u_ispunct" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		// Required because ICU returns a one-byte boolean. Without this C# assumes 4, and picks up 3 more random bytes,
		// which are usually zero, especially in debug builds...but one day we will be sorry.
		[return: MarshalAs(UnmanagedType.I1)]
		private static extern bool u_ispunct(
			int characterCode);
		/// <summary>Determines whether the specified code point is a punctuation character, as
		/// defined by the ICU u_ispunct function.</summary>
		public static bool IsPunct(int characterCode)
		{
			return u_ispunct(characterCode);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		///	Determines whether the code point has the Bidi_Mirrored property.
		///
		///	This property is set for characters that are commonly used in Right-To-Left contexts
		///	and need to be displayed with a "mirrored" glyph.
		///
		///	Same as java.lang.Character.isMirrored(). Same as UCHAR_BIDI_MIRRORED
		/// </summary>
		///	<remarks>
		///	See also:
		///	    UCHAR_BIDI_MIRRORED
		///
		///	Stable:
		///	    ICU 2.0
		///	</remarks>
		/// <param name="characterCode">the code point to be tested</param>
		/// <returns><c>true</c> if the character has the Bidi_Mirrored property</returns>
		/// ------------------------------------------------------------------------------------
		[DllImport(IcuucDllName, EntryPoint = "u_isMirrored" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		// Required because ICU returns a one-byte boolean. Without this C# assumes 4, and picks up 3 more random bytes,
		// which are usually zero, especially in debug builds...but one day we will be sorry.
		[return: MarshalAs(UnmanagedType.I1)]
		private static extern bool u_isMirrored(
			int characterCode);
		/// <summary></summary>
		public static bool u_IsMirrored(int characterCode)
		{
			return u_isMirrored(characterCode);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		///	Determines whether the specified code point is a control character. A control
		///	character is one of the following:
		/// <list>
		///	<item>ISO 8-bit control character (U+0000..U+001f and U+007f..U+009f)</item>
		///	<item>U_CONTROL_CHAR (Cc)</item>
		///	<item>U_FORMAT_CHAR (Cf)</item>
		///	<item>U_LINE_SEPARATOR (Zl)</item>
		///	<item>U_PARAGRAPH_SEPARATOR (Zp)</item>
		///	</list>
		/// </summary>
		/// <param name="characterCode">the code point to be tested</param>
		/// ------------------------------------------------------------------------------------
		[DllImport(IcuucDllName, EntryPoint = "u_iscntrl" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		// Required because ICU returns a one-byte boolean. Without this C# assumes 4, and picks up 3 more random bytes,
		// which are usually zero, especially in debug builds...but one day we will be sorry.
		[return: MarshalAs(UnmanagedType.I1)]
		private static extern bool u_iscntrl(
			int characterCode);
		/// <summary>Determines whether the specified code point is a control character, as
		/// defined by the ICU u_iscntrl function.</summary>
		public static bool IsControl(int characterCode)
		{
			return u_iscntrl(characterCode);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		///	Determines whether the specified character is a control character. A control
		///	character is one of the following:
		/// <list>
		///	<item>ISO 8-bit control character (U+0000..U+001f and U+007f..U+009f)</item>
		///	<item>U_CONTROL_CHAR (Cc)</item>
		///	<item>U_FORMAT_CHAR (Cf)</item>
		///	<item>U_LINE_SEPARATOR (Zl)</item>
		///	<item>U_PARAGRAPH_SEPARATOR (Zp)</item>
		///	</list>
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static bool IsControl(string chr)
		{
			return (!string.IsNullOrEmpty(chr) && chr.Length == 1 && IsControl(chr[0]));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		///	Determines whether the specified character is a space character.
		/// </summary>
		/// <remarks>
		///	See also:
		///	<list>
		///	<item>u_isJavaSpaceChar</item>
		///	<item>u_isWhitespace</item>
		/// <item>u_isUWhiteSpace</item>
		///	</list>
		///
		///	Stable:
		///	    ICU 2.0
		///	</remarks>
		/// <param name="characterCode">the code point to be tested</param>
		/// ------------------------------------------------------------------------------------
		[DllImport(IcuucDllName, EntryPoint = "u_isspace" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		// Required because ICU returns a one-byte boolean. Without this C# assumes 4, and picks up 3 more random bytes,
		// which are usually zero, especially in debug builds...but one day we will be sorry.
		[return: MarshalAs(UnmanagedType.I1)]
		private static extern bool u_isspace(
			int characterCode);
		/// <summary>Determines whether the specified character is a space character, as
		/// defined by the ICU u_isspace function.</summary>
		public static bool IsSpace(int characterCode)
		{
			return u_isspace(characterCode);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		///	Determines whether the specified character is a space character.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static bool IsSpace(string chr)
		{
			return !string.IsNullOrEmpty(chr) && chr.Length == 1 && IsSpace(chr[0]);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get the description for a given ICU code point.
		/// </summary>
		/// <param name="code">the code point to get description/name of</param>
		/// <param name="nameChoice">what type of information to retrieve</param>
		/// <param name="name">return string</param>
		/// <param name="error">return error</param>
		/// <returns>length of string</returns>
		/// ------------------------------------------------------------------------------------
		public static int u_CharName(int code, UCharNameChoice nameChoice, out string name, out UErrorCode error)
		{
			const int nSize = 255;
			IntPtr resPtr = Marshal.AllocCoTaskMem(nSize);
			try
			{
				int nResult = u_CharName(code, nameChoice, resPtr, nSize, out error);
				name = Marshal.PtrToStringAnsi(resPtr);
				return nResult;
			}
			finally
			{
				Marshal.FreeCoTaskMem(resPtr);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the ICU display name of the specified character.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string GetPrettyICUCharName(string chr)
		{
			if (!string.IsNullOrEmpty(chr) && chr.Length == 1)
			{
				string name;
				UErrorCode error;
				if (u_CharName(chr[0], UCharNameChoice.U_UNICODE_CHAR_NAME, out name, out error) > 0)
				{
					name = name.ToLower();
					return CultureInfo.CurrentUICulture.TextInfo.ToTitleCase(name);
				}
			}
			return null;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the raw ICU display name of the specified character code.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string GetCharName(int code)
		{
			string name;
			UErrorCode error;
			if (u_CharName(code, UCharNameChoice.U_UNICODE_CHAR_NAME, out name, out error) > 0 &&
				IsSuccess(error))
			{
				return name;
			}
			return null;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the exemplar characters for the given ICU locale.
		/// </summary>
		/// <param name="icuLocale">Code for the ICU locale.</param>
		/// <returns>
		/// A string containing all the exemplar characters (typically only lowercase
		/// word-forming characters), or an empty string if the given locale is unknown to ICU.
		/// </returns>
		/// ------------------------------------------------------------------------------------
		public static string GetExemplarCharacters(string icuLocale)
		{
			using (var rbLangDef = new IcuResourceBundle(null, icuLocale))
			{
				// If the locale of the resource bundle doesn't match the LocaleAbbr,
				// it loaded something else as a default (e.g. "en").
				// In that case we don't want to use the resource bundle, so ignore it.
				if (rbLangDef.LocaleId != icuLocale)
					return string.Empty;
				return rbLangDef.GetStringByKey("ExemplarCharacters");
			}
		}

		#endregion

		#region Locale related
		/// ------------------------------------------------------------------------------------
		/// <summary>Get the ICU LCID for a locale</summary>
		/// ------------------------------------------------------------------------------------
		[DllImport(IcuucDllName, EntryPoint = "uloc_getLCID" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern int uloc_getLCID(string localeID);
		/// <summary></summary>
		public static int GetLCID(string localeID)
		{
			return uloc_getLCID(localeID);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>Return the ISO 3 char value, if it exists</summary>
		/// ------------------------------------------------------------------------------------
		[DllImport(IcuucDllName, EntryPoint = "uloc_getISO3Country" + VersionSuffix,
			CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr uloc_getISO3Country(
			[MarshalAs(UnmanagedType.LPStr)]string locale);

		/// <summary></summary>
		public static string GetISO3Country(string locale)
		{
			return Marshal.PtrToStringAuto(uloc_getISO3Country(locale));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>Return the ISO 3 char value, if it exists</summary>
		/// ------------------------------------------------------------------------------------
		[DllImport(IcuucDllName, EntryPoint = "uloc_getISO3Language" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr uloc_getISO3Language(
			[MarshalAs(UnmanagedType.LPStr)]string locale);

		/// <summary></summary>
		public static string GetISO3Language(string locale)
		{
			return Marshal.PtrToStringAuto(uloc_getISO3Language(locale));
		}


		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the size of the all available locale list.
		/// </summary>
		/// <returns>the size of the locale list </returns>
		/// ------------------------------------------------------------------------------------
		[DllImport(IcuucDllName, EntryPoint = "uloc_countAvailable" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern int uloc_countAvailable();
		/// <summary></summary>
		public static int CountAvailableLocales()
		{
			return uloc_countAvailable();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the specified locale from a list of all available locales.
		/// The return value is a pointer to an item of a locale name array. Both this array
		/// and the pointers it contains are owned by ICU and should not be deleted or written
		/// through by the caller. The locale name is terminated by a null pointer.
		/// </summary>
		/// <param name="n">n  the specific locale name index of the available locale list</param>
		/// <returns>a specified locale name of all available locales</returns>
		/// ------------------------------------------------------------------------------------
		[DllImport(IcuucDllName, EntryPoint = "uloc_getAvailable" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr uloc_getAvailable(int n);

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the specified locale from a list of all available locales.
		/// The return value is a pointer to an item of a locale name array. Both this array
		/// and the pointers it contains are owned by ICU and should not be deleted or written
		/// through by the caller. The locale name is terminated by a null pointer.
		/// </summary>
		/// <param name="n">n  the specific locale name index of the available locale list</param>
		/// <returns>a specified locale name of all available locales</returns>
		/// ------------------------------------------------------------------------------------
		public static string GetAvailableLocale(int n)
		{
			IntPtr str = uloc_getAvailable(n);
			return Marshal.PtrToStringAnsi(str);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the language code for the specified locale.
		/// </summary>
		/// <param name="localeID">the locale to get the language code with </param>
		/// <param name="language">the language code for localeID </param>
		/// <param name="languageCapacity">the size of the language buffer to store the language
		/// code with </param>
		/// <param name="err">error information if retrieving the language code failed</param>
		/// <returns>the actual buffer size needed for the language code. If it's greater
		/// than languageCapacity, the returned language code will be truncated</returns>
		/// ------------------------------------------------------------------------------------
		[DllImport(IcuucDllName, EntryPoint = "uloc_getLanguage" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern int uloc_getLanguage(string localeID, IntPtr language,
			int languageCapacity, out UErrorCode err);

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the language code for the specified locale.
		/// </summary>
		/// <param name="localeID">the locale to get the language code with </param>
		/// <param name="language">the language code for localeID </param>
		/// <param name="err">error information if retrieving the language code failed</param>
		/// <returns>the actual buffer size needed for the language code. If it's greater
		/// than languageCapacity, the returned language code will be truncated</returns>
		/// ------------------------------------------------------------------------------------
		public static int GetLanguageCode(string localeID, out string language, out UErrorCode err)
		{
			const int nSize = 255;
			IntPtr resPtr = Marshal.AllocCoTaskMem(nSize);
			try
			{
				int nResult = uloc_getLanguage(localeID, resPtr, nSize, out err);
				language = Marshal.PtrToStringAnsi(resPtr);
				return nResult;
			}
			finally
			{
				Marshal.FreeCoTaskMem(resPtr);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the language code for the specified locale.
		/// </summary>
		/// <param name="locale">the locale to get the language code with </param>
		/// <returns>the language code for the locale.</returns>
		/// ------------------------------------------------------------------------------------
		public static string GetLanguageCode(string locale)
		{
			string languageCode;
			UErrorCode err;
			GetLanguageCode(locale, out languageCode, out err);
			if (IsFailure(err))
				throw new IcuException(string.Format("uloc_getLanguage() with argument '{0}' failed with error {1}", locale, err), err);
			return languageCode;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the script code for the specified locale.
		/// </summary>
		/// <param name="localeID">the locale to get the script code with </param>
		/// <param name="script">the script code for localeID </param>
		/// <param name="scriptCapacity">the size of the script buffer to store the script
		/// code with </param>
		/// <param name="err">error information if retrieving the script code failed</param>
		/// <returns>the actual buffer size needed for the script code. If it's greater
		/// than scriptCapacity, the returned script code will be truncated</returns>
		/// ------------------------------------------------------------------------------------
		[DllImport(IcuucDllName, EntryPoint = "uloc_getScript" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern int uloc_getScript(string localeID, IntPtr script,
			int scriptCapacity, out UErrorCode err);

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the script code for the specified locale.
		/// </summary>
		/// <param name="localeID">the locale to get the script code with </param>
		/// <param name="script">the script code for localeID </param>
		/// <param name="err">error information if retrieving the script code failed</param>
		/// <returns>the actual buffer size needed for the script code. If it's greater
		/// than scriptCapacity, the returned script code will be truncated</returns>
		/// ------------------------------------------------------------------------------------
		public static int GetScriptCode(string localeID, out string script, out UErrorCode err)
		{
			const int nSize = 255;
			IntPtr resPtr = Marshal.AllocCoTaskMem(nSize);
			try
			{
				int nResult = uloc_getScript(localeID, resPtr, nSize, out err);
				script = Marshal.PtrToStringAnsi(resPtr);
				return nResult;
			}
			finally
			{
				Marshal.FreeCoTaskMem(resPtr);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the country code for the specified locale.
		/// </summary>
		/// <param name="localeID">the locale to get the country code with </param>
		/// <param name="country">the country code for localeID </param>
		/// <param name="countryCapacity">the size of the country buffer to store the country
		/// code with </param>
		/// <param name="err">error information if retrieving the country code failed</param>
		/// <returns>the actual buffer size needed for the country code. If it's greater
		/// than countryCapacity, the returned country code will be truncated</returns>
		/// ------------------------------------------------------------------------------------
		[DllImport(IcuucDllName, EntryPoint = "uloc_getCountry" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern int uloc_getCountry(string localeID, IntPtr country,
			int countryCapacity, out UErrorCode err);

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the country code for the specified locale.
		/// </summary>
		/// <param name="localeID">the locale to get the country code with </param>
		/// <param name="country">the country code for localeID </param>
		/// <param name="err">error information if retrieving the country code failed</param>
		/// <returns>the actual buffer size needed for the country code. If it's greater
		/// than countryCapacity, the returned country code will be truncated</returns>
		/// ------------------------------------------------------------------------------------
		public static int GetCountryCode(string localeID, out string country, out UErrorCode err)
		{
			const int nSize = 255;
			IntPtr resPtr = Marshal.AllocCoTaskMem(nSize);
			try
			{
				int nResult = uloc_getCountry(localeID, resPtr, nSize, out err);
				country = Marshal.PtrToStringAnsi(resPtr);
				return nResult;
			}
			finally
			{
				Marshal.FreeCoTaskMem(resPtr);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the country code for the specified locale.
		/// </summary>
		/// <param name="locale">the locale to get the country code with </param>
		/// <returns>The country code for the locale.</returns>
		/// ------------------------------------------------------------------------------------
		public static string GetCountryCode(string locale)
		{
			string countryCode;
			UErrorCode err;
			GetCountryCode(locale, out countryCode, out err);
			if (IsFailure(err))
				throw new IcuException(string.Format("uloc_getCountry() with argument '{0}' failed with error {1}", locale, err), err);
			return countryCode;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the variant code for the specified locale.
		/// </summary>
		/// <param name="localeID">the locale to get the variant code with </param>
		/// <param name="variant">the variant code for localeID </param>
		/// <param name="variantCapacity">the size of the variant buffer to store the variant
		/// code with </param>
		/// <param name="err">error information if retrieving the variant code failed</param>
		/// <returns>the actual buffer size needed for the variant code. If it's greater
		/// than variantCapacity, the returned variant code will be truncated</returns>
		/// ------------------------------------------------------------------------------------
		[DllImport(IcuucDllName, EntryPoint = "uloc_getVariant" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern int uloc_getVariant(string localeID, IntPtr variant,
			int variantCapacity, out UErrorCode err);

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the variant code for the specified locale.
		/// </summary>
		/// <param name="localeID">the locale to get the variant code with </param>
		/// <param name="variant">the variant code for localeID </param>
		/// <param name="err">error information if retrieving the variant code failed</param>
		/// <returns>the actual buffer size needed for the variant code. If it's greater
		/// than variantCapacity, the returned variant code will be truncated</returns>
		/// ------------------------------------------------------------------------------------
		public static int GetVariantCode(string localeID, out string variant, out UErrorCode err)
		{
			const int nSize = 255;
			IntPtr resPtr = Marshal.AllocCoTaskMem(nSize);
			try
			{
				int nResult = uloc_getVariant(localeID, resPtr, nSize, out err);
				variant = Marshal.PtrToStringAnsi(resPtr);
				return nResult;
			}
			finally
			{
				Marshal.FreeCoTaskMem(resPtr);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the full name suitable for display for the specified locale.
		/// </summary>
		/// <param name="localeID">the locale to get the displayable name with</param>
		/// <param name="inLocaleID">Specifies the locale to be used to display the name. In
		/// other words, if the locale's language code is "en", passing Locale::getFrench()
		/// for inLocale would result in "Anglais", while passing Locale::getGerman() for
		/// inLocale would result in "Englisch".  </param>
		/// <param name="result">the displayable name for localeID</param>
		/// <param name="maxResultSize">the size of the name buffer to store the displayable
		/// full name with</param>
		/// <param name="err">error information if retrieving the displayable name failed</param>
		/// <returns>the actual buffer size needed for the displayable name. If it's greater
		/// than variantCapacity, the returned displayable name will be truncated.</returns>
		/// ------------------------------------------------------------------------------------
		[DllImport(IcuucDllName, EntryPoint = "uloc_getDisplayName" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern int uloc_getDisplayName(string localeID, string inLocaleID,
			IntPtr result, int maxResultSize, out UErrorCode err);

		//		/// ------------------------------------------------------------------------------------
		//		/// <summary>
		//		/// Gets the full name suitable for display for the specified locale.
		//		/// </summary>
		//		/// <param name="localeID">the locale to get the displayable name with</param>
		//		/// <param name="inLocaleID">Specifies the locale to be used to display the name. In
		//		/// other words, if the locale's language code is "en", passing Locale::getFrench()
		//		/// for inLocale would result in "Anglais", while passing Locale::getGerman() for
		//		/// inLocale would result in "Englisch".  </param>
		//		/// <param name="result">the displayable name for localeID</param>
		//		/// <param name="err">error information if retrieving the displayable name failed</param>
		//		/// <returns>the actual buffer size needed for the displayable name. If it's greater
		//		/// than variantCapacity, the returned displayable name will be truncated.</returns>
		//		/// ------------------------------------------------------------------------------------
		//		public static int GetDisplayName(string localeID, string inLocaleID, out string result,
		//			out UErrorCode err)
		//		{
		//			int nSize = 255;
		//			IntPtr resPtr = Marshal.AllocCoTaskMem(nSize);
		//			int nResult = Icu.uloc_getDisplayName(localeID, inLocaleID, resPtr, nSize, out err);
		//			result = Marshal.PtrToStringUni(resPtr);
		//			Marshal.FreeCoTaskMem(resPtr);
		//			return nResult;
		//		}

		enum DisplayType { Name, Language, Script, Country, Variant };


		[DllImport(IcuucDllName, EntryPoint = "uloc_getDisplayLanguage" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern int uloc_getDisplayLanguage(string localeID, string displayLocaleID,
			IntPtr result, int maxResultSize, out UErrorCode err);

		[DllImport(IcuucDllName, EntryPoint = "uloc_getDisplayScript" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern int uloc_getDisplayScript(string localeID, string displayLocaleID,
			IntPtr result, int maxResultSize, out UErrorCode err);

		[DllImport(IcuucDllName, EntryPoint = "uloc_getDisplayCountry" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern int uloc_getDisplayCountry(string localeID, string displayLocaleID,
			IntPtr result, int maxResultSize, out UErrorCode err);

		[DllImport(IcuucDllName, EntryPoint = "uloc_getDisplayVariant" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern int uloc_getDisplayVariant(string localeID, string displayLocaleID,
			IntPtr result, int maxResultSize, out UErrorCode err);

		/// <summary>
		/// Gets the displayable name.
		/// </summary>
		public static string GetDisplayName(string localeID)
		{
			string uilocale = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
			uilocale = uilocale.Replace('-', '_');

			string displayName;
			UErrorCode uerr;
			GetDisplayName(localeID, uilocale, out displayName, out uerr);
			if (IsSuccess(uerr))
				return displayName;
			return string.Empty;
		}

		/// <summary>
		/// Gets the displayable name following the TryGet pattern (returning a bool for success/failure).
		/// </summary>
		public static bool TryGetDisplayName(string localeID, out string displayName)
		{
			// We could almost just call GetDisplayName and return false if it returns string.Empty, but
			// that could fail if there are any locales whose localized display name is an empty string.
			// So we duplicate the code instead, checking the actual ICU error code for our success/failure.
			string uilocale = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
			uilocale = uilocale.Replace('-', '_');

			UErrorCode uerr;
			GetDisplayName(localeID, uilocale, out displayName, out uerr);
			if (IsSuccess(uerr))
				return true;
			displayName = string.Empty;
			return false;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>Get the displayable Name.</summary>
		/// ------------------------------------------------------------------------------------
		public static int GetDisplayName(string localeID, string displayLocaleID,
			out string result, out UErrorCode err)
		{
			return GetDisplayType(DisplayType.Name, localeID, displayLocaleID, out result, out err);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>Get the displayable Language name.</summary>
		/// ------------------------------------------------------------------------------------
		public static int GetDisplayLanguage(string localeID, string displayLocaleID,
			out string result, out UErrorCode err)
		{
			return GetDisplayType(DisplayType.Language, localeID, displayLocaleID, out result, out err);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>Get the displayable Script name.</summary>
		/// ------------------------------------------------------------------------------------
		public static int GetDisplayScript(string localeID, string displayLocaleID,
			out string result, out UErrorCode err)
		{
			return GetDisplayType(DisplayType.Script, localeID, displayLocaleID, out result, out err);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>Get the displayable Country name.</summary>
		/// ------------------------------------------------------------------------------------
		public static int GetDisplayCountry(string localeID, string displayLocaleID,
			out string result, out UErrorCode err)
		{
			return GetDisplayType(DisplayType.Country, localeID, displayLocaleID, out result, out err);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>Get the displayable Variant name.</summary>
		/// ------------------------------------------------------------------------------------
		public static int GetDisplayVariant(string localeID, string displayLocaleID,
			out string result, out UErrorCode err)
		{
			return GetDisplayType(DisplayType.Variant, localeID, displayLocaleID, out result, out err);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Single method to handle four similiar functioning ICU methods.
		/// </summary>
		/// <param name="method">Which ICU method to call</param>
		/// <param name="localeID">the locale to get the displayable variant code with.
		/// NULL may be used to specify the default.</param>
		/// <param name="displayLocaleID">Specifies the locale to be used to display the name.
		/// In other words, if the locale's language code is "en", passing Locale::getFrench() for
		/// inLocale would result in "Anglais", while passing Locale::getGerman() for inLocale would
		/// result in "Englisch". NULL may be used to specify the default.</param>
		/// <param name="result">the displayable result string</param>
		/// <param name="err">error code</param>
		/// <returns>the actual buffer size needed for the 'result'</returns>
		/// ------------------------------------------------------------------------------------
		private static int GetDisplayType(DisplayType method, string localeID,
			string displayLocaleID, out string result, out UErrorCode err)
		{
			const int nSize = 255;
			IntPtr resPtr = Marshal.AllocCoTaskMem(nSize * 2);
			try
			{
				int nResult = -1;
				err = UErrorCode.U_ZERO_ERROR;
				switch (method)
				{
					case DisplayType.Name:
						nResult = uloc_getDisplayName(localeID, displayLocaleID, resPtr, nSize, out err);
						break;
					case DisplayType.Language:
						nResult = uloc_getDisplayLanguage(localeID, displayLocaleID, resPtr, nSize, out err);
						break;
					case DisplayType.Script:
						nResult = uloc_getDisplayScript(localeID, displayLocaleID, resPtr, nSize, out err);
						break;
					case DisplayType.Country:
						nResult = uloc_getDisplayCountry(localeID, displayLocaleID, resPtr, nSize, out err);
						break;
					case DisplayType.Variant:
						nResult = uloc_getDisplayVariant(localeID, displayLocaleID, resPtr, nSize, out err);
						break;
				}

				result = Marshal.PtrToStringUni(resPtr);
				return nResult;

			}
			finally
			{
				Marshal.FreeCoTaskMem(resPtr);
			}
		}

		/// <summary>
		/// Gets the full name for the specified locale.
		/// </summary>
		[DllImport(IcuucDllName, EntryPoint = "uloc_getName" + VersionSuffix, CharSet = CharSet.Ansi,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern int uloc_getName(
			string localeID,
			byte[] name,
			int nameCapacity,
			out UErrorCode err);

		/// <summary></summary>
		public static int uloc_GetName(string localeID, ref byte[] name, int nameCapacity, out UErrorCode err)
		{
			return uloc_getName(localeID, name, nameCapacity, out err);
		}

		[DllImport(IcuucDllName, EntryPoint = "uloc_getName" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern int uloc_getName(string localeID, IntPtr name,
			int nameCapacity, out UErrorCode err);

		/// <summary>
		/// Gets the name of the locale (e.g. en_US).
		/// </summary>
		public static string GetName(string localeID)
		{
			if (string.IsNullOrEmpty(localeID))
				return string.Empty;

			const int nSize = 255;
			IntPtr resPtr = Marshal.AllocCoTaskMem(nSize);
			try
			{
				UErrorCode err;
				uloc_getName(localeID, resPtr, nSize, out err);
				if (IsFailure(err))
					throw new IcuException("uloc_getName() failed with code " + err, err);
				return Marshal.PtrToStringAnsi(resPtr);
			}
			finally
			{
				Marshal.FreeCoTaskMem(resPtr);
			}
		}

		/// <summary>
		/// Get all locale IDs, and display names in the given locale, that ICU knows about.
		/// Pass "null" to displayLocale parameter to use the user's default locale.
		/// </summary>
		/// <param name="displayLocale">Locale ID in which to display the locale names (e.g., if this is "fr", then the "en" locale will have the display name "anglais").</param>
		/// <returns>An IEnumerable of locale IDs and display names.</returns>
		public static IEnumerable<IcuIdAndName> GetLocaleIdsAndNames(string displayLocale = null)
		{
			// Unlike for converters and transliterators, the ICU API doesn't have an enumeration function for locales.
			// So this one we do the "old-fashioned" way, getting a count and getting locales by numeric index.
			int localeCount = CountAvailableLocales();
			for (int i = 0; i < localeCount; i++)
			{
				string id = GetAvailableLocale(i);
				string name;
				UErrorCode status;
				GetDisplayName(id, displayLocale, out name, out status);
				if (IsSuccess(status))
					yield return new IcuIdAndName(id, name);
			}
		}

		/// <summary>
		/// Get a dictionary, keyed by language ID, with values being a list of locale IDs and names (as returned by GetLocaleIdsAndNames).
		/// This allows "en-GB" and "en-US" to be grouped together under "English" in a menu, for example.
		/// </summary>
		/// <param name="displayLocale">Locale ID in which to display the locale names (e.g., if this is "fr", then the "en" locale will have the display name "anglais").</param>
		/// <returns>A dictionary whose keys are language IDs (2- or 3-letter ISO codes) and whose values are a list of the IcuIdAndName objects that GetLocaleIdsAndNames returns.</returns>
		public static IDictionary<string, IList<IcuIdAndName>> GetLocalesByLanguage(string displayLocale = null)
		{
			Dictionary<string, IList<IcuIdAndName>> result = new Dictionary<string, IList<IcuIdAndName>>();
			foreach (var idAndName in GetLocaleIdsAndNames(displayLocale))
			{
				string languageCode = GetLanguageCode(idAndName.Id);
				IList<IcuIdAndName> entries;
				if (!result.TryGetValue(languageCode, out entries))
				{
					entries = new List<IcuIdAndName>();
					result[languageCode] = entries;
				}
				entries.Add(idAndName);
			}
			return result;
		}

		#endregion // Locale related

		#region collation

		/// <summary>
		/// Open a UCollator for comparing strings.
		/// </summary>
		[DllImport(IcuinDllName, EntryPoint = "ucol_open" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr ucol_open(
			byte[] loc,
			out UErrorCode err);

		/// <summary></summary>
		public static IntPtr OpenCollator(string locale)
		{
			UErrorCode err;
			IntPtr collator = ucol_open(string.IsNullOrEmpty(locale) ? null : Encoding.UTF8.GetBytes(locale), out err);
			if (IsFailure(err))
				throw new IcuException("ucol_open() failed with code " + err, err);
			return collator;
		}

		/// <summary></summary>
		public static void CloseCollator(IntPtr collator)
		{
			ucol_close(collator);
		}

		/// <summary>
		/// Get a sort key for a string from a UCollator
		/// </summary>
		[DllImport(IcuinDllName, EntryPoint = "ucol_getSortKey" + VersionSuffix, CharSet = CharSet.Unicode,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern int ucol_getSortKey(IntPtr col1, string source, int sourceLength, byte[] result, int resultLength);

		/// <summary></summary>
		public static byte[] GetSortKey(IntPtr collator, string source)
		{
			const int keyLength = 1024;
			var key = new byte[keyLength + 1];
			var len = ucol_getSortKey(collator, source, source.Length, key, keyLength);
			if (len > keyLength)
			{
				key = new byte[len + 1];
				len = ucol_getSortKey(collator, source, source.Length, key, len);
			}
			// Ensure that the byte array is truncated to its actual data length.
			Debug.Assert(len <= key.Length);
			if (len < key.Length)
			{
				var result = new byte[len];
				Array.Copy(key, result, len);
				return result;
			}
			return key;
		}


		/// <summary>
		///
		/// </summary>
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		internal struct UParseError
		{
			/// <summary></summary>
			public int line;
			/// <summary></summary>
			public int offset;
			/// <summary>text before the error</summary>
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
			public string preContext;
			/// <summary>text following the error</summary>
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
			public string postContext;
		}

		private enum UColRuleOption
		{
			UCOL_TAILORING_ONLY = 0,
			UCOL_FULL_RULES
		}

		[DllImport(IcuinDllName, EntryPoint = "ucol_open" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern IntPtr ucol_open([MarshalAs(UnmanagedType.LPStr)] string locale, out UErrorCode errorCode);
		[DllImport(IcuinDllName, EntryPoint = "ucol_getRulesEx" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern int ucol_getRulesEx(IntPtr coll, UColRuleOption delta, IntPtr buffer, int bufferLen);
		/// <summary>Test the rules to see if they are valid.</summary>
		[DllImport(IcuinDllName, EntryPoint = "ucol_openRules" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern IntPtr ucol_openRules(string rules, int rulesLength, UColAttributeValue normalizationMode,
			UColAttributeValue strength, out UParseError parseError, out UErrorCode status);
		[DllImport(IcuinDllName, EntryPoint = "ucol_close" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern void ucol_close(IntPtr coll);

		/// <summary>
		/// Opens the specified collation rules as a collator.
		/// </summary>
		/// <param name="rules">The rules.</param>
		/// <returns></returns>
		public static IntPtr OpenRuleBasedCollator(string rules)
		{
			UErrorCode err;
			UParseError parseError;
			IntPtr collator = ucol_openRules(rules, rules.Length, UColAttributeValue.UCOL_DEFAULT,
				UColAttributeValue.UCOL_DEFAULT_STRENGTH, out parseError, out err);
			if (IsSuccess(err))
				return collator;
			throw new CollationRuleException(err, new CollationRuleErrorInfo(parseError));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test collation rules and return an object with error information if it fails.
		/// </summary>
		/// <param name="rules">String containing the collation rules to check</param>
		/// <returns>A CollationRuleErrorInfo object with error information; or <c>null</c> if
		/// no errors are found.</returns>
		/// ------------------------------------------------------------------------------------
		public static CollationRuleErrorInfo CheckRules(string rules)
		{
			if (rules == null)
				return null;
			UErrorCode err;
			UParseError parseError;
			IntPtr col = ucol_openRules(rules, rules.Length, UColAttributeValue.UCOL_DEFAULT,
				UColAttributeValue.UCOL_DEFAULT_STRENGTH, out parseError, out err);
			try
			{
				if (IsSuccess(err))
					return null;

				return new CollationRuleErrorInfo(parseError);
			}
			finally
			{
				ucol_close(col);
			}
		}

		/// <summary>
		/// Gets the collation rules for the specified locale.
		/// </summary>
		/// <param name="locale">The locale.</param>
		/// <returns></returns>
		public static string GetCollationRules(string locale)
		{
			string sortRules = null;
			UErrorCode err;
			IntPtr coll = ucol_open(locale, out err);
			if (coll != IntPtr.Zero)
			{
				const int len = 1000;
				IntPtr buffer = Marshal.AllocCoTaskMem(len * 2);
				try
				{
					int actualLen = ucol_getRulesEx(coll, UColRuleOption.UCOL_TAILORING_ONLY, buffer, len);
					if (actualLen > len)
					{
						Marshal.FreeCoTaskMem(buffer);
						buffer = Marshal.AllocCoTaskMem(actualLen * 2);
						ucol_getRulesEx(coll, UColRuleOption.UCOL_TAILORING_ONLY, buffer, actualLen);
					}
					sortRules = Marshal.PtrToStringUni(buffer);
					ucol_close(coll);
				}
				finally
				{
					Marshal.FreeCoTaskMem(buffer);
				}
			}
			return sortRules;
		}


		/// <summary>
		/// The sort key bound mode
		/// </summary>
		public enum UColBoundMode
		{
			/// <summary>
			/// lower bound
			/// </summary>
			UCOL_BOUND_LOWER = 0,
			/// <summary>
			/// upper bound that will match strings of exact size
			/// </summary>
			UCOL_BOUND_UPPER,
			/// <summary>
			/// upper bound that will match all strings that have the same initial substring as the given string
			/// </summary>
			UCOL_BOUND_UPPER_LONG
		}

		[DllImport(IcuinDllName, EntryPoint = "ucol_getBound" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern int ucol_getBound(byte[] source, int sourceLength, UColBoundMode boundType, int noOfLevels,
			byte[] result, int resultLength, out UErrorCode status);

		/// <summary>
		/// Produces a bound for a given sort key.
		/// </summary>
		/// <param name="sortKey">The sort key.</param>
		/// <param name="boundType">Type of the bound.</param>
		/// <param name="result">The result.</param>
		public static void GetSortKeyBound(byte[] sortKey, UColBoundMode boundType, ref byte[] result)
		{
			UErrorCode err;
			int size = ucol_getBound(sortKey, sortKey.Length, boundType, 1, result, result.Length, out err);
			if (IsFailure(err) && err != UErrorCode.U_BUFFER_OVERFLOW_ERROR)
				throw new IcuException("ucol_getBound() failed with code " + err, err);
			if (size > result.Length)
			{
				result = new byte[size + 1];
				ucol_getBound(sortKey, sortKey.Length, boundType, 1, result, result.Length, out err);
				if (IsFailure(err))
					throw new IcuException("ucol_getBound() failed with code " + err, err);
			}
		}

		[DllImport(IcuinDllName, EntryPoint = "ucol_strcoll" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern int ucol_strcoll(IntPtr coll, string source, int sourceLength, string target, int targetLength);

		/// <summary>
		/// Compares strings using the specified collator.
		/// </summary>
		/// <param name="coll">The collator.</param>
		/// <param name="source">The source.</param>
		/// <param name="target">The target.</param>
		/// <returns></returns>
		public static int Compare(IntPtr coll, string source, string target)
		{
			return ucol_strcoll(coll, source, source.Length, target, target.Length);
		}

		#endregion

		#region case related

		/// <summary>Return the lower case equivalent of the string.</summary>
		[DllImport(IcuucDllName, EntryPoint = "u_strToLower" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern int u_strToLower(IntPtr dest,
			 int destCapacity, string src, int srcLength, [MarshalAs(UnmanagedType.LPStr)] string locale, out UErrorCode errorCode);

		/// <summary>Return the title case equivalent of the string.</summary>
		[DllImport(IcuucDllName, EntryPoint = "u_strToTitle" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern int u_strToTitle(IntPtr dest,
			int destCapacity, string src, int srcLength, IntPtr titleIter, [MarshalAs(UnmanagedType.LPStr)] string locale, out UErrorCode errorCode);

		/// <summary>Return the upper case equivalent of the string.</summary>
		[DllImport(IcuucDllName, EntryPoint = "u_strToUpper" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern int u_strToUpper(IntPtr dest,
			int destCapacity, string src, int srcLength, [MarshalAs(UnmanagedType.LPStr)] string locale, out UErrorCode errorCode);

		[DllImport(IcuucDllName, EntryPoint = "u_toupper" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern int u_toupper(int ch);

		[DllImport(IcuucDllName, EntryPoint = "u_tolower" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern int u_tolower(int ch);

		[DllImport(IcuucDllName, EntryPoint = "u_totitle" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern int u_totitle(int ch);

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Convert the string to lower case, using the convention of the specified locale.
		/// This may be null for the universal locale, or "" for a 'root' locale (whatever that means).
		/// </summary>
		/// <param name="src"></param>
		/// <param name="locale"></param>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public static string ToLower(string src, string locale)
		{
			if (src == null)
				return "";

			int length = src.Length + 10;
			IntPtr resPtr = Marshal.AllocCoTaskMem(length * 2);
			try
			{
				UErrorCode err;
				int outLength = u_strToLower(resPtr, length, src, src.Length, locale, out err);
				if (IsFailure(err) && err != UErrorCode.U_BUFFER_OVERFLOW_ERROR)
					throw new IcuException("u_strToLower() failed with code " + err, err);
				if (outLength > length)
				{
					Marshal.FreeCoTaskMem(resPtr);
					length = outLength + 1;
					resPtr = Marshal.AllocCoTaskMem(length * 2);
					u_strToLower(resPtr, length, src, src.Length, locale, out err);
				}
				if (IsFailure(err))
					throw new IcuException("u_strToLower() failed with code " + err, err);

				string result = Marshal.PtrToStringUni(resPtr);
				// Strip any garbage left over at the end of the string.
				if (err == UErrorCode.U_STRING_NOT_TERMINATED_WARNING && result != null)
					return result.Substring(0, outLength);
				return result;

			}
			finally
			{
				Marshal.FreeCoTaskMem(resPtr);
			}
		}

		/// <summary>
		/// Converts the character to lower case.
		/// </summary>
		public static int ToLower(int ch)
		{
			return u_tolower(ch);
		}

		/// <summary>
		/// Convert the string to title case, using the convention of the specified locale.
		/// This may be null for the universal locale, or "" for a 'root' locale (whatever that means).
		/// </summary>
		/// <param name="src"></param>
		/// <param name="locale"></param>
		/// <returns></returns>
		public static string ToTitle(string src, string locale)
		{
			if (src == null)
				return "";

			// The dotted I in Turkish and other characters like it are not handled properly unless we are NFC, since
			// by default ICU only looks at the first character.
			bool isNfd = IsNormalized(src, UNormalizationMode.UNORM_NFD);
			if (isNfd)
				src = Normalize(src, UNormalizationMode.UNORM_NFC);

			int length = src.Length + 10;
			IntPtr resPtr = Marshal.AllocCoTaskMem(length * 2);
			try
			{
				UErrorCode err;
				int outLength = u_strToTitle(resPtr, length, src, src.Length, IntPtr.Zero, locale, out err);
				if (IsFailure(err) && err != UErrorCode.U_BUFFER_OVERFLOW_ERROR)
					throw new IcuException("u_strToTitle() failed with code " + err, err);
				if (outLength > length)
				{
					Marshal.FreeCoTaskMem(resPtr);
					length = outLength + 1;
					resPtr = Marshal.AllocCoTaskMem(length * 2);
					u_strToTitle(resPtr, length, src, src.Length, IntPtr.Zero, locale, out err);
				}
				if (IsFailure(err))
					throw new IcuException("u_strToTitle() failed with code " + err, err);

				string result = Marshal.PtrToStringUni(resPtr);
				if (result != null)
				{
					// Strip any garbage left over at the end of the string.
					if (err == UErrorCode.U_STRING_NOT_TERMINATED_WARNING)
						result = result.Substring(0, outLength);
					if (isNfd)
						result = Normalize(result, UNormalizationMode.UNORM_NFD);
				}
				return result;
			}
			finally
			{
				Marshal.FreeCoTaskMem(resPtr);
			}
		}

		/// <summary>
		/// Converts the character to title case.
		/// </summary>
		public static int ToTitle(int ch)
		{
			return u_totitle(ch);
		}

		/// <summary>
		/// Convert the string to upper case, using the convention of the specified locale.
		/// This may be null for the universal locale, or "" for a 'root' locale (whatever that means).
		/// </summary>
		/// <param name="src"></param>
		/// <param name="locale"></param>
		/// <returns></returns>
		public static string ToUpper(string src, string locale)
		{
			if (src == null)
				return "";

			int length = src.Length + 10;
			IntPtr resPtr = Marshal.AllocCoTaskMem(length * 2);
			try
			{
				UErrorCode err;
				int outLength = u_strToUpper(resPtr, length, src, src.Length, locale, out err);
				if (IsFailure(err) && err != UErrorCode.U_BUFFER_OVERFLOW_ERROR)
					throw new IcuException("u_strToUpper() failed with code " + err, err);
				if (outLength > length)
				{
					err = UErrorCode.U_ZERO_ERROR; // ignore possible U_BUFFER_OVERFLOW_ERROR
					Marshal.FreeCoTaskMem(resPtr);
					length = outLength + 1;
					resPtr = Marshal.AllocCoTaskMem(length * 2);
					u_strToUpper(resPtr, length, src, src.Length, locale, out err);
				}
				if (IsFailure(err))
					throw new IcuException("u_strToUpper() failed with code " + err, err);

				string result = Marshal.PtrToStringUni(resPtr);
				// Strip any garbage left over at the end of the string.
				if (err == UErrorCode.U_STRING_NOT_TERMINATED_WARNING && result != null)
					return result.Substring(0, outLength);
				return result;
			}
			finally
			{
				Marshal.FreeCoTaskMem(resPtr);
			}
		}

		/// <summary>
		/// Converts the character to upper case.
		/// </summary>
		public static int ToUpper(int ch)
		{
			return u_toupper(ch);
		}

		#endregion

		#region normalization
		/// <summary>
		/// Get a normalizer for the specified name and mode.
		/// </summary>
		[DllImport(IcuucDllName, EntryPoint = "unorm2_getInstance" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		private static extern IntPtr unorm2_getInstance(string packageName, string name, UNormalization2Mode mode, out UErrorCode errorCode);

		/// <summary>
		/// Normalize a string using the specified normalizer.
		/// </summary>
		[DllImport(IcuucDllName, EntryPoint = "unorm2_normalize" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern int unorm2_normalize(IntPtr normalizer, string source, int sourceLength,
			IntPtr result, int resultLength, out UErrorCode errorCode);

		/// <summary>
		/// Decompose a single UTF-32 code point using the specified normalizer.
		/// According to the ICU docs, this is significantly faster than calling Char.ConvertFromUtf32 and decomposing the resulting string.
		/// </summary>
		[DllImport(IcuucDllName, EntryPoint = "unorm2_getDecomposition" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern int unorm2_getDecomposition(IntPtr normalizer, int source,
			IntPtr result, int resultLength, out UErrorCode errorCode);

		/// <summary>
		/// Check whether a string is normalized according to the given mode and options.
		/// </summary>
		[DllImport(IcuucDllName, EntryPoint = "unorm2_isNormalized" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		// Note that ICU's UBool type is typedef to an 8-bit integer.
		private static extern byte unorm2_isNormalized(IntPtr normalizer, string source, int sourceLength,
			out UErrorCode errorCode);

		[DllImport(IcuucDllName, EntryPoint = "unorm2_getCombiningClass" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern byte unorm2_getCombiningClass(IntPtr normalizer, int ch);

		// UBool unorm2_hasBoundaryAfter (const UNormalizer2 *norm2, UChar32 c)
		[DllImport(IcuucDllName, EntryPoint = "unorm2_hasBoundaryAfter" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern byte unorm2_hasBoundaryAfter(IntPtr normalizer, Int32 codepoint);

		// UBool unorm2_hasBoundaryBefore (const UNormalizer2 *norm2, UChar32 c)
		[DllImport(IcuucDllName, EntryPoint = "unorm2_hasBoundaryBefore" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern byte unorm2_hasBoundaryBefore(IntPtr normalizer, Int32 codepoint);

		/// <summary>
		/// Tests if the character always has a normalization boundary before it, regardless of context.
		/// A "normalization boundary" is a point where the string segments before and after it cannot interact,
		/// and therefore dividing the string at that boundary and normalizing each segment independently would
		/// produce the same result as normalizing the entire string as a whole.
		/// </summary>
		/// <param name="norm">The ICU normalizer to use for this check</param>
		/// <param name="codepoint">The Unicode codepoint to check, as a 32-bit int (use Char.ConvertToUtf32(str, idx) to get this)</param>
		/// <returns>True if this character will always have a normalization boundary before it.</returns>
		public static bool HasNormalizationBoundaryBefore(IntPtr norm, int codepoint)
		{
			byte result = unorm2_hasBoundaryBefore(norm, codepoint);
			return result != 0;
		}

		/// <summary>
		/// Tests if the character always has a normalization boundary after it, regardless of context.
		/// A "normalization boundary" is a point where the string segments before and after it cannot interact,
		/// and therefore dividing the string at that boundary and normalizing each segment independently would
		/// produce the same result as normalizing the entire string as a whole.
		/// NOTE: The ICU documentation states that this function may be significantly slower than its Before counterpart.
		/// If possible, prefer HasNormalizationBoundaryBefore() over using this function.
		/// </summary>
		/// <param name="norm">The ICU normalizer to use for this check</param>
		/// <param name="codepoint">The Unicode codepoint to check, as a 32-bit int (use Char.ConvertToUtf32(str, idx) to get this)</param>
		/// <returns>True if this character will always have a normalization boundary after it.</returns>
		public static bool HasNormalizationBoundaryAfter(IntPtr norm, int codepoint)
		{
			byte result = unorm2_hasBoundaryAfter(norm, codepoint);
			return result != 0;
		}


		/// <summary>
		/// Normalization mode constants.
		/// </summary>
		public enum UNormalizationMode
		{
			/// <summary>No decomposition/composition.</summary>
			UNORM_NONE = 1,
			/// <summary>Canonical decomposition.</summary>
			UNORM_NFD = 2,
			/// <summary>Compatibility decomposition.</summary>
			UNORM_NFKD = 3,
			/// <summary>Canonical decomposition followed by canonical composition.</summary>
			UNORM_NFC = 4,
			/// <summary>Default normalization.</summary>
			UNORM_DEFAULT = UNORM_NFC,
			///<summary>Compatibility decomposition followed by canonical composition.</summary>
			UNORM_NFKC = 5,
			/// <summary>"Fast C or D" form.</summary>
			UNORM_FCD = 6
		}

		/// <summary>
		/// Normalization 2 modes
		/// </summary>
		private enum UNormalization2Mode
		{
			/// <summary>
			/// Decomposition followed by composition.
			/// </summary>
			UNORM2_COMPOSE = 0,
			/// <summary>
			/// Map, and reorder canonically.
			/// </summary>
			UNORM2_DECOMPOSE = 1,
			/// <summary>
			/// "Fast C or D" form.
			/// </summary>
			UNORM2_FCD = 2,
			/// <summary>
			/// Compose only contiguously.
			/// </summary>
			UNORM2_COMPOSE_CONTIGUOUS = 3
		}

		/// <summary>
		/// Get an opaque pointer to the Icu normalizer object for a given mode (NFC, NFD, etc.)
		/// Used in several parts of the TsString normalization code.
		/// </summary>
		public static IntPtr GetIcuNormalizer(UNormalizationMode mode)
		{
			var err = UErrorCode.U_ZERO_ERROR;
			IntPtr normalizer = IntPtr.Zero;
			switch (mode)
			{
				case UNormalizationMode.UNORM_NFC:
					normalizer = unorm2_getInstance(null, "nfc_fw", UNormalization2Mode.UNORM2_COMPOSE, out err);
					break;

				case UNormalizationMode.UNORM_NFD:
					normalizer = unorm2_getInstance(null, "nfc_fw", UNormalization2Mode.UNORM2_DECOMPOSE, out err);
					break;

				case UNormalizationMode.UNORM_NFKC:
					normalizer = unorm2_getInstance(null, "nfkc_fw", UNormalization2Mode.UNORM2_COMPOSE, out err);
					break;

				case UNormalizationMode.UNORM_NFKD:
					normalizer = unorm2_getInstance(null, "nfkc_fw", UNormalization2Mode.UNORM2_DECOMPOSE, out err);
					break;

				case UNormalizationMode.UNORM_FCD:
					normalizer = unorm2_getInstance(null, "nfc_fw", UNormalization2Mode.UNORM2_FCD, out err);
					break;
			}

			if (IsFailure(err))
				throw new IcuException("unorm2_getInstance() failed with code " + err, err);

			return normalizer;
		}

		/// <summary>
		/// Normalize the string with the given ICU normalizer.
		/// </summary>
		public static string Normalize(string src, IntPtr norm)
		{
			if (string.IsNullOrEmpty(src))
				return "";

			int length = src.Length + 10;
			IntPtr resPtr = Marshal.AllocCoTaskMem(length * 2);
			try
			{
				UErrorCode err;
				int outLength = unorm2_normalize(norm, src, src.Length, resPtr, length, out err);
				if (IsFailure(err) && err != UErrorCode.U_BUFFER_OVERFLOW_ERROR)
					throw new IcuException("unorm_normalize() failed with code " + err, err);
				if (outLength >= length)
				{
					err = UErrorCode.U_ZERO_ERROR; // ignore possible U_BUFFER_OVERFLOW_ERROR
					Marshal.FreeCoTaskMem(resPtr);
					length = outLength + 1;		// allow room for the terminating NUL (FWR-505)
					resPtr = Marshal.AllocCoTaskMem(length * 2);
					unorm2_normalize(norm, src, src.Length, resPtr, length, out err);
				}
				if (IsFailure(err))
					throw new IcuException("unorm2_normalize() failed with code " + err, err);

				string result = Marshal.PtrToStringUni(resPtr);
				// Strip any garbage left over at the end of the string.
				if (err == UErrorCode.U_STRING_NOT_TERMINATED_WARNING && result != null)
					return result.Substring(0, outLength);

				return result;

			}
			finally
			{
				Marshal.FreeCoTaskMem(resPtr);
			}
		}

		/// <summary>
		/// Normalize the string according to the given mode.
		/// </summary>
		public static string Normalize(string src, UNormalizationMode mode)
		{
			if (mode == UNormalizationMode.UNORM_NONE)
				return src;

			IntPtr norm = GetIcuNormalizer(mode);
			return Normalize(src, norm);
		}

		/// <summary>
		/// Check whether the string is normalized according to the given mode.
		/// </summary>
		/// <param name="src"></param>
		/// <param name="mode"></param>
		/// <returns></returns>
		public static bool IsNormalized(string src, UNormalizationMode mode)
		{
			if (string.IsNullOrEmpty(src) || mode == UNormalizationMode.UNORM_NONE)
				return true;

			IntPtr norm = GetIcuNormalizer(mode);
			UErrorCode err;
			byte fIsNorm = unorm2_isNormalized(norm, src, src.Length, out err);
			if (IsFailure(err))
				throw new IcuException("unorm2_isNormalized() failed with code " + err, err);
			return fIsNorm != 0;
		}

		/// <summary>
		/// Get the decomposition of a UTF-32 character according to the given normalizer (which should be either an NFD or an NFKD normalizer).
		/// This is significantly faster than calling Char.ConvertFromUtf32 and normalizing the resulting string.
		/// </summary>
		/// <returns>A string, not an int codepoint -- because if the normalizer is NFKD, one codepoint could become multiple characters (e.g., the "ffi" ligature will decompose into f, f, i, and so on).</returns>
		public static string GetDecompositionFromUtf32(IntPtr norm, int sourceCodepoint)
		{
			int length = 16; // Should be enough for almost any decomposition
			IntPtr resPtr = Marshal.AllocCoTaskMem(length * 2);
			try
			{
				UErrorCode err;
				int outLength = unorm2_getDecomposition(norm, sourceCodepoint, resPtr, length, out err);
				if (IsFailure(err) && err != UErrorCode.U_BUFFER_OVERFLOW_ERROR)
					throw new IcuException("unorm2_getDecomposition() failed with code " + err, err);
				if (outLength < 0)
				{
					// ICU API says that a negative value means "there is no decomposition for this character"
					return Char.ConvertFromUtf32(sourceCodepoint);
				}
				else if (outLength >= length)
				{
					err = UErrorCode.U_ZERO_ERROR; // ignore possible U_BUFFER_OVERFLOW_ERROR
					Marshal.FreeCoTaskMem(resPtr);
					length = outLength + 1; // allow room for the terminating NUL (FWR-505)
					resPtr = Marshal.AllocCoTaskMem(length * 2);
					unorm2_getDecomposition(norm, sourceCodepoint, resPtr, length, out err);
				}
				if (IsFailure(err))
					throw new IcuException("unorm2_getDecomposition() failed with code " + err, err);

				return Marshal.PtrToStringUni(resPtr, outLength);
			}
			finally
			{
				Marshal.FreeCoTaskMem(resPtr);
			}
		}

		/// <summary>
		/// Gets the combining class for the specified character.
		/// </summary>
		public static int GetCombiningClass(int ch)
		{
			IntPtr norm = GetIcuNormalizer(UNormalizationMode.UNORM_NFC);
			return unorm2_getCombiningClass(norm, ch);
		}

		/// <summary>
		/// Gets the decomposition of the specified character.
		/// </summary>
		public static string GetDecomposition(int ch)
		{
			int type = u_getIntPropertyValue(ch, UProperty.UCHAR_DECOMPOSITION_TYPE);
			UNormalizationMode mode = UNormalizationMode.UNORM_NFD;
			if (type != (int) UDecompositionType.U_DT_NONE)
				mode = UNormalizationMode.UNORM_NFKD;
			return Normalize(char.ConvertFromUtf32(ch), mode);
		}

		#endregion

		#region Break iterator

		/// <summary>
		/// Value indicating all text boundaries have been returned.
		/// </summary>
		private const int UBRK_DONE = -1;

		/// <summary>
		/// Tag value for "words" that do not fit into any of other categories.
		/// Includes spaces and most punctuation.
		/// </summary>
		private const int UBRK_WORD_NONE = 0;
		/// <summary>
		/// Upper bound for tags for uncategorized words.
		/// </summary>
		private const int UBRK_WORD_NONE_LIMIT = 100;

		/// <summary>
		/// The possible types of text boundaries.
		/// </summary>
		public enum UBreakIteratorType
		{
			/// <summary>
			/// Character breaks.
			/// </summary>
			UBRK_CHARACTER = 0,
			/// <summary>
			/// Word breaks.
			/// </summary>
			UBRK_WORD,
			/// <summary>
			/// Line breaks.
			/// </summary>
			UBRK_LINE,
			/// <summary>
			/// Sentence breaks.
			/// </summary>
			UBRK_SENTENCE,
			/// <summary>
			/// Title Case breaks.
			/// </summary>
			UBRK_TITLE
		}

		/// <summary>
		/// Open a new UBreakIterator for locating text boundaries for a specified locale.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="locale">The locale.</param>
		/// <param name="text">The text.</param>
		/// <param name="textLength">Length of the text.</param>
		/// <param name="errorCode">The error code.</param>
		/// <returns></returns>
		[DllImport(IcuucDllName, EntryPoint = "ubrk_open" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern IntPtr ubrk_open(UBreakIteratorType type, string locale,
			IntPtr text, int textLength, out UErrorCode errorCode);

		/// <summary>
		/// Close a UBreakIterator.
		/// </summary>
		/// <param name="bi">The break iterator.</param>
		[DllImport(IcuucDllName, EntryPoint = "ubrk_close" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern void ubrk_close(IntPtr bi);

		/// <summary>
		/// Determine the index of the first character in the text being scanned.
		/// </summary>
		/// <param name="bi">The break iterator.</param>
		/// <returns></returns>
		[DllImport(IcuucDllName, EntryPoint = "ubrk_first" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern int ubrk_first(IntPtr bi);

		/// <summary>
		/// Determine the text boundary following the current text boundary.
		/// </summary>
		/// <param name="bi">The break iterator.</param>
		/// <returns></returns>
		[DllImport(IcuucDllName, EntryPoint = "ubrk_next" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern int ubrk_next(IntPtr bi);

		/// <summary>
		/// Return the status from the break rule that determined the most recently returned break position.
		/// </summary>
		/// <param name="bi">The break iterator.</param>
		/// <returns></returns>
		[DllImport(IcuucDllName, EntryPoint = "ubrk_getRuleStatus" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern int ubrk_getRuleStatus(IntPtr bi);

		/// <summary>
		/// Splits the specified text along the specified type of boundaries.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="locale">The locale.</param>
		/// <param name="text">The text.</param>
		/// <returns>The tokens.</returns>
		public static IEnumerable<string> Split(UBreakIteratorType type, string locale, string text)
		{
			if (string.IsNullOrEmpty(text))
				return Enumerable.Empty<string>();

			// ensure that the unmanaged string exists during the entire lifetime of the break iterator
			IntPtr textPtr = Marshal.StringToHGlobalUni(text);
			try
			{
				UErrorCode err;
				IntPtr bi = ubrk_open(type, locale, textPtr, -1, out err);
				if (IsFailure(err))
					throw new IcuException("ubrk_open() failed with code " + err, err);
				var tokens = new List<string>();
				int cur = ubrk_first(bi);
				while (cur != UBRK_DONE)
				{
					int next = ubrk_next(bi);
					if (next != UBRK_DONE)
						tokens.Add(text.Substring(cur, next - cur));
					cur = next;
				}
				ubrk_close(bi);
				return tokens;
			}
			finally
			{
				Marshal.FreeHGlobal(textPtr);
			}
		}

		#endregion Break iterator

		#region Resource bundles
		// UResourceBundle * ures_open (const char *package, const char *locale, UErrorCode *status)
		[DllImport(IcuucDllName, EntryPoint = "ures_open" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		private static extern IntPtr ures_open(string package, string locale, out UErrorCode status);

		/// <summary>
		/// Initialize a resource bundle. Caller must call CloseResourceBundle later to avoid memory leaks.
		/// </summary>
		/// <param name="package">The name of the ICU resource bundle to open (can be null to open the "default" bundle).</param>
		/// <param name="locale">The ICU locale whose resources should be loaded (null for the default locale).</param>
		/// <returns></returns>
		public static IntPtr OpenResourceBundle(string package, string locale)
		{
			UErrorCode status;
			IntPtr resourceBundle = ures_open(package, locale, out status);
			if (IsFailure(status))
				return IntPtr.Zero;
			return resourceBundle;
		}

		// void ures_close (UResourceBundle *resourceBundle)
		[DllImport(IcuucDllName, EntryPoint = "ures_close" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		private static extern void ures_close(IntPtr resourceBundle);

		/// <summary>
		/// Close a previously-opened resource bundle.
		/// </summary>
		/// <param name="resourceBundle">The previously-opened resource bundle.</param>
		public static void CloseResourceBundle(IntPtr resourceBundle)
		{
			if (resourceBundle != IntPtr.Zero)
				ures_close(resourceBundle);
		}

		// const char * ures_getKey (const UResourceBundle *resourceBundle)
		[DllImport(IcuucDllName, EntryPoint = "ures_getKey" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		// Do NOT free the memory returned by this ICU function.
		private static extern IntPtr ures_getKey(IntPtr resourceBundle);

		/// <summary> Get the key of the bundle. (Icu getKey.) </summary>
		/// <returns>A System.String </returns>
		public static string GetResourceBundleKey(IntPtr resourceBundle)
		{
			IntPtr keyPtr = ures_getKey(resourceBundle);
			if (keyPtr == IntPtr.Zero)
				return String.Empty;
			return Marshal.PtrToStringAnsi(keyPtr);
		}

		// const UChar * ures_getString (const UResourceBundle *resourceBundle, int32_t *len, UErrorCode *status)
		[DllImport(IcuucDllName, EntryPoint = "ures_getString" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		// Do NOT free the memory returned by this ICU function.
		private static extern IntPtr ures_getString(IntPtr resourceBundle, out int len, out UErrorCode status);

		/// <summary>
		/// If a resource bundle is just a single string, get that string. (Icu getString.)
		/// If you are using this function, consider refactoring to use GetResourceBundleStringByKey on the parent resource bundle instead.
		/// </summary>
		/// <param name="resourceBundle">The resource bundle</param>
		/// <returns>A System.String containing the string that this bundle contained, or String.Empty if this bundle was not just a single string.</returns>
		public static string GetResourceBundleString(IntPtr resourceBundle)
		{
			int len;
			UErrorCode status;
			IntPtr resultPtr = ures_getString(resourceBundle, out len, out status);
			if (IsFailure(status) || resultPtr == IntPtr.Zero)
				return String.Empty;
			return Marshal.PtrToStringUni(resultPtr, len);
		}

		// const char * ures_getLocale (const UResourceBundle *resourceBundle, UErrorCode *status)
		[DllImport(IcuucDllName, EntryPoint = "ures_getLocale" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		// Do NOT free the memory returned by this ICU function.
		private static extern IntPtr ures_getLocale(IntPtr resourceBundle, out UErrorCode status);

		/// <summary>
		/// Get the locale ID of the bundle. (Icu getLocale.)
		/// Note that the Key and String of the bundle are often more useful.
		/// </summary>
		/// <param name="resourceBundle">The resource bundle</param>
		/// <returns>A System.String containing the locale ID</returns>
		public static string GetResourceBundleLocaleId(IntPtr resourceBundle)
		{
			UErrorCode status;
			IntPtr resultPtr = ures_getLocale(resourceBundle, out status);
			if (IsFailure(status) || resultPtr == IntPtr.Zero)
				return String.Empty;
			return Marshal.PtrToStringAnsi(resultPtr);
		}

		// UResourceBundle * ures_getByKey (const UResourceBundle *resourceBundle, const char *key, UResourceBundle *fillIn, UErrorCode *status)
		[DllImport(IcuucDllName, EntryPoint = "ures_getByKey" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		private static extern IntPtr ures_getByKey(IntPtr resourceBundle, string key, IntPtr fillIn, out UErrorCode status);

		/// <summary>
		/// Get a subsection of the current resource bundle, identified by a string key.
		/// </summary>
		/// <param name="resourceBundle">The resource bundle to fetch from</param>
		/// <param name="key">String name of the subsection to get</param>
		/// <returns>A new ResourceBundle pointer representing the subsection. Closing it properly is the responsibility of the caller.</returns>
		public static IntPtr GetResourceBundleSubsection(IntPtr resourceBundle, string key)
		{
			UErrorCode status;
			IntPtr subsection = ures_getByKey(resourceBundle, key, IntPtr.Zero, out status);
			if (IsFailure(status))
				return IntPtr.Zero;
			return subsection;
		}

		// const UChar * ures_getStringByKey (const UResourceBundle *resourceBundle, const char *key, int32_t *len, UErrorCode *status)
		[DllImport(IcuucDllName, EntryPoint = "ures_getStringByKey" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		// Do NOT free the memory returned by this ICU function.
		private static extern IntPtr ures_getStringByKey(IntPtr resourceBundle, string key, out int len, out UErrorCode status);

		/// <summary>Get a named string from the resource bundle</summary>
		/// <param name="resourceBundle">The resource bundle to fetch from</param>
		/// <param name='key'>The name of the string to retrieve</param>
		/// <returns>A System.String containing the returned string, or String.Empty if the string was not found (or if that key represented a different
		/// type of data, such as a number or a resource bundle subsection). May also throw IcuException in exceptional error situations.</returns>
		public static string GetResourceBundleStringByKey(IntPtr resourceBundle, string key)
		{
			int len;
			UErrorCode status;
			IntPtr resultPtr = ures_getStringByKey(resourceBundle, key, out len, out status);
			if (IsFailure(status))
			{
				if (status == UErrorCode.U_MISSING_RESOURCE_ERROR || status == UErrorCode.U_INVALID_FORMAT_ERROR)
					return String.Empty;
				throw new IcuException(string.Format("ures_getByKey('{0}','{1}') failed with error {2}", resourceBundle, key, status), status);
			}
			if (resultPtr == IntPtr.Zero)
				return String.Empty;
			return Marshal.PtrToStringUni(resultPtr, len);
		}

		// void ures_resetIterator (UResourceBundle *resourceBundle)
		[DllImport(IcuucDllName, EntryPoint = "ures_resetIterator" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern void ures_resetIterator(IntPtr resourceBundle);

		/// <summary>
		/// Reset a resource bundle's internal iterator to the start of its items.
		/// NOTE: There's no corresponding EndResourceBundleIteration function, as the ICU C API does not require it.
		/// Also note that this is not thread-safe: if two threads share a resourceBundle, they will interfere with each other's iterations.
		/// </summary>
		/// <param name="resourceBundle"></param>
		public static void BeginResourceBundleIteration(IntPtr resourceBundle)
		{
			ures_resetIterator(resourceBundle);
		}

		// const UChar * ures_getNextString (UResourceBundle *resourceBundle, int32_t *len, const char **key, UErrorCode *status)
		[DllImport(IcuucDllName, EntryPoint = "ures_getNextString" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		// Do NOT free the memory returned by this ICU function.
		private static extern IntPtr ures_getNextString(IntPtr resourceBundle, out int len, out IntPtr key, out UErrorCode status);

		/// <summary>
		/// Get next string in a resource bundle, or NULL if its internal iterator has reached the end of the bundle (or if an error occurred).
		/// Note that this is not thread-safe: if two threads share a resourceBundle, they will interfere with each other's iterations.
		/// </summary>
		/// <param name="resourceBundle">The resource bundle we're iterating over</param>
		/// <param name="key">Out parameter: the key associated with the result string, or NULL if the result string had no key in this resource bundle.</param>
		/// <returns>The next string contained in the resource bundle, or NULL if there are no more strings to return.</returns>
		public static string GetNextStringInResourceBundleIteration(IntPtr resourceBundle, out string key)
		{
			int ignoreLen;
			IntPtr keyPtr;
			UErrorCode status;
			IntPtr resultPtr = ures_getNextString(resourceBundle, out ignoreLen, out keyPtr, out status);
			if (IsFailure(status) || resultPtr == IntPtr.Zero)
			{
				key = string.Empty;
				return null;
			}
			if (keyPtr == IntPtr.Zero)
				key = String.Empty;
			else
				key = Marshal.PtrToStringAnsi(keyPtr);
			return Marshal.PtrToStringUni(resultPtr);
		}

		/// <summary>
		/// Get next string in a resource bundle, or NULL if its internal iterator has reached the end of the bundle (or if an error occurred).
		/// Note that this is not thread-safe: if two threads share a resourceBundle, they will interfere with each other's iterations.
		/// This overload should be used if you don't care about the keys in the bundle, but only about its contents.
		/// </summary>
		/// <param name="resourceBundle">The resource bundle we're iterating over</param>
		/// <returns>The next string contained in the resource bundle, or NULL if there are no more strings to return.</returns>
		public static string GetNextStringInResourceBundleIteration(IntPtr resourceBundle)
		{
			string ignoredKey;
			return GetNextStringInResourceBundleIteration(resourceBundle, out ignoredKey);
		}

		#endregion Resource bundles

		#region Converters
		// TODO: Write public methods
		// UEnumeration * ucnv_openAllNames (UErrorCode *pErrorCode)
		[DllImport(IcuucDllName, EntryPoint = "ucnv_openAllNames" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr ucnv_openAllNames(out UErrorCode errorCode);

		/// <summary>
		/// Get an ICU UEnumeration pointer that will enumerate all converter IDs (canonical names).
		/// </summary>
		/// <returns>The opaque UEnumeration pointer. Closing it properly is the responsibility of the caller.</returns>
		public static IntPtr GetConverterEnumerator()
		{
			UErrorCode status;
			IntPtr result = ucnv_openAllNames(out status);
			if (IsFailure(status))
				throw new IcuException(string.Format("ucnv_openAllNames() failed with error {0}", status), status);
			return result;
		}

		//[DllImport(kIcuUcDllName, EntryPoint = "ucnv_getAvailableName" + VersionSuffix,
		// CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		//private static extern string ucnv_getAvailableName(int iconv);

		[DllImport(IcuucDllName, EntryPoint = "ucnv_getStandardName" + VersionSuffix,
		 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		// Do NOT free the memory returned by this ICU function.
		private static extern IntPtr ucnv_getStandardName(string name, string standard, out UErrorCode errorCode);

		/// <summary>
		/// Get the standard name for an encoding converter, accoding to some standard.
		/// </summary>
		/// <param name="name">The canonical name of the converter</param>
		/// <param name="standard">The name of the standard, e.g. "IANA"</param>
		/// <returns>The name as a System.String, or null if ucnv_getStandardName could not determine a name.</returns>
		public static string GetConverterStandardName(string name, string standard)
		{
			UErrorCode status;
			IntPtr resultPtr = ucnv_getStandardName(name, standard, out status);
			if (IsFailure(status))
				throw new IcuException(string.Format("ucnv_getStandardName(\"{0}\", \"{1}\") failed with error {2}", name, standard, status), status);
			if (resultPtr == IntPtr.Zero)
				return null;
			return Marshal.PtrToStringAnsi(resultPtr);
		}

		/// <summary>
		/// Return all IDs (canonical names) and names (standard IANA names) of the converters that ICU knows about.
		/// </summary>
		/// <returns>An IEnumerable of IcuIdAndName objects, which have Id and Name properties.</returns>
		public static IEnumerable<IcuIdAndName> GetConverterIdsAndNames()
		{
			IntPtr icuEnumerator = IntPtr.Zero;
			try
			{
				icuEnumerator = GetConverterEnumerator();
				string id;
				string name;
				while ((id = NextEnumeratorString(icuEnumerator)) != null)
				{
					name = GetConverterStandardName(id, "IANA");
					if (name != null)
						yield return new IcuIdAndName(id, name);
				}
			}
			finally
			{
				if (icuEnumerator != IntPtr.Zero)
					CloseEnumerator(icuEnumerator);
			}
		}

		#endregion Converters

		#region Transliterator enumeration
		// UEnumeration * utrans_openIDs (UErrorCode *pErrorCode)
		[DllImport(IcuinDllName, EntryPoint = "utrans_openIDs" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr utrans_openIDs(out UErrorCode errorCode);

		/// <summary>
		/// Get an ICU UEnumeration pointer that will enumerate all transliterator IDs.
		/// </summary>
		/// <returns>The opaque UEnumeration pointer. Closing it properly is the responsibility of the caller.</returns>
		public static IntPtr GetTransliteratorEnumerator()
		{
			UErrorCode status;
			IntPtr result = utrans_openIDs(out status);
			if (IsFailure(status))
				throw new IcuException(string.Format("utrans_openIDs() failed with error {0}", status), status);
			return result;
		}

		// int32_t uenum_count (UEnumeration *en, UErrorCode *status)
		[DllImport(IcuucDllName, EntryPoint = "uenum_count" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern int uenum_count(IntPtr uenum, out UErrorCode errorCode);

		/// <summary>
		/// Count the number of items in an ICU enumerator. May have to pre-fetch all the enumerator's items, so use this only when necessary.
		/// </summary>
		/// <param name="uenum">The opaque UEnumeration pointer whose contents should be counted.</param>
		/// <returns>The total number of items this UEnumeration will return.</returns>
		public static int CountEnumeratorContents(IntPtr uenum)
		{
			UErrorCode status;
			int result = uenum_count(uenum, out status);
			if (IsFailure(status))
				throw new IcuException(string.Format("uenum_count() failed with error {0}", status), status);
			return result;
		}

		// const UChar * uenum_unext (UEnumeration *en, int32_t *resultLength, UErrorCode *status)
		[DllImport(IcuucDllName, EntryPoint = "uenum_unext" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		// Do NOT free the memory returned by this ICU function.
		private static extern IntPtr uenum_unext(IntPtr uenum, out int resultLen, out UErrorCode errorCode);

		/// <summary>
		/// Advance the enumerator to the next string and return it. Returns NULL if the enumerator is exhausted.
		/// </summary>
		/// <param name="uenum">The opaque UEnumeration pointer to advance.</param>
		/// <returns>The next Unicode string returned by the enumerator, or NULL if no more strings are available.</returns>
		public static string NextEnumeratorString(IntPtr uenum)
		{
			UErrorCode status;
			int len;
			IntPtr resultPtr = uenum_unext(uenum, out len, out status);
			if (IsFailure(status))
				throw new IcuException(string.Format("uenum_unext() failed with error {0}", status), status);
			if (resultPtr == IntPtr.Zero)
				return null;
			return Marshal.PtrToStringUni(resultPtr, len);
		}

		// void uenum_close (UEnumeration *en)
		[DllImport(IcuucDllName, EntryPoint = "uenum_close" + VersionSuffix,
			 CallingConvention = CallingConvention.Cdecl)]
		private static extern void uenum_close(IntPtr uenum);

		/// <summary>
		/// Close an ICU UEnumerator. This is the responsibility of the code that obtained the UEnumerator.
		/// </summary>
		/// <param name="uenum">The opaque UEnumeration pointer to close.</param>
		public static void CloseEnumerator(IntPtr uenum)
		{
			uenum_close(uenum);
		}

		/// <summary>
		/// Count available transliterator IDs. DEPRECATED. If you are using this code, try switching to an LgIcuTransliteratorEnumerator instead.
		/// </summary>
		/// <returns>How many transliterator IDs (and thus, how many transliterators) are known to ICU.</returns>
		public static int CountTransliteratorIDs()
		{
			IntPtr uenum = IntPtr.Zero;
			try
			{
				uenum = GetTransliteratorEnumerator();
				return CountEnumeratorContents(uenum);
			}
			finally
			{
				if (uenum != IntPtr.Zero)
					CloseEnumerator(uenum);
			}
		}

		// No public APIs for the next three functions because they are NOT intended to be used anywhere outside GetTransliteratorDisplayName().

		// UMessageFormat * umsg_open (const UChar *pattern, int32_t patternLength, const char *locale, UParseError *parseError, UErrorCode *status)
		// Open a message formatter with given pattern and for the given locale.
		[DllImport(IcuinDllName, EntryPoint = "umsg_open" + VersionSuffix,
		 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern IntPtr umsg_open(string pattern, int patternLen, string locale, out UParseError parseError, out UErrorCode status);

		// UMessageFormat * umsg_close (UMessageFormat *format)
		// Close a message formatter.
		[DllImport(IcuinDllName, EntryPoint = "umsg_close" + VersionSuffix,
		 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern void umsg_close(IntPtr format);

		// int32_t umsg_format (const UMessageFormat *fmt, UChar *result, int32_t resultLength, UErrorCode *status, ...)
		// We hardcode it to take 3 parameters, an int and two strings, because that's how we'll be calling it from GetTransliteratorDisplayName.
		// The alternative is to use the completely undocumented __arglist keyword, and that's not worth the danger given that we only call this function from one place.
		[DllImport(IcuinDllName, EntryPoint = "umsg_format" + VersionSuffix,
		 CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		private static extern int umsg_format(IntPtr format, StringBuilder result, int resultLen, out UErrorCode status, double arg0, string arg1, string arg2);

		private static int MessageFormat(IntPtr format, out string result, out UErrorCode status, double arg0, string arg1, string arg2)
		{
			StringBuilder builder = new StringBuilder(255);
			int actualLen = umsg_format(format, builder, 256, out status, arg0, arg1, arg2);
			if (IsSuccess(status) && actualLen <= 255)
			{
				result = builder.ToString();
				return actualLen;
			}
			StringBuilder largerBuilder = new StringBuilder(actualLen);
			actualLen = umsg_format(format, largerBuilder, actualLen+1, out status, arg0, arg1, arg2);
			if (IsSuccess(status))
			{
				result = builder.ToString();
				return actualLen;
			}
			result = string.Empty;
			return 0;
		}

		//[DllImport(kIcuinDllName, EntryPoint = "utrans_getDisplayName" + VersionSuffix,
		// CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		//private static extern string utrans_getDisplayName(int iconv); // This doesn't exist in the C API. We've had to, more or less, implement it ourselves via Resource Bundle API.

		/// <summary>
		/// Reimplementation of TransliteratorIDParser::IDtoSTV from the ICU C++ API. Parses a transliterator ID in one of several formats
		/// and returns the source, target and variant components of the ID. Valid formats are T, T/V, S-T, S-T/V, or S/V-T. If source is
		/// missing, it will be set to "Any". If target or variant is missing, they will be the empty string. (If target is missing, the
		/// ID is not well-formed, but this function will not throw an exception). If variant is present, the slash will be included in
		/// </summary>
		/// <param name="transId">Transliterator ID to parse</param>
		/// <param name="source">"Any" if source was missing, otherwise source component of the ID</param>
		/// <param name="target">Empty string if no target, otherwise target component of the ID (should always be present in a well-formed ID)</param>
		/// <param name="variant">Empty string if no variant, otherwise variant component of the ID, *with a '/' as its first character*.</param>
		/// <returns>True if source was present, false if source was missing.</returns>
		private static bool ParseTransliteratorID(string transId, out string source, out string target, out string variant)
		{
			// This is a straight port of the TransliteratorIDParser::IDtoSTV logic, with basically no changes
			source = "Any";
			int tgtSep = transId.IndexOf("-");
			int varSep = transId.IndexOf("/");
			if (varSep < 0)
				varSep = transId.Length;

			bool isSourcePresent = false;
			if (tgtSep < 0)
			{
				// Form: T/V or T (or /V)
				target = transId.Substring(0, varSep);
				variant = transId.Substring(varSep);
			}
			else if (tgtSep < varSep)
			{
				// Form: S-T/V or S-T (or -T/V or -T)
				if (tgtSep > 0)
				{
					source = transId.Substring(0, tgtSep);
					isSourcePresent = true;
				}
				target = transId.Substring(tgtSep + 1, varSep - tgtSep - 1);
				variant = transId.Substring(varSep);
			}
			else
			{
				// Form: S/V-T or /V-T
				if (varSep > 0)
				{
					source = transId.Substring(0, varSep);
					isSourcePresent = true;
				}
				variant = transId.Substring(varSep, tgtSep - varSep);
				target = transId.Substring(tgtSep + 1);
			}

			// The above Substring calls have all left the variant either empty or looking like "/V". In the original C++
			// implementation, we removed the leading "/". But here, we keep it because the only code that needs to call
			// this is GetTransliteratorDisplayName, which wants the leading "/" on variant names.
			//if (variant.Length > 0)
			//	variant = variant.Substring(1);

			return isSourcePresent; // This is currently not used, but we return it anyway for compatibility with original C++ implementation
		}

		/// <summary>
		/// Get a display name for a transliterator. This reimplements the logic from the C++ Transliterator::getDisplayName method,
		/// since the ICU C API doesn't expose a utrans_getDisplayName() call. (Unfortunately).
		/// Note that if no text is found for the given locale, ICU will (by default) fallback to the root locale. However, the
		/// root locale's strings for transliterator display names are ugly and not suitable for displaying to the user. Therefore,
		/// if we have to fallback, we fallback to the "en" locale instead of the root locale.
		/// </summary>
		/// <param name="transId">The translator's system ID in ICU, as returned (one at a time) by utrans_openIDs().</param>
		/// <param name="localeName">The ICU name of the locale in which to calculate the display name.</param>
		/// <returns>A name suitable for displaying to the user in the given locale, or in English if no translated text is present in the given locale.</returns>
		public static string GetTransliteratorDisplayName(string transId, string localeName)
		{
			const string translitDisplayNameRBKeyPrefix = "%Translit%%";  // See RB_DISPLAY_NAME_PREFIX in translit.cpp in ICU source code
			const string scriptDisplayNameRBKeyPrefix = "%Translit%";  // See RB_SCRIPT_DISPLAY_NAME_PREFIX in translit.cpp in ICU source code
			const string translitResourceBundleName = "ICUDATA-translit";
			const string translitDisplayNamePatternKey = "TransliteratorNamePattern";

			string source;
			string target;
			string variant;
			ParseTransliteratorID(transId, out source, out target, out variant);
			if (target.Length < 1)
				return transId;  // Malformed ID? Give up

			IntPtr translitBundle = IntPtr.Zero;
			IntPtr translitBundleFallback = IntPtr.Zero;
			try
			{
				translitBundle = OpenResourceBundle(translitResourceBundleName, localeName);
				translitBundleFallback = OpenResourceBundle(translitResourceBundleName, "en");
				string pattern = GetResourceBundleStringByKey(translitBundle, translitDisplayNamePatternKey);
				// If we don't find a MessageFormat pattern in our locale, try the English fallback
				if (String.IsNullOrEmpty(pattern))
					pattern = GetResourceBundleStringByKey(translitBundleFallback, translitDisplayNamePatternKey);
				// Still can't find a pattern? Then we won't be able to format the ID, so just return it
				if (String.IsNullOrEmpty(pattern))
					return transId;

				// First check if there is a specific localized name for this transliterator, and if so, just return it.
				// Note that we need to check whether the string we got still starts with the "%Translit%%" prefix, because
				// if so, it means that we got a value from the root locale's bundle, which isn't actually localized.
				string translitLocalizedName = GetResourceBundleStringByKey(translitBundle,
					translitDisplayNameRBKeyPrefix + transId);
				if (!String.IsNullOrEmpty(translitLocalizedName) && !translitLocalizedName.StartsWith(translitDisplayNameRBKeyPrefix))
					return translitLocalizedName;

				// There was no specific localized name for this transliterator (which will be true of most cases). Build one.
				UParseError parseErrorIgnored;
				UErrorCode status;
				IntPtr messageFormatter = umsg_open(pattern, pattern.Length, localeName, out parseErrorIgnored, out status);
				if (IsFailure(status))
					return transId;

				// Try getting localized display names for the source and target, if possible.
				string localizedSource = GetResourceBundleStringByKey(translitBundle, scriptDisplayNameRBKeyPrefix + source);
				if (String.IsNullOrEmpty(localizedSource))
				{
					localizedSource = source; // Can't localize
				}
				else
				{
					// As with the transliterator name, we need to check that the string we got didn't come from the root bundle
					// (which just returns a string that still contains the ugly %Translit% prefix). If it did, fall back to English.
					if (localizedSource.StartsWith(scriptDisplayNameRBKeyPrefix))
						localizedSource = GetResourceBundleStringByKey(translitBundleFallback, scriptDisplayNameRBKeyPrefix + source);
					if (String.IsNullOrEmpty(localizedSource) || localizedSource.StartsWith(scriptDisplayNameRBKeyPrefix))
						localizedSource = source;
				}

				// Same thing for target
				string localizedTarget = GetResourceBundleStringByKey(translitBundle, scriptDisplayNameRBKeyPrefix + target);
				if (String.IsNullOrEmpty(localizedTarget))
				{
					localizedTarget = target; // Can't localize
				}
				else
				{
					if (localizedTarget.StartsWith(scriptDisplayNameRBKeyPrefix))
						localizedTarget = GetResourceBundleStringByKey(translitBundleFallback, scriptDisplayNameRBKeyPrefix + target);
					if (String.IsNullOrEmpty(localizedTarget) || localizedTarget.StartsWith(scriptDisplayNameRBKeyPrefix))
						localizedTarget = target;
				}

				string displayName;
				MessageFormat(messageFormatter, out displayName, out status, 2.0, localizedSource, localizedTarget);
				if (IsSuccess(status))
					return displayName + variant; // Variant is either empty string or starts with "/"
				else
					return transId; // If formatting fails, the transliterator's ID is still our final fallback
			}
			finally
			{
				if (translitBundle != IntPtr.Zero)
					CloseResourceBundle(translitBundle);
				if (translitBundleFallback != IntPtr.Zero)
					CloseResourceBundle(translitBundleFallback);
			}
		}

		/// <summary>
		/// Get the IDs and display names of all transliterators registered with ICU.
		/// Display names will be in the locale specified by the displayLocale parameter; omit it or pass in null to use the default locale.
		/// </summary>
		public static IEnumerable<IcuIdAndName> GetTransliteratorIdsAndNames(string displayLocale = null)
		{
			IntPtr icuEnumerator = IntPtr.Zero;
			try
			{
				icuEnumerator = GetTransliteratorEnumerator();
				while (true)
				{
					string id = NextEnumeratorString(icuEnumerator);
					if (id == null)
						break;
					string name = GetTransliteratorDisplayName(id, displayLocale);
					yield return new IcuIdAndName(id, name);
				}
			}
			finally
			{
				if (icuEnumerator != IntPtr.Zero)
					CloseEnumerator(icuEnumerator);
			}
		}

		#endregion Transliterator enumeration

		private static bool IsSuccess(UErrorCode errorCode)
		{
			return errorCode <= 0;
		}

		private static bool IsFailure(UErrorCode errorCode)
		{
			return errorCode > 0;
		}

		/**
		 * Selector constants for u_charName().
		 * u_charName() returns the "modern" name of a
		 * Unicode character; or the name that was defined in
		 * Unicode version 1.0, before the Unicode standard merged
		 * with ISO-10646; or an "extended" name that gives each
		 * Unicode code point a unique name.
		 *
		 * @see u_charName
		 * @stable ICU 2.0
		 */
		/// <summary></summary>
		public enum UCharNameChoice
		{
			/// <summary></summary>
			U_UNICODE_CHAR_NAME,
			/// <summary></summary>
			U_UNICODE_10_CHAR_NAME,
			/// <summary></summary>
			U_EXTENDED_CHAR_NAME,
			/// <summary></summary>
			U_CHAR_NAME_CHOICE_COUNT
		}


		/// <summary>
		/// ICU error code
		/// </summary>
		public enum UErrorCode
		{
			/// <summary></summary>
			U_USING_FALLBACK_WARNING = -128,   /** A resource bundle lookup returned a fallback result (not an error) */
			/// <summary></summary>
			U_ERROR_WARNING_START = -128,   /** Start of information results (semantically successful) */
			/// <summary></summary>
			U_USING_DEFAULT_WARNING = -127,   /** A resource bundle lookup returned a result from the root locale (not an error) */
			/// <summary></summary>
			U_SAFECLONE_ALLOCATED_WARNING = -126, /** A SafeClone operation required allocating memory (informational only) */
			/// <summary></summary>
			U_STATE_OLD_WARNING = -125,   /** ICU has to use compatibility layer to construct the service. Expect performance/memory usage degradation. Consider upgrading */
			/// <summary></summary>
			U_STRING_NOT_TERMINATED_WARNING = -124,/** An output string could not be NUL-terminated because output length==destCapacity. */
			/// <summary></summary>
			U_SORT_KEY_TOO_SHORT_WARNING = -123, /** Number of levels requested in getBound is higher than the number of levels in the sort key */
			/// <summary></summary>
			U_AMBIGUOUS_ALIAS_WARNING = -122,   /** This converter alias can go to different converter implementations */
			/// <summary></summary>
			U_DIFFERENT_UCA_VERSION = -121,     /** ucol_open encountered a mismatch between UCA version and collator image version, so the collator was constructed from rules. No impact to further function */
			/// <summary></summary>
			U_ERROR_WARNING_LIMIT,              /** This must always be the last warning value to indicate the limit for UErrorCode warnings (last warning code +1) */
			/// <summary></summary>
			U_ZERO_ERROR = 0,     /** No error, no warning. */
			/// <summary></summary>
			U_ILLEGAL_ARGUMENT_ERROR = 1,     /** Start of codes indicating failure */
			/// <summary></summary>
			U_MISSING_RESOURCE_ERROR = 2,     /** The requested resource cannot be found */
			/// <summary></summary>
			U_INVALID_FORMAT_ERROR = 3,     /** Data format is not what is expected */
			/// <summary></summary>
			U_FILE_ACCESS_ERROR = 4,     /** The requested file cannot be found */
			/// <summary></summary>
			U_INTERNAL_PROGRAM_ERROR = 5,     /** Indicates a bug in the library code */
			/// <summary></summary>
			U_MESSAGE_PARSE_ERROR = 6,     /** Unable to parse a message (message format) */
			/// <summary></summary>
			U_MEMORY_ALLOCATION_ERROR = 7,     /** Memory allocation error */
			/// <summary></summary>
			U_INDEX_OUTOFBOUNDS_ERROR = 8,     /** Trying to access the index that is out of bounds */
			/// <summary></summary>
			U_PARSE_ERROR = 9,     /** Equivalent to Java ParseException */
			/// <summary></summary>
			U_INVALID_CHAR_FOUND = 10,     /** In the Character conversion routines: Invalid character or sequence was encountered. In other APIs: Invalid character or code point name. */
			/// <summary></summary>
			U_TRUNCATED_CHAR_FOUND = 11,     /** In the Character conversion routines: More bytes are required to complete the conversion successfully */
			/// <summary></summary>
			U_ILLEGAL_CHAR_FOUND = 12,     /** In codeset conversion: a sequence that does NOT belong in the codepage has been encountered */
			/// <summary></summary>
			U_INVALID_TABLE_FORMAT = 13,     /** Conversion table file found, but corrupted */
			/// <summary></summary>
			U_INVALID_TABLE_FILE = 14,     /** Conversion table file not found */
			/// <summary></summary>
			U_BUFFER_OVERFLOW_ERROR = 15,     /** A result would not fit in the supplied buffer */
			/// <summary></summary>
			U_UNSUPPORTED_ERROR = 16,     /** Requested operation not supported in current context */
			/// <summary></summary>
			U_RESOURCE_TYPE_MISMATCH = 17,     /** an operation is requested over a resource that does not support it */
			/// <summary></summary>
			U_ILLEGAL_ESCAPE_SEQUENCE = 18,     /** ISO-2022 illlegal escape sequence */
			/// <summary></summary>
			U_UNSUPPORTED_ESCAPE_SEQUENCE = 19, /** ISO-2022 unsupported escape sequence */
			/// <summary></summary>
			U_NO_SPACE_AVAILABLE = 20,     /** No space available for in-buffer expansion for Arabic shaping */
			/// <summary></summary>
			U_CE_NOT_FOUND_ERROR = 21,     /** Currently used only while setting variable top, but can be used generally */
			/// <summary></summary>
			U_PRIMARY_TOO_LONG_ERROR = 22,     /** User tried to set variable top to a primary that is longer than two bytes */
			/// <summary></summary>
			U_STATE_TOO_OLD_ERROR = 23,     /** ICU cannot construct a service from this state, as it is no longer supported */
			/// <summary></summary>
			U_TOO_MANY_ALIASES_ERROR = 24,     /** There are too many aliases in the path to the requested resource.
											 It is very possible that a circular alias definition has occured */
			/// <summary></summary>
			U_ENUM_OUT_OF_SYNC_ERROR = 25,     /** UEnumeration out of sync with underlying collection */
			/// <summary></summary>
			U_INVARIANT_CONVERSION_ERROR = 26,  /** Unable to convert a UChar* string to char* with the invariant converter. */
			/// <summary></summary>
			U_STANDARD_ERROR_LIMIT,             /** This must always be the last value to indicate the limit for standard errors */
			/// <summary>
			/// the error code range 0x10000 0x10100 are reserved for Transliterator
			/// </summary>
			U_BAD_VARIABLE_DEFINITION = 0x10000,/** Missing '$' or duplicate variable name */
			/// <summary></summary>
			U_PARSE_ERROR_START = 0x10000,    /** Start of Transliterator errors */
			/// <summary></summary>
			U_MALFORMED_RULE,                 /** Elements of a rule are misplaced */
			/// <summary></summary>
			U_MALFORMED_SET,                  /** A UnicodeSet pattern is invalid*/
			/// <summary></summary>
			U_MALFORMED_SYMBOL_REFERENCE,     /** UNUSED as of ICU 2.4 */
			/// <summary></summary>
			U_MALFORMED_UNICODE_ESCAPE,       /** A Unicode escape pattern is invalid*/
			/// <summary></summary>
			U_MALFORMED_VARIABLE_DEFINITION,  /** A variable definition is invalid */
			/// <summary></summary>
			U_MALFORMED_VARIABLE_REFERENCE,   /** A variable reference is invalid */
			/// <summary></summary>
			U_MISMATCHED_SEGMENT_DELIMITERS,  /** UNUSED as of ICU 2.4 */
			/// <summary></summary>
			U_MISPLACED_ANCHOR_START,         /** A start anchor appears at an illegal position */
			/// <summary></summary>
			U_MISPLACED_CURSOR_OFFSET,        /** A cursor offset occurs at an illegal position */
			/// <summary></summary>
			U_MISPLACED_QUANTIFIER,           /** A quantifier appears after a segment close delimiter */
			/// <summary></summary>
			U_MISSING_OPERATOR,               /** A rule contains no operator */
			/// <summary></summary>
			U_MISSING_SEGMENT_CLOSE,          /** UNUSED as of ICU 2.4 */
			/// <summary></summary>
			U_MULTIPLE_ANTE_CONTEXTS,         /** More than one ante context */
			/// <summary></summary>
			U_MULTIPLE_CURSORS,               /** More than one cursor */
			/// <summary></summary>
			U_MULTIPLE_POST_CONTEXTS,         /** More than one post context */
			/// <summary></summary>
			U_TRAILING_BACKSLASH,             /** A dangling backslash */
			/// <summary></summary>
			U_UNDEFINED_SEGMENT_REFERENCE,    /** A segment reference does not correspond to a defined segment */
			/// <summary></summary>
			U_UNDEFINED_VARIABLE,             /** A variable reference does not correspond to a defined variable */
			/// <summary></summary>
			U_UNQUOTED_SPECIAL,               /** A special character was not quoted or escaped */
			/// <summary></summary>
			U_UNTERMINATED_QUOTE,             /** A closing single quote is missing */
			/// <summary></summary>
			U_RULE_MASK_ERROR,                /** A rule is hidden by an earlier more general rule */
			/// <summary></summary>
			U_MISPLACED_COMPOUND_FILTER,      /** A compound filter is in an invalid location */
			/// <summary></summary>
			U_MULTIPLE_COMPOUND_FILTERS,      /** More than one compound filter */
			/// <summary></summary>
			U_INVALID_RBT_SYNTAX,             /** A "::id" rule was passed to the RuleBasedTransliterator parser */
			/// <summary></summary>
			U_INVALID_PROPERTY_PATTERN,       /** UNUSED as of ICU 2.4 */
			/// <summary></summary>
			U_MALFORMED_PRAGMA,               /** A 'use' pragma is invlalid */
			/// <summary></summary>
			U_UNCLOSED_SEGMENT,               /** A closing ')' is missing */
			/// <summary></summary>
			U_ILLEGAL_CHAR_IN_SEGMENT,        /** UNUSED as of ICU 2.4 */
			/// <summary></summary>
			U_VARIABLE_RANGE_EXHAUSTED,       /** Too many stand-ins generated for the given variable range */
			/// <summary></summary>
			U_VARIABLE_RANGE_OVERLAP,         /** The variable range overlaps characters used in rules */
			/// <summary></summary>
			U_ILLEGAL_CHARACTER,              /** A special character is outside its allowed context */
			/// <summary></summary>
			U_INTERNAL_TRANSLITERATOR_ERROR,  /** Internal transliterator system error */
			/// <summary></summary>
			U_INVALID_ID,                     /** A "::id" rule specifies an unknown transliterator */
			/// <summary></summary>
			U_INVALID_FUNCTION,               /** A "&amp;fn()" rule specifies an unknown transliterator */
			/// <summary>The limit for Transliterator errors</summary>
			U_PARSE_ERROR_LIMIT,

			/// <summary>
			/// the error code range 0x10100 0x10200 are reserved for formatting API parsing error
			/// </summary>
			U_UNEXPECTED_TOKEN = 0x10100,       /** Syntax error in format pattern */
			/// <summary></summary>
			U_FMT_PARSE_ERROR_START = 0x10100,  /** Start of format library errors */
			/// <summary></summary>
			U_MULTIPLE_DECIMAL_SEPARATORS,    /** More than one decimal separator in number pattern */
			/// <summary></summary>
			U_MULTIPLE_DECIMAL_SEPERATORS = U_MULTIPLE_DECIMAL_SEPARATORS, /** Typo: kept for backward compatibility. Use U_MULTIPLE_DECIMAL_SEPARATORS */
			/// <summary></summary>
			U_MULTIPLE_EXPONENTIAL_SYMBOLS,   /** More than one exponent symbol in number pattern */
			/// <summary></summary>
			U_MALFORMED_EXPONENTIAL_PATTERN,  /** Grouping symbol in exponent pattern */
			/// <summary></summary>
			U_MULTIPLE_PERCENT_SYMBOLS,       /** More than one percent symbol in number pattern */
			/// <summary></summary>
			U_MULTIPLE_PERMILL_SYMBOLS,       /** More than one permill symbol in number pattern */
			/// <summary></summary>
			U_MULTIPLE_PAD_SPECIFIERS,        /** More than one pad symbol in number pattern */
			/// <summary></summary>
			U_PATTERN_SYNTAX_ERROR,           /** Syntax error in format pattern */
			/// <summary></summary>
			U_ILLEGAL_PAD_POSITION,           /** Pad symbol misplaced in number pattern */
			/// <summary></summary>
			U_UNMATCHED_BRACES,               /** Braces do not match in message pattern */
			/// <summary></summary>
			U_UNSUPPORTED_PROPERTY,           /** UNUSED as of ICU 2.4 */
			/// <summary></summary>
			U_UNSUPPORTED_ATTRIBUTE,          /** UNUSED as of ICU 2.4 */
			/// <summary>The limit for format library errors</summary>
			U_FMT_PARSE_ERROR_LIMIT,

			/// <summary>
			/// the error code range 0x10200 0x102ff are reserved for Break Iterator related error
			/// </summary>
			U_BRK_ERROR_START = 0x10200,             /** Start of codes indicating Break Iterator failures */
			/// <summary></summary>
			U_BRK_INTERNAL_ERROR,                  /** An internal error (bug) was detected.             */
			/// <summary></summary>
			U_BRK_HEX_DIGITS_EXPECTED,             /** Hex digits expected as part of a escaped char in a rule. */
			/// <summary></summary>
			U_BRK_SEMICOLON_EXPECTED,              /** Missing ';' at the end of a RBBI rule.            */
			/// <summary></summary>
			U_BRK_RULE_SYNTAX,                     /** Syntax error in RBBI rule.                        */
			/// <summary></summary>
			U_BRK_UNCLOSED_SET,                    /** UnicodeSet witing an RBBI rule missing a closing ']'.  */
			/// <summary></summary>
			U_BRK_ASSIGN_ERROR,                    /** Syntax error in RBBI rule assignment statement.   */
			/// <summary></summary>
			U_BRK_VARIABLE_REDFINITION,            /** RBBI rule $Variable redefined.                    */
			/// <summary></summary>
			U_BRK_MISMATCHED_PAREN,                /** Mis-matched parentheses in an RBBI rule.          */
			/// <summary></summary>
			U_BRK_NEW_LINE_IN_QUOTED_STRING,       /** Missing closing quote in an RBBI rule.            */
			/// <summary></summary>
			U_BRK_UNDEFINED_VARIABLE,              /** Use of an undefined $Variable in an RBBI rule.    */
			/// <summary></summary>
			U_BRK_INIT_ERROR,                      /** Initialization failure.  Probable missing ICU Data. */
			/// <summary></summary>
			U_BRK_RULE_EMPTY_SET,                  /** Rule contains an empty Unicode Set.               */
			/// <summary>This must always be the last value to indicate the limit for Break Iterator failures</summary>
			U_BRK_ERROR_LIMIT,

			/// <summary>
			/// The error codes in the range 0x10300-0x103ff are reserved for regular expression related errrs
			/// </summary>
			U_REGEX_ERROR_START = 0x10300,          /** Start of codes indicating Regexp failures          */
			/// <summary></summary>
			U_REGEX_INTERNAL_ERROR,               /** An internal error (bug) was detected.              */
			/// <summary></summary>
			U_REGEX_RULE_SYNTAX,                  /** Syntax error in regexp pattern.                    */
			/// <summary></summary>
			U_REGEX_INVALID_STATE,                /** RegexMatcher in invalid state for requested operation */
			/// <summary></summary>
			U_REGEX_BAD_ESCAPE_SEQUENCE,          /** Unrecognized backslash escape sequence in pattern  */
			/// <summary></summary>
			U_REGEX_PROPERTY_SYNTAX,              /** Incorrect Unicode property                         */
			/// <summary></summary>
			U_REGEX_UNIMPLEMENTED,                /** Use of regexp feature that is not yet implemented. */
			/// <summary></summary>
			U_REGEX_MISMATCHED_PAREN,             /** Incorrectly nested parentheses in regexp pattern.  */
			/// <summary></summary>
			U_REGEX_NUMBER_TOO_BIG,               /** Decimal number is too large.                       */
			/// <summary></summary>
			U_REGEX_BAD_INTERVAL,                 /** Error in {min,max} interval                        */
			/// <summary></summary>
			U_REGEX_MAX_LT_MIN,                   /** In {min,max}, max is less than min.                */
			/// <summary></summary>
			U_REGEX_INVALID_BACK_REF,             /** Back-reference to a non-existent capture group.    */
			/// <summary></summary>
			U_REGEX_INVALID_FLAG,                 /** Invalid value for match mode flags.                */
			/// <summary></summary>
			U_REGEX_LOOK_BEHIND_LIMIT,            /** Look-Behind pattern matches must have a bounded maximum length.    */
			/// <summary></summary>
			U_REGEX_SET_CONTAINS_STRING,          /** Regexps cannot have UnicodeSets containing strings.*/
			/// <summary>This must always be the last value to indicate the limit for regexp errors</summary>
			U_REGEX_ERROR_LIMIT,

			/// <summary>
			/// The error code in the range 0x10400-0x104ff are reserved for IDNA related error codes
			/// </summary>
			U_IDNA_ERROR_START = 0x10400,
			/// <summary></summary>
			U_IDNA_PROHIBITED_CODEPOINT_FOUND_ERROR,
			/// <summary></summary>
			U_IDNA_UNASSIGNED_CODEPOINT_FOUND_ERROR,
			/// <summary></summary>
			U_IDNA_CHECK_BIDI_ERROR,
			/// <summary></summary>
			U_IDNA_STD3_ASCII_RULES_ERROR,
			/// <summary></summary>
			U_IDNA_ACE_PREFIX_ERROR,
			/// <summary></summary>
			U_IDNA_VERIFICATION_ERROR,
			/// <summary></summary>
			U_IDNA_LABEL_TOO_LONG_ERROR,
			/// <summary></summary>
			U_IDNA_ERROR_LIMIT,
			/// <summary>This must always be the last value to indicate the limit for UErrorCode (last error code +1)</summary>
			U_ERROR_LIMIT = U_IDNA_ERROR_LIMIT
		}

		/// <summary>
		/// Defined in ICU uchar.h
		/// http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158
		/// </summary>
		public enum UProperty
		{
			/*  See note !!.  Comments of the form "Binary property Dash",
				"Enumerated property Script", "Double property Numeric_Value",
				and "String property Age" are read by genpname. */

			/*  Note: Place UCHAR_ALPHABETIC before UCHAR_BINARY_START so that
				debuggers display UCHAR_ALPHABETIC as the symbolic name for 0,
				rather than UCHAR_BINARY_START.  Likewise for other *_START
				identifiers. */

			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_ALPHABETIC = 0,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_BINARY_START = UCHAR_ALPHABETIC,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_ASCII_HEX_DIGIT,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_BIDI_CONTROL,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_BIDI_MIRRORED,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_DASH,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_DEFAULT_IGNORABLE_CODE_POINT,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_DEPRECATED,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_DIACRITIC,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_EXTENDER,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_FULL_COMPOSITION_EXCLUSION,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_GRAPHEME_BASE,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_GRAPHEME_EXTEND,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_GRAPHEME_LINK,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_HEX_DIGIT,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_HYPHEN,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_ID_CONTINUE,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_ID_START,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_IDEOGRAPHIC,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_IDS_BINARY_OPERATOR,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_IDS_TRINARY_OPERATOR,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_JOIN_CONTROL,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_LOGICAL_ORDER_EXCEPTION,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_LOWERCASE,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_MATH,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_NONCHARACTER_CODE_POINT,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_QUOTATION_MARK,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_RADICAL,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_SOFT_DOTTED,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_TERMINAL_PUNCTUATION,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_UNIFIED_IDEOGRAPH,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_UPPERCASE,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_WHITE_SPACE,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_XID_CONTINUE,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_XID_START,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_CASE_SENSITIVE,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_S_TERM,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_VARIATION_SELECTOR,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_NFD_INERT,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_NFKD_INERT,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_NFC_INERT,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_NFKC_INERT,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_SEGMENT_STARTER,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_BINARY_LIMIT,

			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_BIDI_CLASS = 0x1000,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_INT_START = UCHAR_BIDI_CLASS,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_BLOCK,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_CANONICAL_COMBINING_CLASS,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_DECOMPOSITION_TYPE,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_EAST_ASIAN_WIDTH,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_GENERAL_CATEGORY,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_JOINING_GROUP,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_JOINING_TYPE,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_LINE_BREAK,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_NUMERIC_TYPE,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_SCRIPT,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_HANGUL_SYLLABLE_TYPE,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_NFD_QUICK_CHECK,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_NFKD_QUICK_CHECK,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_NFC_QUICK_CHECK,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_NFKC_QUICK_CHECK,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_LEAD_CANONICAL_COMBINING_CLASS,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_TRAIL_CANONICAL_COMBINING_CLASS,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_INT_LIMIT,

			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_GENERAL_CATEGORY_MASK = 0x2000,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_MASK_START = UCHAR_GENERAL_CATEGORY_MASK,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_MASK_LIMIT,

			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_NUMERIC_VALUE = 0x3000,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_DOUBLE_START = UCHAR_NUMERIC_VALUE,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_DOUBLE_LIMIT,

			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_AGE = 0x4000,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_STRING_START = UCHAR_AGE,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_BIDI_MIRRORING_GLYPH,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_CASE_FOLDING,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_ISO_COMMENT,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_LOWERCASE_MAPPING,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_NAME,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_SIMPLE_CASE_FOLDING,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_SIMPLE_LOWERCASE_MAPPING,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_SIMPLE_TITLECASE_MAPPING,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_SIMPLE_UPPERCASE_MAPPING,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_TITLECASE_MAPPING,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_UNICODE_1_NAME,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_UPPERCASE_MAPPING,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_STRING_LIMIT,

			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l00158</summary>
			UCHAR_INVALID_CODE = -1
		}

		/// <summary>
		/// The compatability decomposition type that appears in field 5 in the "tag".
		/// http://oss.software.ibm.com/icu/apiref/uchar_8h.html#a533
		/// http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html#l01547
		/// </summary>
		public enum UDecompositionType
		{
			/// <summary>http://www.unicode.org/Public/UNIDATA/UCD.html#Character_Decomposition_Mappings</summary>
			U_DT_NONE,
			/// <summary>http://www.unicode.org/Public/UNIDATA/UCD.html#Character_Decomposition_Mappings</summary>
			U_DT_CANONICAL,
			/// <summary>http://www.unicode.org/Public/UNIDATA/UCD.html#Character_Decomposition_Mappings</summary>
			U_DT_COMPAT,
			/// <summary>http://www.unicode.org/Public/UNIDATA/UCD.html#Character_Decomposition_Mappings</summary>
			U_DT_CIRCLE,
			/// <summary>http://www.unicode.org/Public/UNIDATA/UCD.html#Character_Decomposition_Mappings</summary>
			U_DT_FINAL,
			/// <summary>http://www.unicode.org/Public/UNIDATA/UCD.html#Character_Decomposition_Mappings</summary>
			U_DT_FONT,
			/// <summary>http://www.unicode.org/Public/UNIDATA/UCD.html#Character_Decomposition_Mappings</summary>
			U_DT_FRACTION,
			/// <summary>http://www.unicode.org/Public/UNIDATA/UCD.html#Character_Decomposition_Mappings</summary>
			U_DT_INITIAL,
			/// <summary>http://www.unicode.org/Public/UNIDATA/UCD.html#Character_Decomposition_Mappings</summary>
			U_DT_ISOLATED,
			/// <summary>http://www.unicode.org/Public/UNIDATA/UCD.html#Character_Decomposition_Mappings</summary>
			U_DT_MEDIAL,
			/// <summary>http://www.unicode.org/Public/UNIDATA/UCD.html#Character_Decomposition_Mappings</summary>
			U_DT_NARROW,
			/// <summary>http://www.unicode.org/Public/UNIDATA/UCD.html#Character_Decomposition_Mappings</summary>
			U_DT_NOBREAK,
			/// <summary>http://www.unicode.org/Public/UNIDATA/UCD.html#Character_Decomposition_Mappings</summary>
			U_DT_SMALL,
			/// <summary>http://www.unicode.org/Public/UNIDATA/UCD.html#Character_Decomposition_Mappings</summary>
			U_DT_SQUARE,
			/// <summary>http://www.unicode.org/Public/UNIDATA/UCD.html#Character_Decomposition_Mappings</summary>
			U_DT_SUB,
			/// <summary>http://www.unicode.org/Public/UNIDATA/UCD.html#Character_Decomposition_Mappings</summary>
			U_DT_SUPER,
			/// <summary>http://www.unicode.org/Public/UNIDATA/UCD.html#Character_Decomposition_Mappings</summary>
			U_DT_VERTICAL,
			/// <summary>http://www.unicode.org/Public/UNIDATA/UCD.html#Character_Decomposition_Mappings</summary>
			U_DT_WIDE,
			/// <summary>http://www.unicode.org/Public/UNIDATA/UCD.html#Character_Decomposition_Mappings</summary>
			U_DT_COUNT
		}

		/// <summary>
		/// Numeric Type constants
		/// These are used for fields 6,7, and 8
		/// </summary>
		public enum UNumericType
		{
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			U_NT_NONE,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			U_NT_DECIMAL,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			U_NT_DIGIT,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			U_NT_NUMERIC,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			U_NT_COUNT
		}

		/// <summary>
		/// Collation constants
		/// </summary>
		public enum UColAttributeValue
		{
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			UCOL_DEFAULT = -1,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			UCOL_PRIMARY = 0,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			UCOL_SECONDARY = 1,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			UCOL_TERTIARY = 2,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			UCOL_DEFAULT_STRENGTH = UCOL_TERTIARY,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			UCOL_CE_STRENGTH_LIMIT,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			UCOL_QUATERNARY = 3,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			UCOL_IDENTICAL = 15,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			UCOL_STRENGTH_LIMIT,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			UCOL_OFF = 16,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			UCOL_ON = 17,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			UCOL_SHIFTED = 20,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			UCOL_NON_IGNORABLE = 21,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			UCOL_LOWER_FIRST = 24,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			UCOL_UPPER_FIRST = 25,
			/// <summary>http://oss.software.ibm.com/icu/apiref/uchar_8h-source.html</summary>
			UCOL_ATTRIBUTE_VALUE_COUNT
		}

		///<summary>
		/// enumerated Unicode general category types.
		/// See http://www.unicode.org/Public/UNIDATA/UnicodeData.html .
		/// ///</summary>
		public enum UCharCategory
		{
			///<summary>Non-category for unassigned and non-character code points.</summary>
			U_UNASSIGNED = 0,
			///<summary>Cn "Other, Not Assigned (no characters in [UnicodeData.txt] have this property)" (same as U_UNASSIGNED!)</summary>
			U_GENERAL_OTHER_TYPES = 0,
			///<summary>Lu</summary>
			U_UPPERCASE_LETTER = 1,
			///<summary>Ll</summary>
			U_LOWERCASE_LETTER = 2,
			///<summary>Lt</summary>
			U_TITLECASE_LETTER = 3,
			///<summary>Lm</summary>
			U_MODIFIER_LETTER = 4,
			///<summary>Lo</summary>
			U_OTHER_LETTER = 5,
			///<summary>Mn</summary>
			U_NON_SPACING_MARK = 6,
			///<summary>Me</summary>
			U_ENCLOSING_MARK = 7,
			///<summary>Mc</summary>
			U_COMBINING_SPACING_MARK = 8,
			///<summary>Nd</summary>
			U_DECIMAL_DIGIT_NUMBER = 9,
			///<summary>Nl</summary>
			U_LETTER_NUMBER = 10,
			///<summary>No</summary>
			U_OTHER_NUMBER = 11,
			///<summary>Zs</summary>
			U_SPACE_SEPARATOR = 12,
			///<summary>Zl</summary>
			U_LINE_SEPARATOR = 13,
			///<summary>Zp</summary>
			U_PARAGRAPH_SEPARATOR = 14,
			///<summary>Cc</summary>
			U_CONTROL_CHAR = 15,
			///<summary>Cf</summary>
			U_FORMAT_CHAR = 16,
			///<summary>Co</summary>
			U_PRIVATE_USE_CHAR = 17,
			///<summary>Cs</summary>
			U_SURROGATE = 18,
			///<summary>Pd</summary>
			U_DASH_PUNCTUATION = 19,
			///<summary>Ps</summary>
			U_START_PUNCTUATION = 20,
			///<summary>Pe</summary>
			U_END_PUNCTUATION = 21,
			///<summary>Pc</summary>
			U_CONNECTOR_PUNCTUATION = 22,
			///<summary>Po</summary>
			U_OTHER_PUNCTUATION = 23,
			///<summary>Sm</summary>
			U_MATH_SYMBOL = 24,
			///<summary>Sc</summary>
			U_CURRENCY_SYMBOL = 25,
			///<summary>Sk</summary>
			U_MODIFIER_SYMBOL = 26,
			///<summary>So</summary>
			U_OTHER_SYMBOL = 27,
			///<summary>Pi</summary>
			U_INITIAL_PUNCTUATION = 28,
			///<summary>Pf</summary>
			U_FINAL_PUNCTUATION = 29,
			///<summary>One higher than the last enum UCharCategory constant.</summary>
			U_CHAR_CATEGORY_COUNT
		}

		/// <summary>
		/// BIDI direction constants
		/// </summary>
		public enum UCharDirection
		{
			/// <summary></summary>
			U_LEFT_TO_RIGHT = 0,
			/// <summary></summary>
			U_RIGHT_TO_LEFT = 1,
			/// <summary></summary>
			U_EUROPEAN_NUMBER = 2,
			/// <summary></summary>
			U_EUROPEAN_NUMBER_SEPARATOR = 3,
			/// <summary></summary>
			U_EUROPEAN_NUMBER_TERMINATOR = 4,
			/// <summary></summary>
			U_ARABIC_NUMBER = 5,
			/// <summary></summary>
			U_COMMON_NUMBER_SEPARATOR = 6,
			/// <summary></summary>
			U_BLOCK_SEPARATOR = 7,
			/// <summary></summary>
			U_SEGMENT_SEPARATOR = 8,
			/// <summary></summary>
			U_WHITE_SPACE_NEUTRAL = 9,
			/// <summary></summary>
			U_OTHER_NEUTRAL = 10,
			/// <summary></summary>
			U_LEFT_TO_RIGHT_EMBEDDING = 11,
			/// <summary></summary>
			U_LEFT_TO_RIGHT_OVERRIDE = 12,
			/// <summary></summary>
			U_RIGHT_TO_LEFT_ARABIC = 13,
			/// <summary></summary>
			U_RIGHT_TO_LEFT_EMBEDDING = 14,
			/// <summary></summary>
			U_RIGHT_TO_LEFT_OVERRIDE = 15,
			/// <summary></summary>
			U_POP_DIRECTIONAL_FORMAT = 16,
			/// <summary></summary>
			U_DIR_NON_SPACING_MARK = 17,
			/// <summary></summary>
			U_BOUNDARY_NEUTRAL = 18,
			/// <summary></summary>
			U_FIRST_STRONG_ISOLATE = 19,
			/// <summary></summary>
			U_LEFT_TO_RIGHT_ISOLATE = 20,
			/// <summary></summary>
			U_RIGHT_TO_LEFT_ISOLATE = 21,
			/// <summary></summary>
			U_POP_DIRECTIONAL_ISOLATE = 22,
			/// <summary></summary>
			U_CHAR_DIRECTION_COUNT
		}
	}
	// JT notes on how to pass arguments
	// const char *: pass string.


	/// <summary>
	/// General ICU exception
	/// </summary>
	public class IcuException : Exception
	{
		private readonly Icu.UErrorCode m_errorCode;

		/// <summary>
		/// Initializes a new instance of the <see cref="IcuException"/> class.
		/// </summary>
		/// <param name="message">The message.</param>
		/// <param name="errorCode">The error code.</param>
		public IcuException(string message, Icu.UErrorCode errorCode)
			: base(message)
		{
			m_errorCode = errorCode;
		}

		/// <summary>
		/// Gets the ICU error code.
		/// </summary>
		public Icu.UErrorCode ErrorCode
		{
			get { return m_errorCode; }
		}
	}

	/// ------------------------------------------------------------------------------------
	/// <summary>
	/// Simple class to allow passing collation error info back to the caller of CheckRules.
	/// </summary>
	/// ------------------------------------------------------------------------------------
	public class CollationRuleErrorInfo
	{
		internal CollationRuleErrorInfo(Icu.UParseError parseError)
		{
			Line = parseError.line + 1;
			Offset = parseError.offset + 1;
			PreContext = parseError.preContext;
			PostContext = parseError.postContext;
		}

		/// <summary>Line number (1-based) containing the error</summary>
		public int Line { get; private set; }
		/// <summary>Character offset (1-based) on Line where the error was detected</summary>
		public int Offset { get; private set; }
		/// <summary>Characters preceding the the error</summary>
		public String PreContext { get; private set; }
		/// <summary>Characters following the the error</summary>
		public String PostContext { get; private set; }
	}

	/// <summary>
	/// Collation rule exception
	/// </summary>
	public class CollationRuleException : IcuException
	{
		private readonly CollationRuleErrorInfo m_errorInfo;

		/// <summary>
		/// Initializes a new instance of the <see cref="CollationRuleException"/> class.
		/// </summary>
		/// <param name="errorCode">The ICU error code.</param>
		/// <param name="errorInfo">The error information.</param>
		public CollationRuleException(Icu.UErrorCode errorCode, CollationRuleErrorInfo errorInfo)
			: base("Failed to parse collation rules.", errorCode)
		{
			m_errorInfo = errorInfo;
		}

		/// <summary>
		/// Gets the parse error information.
		/// </summary>
		public CollationRuleErrorInfo ErrorInfo
		{
			get { return m_errorInfo; }
		}
	}
}
