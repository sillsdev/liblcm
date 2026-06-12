using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using System.Collections.Generic;

namespace SIL.LCModel.DomainServices
{
	public class SmartTextServices
	{
		public SmartTextServices(LcmCache cache)
		{
			Cache = cache;
		}

		LcmCache Cache { get; set; }

		public string SmartTextTitle = "";

		public bool OneSmartTextPerLetter = false;

		/// <summary>
		/// Add new example sentences in the lexicon to the appropriate smart texts.
		/// </summary>
		public void AddNewExampleSentences()
		{
			IEnumerable<ILexExampleSentence> sentences = GetExampleSentences();
			IList<IStTxtPara> smartParas = GetSmartParagraphs(Cache);
			ISet<ILexExampleSentence> processedSentences = new HashSet<ILexExampleSentence>();
			foreach (IStTxtPara smartPara in smartParas)
			{
				processedSentences.Add(smartPara.ExampleSentenceRA);
			}
			foreach (var sentence in sentences)
			{
				if (!processedSentences.Contains(sentence))
				{
					AddExampleSentence(sentence);
				}
			}
		}

		public IEnumerable<ILexExampleSentence> GetExampleSentences()
		{
			var exampleRepo = Cache.ServiceLocator.GetInstance<ILexExampleSentenceRepository>();
			return exampleRepo.AllInstances();
		}

		public static IList<IStTxtPara> GetSmartParagraphs(LcmCache cache)
		{
			var paraRepo = cache.ServiceLocator.GetInstance<IStTxtParaRepository>();
			IList<IStTxtPara> smartParagraphs = new List<IStTxtPara>();
			foreach (var para in paraRepo.AllInstances())
			{
				if (IsSmartParagraph(para))
				{
					smartParagraphs.Add(para);
				}
			}
			return smartParagraphs;
		}

		/// <summary>
		/// Add the given example sentence to the appropriate smart text.
		/// </summary>
		public void AddExampleSentence(ILexExampleSentence exampleSentence)
		{
			IText smartText = GetSmartText(exampleSentence);
			// Determine the best place to insert a new paragraph.
			ITsString newLabel = GetSmartLabel(exampleSentence);
			var wsObj = Cache.ServiceLocator.WritingSystemManager.Get(Cache.DefaultVernWs);
			var comparer = new TsStringComparer(wsObj);
			int pos = 0;
			foreach (var para in smartText.ContentsOA.ParagraphsOS)
			{
				if (para is IStTxtPara txtPara)
				{
					int result = comparer.Compare(newLabel, GetSmartLabel(txtPara.ExampleSentenceRA));
					if (result <= 0)
					{
						break;
					}
				}
				pos++;
			}
			var newPara = Cache.ServiceLocator.GetInstance<IStTxtParaFactory>().Create();
			smartText.ContentsOA.ParagraphsOS.Insert(pos, newPara);
			var segmentFactory = Cache.ServiceLocator.GetInstance<ISegmentFactory>();
			segmentFactory.Create(newPara, 0);
			newPara.ExampleSentenceRA = exampleSentence;
			ExampleSentenceChanged(newPara);
		}

		/// <summary>
		/// Get the appropriate smart text for the given example sentence.
		/// </summary>
		private IText GetSmartText(ILexExampleSentence exampleSentence)
		{
			IText bestText = null;
			ILexEntry entry = ((ILexSense)exampleSentence.Owner)?.Entry;
			if (entry == null)
			{
				return CreateSmartText("*");
			}
			string headword = entry.HeadWord.Text.ToLower();
			foreach (var text in GetSmartTexts())
			{
				if (InRange(headword, text))
				{
					if (bestText == null || InRange(text.SmartRangeStart.Text, bestText))
					{
						bestText = text;
					}
				}
			}
			if (bestText != null)
			{
				return bestText;
			}
			if (OneSmartTextPerLetter)
			{
				return CreateSmartText(headword[0].ToString());
			}
			return CreateSmartText("*");
		}

		public IList<IText> GetSmartTexts()
		{
			IList<IText> smartTexts = new List<IText>();
			var textRepo = Cache.ServiceLocator.GetInstance<ITextRepository>();
			foreach (var text in textRepo.AllInstances())
			{
				if (IsSmartText(text))
				{
					smartTexts.Add(text);
				}
			}
			return smartTexts;
		}

