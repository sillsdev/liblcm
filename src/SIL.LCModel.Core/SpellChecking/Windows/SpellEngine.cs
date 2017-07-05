// Copyright (c) 2015-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Utils;
using NHunspell;

namespace SIL.LCModel.Core.SpellChecking
{
	internal class SpellEngine : ISpellEngine, IDisposable
	{
		/// <summary>
		/// File of exceptions (for a vernacular dictionary, may be the whole dictionary).
		/// Words are added by appearing, each on a line
		/// Words beginning * are known bad words.
		/// </summary>
		internal string ExceptionPath { get; set; }
		private Hunspell m_hunspellHandle;
		
		internal SpellEngine(string affixPath, string dictPath, string exceptionPath)
		{
			try
			{
				ExceptionPath = exceptionPath;
				m_hunspellHandle = new Hunspell(affixPath, dictPath);
				if (File.Exists(ExceptionPath))
				{
					using (var reader = new StreamReader(ExceptionPath, Encoding.UTF8))
					{
						string line;
						while ((line = reader.ReadLine()) != null)
						{
							var item = line;
							bool correct = true;
							if (item.Length > 0 && item[0] == '*')
							{
								correct = false;
								item = item.Substring(1);
							}
							SetInternalStatus(item, correct);
						}
					}
				}
			}
			catch (Exception e)
			{
				Debug.WriteLine("Initializing Hunspell: {0} exception: {1} ", e.GetType(), e.Message);
				if (m_hunspellHandle != null)
				{
					m_hunspellHandle.Dispose();
					throw;
				}
			}

		}

		public bool Check(string word)
		{
			return m_hunspellHandle.Spell(MarshallAsUtf8Bytes(word));
		}

		/// <summary>
		/// We can't declare these arguments (char * in C++) as [MarshalAs(UnmanagedType.LPStr)] string, because that
		/// unconditionally coverts the string to bytes using the current system code page, which is never what we want.
		/// So we declare them as byte[] and marshal like this. The C++ code requires null termination so add a null
		/// before converting. (This doesn't seem to be necessary, but better safe than sorry.)
		/// </summary>
		/// <param name="word"></param>
		/// <returns></returns>
		private static string MarshallAsUtf8Bytes(string word)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(Icu.Normalize(word, Icu.UNormalizationMode.UNORM_NFC) + "\0");
			return Encoding.UTF8.GetString(bytes);
		}

		private bool m_isVernacular;
		private bool m_fGotIsVernacular;
		/// <summary>
		/// A dictionary is considered vernacular if it contains our special word. That is, we presume it is one
		/// we created and can rewrite if we choose; and it should not be used for any dictionary ID that is
		/// not an exact match.
		/// </summary>
		public bool IsVernacular
		{
			get
			{
				if (!m_fGotIsVernacular)
				{
					m_isVernacular = Check(MarshallAsUtf8Bytes(SpellingHelper.PrototypeWord));
					m_fGotIsVernacular = true;
				}
				return m_isVernacular;
			}
		}

		/// <summary>
		/// Get a list of suggestions for alternate words to use in place of the mis-spelled one.
		/// </summary>
		/// <param name="badWord"></param>
		/// <returns></returns>
		public ICollection<string> Suggest(string badWord)
		{
			List<string> suggestions = m_hunspellHandle.Suggest(MarshallAsUtf8Bytes(badWord));
			int resultCount = suggestions.Count;
			if (resultCount == 0)
				return new string[0];

			var results = suggestions;
			return results;
		}

		public void SetStatus(string word1, bool isCorrect)
		{
			var word = Icu.Normalize(word1, Icu.UNormalizationMode.UNORM_NFC);
			if (Check(word) == isCorrect)
				return; // nothing to do.
			// Review: any IO exceptions we should handle? How??
			SetInternalStatus(word, isCorrect);
			var builder = new StringBuilder();
			bool insertedLineForWord = false;
			if (File.Exists(ExceptionPath))
			{
				using (var reader = new StreamReader(ExceptionPath, Encoding.UTF8))
				{
					string line;
					while ((line = reader.ReadLine()) != null)
					{
						var item = line;
						if (item.Length > 0 && item[0] == '*')
							item = item.Substring(1);
						// If we already got it, or the current line is before the word, just copy the line to the output.
						if (insertedLineForWord || string.Compare(item, word, StringComparison.Ordinal) < 0)
						{
							builder.AppendLine(line);
							continue;
						}
						// We've come to the right place to insert our word.
						if (!isCorrect)
							builder.Append("*");
						builder.AppendLine(word);
						insertedLineForWord = true;
						if (word != item) // then current line must be a pre-existing word that comes after ours.
							builder.AppendLine(line); // so add it in after item
					}
				}
			}
			if (!insertedLineForWord) // no input file, or the word comes after any existing one
			{
				// The very first exception!
				if (!isCorrect)
					builder.Append("*");
				builder.AppendLine(word);
			}
			// Write the new file over the old one.
			File.WriteAllText(ExceptionPath, builder.ToString(), Encoding.UTF8);
		}

		private void SetInternalStatus(string word, bool isCorrect)
		{
			if (isCorrect)
			{
				if (IsVernacular)
				{
					// Custom vernacular-only dictionary.
					// want it 'affixed' like the prototype, which has been marked to suppress other-case matches
					m_hunspellHandle.AddWithAffix(MarshallAsUtf8Bytes(word), MarshallAsUtf8Bytes(SpellingHelper.PrototypeWord));
				}
				else
				{
					// not our custom dictionary, some majority language, we can't (and probably don't want)
					// to be restrictive about case.
					m_hunspellHandle.Add(MarshallAsUtf8Bytes(word));
				}
			}
			else
			{
				m_hunspellHandle.Remove(MarshallAsUtf8Bytes(word));
			}
		}

		~SpellEngine()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			// This object will be cleaned up by the Dispose method.
			// Therefore, you should call GC.SupressFinalize to
			// take this object off the finalization queue
			// and prevent finalization code for this object
			// from executing a second time.
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
			m_hunspellHandle?.Dispose();
		}
	}
}
