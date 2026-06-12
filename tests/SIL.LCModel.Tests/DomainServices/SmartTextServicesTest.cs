using NUnit.Framework;
using SIL.Extensions;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.DomainImpl;
using SIL.ObjectModel;
using System.Collections.Generic;
using System.Linq;

namespace SIL.LCModel.DomainServices
{
	public class SmartTextServicesTest : MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{
		internal class SmartTextBaseSetup : DisposableBase
		{
			LcmCache Cache { get; set; }

			public int m_SpanishWs;
			public int m_EnglishWs;

			internal SmartTextBaseSetup(LcmCache cache)
			{
				Cache = cache;
				CreateLexExampleSentences();
			}

			public void CreateLexExampleSentences()
			{
				// Switch to Spanish.
				WritingSystemManager wsManager = Cache.ServiceLocator.WritingSystemManager;
				CoreWritingSystemDefinition ws;
				wsManager.GetOrSet("es", out ws);
				m_SpanishWs = ws.Handle;
				Cache.ServiceLocator.WritingSystems.DefaultVernacularWritingSystem = ws;
				// Get English.
				wsManager.GetOrSet("en", out ws);
				m_EnglishWs = ws.Handle;
				// Create examples.
				CreateLexExampleSentence("chico", "chico", "boy");
				CreateLexExampleSentence("casa", "casa", "house");
				CreateLexExampleSentence("cosina", "cosina", "kitchen");
				List<ILexEntry> mesaEntries = new()
				{
					CreateLexExampleSentence("mesa", "mesa", "table"),
					CreateLexExampleSentence("mesa", "mesa", "desk")
				};
				LexDb.CorrectHomographNumbers(mesaEntries);
				ILexEntry agua = CreateLexExampleSentence("agua", "agua", "water");
				AddExampleSentence(agua.SensesOS.FirstOrDefault(), "es aqua", "is water");
				ILexSense sense = Cache.ServiceLocator.GetInstance<ILexSenseFactory>().Create();
				agua.SensesOS.Add(sense);
				AddExampleSentence(sense, "era aqua", "was water");
			}

			public ILexEntry CreateLexExampleSentence(string entryForm, string example, string translation)
			{
				ILexEntryFactory lexEntryFactory = Cache.ServiceLocator.GetInstance<ILexEntryFactory>();
				ILexEntry entry = lexEntryFactory.Create(entryForm, entryForm, SandboxGenericMSA.Create(MsaType.kStem, null));
				ILexSense sense = entry.SensesOS.FirstOrDefault();
				AddExampleSentence(sense, example, translation);
				return entry;
			}

			public void AddExampleSentence(ILexSense sense, string example, string translation)
			{
				ILexExampleSentence exSentence = Cache.ServiceLocator.GetInstance<ILexExampleSentenceFactory>().Create();
				sense.ExamplesOS.Add(exSentence);
				exSentence.Example.set_String(m_SpanishWs, example);
				var freeTrans = Cache.ServiceLocator.GetInstance<ICmPossibilityRepository>().GetObject(CmPossibilityTags.kguidTranFreeTranslation);
				ICmTranslation transObj = Cache.ServiceLocator.GetInstance<ICmTranslationFactory>().Create(exSentence, freeTrans);
				transObj.Translation.set_String(m_EnglishWs, translation);
			}

		}

		/// <summary>
		/// Kludge: undo doesn't work for everything in these tests, so RestartCache to be more radical.
		/// </summary>
		public override void TestTearDown()
		{
			base.TestTearDown();
			base.FixtureTeardown();
			base.FixtureSetup();
		}

