// Copyright (c) 2015-2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using NUnit.Framework;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.Infrastructure;
using SIL.WritingSystems;

namespace SIL.LCModel.DomainServices
{
	/// <summary>
	/// </summary>
	[TestFixture]
	public class ParagraphParserTests: InterlinearTestBase
	{
		IText m_text1 = null;
		private XmlNode m_testFixtureTextsDefn = null;
		XmlDocument m_textsDefn = null;
		private CoreWritingSystemDefinition m_wsXkal = null;

		/// <summary>
		///
		/// </summary>
		[TestFixtureSetUp]
		public override void FixtureSetup()
		{
			base.FixtureSetup();
			m_textsDefn = new XmlDocument();
			NonUndoableUnitOfWorkHelper.Do(Cache.ActionHandlerAccessor,
				DoSetupFixture);
		}

		/// <summary>
		/// non-undoable task
		/// </summary>
		private void DoSetupFixture()
		{
			// setup default vernacular ws.
			m_wsXkal = Cache.ServiceLocator.WritingSystemManager.Set("qaa-x-kal");
			m_wsXkal.Fonts.Clear();
			m_wsXkal.DefaultFont = new FontDefinition("Times New Roman");
			Cache.ServiceLocator.WritingSystems.DefaultVernacularWritingSystem = m_wsXkal;

			m_text1 = LoadTestText(Path.Combine(TestDirectoryFinder.TestDataDirectory, "ParagraphParserTestTexts.xml"), 1, m_textsDefn);
			// capture text defn state.
			m_testFixtureTextsDefn = m_textsDefn;
		}

