// Copyright (c) 2009-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SIL.LCModel.Core.Text;
using SIL.LCModel.DomainImpl;
using SIL.LCModel.Infrastructure;
using SIL.ObjectModel;

namespace SIL.LCModel.DomainServices
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	///
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[TestFixture]
	public class AnalysisGuessServicesTests : MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{
		// REVIEW (TomB): There is no reason to derive from FwDisposableBase because neither
		// Dispose method is being overriden. Either override one of those methods or get rid
		// of all the using statements where objects of this class are instantiated.
		internal class AnalysisGuessBaseSetup : DisposableBase
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

			AnalysisGuessBaseSetup()
			{
				Words_para0 = new List<IWfiWordform>();
			}

			LcmCache Cache { get; set; }

			internal AnalysisGuessBaseSetup(LcmCache cache) : this()
			{
				Cache = cache;
				UserAgent = Cache.LanguageProject.DefaultUserAgent;
				ParserAgent = Cache.LangProject.DefaultParserAgent;
				GuessServices = new AnalysisGuessServices(Cache);
				EntryFactory = Cache.ServiceLocator.GetInstance<ILexEntryFactory>();
				DoDataSetup();
			}

			internal AnalysisGuessBaseSetup(LcmCache cache, params Flags[] options)
				: this(cache)
			{
				if (options.Contains(Flags.PartsOfSpeech))
					SetupPartsOfSpeech();
				if (options.Contains(Flags.VariantEntryTypes))
					SetupVariantEntryTypes();
			}

			internal void DoDataSetup()
			{
				var textFactory = Cache.ServiceLocator.GetInstance<ITextFactory>();
				var stTextFactory = Cache.ServiceLocator.GetInstance<IStTextFactory>();
				Text = textFactory.Create();
				//Cache.LangProject.TextsOC.Add(Text);
				StText = stTextFactory.Create();
				Text.ContentsOA = StText;
				Para0 = (StTxtPara)StText.AddNewTextPara(null);
				var wfFactory = Cache.ServiceLocator.GetInstance<IWfiWordformFactory>();
				var wsVern = Cache.DefaultVernWs;
				/* A a a a. */
				IWfiWordform A = wfFactory.Create(TsStringUtils.MakeString("A", wsVern));
				IWfiWordform a = wfFactory.Create(TsStringUtils.MakeString("a", wsVern));
				Words_para0.Add(A);
				Words_para0.Add(a);
				Words_para0.Add(a);
				Words_para0.Add(a);
				Para0.Contents = TsStringUtils.MakeString(
					Words_para0[0].Form.BestVernacularAlternative.Text + " " +
					Words_para0[1].Form.BestVernacularAlternative.Text + " " +
					Words_para0[2].Form.BestVernacularAlternative.Text + " " +
					Words_para0[3].Form.BestVernacularAlternative.Text + ".", wsVern);
				/* b B. */
				IWfiWordform b = wfFactory.Create(TsStringUtils.MakeString("b", wsVern));
				IWfiWordform B = wfFactory.Create(TsStringUtils.MakeString("B", wsVern));
				Words_para0.Add(b);
				Words_para0.Add(B);
				var bldr = Para0.Contents.GetIncBldr();
				bldr.AppendTsString(TsStringUtils.MakeString(
					" " + Words_para0[4].Form.BestVernacularAlternative.Text + " " +
					Words_para0[5].Form.BestVernacularAlternative.Text + ".", wsVern));
				Para0.Contents = bldr.GetString();
				/* c c c c d c d c d c d c, c. */
				IWfiWordform c = wfFactory.Create(TsStringUtils.MakeString("c", wsVern));
				IWfiWordform d = wfFactory.Create(TsStringUtils.MakeString("d", wsVern));
				Words_para0.Add(c);
				Words_para0.Add(c);
				Words_para0.Add(c);
				Words_para0.Add(c);
				Words_para0.Add(d);
				Words_para0.Add(c);
				Words_para0.Add(d); 
				Words_para0.Add(c);
				Words_para0.Add(d);
				Words_para0.Add(c);
				Words_para0.Add(d);
				Words_para0.Add(c);
				Words_para0.Add(c); // after punctuation
				var bldr2 = Para0.Contents.GetIncBldr();
				bldr2.AppendTsString(TsStringUtils.MakeString(
					" " + Words_para0[6].Form.BestVernacularAlternative.Text +
					" " + Words_para0[7].Form.BestVernacularAlternative.Text +
					" " + Words_para0[8].Form.BestVernacularAlternative.Text +
					" " + Words_para0[9].Form.BestVernacularAlternative.Text +
					" " + Words_para0[10].Form.BestVernacularAlternative.Text +
					" " + Words_para0[11].Form.BestVernacularAlternative.Text +
					" " + Words_para0[12].Form.BestVernacularAlternative.Text +
					" " + Words_para0[13].Form.BestVernacularAlternative.Text +
					" " + Words_para0[14].Form.BestVernacularAlternative.Text +
					" " + Words_para0[15].Form.BestVernacularAlternative.Text +
					" " + Words_para0[16].Form.BestVernacularAlternative.Text +
					" " + Words_para0[17].Form.BestVernacularAlternative.Text +
					", " + Words_para0[18].Form.BestVernacularAlternative.Text +
					".", wsVern));
				Para0.Contents = bldr2.GetString();
				/* E. */
				IWfiWordform E = wfFactory.Create(TsStringUtils.MakeString("E", wsVern));
				Words_para0.Add(E);
				var bldr3 = Para0.Contents.GetIncBldr();
				bldr3.AppendTsString(TsStringUtils.MakeString(
					" " + Words_para0[19].Form.BestVernacularAlternative.Text + ".", wsVern));
				Para0.Contents = bldr3.GetString();
				using (ParagraphParser pp = new ParagraphParser(Cache))
				{
					foreach (IStTxtPara para in StText.ParagraphsOS)
						pp.Parse(para);
				}
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
		///
		/// </summary>
		[Test]
		public void NoExpectedGuessForWord_NoAnalyses()
		{
			// don't make any analyses. so we don't expect any guesses.
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(new NullWAG(), guessActual);
			}
		}

		/// <summary>
		///
		/// </summary>
		[Test]
		public void NoExpectedGuessForAnalysis_NoAnalyses()
		{
			// don't make any analyses. so we don't expect any guesses.
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(new NullWAG(), guessActual);
			}
		}

		/// <summary>
		///
		/// </summary>
		[Test]
		public void NoExpectedGuessForAnalysis_NoGlosses()
		{
			// make two analyses, but don't make any glosses. so we don't expect any guesses for one of the analyses.
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				var newAnalysisWag2 = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				var guessActual = setup.GuessServices.GetBestGuess(newAnalysisWag2.Analysis);
				Assert.AreEqual(new NullWAG(), guessActual);
			}
		}


		/// <summary>
		/// make a disapproved analysis that shouldn't be returned as a guess.
		/// </summary>
		[Test]
		public void NoExpectedGuessForWord_DisapprovesHumanAnalysis()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWag = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				var newAnalysis = newWag.Analysis;
				setup.UserAgent.SetEvaluation(newAnalysis, Opinions.disapproves);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(new NullWAG(), guessActual);
			}
		}

		/// <summary>
		/// make a disapproved analysis that shouldn't be returned as a guess for another analysis.
		/// </summary>
		[Test]
		public void NoExpectedGuessForAnalysis_DisapprovesHumanAnalysis()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newAnalysisWag = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				var newAnalysisWag2 = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				setup.UserAgent.SetEvaluation(newAnalysisWag.Analysis, Opinions.disapproves);
				var guessActual = setup.GuessServices.GetBestGuess(newAnalysisWag2.Analysis);
				Assert.AreEqual(new NullWAG(), guessActual);
			}
		}

		/// <summary>
		/// make a gloss with a disapproved analysis that shouldn't be returned as a guess.
		/// </summary>
		[Test]
		public void NoExpectedGuessForWord_DisapprovesHumanAnalysisOfGloss()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newGlossWag = WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[1]);
				setup.UserAgent.SetEvaluation(newGlossWag.WfiAnalysis, Opinions.disapproves);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(new NullWAG(), guessActual);
			}
		}

		/// <summary>
		/// make a gloss with a disapproved analysis that shouldn't be returned as a guess.
		/// </summary>
		[Test]
		public void NoExpectedGuessForAnalysis_DisapprovesHumanAnalysisOfGloss()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newGlossWag = WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[1]);
				setup.UserAgent.SetEvaluation(newGlossWag.WfiAnalysis, Opinions.disapproves);
				var guessActual = setup.GuessServices.GetBestGuess(newGlossWag.WfiAnalysis);
				Assert.AreEqual(new NullWAG(), guessActual);
			}
		}

		/// <summary>
		/// make a human disapproved analysis that shouldn't be returned as a guess.
		/// </summary>
		[Test]
		public void NoExpectedGuessForWord_DisapprovesParserApprovedAnalysis()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWag = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				var newAnalysis = newWag.Analysis;
				setup.ParserAgent.SetEvaluation(newAnalysis, Opinions.approves);
				setup.UserAgent.SetEvaluation(newAnalysis, Opinions.disapproves);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(new NullWAG(), guessActual);
			}
		}

		/// <summary>
		/// make an entry (affix) that shouldn't be returned as a guess.
		/// </summary>
		[Test]
		public void NoExpectedGuessForWord_NoMatchingEntries()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				// create an affix entry
				setup.EntryFactory.Create("a-", "aPrefix", SandboxGenericMSA.Create(MsaType.kInfl, null));
				setup.EntryFactory.Create("-a", "aSuffix", SandboxGenericMSA.Create(MsaType.kDeriv, null));
				setup.EntryFactory.Create("-a-", "aInfix", SandboxGenericMSA.Create(MsaType.kUnclassified, null));
				setup.EntryFactory.Create("ay", "Astem", SandboxGenericMSA.Create(MsaType.kStem, null));
				setup.EntryFactory.Create("ay", "Aroot", SandboxGenericMSA.Create(MsaType.kRoot, null));
				// try to generate analyses for matching entries (should have no results)
				setup.GuessServices.GenerateEntryGuesses(setup.StText);

				int cAnalyses = Cache.ServiceLocator.GetInstance<IWfiAnalysisRepository>().Count;
				Assert.AreEqual(0, cAnalyses, "Should not have generated guesses.");

				// make sure we don't actually make a guess.
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(new NullWAG(), guessActual);
			}
		}


		/// <summary>
		///
		/// </summary>
		[Test]
		public void NoExpectedGuessForWord_DontMatchBoundedStemOrRoot()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var morphTypeRepository = Cache.ServiceLocator.GetInstance<IMoMorphTypeRepository>();
				var morphTypeBoundedRoot = morphTypeRepository.GetObject(MoMorphTypeTags.kguidMorphBoundRoot);
				var morphTypeBoundedStem = morphTypeRepository.GetObject(MoMorphTypeTags.kguidMorphBoundStem);
				// first make sure these types don't care about prefix/postfix markers
				morphTypeBoundedRoot.Prefix = morphTypeBoundedRoot.Postfix = null;
				morphTypeBoundedStem.Prefix = morphTypeBoundedStem.Postfix = null;

				// create an affix entry
				setup.EntryFactory.Create("a-", "aPrefix", SandboxGenericMSA.Create(MsaType.kInfl, null));
				setup.EntryFactory.Create("-a", "aSuffix", SandboxGenericMSA.Create(MsaType.kDeriv, null));
				setup.EntryFactory.Create("-a-", "aInfix", SandboxGenericMSA.Create(MsaType.kUnclassified, null));
				setup.EntryFactory.Create(morphTypeBoundedStem, TsStringUtils.MakeString("a", Cache.DefaultVernWs),
					"aboundedstem", SandboxGenericMSA.Create(MsaType.kStem, null));
				setup.EntryFactory.Create(morphTypeBoundedRoot, TsStringUtils.MakeString("a", Cache.DefaultVernWs),
					"aboundedroot", SandboxGenericMSA.Create(MsaType.kRoot, null));
				// try to generate analyses for matching entries (should have no results)
				setup.GuessServices.GenerateEntryGuesses(setup.StText);

				int cAnalyses = Cache.ServiceLocator.GetInstance<IWfiAnalysisRepository>().Count;
				Assert.AreEqual(0, cAnalyses, "Should not have generated guesses.");

				// make sure we don't actually make a guess.
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(new NullWAG(), guessActual);
			}

		}

		/// <summary>
		/// make a human disapproved analysis that shouldn't be returned as a guess.
		/// </summary>
		[Test]
		public void NoExpectedGuessForAnalysis_DisapprovesParserApprovedAnalysis()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWag = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				var newAnalysis = newWag.Analysis;
				setup.ParserAgent.SetEvaluation(newAnalysis, Opinions.approves);
				setup.UserAgent.SetEvaluation(newAnalysis, Opinions.disapproves);
				var guessActual = setup.GuessServices.GetBestGuess(newWag.Analysis);
				Assert.AreEqual(new NullWAG(), guessActual);
			}
		}

		/// <summary>
		/// make an entry (stem) that should be returned as a guess.
		/// </summary>
		[Test]
		public void ExpectedGuessForWord_MatchingEntry()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache, AnalysisGuessBaseSetup.Flags.PartsOfSpeech))
			{
				// create an affix entry
				setup.EntryFactory.Create("a-", "aPrefix", SandboxGenericMSA.Create(MsaType.kInfl, null));
				setup.EntryFactory.Create("-a", "aSuffix", SandboxGenericMSA.Create(MsaType.kDeriv, null));
				setup.EntryFactory.Create("-a-", "aInfix", SandboxGenericMSA.Create(MsaType.kUnclassified, null));
				var newEntry4_expectedMatch = setup.EntryFactory.Create("a", "astem", SandboxGenericMSA.Create(MsaType.kStem, setup.Pos_noun));
				setup.EntryFactory.Create("a", "aroot", SandboxGenericMSA.Create(MsaType.kRoot, null));

				// expect a guess to be generated
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreNotEqual(new NullWAG(), guessActual);
				Assert.AreEqual(newEntry4_expectedMatch.LexemeFormOA.Form.BestVernacularAlternative.Text, guessActual.Wordform.Form.BestVernacularAlternative.Text);
				Assert.AreEqual(1, guessActual.Analysis.MorphBundlesOS.Count);
				Assert.AreEqual(newEntry4_expectedMatch.LexemeFormOA, guessActual.Analysis.MorphBundlesOS[0].MorphRA);
				Assert.AreEqual(newEntry4_expectedMatch.SensesOS[0], guessActual.Analysis.MorphBundlesOS[0].SenseRA);
				Assert.AreEqual(newEntry4_expectedMatch.SensesOS[0].MorphoSyntaxAnalysisRA, guessActual.Analysis.MorphBundlesOS[0].MsaRA);
				Assert.AreEqual(newEntry4_expectedMatch.SensesOS[0].Gloss.BestAnalysisAlternative.Text,
								guessActual.Analysis.MeaningsOC.First().Form.BestAnalysisAlternative.Text);
				Assert.AreEqual(setup.Pos_noun, guessActual.Analysis.CategoryRA);
				int cAnalyses = Cache.ServiceLocator.GetInstance<IWfiAnalysisRepository>().Count;
				Assert.AreEqual(1, cAnalyses, "Should have only generated one computer guess analysis.");
			}
		}

		/// <summary>
		/// make a variant entry (stem) that should be returned as a guess.
		/// </summary>
		[Test]
		public void ExpectedGuessForWord_MatchingVariantOfEntry()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache,
				AnalysisGuessBaseSetup.Flags.PartsOfSpeech, AnalysisGuessBaseSetup.Flags.VariantEntryTypes))
			{
				// create an affix entry
				var mainEntry = setup.EntryFactory.Create("aMain", "astem", SandboxGenericMSA.Create(MsaType.kStem, setup.Pos_noun));
				ILexEntryRef ler1 = mainEntry.CreateVariantEntryAndBackRef(setup.Vet_DialectalVariant, TsStringUtils.MakeString("a", Cache.DefaultVernWs));
				var variantOfEntry = ler1.OwnerOfClass<ILexEntry>();
				// try to generate analyses for matching entries (should have no results)
				setup.GuessServices.GenerateEntryGuesses(setup.StText);
				var guessVariantOfEntry = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreNotEqual(new NullWAG(), guessVariantOfEntry);
				Assert.AreEqual(1, guessVariantOfEntry.Analysis.MorphBundlesOS.Count);
				Assert.AreEqual(variantOfEntry.LexemeFormOA, guessVariantOfEntry.Analysis.MorphBundlesOS[0].MorphRA);
				Assert.AreEqual(mainEntry.SensesOS[0], guessVariantOfEntry.Analysis.MorphBundlesOS[0].SenseRA);
				Assert.AreEqual(mainEntry.SensesOS[0].MorphoSyntaxAnalysisRA, guessVariantOfEntry.Analysis.MorphBundlesOS[0].MsaRA);
				Assert.AreEqual(mainEntry.SensesOS[0].Gloss.BestAnalysisAlternative.Text,
								guessVariantOfEntry.Analysis.MeaningsOC.First().Form.BestAnalysisAlternative.Text);
				Assert.AreEqual(setup.Pos_noun, guessVariantOfEntry.Analysis.CategoryRA);
				int cAnalyses = Cache.ServiceLocator.GetInstance<IWfiAnalysisRepository>().Count;
				Assert.AreEqual(1, cAnalyses, "Should have only generated one computer guess analysis.");
			}
		}

		/// <summary>
		/// make an variant entry with its own sense/gloss that should be returned as a guess.
		/// </summary>
		[Test]
		public void ExpectedGuessForWord_MatchingVariantofSense()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache,
				AnalysisGuessBaseSetup.Flags.PartsOfSpeech, AnalysisGuessBaseSetup.Flags.VariantEntryTypes))
			{
				// create an affix entry
				var mainEntry = setup.EntryFactory.Create("aMain", "astem", SandboxGenericMSA.Create(MsaType.kStem, setup.Pos_noun));
				ILexEntryRef ler = mainEntry.SensesOS[0].CreateVariantEntryAndBackRef(setup.Vet_FreeVariant, TsStringUtils.MakeString("a", Cache.DefaultVernWs));
				var variantOfSense = ler.OwnerOfClass<ILexEntry>();
				// try to generate analyses for matching entries (should have no results)
				setup.GuessServices.GenerateEntryGuesses(setup.StText);
				var guessVariantOfSense = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreNotEqual(new NullWAG(), guessVariantOfSense);
				Assert.AreEqual(1, guessVariantOfSense.Analysis.MorphBundlesOS.Count);
				// (LT-9681) Not sure how the MorphRA/SenseRA/MsaRA data should be represented for this case.
				// typically for variants, MorphRA points to the variant, and SenseRA points to the primary entry's sense.
				// in this case, perhaps the SenseRA should point to the variant's sense.
				Assert.AreEqual(variantOfSense.LexemeFormOA, guessVariantOfSense.Analysis.MorphBundlesOS[0].MorphRA);
				Assert.AreEqual(mainEntry.SensesOS[0], guessVariantOfSense.Analysis.MorphBundlesOS[0].SenseRA);
				Assert.AreEqual(mainEntry.SensesOS[0].MorphoSyntaxAnalysisRA, guessVariantOfSense.Analysis.MorphBundlesOS[0].MsaRA);
				Assert.AreEqual(mainEntry.SensesOS[0].Gloss.BestAnalysisAlternative.Text,
								guessVariantOfSense.Analysis.MeaningsOC.First().Form.BestAnalysisAlternative.Text);
				Assert.AreEqual(setup.Pos_noun, guessVariantOfSense.Analysis.CategoryRA);
				int cAnalyses = Cache.ServiceLocator.GetInstance<IWfiAnalysisRepository>().Count;
				Assert.AreEqual(1, cAnalyses, "Should have only generated one computer guess analysis.");
			}
		}

		/// <summary>
		/// make an variant entry with its own sense/gloss that should be returned as a guess.
		/// </summary>
		[Ignore("support for LT-9681. Not sure how the MorphRA/SenseRA/MsaRA data should be represented for this case.")]
		[Test]
		public void ExpectedGuessForWord_MatchingVariantHavingSense()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache,
				AnalysisGuessBaseSetup.Flags.PartsOfSpeech, AnalysisGuessBaseSetup.Flags.VariantEntryTypes))
			{
				// create an affix entry
				var mainEntry = setup.EntryFactory.Create("aMain", "astem", SandboxGenericMSA.Create(MsaType.kStem, setup.Pos_noun));
				ILexEntryRef ler = mainEntry.CreateVariantEntryAndBackRef(setup.Vet_FreeVariant, TsStringUtils.MakeString("a", Cache.DefaultVernWs));
				var variantOfEntry = ler.OwnerOfClass<ILexEntry>();
				// make the variant have it's own gloss...should take precendence over main entry gloss info (cf. LT-9681)
				var senseFactory = Cache.ServiceLocator.GetInstance<ILexSenseFactory>();
				senseFactory.Create(variantOfEntry, SandboxGenericMSA.Create(MsaType.kStem, setup.Pos_verb), "variantOfSenseGloss");
				// try to generate analyses for matching entries (should have no results)
				setup.GuessServices.GenerateEntryGuesses(setup.StText);
				var guessVariantOfSense = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreNotEqual(new NullWAG(), guessVariantOfSense);
				Assert.AreEqual(1, guessVariantOfSense.Analysis.MorphBundlesOS.Count);
				// (LT-9681) Not sure how the MorphRA/SenseRA/MsaRA data should be represented for this case.
				// typically for variants, MorphRA points to the variant, and SenseRA points to the primary entry's sense.
				// in this case, perhaps the SenseRA should point to the variant's sense.
				Assert.AreEqual(variantOfEntry.LexemeFormOA, guessVariantOfSense.Analysis.MorphBundlesOS[0].MorphRA);
				Assert.AreEqual(variantOfEntry.SensesOS[0], guessVariantOfSense.Analysis.MorphBundlesOS[0].SenseRA);
				Assert.AreEqual(variantOfEntry.SensesOS[0].MorphoSyntaxAnalysisRA, guessVariantOfSense.Analysis.MorphBundlesOS[0].MsaRA);
				Assert.AreEqual(variantOfEntry.SensesOS[0].Gloss.BestAnalysisAlternative.Text,
								guessVariantOfSense.Analysis.MeaningsOC.First().Form.BestAnalysisAlternative.Text);
				Assert.AreEqual(setup.Pos_noun, guessVariantOfSense.Analysis.CategoryRA);
				int cAnalyses = Cache.ServiceLocator.GetInstance<IWfiAnalysisRepository>().Count;
				Assert.AreEqual(1, cAnalyses, "Should have only generated one computer guess analysis.");
			}
		}

		/// <summary>
		/// make generated entries for upper and lower case and return both for upper case word at beginning of sentence.
		/// </summary>
		[Test]
		public void ExpectedGuessForWord_GuessUpperAndLowerGenerated()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache,
				AnalysisGuessBaseSetup.Flags.PartsOfSpeech, AnalysisGuessBaseSetup.Flags.VariantEntryTypes))
			{
				// create an affix entry
				setup.EntryFactory.Create("a", "astem", SandboxGenericMSA.Create(MsaType.kStem, setup.Pos_noun));
				setup.EntryFactory.Create("A", "Astem", SandboxGenericMSA.Create(MsaType.kStem, setup.Pos_noun));
				AnalysisOccurrence occurrence = new AnalysisOccurrence(setup.Para0.SegmentsOS[0], 0);
				// GenerateEntryGuesses implicitly gets called.
				var sorted_analyses = setup.GuessServices.GetSortedAnalysisGuesses(occurrence.Analysis.Wordform, occurrence);
				// The uppercase wordform is preferred because it hasn't been lowercased.
				Assert.AreEqual(2, sorted_analyses.Count);
				Assert.AreEqual("A", sorted_analyses[0].Analysis.Wordform.ShortName);
				Assert.AreEqual("a", sorted_analyses[1].Analysis.Wordform.ShortName);
				// Test GetOriginalCaseWordform.
				// Set the analysis to the lowercase wordform.
				setup.Para0.SetAnalysis(0, 0, sorted_analyses[1]);
				setup.GuessServices.ClearGuessData();
				sorted_analyses = setup.GuessServices.GetSortedAnalysisGuesses(occurrence.Analysis.Wordform, occurrence);
				// We should still get the uppercase wordform as a guess.
				// The lowercase wordform is preferred because it is human-approved.
				Assert.AreEqual(2, sorted_analyses.Count);
				Assert.AreEqual("a", sorted_analyses[0].Analysis.Wordform.ShortName);
				Assert.AreEqual("A", sorted_analyses[1].Analysis.Wordform.ShortName);
			}
		}

		/// <summary>
		/// make generated entries for upper and lower case when only upper case is in corpus.
		/// </summary>
		[Test]
		public void ExpectedGuessForWord_GuessUpperAndLowerGenerated2()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache,
				AnalysisGuessBaseSetup.Flags.PartsOfSpeech, AnalysisGuessBaseSetup.Flags.VariantEntryTypes))
			{
				// create an affix entry
				setup.EntryFactory.Create("e", "astem", SandboxGenericMSA.Create(MsaType.kStem, setup.Pos_noun));
				setup.EntryFactory.Create("E", "Astem", SandboxGenericMSA.Create(MsaType.kStem, setup.Pos_noun));
				AnalysisOccurrence occurrence = new AnalysisOccurrence(setup.Para0.SegmentsOS[3], 0);
				// GenerateEntryGuesses implicitly gets called.
				var sorted_analyses = setup.GuessServices.GetSortedAnalysisGuesses(occurrence.Analysis.Wordform, occurrence);
				Assert.AreEqual(2, sorted_analyses.Count);
				Assert.AreEqual("E", sorted_analyses[0].Analysis.Wordform.ShortName);
				Assert.AreEqual("e", sorted_analyses[1].Analysis.Wordform.ShortName);
			}
		}

		/// <summary>
		/// make an approved analysis, expected to be a guess.
		/// </summary>
		[Test]
		public void ExpectedGuess_OneAnalysis_HumanApproves()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWag = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				setup.UserAgent.SetEvaluation(newWag.Analysis, Opinions.approves);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(newWag.Analysis, guessActual);
			}
		}

		/// <summary>
		/// make an human approved analysis (but parser disapproves), expected to be a guess.
		/// </summary>
		[Test]
		public void ExpectedGuess_OneAnalysis_HumanApproves_ParserDisapproves()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWag = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				setup.ParserAgent.SetEvaluation(newWag.Analysis, Opinions.disapproves);
				setup.UserAgent.SetEvaluation(newWag.Analysis, Opinions.approves);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(newWag.Analysis, guessActual);
			}
		}

		/// <summary>
		/// make a gloss with an approved analysis, expected to be a guess.
		/// </summary>
		[Test]
		public void ExpectedGuessForWord_OneGloss_HumanApprovesAnalysis()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWagGloss = WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[1]);
				setup.UserAgent.SetEvaluation(newWagGloss.WfiAnalysis, Opinions.approves);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(newWagGloss.Gloss, guessActual);
			}
		}

		/// <summary>
		/// make a gloss with an approved analysis, expected to be a guess.
		/// </summary>
		[Test]
		public void ExpectedGuessForAnalysis_OneGloss_HumanApprovesAnalysis()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWagGloss = WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[1]);
				setup.UserAgent.SetEvaluation(newWagGloss.WfiAnalysis, Opinions.approves);
				var guessActual = setup.GuessServices.GetBestGuess(newWagGloss.WfiAnalysis);
				Assert.AreEqual(newWagGloss.Gloss, guessActual);
			}
		}

		/// <summary>
		/// make an approved analysis, expected to be a guess.
		/// </summary>
		[Test]
		public void ExpectedGuess_OneAnalysis_ParserApproves()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWag = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				var newAnalysis = newWag.Analysis;
				setup.ParserAgent.SetEvaluation(newAnalysis, Opinions.approves);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(newWag.Analysis, guessActual);
			}
		}

		/// <summary>
		/// make an analysis with an "noopinion" evaluation, expected to be a guess.
		/// </summary>
		[Test]
		public void ExpectedGuess_OneAnalysis_NoOpinion()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWag = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				var newAnalysis = newWag.Analysis;
				setup.UserAgent.SetEvaluation(newAnalysis, Opinions.noopinion);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(newWag.Analysis, guessActual);
			}
		}

		/// <summary>
		/// make a gloss with analysis with an "noopinion" evaluation, expected to be a guess.
		/// </summary>
		[Test]
		public void ExpectedGuessForWord_OneGloss_NoOpinion()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWagGloss = WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[1]);
				setup.UserAgent.SetEvaluation(newWagGloss.WfiAnalysis, Opinions.noopinion); // should be equivalent to no evaluation.
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(newWagGloss.Gloss, guessActual);
			}
		}

		/// <summary>
		/// make a gloss with analysis with an "noopinion" evaluation, expected to be a guess.
		/// </summary>
		[Test]
		public void ExpectedGuessForAnalysis_OneGloss_NoOpinion()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWagGloss = WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[1]);
				setup.UserAgent.SetEvaluation(newWagGloss.WfiAnalysis, Opinions.noopinion); // should be equivalent to no evaluation.
				var guessActual = setup.GuessServices.GetBestGuess(newWagGloss.WfiAnalysis);
				Assert.AreEqual(newWagGloss.Gloss, guessActual);
			}
		}

		/// <summary>
		/// Not sure which to choose is right if they are all equally approved.
		/// Just make sure we return something.
		/// </summary>
		[Test]
		public void ExpectedGuess_MultipleAnalyses_EquallyApproved_NoneInTexts()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newAnalysisWag1 = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				var newAnalysisWag2 = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				var newAnalysis1 = newAnalysisWag1.Analysis;
				var newAnalysis2 = newAnalysisWag2.Analysis;
				setup.UserAgent.SetEvaluation(newAnalysis1, Opinions.approves);
				setup.UserAgent.SetEvaluation(newAnalysis2, Opinions.approves);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreNotEqual(new NullWAG(), guessActual);
			}
		}

		/// <summary>
		/// Prefer an analysis 'approved' to one created without an evaluation.
		/// </summary>
		[Test]
		public void ExpectedGuess_MultipleAnalyses_PreferOneApprovedToNoEvaluation()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWagEvaluation = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				setup.UserAgent.SetEvaluation(newWagEvaluation.Analysis, Opinions.approves);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(newWagEvaluation.Analysis, guessActual);
			}
		}

		/// <summary>
		/// Prefer a matching entry parser generated guess over a (computer) guess matching lexical entry.
		/// </summary>
		[Test]
		public void ExpectedGuess_MultipleAnalyses_PreferParserAgentGuessOverMatchingEntryGuess()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache, AnalysisGuessBaseSetup.Flags.PartsOfSpeech))
			{
				// create an affix entry
				setup.EntryFactory.Create("a", "astem", SandboxGenericMSA.Create(MsaType.kStem, setup.Pos_noun));
				// expect a guess to be generated
				setup.GuessServices.GenerateEntryGuesses(setup.StText);
				// create parser approved guess
				var newWagParserApproves = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				setup.ParserAgent.SetEvaluation(newWagParserApproves.Analysis, Opinions.approves);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(newWagParserApproves.Analysis, guessActual);
			}
		}

		/// <summary>
		/// Prefer an user agent analysis to one created as a matching entry
		/// </summary>
		[Test]
		public void ExpectedGuess_MultipleAnalyses_PreferUserAgentGuessOverMatchingEntryGuess()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache, AnalysisGuessBaseSetup.Flags.PartsOfSpeech))
			{
				// create an affix entry
				setup.EntryFactory.Create("a", "astem", SandboxGenericMSA.Create(MsaType.kStem, setup.Pos_noun));
				// expect a guess to be generated
				setup.GuessServices.GenerateEntryGuesses(setup.StText);
				// create user approved guess
				var newWagUserApproves = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				setup.UserAgent.SetEvaluation(newWagUserApproves.Analysis, Opinions.approves);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(newWagUserApproves.Analysis, guessActual);
			}
		}

		/// <summary>
		/// Prefer an analysis approved in a text to one approved outside of text.
		/// (All other things equal, it makes more sense when guessing for a wordform to use analyses
		/// approved in text than one approved in analyses.)
		/// </summary>
		[Test]
		public void ExpectedAnalysisGuess_MultipleAnalyses_PreferOneApprovedInText()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				var newWagInText = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				// set the analysis at the appropriate location to be the one we created.
				setup.Para0.SetAnalysis(0, 1, newWagInText.Analysis);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(newWagInText.Analysis, guessActual);
			}
		}

		/// <summary>
		/// Prefer an analysis 'approved' in the text to one created as a matching entry
		/// </summary>
		[Test]
		public void ExpectedGuess_MultipleAnalyses_PreferOneApprovedInTextToMatchingEntry()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache, AnalysisGuessBaseSetup.Flags.PartsOfSpeech))
			{
				// create an affix entry
				setup.EntryFactory.Create("a", "astem", SandboxGenericMSA.Create(MsaType.kStem, setup.Pos_noun));
				// expect a guess to be generated
				setup.GuessServices.GenerateEntryGuesses(setup.StText);
				// create user approved guess
				var newWagInText = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				// set the analysis at the appropriate location to be the one we created.
				setup.Para0.SetAnalysis(0, 1, newWagInText.Analysis);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(newWagInText.Analysis, guessActual);
			}
		}

		/// <summary>
		/// Prefer an analysis approved in a text to a gloss outside of text.
		/// (All other things equal, it makes more sense when guessing for a wordform to use analyses
		/// approved in text than one approved in analyses.)
		/// </summary>
		[Test]
		public void ExpectedAnalysisGuess_MultipleAnalyses_PreferAnalysisInText()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[1]);
				var newWagInText = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				// set the analysis at the appropriate location to be the one we created.
				setup.Para0.SetAnalysis(0, 1, newWagInText.Analysis);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(newWagInText.Analysis, guessActual);
			}
		}


		/// <summary>
		/// If a wordform is in a sentence initial position (and non-lowercase), consider
		/// the lowercase form.
		/// </summary>
		[Test]
		public void ExpectedAnalysisGuess_ForSentenceInitialPositionLowerCaseAlternative()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWagUppercase = WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[0]);
				var newWagLowercase = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				var uppercaseOccurrence = new AnalysisOccurrence(setup.Para0.SegmentsOS[0], 0);
				// There should be two possible analyses: one uppercase and one lowercase.
				var wordform = uppercaseOccurrence.Analysis.Wordform;
				var analyses = setup.GuessServices.GetSortedAnalysisGuesses(wordform, uppercaseOccurrence);
				Assert.AreEqual(analyses.Count, 2);
				// All else being equal, prefer the uppercase analysis.
				var guessActual = setup.GuessServices.GetBestGuess(uppercaseOccurrence);
				Assert.AreEqual(newWagUppercase.Analysis, guessActual);
				// If the lowercase has been selected, prefer the lowercase analysis.
				setup.Para0.SetAnalysis(0, 1, newWagLowercase);
				setup.GuessServices.ClearGuessData();
				guessActual = setup.GuessServices.GetBestGuess(uppercaseOccurrence);
				Assert.AreEqual(newWagLowercase.Analysis, guessActual);
			}

		}

		/// <summary>
		/// if a wordform is in a sentence initial position (and non-lowercase),
		/// default to looking for the upper case form when lower isn't found
		/// </summary>
		[Test]
		public void ExpectedAnalysisGuess_ForSentenceInitialPositionUpperCaseAlternative()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWagUppercase = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[0]);
				var wagUppercase = new AnalysisOccurrence(setup.Para0.SegmentsOS[0],0);
				var guessActual = setup.GuessServices.GetBestGuess(wagUppercase);
				Assert.AreEqual(newWagUppercase.Analysis, guessActual);
			}

		}

		/// <summary>
		/// if a wordform is in a sentence initial position and lowercase,
		/// don't default to looking for the upper case form when lower isn't found
		/// </summary>
		[Test]
		public void ExpectedAnalysisGuess_ForSentenceInitialOnlyLowercase()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[5]);
				var wagLowercaseB = new AnalysisOccurrence(setup.Para0.SegmentsOS[1], 0);
				var guessActual = setup.GuessServices.GetBestGuess(wagLowercaseB);
				Assert.AreEqual(new NullWAG(), guessActual);
			}
		}

		/// <summary>
		/// This class allows us to fake out the guesser by passing an analysis occurrence with the analyis we want,
		/// even though it isn't the analysis recorded in the paragraph.
		/// Since we haven't ensured consistency of any other properties (like baseline text), be careful how you use this.
		/// </summary>
		class TestModAnalysisOccurrence : AnalysisOccurrence
		{
			private IAnalysis m_trickAnalysis;
			public TestModAnalysisOccurrence(ISegment seg, int index, IAnalysis trickAnalysis) : base(seg, index)
			{
				m_trickAnalysis = trickAnalysis;
			}

			public override IAnalysis Analysis
			{
				get { return m_trickAnalysis; }
			}
		}

		/// <summary>
		/// if an uppercase wordform is in a sentence initial position and already has an analysis
		/// don't default to looking for the lowercase form
		/// </summary>
		[Test]
		public void ExpectedAnalysisGuess_ForSentenceInitialUppercaseWithAnalysis()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWagUppercase = WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[0]);
				WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[1]);
				var wagUppercase = new TestModAnalysisOccurrence(setup.Para0.SegmentsOS[0], 0, newWagUppercase.WfiAnalysis);
				var guessActual = setup.GuessServices.GetBestGuess(wagUppercase);
				Assert.AreEqual(newWagUppercase.Gloss, guessActual);
			}
		}

		/// <summary>
		///
		/// </summary>
		[Test]
		public void ExpectedAnalysisGuessForWord_GlossOfApprovedAnalysis()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWagApproves = WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[1]);
				// set the analysis at the appropriate location to be the one we created.
				setup.Para0.SetAnalysis(0, 1, newWagApproves.Gloss);
				setup.UserAgent.SetEvaluation(newWagApproves.WfiAnalysis, Opinions.approves);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(newWagApproves.Gloss, guessActual);
			}
		}

		/// <summary>
		///
		/// </summary>
		[Test]
		public void ExpectedAnalysisGuessForAnalysis_GlossOfApprovedAnalysis()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWagApproves = WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[1]);
				setup.Para0.SetAnalysis(0, 1, newWagApproves.Gloss);
				setup.UserAgent.SetEvaluation(newWagApproves.WfiAnalysis, Opinions.approves);
				var guessActual = setup.GuessServices.GetBestGuess(newWagApproves.WfiAnalysis);
				Assert.AreEqual(newWagApproves.Gloss, guessActual);
			}
		}

		/// <summary>
		///
		/// </summary>
		[Test]
		public void ExpectedAnalysisGuessForWord_PreferOneGlossOverOneAnalysis()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWagApproves = WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[1]);
				setup.Para0.SetAnalysis(0, 1, newWagApproves.WfiAnalysis);
				setup.Para0.SetAnalysis(0, 2, newWagApproves.Gloss);
				setup.UserAgent.SetEvaluation(newWagApproves.WfiAnalysis, Opinions.approves);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(newWagApproves.Gloss, guessActual);
			}
		}

		/// <summary>
		/// corner case
		/// </summary>
		[Test]
		public void ExpectedAnalysisGuessForAnalysis_PreferOneGlossOverOneAnalysis()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWagApproves = WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[1]);
				setup.Para0.SetAnalysis(0, 1, newWagApproves.WfiAnalysis);
				setup.Para0.SetAnalysis(0, 2, newWagApproves.Gloss);
				setup.UserAgent.SetEvaluation(newWagApproves.WfiAnalysis, Opinions.approves);
				var guessActual = setup.GuessServices.GetBestGuess(newWagApproves.WfiAnalysis);
				Assert.AreEqual(newWagApproves.Gloss, guessActual);
			}
		}

		/// <summary>
		///
		/// </summary>
		[Test]
		public void ExpectedAnalysisGuessForWord_PreferFrequentAnalysisOverLessFrequentGloss()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWagApproves = WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[1]);
				var newWagApproves2 = WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[1]);
				setup.Para0.SetAnalysis(0, 1, newWagApproves2.Gloss);
				setup.Para0.SetAnalysis(0, 2, newWagApproves.WfiAnalysis);
				setup.Para0.SetAnalysis(0, 3, newWagApproves.WfiAnalysis);
				setup.UserAgent.SetEvaluation(newWagApproves.WfiAnalysis, Opinions.approves);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(newWagApproves.Analysis, guessActual);
			}
		}

		/// <summary>
		///
		/// </summary>
		[Test]
		public void ExpectedAnalysisGuessForWord_GetMostCommonGlossOfMostCommonAnalysis()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWagApproves = WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[1]);
				setup.Para0.SetAnalysis(0, 1, newWagApproves.Gloss);
				setup.Para0.SetAnalysis(0, 2, newWagApproves.WfiAnalysis);
				setup.Para0.SetAnalysis(0, 3, newWagApproves.WfiAnalysis);
				setup.UserAgent.SetEvaluation(newWagApproves.WfiAnalysis, Opinions.approves);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(newWagApproves.Gloss, guessActual);
			}
		}

		/// <summary>
		///
		/// </summary>
		[Test]
		public void ExpectedAnalysisGuess_PreferFrequentGlossOverLessFrequentAnalysis()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWagApproves = WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[1]);
				setup.Para0.SetAnalysis(0, 1, newWagApproves.WfiAnalysis);
				setup.Para0.SetAnalysis(0, 2, newWagApproves.Gloss);
				setup.Para0.SetAnalysis(0, 3, newWagApproves.Gloss);
				setup.UserAgent.SetEvaluation(newWagApproves.WfiAnalysis, Opinions.approves);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(newWagApproves.Gloss, guessActual);
			}
		}

		/// <summary>
		///
		/// </summary>
		[Test]
		public void ExpectedAnalysisGuessForWord_PreferFrequentGlossOverLessFrequentGloss()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWagFrequentGloss = WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[1]);
				var newWagLessFrequentGloss = WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[1]);
				setup.Para0.SetAnalysis(0, 1, newWagLessFrequentGloss.Gloss);
				setup.Para0.SetAnalysis(0, 2, newWagFrequentGloss.Gloss);
				setup.Para0.SetAnalysis(0, 3, newWagFrequentGloss.Gloss);
				setup.UserAgent.SetEvaluation(newWagLessFrequentGloss.WfiAnalysis, Opinions.approves);
				setup.UserAgent.SetEvaluation(newWagFrequentGloss.WfiAnalysis, Opinions.approves);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(newWagFrequentGloss.Gloss, guessActual);
			}
		}

		/// <summary>
		///
		/// </summary>
		[Test]
		public void ExpectedAnalysisGuessForAnalysis_PreferFrequentGlossOverLessFrequentGloss()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWagFrequentGloss = WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[1]);
				var newWagLessFrequentGloss = WordAnalysisOrGlossServices.CreateNewAnalysisTreeGloss(setup.Words_para0[1]);
				setup.Para0.SetAnalysis(0, 1, newWagLessFrequentGloss.Gloss);
				setup.Para0.SetAnalysis(0, 2, newWagFrequentGloss.Gloss);
				setup.Para0.SetAnalysis(0, 3, newWagFrequentGloss.Gloss);
				setup.UserAgent.SetEvaluation(newWagLessFrequentGloss.WfiAnalysis, Opinions.approves);
				setup.UserAgent.SetEvaluation(newWagFrequentGloss.WfiAnalysis, Opinions.approves);
				var guessActual = setup.GuessServices.GetBestGuess(newWagFrequentGloss.WfiAnalysis);
				Assert.AreEqual(newWagFrequentGloss.Gloss, guessActual);
			}
		}

		/// <summary>
		/// Make sure we don't select the disapproved analysis
		/// </summary>
		[Test]
		public void ExpectedGuess_MultipleAnalyses_MostApprovedAfterDisapproved()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWagDisapproved = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				var newWagApproved = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				setup.UserAgent.SetEvaluation(newWagDisapproved.Analysis, Opinions.disapproves);
				setup.UserAgent.SetEvaluation(newWagApproved.Analysis, Opinions.approves);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(newWagApproved.Analysis, guessActual);
			}
		}

		/// <summary>
		/// </summary>
		[Test]
		public void ExpectedGuess_PreferUserApprovedAnalysisOverParserApprovedAnalysis()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var newWagParserApproves = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				var newWagHumanApproves = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(setup.Words_para0[1]);
				setup.ParserAgent.SetEvaluation(newWagParserApproves.Analysis, Opinions.approves);
				setup.UserAgent.SetEvaluation(newWagHumanApproves.Analysis, Opinions.approves);
				var guessActual = setup.GuessServices.GetBestGuess(setup.Words_para0[1]);
				Assert.AreEqual(newWagHumanApproves.Analysis, guessActual);
			}
		}

		/// <summary>
		/// Prefer analyses that are in the right context over analyses that are not.
		/// </summary>
		[Test]
		public void ExpectedContextAwareGuess_PreferContextedOverUncontexted()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var segment = setup.Para0.SegmentsOS[2];
				var uncontextedApprovedAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[1].Wordform).Analysis;
				var dAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[4].Wordform).Analysis;
				var contextedApprovedAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[5].Wordform).Analysis;
				// Analyses must be set in order.
				setup.Para0.SetAnalysis(2, 0, uncontextedApprovedAnalysis); // "c"
				setup.Para0.SetAnalysis(2, 1, uncontextedApprovedAnalysis); // "c"
				setup.Para0.SetAnalysis(2, 2, uncontextedApprovedAnalysis); // "c"
				setup.Para0.SetAnalysis(2, 3, uncontextedApprovedAnalysis); // "c"
				setup.Para0.SetAnalysis(2, 4, dAnalysis); // "d"
				setup.Para0.SetAnalysis(2, 5, contextedApprovedAnalysis); // "c"
				// Verify uncontexted guess.
				var wordform = segment.AnalysesRS[7].Wordform;
				var guessActual = setup.GuessServices.GetBestGuess(wordform);
				Assert.AreEqual(uncontextedApprovedAnalysis, guessActual);
				AnalysisOccurrence occurrence = new AnalysisOccurrence(segment, 7);
				// Make sure we get a contexted guess for occurrence instead of an uncontexted guess.
				guessActual = setup.GuessServices.GetBestGuess(occurrence);
				Assert.AreEqual(contextedApprovedAnalysis, guessActual);
				// Verify uncontexted guess for sort.
				var sorted_analyses = setup.GuessServices.GetSortedAnalysisGuesses(wordform, wordform.Cache.DefaultVernWs);
				Assert.AreEqual(2, sorted_analyses.Count);
				Assert.AreEqual(uncontextedApprovedAnalysis, sorted_analyses[0]);
				Assert.AreEqual(contextedApprovedAnalysis, sorted_analyses[1]);
				// Make sure the contexted guess is prioritized.
				sorted_analyses = setup.GuessServices.GetSortedAnalysisGuesses(wordform, occurrence);
				Assert.AreEqual(2, sorted_analyses.Count);
				Assert.AreEqual(contextedApprovedAnalysis, sorted_analyses[0]);
				Assert.AreEqual(uncontextedApprovedAnalysis, sorted_analyses[1]);
			}
		}

		/// <summary>
		/// Prefer analyses that are approved more often in the right context.
		/// </summary>
		[Test]
		public void ExpectedContextAwareGuess_PreferTwoContextedApprovedOverOneContextedApproved()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var segment = setup.Para0.SegmentsOS[2];
				var uncontextedApprovedAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[1].Wordform).Analysis;
				var dAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[4].Wordform).Analysis;
				var approvedAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[5].Wordform).Analysis;
				var approvedAnalysis2 = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[7].Wordform).Analysis;
				// Analyses must be set in order.
				// Add uncontexted analyses as a distractor.
				setup.Para0.SetAnalysis(2, 0, uncontextedApprovedAnalysis); // "c"
				setup.Para0.SetAnalysis(2, 1, uncontextedApprovedAnalysis); // "c"
				setup.Para0.SetAnalysis(2, 2, uncontextedApprovedAnalysis); // "c"
				setup.Para0.SetAnalysis(2, 3, uncontextedApprovedAnalysis); // "c"
				// Set up test.
				setup.Para0.SetAnalysis(2, 4, dAnalysis); // "d"
				setup.Para0.SetAnalysis(2, 5, approvedAnalysis.Analysis); // "c"
				setup.Para0.SetAnalysis(2, 7, approvedAnalysis2.Analysis); // "c"
				setup.Para0.SetAnalysis(2, 9, approvedAnalysis2.Analysis); // "c"
				// Check guess for occurrence.
				AnalysisOccurrence occurrence = new AnalysisOccurrence(segment, 11);
				var guessActual = setup.GuessServices.GetBestGuess(occurrence);
				Assert.AreEqual(approvedAnalysis2.Analysis, guessActual);
				// Check sorted analyses.
				var wordform = segment.AnalysesRS[11].Wordform;
				var sorted_analyses = setup.GuessServices.GetSortedAnalysisGuesses(wordform, occurrence);
				Assert.AreEqual(3, sorted_analyses.Count);
				Assert.AreEqual(approvedAnalysis2, sorted_analyses[0]);
				Assert.AreEqual(approvedAnalysis, sorted_analyses[1]);
				Assert.AreEqual(uncontextedApprovedAnalysis, sorted_analyses[2]);
			}
		}

		/// <summary>
		/// Prefer analyses that are approved in the right context over analyses that are human approved.
		/// </summary>
		[Test]
		public void ExpectedContextAwareGuess_PreferContextedApprovedOverHumanApproved()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var segment = setup.Para0.SegmentsOS[2];
				var uncontextedApprovedAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[1].Wordform).Analysis;
				var dAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[4].Wordform).Analysis;
				var approvedAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[5].Wordform).Analysis;
				var humanApprovedAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[7].Wordform).Analysis;
				// Analyses must be set in order.
				// Add uncontexted analyses as a distractor.
				setup.Para0.SetAnalysis(2, 0, uncontextedApprovedAnalysis); // "c"
				setup.Para0.SetAnalysis(2, 1, uncontextedApprovedAnalysis); // "c"
				setup.Para0.SetAnalysis(2, 2, uncontextedApprovedAnalysis); // "c"
				setup.Para0.SetAnalysis(2, 3, uncontextedApprovedAnalysis); // "c"
				// Set up test.
				setup.Para0.SetAnalysis(2, 4, dAnalysis); // "d"
				setup.Para0.SetAnalysis(2, 5, approvedAnalysis.Analysis); // "c"
				setup.UserAgent.SetEvaluation(humanApprovedAnalysis, Opinions.approves); // "c"
				// Check guess for occurrence.
				AnalysisOccurrence occurrence = new AnalysisOccurrence(segment, 11);
				var guessActual = setup.GuessServices.GetBestGuess(occurrence);
				Assert.AreEqual(approvedAnalysis.Analysis, guessActual);
				// Check sorted analyses.
				var wordform = segment.AnalysesRS[11].Wordform;
				var sorted_analyses = setup.GuessServices.GetSortedAnalysisGuesses(wordform, occurrence);
				Assert.AreEqual(3, sorted_analyses.Count);
				Assert.AreEqual(approvedAnalysis, sorted_analyses[0]);
				Assert.AreEqual(uncontextedApprovedAnalysis, sorted_analyses[1]);
				Assert.AreEqual(humanApprovedAnalysis, sorted_analyses[2]);
			}
		}

		/// <summary>
		/// Prefer analyses that are approved in the right context over analyses that are parser approved.
		/// </summary>
		[Test]
		public void ExpectedContextAwareGuess_PreferContextedApprovedOverParserApproved()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var segment = setup.Para0.SegmentsOS[2];
				var uncontextedApprovedAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[1].Wordform).Analysis;
				var dAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[4].Wordform).Analysis;
				var approvedAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[5].Wordform).Analysis;
				var parserApprovedAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[7].Wordform).Analysis;
				// Analyses must be set in order.
				// Add uncontexted analyses as a distractor.
				setup.Para0.SetAnalysis(2, 0, uncontextedApprovedAnalysis); // "c"
				setup.Para0.SetAnalysis(2, 1, uncontextedApprovedAnalysis); // "c"
				setup.Para0.SetAnalysis(2, 2, uncontextedApprovedAnalysis); // "c"
				setup.Para0.SetAnalysis(2, 3, uncontextedApprovedAnalysis); // "c"
				// Set up test.
				setup.Para0.SetAnalysis(2, 4, dAnalysis); // "d"
				setup.Para0.SetAnalysis(2, 5, approvedAnalysis.Analysis); // "c"
				setup.ParserAgent.SetEvaluation(parserApprovedAnalysis, Opinions.approves); // "c"
				// Check guess for occurrence.
				AnalysisOccurrence occurrence = new AnalysisOccurrence(segment, 11);
				var guessActual = setup.GuessServices.GetBestGuess(occurrence);
				Assert.AreEqual(approvedAnalysis.Analysis, guessActual);
				// Check sorted analyses.
				var wordform = segment.AnalysesRS[11].Wordform;
				var sorted_analyses = setup.GuessServices.GetSortedAnalysisGuesses(wordform, occurrence);
				Assert.AreEqual(3, sorted_analyses.Count);
				Assert.AreEqual(approvedAnalysis, sorted_analyses[0]);
				Assert.AreEqual(uncontextedApprovedAnalysis, sorted_analyses[1]);
				Assert.AreEqual(parserApprovedAnalysis, sorted_analyses[2]);
			}
		}

		/// <summary>
		/// Prefer analyses that are approved in the right context over analyses that are not approved.
		/// </summary>
		[Test]
		public void ExpectedContextAwareGuess_PreferContextedApprovedOverUnapproved()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var segment = setup.Para0.SegmentsOS[2];
				var uncontextedApprovedAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[1].Wordform).Analysis;
				var dAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[4].Wordform).Analysis;
				var approvedAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[5].Wordform).Analysis;
				var unapprovedAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[7].Wordform).Analysis;
				// Analyses must be set in order.
				// Add uncontexted analyses as a distractor.
				setup.Para0.SetAnalysis(2, 0, uncontextedApprovedAnalysis); // "c"
				setup.Para0.SetAnalysis(2, 1, uncontextedApprovedAnalysis); // "c"
				setup.Para0.SetAnalysis(2, 2, uncontextedApprovedAnalysis); // "c"
				setup.Para0.SetAnalysis(2, 3, uncontextedApprovedAnalysis); // "c"
				// Set up test.
				setup.Para0.SetAnalysis(2, 4, dAnalysis); // "d"
				setup.Para0.SetAnalysis(2, 5, approvedAnalysis.Analysis); // "c"
				// Check guess for occurrence.
				AnalysisOccurrence occurrence = new AnalysisOccurrence(segment, 11);
				var guessActual = setup.GuessServices.GetBestGuess(occurrence);
				Assert.AreEqual(approvedAnalysis.Analysis, guessActual);
				// Check sorted analyses.
				var wordform = segment.AnalysesRS[11].Wordform;
				var sorted_analyses = setup.GuessServices.GetSortedAnalysisGuesses(wordform, occurrence);
				Assert.AreEqual(3, sorted_analyses.Count);
				Assert.AreEqual(approvedAnalysis, sorted_analyses[0]);
				Assert.AreEqual(uncontextedApprovedAnalysis, sorted_analyses[1]);
				Assert.AreEqual(unapprovedAnalysis, sorted_analyses[2]);
			}
		}

		/// <summary>
		/// GetBestGuess should equal GetSortedAnalyses[0].
		/// </summary>
		[Test]
		public void ExpectedContextAwareGuess_CheckGuessWithSorted()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var segment = setup.Para0.SegmentsOS[2];
				var dAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[4].Wordform).Analysis;
				var approvedAnalysis1 = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[0].Wordform).Analysis;
				var approvedAnalysis2 = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[1].Wordform).Analysis;
				var approvedAnalysis3 = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[2].Wordform).Analysis;
				var approvedAnalysis4 = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[3].Wordform).Analysis;
				var approvedAnalysis5 = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[5].Wordform).Analysis;
				var approvedAnalysis6 = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[7].Wordform).Analysis;
				var approvedAnalysis7 = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[9].Wordform).Analysis;
				// Analyses must be set in order.
				// Create analyses with equal priority.
				setup.Para0.SetAnalysis(2, 0, approvedAnalysis7.Analysis); // "c"
				setup.Para0.SetAnalysis(2, 1, approvedAnalysis6.Analysis); // "c"
				setup.Para0.SetAnalysis(2, 2, approvedAnalysis5.Analysis); // "c"
				setup.Para0.SetAnalysis(2, 3, approvedAnalysis4.Analysis); // "c"
				setup.Para0.SetAnalysis(2, 4, dAnalysis); // "d"
				setup.Para0.SetAnalysis(2, 5, approvedAnalysis3.Analysis); // "c"
				setup.Para0.SetAnalysis(2, 7, approvedAnalysis2.Analysis); // "c"
				setup.Para0.SetAnalysis(2, 9, approvedAnalysis1.Analysis); // "c"
				// Check guess with sorted.
				var wordform = segment.AnalysesRS[11].Wordform;
				var guessActual = setup.GuessServices.GetBestGuess(wordform);
				var sorted_analyses = setup.GuessServices.GetSortedAnalysisGuesses(wordform, wordform.Cache.DefaultVernWs);
				Assert.AreEqual(guessActual, sorted_analyses[0]);
			}
		}

		/// <summary>
		/// Prefer gloss based on previous word ("river bank" vs. "financial bank").
		/// </summary>
		[Test]
		public void ExpectedContextAwareGloss_PreferContextedOverUncontexted()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var segment = setup.Para0.SegmentsOS[2];
				var servLoc = segment.Cache.ServiceLocator;
				var glossFactory = servLoc.GetInstance<IWfiGlossFactory>();
				var analysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[1].Wordform).Analysis;
				var dAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[4].Wordform).Analysis;
				var uncontextedApprovedGloss = glossFactory.Create();
				var contextedApprovedGloss = glossFactory.Create();
				analysis.MeaningsOC.Add(uncontextedApprovedGloss);
				analysis.MeaningsOC.Add(contextedApprovedGloss);
				// Analyses must be set in order.
				setup.Para0.SetAnalysis(2, 0, uncontextedApprovedGloss); // "c"
				setup.Para0.SetAnalysis(2, 1, uncontextedApprovedGloss); // "c"
				setup.Para0.SetAnalysis(2, 2, uncontextedApprovedGloss); // "c"
				setup.Para0.SetAnalysis(2, 3, uncontextedApprovedGloss); // "c"
				setup.Para0.SetAnalysis(2, 4, dAnalysis); // "d"
				setup.Para0.SetAnalysis(2, 5, contextedApprovedGloss); // "c"
				// Verify uncontexted guess.
				var wordform = segment.AnalysesRS[11].Wordform;
				var guessActual = setup.GuessServices.GetBestGuess(wordform);
				Assert.AreEqual(uncontextedApprovedGloss, guessActual);
				AnalysisOccurrence occurrence = new AnalysisOccurrence(segment, 11);
				// Make sure we get a contexted guess for occurrence instead of an uncontexted guess.
				guessActual = setup.GuessServices.GetBestGuess(occurrence);
				Assert.AreEqual(contextedApprovedGloss, guessActual);
				guessActual = setup.GuessServices.GetBestGuess(occurrence, includeContext: false);
				Assert.AreEqual(uncontextedApprovedGloss, guessActual);
				// Verify uncontexted guess for sort.
				var sorted_glosses = setup.GuessServices.GetSortedGlossGuesses(analysis);
				Assert.AreEqual(2, sorted_glosses.Count);
				Assert.AreEqual(uncontextedApprovedGloss, sorted_glosses[0]);
				Assert.AreEqual(contextedApprovedGloss, sorted_glosses[1]);
				// Make sure the contexted guess is prioritized.
				sorted_glosses = setup.GuessServices.GetSortedGlossGuesses(analysis, occurrence);
				Assert.AreEqual(2, sorted_glosses.Count);
				Assert.AreEqual(contextedApprovedGloss, sorted_glosses[0]);
				Assert.AreEqual(uncontextedApprovedGloss, sorted_glosses[1]);
			}
		}

		/// <summary>
		/// Prefer glosses that are approved more often in the right context.
		/// </summary>
		[Test]
		public void ExpectedContextAwareGloss_PreferTwoContextedOverOneContexted()
		{
			using (var setup = new AnalysisGuessBaseSetup(Cache))
			{
				var segment = setup.Para0.SegmentsOS[2];
				var servLoc = segment.Cache.ServiceLocator;
				var glossFactory = servLoc.GetInstance<IWfiGlossFactory>();
				var analysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[1].Wordform).Analysis;
				var dAnalysis = WordAnalysisOrGlossServices.CreateNewAnalysisWAG(segment.AnalysesRS[4].Wordform).Analysis;
				var uncontextedApprovedGloss = glossFactory.Create();
				var contextedApprovedGloss1 = glossFactory.Create();
				var contextedApprovedGloss2 = glossFactory.Create();
				analysis.MeaningsOC.Add(uncontextedApprovedGloss);
				analysis.MeaningsOC.Add(contextedApprovedGloss1);
				analysis.MeaningsOC.Add(contextedApprovedGloss2);
				// Analyses must be set in order.
				setup.Para0.SetAnalysis(2, 0, uncontextedApprovedGloss); // "c"
				setup.Para0.SetAnalysis(2, 1, uncontextedApprovedGloss); // "c"
				setup.Para0.SetAnalysis(2, 2, uncontextedApprovedGloss); // "c"
				setup.Para0.SetAnalysis(2, 3, uncontextedApprovedGloss); // "c"
				setup.Para0.SetAnalysis(2, 4, dAnalysis); // "d"
				setup.Para0.SetAnalysis(2, 5, contextedApprovedGloss1); // "c"
				setup.Para0.SetAnalysis(2, 7, contextedApprovedGloss2); // "c"
				setup.Para0.SetAnalysis(2, 9, contextedApprovedGloss2); // "c"
				AnalysisOccurrence occurrence = new AnalysisOccurrence(segment, 11);
				// Check guess.
				var guessActual = setup.GuessServices.GetBestGuess(occurrence);
				Assert.AreEqual(contextedApprovedGloss2, guessActual);
				// Check sorting.
				var sorted_glosses = setup.GuessServices.GetSortedGlossGuesses(analysis, occurrence);
				Assert.AreEqual(3, sorted_glosses.Count);
				Assert.AreEqual(contextedApprovedGloss2, sorted_glosses[0]);
				Assert.AreEqual(contextedApprovedGloss1, sorted_glosses[1]);
				Assert.AreEqual(uncontextedApprovedGloss, sorted_glosses[2]);
			}
		}

		[Test]
		public void TestProjects()
		{
			TestProjects("C:\\Users\\PC\\source\\repos\\FieldWorks\\DistFiles\\Projects");
		}

		private void TestProjects(string directory)
		{
			float count = 0;
			int correct = 0;
			int total = 0;
			foreach (string subdir in Directory.GetDirectories(directory))
			{
				foreach (string file in Directory.GetFiles(subdir, "*.fwdata"))
				{
					int pCorrect;
					int pTotal;
					int min = 5;
					int cutoff = 100;
					TestProject(subdir, file, min, cutoff, out pCorrect, out pTotal);
					if (pTotal < min) continue;
					correct += pCorrect;
					total += pTotal;
					count++;
				}
			}
			float ratio = (float)correct / (float)total;
			Console.WriteLine("overall correct: " + correct.ToString() + ", total: " + total.ToString() + " (" + (100 * ratio).ToString() + "%) for " + count + " projects");
		}

		private void TestProject(string projectsDirectory, string dbFileName, int min, int cutoff, out int outCorrect, out int outTotal)
		{
			int correct = 0;
			int total = 0;
			var projectId = new TestProjectId(BackendProviderType.kXML, dbFileName);
			var m_ui = new DummyLcmUI();
			var m_lcmDirectories = new TestLcmDirectories(projectsDirectory);
			using (var cache = LcmCache.CreateCacheFromExistingData(projectId, "en", m_ui, m_lcmDirectories, new LcmSettings(),
					new DummyProgressDlg()))
			{
				AnalysisGuessServices guesser = new AnalysisGuessServices(cache);
				IStTextRepository textRepository = cache.ServiceLocator.GetInstance<IStTextRepository>();
				foreach (IStText text in textRepository.AllInstances())
				{
					if (total == cutoff) break;
					foreach (IStTxtPara para in text.ParagraphsOS)
					{
						if (total == cutoff) break;
						foreach (var occurrence in SegmentServices.StTextAnnotationNavigator.GetWordformOccurrencesAdvancingInPara(para))
						{
							if (total == cutoff) break;
							var analysis = occurrence.Analysis;
							if (analysis is IWfiGloss)
							{
								NonUndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW(cache.ActionHandlerAccessor, () =>
								{
									guesser.ClearGuessData();
									guesser.IgnoreOccurrence = occurrence;
									occurrence.Analysis = analysis.Wordform;
									var bestGuess = guesser.GetBestGuess(occurrence);
									occurrence.Analysis = analysis;
									if (bestGuess == analysis)
										correct++;
									total++;
								});
							}
						}
					}
				}
			}
			outCorrect = correct;
			outTotal = total;
			if (total < min) return;
			float ratio = total == 0 ? 0 : (float)correct / (float)total;
			string name = dbFileName.Substring(projectsDirectory.Length + 1);
			Console.WriteLine("correct: " + correct.ToString() + ", total: " + total.ToString() + " (" + (100 * ratio).ToString() + "%): " + name);
		}
	}
}