		[Test]
		public void TestIsSmartText()
		{
			using (var setup = new SmartTextBaseSetup(Cache))
			{
				SmartTextServices services = new SmartTextServices(Cache);
				IText smartText = services.CreateSmartText("*");
				Assert.AreEqual(true, SmartTextServices.IsSmartText(smartText));
				var textFactory = Cache.ServiceLocator.GetInstance<ITextFactory>();
				var stTextFactory = Cache.ServiceLocator.GetInstance<IStTextFactory>();
				var text = textFactory.Create();
				smartText.ContentsOA = stTextFactory.Create();
				Assert.AreEqual(false, SmartTextServices.IsSmartText(text));
			}
		}

		/// <summary>
		/// Test AddNewExampleSentences when there is only one smart text.
		/// </summary>
		[Test]
		public void TestSmartText()
		{
			using (var setup = new SmartTextBaseSetup(Cache))
			{
				SmartTextServices services = new SmartTextServices(Cache);
				services.SmartTextTitle = "Example Sentences";
				services.AddNewExampleSentences();
				IList<IText> smartTexts = services.GetSmartTexts();
				Assert.AreEqual(1, smartTexts.Count);
				IText smartText = smartTexts[0];
				Assert.AreEqual("Example Sentences", smartText.Name.BestVernacularAlternative.Text);
				Assert.AreEqual(8, smartText.ContentsOA.ParagraphsOS.Count);
				IStTxtPara para;
				para = smartText.ContentsOA.ParagraphsOS[0] as IStTxtPara;
				Assert.AreEqual("agua.1.1", services.GetSmartLabel(para.ExampleSentenceRA).Text);
				para = smartText.ContentsOA.ParagraphsOS[1] as IStTxtPara;
				Assert.AreEqual("agua.1.2", services.GetSmartLabel(para.ExampleSentenceRA).Text);
				para = smartText.ContentsOA.ParagraphsOS[2] as IStTxtPara;
				Assert.AreEqual("agua.2.1", services.GetSmartLabel(para.ExampleSentenceRA).Text);
				para = smartText.ContentsOA.ParagraphsOS[3] as IStTxtPara;
				Assert.AreEqual("casa.1.1", services.GetSmartLabel(para.ExampleSentenceRA).Text);
				para = smartText.ContentsOA.ParagraphsOS[4] as IStTxtPara;
				Assert.AreEqual("chico.1.1", services.GetSmartLabel(para.ExampleSentenceRA).Text);
				para = smartText.ContentsOA.ParagraphsOS[5] as IStTxtPara;
				// Note: cosina follows chico in modern Spanish.
				Assert.AreEqual("cosina.1.1", services.GetSmartLabel(para.ExampleSentenceRA).Text);
				para = smartText.ContentsOA.ParagraphsOS[6] as IStTxtPara;
				Assert.AreEqual("mesa1.1.1", services.GetSmartLabel(para.ExampleSentenceRA).Text);
				para = smartText.ContentsOA.ParagraphsOS[7] as IStTxtPara;
				Assert.AreEqual("mesa2.1.1", services.GetSmartLabel(para.ExampleSentenceRA).Text);
			}
		}

		/// <summary>
		/// Test AddNewExampleSentences when the user has requested one smart text per letter.
		/// </summary>
		[Test]
		public void TestAlphabeticSmartTexts()
		{
			using (var setup = new SmartTextBaseSetup(Cache))
			{
				SmartTextServices services = new SmartTextServices(Cache);
				services.SmartTextTitle = "Example Sentences";
				services.OneSmartTextPerLetter = true;
				services.AddNewExampleSentences();
				IList<IText> smartTexts = services.GetSmartTexts();
				Assert.AreEqual(3, smartTexts.Count);
				smartTexts.Sort((x, y) => x.Name.BestVernacularAlternative.Text.CompareTo(y.Name.BestVernacularAlternative.Text));
				Assert.AreEqual("Example Sentences (a)", smartTexts[0].Name.BestVernacularAlternative.Text);
				Assert.AreEqual(3, smartTexts[0].ContentsOA.ParagraphsOS.Count);
				Assert.AreEqual("Example Sentences (c)", smartTexts[1].Name.BestVernacularAlternative.Text);
				Assert.AreEqual(3, smartTexts[1].ContentsOA.ParagraphsOS.Count);
				Assert.AreEqual("Example Sentences (m)", smartTexts[2].Name.BestVernacularAlternative.Text);
				Assert.AreEqual(2, smartTexts[2].ContentsOA.ParagraphsOS.Count);
			}
		}

