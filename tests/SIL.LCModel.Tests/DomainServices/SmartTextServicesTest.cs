using NUnit.Framework;
using SIL.LCModel.Core.Text;
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
			internal IText Text { get; set; }
			internal IStText StText { get; set; }
			internal StTxtPara Para0 { get; set; }
			internal IList<IWfiWordform> Words_para0 { get; set; }
			internal ICmAgent UserAgent { get; set; }
			internal ICmAgent ParserAgent { get; set; }
			internal AnalysisGuessServices GuessServices { get; set; }
			internal ILexEntryFactory EntryFactory { get; set; }


			// parts of speech
			internal IPartOfSpeech Pos_adjunct { get; set; }
			internal IPartOfSpeech Pos_noun { get; set; }
			internal IPartOfSpeech Pos_verb { get; set; }
			internal IPartOfSpeech Pos_transitiveVerb { get; set; }

			// variant entry types
			internal ILexEntryType Vet_DialectalVariant { get; set; }
			internal ILexEntryType Vet_FreeVariant { get; set; }
			internal ILexEntryType Vet_InflectionalVariant { get; set; }

			internal enum Flags
			{
				PartsOfSpeech,
				VariantEntryTypes
			}

			SmartTextBaseSetup()
			{
				Words_para0 = new List<IWfiWordform>();
			}

			LcmCache Cache { get; set; }

			internal SmartTextBaseSetup(LcmCache cache, bool prioritizeParser = false) : this()
			{
				Cache = cache;
				UserAgent = Cache.LanguageProject.DefaultUserAgent;
				ParserAgent = Cache.LangProject.DefaultParserAgent;
				GuessServices = new AnalysisGuessServices(Cache, prioritizeParser);
				EntryFactory = Cache.ServiceLocator.GetInstance<ILexEntryFactory>();
				DoDataSetup();
			}

			internal SmartTextBaseSetup(LcmCache cache, params Flags[] options)
				: this(cache)
			{
				if (options.Contains(Flags.PartsOfSpeech))
					SetupPartsOfSpeech();
				if (options.Contains(Flags.VariantEntryTypes))
					SetupVariantEntryTypes();
			}

			internal void DoDataSetup()
			{
				CreateLexExampleSentence();
			}

			public void CreateLexExampleSentence()
			{
				ILexEntryFactory lexEntryFactory = Cache.ServiceLocator.GetInstance<ILexEntryFactory>();
				ILexEntry entry = lexEntryFactory.Create("ay", "Astem", SandboxGenericMSA.Create(MsaType.kStem, null));
				ILexSense sense = Cache.ServiceLocator.GetInstance<ILexSenseFactory>().Create();
				entry.SensesOS.Add(sense);
				ILexExampleSentence exSentence = Cache.ServiceLocator.GetInstance<ILexExampleSentenceFactory>().Create();
				sense.ExamplesOS.Add(exSentence);
			}

			internal void SetupPartsOfSpeech()
			{
				// setup language project parts of speech
				var partOfSpeechFactory = Cache.ServiceLocator.GetInstance<IPartOfSpeechFactory>();
				Pos_adjunct = partOfSpeechFactory.Create();
				Pos_noun = partOfSpeechFactory.Create();
				Pos_verb = partOfSpeechFactory.Create();
				Pos_transitiveVerb = partOfSpeechFactory.Create();
				Cache.LangProject.PartsOfSpeechOA.PossibilitiesOS.Add(Pos_adjunct);
				Cache.LangProject.PartsOfSpeechOA.PossibilitiesOS.Add(Pos_noun);
				Cache.LangProject.PartsOfSpeechOA.PossibilitiesOS.Add(Pos_verb);
				Pos_verb.SubPossibilitiesOS.Add(Pos_transitiveVerb);
				Pos_adjunct.Name.set_String(Cache.DefaultAnalWs, "adjunct");
				Pos_noun.Name.set_String(Cache.DefaultAnalWs, "noun");
				Pos_verb.Name.set_String(Cache.DefaultAnalWs, "verb");
				Pos_transitiveVerb.Name.set_String(Cache.DefaultAnalWs, "transitive verb");
			}

			internal void SetupVariantEntryTypes()
			{
				VariantEntryTypes = Cache.LangProject.LexDbOA.VariantEntryTypesOA;
				Vet_DialectalVariant = VariantEntryTypes.PossibilitiesOS[0] as ILexEntryType;
				Vet_FreeVariant = VariantEntryTypes.PossibilitiesOS[1] as ILexEntryType;
				Vet_InflectionalVariant = VariantEntryTypes.PossibilitiesOS[2] as ILexEntryType;
			}

			ICmPossibilityList VariantEntryTypes { get; set; }
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
				services.AddNewExampleSentences();
				IList<IText> smartTexts = services.GetSmartTexts();
				Assert.AreEqual(1, smartTexts.Count);
			}
		}
	}
}
