// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Icu;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Utils;
using SIL.PlatformUtilities;

namespace SIL.LCModel.Core.SpellChecking
{
	/// <summary>
	/// This class manages a dictionary of (currently) Hunspell objects so that we can do spell checking.
	/// </summary>
	public static class SpellingHelper
	{
		// A helper object used to ensure that the spelling engines are properly disposed of
		private sealed class SingletonToDispose : IDisposable
		{
			~SingletonToDispose()
			{
				Dispose(false);
			}

			public void Dispose()
			{
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			private void Dispose(bool disposing)
			{
				Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
				if (disposing)
					ClearAllDictionaries();
			}

		}

		static SpellingHelper()
		{
			// The SingletonToDispose will be disposed during system shutdown.
			// This will clear m_spellers and properly dispose of all the engines (and the C++ memory they allocate).
			SingletonsContainer.Add(new SingletonToDispose());
		}

		private static Dictionary<string, SpellEngine> m_spellers = new Dictionary<string, SpellEngine>();

		/// <summary>
		/// Get a spelling engine (or null) appropriate to the particular ID.
		/// </summary>
		/// <param name="dictId"></param>
		/// <returns></returns>
		public static ISpellEngine GetSpellChecker(string dictId)
		{
			SpellEngine result;
			if (m_spellers.TryGetValue(dictId, out result))
				return result;
			result = RawGetSpellChecker(dictId);
			if (result != null)
			{
				// Found exactly matching set of files. Remember and use.
				m_spellers[dictId] = result;
				return result;
			}
			// If no dictionary exists which matches the language name exactly then
			// search for one. Currently any file %appdata%/hunspell/X.dic indicates the existence of a
			// dictionary we can use. If one of these starts with the desired ID, use it...unless it is a private one
			// (created by us from a wordform inventory); these must be used only if they match exactly.
			// Enhance: also search OO dictionaries, maybe Firefox?
			foreach (var path in Directory.GetFiles(GetSpellingDirectoryPath(), dictId + "*.dic"))
			{
				if (IsValidDictionary(path))
				{
					var changedId = DictIdFromPath(path);
					if (!m_spellers.TryGetValue(changedId, out result))
					{
						result = RawGetSpellChecker(changedId);
						m_spellers[changedId] = result; // In case we are asked for one for that exact ID, use it.
					}
					if (result.IsVernacular)
						continue; // it's a private dictionary, we must not use it for dictId.
					m_spellers[dictId] = result; // use it!
					return result;
				}
			}
			//
			// We may also want to try the dictionaries found in Open Office.
			// C:\Program Files\OpenOffice.org 2.4\share\dict\ooo
			// or more likely something similar for OO 3/4

			return null;
		}

		/// <summary>
		/// Return the files that should be backed up for the given dictionary.
		/// For now this is just the exc file, if it exists, and if the dictionary is not vernacular.
		/// (We assume any major language dictionary can be recovered from somewhere, and a vernacular
		/// one can be rebuilt from the WFI.)
		/// </summary>
		/// <returns></returns>
		public static IEnumerable<string> PathsToBackup(string dictId)
		{
			var dict = GetSpellChecker(dictId);
			if (dict.IsVernacular)
				return new string[0];
			var exceptionPath = ((SpellEngine) dict).ExceptionPath;
			if (File.Exists(exceptionPath))
				return new string[] {exceptionPath};
			return new string[0];
		}

		/// <summary>
		/// Return true iff GetSpellChecker will return a non-null value.
		/// </summary>
		/// <param name="ws"></param>
		/// <param name="wsf"></param>
		/// <returns></returns>
		public static bool DictionaryExists(int ws, ILgWritingSystemFactory wsf)
		{
			// Enhance JohnT: there may be a way we can implement without actually creating it. Would this have benefits?
			return GetSpellChecker(ws, wsf) != null;
		}

		/// <summary>
		/// Return true iff GetSpellChecker will return a non-null value.
		/// </summary>
		/// <param name="dictId"></param>
		/// <returns></returns>
		public static bool DictionaryExists(string dictId)
		{
			// Enhance JohnT: there may be a way we can implement without actually creating it. Would this have benefits?
			return GetSpellChecker(dictId) != null;
		}

		/// <summary>
		/// Used in restoring backups, copy the specified file, typically an exc, to the hunspell directory
		/// </summary>
		/// <remarks>
		/// Will not overwrite any existing files.
		/// Exceptions for vernacular dictionaries are wiped out when the dictionary is initialized.
		/// </remarks>
		public static void AddAnySpellingExceptionsFromBackup(string backupPath)
		{
			AddAnySpellingExceptionsFromBackup(backupPath, GetSpellingDirectoryPath());
		}

