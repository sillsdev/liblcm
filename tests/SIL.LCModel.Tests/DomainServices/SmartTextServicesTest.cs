using NUnit.Framework;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.DomainImpl;
using SIL.ObjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SIL.LCModel.DomainServices.AnalysisGuessServicesTests;

namespace SIL.LCModel.DomainServices
{
	public class SmartTextServicesTest : MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{
		internal class SmartTextBaseSetup : DisposableBase
		{
			LcmCache Cache { get; set; }

			int m_SpanishWs;
			int m_EnglishWs;

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
				Assert.AreEqual("[agua:1:1]", services.GetSmartLabel(para.ExampleSentenceRA).Text);
				para = smartText.ContentsOA.ParagraphsOS[1] as IStTxtPara;
				Assert.AreEqual("[agua:1:2]", services.GetSmartLabel(para.ExampleSentenceRA).Text);
				para = smartText.ContentsOA.ParagraphsOS[2] as IStTxtPara;
				Assert.AreEqual("[agua:2:1]", services.GetSmartLabel(para.ExampleSentenceRA).Text);
				para = smartText.ContentsOA.ParagraphsOS[3] as IStTxtPara;
				Assert.AreEqual("[casa:1:1]", services.GetSmartLabel(para.ExampleSentenceRA).Text);
				para = smartText.ContentsOA.ParagraphsOS[4] as IStTxtPara;
				Assert.AreEqual("[chico:1:1]", services.GetSmartLabel(para.ExampleSentenceRA).Text);
				para = smartText.ContentsOA.ParagraphsOS[5] as IStTxtPara;
				// Note: cosina follows chico in modern Spanish.
				Assert.AreEqual("[cosina:1:1]", services.GetSmartLabel(para.ExampleSentenceRA).Text);
				para = smartText.ContentsOA.ParagraphsOS[6] as IStTxtPara;
				Assert.AreEqual("[mesa1:1:1]", services.GetSmartLabel(para.ExampleSentenceRA).Text);
				para = smartText.ContentsOA.ParagraphsOS[7] as IStTxtPara;
				Assert.AreEqual("[mesa2:1:1]", services.GetSmartLabel(para.ExampleSentenceRA).Text);
			}
		}
	}
}
