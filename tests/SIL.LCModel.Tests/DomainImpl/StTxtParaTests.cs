// Copyright (c) 2011-2013 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
//
// File: StTxtParaTests.cs
// Responsibility: FW Team
// ---------------------------------------------------------------------------------------------

using NUnit.Framework;

namespace SIL.LCModel.DomainImpl
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	///
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[TestFixture]
	public class StTxtParaTests : MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{
		#region Member variables
		private IStText m_stText;
		#endregion

		#region Setup/Teardown
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates the test data.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected override void CreateTestData()
		{
			IText text = AddInterlinearTextToLangProj("My Interlinear Text");
			m_stText = text.ContentsOA;
		}
		#endregion

		#region Reference[ForSorting] method tests
		[Test]
		public void Reference_ForSorting()
		{
			var para1 = AddParaToMockedText(m_stText, null);
			AddRunToMockedPara(para1, "This text is indexed. It is also segmented.", null);

			var para2 = AddParaToMockedText(m_stText, null);
			AddRunToMockedPara(para2, "This is the second paragraph. It is runny. It has three sentences.", null);

			// SUT
			var reference = para1.Reference(para1.SegmentsOS[0], 10);
			var refForSort = para1.ReferenceForSorting(para1.SegmentsOS[0], 10);
			Assert.That(reference.Text, Is.EqualTo("My Inter 1.1"));
			Assert.That(refForSort.Text, Is.EqualTo("My Interlinear Text 0000000001.0000000001 0000000010"));
			reference = para1.Reference(para1.SegmentsOS[1], 25);
			refForSort = para1.ReferenceForSorting(para1.SegmentsOS[1], 25);
			Assert.That(reference.Text, Is.EqualTo("My Inter 1.2"));
			Assert.That(refForSort.Text, Is.EqualTo("My Interlinear Text 0000000001.0000000002 0000000025"));
			reference = para2.Reference(para2.SegmentsOS[0], 5);
			refForSort = para2.ReferenceForSorting(para2.SegmentsOS[0], 5);
			Assert.That(reference.Text, Is.EqualTo("My Inter 2.1"));
			Assert.That(refForSort.Text, Is.EqualTo("My Interlinear Text 0000000002.0000000001 0000000005"));
		}

		[Test]
		public void Reference_ForSorting_TextHasAbbr()
		{
			((IText)m_stText.Owner).Abbreviation.set_String(Cache.DefaultVernWs, "MIT");

			var para1 = AddParaToMockedText(m_stText, null);
			AddRunToMockedPara(para1, "This text is indexed. It is also segmented.", null);

			var para2 = AddParaToMockedText(m_stText, null);
			AddRunToMockedPara(para2, "This is the second paragraph that is in this text", null);

			// SUT
			var reference = para1.Reference(para1.SegmentsOS[0], 10);
			var refForSort = para1.ReferenceForSorting(para1.SegmentsOS[0], 10);
			Assert.That(reference.Text, Is.EqualTo("MIT 1.1"));
			Assert.That(refForSort.Text, Is.EqualTo("MIT 0000000001.0000000001 0000000010"));
			reference = para1.Reference(para1.SegmentsOS[1], 25);
			refForSort = para1.ReferenceForSorting(para1.SegmentsOS[1], 25);
			Assert.That(reference.Text, Is.EqualTo("MIT 1.2"));
			Assert.That(refForSort.Text, Is.EqualTo("MIT 0000000001.0000000002 0000000025"));
			reference = para2.Reference(para2.SegmentsOS[0], 5);
			refForSort = para2.ReferenceForSorting(para2.SegmentsOS[0], 5);
			Assert.That(reference.Text, Is.EqualTo("MIT 2.1"));
			Assert.That(refForSort.Text, Is.EqualTo("MIT 0000000002.0000000001 0000000005"));
			reference = para2.Reference(para2.SegmentsOS[0], 34);
			refForSort = para2.ReferenceForSorting(para2.SegmentsOS[0], 34);
			Assert.That(reference.Text, Is.EqualTo("MIT 2.1"));
			Assert.That(refForSort.Text, Is.EqualTo("MIT 0000000002.0000000001 0000000034"));
		}

		[Test]
		public void Reference_ForSorting_TextHasNoTitleOrAbbr()
		{
			var untitledText = AddInterlinearTextToLangProj(Strings.ksStars).ContentsOA;

			var para1 = AddParaToMockedText(untitledText, null);
			AddRunToMockedPara(para1, "This is text.", null);

			// SUT
			var reference = para1.Reference(para1.SegmentsOS[0], 5);
			var refForSort = para1.ReferenceForSorting(para1.SegmentsOS[0], 5);
			Assert.That(reference.Text, Is.EqualTo("1.1"));
			Assert.That(refForSort.Text, Is.EqualTo(" 0000000001.0000000001 0000000005"));
		}

		[TestCase(0, ExpectedResult = "0000000000")]
		[TestCase(1, ExpectedResult = "0000000001")]
		[TestCase(12, ExpectedResult = "0000000012")]
		[TestCase(512, ExpectedResult = "0000000512")]
		[TestCase(int.MaxValue, ExpectedResult = "2147483647")]
		public string ZeroPadForStringComparison(int i) => StTxtPara.ZeroPadForStringComparison(i);
		#endregion Reference[ForSorting] method tests

		#region ReplaceTextRange method tests
		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the method ReplaceTextRange when appending text from another paragraph.
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void ReplaceTextRange_Append()
		{
			IStTxtPara para1 = AddParaToMockedText(m_stText, "Monkey");
			AddRunToMockedPara(para1, "This is text.", null);
			AddSegmentTrans(para1, 0, "Hello");
			para1.ParseIsCurrent = true;

			IStTxtPara para2 = AddParaToMockedText(m_stText, "Monkey");
			AddRunToMockedPara(para2, "So what?", null);
			AddSegmentTrans(para2, 0, "there");
			para2.ParseIsCurrent = true;

			para1.ReplaceTextRange(para1.Contents.Length, para1.Contents.Length, para2, 0, para2.Contents.Length);

			VerifyPara(para1, "This is text.So what?");
			VerifyParaSegments(para1, "Hello", "there");
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the method ReplaceTextRange when appending text from another paragraph when
		/// paragraph one ends with non-wordforming characters and paragraph two starts with
		/// non-wordforming characters. (TE-9287)
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void ReplaceTextRange_Append_WithNWFC()
		{
			IStTxtPara para1 = AddParaToMockedText(m_stText, "Monkey");
			AddRunToMockedPara(para1, "\"This is text.\"", null);
			AddSegmentTrans(para1, 0, "Hello");
			para1.ParseIsCurrent = true;

			IStTxtPara para2 = AddParaToMockedText(m_stText, "Monkey");
			AddRunToMockedPara(para2, "\"So what?\"", null);
			AddSegmentTrans(para2, 0, "there");
			para2.ParseIsCurrent = true;

			para1.ReplaceTextRange(para1.Contents.Length, para1.Contents.Length, para2, 0, para2.Contents.Length);

			VerifyPara(para1, "\"This is text.\"\"So what?\"");
			VerifyParaSegments(para1, "Hello", "there");
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the method ReplaceTextRange when inserting text from another paragraph.
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void ReplaceTextRange_InsertAtBeginning()
		{
			IStTxtPara para1 = AddParaToMockedText(m_stText, "Monkey");
			AddRunToMockedPara(para1, "This is text.", null);
			AddSegmentTrans(para1, 0, "Hello");
			para1.ParseIsCurrent = true;

			IStTxtPara para2 = AddParaToMockedText(m_stText, "Monkey");
			AddRunToMockedPara(para2, "So what?", null);
			AddSegmentTrans(para2, 0, "there");
			para2.ParseIsCurrent = true;

			para1.ReplaceTextRange(0, 0, para2, 0, para2.Contents.Length);

			VerifyPara(para1, "So what?This is text.");
			VerifyParaSegments(para1, "there", "Hello");
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the method ReplaceTextRange when inserting text from another paragraph when
		/// there are non-wordforming characters at the segment boundaries. (TE-9287)
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void ReplaceTextRange_InsertAtBeginning_WithNWFC()
		{
			IStTxtPara para1 = AddParaToMockedText(m_stText, "Monkey");
			AddRunToMockedPara(para1, "\"This is text.\"", null);
			AddSegmentTrans(para1, 0, "Hello");
			para1.ParseIsCurrent = true;

			IStTxtPara para2 = AddParaToMockedText(m_stText, "Monkey");
			AddRunToMockedPara(para2, "\"So what?\"", null);
			AddSegmentTrans(para2, 0, "there");
			para2.ParseIsCurrent = true;

			para1.ReplaceTextRange(0, 0, para2, 0, para2.Contents.Length);

			VerifyPara(para1, "\"So what?\"\"This is text.\"");
			VerifyParaSegments(para1, "there", "Hello");
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the method ReplaceTextRange when replacing the end of the first paragraph with
		/// the contents of the second replacing the last full segment.
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void ReplaceTextRange_ReplaceRangeAtEnd_WholeSegment()
		{
			IStTxtPara para1 = AddParaToMockedText(m_stText, "Monkey");
			AddRunToMockedPara(para1, "This is text. This text might be gone.", null);
			AddSegmentTrans(para1, 0, "My");
			AddSegmentTrans(para1, 1, "text");
			para1.ParseIsCurrent = true;

			IStTxtPara para2 = AddParaToMockedText(m_stText, "Monkey");
			AddRunToMockedPara(para2, "So what?", null);
			AddSegmentTrans(para2, 0, "there");
			para2.ParseIsCurrent = true;

			const int ich = 14; // right before 'This'
			para1.ReplaceTextRange(ich, para1.Contents.Length, para2, 0, para2.Contents.Length);

			VerifyPara(para1, "This is text. So what?");
			VerifyParaSegments(para1, "My", "there");
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the method ReplaceTextRange when replacing the end of the first paragraph with
		/// the contents of the second replacing part of the last segment.
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void ReplaceTextRange_ReplaceRangeAtEnd_PartialSegment()
		{
			IStTxtPara para1 = AddParaToMockedText(m_stText, "Monkey");
			AddRunToMockedPara(para1, "This is text. This text might be gone.", null);
			AddSegmentTrans(para1, 0, "My");
			AddSegmentTrans(para1, 1, "text");
			para1.ParseIsCurrent = true;

			IStTxtPara para2 = AddParaToMockedText(m_stText, "Monkey");
			AddRunToMockedPara(para2, "So what?", null);
			AddSegmentTrans(para2, 0, "there");
			para2.ParseIsCurrent = true;

			const int ich = 19; // right after the space following 'This'
			para1.ReplaceTextRange(ich, para1.Contents.Length, para2, 0, para2.Contents.Length);

			VerifyPara(para1, "This is text. This So what?");
			VerifyParaSegments(para1, "My", "text there");
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the method ReplaceTextRange when replacing the end of the first paragraph with
		/// the contents of the second.
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void ReplaceTextRange_ReplaceRangeAtBeginning_WholeSegment()
		{
			IStTxtPara para1 = AddParaToMockedText(m_stText, "Monkey");
			AddRunToMockedPara(para1, "This is text. This text might be gone.", null);
			AddSegmentTrans(para1, 0, "My");
			AddSegmentTrans(para1, 1, "text");
			para1.ParseIsCurrent = true;

			IStTxtPara para2 = AddParaToMockedText(m_stText, "Monkey");
			AddRunToMockedPara(para2, "So what?", null);
			AddSegmentTrans(para2, 0, "there");
			para2.ParseIsCurrent = true;

			const int ich = 14; // right before the second 'This'
			para1.ReplaceTextRange(0, ich, para2, 0, para2.Contents.Length);

			VerifyPara(para1, "So what?This text might be gone.");
			VerifyParaSegments(para1, "there", "text");
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the method ReplaceTextRange when replacing the end of the first paragraph with
		/// the contents of the second.
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void ReplaceTextRange_ReplaceRangeAtBeginning_PartialSegment()
		{
			IStTxtPara para1 = AddParaToMockedText(m_stText, "Monkey");
			AddRunToMockedPara(para1, "This is text. This text might be gone.", null);
			AddSegmentTrans(para1, 0, "My");
			AddSegmentTrans(para1, 1, "text");
			para1.ParseIsCurrent = true;

			IStTxtPara para2 = AddParaToMockedText(m_stText, "Monkey");
			AddRunToMockedPara(para2, "So what?", null);
			AddSegmentTrans(para2, 0, "there");
			para2.ParseIsCurrent = true;

			const int ich = 8; // right after the space following 'is'
			para1.ReplaceTextRange(0, ich, para2, 0, para2.Contents.Length);

			VerifyPara(para1, "So what?text. This text might be gone.");
			VerifyParaSegments(para1, "there", "My", "text");
		}
		#endregion

		#region Helper methods
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Adds a free translation and analyses to the specified segment on the specified
		/// paragraph
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private static void AddSegmentTrans(IStTxtPara para, int iSeg, string transFT)
		{
			AddSegmentFt(para, iSeg, transFT, para.Cache.DefaultAnalWs);
			ISegment seg = para.SegmentsOS[iSeg];
			LcmTestHelper.CreateAnalyses(seg, para.Contents, seg.BeginOffset, seg.EndOffset, true);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Verifies the state of the specified paragraph.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private static void VerifyPara(IStTxtPara para, string contents)
		{
			Assert.AreEqual(contents, para.Contents.Text);
			Assert.AreEqual(1, para.Contents.RunCount);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Verifies the free translations for the segments of the specified paragraph.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private static void VerifyParaSegments(IStTxtPara para, params string[] segmentFTs)
		{
			Assert.AreEqual(segmentFTs.Length, para.SegmentsOS.Count);
			for (int i = 0; i < segmentFTs.Length; i++)
			{
				Assert.AreEqual(segmentFTs[i], para.SegmentsOS[i].FreeTranslation.AnalysisDefaultWritingSystem.Text,
					"Free translation for segment " + i + " is wrong");
				LcmTestHelper.VerifyAnalysis(para.SegmentsOS[i], i, new int[0], new int[0]);
			}
		}
		#endregion
	}
}