		/// <summary/>
		internal static void AddAnySpellingExceptionsFromBackup(string backupDictionaryPath, string destinationPath)
		{
			foreach(var file in Directory.GetFiles(backupDictionaryPath))
			{
				var destFilePath = Path.Combine(destinationPath, Path.GetFileName(file));
				if(Path.GetExtension(file) == ".dic")
				{
					// For Hunspell, we only backup exc files.
					// If a backup contains a dic file, it must be from Enchant.
					// There is a possibility that there were exceptions there which should be copied
					// but some enchant backups also containt a .exc file with exceptions.
					continue;
				}
				// Copy to the hunspell directory, but do not overwrite.
				// Currently these exceptions are only potentially useful for analysis language dictionaries - Jul 2014
				if(!File.Exists(destFilePath))
				{
					File.Copy(file, destFilePath);
				}
			}
		}

		private static bool IsValidDictionary(string path)
		{
			return File.Exists(Path.ChangeExtension(path, "aff"));
		}

		/// <summary>
		/// Get all the IDs that will produce dictionaries (without any tricks like prefix matching).
		/// </summary>
		/// <returns></returns>
		public static IEnumerable<string> GetDictionaryIds()
		{
			return from path in Directory.GetFiles(GetSpellingDirectoryPath(), "*.dic")
				where IsValidDictionary(path)
				select DictIdFromPath(path);
		}

		private static string DictIdFromPath(string path)
		{
			return Path.ChangeExtension(Path.GetFileName(path), null);
		}

		/// <summary>
		/// Get a dictionary for the specified writing system, or null if we don't know of one.
		/// We ideally want a dictionary that exactly matches the specified writing system, that is,
		/// the file name for the .dic file == the SpellCheckDictionary of the writing system.
		/// If we can't find such a dictionary, for major languages (those we didn't create from wordform inventory),
		/// we will return a dictionary that shares a prefix, for example, 'en' when looking for 'en_US' or vice versa.
		/// This is not allowed for vernacular languages (where the dictionary is one we created ourselves); we return null if we can't find
		/// an exact match or an approximate match that is a 'major' language dictionary.
		/// </summary>
		public static ISpellEngine GetSpellChecker(int ws, ILgWritingSystemFactory wsf)
		{
			string dictId = DictionaryId(ws, wsf);
			if (dictId == null)
				return null;
			return GetSpellChecker(dictId);
		}

		private static SpellEngine RawGetSpellChecker(string dictId)
		{
			SpellEngine result = null;
			var rootDir = GetSpellingDirectoryPath();
			var dictPath = GetShortName(Path.Combine(rootDir, Path.ChangeExtension(dictId, "dic")));
			var affixPath = GetShortName(Path.Combine(rootDir, Path.ChangeExtension(dictId, "aff")));
			var exceptionPath = GetExceptionFileName(dictPath);
			if (File.Exists(dictPath) && File.Exists(affixPath))
			{
				try
				{
					result = SpellEngine.Create(affixPath, dictPath, exceptionPath);
				}
				catch (Exception)
				{
					result?.Dispose();
					throw;
				}
			}
			// Enhance JohnT: may want to look in OO and/or Firefox directory.
			return result;
		}

		private static string m_SpellingDirectoryPath;

		/// <summary>
		/// Locates the directory in which we look for and create hunspell dictionaries.
		/// </summary>
		internal static string GetSpellingDirectoryPath()
		{
			if (m_SpellingDirectoryPath == null)
			{
				var appdataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

				// When running multiple builds in parallel we have to use separate directories for
				// each build, otherwise some unit tests might fail.
				var buildagentSubdir = Environment.GetEnvironmentVariable("BUILDAGENT_SUBKEY");
				if (!string.IsNullOrEmpty(buildagentSubdir))
					appdataFolder = Path.Combine(appdataFolder, buildagentSubdir);

				m_SpellingDirectoryPath = Path.Combine(appdataFolder, "hunspell");
				if (!Directory.Exists(m_SpellingDirectoryPath))
					Directory.CreateDirectory(m_SpellingDirectoryPath);
			}
			return m_SpellingDirectoryPath;
		}

