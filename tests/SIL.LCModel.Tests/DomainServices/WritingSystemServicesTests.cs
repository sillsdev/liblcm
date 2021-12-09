// Copyright (c) 2015-2021 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.DomainImpl;
using SIL.LCModel.Infrastructure;
using SIL.LCModel.Infrastructure.Impl;

namespace SIL.LCModel.DomainServices
{
	/// <summary>
	/// Test (currently only parts of) WritingSystemServices
	/// </summary>
	[TestFixture]
	public class WritingSystemServicesTests : MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{

		/// <summary/>
		/// [Test]
		public void UpdateWritingSystemListField_DoesNothingIfNotFound()
		{
			Cache.LangProject.AnalysisWss = "fr en qaa-x-kal";
			WritingSystemServices.UpdateWritingSystemListField(Cache, Cache.LangProject, LangProjectTags.kflidAnalysisWss, "de",
				"de-NO");
			Assert.That(Cache.LangProject.AnalysisWss, Is.EqualTo("fr en qaa-x-kal"));
		}

		/// <summary/>
		[Test]
		public void UpdateWritingSystemListField_ReplacesNonDuplicateCode()
		{
			Cache.LangProject.AnalysisWss = "fr en qaa-x-kal";
			WritingSystemServices.UpdateWritingSystemListField(Cache, Cache.LangProject, LangProjectTags.kflidAnalysisWss, "fr",
				"de-NO");
			Assert.That(Cache.LangProject.AnalysisWss, Is.EqualTo("de-NO en qaa-x-kal"));
		}

