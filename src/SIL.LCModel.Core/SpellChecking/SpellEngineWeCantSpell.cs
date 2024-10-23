using System;
using System.Collections.Generic;
using System.Linq;
using WeCantSpell.Hunspell;

namespace SIL.LCModel.Core.SpellChecking
{
	internal class SpellEngineWeCantSpell: SpellEngine
	{
		private readonly WordList _wordList;
		private readonly WordList.Builder _customWordsBuilder;
		private WordList _customWordList;
		private readonly HashSet<string> _badWords = new HashSet<string>();

		public SpellEngineWeCantSpell(string affixPath, string dictPath, string exceptionPath) : base(exceptionPath)
		{
			_wordList = WordList.CreateFromFiles(dictPath, affixPath);
			_customWordsBuilder = new WordList.Builder(_wordList.Affix);
			_customWordList = _customWordsBuilder.ToImmutable();
		}

		public override bool Check(string word)
		{
			if (_badWords.Contains(word)) return false;
			if (_customWordList.Check(word)) return true;
			return _wordList.Check(word);
		}

		public override ICollection<string> Suggest(string badWord)
		{
			var suggestions = _wordList.Suggest(badWord).Union(_customWordList.Suggest(badWord));
			return suggestions.Where(suggestion => !_badWords.Contains(suggestion)).ToArray();
		}

		protected override void SetStatusInternal(string word1, bool isCorrect)
		{
			// WeCantSpell does not support modifying the word list, so we have to use 2 and merge them.
			if (isCorrect)
			{
				var detail = IsVernacular
					? new WordEntryDetail(FlagSet.Empty,
						MorphSet.Create(new []{SpellingHelper.PrototypeWord}),
						WordEntryOptions.None)
					: WordEntryDetail.Default;
				_customWordsBuilder.Add(word1, detail);
				_customWordList = _customWordsBuilder.ToImmutable();
			}
			else
			{
				_badWords.Add(word1);
			}
		}
	}
}