		/// <summary>
		/// Make sure that a dictionary exists for the specified writing system.
		/// Currently this will NOT do so if its spelling ID is set to None (in angle brackets).
		/// Callers may want to include code like this:
		/// 			    var wsObj = Cache.ServiceLocator.WritingSystems.DefaultVernacularWritingSystem;
		///        if (wsObj.SpellCheckingId == "None") // add angle brackets around None
		///           wsObj.SpellCheckingId = wsObj.Id.Replace('-', '_');
		/// Enhance JohnT: maybe code like that should be part of this method? But it is meant to
		/// just make sure the dictionary exists, once the right SpellCheckingId has been established.
		/// </summary>
		public static ISpellEngine EnsureDictionary(int ws, ILgWritingSystemFactory wsf)
		{
			string dictId = RawDictionaryId(ws, wsf);
			if (dictId == null)
				return null; // No Dictionary ID set. Caller has probably messed up, but we can't fix it here.
			EnsureDictionary(dictId);
			// Now it should exist!
			return GetSpellChecker(dictId);
		}

		/// <summary>
		/// For testing (so far), clear all dictionaries so we can make a new one and verify persistence.
		/// </summary>
		internal static void ClearAllDictionaries()
		{
			foreach (var kvp in m_spellers)
				kvp.Value.Dispose();
			m_spellers.Clear();
		}



		/// <summary>
		/// This is a word we will falsely claim is correct, so it should be one that is very unlikely
		/// to occur. It's purpose is to serve as a prototype for the /C flags, so that it can be used
		/// to indicate that other words should be keep-case also.
		/// </summary>
		internal const string PrototypeWord = "XXPatternWordDoNotDeleteXX";
		private const string keepCaseFlag = "C";

