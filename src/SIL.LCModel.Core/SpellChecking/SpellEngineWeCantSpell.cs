using System;
using System.Collections.Generic;
using System.Linq;
using Icu;
using WeCantSpell.Hunspell;

namespace SIL.LCModel.Core.SpellChecking
{
	internal class SpellEngineWeCantSpell: SpellEngine
	{
		private readonly WordList _wordList;

		public SpellEngineWeCantSpell(string affixPath, string dictPath, string exceptionPath) : base(exceptionPath)
		{
			_wordList = WordList.CreateFromFiles(dictPath, affixPath);
		}

		public override bool Check(string word)
		{
			return _wordList.Check(Normalizer.Normalize(word, Normalizer.UNormalizationMode.UNORM_NFC));
		}

		public override ICollection<string> Suggest(string badWord)
		{
			var result =  _wordList.Suggest(badWord);
			return result as ICollection<string> ?? result.ToArray();
		}

		protected override void SetStatusInternal(string word1, bool isCorrect)
		{
			if (isCorrect)
			{
				var detail = IsVernacular
					? new WordEntryDetail(FlagSet.Create(new FlagValue(SpellingHelper.keepCaseFlag)),
						MorphSet.Create(new []{SpellingHelper.PrototypeWord}),
						WordEntryOptions.None)
					: WordEntryDetail.Default;
				_wordList.Add(word1, detail);
			}
			else
			{
				_wordList.Remove(word1);
			}
		}
	}
}