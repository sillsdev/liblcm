// Copyright (c) 2015-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Icu;
using SIL.PlatformUtilities;

namespace SIL.LCModel.Core.SpellChecking
{
	internal abstract class SpellEngine : ISpellEngine, IDisposable
	{
		/// <summary>
		/// File of exceptions (for a vernacular dictionary, may be the whole dictionary).
		/// Words are added by appearing, each on a line
		/// Words beginning * are known bad words.
		/// </summary>
		internal string ExceptionPath { get; set; }

		internal static SpellEngine Create(string affixPath, string dictPath, string exceptionPath)
		{
			SpellEngine spellEngine = null;
			try
			{
				if (Platform.IsWindows)
					spellEngine = CreateSpellEngineWindows(affixPath, dictPath, exceptionPath);
				else
					spellEngine = CreateSpellEngineLinux(affixPath, dictPath, exceptionPath);

				spellEngine.Initialize();
			}
			catch (Exception e)
			{
				Debug.WriteLine("Initializing Hunspell: {0} exception: {1} ", e.GetType(), e.Message);
				spellEngine?.Dispose();
				throw;
			}

			return spellEngine;
		}

		private static SpellEngine CreateSpellEngineWindows(string affixPath, string dictPath,
			string exceptionPath)
		{
			// Separate method so that we don't try to instantiate the class when running on Linux
			return new SpellEngineWindows(affixPath, dictPath, exceptionPath);
		}

		private static SpellEngine CreateSpellEngineLinux(string affixPath, string dictPath,
			string exceptionPath)
		{
			// Separate method so that we don't try to instantiate the class when running on Windows
			return new SpellEngineLinux(affixPath, dictPath, exceptionPath);
		}

		internal SpellEngine(string exceptionPath)
		{
			ExceptionPath = exceptionPath;
		}

		private void Initialize()
		{
			if (!File.Exists(ExceptionPath))
				return;

			using (var reader = new StreamReader(ExceptionPath, Encoding.UTF8))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					var item = line;
					var correct = true;
					if (item.Length > 0 && item[0] == '*')
					{
						correct = false;
						item = item.Substring(1);
					}
					SetStatusInternal(item, correct);
				}
			}
		}

		/// <inheritdoc />
		public abstract bool Check(string word);


		/// <inheritdoc />
		public abstract bool IsVernacular { get; }

		/// <inheritdoc />
		public abstract ICollection<string> Suggest(string badWord);

		/// <inheritdoc />
		public void SetStatus(string word1, bool isCorrect)
		{
			var word = Normalizer.Normalize(word1, Normalizer.UNormalizationMode.UNORM_NFC);
			if (Check(word) == isCorrect)
				return; // nothing to do.
			// Review: any IO exceptions we should handle? How??
			SetStatusInternal(word, isCorrect);
			var builder = new StringBuilder();
			var insertedLineForWord = false;
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

		protected abstract void SetStatusInternal(string word1, bool isCorrect);

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
			ExceptionPath = null;
		}

	}

}