		/// <summary/>
		[Test]
		public void GetMagicStringAlt_TestFirstAnaly()
		{
			int wsId;
			var entry = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
			var sense = Cache.ServiceLocator.GetInstance<ILexSenseFactory>().Create();
			entry.SensesOS.Add(sense);

			//SUT magic gets the default analysis when there are no others
			WritingSystemServices.GetMagicStringAlt(Cache, Cache.MainCacheAccessor,
				WritingSystemServices.kwsFirstAnal, sense.Hvo, sense.Definition.Flid, false, out wsId);
			Assert.AreEqual(wsId, Cache.DefaultAnalWs, "Did not get the default analysis when there are no others.");

			CoreWritingSystemDefinition frWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "fr", false, false, out frWs);
			var frId = frWs.Handle;
			CoreWritingSystemDefinition enWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "en", false, false, out enWs);
			var enId = enWs.Handle;
			CoreWritingSystemDefinition ptWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "pt", false, false, out ptWs);
			var ptId = ptWs.Handle;
			Cache.LangProject.CurrentAnalysisWritingSystems.Clear();
			Cache.LangProject.AddToCurrentAnalysisWritingSystems(frWs);
			Cache.LangProject.AddToCurrentAnalysisWritingSystems(enWs);
			Cache.LangProject.AnalysisWritingSystems.Add(ptWs);
			sense.Definition.set_String(frId, TsStringUtils.MakeString("fr", frId));
			sense.Definition.set_String(enId, TsStringUtils.MakeString("en", enId));
			sense.Definition.set_String(ptId, TsStringUtils.MakeString("pt", ptId));

			//SUT magic gets first analysis when there is one.
			WritingSystemServices.GetMagicStringAlt(Cache, Cache.MainCacheAccessor,
																 WritingSystemServices.kwsFirstAnal, sense.Hvo, sense.Definition.Flid, false, out wsId);
			Assert.AreEqual(wsId, frId, "Did not pull first analysis language first.");
			//SUT magic gets second analysis when the first is empty
			sense.Definition.set_String(frId, TsStringUtils.EmptyString(frId)); //wipe french
			WritingSystemServices.GetMagicStringAlt(Cache, Cache.MainCacheAccessor,
																 WritingSystemServices.kwsFirstAnal, sense.Hvo, sense.Definition.Flid, false, out wsId);
			Assert.AreEqual(wsId, enId, "Did not pull second analysis language when first was empty.");
			//SUT magic gets non current analysis when all current analysis languages are empty
			sense.Definition.set_String(enId, TsStringUtils.EmptyString(enId)); //wipe english
			WritingSystemServices.GetMagicStringAlt(Cache, Cache.MainCacheAccessor,
																 WritingSystemServices.kwsFirstAnal, sense.Hvo, sense.Definition.Flid, false, out wsId);
			Assert.AreEqual(wsId, ptId, "Did not pull from non current analysis language when all current languages were empty.");
		}

		/// <summary/>
		[Test]
		public void GetMagicStringAlt_TestFirstVernOrAnaly()
		{
			CoreWritingSystemDefinition senWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "sen", false, false, out senWs);
			var senId = senWs.Handle;
			CoreWritingSystemDefinition mluWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "mlu", false, false, out mluWs);
			var mluId = mluWs.Handle;
			CoreWritingSystemDefinition sekWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "sek", false, false, out sekWs);
			var sekId = sekWs.Handle;
			Cache.LangProject.CurrentVernacularWritingSystems.Clear();
			Cache.LangProject.AddToCurrentVernacularWritingSystems(mluWs);
			Cache.LangProject.AddToCurrentVernacularWritingSystems(senWs);
			Cache.LangProject.VernacularWritingSystems.Add(sekWs);
			CoreWritingSystemDefinition frWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "fr", false, false, out frWs);
			var frId = frWs.Handle;
			CoreWritingSystemDefinition enWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "en", false, false, out enWs);
			var enId = enWs.Handle;
			CoreWritingSystemDefinition ptWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "pt", false, false, out ptWs);
			var ptId = ptWs.Handle;
			Cache.LangProject.CurrentAnalysisWritingSystems.Clear();
			Cache.LangProject.AddToCurrentAnalysisWritingSystems(frWs);
			Cache.LangProject.AddToCurrentAnalysisWritingSystems(enWs);
			Cache.LangProject.AnalysisWritingSystems.Add(ptWs);
			var entry = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
			var sense = Cache.ServiceLocator.GetInstance<ILexSenseFactory>().Create();
			entry.SensesOS.Add(sense);
			sense.Definition.set_String(frId, TsStringUtils.MakeString("fr", frId));
			sense.Definition.set_String(enId, TsStringUtils.MakeString("en", enId));
			sense.Definition.set_String(ptId, TsStringUtils.MakeString("pt", ptId));
			entry.CitationForm.set_String(mluId, TsStringUtils.MakeString("To'abaita", mluId));
			entry.CitationForm.set_String(senId, TsStringUtils.MakeString("Sena", senId));
			entry.CitationForm.set_String(sekId, TsStringUtils.MakeString("Sekani", sekId));
			int wsId;
			//SUT magic gets first analysis when there is one.
			WritingSystemServices.GetMagicStringAlt(Cache, Cache.MainCacheAccessor,
																 WritingSystemServices.kwsFirstAnal, sense.Hvo, sense.Definition.Flid, false, out wsId);
			Assert.AreEqual(wsId, frId, "Did not pull first analysis language first.");
			//SUT magic gets second analysis when the first is empty
			sense.Definition.set_String(frId, TsStringUtils.EmptyString(frId)); //wipe french
			WritingSystemServices.GetMagicStringAlt(Cache, Cache.MainCacheAccessor,
																 WritingSystemServices.kwsFirstAnal, sense.Hvo, sense.Definition.Flid, false, out wsId);
			Assert.AreEqual(wsId, enId, "Did not pull second analysis language when first was empty.");
			//SUT magic gets non current analysis when all current analysis languages are empty
			sense.Definition.set_String(enId, TsStringUtils.EmptyString(enId)); //wipe english
			WritingSystemServices.GetMagicStringAlt(Cache, Cache.MainCacheAccessor,
																 WritingSystemServices.kwsFirstAnal, sense.Hvo, sense.Definition.Flid, false, out wsId);
			Assert.AreEqual(wsId, ptId, "Did not pull from non current analysis language when all current languages were empty.");
			//SUT magic gets first vernacular when there is one.
			WritingSystemServices.GetMagicStringAlt(Cache, Cache.MainCacheAccessor,
																 WritingSystemServices.kwsFirstVern, entry.Hvo, entry.CitationForm.Flid, false, out wsId);
			Assert.AreEqual(wsId, mluId, "Did not pull first vernacular language first.");
			//SUT magic gets second vernacular when the first is empty
			entry.CitationForm.set_String(mluId, TsStringUtils.EmptyString(mluId)); //wipe Sena
			WritingSystemServices.GetMagicStringAlt(Cache, Cache.MainCacheAccessor,
																 WritingSystemServices.kwsFirstVern, entry.Hvo, entry.CitationForm.Flid, false, out wsId);
			Assert.AreEqual(wsId, senId, "Did not pull second vernacular language when first was empty.");
			//SUT magic gets non current vernacular when all current analysis languages are empty
			entry.CitationForm.set_String(senId, TsStringUtils.EmptyString(senId)); //wipe To'abaita
			WritingSystemServices.GetMagicStringAlt(Cache, Cache.MainCacheAccessor,
																 WritingSystemServices.kwsFirstVern, entry.Hvo, entry.CitationForm.Flid, false, out wsId);
			Assert.AreEqual(wsId, sekId, "Did not pull from non current vernacular language when all current languages were empty.");
		}

		/// <summary/>
		[Test]
		public void GetMagicStringAlt_TestFirstVern()
		{
			int wsId;
			var entry = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();

			//SUT magic gets the default vernacular when there are no others
			WritingSystemServices.GetMagicStringAlt(Cache, Cache.MainCacheAccessor,
				WritingSystemServices.kwsFirstVern, entry.Hvo, entry.CitationForm.Flid, false, out wsId);
			Assert.AreEqual(wsId, Cache.DefaultVernWs, "Did not get the default vernacular when there are no others.");

			CoreWritingSystemDefinition mluWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "mlu", false, false, out mluWs);
			var mluId = mluWs.Handle;
			CoreWritingSystemDefinition senWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "sen", false, false, out senWs);
			var senId = senWs.Handle;
			CoreWritingSystemDefinition sekWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "sek", false, false, out sekWs);
			var sekId = sekWs.Handle;
			Cache.LangProject.CurrentVernacularWritingSystems.Clear();
			Cache.LangProject.AddToCurrentVernacularWritingSystems(mluWs);
			Cache.LangProject.AddToCurrentVernacularWritingSystems(senWs);
			Cache.LangProject.VernacularWritingSystems.Add(sekWs);
			entry.CitationForm.set_String(mluId, TsStringUtils.MakeString("To'abaita", mluId));
			entry.CitationForm.set_String(senId, TsStringUtils.MakeString("Sena", senId));
			entry.CitationForm.set_String(sekId, TsStringUtils.MakeString("Sekani", sekId));

			//SUT magic gets first vernacular when there is one.
			WritingSystemServices.GetMagicStringAlt(Cache, Cache.MainCacheAccessor,
																 WritingSystemServices.kwsFirstVern, entry.Hvo, entry.CitationForm.Flid, false, out wsId);
			Assert.AreEqual(wsId, mluId, "Did not pull first vernacular language first.");
			//SUT magic gets second vernacular when the first is empty
			entry.CitationForm.set_String(mluId, TsStringUtils.EmptyString(mluId)); //wipe Sena
			WritingSystemServices.GetMagicStringAlt(Cache, Cache.MainCacheAccessor,
																 WritingSystemServices.kwsFirstVern, entry.Hvo, entry.CitationForm.Flid, false, out wsId);
			Assert.AreEqual(wsId, senId, "Did not pull second vernacular language when first was empty.");
			//SUT magic gets non current vernacular when all current vernacular languages are empty
			entry.CitationForm.set_String(senId, TsStringUtils.EmptyString(senId)); //wipe To'abaita
			WritingSystemServices.GetMagicStringAlt(Cache, Cache.MainCacheAccessor,
																 WritingSystemServices.kwsFirstVernOrAnal, entry.Hvo, entry.CitationForm.Flid, false, out wsId);
			Assert.AreEqual(wsId, sekId, "Did not pull from non current vernacular language when all current languages were empty.");
		}

		/// <summary/>
		[Test]
		public void GetMagicStringAlt_TestFirstPronunciation()
		{
			CoreWritingSystemDefinition mluWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "mlu", false, false, out mluWs);
			var mluId = mluWs.Handle;
			CoreWritingSystemDefinition senWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "sen", false, false, out senWs);
			var senId = senWs.Handle;
			Cache.LangProject.CurrentPronunciationWritingSystems.Clear();
			Cache.LangProject.CurrentPronunciationWritingSystems.Add(mluWs);
			Cache.LangProject.CurrentPronunciationWritingSystems.Add(senWs);
			var entry = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
			entry.CitationForm.set_String(mluId, TsStringUtils.MakeString("To'abaita", mluId));
			entry.CitationForm.set_String(senId, TsStringUtils.MakeString("Sena", senId));
			int wsId;
			//SUT magic gets first pronuciation when there is one.
			WritingSystemServices.GetMagicStringAlt(Cache, Cache.MainCacheAccessor,
																 WritingSystemServices.kwsFirstPronunciation, entry.Hvo, entry.CitationForm.Flid, false, out wsId);
			Assert.AreEqual(wsId, mluId, "Did not pull first pronuciation language first.");
			//SUT magic gets second pronuciation when the first is empty
			entry.CitationForm.set_String(mluId, TsStringUtils.EmptyString(mluId)); //wipe Sena
			WritingSystemServices.GetMagicStringAlt(Cache, Cache.MainCacheAccessor,
																 WritingSystemServices.kwsFirstPronunciation, entry.Hvo, entry.CitationForm.Flid, false, out wsId);
			Assert.AreEqual(wsId, senId, "Did not pull second pronuciation language when first was empty.");
		}

		/// <summary>The first vernacular WS in a given paragraph. Not the most-prevalent, not the default, not the first analysis.</summary>
		[Test]
		public void GetMagicStringAlt_TestVernInPara()
		{
			// set up project writing systems
			Cache.LangProject.CurrentVernacularWritingSystems.Clear();
			Cache.LangProject.CurrentAnalysisWritingSystems.Clear();
			CoreWritingSystemDefinition mluWs, senWs, engWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "mlu", false, true, out mluWs);
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "sen", false, true, out senWs);
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "en", true, false, out engWs);
			Cache.LangProject.AddToCurrentVernacularWritingSystems(mluWs);
			Cache.LangProject.AddToCurrentVernacularWritingSystems(senWs);
			Cache.LangProject.AddToCurrentAnalysisWritingSystems(engWs);
			var mluId = mluWs.Handle;
			var senId = senWs.Handle;
			var engId = engWs.Handle;

			// set up a paragraph that starts with Anal and ends with Vern
			var tssf = new TsStrFactory();
			var tssb = tssf.GetBldr();
			tssb.Append(tssf.MakeString("Analysis ", engId));
			tssb.Append(tssf.MakeString("Vernacular ", senId));
			tssb.Append(tssf.MakeString("Other Vernacular ", mluId));
			var paraBldr = Cache.ServiceLocator.GetInstance<StTxtParaBldr>();
			var stText = Cache.ServiceLocator.GetInstance<IStTextFactory>().Create();
			Cache.ServiceLocator.GetInstance<ITextFactory>().Create().ContentsOA = stText; // needed to put a Cache in stText
			var para = paraBldr.CreateParagraph(stText);
			para.Contents = tssb.GetString();

			// SUT
			int wsId;
			WritingSystemServices.GetMagicStringAlt(Cache, Cache.MainCacheAccessor, WritingSystemServices.kwsVernInParagraph, para.Hvo, 0, false, out wsId);
			Assert.AreEqual(senId, wsId, "The first vernacular WS in the para is 'sen'");
		}

		/// <summary>
		/// For LT-12274. "Fr-Tech 30Oct" should convert to fr-x-Tech30Oc.
		/// (Fr-x-Tech-30Oct or Fr-Qaaa-x-Tech-30Oct might be better, but this is last-resort handling for a code we don't really understand;
		/// main thing is the result is a valid code that is recognizably derived from the original.
		/// </summary>
		[Test]
		public void FindOrCreateSomeWritingSystem_Converts_Fr_Tech_30Oct_To_fr_x_Tech30Oct()
		{
			CoreWritingSystemDefinition ws;
			Assert.That(WritingSystemServices.FindOrCreateSomeWritingSystem(Cache, null, "Fr-Tech 30Oct", true, false, out ws), Is.False);
			Assert.That(ws.Id, Is.EqualTo("fr-x-Tech30Oc")); //8 characters is the maximum allowed for a part.
		}

		/// <summary>
		/// Special case for a plain x.
		/// </summary>
		[Test]
		public void FindOrCreateSomeWritingSystem_Converts_x_To_qaa()
		{
			CoreWritingSystemDefinition ws;
			Assert.That(WritingSystemServices.FindOrCreateSomeWritingSystem(Cache, null, "x", true, false, out ws), Is.False);
			Assert.That(ws.Id, Is.EqualTo("qaa"));
		}

		/// <summary/>
		[Test]
		public void UpdateWritingSystemListField_RemovesMergedCodeAfterMergeWith()
		{
			Cache.LangProject.AnalysisWss = "fr en fr-NO";
			WritingSystemServices.UpdateWritingSystemListField(Cache, Cache.LangProject, LangProjectTags.kflidAnalysisWss, "fr-NO",
				"fr");
			Assert.That(Cache.LangProject.AnalysisWss, Is.EqualTo("fr en"));
		}

		/// <summary/>
		[Test]
		public void FindAllWritingSystemsWithData_FindsHiddenWritingSystems()
		{
			var wsMgr = Cache.ServiceLocator.WritingSystemManager;
			wsMgr.GetOrSet("en", out var en);
			wsMgr.GetOrSet("fr", out var fr);
			wsMgr.GetOrSet("fr-x-has-no-strings", out _);
			wsMgr.GetOrSet("fr-x-zero-length", out var zeroLength);
			wsMgr.GetOrSet("blz", out var blz);
			wsMgr.GetOrSet("hid", out var hid);
			wsMgr.GetOrSet("hid-x-embedded", out var hidEmbedded);
			wsMgr.GetOrSet("hid-x-edgeCase", out var hidEdgeCase);
			wsMgr.GetOrSet("hid-x-baselineText", out var hidBaseline);
			var entry = SenseOrEntryTests.CreateInterestingLexEntry(Cache);
			entry.CitationForm.set_String(hid.Handle, "Headword");
			entry.CitationForm.set_String(zeroLength.Handle, string.Empty);
			var exampleBldr = new TsStrBldr().Append("Example ", blz.Handle).Append("with embedded WS", hidEmbedded.Handle).Append("!", blz.Handle);
			var example = Cache.ServiceLocator.GetInstance<ILexExampleSentenceFactory>().Create();
			entry.SensesOS.First().ExamplesOS.Add(example);
			// The Example MultiString has an Edge Case alternative, but no Edge Case text.
			example.Example.set_String(hidEdgeCase.Handle, exampleBldr.GetString());
			// Interlinear texts are monolingual strings.
			AddInterlinearTextToLangProj("Title").ContentsOA.AddNewTextPara(null).Contents = TsStringUtils.MakeString("Content", hidBaseline.Handle);

			// SUT
			var result = WritingSystemServices.FindAllWritingSystemsWithText(Cache);

			Assert.That(new SortedSet<int>(result), Is.EquivalentTo(new[]
			{
				en.Handle, fr.Handle, blz.Handle, hid.Handle, hidEmbedded.Handle, hidEdgeCase.Handle, hidBaseline.Handle
			}));
		}

		/// <summary/>
		[Test]
		public void DeleteWritingSystem()
		{
			Cache.ServiceLocator.WritingSystemManager.GetOrSet("fr", out var wsFr);
			Cache.ServiceLocator.WritingSystemManager.GetOrSet("blz", out var wsBlz);

			var revIndex = Cache.ServiceLocator.GetInstance<IReversalIndexRepository>().FindOrCreateIndexForWs(wsBlz.Handle);
			Cache.LangProject.LexDbOA.ReversalIndexesOC.Add(revIndex);
			var lexEntry = SenseOrEntryTests.CreateInterestingLexEntry(Cache);

			lexEntry.CitationForm.set_String(wsBlz.Handle, "Citation");
			var example = Cache.ServiceLocator.GetInstance<ILexExampleSentenceFactory>().Create();
			lexEntry.SensesOS.First().ExamplesOS.Add(example);
			var exampleBldr = new TsStrBldr().Append("Example embedding", wsFr.Handle).Append("Balantak!", wsBlz.Handle);
			example.Example.set_String(wsFr.Handle, exampleBldr.GetString());

			var revEntry = revIndex.FindOrCreateReversalEntry("first");
			revEntry.SensesRS.Add(lexEntry.SensesOS.First());

			revEntry.ReversalIndex.WritingSystem = "blz";
			revEntry.ReversalForm.set_String(wsBlz.Handle, "blz");
			Assert.That(revEntry.ReversalIndex.WritingSystem, Is.EqualTo("blz"));
			// SUT
			WritingSystemServices.DeleteWritingSystem(Cache, wsBlz);
			TsStringUtilsTests.AssertIsNullOrEmpty(lexEntry.CitationForm.get_String(wsBlz.Handle));
			var exampleAfter = example.Example;
			TsStringUtilsTests.AssertIsNullOrEmpty(exampleAfter.get_String(wsBlz.Handle));
			var exampleAfterFr = exampleAfter.get_String(wsFr.Handle);
			Assert.AreEqual("Example embedding", exampleAfterFr.Text);
			Assert.AreEqual(1, exampleAfterFr.RunCount);
			Assert.AreEqual(wsFr.Handle, exampleAfterFr.get_WritingSystemAt(0));
			Assert.IsFalse(revEntry.IsValidObject);
			Assert.IsFalse(Cache.LangProject.LexDbOA.ReversalIndexesOC.Contains(revIndex));
		}

		/// <summary/>
		[Test]
		public void UpdateWritingSystemListField_RemovesMergedCodeBeforeMergeWith()
		{
			Cache.LangProject.AnalysisWss = "fr-NO en fr";
			WritingSystemServices.UpdateWritingSystemListField(Cache, Cache.LangProject, LangProjectTags.kflidAnalysisWss, "fr-NO",
				"fr");
			Assert.That(Cache.LangProject.AnalysisWss, Is.EqualTo("en fr"));
		}

		/// <summary/>
		public void UpdateWritingSystemListField_RemovesWsCode()
		{
			var wsEn = Cache.WritingSystemFactory.GetWsFromStr("en");
			var wsFr = Cache.WritingSystemFactory.GetWsFromStr("fr");

			Cache.LangProject.HomographWs = "fr";
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "blz", false, false, out _);

			var revIndex = Cache.ServiceLocator.GetInstance<IReversalIndexRepository>().FindOrCreateIndexForWs(wsEn);

			var entry1 = SenseOrEntryTests.CreateInterestingLexEntry(Cache);
			var msa1 = Cache.ServiceLocator.GetInstance<IMoStemMsaFactory>().Create();
			entry1.MorphoSyntaxAnalysesOC.Add(msa1);
			entry1.SensesOS.First().MorphoSyntaxAnalysisRA = msa1;

			var entry2 = SenseOrEntryTests.CreateInterestingLexEntry(Cache);
			var msa2 = Cache.ServiceLocator.GetInstance<IMoStemMsaFactory>().Create();
			entry2.MorphoSyntaxAnalysesOC.Add(msa2);
			entry2.SensesOS.First().MorphoSyntaxAnalysisRA = msa2;

			var testEntry = revIndex.FindOrCreateReversalEntry("first");
			testEntry.SensesRS.Add(entry1.SensesOS.First());
			testEntry.SensesRS.Add(entry2.SensesOS.First());

			testEntry.ReversalIndex.WritingSystem = "fr";
			testEntry.ReversalForm.set_String(wsFr, "fr");
			WritingSystemServices.UpdateWritingSystemFields(Cache, "fr", "blz");
			Assert.DoesNotThrow(() => WritingSystemServices.UpdateWritingSystemFields(Cache, "fr", null));
			Assert.That(testEntry.ReversalIndex.WritingSystem, Is.EqualTo("blz"));
			Assert.That(testEntry.ReversalIndex.ShortName, Is.EqualTo("Balantak"));
			Assert.That(Cache.LangProject.HomographWs, Is.Null);
		}

		/// <summary>
		/// Test that UpdateWritingSystemTag marks things as dirty if they use a problem WS.
		/// </summary>
		[Test]
		public void UpdateWritingSystemTag_ChangesWsContent()
		{
			var entry0 = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
			CoreWritingSystemDefinition newWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "en-SU", true, false, out newWs);
			// A string property NOT using the WS we will change.
			entry0.ImportResidue = TsStringUtils.MakeString("hello", Cache.DefaultAnalWs);
			// A multilingual one using the WS.
			entry0.LiteralMeaning.set_String(Cache.DefaultAnalWs, TsStringUtils.MakeString("whatever", Cache.DefaultAnalWs));

			var entry1 = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
			var sense1 = Cache.ServiceLocator.GetInstance<ILexSenseFactory>().Create();
			entry1.SensesOS.Add(sense1);
			// Sense1 should be dirty: it has a gloss in the changing WS.
			sense1.Gloss.set_String(newWs.Handle, TsStringUtils.MakeString("whatever", newWs.Handle));

			// Entry2 should be dirty: it has a string property with a run in the changing WS.
			var entry2 = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
			var bldr = TsStringUtils.MakeString("abc ", Cache.DefaultAnalWs).GetBldr();
			bldr.ReplaceTsString(bldr.Length, bldr.Length, TsStringUtils.MakeString("def", newWs.Handle));
			var stringWithNewWs = bldr.GetString();
			entry2.ImportResidue = stringWithNewWs;

			// Sense3 should be dirty: it has a multistring string property with a run in the changing WS.
			var entry3 = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
			var sense3 = Cache.ServiceLocator.GetInstance<ILexSenseFactory>().Create();
			entry3.SensesOS.Add(sense3);
			var styledAndNormalRunInChangingWs = TsStringUtils.MakeString("changing", newWs.Handle);
			styledAndNormalRunInChangingWs.Insert(0, TsStringUtils.MakeString("8", newWs.Handle, "Verse Number"));
			sense3.Definition.set_String(newWs.Handle, styledAndNormalRunInChangingWs);

			Cache.LangProject.AnalysisWss = "en en-SU";
			// Add Free Translation in the changing ws
			var paraBldr = Cache.ServiceLocator.GetInstance<StTxtParaBldr>();
			var stText = Cache.ServiceLocator.GetInstance<IStTextFactory>().Create();
			Cache.ServiceLocator.GetInstance<ITextFactory>().Create().ContentsOA = stText; // needed to put a Cache in stText
			var para = paraBldr.CreateParagraph(stText);
			para.Contents = TsStringUtils.MakeString("vernacular", Cache.DefaultVernWs);
			para.SegmentsOS[0].FreeTranslation.set_String(newWs.Handle, "Free Willy!");
			m_actionHandler.EndUndoTask();
			var undoManager = Cache.ServiceLocator.GetInstance<IUndoStackManager>();
			undoManager.Save(); // makes everything non-dirty.

			var newbies = new HashSet<ICmObjectId>();
			var dirtballs = new HashSet<ICmObjectOrSurrogate>(new ObjectSurrogateEquater());
			var goners = new HashSet<ICmObjectId>();

			var uowServices = Cache.ServiceLocator.GetInstance<IUnitOfWorkService>();
			Assert.That(dirtballs.Count, Is.EqualTo(0)); // After save nothing should be dirty.

			uowServices.GatherChanges(newbies, dirtballs, goners);
			int oldWsHandle = newWs.Handle;
			var tempWs = new CoreWritingSystemDefinition("en-GB");
			newWs.Copy(tempWs);
			Cache.ServiceLocator.GetInstance<WritingSystemManager>().Set(newWs);

			UndoableUnitOfWorkHelper.Do("doit", "undoit", m_actionHandler,
				() => WritingSystemServices.UpdateWritingSystemId(Cache, newWs, oldWsHandle, "en-SU"));

			newbies = new HashSet<ICmObjectId>();
			dirtballs = new HashSet<ICmObjectOrSurrogate>(new ObjectSurrogateEquater());
			goners = new HashSet<ICmObjectId>();

			uowServices.GatherChanges(newbies, dirtballs, goners);

			Assert.That(dirtballs.Contains((ICmObjectOrSurrogate)sense1));
			Assert.That(!dirtballs.Contains((ICmObjectOrSurrogate)entry0)); // make sure the implementation doesn't just dirty everything.
			Assert.That(dirtballs.Contains((ICmObjectOrSurrogate)entry2));
			Assert.That(dirtballs.Contains((ICmObjectOrSurrogate)sense3));
			Assert.That(dirtballs.Contains((ICmObjectOrSurrogate)para.SegmentsOS[0]));
			Assert.That(Cache.LangProject.AnalysisWss, Is.EqualTo("en en-GB"), "should have updated WS lists");
		}

		/// <summary />
		[Test]
		public void MergeWritingSystem_ConvertsMultiStrings()
		{
			var entry1 = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
			CoreWritingSystemDefinition fromWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "en-NO", true, false, out fromWs);
			CoreWritingSystemDefinition toWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "en-SO", true, false, out toWs);
			EnsureAnalysisWs(new [] {fromWs, toWs});
			var sense1 = Cache.ServiceLocator.GetInstance<ILexSenseFactory>().Create();
			entry1.SensesOS.Add(sense1);
			// Sense1 should be dirty: it has a gloss in the changing WS.
			sense1.Gloss.set_String(fromWs.Handle, TsStringUtils.MakeString("whatever", fromWs.Handle));
			sense1.Gloss.get_String(fromWs.Handle).Insert(0, TsStringUtils.MakeString("8", fromWs.Handle, "Verse Number"));
			m_actionHandler.EndUndoTask();
			UndoableUnitOfWorkHelper.Do("doit", "undoit", m_actionHandler,
				() => WritingSystemServices.MergeWritingSystems(Cache, fromWs, toWs));
			Assert.That(sense1.Gloss.get_String(toWs.Handle).Text, Is.EqualTo("whatever"));
		}

		/// <summary>
		/// Make sure these writing systems are actually noted as current analysis writing systems.
		/// (The way we create them using FindOrCreateWritingSystem normally makes this so. But
		/// sometimes if an earlier test already created one FindOrCreate doesn't have to create,
		/// and then it no longer ensures it is in the list.)
		/// </summary>
		void EnsureAnalysisWs(CoreWritingSystemDefinition[] wss)
		{
			foreach (var ws in wss)
			{
				if (!Cache.ServiceLocator.WritingSystems.AnalysisWritingSystems.Contains(ws))
					Cache.ServiceLocator.WritingSystems.AddToCurrentAnalysisWritingSystems(ws);
			}
		}

		/// <summary>
		/// A style definition that has an override for the fromWs writing system should now have one
		/// for the toWs (unless there was already one for toWs).
		/// </summary>
		[Test]
		public void MergeWritingSystem_ConvertsStyleDefinition()
		{
			CoreWritingSystemDefinition fromWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "en-NO", true, false, out fromWs);
			CoreWritingSystemDefinition toWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "en-SO", true, false, out toWs);
			EnsureAnalysisWs(new [] { fromWs, toWs });

			var style1 = Cache.ServiceLocator.GetInstance<IStStyleFactory>().Create();
			Cache.LangProject.StylesOC.Add(style1);
			var fontOverrides = new Dictionary<int, FontInfo>();
			var fontOverride = new FontInfo();
			fontOverride.m_italic.ExplicitValue = true;
			fontOverrides[fromWs.Handle] = fontOverride;
			var bldr = TsStringUtils.MakePropsBldr();
			BaseStyleInfo.SaveFontOverridesToBuilder(fontOverrides, bldr);
			style1.Rules = bldr.GetTextProps();
			m_actionHandler.EndUndoTask();
			UndoableUnitOfWorkHelper.Do("doit", "undoit", m_actionHandler,
				() => WritingSystemServices.MergeWritingSystems(Cache, fromWs, toWs));
			var styleInfo = new BaseStyleInfo(style1);
			var overrideInfo = styleInfo.OverrideCharacterStyleInfo(toWs.Handle);
			Assert.IsNotNull(overrideInfo);
			Assert.That(overrideInfo.Italic.Value, Is.True);
		}

		/// <summary>
		/// A style definition that has an override for the fromWs AND toWs should not change the
		/// one for toWs.
		/// </summary>
		[Test]
		public void MergeWritingSystemWithStyleDefnForToWs_DoesNotConvertStyleDefinition()
		{
			CoreWritingSystemDefinition fromWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "en-NO", true, false, out fromWs);
			CoreWritingSystemDefinition toWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "en-SO", true, false, out toWs);
			EnsureAnalysisWs(new[] { fromWs, toWs });

			var style1 = Cache.ServiceLocator.GetInstance<IStStyleFactory>().Create();
			Cache.LangProject.StylesOC.Add(style1);
			var fontOverrides = new Dictionary<int, FontInfo>();
			var fontOverride = new FontInfo();
			fontOverride.m_italic.ExplicitValue = true;
			fontOverrides[fromWs.Handle] = fontOverride;
			fontOverride = new FontInfo();
			fontOverride.m_bold.ExplicitValue = true;
			fontOverrides[toWs.Handle] = fontOverride;
			var bldr = TsStringUtils.MakePropsBldr();
			BaseStyleInfo.SaveFontOverridesToBuilder(fontOverrides, bldr);
			style1.Rules = bldr.GetTextProps();
			m_actionHandler.EndUndoTask();
			UndoableUnitOfWorkHelper.Do("doit", "undoit", m_actionHandler,
				() => WritingSystemServices.MergeWritingSystems(Cache, fromWs, toWs));
			var styleInfo = new BaseStyleInfo(style1);
			var overrideInfo = styleInfo.OverrideCharacterStyleInfo(toWs.Handle);
			Assert.IsNotNull(overrideInfo);
			Assert.That(overrideInfo.Bold.Value, Is.True);
			Assert.That(overrideInfo.Italic.ValueIsSet, Is.False);
		}

		/// <summary>
		/// If old objects contain LiftResidue data with a lang attribute that matches the fromWs,
		/// change it to the toWs.
		/// Enhance JohnT: possibly we need to do something about merging corresponding data if
		/// the same parent element contains an alternative in the toWs.
		/// </summary>
		[Test]
		public void MergeWritingSystem_ConvertsLiftResidue()
		{
			CoreWritingSystemDefinition fromWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "en-NO", true, false, out fromWs);
			CoreWritingSystemDefinition toWs;
			WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "en-SO", true, false, out toWs);
			EnsureAnalysisWs(new[] { fromWs, toWs });

			var entry1 = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
			entry1.LiftResidue =
				"<lift-residue id=\"aj1_8ef13061-21ae-480f-b3a9-6b694e1ec3c4\" dateCreated=\"2005-11-09T02:57:45Z\" dateModified=\"2010-07-03T08:15:00Z\"><field type=\"Source Language\">"
				+"<form lang=\"en\"><text>Proto-Tai</text></form>"
				+"<form lang=\"en-NO\"><text>￥ﾎﾟ￥ﾧﾋ￥ﾏﾰ￨ﾯﾭ</text></form>"
				+"</field>"
				+"</lift-residue>";

			m_actionHandler.EndUndoTask();
			UndoableUnitOfWorkHelper.Do("doit", "undoit", m_actionHandler,
				() => WritingSystemServices.MergeWritingSystems(Cache, fromWs, toWs));
			Assert.That(entry1.LiftResidue.Contains("lang=\"en-SO\""));
		}


		/// <summary>
		/// What it says
		/// </summary>
		[Test]
		public void CollatorSort_DoesNotThrow()
		{
			Assert.DoesNotThrow(() =>
				{
					CoreWritingSystemDefinition fromWs;
					WritingSystemServices.FindOrCreateWritingSystem(Cache, null, "sen", false, true, out fromWs);
					Cache.LangProject.DefaultVernacularWritingSystem = fromWs;
					fromWs.DefaultCollation.Collator.GetSortKey("boom");
				});
		}
	}
}