		/// <summary>
		/// Test AddNewExampleSentences when the user chooses the ranges.
		/// </summary>
		[Test]
		public void TestRangedSmartTexts()
		{
			using (var setup = new SmartTextBaseSetup(Cache))
			{
				SmartTextServices services = new SmartTextServices(Cache);
				services.SmartTextTitle = "Example Sentences";
				services.CreateSmartText("a", "c");
				services.AddNewExampleSentences();
				IList<IText> smartTexts = services.GetSmartTexts();
				Assert.AreEqual(2, smartTexts.Count);
				smartTexts.Sort((x, y) => x.Name.BestVernacularAlternative.Text.CompareTo(y.Name.BestVernacularAlternative.Text));
				Assert.AreEqual("Example Sentences", smartTexts[0].Name.BestVernacularAlternative.Text);
				Assert.AreEqual(2, smartTexts[0].ContentsOA.ParagraphsOS.Count);
				Assert.AreEqual("Example Sentences (a-c)", smartTexts[1].Name.BestVernacularAlternative.Text);
				Assert.AreEqual(6, smartTexts[1].ContentsOA.ParagraphsOS.Count);
			}
		}

		/// <summary>
		/// Test synchronization between smart texts and example sentences.
		/// </summary>
		[Test]
		public void TestSmartTextSynchronization()
		{
			using (var setup = new SmartTextBaseSetup(Cache))
			{
				SmartTextServices services = new SmartTextServices(Cache);
				services.SmartTextTitle = "Example Sentences";
				services.AddNewExampleSentences();
				IList<IText> smartTexts = services.GetSmartTexts();
				Assert.AreEqual(1, smartTexts.Count);
				IText smartText = smartTexts[0];

				// Test initial contents.
				IStTxtPara para = smartText.ContentsOA.ParagraphsOS[0] as IStTxtPara;
				ILexExampleSentence exampleSentence = para.ExampleSentenceRA;
				IMultiString freeTranslation = para.SegmentsOS.ToArray()[0].FreeTranslation;
				IMultiString exampleTranslation = exampleSentence.TranslationsOC.ToArray()[0].Translation;
				Assert.AreEqual("agua", para.Contents.Text);
				Assert.AreEqual(1, para.SegmentsOS.Count);
				Assert.AreEqual("water", freeTranslation.BestAnalysisAlternative.Text);

				// Test changes to example sentence.
				exampleSentence.Example.set_String(setup.m_SpanishWs, "agua! agua!");
				SmartTextServices.UpdateSmartTexts(exampleSentence);
				Assert.AreEqual("agua! agua!", para.Contents.Text);
				Assert.AreEqual(1, para.SegmentsOS.Count);

				// Test changes to contents.
				para.Contents = TsStringUtils.MakeString("agua? agua?", setup.m_SpanishWs);
				Assert.AreEqual("agua? agua?", exampleSentence.Example.BestVernacularAlternative.Text);

				// Test changes to example sentence translation.
				exampleTranslation.set_String(setup.m_EnglishWs, "water! water!");
				SmartTextServices.UpdateSmartTexts(exampleSentence);
				Assert.AreEqual("water! water!", freeTranslation.BestAnalysisAlternative.Text);

				// Test changes to free translation.
				freeTranslation.set_String(setup.m_EnglishWs, "water? water?");
				SmartTextServices.TranslationChanged(para);
				Assert.AreEqual("water? water?", exampleTranslation.BestAnalysisAlternative.Text);

			}
		}

	}
}
