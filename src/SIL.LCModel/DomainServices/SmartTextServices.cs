using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.DomainImpl;
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

		/// <summary>
		/// Add new example sentences in the lexicon to the appropriate smart texts.
		/// </summary>
		public void AddNewExampleSentences()
		{
			IEnumerable<ILexExampleSentence> sentences = GetExampleSentences();
			IList<IStTxtPara> smartParas = GetSmartParagraphs();
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

		public IList<IStTxtPara> GetSmartParagraphs()
		{
			var paraRepo = Cache.ServiceLocator.GetInstance<IStTxtParaRepository>();
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
			ITsString example = exampleSentence.Example.BestVernacularAnalysisAlternative;
			var wsObj = Cache.ServiceLocator.WritingSystemManager.Get(example.get_WritingSystemAt(0));
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
			UpdateExampleSentence(newPara, exampleSentence);
		}

		/// <summary>
		/// Get the appropriate smart text for the given example sentence.
		/// </summary>
		private IText GetSmartText(ILexExampleSentence sentence)
		{
			var textRepo = Cache.ServiceLocator.GetInstance<IStTextRepository>();
			foreach (var text in GetSmartTexts())
			{
				return text;
			}
			return CreateSmartText("");
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

		private IText CreateSmartText(string rangeStart)
		{
			var textFactory = Cache.ServiceLocator.GetInstance<ITextFactory>();
			var stTextFactory = Cache.ServiceLocator.GetInstance<IStTextFactory>();
			var smartText = textFactory.Create();
			//Cache.LangProject.TextsOC.Add(Text);
			var text = stTextFactory.Create();
			smartText.ContentsOA = text;
			smartText.SmartRangeStart = TsStringUtils.MakeString(rangeStart, Cache.DefaultVernWs);
			smartText.Name.set_String(Cache.DefaultVernWs, SmartTextTitle);
			return smartText;
		}

		/// <summary>
		/// Get a unique label based on the given example sentence.
		/// </summary>
		public ITsString GetSmartLabel(ILexExampleSentence exampleSentence)
		{
			ITsString example = exampleSentence.Example.BestVernacularAnalysisAlternative;
			ITsStrBldr bldr = example.GetBldr();
			bldr.Clear();
			bldr.Append("[", Cache.DefaultVernWs);
			if (exampleSentence.Owner is ILexSense sense)
			{
				bldr.Append(sense.Entry.HeadWord.Text, Cache.DefaultVernWs);
				bldr.Append(":" + (sense.IndexInOwner + 1), Cache.DefaultVernWs);
				bldr.Append(":" + (exampleSentence.IndexInOwner + 1), Cache.DefaultVernWs);
			}
			bldr.Append("]", Cache.DefaultVernWs);
			return bldr.GetString();
		}

		public static bool IsSmartText(IText text)
		{
			return text.SmartRangeStart != null;
		}

		public static bool IsSmartParagraph(IStTxtPara para)
		{
			return para.ExampleSentenceRA != null;
		}

		public static void UpdateExampleSentence(IStTxtPara para, ILexExampleSentence exampleSentence)
		{
			para.ExampleSentenceRA = exampleSentence;
			para.Contents = exampleSentence.Example.BestVernacularAnalysisAlternative;
		}
	}
}