		/// <summary>
		///
		/// </summary>
		[TestFixtureTearDown]
		public override void FixtureTeardown()
		{
			m_textsDefn = null;
			m_text1 = null;

			base.FixtureTeardown();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Guarantees that the default vernacular writing system has the single quote and
		/// hyphen characters as word-forming (which used to be the default based on the old
		/// word-forming overrides XML file).
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void SetupOldWordformingOverrides()
		{
			CoreWritingSystemDefinition wsObj = Cache.ServiceLocator.WritingSystems.DefaultVernacularWritingSystem;
			var validChars = ValidCharacters.Load(wsObj);
			var fChangedSomething = false;
			if (!validChars.IsWordForming('-'))
			{
				validChars.AddCharacter("-", ValidCharacterType.WordForming);
				fChangedSomething = true;
			}
			if (!validChars.IsWordForming('\''))
			{
				validChars.AddCharacter("'", ValidCharacterType.WordForming);
				fChangedSomething = true;
			}
			if (!fChangedSomething)
				return;
			validChars.SaveTo(wsObj);
		}

		private void RestoreTextDefn()
		{
			m_textsDefn = ParagraphBuilder.Snapshot(m_testFixtureTextsDefn) as XmlDocument;
		}


		/// <summary>
		/// Restore our TestFixture data for each test use.
		/// </summary>
		[SetUp]
		public override void TestSetup()
		{
			base.TestSetup();

			RestoreTextDefn();
		}

		/// <summary>
		/// Test the method that gets the reference of an AnalysisOccurrence
		/// Enhance: It would be nice to test references for more than one paragraph.
		/// </summary>
		[Test]
		public void AnalysisReference()
		{
			// Make a text like "my green mat".
			var sttext = MakeText("My Green mat");
			var seg1 = MakeSegment(sttext, "pus yalola nihimbilira.");
			var analysis1 = MakeWordformAnalysis(seg1, "pus");
			Assert.That(analysis1.Reference.Text, Is.EqualTo("My Green 1.1"));
			//// Now try for one in the second segment.
			MakeWordformAnalysis(seg1, "yalola");
			MakeWordformAnalysis(seg1, "nihimbilira");
			var seg2 = MakeSegment(sttext, "hesyla nihimbilira.");
			MakeWordformAnalysis(seg2, "hesyla");
			var analysis5 = MakeWordformAnalysis(seg2, "nihimbilira");
			Assert.That(analysis5.Reference.Text, Is.EqualTo("My Green 1.2"));

			//// Set an empty text name and check.
			var text = (IText) sttext.Owner;
			text.Name.AnalysisDefaultWritingSystem = TsStringUtils.EmptyString(Cache.DefaultAnalWs);
			Assert.That(analysis1.Reference.Text, Is.EqualTo("1.1"));

			//// Try with a name less than 5 chars.
			text.Name.AnalysisDefaultWritingSystem = TsStringUtils.MakeString("abc", Cache.DefaultAnalWs);
			Assert.That(analysis1.Reference.Text, Is.EqualTo("abc 1.1"));

			// It prefers to use the abbreviation.
			text.Abbreviation.AnalysisDefaultWritingSystem = TsStringUtils.MakeString("mg", Cache.DefaultAnalWs);
			Assert.That(analysis1.Reference.Text, Is.EqualTo("mg 1.1"));
		}

		private AnalysisOccurrence MakeWordformAnalysis(ISegment seg, string form)
		{
			var wf = WfiWordformServices.FindOrCreateWordform(Cache, form,
				Cache.ServiceLocator.WritingSystems.DefaultVernacularWritingSystem);
			seg.AnalysesRS.Add(wf);
			return new AnalysisOccurrence(seg, seg.AnalysesRS.Count - 1);
		}

		/// <summary>
		/// Add a segment of text to the paragraph and return the resulting segment.
		/// Note that this depends on the code that automatically reparses the paragraph,
		/// so the strings added must really produce segments.
		/// </summary>
		private ISegment MakeSegment(IStText text, string contents)
		{
			var para = (IStTxtPara) text.ParagraphsOS[0];
			int length = para.Contents.Length;
			if (length == 0)
				para.Contents = TsStringUtils.MakeString(contents, Cache.DefaultVernWs);
			else
			{
				var bldr = para.Contents.GetBldr();
				bldr.Replace(length, length, " " + contents, null);
				para.Contents = bldr.GetString();
			}
			var seg = para.SegmentsOS[para.SegmentsOS.Count - 1];
			return seg;
		}

		private IStText MakeText(string title)
		{
			var text = Cache.ServiceLocator.GetInstance<ITextFactory>().Create();
			//Cache.LangProject.TextsOC.Add(text);
			var result = Cache.ServiceLocator.GetInstance<IStTextFactory>().Create();
			text.ContentsOA = result;
			text.Name.AnalysisDefaultWritingSystem = TsStringUtils.MakeString(title, Cache.DefaultAnalWs);
			var para = Cache.ServiceLocator.GetInstance<IStTxtParaFactory>().Create();
			result.ParagraphsOS.Add(para);
			return result;
		}

		/// <summary>Indices for paragraphs used in these tests</summary>
		public enum Text1ParaIndex {
			/// <summary>
			/// xxxpus xxxyalola xxxnihimbilira. xxxnihimbilira xxxpus xxxyalola. xxxhesyla xxxnihimbilira.
			/// </summary>
			SimpleSegmentPara = 0,
			/// <summary> ""; don't put this first as WS is not well established. </summary>
			EmptyParagraph,
			/// <summary>
			/// <code>xxxnihimbiligu  xxxyaloni, xxxkasani &amp; xxxnihimbilibi... "xxxnihimbilira (xxxpus) xxxkasani."! ! ! xxxpus</code>
			/// </summary>
			ComplexPunctuations,
			/// <summary>
			/// xxxnihimbili'gu xxxyaloni. xxxkasani1xxxpus xxxnihimbilibi 123. xxxnihimbilira xxxpus-kola xxxkasani.
			/// </summary>
			ComplexWordforms,
			/// <summary>
			/// Xxxpus xxxyalola xxxnihimbilira. Xxxnihimbilira xxxpus Xxxyalola. Xxxhesyla XXXNIHIMBILIRA.
			/// </summary>
			MixedCases,
			/// <summary>
			/// xxxpus xxes xxxnihimbilira. xxfr xxen xxxnihimbilira xxxpus xxde. xxkal xxkal xxxxhesyla xxxxhesyla.
			/// </summary>
			MultipleWritingSystems,
			/// <summary>
			/// xxxpus xxxyalola xxxnihimbilira. xxxpus xxxyalola xxxhesyla xxxnihimbilira. xxxpus xxxyalola xxxnihimbilira.
			/// </summary>
			PhraseWordforms};
		/// <summary>
		/// The actual paragraphs are built from ParagraphParserTestTexts.xml. ParagraphContents is simply for auditing.
		/// </summary>
		readonly string[] ParagraphContents = {
			// Paragraph 0 - simple segments
			"xxxpus xxxyalola xxxnihimbilira. xxxnihimbilira xxxpus xxxyalola. xxxhesyla xxxnihimbilira.",
			// Paragraph 1 - empty paragraph
			"",
			// Paragraph 2 - complex punctuation
			"xxxnihimbiligu  xxxyaloni, xxxkasani & xxxnihimbilibi... \"xxxnihimbilira (xxxpus) xxxkasani.\"! ! ! xxxpus",
			// Paragraph 3 - complex wordforms.
			"xxxnihimbili'gu xxxyaloni. xxxkasani1xxxpus xxxnihimbilibi 123. xxxnihimbilira xxxpus-kola xxxkasani.",
			// Paragraph 4 - mixed case.
			"Xxxpus xxxyalola xxxnihimbilira. Xxxnihimbilira xxxpus Xxxyalola. Xxxhesyla XXXNIHIMBILIRA.",
			// Paragraph 5 - multiple vernacular writing systems.
			"xxxpus xxes xxxnihimbilira. xxfr xxen xxxnihimbilira xxxpus xxde. xxkal xxkal xxxxhesyla xxxxhesyla.",
			// Paragraph 6 - PhraseWordforms.
			"xxxpus xxxyalola xxxnihimbilira. xxxpus xxxyalola xxxhesyla xxxnihimbilira. xxxpus xxxyalola xxxnihimbilira."
		};

		/// <summary>
		/// This checks that our TextBuilder and ParagraphBuilder build texts like we expect.
		/// </summary>
		[Test]
		public void BuildTextFromAnnotations()
		{
			Assert.IsNotNull(m_text1);
			Assert.AreEqual(m_text1.Name.VernacularDefaultWritingSystem.Text, "Test Text1 for ParagraphParser");
			Assert.IsNotNull(m_text1.ContentsOA);
			Assert.AreEqual(Enum.GetValues(typeof(Text1ParaIndex)).Length, m_text1.ContentsOA.ParagraphsOS.Count);
			// Empty paragraph.
			Assert.AreEqual(ParagraphContents[(int)Text1ParaIndex.EmptyParagraph].Length,
							((IStTxtPara)m_text1.ContentsOA.ParagraphsOS[(int)Text1ParaIndex.EmptyParagraph]).Contents.Length);
			// Simple Segments.
			Assert.AreEqual(ParagraphContents[(int)Text1ParaIndex.SimpleSegmentPara],
							((IStTxtPara)m_text1.ContentsOA.ParagraphsOS[(int)Text1ParaIndex.SimpleSegmentPara]).Contents.Text);
			// Complex Punctuations
			Assert.AreEqual(ParagraphContents[(int)Text1ParaIndex.ComplexPunctuations],
							((IStTxtPara)m_text1.ContentsOA.ParagraphsOS[(int)Text1ParaIndex.ComplexPunctuations]).Contents.Text);
			// Complex Wordforms
			Assert.AreEqual(ParagraphContents[(int)Text1ParaIndex.ComplexWordforms],
							((IStTxtPara)m_text1.ContentsOA.ParagraphsOS[(int)Text1ParaIndex.ComplexWordforms]).Contents.Text);
			// Mixed Cases
			Assert.AreEqual(ParagraphContents[(int)Text1ParaIndex.MixedCases],
							((IStTxtPara)m_text1.ContentsOA.ParagraphsOS[(int)Text1ParaIndex.MixedCases]).Contents.Text);
			// Multiple Writing systems.
			Assert.AreEqual(ParagraphContents[(int)Text1ParaIndex.MultipleWritingSystems],
							((IStTxtPara)m_text1.ContentsOA.ParagraphsOS[(int)Text1ParaIndex.MultipleWritingSystems]).Contents.Text);
			// Phrase wordforms
			Assert.AreEqual(ParagraphContents[(int)Text1ParaIndex.PhraseWordforms],
							((IStTxtPara)m_text1.ContentsOA.ParagraphsOS[(int)Text1ParaIndex.PhraseWordforms]).Contents.Text);
		}

		/// <summary>
		/// Test the annotations for most paragraphs.
		/// </summary>
		[Test]
		public void NoAnalyses_NoEdits(
			[Values(Text1ParaIndex.EmptyParagraph, Text1ParaIndex.SimpleSegmentPara, Text1ParaIndex.ComplexPunctuations,
				Text1ParaIndex.MixedCases, Text1ParaIndex.MultipleWritingSystems)] Text1ParaIndex paraIdx)
		{
			var pb = new ParagraphBuilder(m_textsDefn, m_text1, (int)paraIdx);
			var tapb = new ParagraphAnnotatorForParagraphBuilder(pb);
			pb.ParseParagraph();
			tapb.ValidateAnnotations();
		}

		/// <summary>
		/// Test the annotations for complex wordform paragraph.
		/// </summary>
		[Test]
		public void NoAnalyses_NoEdits_ComplexWordformsParagraph()
		{
			SetupOldWordformingOverrides();
			NoAnalyses_NoEdits(Text1ParaIndex.ComplexWordforms);
		}

		/// <summary>
		/// 0		 1		 2		 3		 4		 5
		/// 012345678901234567890123456789012345678901234567890123456789
		/// xxxpus xxxyalola xxxnihimbilira. xxxpus xxxyalola xxxhesyla xxxnihimbilira. xxxpus xxxyalola xxxnihimbilira.
		/// xxxpus xxxyalola xxxnihimbilira. [xxxpus xxxyalola] xxxhesyla xxxnihimbilira. xxxpus xxxyalola xxxnihimbilira.
		/// </summary>
		[Test]
		public void NoAnalyses_NoEdits_PhraseWordforms()
		{
			// 1. Setup Tests with a basic phrase
			ParagraphBuilder pb = new ParagraphBuilder(m_textsDefn, m_text1, (int) Text1ParaIndex.PhraseWordforms);

			// first do a basic phrase (without secondary phrases (guesses))
			// xxxpus xxxyalola xxxnihimbilira. [xxxpus xxxyalola] xxxhesyla xxxnihimbilira. xxxpus xxxyalola xxxnihimbilira
			pb.MergeAdjacentAnnotations(1, 0);
			// generate mock ids
			pb.RebuildParagraphContentFromAnnotations();
			// now produce a guess to establish the phrase annotation.
			var tapb = new ParagraphAnnotatorForParagraphBuilder(pb);
			pb.ParseParagraph();
			pb.ActualParagraph.SegmentsOS[1].AnalysesRS.RemoveAt(0); // delete "xxxpus"
			// now replace "xxxyalola" with the new phrase form "xxxpus xxxyalola"
			IAnalysis beforeParse_phrase1_0 = pb.ExportCbaNodeToReal(1, 0);
			//string gloss;
			//IWfiGloss wg_phrase1_0 = tapb.SetDefaultWordGloss(1, 0, out gloss);
			// NOTE: Precondition checks to make sure we set up the annotation properly

			// The real test: now parse and verify that we maintained the expected result for the phrase annotation.
			pb.ParseParagraph();
			var afterParse_actualWordform = tapb.GetAnalysis(1, 0);
			Assert.AreEqual(beforeParse_phrase1_0, afterParse_actualWordform, "word mismatch");
			// verify the rest.
			tapb.ValidateAnnotations();
		}

		/// <summary>
		/// Test the annotations for mixed case wordform paragraph.
		/// </summary>
		[Test]
		public void NoAnalyses_NoEdits_MultipleWritingSystemsParagraph_LT5379()
		{
			var pb = new ParagraphBuilder(m_textsDefn, m_text1, (int)Text1ParaIndex.MultipleWritingSystems);
			var tapb = new ParagraphAnnotatorForParagraphBuilder(pb);
			pb.ParseParagraph();
			tapb.ValidateAnnotations();
		}

		/// <summary>
		/// Change the writing system of a word in the text after parsing it the first time.
		/// xxxpus xxes xxxnihimbilira. xxfr xxen xxxnihimbilira xxxpus xxde. xxkal xxkal xxxxhesyla xxxxhesyla.
		/// </summary>
		[Test]
		public void NoAnalyses_SimpleEdits_MultipleWritingSystemsParagraph()
		{
			ParagraphBuilder pb = new ParagraphBuilder(m_textsDefn, m_text1, (int)Text1ParaIndex.MultipleWritingSystems);
			// verify that our wfics point to wordforms in the expected wss.
			pb.ParseParagraph();
			ParagraphAnnotatorForParagraphBuilder tapb = new ParagraphAnnotatorForParagraphBuilder(pb);
			LcmValidator.ValidateCbaWordToBaselineWord(tapb, 0, 0);
			// validate the rest
			tapb.ValidateAnnotations();

			// xxxpus xxes xxxnihimbilira. xxfr xxen xxxnihimbilira xxxpus xxde. xxkal xxkal xxxxhesyla xxxxhesyla.
			//	xxkal: German (de)		-- occurrence 0
			//	xxkal: Kalaba (xkal)	-- occurrence 1
			Dictionary<string, int> expectedOccurrences = pb.ExpectedWordformsAndOccurrences;
			CheckExpectedWordformsAndOccurrences(pb.ActualParagraph, expectedOccurrences);
			// replace the german occurrence of "xxkal" with a xkal version.
			int wsDe;
			LcmValidator.GetTssStringValue(tapb, 2, 0, out wsDe);
			int wsVernDef = Cache.DefaultVernWs;
			Assert.AreNotEqual(wsVernDef, wsDe, "Precondition: did not expect to have a default vern ws.");
			pb.ReplaceSegmentForm(2, 0, "xxkal", wsVernDef);
			var segformNode = pb.SegmentFormNode(2, 0);
			// Now it should parse as a wfic.
			var linkNode = segformNode.SelectSingleNode("AnnotationType34/Link");
			linkNode.Attributes["guid"].Value = ParagraphBuilder.WficGuid;
			linkNode.Attributes["name"].Value = "Wordform In Context";

			expectedOccurrences.Remove("xxkal" + wsDe.ToString());
			expectedOccurrences["xxkal" + wsVernDef.ToString()] += 1;
			pb.RebuildParagraphContentFromAnnotations(true);
			pb.ParseParagraph();
			int wsAnalysis2_0;
			LcmValidator.GetTssStringValue(tapb, 2, 0, out wsAnalysis2_0);
			Assert.AreEqual(Cache.DefaultVernWs, wsAnalysis2_0, "Precondition: expected to have default vern ws.");
			LcmValidator.ValidateCbaWordToBaselineWord(tapb, 2, 0);
			// validate the rest.
			tapb.ValidateAnnotations();
			CheckExpectedWordformsAndOccurrences(pb.ActualParagraph, expectedOccurrences);
		}

		/// <summary>
		/// Add a new ws alternative to an existing wordform.
		/// Then add another alternative that already has an existing hvo.
		/// TODO 2012.01
		/// </summary>
		[Ignore("LT-10147: Need to do this when we get more serious about merging wordform information.")]
		[Test]
		public void SimpleAnalyses_SimpleEdits_MultipleWritingSystemsParagraph_LT10147()
		{
		}

		/// <summary>
		/// Test making a phrase.
		/// </summary>
		[Test]
		public void Phrase_Make()
		{
			// 1. Make Phrases
			IList<int[]> secondaryPathsToJoinWords = new List<int[]>();
			IList<int[]> secondaryPathsToBreakPhrases = new List<int[]>();

			ParagraphBuilder pb = new ParagraphBuilder(m_textsDefn, m_text1, (int) Text1ParaIndex.PhraseWordforms);
			ParagraphAnnotatorForParagraphBuilder tapb = new ParagraphAnnotatorForParagraphBuilder(pb);

			pb.ParseParagraph();
			// xxxpus xxxyalola xxxnihimbilira. xxxpus xxxyalola [xxxhesyla xxxnihimbilira]. xxxpus xxxyalola xxxnihimbilira
			tapb.MergeAdjacentAnnotations(1, 2, secondaryPathsToJoinWords, secondaryPathsToBreakPhrases);
			tapb.ValidateAnnotations();
		}

		/// <summary>
		/// Test that making and breaking phrases does not delete wordforms occurring elsewhere in a text
		/// that has not been loaded as part of a concordance, because that can invalidate the display
		/// of those wordforms in the Interlinear tabs.
		/// </summary>
		[Test]
		public void LT7974_Phrase_MakeAndBreak()
		{
			// 1. Make Phrases
			IList<int[]> secondaryPathsToJoinWords = new List<int[]>();
			IList<int[]> secondaryPathsToBreakPhrases = new List<int[]>();
			//Thist test proved to be incapable of being Undone, since further tests relied on state this test affected
			//the following two variables duplicate member variables to isolate changes to this test.
			XmlDocument donTouchIt = new XmlDocument();
			IText noLoToque = LoadTestText(Path.Combine(TestDirectoryFinder.TestDataDirectory, "ParagraphParserTestTexts.xml"), 1,
				donTouchIt);

			ParagraphBuilder pb = new ParagraphBuilder(donTouchIt, noLoToque, (int)Text1ParaIndex.PhraseWordforms);
			ParagraphAnnotatorForParagraphBuilder tapb = new ParagraphAnnotatorForParagraphBuilder(pb);
			// go ahead and parse the text generating real wordforms to be more like a real user situation (LT-7974)
			// and reset the concordance to simulate the situation where the user hasn't loaded ConcordanceWords yet.
			ParagraphParser.ParseText(noLoToque.ContentsOA);
			pb.ResyncExpectedAnnotationIds();

			// first do a basic phrase (without secondary phrases (guesses)), which should not delete "xxxpus" or "xxxyalola"
			// since they occur later in the text.
			// [xxxpus xxxyalola] xxxnihimbilira. xxxpus xxxyalola xxxhesyla xxxnihimbilira. xxxpus xxxyalola xxxnihimbilira
			IWfiWordform wf1 = pb.ActualParagraph.SegmentsOS[0].AnalysesRS[0].Wordform;
			IWfiWordform wf2 = pb.ActualParagraph.SegmentsOS[0].AnalysesRS[1].Wordform;
			tapb.MergeAdjacentAnnotations(0, 0, secondaryPathsToJoinWords, secondaryPathsToBreakPhrases);
			// after the merge, make sure the wordforms for xxxpus and xxxyalola are still valid
			Assert.IsTrue(wf1.IsValidObject, "expected xxxpus to still be valid");
			Assert.IsTrue(wf2.IsValidObject, "expected xxxyalola to still be valid");
			tapb.ValidateAnnotations(true);

			// now break the phrase and make sure we delete this wordform, b/c it is not occurring elsewhere
			pb.ResyncExpectedAnnotationIds();
			// \xxxpus\ \xxxyalola\ xxxnihimbilira. xxxpus xxxyalola xxxhesyla xxxnihimbilira. xxxpus xxxyalola xxxnihimbilira
			IWfiWordform wf3 = pb.ActualParagraph.SegmentsOS[0].AnalysesRS[0].Wordform;
			secondaryPathsToJoinWords.Clear();
			secondaryPathsToBreakPhrases.Clear();
			tapb.BreakPhrase(0, 0, secondaryPathsToBreakPhrases, secondaryPathsToJoinWords, null);
			Assert.IsFalse(wf3.IsValidObject, "expected 'xxxpus xxxyalola' to be deleted.");
			tapb.ValidateAnnotations(true);

			// rejoin the first phrase, parse the text, and break the first phrase
			// [xxxpus xxxyalola] xxxnihimbilira. {xxxpus xxxyalola} xxxhesyla xxxnihimbilira. {xxxpus xxxyalola} xxxnihimbilira
			secondaryPathsToJoinWords.Add(new int[2] { 1, 0 }); // {xxxpus xxxyalola} xxxhesyla
			secondaryPathsToJoinWords.Add(new int[2] { 2, 0 }); // {xxxpus xxxyalola} xxxnihimbilira
			tapb.MergeAdjacentAnnotations(0, 0, secondaryPathsToJoinWords, secondaryPathsToBreakPhrases);
			wf1 = pb.ActualParagraph.SegmentsOS[0].AnalysesRS[0].Wordform;
			tapb.ReparseParagraph();
			// just break the new phrase (and the leave existing guesses)
			// \xxxpus\ \xxxyalola\ xxxnihimbilira. {xxxpus xxxyalola} xxxhesyla xxxnihimbilira. {xxxpus xxxyalola} xxxnihimbilira
			secondaryPathsToJoinWords.Clear();
			tapb.BreakPhrase(0, 0, secondaryPathsToBreakPhrases, secondaryPathsToJoinWords, null);
			Assert.IsTrue(wf1.IsValidObject, "expected 'xxxpus xxxyalola' to still be valid");
			tapb.ValidateAnnotations(true);
			// xxxpus xxxyalola xxxnihimbilira. \xxxpus\ \xxxyalola\ xxxhesyla xxxnihimbilira. {xxxpus xxxyalola} xxxnihimbilira
			tapb.BreakPhrase(1, 0, secondaryPathsToBreakPhrases, secondaryPathsToJoinWords, null);
			Assert.IsTrue(wf1.IsValidObject, "expected 'xxxpus xxxyalola' to still be valid");
			tapb.ValidateAnnotations(true);
			// xxxpus xxxyalola xxxnihimbilira. xxxpus xxxyalola xxxhesyla xxxnihimbilira. \xxxpus\ \xxxyalola\ xxxnihimbilira
			tapb.BreakPhrase(2, 0, secondaryPathsToBreakPhrases, secondaryPathsToJoinWords, null);
			Assert.IsFalse(wf1.IsValidObject, "expected 'xxxpus xxxyalola' to be deleted");
			tapb.ValidateAnnotations(true);

			// now do two joins, the second resulting in deletion of a wordform.
			// xxxpus xxxyalola xxxnihimbilira. xxxpus [xxxyalola xxxhesyla] xxxnihimbilira. xxxpus xxxyalola xxxnihimbilira
			// xxxpus xxxyalola xxxnihimbilira. xxxpus [xxxyalola xxxhesyla xxxnihimbilira]. xxxpus xxxyalola xxxnihimbilira
			ParagraphParser.ParseText(noLoToque.ContentsOA);
			pb.ResyncExpectedAnnotationIds();
			tapb.MergeAdjacentAnnotations(1, 1, secondaryPathsToJoinWords, secondaryPathsToBreakPhrases);
			IWfiWordform wf4 = pb.ActualParagraph.SegmentsOS[1].AnalysesRS[1].Wordform;
			tapb.MergeAdjacentAnnotations(1, 1, secondaryPathsToJoinWords, secondaryPathsToBreakPhrases);
			// this merge should have deleted 'xxxyalola xxxhesyla'
			Assert.IsFalse(wf4.IsValidObject, "expected 'xxxyalola xxxhesyla' to be deleted.");
			tapb.ValidateAnnotations(true);
		}

		/// <summary>
		/// Test the annotations for paragraph with phrase-wordform annotations.
		/// </summary>
		[Test]
		//[Ignore("FWC-16: this test causes NUnit to hang in fixture teardown - need more investigation")]
		public void Phrase_MakeAndBreak()
		{
			// 1. Make Phrases
			IList<int[]> secondaryPathsToJoinWords = new List<int[]>();
			IList<int[]> secondaryPathsToBreakPhrases = new List<int[]>();

			ParagraphBuilder pb = new ParagraphBuilder(m_textsDefn, m_text1, (int)Text1ParaIndex.PhraseWordforms);
			ParagraphAnnotatorForParagraphBuilder tapb = new ParagraphAnnotatorForParagraphBuilder(pb);
			pb.ParseParagraph();

			// first do a basic phrase (without secondary phrases (guesses)) and break it
			// [xxxpus xxxyalola] xxxnihimbilira. xxxpus xxxyalola xxxhesyla xxxnihimbilira. xxxpus xxxyalola xxxnihimbilira
			tapb.MergeAdjacentAnnotations(0, 0, secondaryPathsToJoinWords, secondaryPathsToBreakPhrases);
			tapb.ValidateAnnotations(true);

			// \xxxpus\ \xxxyalola\ xxxnihimbilira. xxxpus xxxyalola xxxhesyla xxxnihimbilira. xxxpus xxxyalola xxxnihimbilira
			secondaryPathsToJoinWords.Clear();
			secondaryPathsToBreakPhrases.Clear();
			tapb.BreakPhrase(0, 0, secondaryPathsToBreakPhrases, secondaryPathsToJoinWords, null);
			tapb.ValidateAnnotations(true);

			// make phrases with secondary phrases (guesses).
			// [xxxpus xxxyalola] xxxnihimbilira. {xxxpus xxxyalola} xxxhesyla xxxnihimbilira. {xxxpus xxxyalola} xxxnihimbilira
			secondaryPathsToJoinWords.Clear();
			secondaryPathsToBreakPhrases.Clear();
			secondaryPathsToJoinWords.Add(new int[2] { 1, 0 }); // {xxxpus xxxyalola} xxxhesyla
			secondaryPathsToJoinWords.Add(new int[2] { 2, 0 }); // {xxxpus xxxyalola} xxxnihimbilira
			tapb.MergeAdjacentAnnotations(0, 0, secondaryPathsToJoinWords, secondaryPathsToBreakPhrases);
			// then check that the last phrase has been properly annotated.
			tapb.ValidateAnnotations();

			// JohnT: in 6.0, making the larger phrase would apparently get rid of the guessed phrase.
			// in 7.0+ there's no distinction between a guess and user-made phrase, so once it exists it doesn't go away.
			// [xxxpus xxxyalola xxxnihimbilira]. [xxxpus xxxyalola] xxxhesyla xxxnihimbilira. {xxxpus xxxyalola xxxnihimbilira}.
			secondaryPathsToJoinWords.Clear();
			secondaryPathsToBreakPhrases.Clear();
			// 7.0+ secondaryPathsToBreakPhrases.Add(new int[2] { 1, 0 }); // \xxxpus\ \xxxyalola\ xxxhesyla
			secondaryPathsToJoinWords.Add(new int[2] { 2, 0 }); // {{xxxpus xxxyalola} xxxnihimbilira}
			tapb.MergeAdjacentAnnotations(0, 0, secondaryPathsToJoinWords, secondaryPathsToBreakPhrases);
			tapb.ValidateAnnotations();

			// [xxxpus xxxyalola xxxnihimbilira]. [xxxpus xxxyalola] xxxhesyla xxxnihimbilira. {xxxpus xxxyalola xxxnihimbilira}
			// 7.0+ nothing to do here; it's in this state after the previous change.
			//secondaryPathsToJoinWords.Clear();
			//secondaryPathsToBreakPhrases.Clear();
			//tapb.MergeAdjacentAnnotations(1, 0, secondaryPathsToJoinWords, secondaryPathsToBreakPhrases);
			//tapb.ValidateAnnotations();

			// 2. Break Phrases.
			// [xxxpus xxxyalola xxxnihimbilira]. \xxxpus\ \xxxyalola\ xxxhesyla xxxnihimbilira. {xxxpus xxxyalola xxxnihimbilira}
			secondaryPathsToJoinWords.Clear();
			secondaryPathsToBreakPhrases.Clear();
			tapb.BreakPhrase(1, 0, secondaryPathsToBreakPhrases, secondaryPathsToJoinWords, null);
			tapb.ValidateAnnotations();

			// [\xxxpus\ \xxxyalola\ \xxxnihimbilira\]. xxxpus xxxyalola xxxhesyla xxxnihimbilira. {\xxxpus\ \xxxyalola\ \xxxnihimbilira\}
			secondaryPathsToBreakPhrases.Clear();
			// 7.0+ secondaryPathsToBreakPhrases.Add(new int[2] { 2, 0 }); // {\xxxpus\ \xxxyalola\ \xxxnihimbilira\}
			tapb.BreakPhrase(0, 0, secondaryPathsToBreakPhrases, secondaryPathsToJoinWords, null);
			tapb.ValidateAnnotations();

			// 7.0+ breaking the guessed phrase has to be an extra step.
			tapb.BreakPhrase(2, 0, secondaryPathsToBreakPhrases, secondaryPathsToJoinWords, null);
			tapb.ValidateAnnotations();

			// gloss the second occurrence of xxxyalola and check that we don't overwrite it with a secondary guess.
			secondaryPathsToJoinWords.Clear();
			secondaryPathsToBreakPhrases.Clear();
			// [xxxpus xxxyalola] xxxnihimbilira. xxxpus [xxxyalola] xxxhesyla xxxnihimbilira. {xxxpus xxxyalola} xxxnihimbilira
			tapb.SetDefaultWordGloss("xxxyalola", 1);
			secondaryPathsToJoinWords.Add(new int[2] { 2, 0 }); // {xxxpus xxxyalola} xxxnihimbilira
			tapb.MergeAdjacentAnnotations(0, 0, secondaryPathsToJoinWords, secondaryPathsToBreakPhrases);
			tapb.ValidateAnnotations();	// reparse so that we can 'confirm' "xxxpus xxxyalola" phrase.

			tapb.SetDefaultWordGloss("xxxpus xxxyalola", 0);	// 'confirm' this merge.
			tapb.ValidateAnnotations();

			// join the last occurrence of 'xxxpus xxxyalola xxxnihimbilira'
			// and check that we don't overwrite the first join of 'xxxpus xxxyalola' with a secondary guess.
			secondaryPathsToJoinWords.Clear();
			secondaryPathsToBreakPhrases.Clear();
			// [xxxpus xxxyalola] xxxnihimbilira. xxxpus [xxxyalola] xxxhesyla xxxnihimbilira. [xxxpus xxxyalola xxxnihimbilira]
			tapb.MergeAdjacentAnnotations(2, 0, secondaryPathsToJoinWords, secondaryPathsToBreakPhrases);
			tapb.ValidateAnnotations();	// reparse so that we can 'confirm' "xxxpus xxxyalola xxxnihimbilira" phrase.

			tapb.SetDefaultWordGloss("xxxpus xxxyalola xxxnihimbilira", 0);	// 'confirm' this merge.
			tapb.ValidateAnnotations();

			// make sure we can break our analysis.
			secondaryPathsToJoinWords.Clear();
			secondaryPathsToBreakPhrases.Clear();
			// [xxxpus xxxyalola] xxxnihimbilira. xxxpus [xxxyalola] xxxhesyla xxxnihimbilira. {\xxxpus\ \xxxyalola\} \xxxnihimbilira\
			// In 6.0, apparently we would re-guess the shorter pus yalola phrase. In 7.0+, breaking a phrase does not
			// produce a new parse and new phrase guesses. Otherwise, if the longer phrase still existed somewhere else,
			// it would be guessed again! Don't see how this ever worked right, except that after breaking the phrase the
			// user would usually annotate the parts before there was occasion to re-parse.
			// 7.0+ secondaryPathsToJoinWords.Add(new int[2] { 2, 0 }); // {xxxpus xxxyalola} xxxnihimbilira
			tapb.BreakPhrase(2, 0, secondaryPathsToBreakPhrases, secondaryPathsToJoinWords, "xxxpus xxxyalola");
			tapb.ValidateAnnotations();
		}

		/// <summary>
		/// Test editing the text after making a phrase.
		/// </summary>
		[Test]
		public void Phrase_SimpleEdits_LT6244()
		{
			// 1. Make Phrases
			IList<int[]> secondaryPathsToJoinWords = new List<int[]>();
			IList<int[]> secondaryPathsToBreakPhrases = new List<int[]>();

			ParagraphBuilder pb = new ParagraphBuilder(m_textsDefn, m_text1, (int)Text1ParaIndex.PhraseWordforms);
			ParagraphAnnotatorForParagraphBuilder tapb = new ParagraphAnnotatorForParagraphBuilder(pb);
			pb.ParseParagraph();

			// first do a basic phrase (without secondary phrases (guesses))
			// xxxpus xxxyalola xxxnihimbilira. xxxpus xxxyalola [xxxhesyla xxxnihimbilira]. xxxpus xxxyalola xxxnihimbilira
			tapb.MergeAdjacentAnnotations(1, 2, secondaryPathsToJoinWords, secondaryPathsToBreakPhrases);
			tapb.ValidateAnnotations(true);

			// edit the second word in the phrase.
			pb.ReplaceSegmentForm("xxxhesyla xxxnihimbilira", 0, "xxxhesyla xxxra");
			pb.BreakPhraseAnnotation(1, 2);  // this edit should break the phrase back into words.
			tapb.ValidateAnnotations();
		}

		/// <summary>
		/// This tests some stuff that was dubious in 6.0 but pretty much automatic in 7.0+. We may want to drop it.
		/// </summary>
		[Test]
		public void SparseSegmentAnalyses_FreeformAnnotations_LT7318()
		{
			// Make some analyses linked to the same LexSense.
			//<!--xxxpus xxxyalola xxxnihimbilira. xxxnihimbilira xxxpus xxxyalola. xxxhesyla xxxnihimbilira.-->
			ParagraphBuilder pb = new ParagraphBuilder(m_textsDefn, m_text1, (int)Text1ParaIndex.SimpleSegmentPara);
			var segments = pb.ActualParagraph.SegmentsOS;
			Assert.AreEqual(3, segments.Count, "Unexpected number of senses.");

			// Load free form annotations for segments. shouldn't have any.
			Assert.That(segments[0].FreeTranslation.AvailableWritingSystemIds.Length, Is.EqualTo(0));
			Assert.That(segments[1].FreeTranslation.AvailableWritingSystemIds.Length, Is.EqualTo(0));
			Assert.That(segments[2].FreeTranslation.AvailableWritingSystemIds.Length, Is.EqualTo(0));

			// Try adding some freeform translations to third segment.
			segments[2].FreeTranslation.AnalysisDefaultWritingSystem =
				TsStringUtils.MakeString("Segment2: Freeform translation.", Cache.DefaultAnalWs);
			segments[2].LiteralTranslation.AnalysisDefaultWritingSystem =
				TsStringUtils.MakeString("Segment2: Literal translation.", Cache.DefaultAnalWs);

			// make sure the other segments don't have freeform annotations.
			Assert.That(segments[0].FreeTranslation.AvailableWritingSystemIds.Length, Is.EqualTo(0));
			Assert.That(segments[1].FreeTranslation.AvailableWritingSystemIds.Length, Is.EqualTo(0));

			// reparse the paragraph and make sure nothing changed.
			ParagraphAnnotatorForParagraphBuilder tapb = new ParagraphAnnotatorForParagraphBuilder(pb);
			tapb.ReparseParagraph();
			segments = (pb.ActualParagraph as IStTxtPara).SegmentsOS;

			Assert.That(segments[0].FreeTranslation.AvailableWritingSystemIds.Length, Is.EqualTo(0));
			Assert.That(segments[1].FreeTranslation.AvailableWritingSystemIds.Length, Is.EqualTo(0));
			Assert.That(segments[2].FreeTranslation.AnalysisDefaultWritingSystem.Text, Is.EqualTo("Segment2: Freeform translation."));
			Assert.That(segments[2].LiteralTranslation.AnalysisDefaultWritingSystem.Text, Is.EqualTo("Segment2: Literal translation."));
		}

		/// <summary>
		/// This is a test adapted from 6.0, where I think it mainly tested various virtual properties that no longer
		/// exist. It sets up some of the data that would be needed to find example sentences.
		/// </summary>
		[Test]
		public void FindExampleSentences()
		{
			// Make some analyses linked to the same LexSense.
			ParagraphBuilder pb = new ParagraphBuilder(m_textsDefn, m_text1, (int)Text1ParaIndex.SimpleSegmentPara);
			ParagraphAnnotatorForParagraphBuilder tapb = new ParagraphAnnotatorForParagraphBuilder(pb);
			ParagraphParser.ParseParagraph(pb.ActualParagraph);
			// Create a new lexical entry and sense.
			string formLexEntry = "xnihimbilira";
			var morphTypeRepository = Cache.ServiceLocator.GetInstance<IMoMorphTypeRepository>();
			var rootMorphType = morphTypeRepository. GetObject(MoMorphTypeTags.kguidMorphRoot);
			ITsString tssLexEntryForm = TsStringUtils.MakeString(formLexEntry, Cache.DefaultVernWs);
			var entryFactory = Cache.ServiceLocator.GetInstance<ILexEntryFactory>();
			ILexEntry xnihimbilira_Entry = entryFactory.Create(rootMorphType, tssLexEntryForm, "xnihimbilira.sense1", null);

			ILexSense xnihimbilira_Sense1 = xnihimbilira_Entry.SensesOS[0];
			var senseFactory = Cache.ServiceLocator.GetInstance<ILexSenseFactory>();
			ILexSense xnihimbilira_Sense2 = senseFactory.Create(xnihimbilira_Entry, null, "xnihimbilira.sense2");
			//<!--xxxpus xxxyalola xxxnihimbilira. xxxnihimbilira xxxpus xxxyalola. xxxhesyla xxxnihimbilira.-->
			ArrayList moForms = new ArrayList();
			moForms.Add("xx");
			moForms.Add(xnihimbilira_Entry.LexemeFormOA);
			// 1. Establish first analysis with Sense1
			IWfiAnalysis wfiAnalysis1 = tapb.BreakIntoMorphs(1, 0, moForms);
			tapb.SetMorphSense(1, 0, 1, xnihimbilira_Sense1);
			Assert.That(xnihimbilira_Sense1.ReferringObjects, Has.Count.EqualTo(1));
			Assert.That(xnihimbilira_Sense2.ReferringObjects, Has.Count.EqualTo(0));

			IWfiAnalysis waCba1_0 = pb.ActualParagraph.SegmentsOS[1].AnalysesRS[0] as IWfiAnalysis;
			Assert.IsNotNull(waCba1_0,
							 String.Format("Unexpected class({0}) of Analysis({1}) for cba0.",
							 pb.ActualParagraph.SegmentsOS[0].AnalysesRS[2].GetType(),
							 pb.ActualParagraph.SegmentsOS[0].AnalysesRS[2].Hvo));
			Assert.AreEqual(xnihimbilira_Sense1, waCba1_0.MorphBundlesOS[1].SenseRA);
			Assert.That(waCba1_0.ReferringObjects, Has.Count.EqualTo(1)); // one ref to the analysis (from segment 0)
			var wordform = (IWfiWordform)waCba1_0.Owner;
			Assert.That(wordform.FullConcordanceCount, Is.EqualTo(3)); // unchanged even though one is now an analysis

			// 2. Establish word gloss on the existing analysis.
			string wordGloss1;
			tapb.SetDefaultWordGloss(2, 1, wfiAnalysis1, out wordGloss1);
			Assert.That(wordform.FullConcordanceCount, Is.EqualTo(3)); // analysis and gloss and unchanged wordform all count

			var wgCba2_1 = pb.ActualParagraph.SegmentsOS[2].AnalysesRS[1] as IWfiGloss;
			Assert.IsNotNull(wgCba2_1,
							 String.Format("Unexpected class({0}) of InstanceOf({1}) for cba1.",
							 pb.ActualParagraph.SegmentsOS[2].AnalysesRS[1].GetType(), pb.ActualParagraph.SegmentsOS[2].AnalysesRS[1].Hvo));
			var waCba2_1 = wgCba2_1.Owner as IWfiAnalysis;
			Assert.AreEqual(xnihimbilira_Sense1.Hvo, waCba2_1.MorphBundlesOS[1].SenseRA.Hvo);

			// 3. establish a new analysis with Sense1.
			tapb.BreakIntoMorphs(0, 2, moForms); // xxxnihimbilira (first occurrence)
			tapb.SetMorphSense(0, 2, 1, xnihimbilira_Sense1);
			Assert.That(wordform.FullConcordanceCount, Is.EqualTo(3));

			var waCba0_2 = pb.ActualParagraph.SegmentsOS[0].AnalysesRS[2] as IWfiAnalysis;
			Assert.IsNotNull(waCba0_2,
							 String.Format("Unexpected class({0}) of InstanceOf({1}) for cba0.",
							 pb.ActualParagraph.SegmentsOS[2].AnalysesRS[1].GetType(), pb.ActualParagraph.SegmentsOS[2].AnalysesRS[1].Hvo));
			Assert.AreEqual(xnihimbilira_Sense1.Hvo, waCba0_2.MorphBundlesOS[1].SenseRA.Hvo);
			Assert.That(xnihimbilira_Sense1.ReferringObjects, Has.Count.EqualTo(2));
			Assert.That(xnihimbilira_Sense2.ReferringObjects, Has.Count.EqualTo(0));

			// 4. change an existing sense to sense2.
			tapb.SetMorphSense(0, 2, 1, xnihimbilira_Sense2);
			Assert.That(xnihimbilira_Sense1.ReferringObjects, Has.Count.EqualTo(1));
			Assert.That(xnihimbilira_Sense2.ReferringObjects, Has.Count.EqualTo(1));

			waCba0_2 = pb.ActualParagraph.SegmentsOS[0].AnalysesRS[2] as IWfiAnalysis;
			Assert.IsNotNull(waCba0_2,
							 String.Format("Unexpected class({0}) of InstanceOf({1}) for cba0.",
							 pb.ActualParagraph.SegmentsOS[2].AnalysesRS[1].GetType(), pb.ActualParagraph.SegmentsOS[2].AnalysesRS[1].Hvo));
			Assert.AreEqual(xnihimbilira_Sense2, waCba0_2.MorphBundlesOS[1].SenseRA);

			// do multiple occurrences of the same sense in the same segment.
			tapb.BreakIntoMorphs(0, 0, moForms); // break xxxpus into xx xnihimbilira (for fun).
			tapb.SetMorphSense(0, 0, 1, xnihimbilira_Sense2);
			Assert.That(xnihimbilira_Sense2.ReferringObjects, Has.Count.EqualTo(2));

			// reparse paragraph and make sure important stuff doesn't change
			tapb.ReparseParagraph();
			Assert.That(xnihimbilira_Sense1.ReferringObjects, Has.Count.EqualTo(1));
			Assert.That(xnihimbilira_Sense2.ReferringObjects, Has.Count.EqualTo(2));
		}

		/// <summary>
		/// Test editing simple segment paragraph with sparse analyses.
		/// xxxpus [xxxyalola] xxxnihimbilira. xxxnihimbilira [xxxpus] xxxyalola. xxxhesyla [xxxnihimbilira].
		/// </summary>
		[Test]
		public void SparseAnalyses_SimpleEdits_SimpleSegmentParagraph_DuplicateWordforms()
		{
			// First set sparse analyses on wordforms that have multiple occurrences.
			ParagraphBuilder pb = new ParagraphBuilder(m_textsDefn, m_text1, (int)Text1ParaIndex.SimpleSegmentPara);
			ParagraphAnnotatorForParagraphBuilder tapb = new ParagraphAnnotatorForParagraphBuilder(pb);
			pb.ParseParagraph();
			IWfiGloss gloss_xxxyalola0_1 = tapb.SetDefaultWordGloss("xxxyalola", 0);		// gloss first occurrence.
			IWfiGloss gloss_xxxpus1_1 = tapb.SetDefaultWordGloss("xxxpus", 1);			// gloss second occurrence.
			IWfiGloss gloss_xxxnihimbilira2_1 = tapb.SetDefaultWordGloss("xxxnihimbilira", 2);	// gloss third occurrence.
			pb.ParseParagraph();
			var actualAnalysis_xxxyalola0_1 = tapb.GetAnalysis(0, 1);
			var actualAnalysis_xxxpus1_1 = tapb.GetAnalysis(1, 1);
			var actualAnalysis_xxxnihimbilira2_1 = tapb.GetAnalysis(2, 1);
			Assert.AreEqual(gloss_xxxyalola0_1, actualAnalysis_xxxyalola0_1);
			Assert.AreEqual(gloss_xxxpus1_1, actualAnalysis_xxxpus1_1);
			Assert.AreEqual(gloss_xxxnihimbilira2_1, actualAnalysis_xxxnihimbilira2_1);
			// verify the rest
			tapb.ValidateAnnotations();
			// Replace some occurrences of these wordforms from the text to validate the analysis does not show up on the wrong occurrence.
			// Remove the first occurrence of 'xxxnihimbilira'; the (newly) second occurrence should still have the gloss.
			pb.ReplaceSegmentForm("xxxnihimbilira", 0, "");
			pb.RebuildParagraphContentFromAnnotations();
			pb.ParseParagraph();
			actualAnalysis_xxxnihimbilira2_1 = tapb.GetAnalysis(2, 1);
			Assert.AreEqual(gloss_xxxnihimbilira2_1, actualAnalysis_xxxnihimbilira2_1);
			tapb.ValidateAnnotations();
			// Remove first occurrence of 'xxxpus'; the next one should still have the gloss.
			pb.ReplaceSegmentForm("xxxpus", 0, "");
			pb.RebuildParagraphContentFromAnnotations();
			pb.ParseParagraph();
			actualAnalysis_xxxpus1_1 = tapb.GetAnalysis(1, 1);
			Assert.AreEqual(gloss_xxxpus1_1, actualAnalysis_xxxpus1_1);
			tapb.ValidateAnnotations();
		}

		/// <summary/>
		[Test]
		public void SparseAnalyses_SimpleEdits_SimpleSegmentParagraph_DuplicateWordforms_RemoveSegment_LT5376()
		{
			// First set sparse analyses on wordforms that have multiple occurrences.
			ParagraphBuilder pb = new ParagraphBuilder(m_textsDefn, m_text1, (int)Text1ParaIndex.SimpleSegmentPara);
			pb.ParseParagraph();
			ParagraphAnnotatorForParagraphBuilder tapb = new ParagraphAnnotatorForParagraphBuilder(pb);
			tapb.SetDefaultWordGloss("xxxyalola", 0);		// gloss first occurrence.
			tapb.ValidateAnnotations();
			// Remove first sentence containing 'xxxyalola'; its annotation should be removed.
			pb.RemoveSegment(0);
			pb.RebuildParagraphContentFromAnnotations();
			pb.ParseParagraph();
			tapb.ValidateAnnotations();
		}

		/// <summary>
		///			1		  2		 3		 4		  5		  6		 7		  8		  9
		/// 0123456 789012345 67890123456789012345678901234567 890123 4567890123456789012345 67890123456789 0123456789
		/// xxxpus [xxxyalola] xxxnihimbilira. xxxnihimbilira [xxxpus] xxxyalola. xxxhesyla [xxxnihimbilira].
		/// xxxpus  [xxxyalola] xxxnihimbilira. xxxnihimbilira [xxxpus] xxxyalola. xxxhesyla [xxxnihimbilira].
		/// </summary>
		[Test]
		public void SparseAnalyses_SimpleEdits_SimpleSegmentParagraph_DuplicateWordforms_AddWhitespace_LT5313()
		{
			// First set sparse analyses on wordforms that have multiple occurrences.
			ParagraphBuilder pb = new ParagraphBuilder(m_textsDefn, m_text1, (int)Text1ParaIndex.SimpleSegmentPara);
			pb.ParseParagraph();
			ParagraphAnnotatorForParagraphBuilder tapb = new ParagraphAnnotatorForParagraphBuilder(pb);
			string gloss;
			var gloss0_1 = tapb.SetDefaultWordGloss(0, 1, out gloss);   // xxxyalola 1
			var gloss1_1 = tapb.SetDefaultWordGloss(1, 1, out gloss);   // xxxpus 2
			var gloss2_1 = tapb.SetDefaultWordGloss(2, 1, out gloss);   // xxxnihimbilira 3

			tapb.ValidateAnnotations(); // precondition testing.
			// Append whitespace in the text, and see if the analyses still show up in the right place
			// (cf. LT-5313).
			pb.ReplaceTrailingWhitepace(0, 0, 1);
			pb.RebuildParagraphContentFromAnnotations();
			pb.ParseParagraph();
			Assert.AreEqual(gloss0_1, tapb.GetAnalysis(0, 1));
			Assert.AreEqual(gloss1_1, tapb.GetAnalysis(1, 1));
			Assert.AreEqual(gloss2_1, tapb.GetAnalysis(2, 1));
			tapb.ValidateAnnotations();
		}

		/// <summary>
		/// Xxxpus xxxyalola xxxnihimbilira. Xxxnihimbilira xxxpus Xxxyalola. Xxxhesyla XXXNIHIMBILIRA.
		/// </summary>
		[Test]
		public void SparseAnalyses_NoEdits_MixedCaseWordformsParagraph()
		{
			// First set sparse analyses on wordforms that have multiple occurrences.
			ParagraphBuilder pb = new ParagraphBuilder(m_textsDefn, m_text1, (int)Text1ParaIndex.MixedCases);
			ParagraphAnnotatorForParagraphBuilder tapb = new ParagraphAnnotatorForParagraphBuilder(pb);
			pb.ParseParagraph();

			// set the corresponding annotations to lowercase wordforms.
			IWfiWordform wf_xxxpus0_0 = tapb.SetAlternateCase("Xxxpus", 0, StringCaseStatus.allLower);
			IWfiWordform wf_xxxnihimbilira1_0 = tapb.SetAlternateCase("Xxxnihimbilira", 0, StringCaseStatus.allLower);
			IWfiWordform wf_xxxhesyla2_0 = tapb.SetAlternateCase("Xxxhesyla", 0, StringCaseStatus.allLower);
			IWfiWordform wf_xxxnihimbilira2_1 = tapb.SetAlternateCase("XXXNIHIMBILIRA", 0, StringCaseStatus.allLower);
			pb.ParseParagraph();
			Assert.AreEqual(wf_xxxpus0_0, tapb.GetAnalysis(0, 0));
			Assert.AreEqual(wf_xxxnihimbilira1_0, tapb.GetAnalysis(1, 0));
			Assert.AreEqual(wf_xxxhesyla2_0, tapb.GetAnalysis(2, 0));
			Assert.AreEqual(wf_xxxnihimbilira2_1, tapb.GetAnalysis(2, 1));
			tapb.ValidateAnnotations();
		}

		/// <summary>
		/// test that the paragraph parser can maintain sparse analyses in a simple paragraph.
		/// xxxpus xxxyalola xxxnihimbilira. xxxnihimbilira xxxpus [xxxyalola]. xxxhesyla [xxxnihimbilira].
		/// </summary>
		[Test]
		public void SparseAnalyses_NoEdits_SimpleSegmentParagraph()
		{
			// First set sparse analyses on wordforms that have multiple occurrences.
			ParagraphBuilder pb = new ParagraphBuilder(m_textsDefn, m_text1, (int)Text1ParaIndex.SimpleSegmentPara);
			ParagraphAnnotatorForParagraphBuilder tapb = new ParagraphAnnotatorForParagraphBuilder(pb);
			pb.ParseParagraph();
			string gloss;
			IWfiGloss gloss_xxxyalola1_2 = tapb.SetDefaultWordGloss(1, 2, out gloss);
			IWfiGloss gloss_xxxnihimbilira2_1 = tapb.SetDefaultWordGloss(2, 1, out gloss);

			// now parse through the text and make sure our two glosses are maintained.
			pb.ParseParagraph();
			Assert.AreEqual(gloss_xxxyalola1_2, tapb.GetAnalysis(1, 2));
			Assert.AreEqual(gloss_xxxnihimbilira2_1, tapb.GetAnalysis(2, 1));
			// validate the rest of the stuff.
			tapb.ValidateAnnotations();
		}

		/// <summary>
		/// Tests that ambiguous-cased (segment-initial) words prefer matching extant lowercase WF's to creating new.
		/// Simple: xxxpus xxxyalola xxxnihimbilira. xxxnihimbilira xxxpus xxxyalola. xxxhesyla xxxnihimbilira.
		/// Mixed:  Xxxpus xxxyalola xxxnihimbilira. Xxxnihimbilira xxxpus Xxxyalola. Xxxhesyla XXXNIHIMBILIRA.
		/// </summary>
		[Test]
		public void SegmentInitialUppercaseWordMatchesLowercaseWordform()
		{
			// prepopulate lowercase Wordforms
			new ParagraphBuilder(m_textsDefn, m_text1, (int)Text1ParaIndex.SimpleSegmentPara).ParseParagraph();

			// Build and parse paragraph
			var pb = new ParagraphBuilder(m_textsDefn, m_text1, (int)Text1ParaIndex.MixedCases);
			var tapb = new ParagraphAnnotatorForParagraphBuilder(pb);
			pb.ParseParagraph();

			// Verify WF's are reused across case iff appropriate:
			Assert.AreEqual(   tapb.GetAnalysis(0, 0), tapb.GetAnalysis(1, 1), "Initial Xxxpus should have been interpreted as sentence case");
			Assert.AreNotEqual(tapb.GetAnalysis(0, 1), tapb.GetAnalysis(1, 2), "Mid-sentence Xxxyalola should not match lowercase WF");
			Assert.AreNotEqual(tapb.GetAnalysis(0, 2), tapb.GetAnalysis(1, 0),
				"Congratulations! You fixed it! Please Assert.AreEqual with this message: xxxnihimbilira should have been reused for segment initial.");
			Assert.AreNotEqual(tapb.GetAnalysis(0, 2), tapb.GetAnalysis(2, 1), "XXXNIHIMBILIRA should have been given its own all-caps WF");
		}

		/// <summary>
		///
		/// </summary>
		[Test]
		public void CheckValidGuessesAfterInsertNewWord_LT8467()
		{
			//NOTE: The new test paragraphs need to have all new words w/o duplicates so we can predict the guesses
			//xxxcrayzee xxxyouneek xxxsintents.

			// copy a text of first paragraph into a new paragraph to generate guesses.
			IStTxtPara paraGlossed = m_text1.ContentsOA.AddNewTextPara("Normal");
			IStTxtPara paraGuessed = m_text1.ContentsOA.AddNewTextPara("Normal");

			paraGlossed.Contents = TsStringUtils.MakeString("xxxcrayzee xxxyouneek xxxsintents.", Cache.DefaultVernWs);
			paraGuessed.Contents = paraGlossed.Contents;

			// collect expected guesses from the glosses in the first paragraph.
			ParagraphAnnotator paGlossed = new ParagraphAnnotator(paraGlossed);
			ParagraphParser.ParseText(m_text1.ContentsOA);
			IList<IWfiGloss> expectedGuesses = paGlossed.SetupDefaultWordGlosses();

			// then verify we've created guesses for the new text.
			IList<IWfiGloss> expectedGuessesBeforeEdit = expectedGuesses;
			ValidateGuesses(expectedGuessesBeforeEdit, paraGuessed);

			// now edit the paraGuessed and expected Guesses.
			paraGuessed.Contents = TsStringUtils.MakeString("xxxcrayzee xxxguessless xxxyouneek xxxsintents.", Cache.DefaultVernWs);
			IList<IWfiGloss> expectedGuessesAfterEdit = new List<IWfiGloss>(expectedGuesses);
			// we don't expect a guess for the inserted word, so insert 0 after first twfic.
			expectedGuessesAfterEdit.Insert(1, null);

			// Note: we need to use ParseText rather than ReparseParagraph, because it uses
			// code to Reuse dummy annotations.
			ParagraphParser.ParseText(m_text1.ContentsOA);
			ValidateGuesses(expectedGuessesAfterEdit, paraGuessed);
		}

		private void ValidateGuesses(IList<IWfiGloss> expectedGuesses, IStTxtPara paraWithGuesses)
		{
			var segsParaGuesses = paraWithGuesses.SegmentsOS;
			int iExpectedGuess = 0;
			var GuessServices = new AnalysisGuessServices(Cache);
			foreach (var segParaGuesses in segsParaGuesses)
			{
				var segFormsParaGuesses = segParaGuesses.AnalysesRS;
				Assert.AreEqual(expectedGuesses.Count, segFormsParaGuesses.Count);
				int ianalysis = 0;
				foreach (var analysis in segFormsParaGuesses)
				{
					IAnalysis hvoGuessActual = null;
					if ((analysis.HasWordform))
					{
						// makes sense to guess
						hvoGuessActual = GuessServices.GetBestGuess(new AnalysisOccurrence(segParaGuesses, ianalysis));
						if (hvoGuessActual is NullWAG)
							hvoGuessActual = null;
					}
					Assert.AreEqual(expectedGuesses[iExpectedGuess], hvoGuessActual, "Guess mismatch");
					iExpectedGuess++;
					ianalysis++;
				}
			}
		}

		private void CheckExpectedWordformsAndOccurrences(IStTxtPara para, Dictionary<string, int> expectedOccurrences)
		{
			var wordforms = (from seg in para.SegmentsOS
						   from analysis in seg.AnalysesRS
						   where analysis.HasWordform
						   select analysis.Wordform).Distinct();
			var wfiRepo = para.Services.GetInstance<IWfiWordformRepository>();
			List<string> expectedWordforms = new List<string>(expectedOccurrences.Keys);
			foreach (string key in expectedWordforms)
			{
				int ichWs = key.IndexOfAny(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' });
				string form = key.Substring(0, ichWs);
				int ws = Convert.ToInt32(key.Substring(ichWs));
				var wordform = wfiRepo.GetMatchingWordform(ws, form);
				Assert.That(wordform, Is.Not.Null, string.Format("Expected to find key {0} in ConcordanceWords.", key));
				Assert.That(wordforms, Has.Member(wordform),
					"The wordforms collected in last parse session doesn't contain the wordform "
					+ wordform.Form.VernacularDefaultWritingSystem.Text);

				// see if we match the expected occurrences.
				int occurrenceCount = wordform.OccurrencesInTexts.Count();
				Assert.AreEqual(expectedOccurrences[key], occurrenceCount,
								String.Format("Unexpected number of occurrences for wordform {0}", key));
			}
			Assert.AreEqual(expectedWordforms.Count, wordforms.Count(), "Word count mismatch.");
		}
	}
}
