// Copyright (c) 2003-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Icu;
using Icu.Normalization;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Utils;

namespace SIL.LCModel.Core.Text
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Wrapper for ICU methods
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public static class CustomIcu
	{
		/// <summary>
		/// The ICU major version
		/// </summary>
		public const string Version = "54";

		private const string IcuucDllName = "icuuc" + Version + ".dll";

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
				var executingAssemblyFolder = Uri.UnescapeDataString(new UriBuilder(Assembly.GetExecutingAssembly().CodeBase).Path);
				// ReSharper disable once AssignNullToNotNullAttribute -- If FlexExe returns null we have bigger problems
				var icuPath = Path.Combine(Path.GetDirectoryName(executingAssemblyFolder), "lib", $"win-{arch}");
				// Append icu dll location to PATH, such as .../lib/x64, to help C# and C++ code find icu.
				Environment.SetEnvironmentVariable("PATH",
					icuPath + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
			}

			var dataDirectory = Wrapper.DataDirectory;
			if (string.IsNullOrEmpty(dataDirectory) || dataDirectory == "." + Path.DirectorySeparatorChar)
			{
				dataDirectory = DefaultDataDirectory;
				Wrapper.DataDirectory = dataDirectory;
			}
			else if (!Path.IsPathRooted(dataDirectory))
			{
				dataDirectory = Path.Combine(Environment.CurrentDirectory, dataDirectory);
				Wrapper.DataDirectory = dataDirectory;
			}

			// ICU docs say to do this after the directory is set, but before others are called.
			// And it can be called n times with little hit.
			Wrapper.Init();

			string overrideDataPath = Path.Combine(dataDirectory, "UnicodeDataOverrides.txt");
			if (!File.Exists(overrideDataPath))
			{
				// See if we can get the 'original' one in the data directory.
				overrideDataPath = Path.Combine(dataDirectory, Path.Combine("data", "UnicodeDataOverrides.txt"));
			}

			try
			{
				HaveCustomIcuLibrary = SilIcuInit(overrideDataPath);
			}
			catch (DllNotFoundException)
			{
				// we don't have a custom ICU installed
				HaveCustomIcuLibrary = false;
			}
			catch (BadImageFormatException)
			{
				// we found a custom ICU but with an incorrect format (e.g. x64 instead of x86)
				HaveCustomIcuLibrary = false;
			}

			if (!HaveCustomIcuLibrary)
			{
				Debug.WriteLine("SilIcuInit returned false. It was trying to load from " + overrideDataPath + ". The file " +
								(File.Exists(overrideDataPath) ? "exists." : "does not exist."));
				Debug.WriteLine("Falling back to default ICU");
				return;
			}

			var version = int.Parse(Version);
			Wrapper.ConfineIcuVersions(version, version);
		}

		public static bool HaveCustomIcuLibrary { get; private set; }

		#endregion

		#region ICU methods that are not exposed directly

		/// <summary>SIL-specific initialization. Note that we do not currently define the kIcuVersion extension for this method.</summary>
		[DllImport(IcuucDllName, EntryPoint = "SilIcuInit",
			 CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		private static extern bool SilIcuInit(
			[MarshalAs(UnmanagedType.LPStr)]string pathname);

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
		/// Gets the decomposition type information of the given character.
		/// </summary>
		/// <param name="characterCode">The character code.</param>
		/// ------------------------------------------------------------------------------------
		public static UcdProperty GetDecompositionTypeInfo(int characterCode)
		{
			return UcdProperty.GetInstance(Character.GetIntPropertyValue(characterCode, Character.UProperty.DECOMPOSITION_TYPE));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the numeric type information of the given character.
		/// </summary>
		/// <param name="characterCode">The character code.</param>
		/// ------------------------------------------------------------------------------------
		public static UcdProperty GetNumericTypeInfo(int characterCode)
		{
			return UcdProperty.GetInstance(Character.GetIntPropertyValue(characterCode, Character.UProperty.NUMERIC_TYPE));
		}

		/// <summary>
		/// Gets the general category information of the specified character.
		/// </summary>
		public static UcdProperty GetGeneralCategoryInfo(int characterCode)
		{
			return UcdProperty.GetInstance(Character.GetCharType(characterCode));
		}

		/// <summary>
		/// Gets the bidi class information of the specified character.
		/// </summary>
		public static UcdProperty GetBidiClassInfo(int characterCode)
		{
			return UcdProperty.GetInstance(Character.CharDirection(characterCode));
		}

		/// <summary>
		/// Gets the combining class information of the specified character.
		/// </summary>
		public static UcdProperty GetCombiningClassInfo(int characterCode)
		{
			var normalizer = GetIcuNormalizer(FwNormalizationMode.knmNFC);
			return UcdProperty.GetInstance(normalizer.GetCombiningClass(characterCode));
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
			using (var rbLangDef = new ResourceBundle(null, icuLocale))
			{
				// If the locale of the resource bundle doesn't match the LocaleAbbr,
				// it loaded something else as a default (e.g. "en").
				// In that case we don't want to use the resource bundle, so ignore it.
				if (rbLangDef.Name != icuLocale)
					return string.Empty;
				return rbLangDef.GetStringByKey("ExemplarCharacters");
			}
		}

		#endregion

		/// <summary>
		/// Get an opaque pointer to the CustomIcu normalizer object for a given mode (NFC, NFD, etc.)
		/// Used in several parts of the TsString normalization code.
		/// </summary>
		public static Normalizer2 GetIcuNormalizer(FwNormalizationMode normalizationMode)
		{
			string name;
			Normalizer2.Mode mode;
			switch (normalizationMode)
			{
				case FwNormalizationMode.knmNFC:
				case FwNormalizationMode.knmNFSC:
					name = HaveCustomIcuLibrary ? "nfc_fw" : "nfc";
					mode = Normalizer2.Mode.COMPOSE;
					break;

				case FwNormalizationMode.knmNFD:
					name = HaveCustomIcuLibrary ? "nfc_fw" : "nfc";
					mode = Normalizer2.Mode.DECOMPOSE;
					break;

				case FwNormalizationMode.knmNFKC:
					name = HaveCustomIcuLibrary ? "nfkc_fw" : "nfkc";
					mode = Normalizer2.Mode.COMPOSE;
					break;

				case FwNormalizationMode.knmNFKD:
					name = HaveCustomIcuLibrary ? "nfkc_fw" : "nfkc";
					mode = Normalizer2.Mode.DECOMPOSE;
					break;

				case FwNormalizationMode.knmFCD:
					name = HaveCustomIcuLibrary ? "nfc_fw" : "nfc";
					mode = Normalizer2.Mode.FCD;
					break;

				default:
					throw new NotImplementedException("Unimplemented value for FwNormalizationMode");
			}

			return Normalizer2.GetInstance(null, name, mode);
		}

		/// <summary>
		/// Gets the decomposition of the specified character.
		/// </summary>
		public static string GetDecomposition(int ch)
		{
			var normalizer = GetIcuNormalizer(FwNormalizationMode.knmNFC); // doesn't matter which one we use here
			return normalizer.GetDecomposition(ch) ?? char.ConvertFromUtf32(ch);
		}
	}
}