		public IText CreateSmartText(string rangeStart, string rangeEnd = null)
		{
			var textFactory = Cache.ServiceLocator.GetInstance<ITextFactory>();
			var stTextFactory = Cache.ServiceLocator.GetInstance<IStTextFactory>();
			var smartText = textFactory.Create();
			var text = stTextFactory.Create();
			smartText.ContentsOA = text;
			smartText.SmartRangeStart = TsStringUtils.MakeString(rangeStart, Cache.DefaultVernWs);
			if (rangeEnd != null)
			{
				smartText.SmartRangeEnd = TsStringUtils.MakeString(rangeEnd, Cache.DefaultVernWs);
			}
			string title = SmartTextTitle;
			if (rangeStart != "*")
			{
				title += " (" + rangeStart;
				if (rangeEnd != null)
				{
					title += "-" + rangeEnd;
				}
				title += ")";
			}
			smartText.Name.set_String(Cache.DefaultVernWs, title);
			return smartText;
		}

		/// <summary>
		/// Is sentence in smartText's range?
		/// </summary>
		private bool InRange(string name, IText smartText)
		{
			if (smartText.SmartRangeStart.Text == "*")
			{
				return true;
			}
			if (name == null)
			{
				return false;
			}
			if (name.StartsWith(smartText.SmartRangeStart.Text))
			{
				return true;
			}
			if (smartText.SmartRangeEnd.Text == null)
			{
				return false;
			}
			if (name.StartsWith(smartText.SmartRangeEnd.Text))
			{
				return true;
			}
			// Is sentence between SmartRangeStart and SmartRangeEnd?
			var wsObj = Cache.ServiceLocator.WritingSystemManager.Get(Cache.DefaultVernWs);
			var comparer = new TsStringComparer(wsObj);
			if (comparer.Compare(smartText.SmartRangeStart.Text, name) < 0 &&
				comparer.Compare(name, smartText.SmartRangeEnd.Text) < 0)
			{
				return true;
			}
			return false;
		}

		/// <summary>
		/// Get a unique label based on the given example sentence.
		/// </summary>
		public ITsString GetSmartLabel(ILexExampleSentence exampleSentence)
		{
			ITsString example = exampleSentence.Example.BestVernacularAnalysisAlternative;
			ITsStrBldr bldr = example.GetBldr();
			bldr.Clear();
			if (exampleSentence.Owner is ILexSense sense)
			{
				bldr.Append(sense.Entry.HeadWord.Text, Cache.DefaultVernWs);
				bldr.Append("." + (sense.IndexInOwner + 1), Cache.DefaultVernWs);
				bldr.Append("." + (exampleSentence.IndexInOwner + 1), Cache.DefaultVernWs);
			}
			return bldr.GetString();
		}

		public static bool IsSmartText(IText text)
		{
			return text.SmartRangeStart.Text != null;
		}

		public static bool IsSmartParagraph(IStTxtPara para)
		{
			return para.ExampleSentenceRA != null;
		}

		/// <summary>
		/// Update smart texts to reflect changes to exampleSentence.
		/// </summary>
		public static void UpdateSmartTexts(ILexExampleSentence exampleSentence)
		{
			LcmCache cache = exampleSentence.Cache;
			foreach (var smartPara in GetSmartParagraphs(cache))
			{
				if (smartPara.ExampleSentenceRA == exampleSentence)
				{
					ExampleSentenceChanged(smartPara);
				}
			}
		}

		/// <summary>
		/// Update para to reflect exampleSentence.
		/// </summary>
		public static void ExampleSentenceChanged(IStTxtPara para)
		{
			para.Contents = para.ExampleSentenceRA.Example.BestVernacularAnalysisAlternative;
			if (para.ExampleSentenceRA.TranslationsOC.Count == 1 && para.SegmentsOS.Count == 1)
			{
				foreach (var obj in para.ExampleSentenceRA.TranslationsOC.Objects)
				{
					para.SegmentsOS[0].FreeTranslation.CopyAlternatives(((ICmTranslation)obj).Translation);
				}
			}
		}

		/// <summary>
		/// Update para to reflect new contents.
		/// </summary>
		/// <param name="para"></param>
		/// <param name="contents"></param>
		public static void ContentsChanged(IStTxtPara para)
		{
			para.ExampleSentenceRA.Example.set_String(para.Contents.get_WritingSystemAt(0), para.Contents);
		}

		/// <summary>
		/// Update para to reflect new free translation.
		/// </summary>
		/// <param name="para"></param>
		/// <param name="contents"></param>
		public static void TranslationChanged(IStTxtPara para)
		{
			if (para.ExampleSentenceRA.TranslationsOC.Count == 1 && para.SegmentsOS.Count == 1)
			{
				IMultiString freeTranslation = para.SegmentsOS[0].FreeTranslation;
				IMultiString exampleTranslation = para.ExampleSentenceRA.TranslationsOC.ToArray()[0].Translation;
				exampleTranslation.CopyAlternatives(freeTranslation);
			}
		}
	}
}