		internal static void EnsureDictionary(string dictId)
		{
			string dirPath = GetSpellingDirectoryPath();
			if (!Directory.Exists(dirPath))
				Directory.CreateDirectory(dirPath);
			string dicPath = GetDicPath(dirPath, dictId);
			// if the dictionary already exists, and it is not vernacular, return rather than wiping it.
			if(File.Exists(dicPath))
			{
				var dict = GetSpellChecker(dictId);
				if(dict != null && !dict.IsVernacular)
					return;
			}
			InitDictionary(dicPath, new string[0]);
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		static extern uint GetShortPathName(string lpszLongPath, char[] lpszShortPath, int cchBuffer);

		private static string GetShortName(string input)
		{
			if (Platform.IsWindows)
			{
				if (!File.Exists(input))
					return input; // can only convert real files, hope for the best on others.
				char[] buffer = new char[270];
				uint chars = GetShortPathName(input, buffer, 270);
				return new string(buffer).Substring(0, (int) chars);
			}
			return input;
		}

		/// <summary>
		/// The file name where we store exceptions is unfortunately the short-file-name version of the dictionary.
		/// We can't easily change this, so that is what we have to deal with any time we care about it.
		/// </summary>
		/// <param name="dictFileName"></param>
		/// <returns></returns>
		public static string GetExceptionFileName(string dictFileName)
		{
			// The order here is important. Since the exception file might not exist, we need to get the short name of
			// the dictionary itself, which hopefully does, and change its extension. Changing the extension first
			// will produce the name of a possibly non-existent file, for which we can't get a short name.
			return Path.ChangeExtension(GetShortName(dictFileName), ".exc");
		}

		private static void InitDictionary(string dicPath, IEnumerable<string> words)
		{

			var affixFile = Path.ChangeExtension(dicPath, ".aff");
			if(!File.Exists(affixFile))
			{
				using(var writer = FileUtils.OpenFileForWrite(affixFile, Encoding.UTF8))
				{
					writer.WriteLine("SET UTF-8");
					// Enhance JohnT: may be helpful to write TRY followed by the word-forming and possibly punctuation
					// characters of the language. This somehow affects the suggestions, but I haven't figured out how yet.
					writer.WriteLine("KEEPCASE " + keepCaseFlag);
				}
			}
			// If it already exists, probably we disabled it by deleting the .aff file--an approach we
			// no longer use; re-creating it should reinstate it.
			using (var writer = FileUtils.OpenFileForWrite(dicPath, Encoding.UTF8))
			{
				// This is a size of hash table to allocate, NOT the exact number of words in the dictionary.
				// In particular it must NOT be zero or Hunspell will malfunction (divide by zero).
				// However, making it equal the number of words helps Hunspell allocate a good size of hashtable.
				writer.WriteLine(Math.Max(10, words.Count()).ToString());
				writer.WriteLine(PrototypeWord + "/" + keepCaseFlag);
				foreach (var word in words)
					writer.WriteLine(Normalizer.Normalize(word, Normalizer.UNormalizationMode.UNORM_NFC));
			}
		}

		/// <summary>
		/// Return the string which should be used to request a dictionary for the specified writing system,
		/// or null if none will work.
		/// </summary>
		/// <param name="ws"></param>
		/// <param name="wsf"></param>
		/// <returns></returns>
		public static string DictionaryId(int ws, ILgWritingSystemFactory wsf)
		{
			string wsId = RawDictionaryId(ws, wsf);
			return DictionaryId(wsId);
		}

		private static string DictionaryId(string dictId)
		{
			if (String.IsNullOrEmpty(dictId) || dictId == @"<None>")
				return null; // nothing to search for, or we know we don't want one for this WS.
			return dictId; // no change needed
		}

		/// <summary>
		/// Get the path for the dictionary for a particular locale, if it is one of our private ones,
		/// given the path to the directory where we make them and the icuLocale.
		/// </summary>
		/// <param name="dirPath"></param>
		/// <param name="icuLocale"></param>
		/// <returns></returns>
		internal static string GetDicPath(string dirPath, string icuLocale)
		{
			string filePath = Path.Combine(dirPath, icuLocale);
			return Path.ChangeExtension(filePath, ".dic");
		}

		// The raw id that should be used to create a dictionary for the given WS, if none exists.
		private static string RawDictionaryId(int ws, ILgWritingSystemFactory wsf)
		{
			ILgWritingSystem wsEngine = wsf.get_EngineOrNull(ws);
			if (wsEngine == null)
				return null;
			string wsId = wsEngine.SpellCheckingId;
			if (String.IsNullOrEmpty(wsId))
			{
				// Our old spelling engine, Enchant, did not allow hyphen;
				// keeping that rule in case we switch again or there is some other good reason for it that we don't know.
				// Changing to underscore is OK since lang ID does not allow underscore.
				return wsEngine.Id.Replace('-', '_');
			}
			if (wsId == "<None>")
				return null;
			return wsId;
		}

		/// <summary>
		/// Ensure that the spelling dictionary (if any) for the specified ws will give the specified
		/// answer regarding the specified word.
		/// </summary>
		public static void SetSpellingStatus(string word, int ws,
			ILgWritingSystemFactory wsf, bool fCorrect)
		{
			var dict = GetSpellChecker(ws, wsf);
			if (dict == null)
				return; // paranoia
			dict.SetStatus(word, fCorrect);
		}

		/// <summary>
		/// Reset the whole dictionary for this ID. Henceforth it will contain exactly the words passed.
		/// Note that this will not affect any existing ISpellEngine; try not to have any left around.
		/// </summary>
		/// <param name="id"></param>
		/// <param name="words"></param>
		public static void ResetDictionary(string id, IEnumerable<string> words)
		{
			var dict = GetSpellChecker(id);
			if (dict != null)
			{
				if(!dict.IsVernacular)
				{
					// If the discovered dictionary is not one of our vernaculars we will not reset it or crash (LT-15285)
					return;
				}
				foreach (var kvp in m_spellers.ToList())
				{
					if (kvp.Value == dict)
						m_spellers.Remove(kvp.Key);
				}
				((SpellEngine) dict).Dispose();
			}
			var sorted = new List<string>(words);
			sorted.Sort((x, y) => string.Compare(x, y, StringComparison.Ordinal));
			var dicPath = GetDicPath(GetSpellingDirectoryPath(), id);
			File.Delete(GetExceptionFileName(dicPath)); // get rid of any obsolete exceptions
			InitDictionary(dicPath, sorted);
			m_spellers.Remove(id); // make a new one when next asked.
		}

		private static IGetSpellChecker s_checker;

		/// <summary>
		/// Get an implementation of this interface (typically to pass to Views).
		/// </summary>
		public static IGetSpellChecker GetCheckerInstance
		{
			get
			{
				if(s_checker == null)
					s_checker = new GetSpellChecker();
				return s_checker;
			}
		}

		/// <summary>
		/// A dictionary is considered vernacular if it contains our special word. That is, we presume it is one
		/// we created and can rewrite if we choose; and it should not be used for any dictionary ID that is
		/// not an exact match.
		/// </summary>
		public static bool IsVernacular(string dictId)
		{
			return GetSpellChecker(dictId).IsVernacular;
		}
	}

	internal class GetSpellChecker : IGetSpellChecker
	{
		public ICheckWord GetChecker(string dictId)
		{
			return SpellingHelper.GetSpellChecker(dictId);
		}
	}
}
