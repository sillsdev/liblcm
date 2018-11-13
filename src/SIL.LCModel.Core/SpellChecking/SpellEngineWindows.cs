// // Copyright (c) 2018 SIL International
// // This software is licensed under the MIT License (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Icu;
using NHunspell;

namespace SIL.LCModel.Core.SpellChecking
{
	internal sealed class SpellEngineWindows: SpellEngine
	{
		private readonly Hunspell _hunspellHandle;
		private bool _isVernacular;
		private bool _gotIsVernacular;

		internal SpellEngineWindows(string affixPath, string dictPath, string exceptionPath)
			: base(exceptionPath)
		{
			try
			{
				_hunspellHandle = new Hunspell(affixPath, dictPath);
			}
			catch (Exception e)
			{
				Debug.WriteLine("Initializing Hunspell: {0} exception: {1} ", e.GetType(), e.Message);
				_hunspellHandle?.Dispose();
				throw;
			}
		}

		#region Disposable
		protected override void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
			if (disposing)
			{
				_hunspellHandle?.Dispose();
			}

			base.Dispose(disposing);
		}
		#endregion

		public override bool Check(string word)
		{
			return _hunspellHandle.Spell(MarshallAsUtf8Bytes(word));
		}

		protected override void SetStatusInternal(string word, bool isCorrect)
		{
			if (isCorrect)
			{
				if (IsVernacular)
				{
					// Custom vernacular-only dictionary.
					// want it 'affixed' like the prototype, which has been marked to suppress other-case matches
					_hunspellHandle.AddWithAffix(MarshallAsUtf8Bytes(word), MarshallAsUtf8Bytes(SpellingHelper.PrototypeWord));
				}
				else
				{
					// not our custom dictionary, some majority language, we can't (and probably don't want)
					// to be restrictive about case.
					_hunspellHandle.Add(MarshallAsUtf8Bytes(word));
				}
			}
			else
			{
				_hunspellHandle.Remove(MarshallAsUtf8Bytes(word));
			}
		}

		/// <inheritdoc />
		public override ICollection<string> Suggest(string badWord)
		{
			return _hunspellHandle.Suggest(MarshallAsUtf8Bytes(badWord));
		}

		/// <inheritdoc />
		public override bool IsVernacular
		{
			get
			{
				if (_gotIsVernacular)
					return _isVernacular;

				_isVernacular = Check(MarshallAsUtf8Bytes(SpellingHelper.PrototypeWord));
				_gotIsVernacular = true;
				return _isVernacular;
			}
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
			var bytes = Encoding.UTF8.GetBytes(Normalizer.Normalize(word,
				Normalizer.UNormalizationMode.UNORM_NFC) + "\0");
			return Encoding.UTF8.GetString(bytes);
		}
	}
}