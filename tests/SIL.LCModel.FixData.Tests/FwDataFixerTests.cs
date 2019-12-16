﻿// Copyright (c) 2015-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using NUnit.Framework;
using SIL.TestUtilities;

namespace SIL.LCModel.FixData.Tests
{
	[TestFixture]
	class FwDataFixerTests
	{
		private const string WordformswithsameformTestDir = "WordformsWithSameForm";
		private List<string> _errors = new List<string>();
		private int _errorsFixed = 0;
		private void LogErrors(string message, bool errorFixed)
		{
			_errors.Add(message);
			if (errorFixed)
				++_errorsFixed;
		}

		private int ErrorCount()
		{
			return _errorsFixed;
		}

		private static XmlDocument GetResult(string filePath)
		{
			XmlDocument doc = new XmlDocument();
			doc.Load(filePath);
			//Don't write this to the console. It's currently 209,000 lines that takes about 5 minutes to write.
			//Console.WriteLine(File.ReadAllText(filePath));
			return doc;
		}

		internal XmlNodeList VerifyEntryExists(XmlDocument xmlDoc, string xPath)
		{
			XmlNodeList selectedEntries = xmlDoc.SelectNodes(xPath);
			Assert.IsNotNull(selectedEntries);
			Assert.AreEqual(1, selectedEntries.Count, String.Format("An entry with the following criteria should exist:{0}", xPath));
			return selectedEntries;
		}

		private string _basePath;

		private readonly string[] _testFileDirectories =
			{
				"DuplicateGuid", "DanglingCustomListReference", "DanglingCustomProperty", "DanglingReference",
				"DuplicateWs", "SequenceFixer", "EntryWithExtraMSA", "EntryWithMsaAndNoSenses", "EntryExtraMsaAndBustedSenseRef", "TagAndCellRefs", "GenericDates",
				"HomographFixer", WordformswithsameformTestDir, "MorphBundleProblems", "MissingBasicCustomField", "DeletedMsaRefBySenseAndBundle",
				"DuplicateNameCustomList", "SingleTargetLexRefs", "DuplicateStyles"
			};

		private string TestDataDirectory => Path.Combine(TestDirectoryFinder.RootDirectory, "tests", "SIL.LCModel.FixData.Tests", "TestData");

		[TestFixtureSetUp]
		public void FixtureSetup()
		{
			_basePath = TestDataDirectory;
			foreach (var testDir in _testFileDirectories)
				CopyTestData(testDir);
		}

		private void CopyTestData(string testFolder)
		{
			var testPath = Path.Combine(_basePath, testFolder);
			File.Copy(Path.Combine(testPath, "Test.fwdata"), Path.Combine(testPath, "BasicFixup.fwdata"), true);
			File.SetAttributes(Path.Combine(testPath, "BasicFixup.fwdata"), FileAttributes.Normal);
		}

		[TestFixtureTearDown]
		public void AllTestTearDown()
		{
			foreach (var testDir in _testFileDirectories)
				CleanupTestDir(testDir);
		}

		private void CleanupTestDir(string testDir)
		{
			var testPath = Path.Combine(_basePath, testDir);
			File.Delete(Path.Combine(testPath, "BasicFixup.fwdata"));
			File.Delete(Path.Combine(testPath, "BasicFixup.bak"));
		}

		[SetUp]
		public void Setup()
		{
			_errors.Clear();
			_errorsFixed = 0;
		}

		[Test]
		public void DuplicateNameCustomLists_OneIsRenamed()
		{
			var testPath = Path.Combine(_basePath, "DuplicateNameCustomList");

			var fixedDataPath = Path.Combine(testPath, "BasicFixup.fwdata");
			var originalFwData = File.ReadAllText(fixedDataPath);

			var data = new FwDataFixer(fixedDataPath, new DummyProgressDlg(), LogErrors, ErrorCount);
			data.FixErrorsAndSave();

			// Read the file data into memory once.
			var backupFwData = File.ReadAllText(Path.Combine(testPath, "BasicFixup.bak"));
			var fixedFwData = File.ReadAllText(fixedDataPath);

			Assert.AreEqual(originalFwData, backupFwData, "backup file should preserve original (bad) data");
			Assert.AreNotEqual(originalFwData, fixedFwData, "fixing data should have changed something!");

			// Verify initial state: two lists with the same name.
			AssertThatXmlIn.String(backupFwData).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='CmPossibilityList']/Name/AUni[text()='Custom test list']", 2);

			// Verify output: two lists with original IDs and second is renamed.
			AssertThatXmlIn.String(fixedFwData).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='CmPossibilityList' and @guid='5250350b-83fb-432b-a24f-a8dad580d350']/Name/AUni[text()='Custom test list']", 1);
			AssertThatXmlIn.String(fixedFwData).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='CmPossibilityList' and @guid='1e4c4389-7835-408f-9408-c7f1c488d737']/Name/AUni[text()='Custom test list1']", 1);

			Assert.AreEqual(1, _errors.Count, "Unexpected number of errors found");

			Assert.That(_errors[0], Is.EqualTo("Repairing duplicate lists both named \"Custom test list\". \"5250350b-83fb-432b-a24f-a8dad580d350\" kept the original name and \"1e4c4389-7835-408f-9408-c7f1c488d737\"  was renamed to \"Custom test list1\""),
				"Error message is incorrect for dup.");
		}

		[Test]
		public void DuplicateWordforms_AreMerged()
		{
			var testPath = Path.Combine(_basePath, WordformswithsameformTestDir);

			var fixedDataPath = Path.Combine(testPath, "BasicFixup.fwdata");
			var data = new FwDataFixer(fixedDataPath, new DummyProgressDlg(), LogErrors, ErrorCount);
			data.FixErrorsAndSave();

			// Test data has:
			// - Two wordforms with French form "dup". The second goes away, and its two analyses are moved to the first.
			// - a wordfom with a different French form ("other"). It is unaffected.
			// - a wordform with French form "dup", but also Spanish form "other". It is unaffected.
			// - two wordforms  with French form "dupFr" and Spanish form "dupSp". The alternatives are in the opposite order.
			//		Despite this they are merged, and the one Analysis of the second one is moved to the first, which
			//		previously had none.
			// - a segment which references one of the deleted wordforms, and is changed to reference the replacement.
			var firstdupGuid = "64cf9708-a7d4-4e1e-a403-deec87c34455"; // First wordform with simple form "dup"
			var secondDupGuid = "0964665E-BB56-4406-8310-ADE04A7A23C7"; // Second with simple form "dup"
			var analysis2_1Guid = "86DB9E97-2E6C-4AAC-B78B-9EDA834254E7"; // first analysis of deleted wordform
			var analysis2_2Guid = "BCD9971A-D871-472D-8843-9B5392AAA57F"; // second analysis of deleted wordform


			VerifyElementCount(fixedDataPath, "WfiWordform", firstdupGuid, 1); // First "dup" should survive
			VerifyElementCount(fixedDataPath, "WfiWordform", secondDupGuid, 0); // Second "dup" should go away
			VerifyElementCount(fixedDataPath, "WfiWordform", "31EBCDB7-8274-4776-A6E0-1AB523AA9E1E", 1); // Non-dup should survive
			VerifyElementCount(fixedDataPath, "WfiWordform", "D596FD07-A4E6-4ED1-A859-7601ACD2CD36", 1); // Partial duplicate should survive
			VerifyElementCount(fixedDataPath, "WfiAnalysis", analysis2_1Guid, 1); // First analysis of deleted wordform should survive
			VerifyElementCount(fixedDataPath, "WfiAnalysis", analysis2_2Guid, 1); // Second analysis of deleted wordform should survive

			// The surviving analyses should have their owners corrected. I think it's enough to check one.
			AssertThatXmlIn.File(fixedDataPath).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='WfiAnalysis' and @guid='" + analysis2_2Guid + "' and @ownerguid='" + firstdupGuid + "']", 1);

			// The merged "dup" wordform should have four analyses
			AssertThatXmlIn.File(fixedDataPath).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='WfiWordform' and @guid='" + firstdupGuid + "']/Analyses/objsur", 4);

			// One of which should be the last one from the deleted wordform.
			AssertThatXmlIn.File(fixedDataPath).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='WfiWordform' and @guid='" + firstdupGuid + "']/Analyses/objsur[@guid='BCD9971A-D871-472D-8843-9B5392AAA57F']", 1);

			// The segment which refers to the deleted wordform should be fixed.
			AssertThatXmlIn.File(fixedDataPath).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='Segment' and @guid='b405f3c0-58e1-4492-8a40-e955774a6911']/Analyses/objsur[@guid='" + firstdupGuid + "']", 1);

			// Merging these two verifies that it does the right things when (a) the second word of a set is the only one with analyses
			// and (b) the writing systems are in a different order
			var firstDupFrSp = "83A4D906-3ED1-49D1-AA5D-FC2DB938B6A4";
			VerifyElementCount(fixedDataPath, "WfiWordform", firstDupFrSp, 1); // first "dupFr/dupSp" should survive
			var secondDupSpFr = "D8444A90-A5CF-4163-B312-AFF577B0452E";
			VerifyElementCount(fixedDataPath, "WfiWordform", secondDupSpFr, 0); // second "dupFr/dupSp" should go away

			// The second merged wordform should have one analysis
			AssertThatXmlIn.File(fixedDataPath).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='WfiWordform' and @guid='" + firstDupFrSp + "']/Analyses/objsur", 1);

			// The surviving analyses should have their owners corrected. I think it's enough to check one.
			AssertThatXmlIn.File(fixedDataPath).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='WfiAnalysis' and @guid='AE2BA69A-42BA-4582-AFA4-B8AC3E5567C2' and @ownerguid='" + firstDupFrSp + "']", 1);


			Assert.AreEqual(2, _errors.Count, "Unexpected number of errors found");

			Assert.That(_errors[0], Is.EqualTo("Wordform with guid '" + secondDupGuid + "' has same form (fr>dup) as '" + firstdupGuid + "' and was merged"),
				"Error message is incorrect for dup.");
			Assert.That(_errors[1], Is.EqualTo("Wordform with guid '" + secondDupSpFr + "' has same form (fr>dupFr&sp>dupSp) as '" + firstDupFrSp + "' and was merged"),
				"Error message is incorrect for dupSpFr.");

			// Check original errors. I think it's enough to verify that the two elements the merger was supposed to delete
			// were originally present. If the properties that allowed them to be merged were missing, it wouldn't happen.
			// If the components that get moved were not present, they would not show up in the fixed data.
			string backupPath = Path.Combine(testPath, "BasicFixup.bak"); // the original data we corrected
			VerifyElementCount(backupPath, "WfiWordform", secondDupGuid, 1); // Second "dup" was there originally.
			VerifyElementCount(backupPath, "WfiWordform", secondDupSpFr, 1); // second "dupFr/dupSp" was there originally.
		}

		/// <summary>
		/// Verify that the object with the specified class and guid occurs the expected number of times (0 or 1)
		/// </summary>
		/// <param name="testPath"></param>
		/// <param name="className"></param>
		/// <param name="guid"></param>
		/// <param name="expectedCount"></param>
		private static void VerifyElementCount(string testPath, string className, string guid, int expectedCount)
		{
			AssertThatXmlIn.File(testPath).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"" + className + "\" and @guid=\"" + guid + "\"]", expectedCount);
		}

		[Test]
		public void MissingBasicCustomField()
		{
			var testPath = Path.Combine(_basePath, "MissingBasicCustomField");

			var fixedDataPath = Path.Combine(testPath, "BasicFixup.fwdata");
			var data = new FwDataFixer(fixedDataPath, new DummyProgressDlg(), LogErrors, ErrorCount);
			data.FixErrorsAndSave();

			// Verify initial state: no custom field element on one input, the other already has them.
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='MoStemAllomorph' and @guid='47ff8685-502a-4617-8ab7-9d27889b3b3f']/Custom", 0);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='MoStemAllomorph' and @guid='6c8f0104-dffb-43f3-9d1d-4c2ec2a4afa5']/Custom[@name='MyNumber' and @val='1']", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='MoStemAllomorph' and @guid='6c8f0104-dffb-43f3-9d1d-4c2ec2a4afa5']/Custom[@name='MyDate' and @val='2']", 1);

			// Verify Results: the allomorph has had the expected basic custom fields added.
			AssertThatXmlIn.File(fixedDataPath).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='MoStemAllomorph' and @guid='47ff8685-502a-4617-8ab7-9d27889b3b3f']/Custom[@name='MyNumber' and @val='0']", 1);
			AssertThatXmlIn.File(fixedDataPath).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='MoStemAllomorph' and @guid='47ff8685-502a-4617-8ab7-9d27889b3b3f']/Custom[@name='MyDate' and @val='0']", 1);
			// The one that already had them should still only have one of each.
			AssertThatXmlIn.File(fixedDataPath).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='MoStemAllomorph' and @guid='6c8f0104-dffb-43f3-9d1d-4c2ec2a4afa5']/Custom[@name='MyNumber' and @val='1']", 1);
			AssertThatXmlIn.File(fixedDataPath).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='MoStemAllomorph' and @guid='6c8f0104-dffb-43f3-9d1d-4c2ec2a4afa5']/Custom[@name='MyDate' and @val='2']", 1);
			AssertThatXmlIn.File(fixedDataPath).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='MoStemAllomorph' and @guid='6c8f0104-dffb-43f3-9d1d-4c2ec2a4afa5']/Custom[@name='MyNumber' and @val='0']", 0);
			AssertThatXmlIn.File(fixedDataPath).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='MoStemAllomorph' and @guid='6c8f0104-dffb-43f3-9d1d-4c2ec2a4afa5']/Custom[@name='MyDate' and @val='0']", 0);
			Assert.That(_errors[0], Is.StringStarting("Missing default value type added to "));
		}

		[Test]
		public void DuplicateGuids()
		{
			var testPath = Path.Combine(_basePath, "DuplicateGuid");
			// This test checks that duplicate guids are identified and that an error message is produced for them.
			string testGuid = "2110cf83-ad6c-47fe-91f8-8bf789473792";
			var data = new FwDataFixer(Path.Combine(testPath, "BasicFixup.fwdata"), new DummyProgressDlg(), LogErrors, ErrorCount);
			data.FixErrorsAndSave();
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexSense\" and @guid=\"" + testGuid + "\"]", 2);
			Assert.AreEqual(1, _errors.Count, "Unexpected number of errors found");
			Assert.True(_errors[0].EndsWith("Object with guid '" + testGuid + "' already exists! (not fixed)"),
				"Error message is incorrect."); // OriginalFixer--ksObjectWithGuidAlreadyExists
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexSense\" and @guid=\"" + testGuid + "\"]", 2);
		}

		[Test]
		public void DanglingReferences()
		{
			var testPath = Path.Combine(_basePath, "DanglingReference");
			// This test checks that dangling references guids are identified and removed
			// and that an error message is produced for them.
			string testObjsurGuid = "aaaaaaaa-e15a-448e-a618-3855f93bd3c2";
			string lexSenseGuid = "2210cf83-ad6c-47fe-91f8-8bf789473792";
			string lexEntryGuid = "64cf9708-a7d4-4e1e-a403-deec87c34455";
			string testChangeGuid = "cccccccc-a7d4-4e1e-a403-deec87c34455";
			string secondDangler = "408ba7ca-e15a-448e-aaaa-3855f93bd3c2";
			string partOfSpeechGuid = "4c90d669-cc98-49ea-8c9c-a739253336ed";
			string parfOfSpeechOwnerGuid = "8e45de56-5105-48dc-b302-05985432e1e7";
			var data = new FwDataFixer(Path.Combine(testPath, "BasicFixup.fwdata"), new DummyProgressDlg(), LogErrors, ErrorCount);
			data.FixErrorsAndSave();
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//objsur[@guid=\"" + testObjsurGuid + "\"]", 0);
			Assert.AreEqual(4, _errors.Count, "Unexpected number of errors found.");
			Assert.True(_errors[0].StartsWith("Removing dangling link to '" + testObjsurGuid + "' (class='LexEntry'"),
				"Error message is incorrect."); // OriginalFixer--ksRemovingLinkToNonexistingObject
			Assert.True(_errors[1].StartsWith("Changing ownerguid value from '" + testChangeGuid + "' to '" + lexEntryGuid
				+ "' (class='LexSense', guid='" + lexSenseGuid),
				"Error message is incorrect."); // OriginalFixer--ksRemovingLinkToNonexistingObject
			Assert.True(_errors[3].EndsWith("Removing object with nonexistent owner (invalid ownerguid='" + parfOfSpeechOwnerGuid
				+ "', class='PartOfSpeech', guid='" + partOfSpeechGuid + "')."),
				"Error message is incorrect."); // OriginalFixer--ksRemovingLinkToNonexistentOwner
			Assert.True(_errors[2].StartsWith("Removing dangling link to '" + secondDangler + "' (class='LexSense'"),
				"Error message is incorrect."); // OriginalFixer--ksRemovingLinkToNonexistingObject
			// The parent SenseType property of the danging <objsur> should be removed with it.
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexSense\" and @guid=\"3110cf83-ad6c-47fe-91f8-8bf789473792\"]/SenseType", 0);

			// Check original errors
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"PartOfSpeech\" and @ownerguid=\"" + parfOfSpeechOwnerGuid + "\"]", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexSense\" and @ownerguid=\"" + testChangeGuid + "\"]", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//objsur[@guid=\"" + testObjsurGuid + "\"]", 1);
			// Check that they were fixed
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"PartOfSpeech\" and @ownerguid=\"" + parfOfSpeechOwnerGuid + "\"]", 0);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexSense\" and @ownerguid=\"" + lexEntryGuid + "\"]", 2);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//objsur[@guid=\"" + testObjsurGuid + "\"]", 0);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//SenseType/objsur[@guid=\"" + secondDangler + "\"]", 0);
		}

		[Test]
		public void DuplicateStyles()
		{
			var testPath = Path.Combine(_basePath, "DuplicateStyles");
			// This test checks that styles with duplicate names are removed
			// and that an error message is produced for them.
			const string lpBob1 = "be765e3e-ea5e-11de-9d42-0013722f8dec";
			const string lpBob2 = "d37394b7-d47c-4e3d-9f2a-d023fd6fef24";
			string lpFred = "cea17237-b704-4bee-9a9b-1779d3108831";
			const string lpBob3 = "d778dff3-36fb-4ddf-aa08-91827f2c22dc";
			string scBob = "a89670f5-8d59-42ec-bfb5-10c63642b1dc";
			const string scJoe1 = "ac48acc9-12f2-42c6-89dc-7d48870b7758";
			const string scJoe2 = "ba7ffa0b-1beb-45fe-976a-c849cceb3026";
			var data = new FwDataFixer(Path.Combine(testPath, "BasicFixup.fwdata"), new DummyProgressDlg(), LogErrors, ErrorCount);
			data.FixErrorsAndSave();
			// Make sure the original styles are present
			foreach (var guid in new[] {lpBob1, lpBob2, scJoe1, scJoe2})
			{
				AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
					"//objsur[@guid=\"" + guid + "\"]", 1);
			}
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//objsur[@guid=\"" + lpBob3 + "\"]", 2);

			Assert.AreEqual(4, _errors.Count, "Unexpected number of errors found.");
			//The order of these doesn't really matter but this is the one we happen to get.
			Assert.That(_errors[0], Is.EqualTo("Removing duplicate style Bob."), "Error message is incorrect.");
			Assert.That(_errors[1], Is.StringStarting("Removing dangling link to"));
			Assert.That(_errors[2], Is.EqualTo("Removing duplicate style Bob."), "Error message is incorrect.");
			Assert.That(_errors[3], Is.EqualTo("Removing duplicate style Joe."), "Error message is incorrect.");

			// These should survive
			foreach (var guid in new[] {lpBob1, lpFred, scJoe1, scBob})
			{
				AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
					"//objsur[@guid=\"" + guid + "\"]", 1);
				AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
					"//rt[@guid=\"" + guid + "\"]", 1);
			}

			// These should not (the first xpath would match the reference to lpBob3 in fred as well as the owning one)
			foreach (var guid in new[] { lpBob2, lpBob3, scJoe2 })
			{
				AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
					"//objsur[@guid=\"" + guid + "\"]", 0);
				AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
					"//rt[@guid=\"" + guid + "\"]", 0);
			}
		}

		[Test]
		public void SingleTargetLexReferences()
		{
			var testPath = Path.Combine(_basePath, "SingleTargetLexRefs");
			// This test checks that LexReference objects with less than two Targets are identified and removed
			// and that an error message is produced for them.
			string lexRefDeleteGuid1 = "d4bea1a5-9877-4f16-bd56-9bb7ff6b2db1";
			string lexRefDeleteGuid2 = "1ad0d051-b388-45e1-a45e-48cb4274df90";
			string lexRefNotDeleted = "580dc185-b017-4d83-b302-8179538be3a1";
			string lexRefTypeChangedGuid = "0d5692fa-aa84-4bed-ab0a-f05186315fcd";
			var data = new FwDataFixer(Path.Combine(testPath, "BasicFixup.fwdata"), new DummyProgressDlg(), LogErrors, ErrorCount);
			data.FixErrorsAndSave();

			// Should be two errors reported
			Assert.AreEqual(2, _errors.Count, "Unexpected number of errors found.");
			Assert.True(_errors[0].StartsWith("Removing LexReference with too few references (Targets) (guid='" + lexRefDeleteGuid1 +
				"') from its owner (guid='" + lexRefTypeChangedGuid), "Error message is incorrect."); // SequenceFixer--ksRemovingBadLexReference
			Assert.True(_errors[1].StartsWith("Removing LexReference with too few references (Targets) (guid='" + lexRefDeleteGuid2 +
				"') from its owner (guid='" + lexRefTypeChangedGuid), "Error message is incorrect."); // SequenceFixer--ksRemovingBadLexReference

			// Check original errors
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexReference\"]", 3);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//objsur[@guid=\"" + lexRefDeleteGuid1 + "\"]", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//objsur[@guid=\"" + lexRefDeleteGuid2 + "\"]", 1);

			// Check that they were fixed
			// Should have deleted both LexReference objects
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//objsur[@guid=\"" + lexRefDeleteGuid1 + "\"]", 0);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//objsur[@guid=\"" + lexRefDeleteGuid2 + "\"]", 0);

			// Should still have the LexRefType object
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexRefType\" and @guid=\"" + lexRefTypeChangedGuid + "\"]", 1);
			// And that it has only one objsur now
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//objsur[@guid=\"" + lexRefNotDeleted + "\"]", 1);
			// Check that the valid LexReference is not deleted
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexReference\"]", 1);
		}

		/// <summary>
		/// This test checks that when a WfiMorphBundle has a dangling MSA pointer
		///		- if it has a sense that has an MSA, the MSA is fixed to point to it.
		///		- if it has a morph that points to a valid entry that has only one MSA, the MSA is fixed to point to it.
		/// Also, if it has a dangling Morph pointer,
		///		- if it has a sense that belongs to an entry with only a LexemeForm and no allomorphs,
		///			the Morph is fixed to point to that Lexeme form.
		/// If a good fix cannot be made the objsur should be removed.
		/// Also verifies that we correctly fix an MSA pointer that is not originally dangling, but will become so
		/// because the MSA will be deleted for lack of any referring senses.
		/// </summary>
		[Test]
		public void DanglingWordformLinks()
		{
			var testPath = Path.Combine(_basePath, "MorphBundleProblems");
			var data = new FwDataFixer(Path.Combine(testPath, "BasicFixup.fwdata"), new DummyProgressDlg(), LogErrors, ErrorCount);
			data.FixErrorsAndSave();
			var danglingMsaGuid = "aaaaaaaa-e15a-448e-a618-3855f93bd3c2"; // nonexistent 'msa'
			var repairableBundleGuid = "10f3db1e-33db-4d9d-9a06-e0a8e1ed8a92";
			var unrepairableBundleGuid = "d70b57bc-ecfe-4e32-a590-f2852bce69fc";
			var repairOnlyMsaBundleGuid = "e4396e8e-b7d2-43ba-93bd-104cf2011aaf";
			var repairForWillDeleteMsa = "cb941dc9-6f6e-44b2-97f2-07854e164b4e";
			var willDeleteMsaGuid = "63d132a9-4a41-43bb-a395-a6ad7f3ae7e2";
			var danglingMorphGoodSenseGuid = "9665bf3b-2aab-4f7f-88a9-4ca738b75110";
			var danglingMorphNoRepairGuid = "5752ed24-40e1-4282-9ba0-d82c89592432";
			var danglingMorphNoRepairAfGuid = "1f568cae-b0f8-413d-84a6-41cbd90923e9";

			// Verify initial state.
			// We start out with morph bundles that have broken links.
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"WfiMorphBundle\" and @guid=\"" + repairableBundleGuid + "\"]/Msa/objsur[@guid=\"" + danglingMsaGuid + "\"]", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"WfiMorphBundle\" and @guid=\"" + unrepairableBundleGuid + "\"]/Msa/objsur[@guid=\"" + danglingMsaGuid + "\"]", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"WfiMorphBundle\" and @guid=\"" + repairOnlyMsaBundleGuid + "\"]/Msa/objsur[@guid=\"" + danglingMsaGuid + "\"]", 1);
			// This one is not obviously broken, but the grammatical sense fixer will kill its target.
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"WfiMorphBundle\" and @guid=\"" + repairForWillDeleteMsa + "\"]/Msa/objsur[@guid=\"" + willDeleteMsaGuid + "\"]", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"MoStemMsa\" and @guid=\"" + willDeleteMsaGuid + "\"]", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"WfiMorphBundle\" and @guid=\"" + danglingMorphGoodSenseGuid + "\"]/Morph/objsur[@guid=\"" + danglingMsaGuid + "\"]", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"WfiMorphBundle\" and @guid=\"" + danglingMorphNoRepairGuid + "\"]/Morph/objsur[@guid=\"" + danglingMsaGuid + "\"]", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"WfiMorphBundle\" and @guid=\"" + danglingMorphNoRepairAfGuid + "\"]/Morph/objsur[@guid=\"" + danglingMsaGuid + "\"]", 1);

			// Check errors
			Assert.AreEqual(10, _errors.Count, "Unexpected number of errors found.");
			Assert.True(_errors[0].StartsWith("Removing dangling link to '" + danglingMsaGuid + "' (class='LexEntry'"),
				"Error message is incorrect."); // OriginalFixer--ksRemovingLinkToNonexistingObject
			Assert.That(_errors[1], Is.EqualTo("Fixing link to MSA based on Sense MSA (class='WfiMorphBundle', guid='" + repairableBundleGuid + "')."),
				"Error message is incorrect."); // MorphBundleFixer--ksRepairingMorphBundleFromSense
			Assert.That(_errors[2], Is.EqualTo("Removing dangling link to MSA '" + danglingMsaGuid + "' for WfiMorphBundle '" + unrepairableBundleGuid + "'."),
				"Error message is incorrect."); // MorphBundleFixer--ksRemovingDanglingMsa
			Assert.That(_errors[3], Is.EqualTo("Fixing link to MSA based on only MSA of entry for WfiMorphBundle '" + repairOnlyMsaBundleGuid + "'."),
				"Error message is incorrect."); // MorphBundleFixer--ksRepairingMorphBundleFromEntry
			// 4 is removing the unused MSA reference. Not interesting here.
			// 5 is removing the unused rt MSA element. Not interesting here.
			Assert.That(_errors[6], Is.EqualTo("Fixing link to MSA based on only MSA of entry for WfiMorphBundle '" + repairForWillDeleteMsa + "'."),
				"Error message is incorrect."); // MorphBundleFixer--ksRepairingMorphBundleFromEntry
			Assert.That(_errors[7], Is.EqualTo("Fixing link to Form based on only Form of entry for WfiMorphBundle '" + danglingMorphGoodSenseGuid + "'."),
				"Error message is incorrect."); // MorphBundleFixer--ksRemovingDanglingMsa
			Assert.That(_errors[8], Is.EqualTo("Removing dangling link to Form '" + danglingMsaGuid + "' for WfiMorphBundle '" + danglingMorphNoRepairGuid + "'."),
				"Error message is incorrect."); // MorphBundleFixer--ksRemovingDanglingMorph
			Assert.That(_errors[9], Is.EqualTo("Removing dangling link to Form '" + danglingMsaGuid + "' for WfiMorphBundle '" + danglingMorphNoRepairAfGuid + "'."),
				"Error message is incorrect."); // MorphBundleFixer--ksRemovingDanglingMorph

			// Check file repair
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"WfiMorphBundle\" and @guid=\"" + repairableBundleGuid + "\"]/Msa/objsur[@guid=\"408ba7ca-e15a-448e-a618-3855f93bd3c2\"]", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"WfiMorphBundle\" and @guid=\"" + unrepairableBundleGuid + "\"]/Msa", 0); // must remove Msa, not just child objsur
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"WfiMorphBundle\" and @guid=\"" + repairOnlyMsaBundleGuid + "\"]/Msa/objsur[@guid=\"408ba7ca-e15a-448e-a618-3855f93bd3c2\"]", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"WfiMorphBundle\" and @guid=\"" + repairForWillDeleteMsa + "\"]/Msa/objsur[@guid=\"26ae3989-d424-4dd2-a32d-c556c6985a99\"]", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"WfiMorphBundle\" and @guid=\"" + danglingMorphGoodSenseGuid + "\"]/Morph/objsur[@guid=\"8056c7d9-70ea-41b9-89e9-de28f7d686a7\"]", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"WfiMorphBundle\" and @guid=\"" + danglingMorphNoRepairGuid + "\"]/Morph", 0);  // must remove Morph, not just child objsur
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"WfiMorphBundle\" and @guid=\"" + danglingMorphNoRepairAfGuid + "\"]/Morph/objsur", 0);
		}

		/// <summary>
		/// This test deals with a special case (LT-14493) where an MSA is deleted that is referenced by both a sense and
		/// a bundle (that also references the sense). In particular we should not restore the broken link based on what the Sense
		/// used to have as its msa before it was fixed.
		/// </summary>
		[Test]
		public void DanglingSenseAndBundleLink()
		{
			var testPath = Path.Combine(_basePath, "DeletedMsaRefBySenseAndBundle");
			var data = new FwDataFixer(Path.Combine(testPath, "BasicFixup.fwdata"), new DummyProgressDlg(), LogErrors, ErrorCount);
			data.FixErrorsAndSave();
			var danglingMsaGuid = "aaaaaaaa-e15a-448e-a618-3855f93bd3c2"; // nonexistent 'msa'
			var bundleGuid = "10f3db1e-33db-4d9d-9a06-e0a8e1ed8a92";
			var senseGuid = "3110cf83-ad6c-47fe-91f8-8bf789473792";

			// Verify initial state.
			// We start out with a morph bundles and a sense that have broken links to the same missing MSA.
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"WfiMorphBundle\" and @guid=\"" + bundleGuid + "\"]/Msa/objsur[@guid=\"" + danglingMsaGuid + "\"]", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexSense\" and @guid=\"" + senseGuid + "\"]/MorphoSyntaxAnalysis/objsur[@guid=\"" + danglingMsaGuid + "\"]", 1);

			// Check errors
			Assert.AreEqual(2, _errors.Count, "Unexpected number of errors found.");
			Assert.True(_errors[0].StartsWith("Removing dangling link to '" + danglingMsaGuid + "' (class='LexSense'"),
				"Error message is incorrect.");
			Assert.That(_errors[1], Is.EqualTo("Removing dangling link to MSA '" + danglingMsaGuid + "' for WfiMorphBundle '" + bundleGuid + "'."),
				"Error message is incorrect."); // MorphBundleFixer--ksRemovingDanglingMsa

			// Check file repair
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"WfiMorphBundle\" and @guid=\"" + bundleGuid + "\"]/Msa", 0);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexSense\" and @guid=\"" + senseGuid + "\"]/MorphoSyntaxAnalysis", 0);		}

		[Test]
		public void DanglingCustomListReferences()
		{
			var testPath = Path.Combine(_basePath, "DanglingCustomListReference");
			// This test checks that dangling reference guids left by an edit/delete conflict
			// when a custom list item is deleted are identified and removed
			// and that an error message is produced for them.
			const string custItem1Guid = "ee90ab2a-cf14-47e4-b49d-83d967447b65"; // the dangler
			const string custItem2Guid = "0f2d36b5-504b-462a-9004-4ded018a89d1";
			var data = new FwDataFixer(Path.Combine(testPath, "BasicFixup.fwdata"), new DummyProgressDlg(), LogErrors, ErrorCount);
			data.FixErrorsAndSave();
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexEntry\"]", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//objsur[@guid=\"" + custItem1Guid + "\"]", 0);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//objsur[@guid=\"" + custItem2Guid + "\"]", 1);
			// The containing <Custom> element as well as the objsur should be removed
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@guid='c5e6aab3-4236-43b4-998e-d41ff26eba7b']/Custom", 0);
			Assert.AreEqual(1, _errors.Count, "Unexpected number of errors found.");
			Assert.True(_errors[0].StartsWith("Removing dangling link to '" + custItem1Guid + "' (class='LexEntry'"),
				"Error message is incorrect."); // OriginalFixer--ksRemovingLinkToNonexistingObject

			// Check original errors
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//objsur[@guid=\"" + custItem1Guid + "\"]", 1);
		}

		[Test]
		public void DanglingCustomProperty()
		{
			var testPath = Path.Combine(_basePath, "DanglingCustomProperty");
			// This test checks that dangling custom properties left by an edit/delete conflict
			// when a custom list is deleted are identified and removed
			// and that an error message is produced for them.
			const string danglingPropertyName = "Nonexistent";
			const string custItem1Guid = "ee90ab2a-cf14-47e4-b49d-83d967447b65"; // guid referenced inside the dangler
			const string custItem2Guid = "0f2d36b5-504b-462a-9004-4ded018a89d1";
			const string entryGuid = "efc0f898-7a52-4fe4-b827-f87a348e1b4b"; // dangling property should be removed from this rt element
			var data = new FwDataFixer(Path.Combine(testPath, "BasicFixup.fwdata"), new DummyProgressDlg(), LogErrors, ErrorCount);
			data.FixErrorsAndSave();
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexEntry\"]", 2);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexEntry\"]/Custom[@name=\"" + danglingPropertyName + "\"]", 0);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//objsur[@guid=\"" + custItem2Guid + "\"]", 1);
			Assert.AreEqual(2, _errors.Count, "Unexpected number of errors found.");
			Assert.True(_errors[0].StartsWith("Removing dangling link to '" + custItem1Guid + "' (class='LexEntry'"),
				"Error message is incorrect."); // OriginalFixer--ksRemovingLinkToNonexistingObject
			Assert.That(_errors[1], Is.StringStarting("Removing undefined custom property '" + danglingPropertyName +
				"' from class='LexEntry', guid='" + entryGuid + "'."),
				"Error message is incorrect."); // CustomPropertyFixer--ksRemovingUndefinedCustomProperty
			// Note that we don't currently get an error about deleting the FIRST dangling custom property,
			// because it is also a dangling reference, and hence gets deleted before the custom prop code
			// sees it. They could equally be checked in the opposite order, in which case we would get
			// two dangling property and no dangling ref messages.

			// Check original errors
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//objsur[@guid=\"" + custItem1Guid + "\"]", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexEntry\"]/Custom[@name=\"" + danglingPropertyName + "\"]", 2);
		}

		[Test]
		public void DuplicateWritingSystems()
		{
			var testPath = Path.Combine(_basePath, "DuplicateWs");
			// Looks for duplicate AStr elements with the same writing system (english) and makes sure the Fixer fixes 'em up.
			const string testGuid = "00041516-72d1-4e56-9ed8-fe235a9b1a68";
			var data = new FwDataFixer(Path.Combine(testPath, "BasicFixup.fwdata"), new DummyProgressDlg(), LogErrors, ErrorCount);
			data.FixErrorsAndSave();
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"CmSemanticDomain\" and @guid=\"" + testGuid + "\"]//Description/AStr[@ws=\"en\"]", 1);
			Assert.AreEqual(1, _errors.Count, "Incorrect number of errors.");
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"CmSemanticDomain\" and @guid=\"" + testGuid + "\"]//Description/AStr[@ws=\"en\"]", 2);
		}

		/// <summary>
		/// This test checks that sequences that should not be empty are identified and their parents removed
		/// and that an error message is produced for them.
		/// </summary>
		[Test]
		public void ForEmptySequences()
		{
			// Setup
			var testPath = Path.Combine(_basePath, "SequenceFixer");
			// This rt element is a clause marker that has no dependent clauses
			// and so should be deleted from its chart.
			const string clauseMarkerGuid = "c4e487c6-bbbe-4b8f-8137-7d5fa7d2dc09";
			// This rt element will have no component cells after the above clause marker is deleted
			// and so it also should be deleted from its chart.
			const string chartRowGuid = "6d9fe079-df9c-40c6-9cec-8e1dc1bbda92";
			// This is the row's chart (owner).
			const string chartGuid = "8fa53cdf-9950-4a23-ba1c-844723c2342d";
			// This rt element holds a sequence of phonetic contexts that is empty
			// and so should be deleted from its rule.
			const string sequenceContextGuid = "09acafc4-33fd-4c12-a96d-af0d87c343d0";
			// This is the sequence context's owner.
			const string segmentRuleRhsGuid = "bd72b1c5-3067-433d-980d-5aae9271556d";
			Assert.DoesNotThrow(() =>
									{
										var data = new FwDataFixer(Path.Combine(testPath, "BasicFixup.fwdata"), new DummyProgressDlg(),
																   LogErrors, ErrorCount);

										// SUT
										data.FixErrorsAndSave();
									}, "Exception running the data fixer on the sequence test data.");

			// Verification
			// check that the clause marker was there originally
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"ConstChartClauseMarker\" and @guid=\"" + clauseMarkerGuid + "\"]", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//objsur[@guid=\"" + chartRowGuid + "\"]", 1);

			// check that the clause marker has been deleted
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"ConstChartClauseMarker\" and @guid=\"" + clauseMarkerGuid + "\"]", 0);

			// check that the row is no longer in the chart
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//objsur[@guid=\"" + chartRowGuid + "\"]", 0);

			// check that the row has been deleted
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"ConstChartRow\" and @guid=\"" + chartRowGuid + "\"]", 0);

			// check that the phone rule sequence context was there originally
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"PhSequenceContext\" and @guid=\"" + sequenceContextGuid + "\"]", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//objsur[@guid=\"" + segmentRuleRhsGuid + "\"]", 1);

			// check that the phone rule sequence context has been deleted
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"PhSequenceContext\" and @guid=\"" + sequenceContextGuid + "\"]", 0);

			Assert.AreEqual(3, _errors.Count, "Unexpected number of errors found.");
			Assert.AreEqual("Removing owner of empty sequence (guid='" + chartRowGuid +
				"' class='ConstChartRow') from its owner (guid='" + chartGuid + "').", _errors[0],
				"Error message is incorrect.");//SequenceFixer--ksRemovingOwnerOfEmptySequence
			Assert.AreEqual("Removing owner of empty sequence (guid='" + clauseMarkerGuid +
				"' class='ConstChartClauseMarker') from its owner (guid='" + chartRowGuid + "').", _errors[1],
				"Error message is incorrect.");//SequenceFixer--ksRemovingOwnerOfEmptySequence
			Assert.AreEqual("Removing owner of empty sequence (guid='" + sequenceContextGuid +
				"' class='PhSequenceContext') from its owner (guid='" + segmentRuleRhsGuid + "').", _errors[2],
				"Error message is incorrect.");//SequenceFixer--ksRemovingOwnerOfEmptySequence
		}

		[Test]
		public void EntryWithExtraMSA()
		{
			var testPath = Path.Combine(_basePath, "EntryWithExtraMSA");
			_errors.Clear();
			Assert.DoesNotThrow(() =>
			{
				var data = new FwDataFixer(Path.Combine(testPath, "BasicFixup.fwdata"), new DummyProgressDlg(),
										   LogErrors, ErrorCount);

				// SUT
				data.FixErrorsAndSave();
			}, "Exception running the data fixer on the entry with extra MSA test data.");

			Assert.That(_errors.Count, Is.GreaterThan(0), "fixing anything should log an error");

			// check that the clause marker was there originally
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexEntry\"]/MorphoSyntaxAnalyses/objsur", 4);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"MoStemMsa\"]", 5);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"FsFeatStruc\"]", 2);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"FsComplexValue\"]", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"FsClosedValue\"]", 1);

			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexEntry\"]/MorphoSyntaxAnalyses/objsur", 2);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"MoStemMsa\"]", 3);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"FsFeatStruc\"]", 0);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"FsComplexValue\"]", 0);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"FsClosedValue\"]", 0);
		}

		[Test]
		public void EntryWithMsaAndNoSenses()
		{
			var testPath = Path.Combine(_basePath, "EntryWithMsaAndNoSenses");
			_errors.Clear();
			Assert.DoesNotThrow(() =>
			{
				var data = new FwDataFixer(Path.Combine(testPath, "BasicFixup.fwdata"), new DummyProgressDlg(),
										   LogErrors, ErrorCount);

				// SUT
				data.FixErrorsAndSave();
			}, "Exception running the data fixer on the entry with MSA and no senses test data.");
			Assert.That(_errors.Count, Is.GreaterThan(0), "fixing anything should log an error");

			// check that the msa was there originally
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexEntry\"]/MorphoSyntaxAnalyses/objsur", 1);
			// And that it was deleted.
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexEntry\"]/MorphoSyntaxAnalyses/objsur", 0);
		}

		[Test]
		public void EntryWithOneExtraMsaAndOneSenseWithABustedRef()
		{
			var testPath = Path.Combine(_basePath, "EntryExtraMsaAndBustedSenseRef");
			_errors.Clear();
			Assert.DoesNotThrow(() =>
			{
				var data = new FwDataFixer(Path.Combine(testPath, "BasicFixup.fwdata"), new DummyProgressDlg(),
										   LogErrors, ErrorCount);

				// SUT
				data.FixErrorsAndSave();
			}, "Exception running the data fixer on the entry with MSA and no senses test data.");
			Assert.That(_errors.Count, Is.GreaterThan(0), "fixing anything should log an error");

			// check that the msa was there originally
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.bak")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexEntry\"]/MorphoSyntaxAnalyses/objsur", 2);
			// And that it was deleted.
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexEntry\"]/MorphoSyntaxAnalyses/objsur", 1);
			AssertThatXmlIn.File(Path.Combine(testPath, "BasicFixup.fwdata")).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexSense\"]/MorphoSyntaxAnalysis/objsur", 1);
		}

		[Test]
		public void DanglingTextTagAndChartReferences()
		{
			var testPath = Path.Combine(_basePath, "TagAndCellRefs");
			// This test checks that dangling reference guids are identified and removed
			// and that an error message is produced for them.
			// It also checks that TextTags and ChartCells with missing references have been cleaned up.
			const string segmentGuid = "0157b3fd-b464-4983-a865-3eb9dbc7fa72"; // this Segment was deleted by the merge.
			// This ConstChartWordGroup references the Segment that went away.
			// Both BeginSegment and EndSegment are null (after Dangling Reference repair).
			// Delete the word group.
			const string chartCellGuid = "f864b36d-ecf0-4c22-9fac-ff91b009a8f8";
			// This TextTag references the Segment that went away.
			// Its BeginSegment is still okay, but its EndSegment is bad. Dangling Reference repair will
			// delete the reference. Repair the tag.
			// At this point, the UI can't make a tag that references more than one Segment, but it may someday.
			const string textTagGuid = "fa0c3376-1dbc-42c0-b4ff-cd6bf0372b13";
			const string chartRowGuid = "d2e52268-71bc-427e-a666-dbe66751b132";
			const string chartGuid = "8fa53cdf-9950-4a23-ba1c-844723c2342d";

			var dataFilePath = Path.Combine(testPath, "BasicFixup.fwdata");
			var originalFwData = File.ReadAllText(dataFilePath);

			var data = new FwDataFixer(dataFilePath, new DummyProgressDlg(), LogErrors, ErrorCount);
			data.FixErrorsAndSave();

			var backupFilePath = Path.Combine(testPath, "BasicFixup.bak");
			var backupFwData = File.ReadAllText(backupFilePath);
			var fixedFwData = File.ReadAllText(dataFilePath);

			Assert.AreEqual(originalFwData, backupFwData, "backup file should preserve the original (bad) file content");
			Assert.AreNotEqual(originalFwData, fixedFwData, "fixing data should have changed something!");

			// Check initial state of the test file
			AssertThatXmlIn.String(originalFwData).HasSpecifiedNumberOfMatchesForXpath("//rt[@class=\"TextTag\"]", 1);
			AssertThatXmlIn.String(originalFwData).HasSpecifiedNumberOfMatchesForXpath("//rt[@class=\"ConstChartWordGroup\"]", 3);
			AssertThatXmlIn.String(originalFwData).HasSpecifiedNumberOfMatchesForXpath("//objsur[@guid=\"" + segmentGuid + "\"]", 3);
			AssertThatXmlIn.String(originalFwData).HasSpecifiedNumberOfMatchesForXpath("//rt[@class=\"ConstChartRow\" and @guid=\"" + chartRowGuid + "\"]", 1);
			// Check the repaired state of the test file
			AssertThatXmlIn.String(fixedFwData).HasSpecifiedNumberOfMatchesForXpath("//rt[@class=\"ConstChartRow\" and @guid=\"" + chartRowGuid + "\"]", 0);
			AssertThatXmlIn.String(fixedFwData).HasSpecifiedNumberOfMatchesForXpath("//rt[@class=\"TextTag\"]", 1);
			AssertThatXmlIn.String(fixedFwData).HasSpecifiedNumberOfMatchesForXpath("//rt[@class=\"ConstChartWordGroup\"]", 2);
			AssertThatXmlIn.String(fixedFwData).HasSpecifiedNumberOfMatchesForXpath("//objsur[@guid=\"" + segmentGuid + "\"]", 0); // got rid of all the refs to the bad segment.
			// The parent (property) elements of the deleted objsur elements should be gone, too.
			AssertThatXmlIn.String(fixedFwData).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@guid='f864b36d-ecf0-4c22-9fac-ff91b009a8f8']/BeginSegment", 0);
			AssertThatXmlIn.String(fixedFwData).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@guid='f864b36d-ecf0-4c22-9fac-ff91b009a8f8']/EndSegment", 0);
			// Note that the other dangling EndSegment does not result in the property being deleted; it is
			// repaired instead.

			// check that the row has been deleted
			AssertThatXmlIn.String(fixedFwData).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"ConstChartRow\" and @guid=\"" + chartRowGuid + "\"]", 0);
			Assert.AreEqual(7, _errors.Count, "Unexpected number of errors found.");
			Assert.True(_errors[0].StartsWith("Removing dangling link to '" + segmentGuid + "' (class='ConstChartWordGroup'"),
				"Error message is incorrect."); // OriginalFixer--ksRemovingLinkToNonexistingObject
			Assert.True(_errors[1].StartsWith("Removing dangling link to '" + segmentGuid + "' (class='ConstChartWordGroup'"),
				"Error message is incorrect."); // OriginalFixer--ksRemovingLinkToNonexistingObject
			Assert.True(_errors[2].StartsWith("Removing reference to missing Segment by deleting analysis object guid='" +
				chartCellGuid + "', class='ConstChartWordGroup'"),
				"Error message is incorrect."); // SequenceFixer--ksRemovingBadAnalysisRefObj
			Assert.True(_errors[3].StartsWith("Removing dangling link to '" + segmentGuid + "' (class='TextTag'"),
				"Error message is incorrect."); // OriginalFixer--ksRemovingLinkToNonexistingObject
			Assert.True(_errors[4].EndsWith("changing analysis object guid='" + textTagGuid +
				"', class='TextTag', field='EndSegment'."),
				"Error message is incorrect."); // SequenceFixer--ksAdjustingAnalysisRefObj
			Assert.AreEqual("Removing owner of empty sequence (guid='" + chartRowGuid +
				"' class='ConstChartRow') from its owner (guid='" + chartGuid + "').", _errors[5],
				"Error message is incorrect.");//SequenceFixer--ksRemovingOwnerOfEmptySequence
			Assert.True(_errors[6].StartsWith("Removing dangling link to '"), "second pass removed a dangling link created by first pass");
		}

		[Test]
		public void GenericDateFixup()
		{
			var fileLoc = Path.Combine(Path.Combine(_basePath, "GenericDates"), "BasicFixup.fwdata");
			var data = new FwDataFixer(fileLoc, new DummyProgressDlg(), LogErrors, ErrorCount);
			_errors.Clear();
			data.FixErrorsAndSave();
			Assert.That(_errors.Count, Is.GreaterThan(0), "fixing anything should log an error");

			AssertThatXmlIn.File(fileLoc).HasSpecifiedNumberOfMatchesForXpath("//rt[@class='RnGenericRec']/DateOfEvent", 3);
			AssertThatXmlIn.File(fileLoc).HasAtLeastOneMatchForXpath("//rt[@class='RnGenericRec']/DateOfEvent[@val='0']");
		}

		/// <summary>
		/// LT-13509 Identical entries homograph numbering inconsistency.
		/// </summary>
		[Test]
		public void HomographNumberInconsistency()
		{
			// Setup
			var testPath = Path.Combine(_basePath, "HomographFixer");
			// LexEntries needing homograph number set to 1 or 2
			const string lexEntry_dinding1Guid = "a39f2112-b82c-46ba-9f69-6b46e45efff4";
			const string lexEntry_dinding2Guid = "b35e8d52-e74d-47b4-b300-82e8c45cdfb7";
			const string emptyEntry1Guid = "2B5C8394-A9BD-4873-8D7D-25C97DF72ACD";
			const string emptyEntry2Guid = "49E8B5A0-3D68-4544-BBB6-195EEC5CA367";
			const string emptyEntry3Guid = "1ABB7D7B-86C1-4811-B5AD-6A776AD1BADF";
			const string emptyEntry4Guid = "984DBC2D-B482-4A91-B250-DC0858DF14FD";
			const string homoSomethingGuid = "972EAFEA-49D6-40D7-A848-6E658C1A2A79"; // homo in de, something in fr
			const string homoElseGuid = "9BE4E7C0-BDF2-40A5-98E3-70E42D955C49"; // homo in de, else in fr
			const string irrelevantElseGuid = "A42984AA-EEA0-4204-AC81-9B9B115E9785"; // irrelevant in de, else in fr
			const string citationDiffGuid1 = "21122112-bad1-46ba-9f69-6b46e45efff4";
			const string citationDiffGuid2 = "21122112-bad2-46ba-9f69-6b46e45efff4";
			const string citationSameGuid1 = "33333333-bad1-46ba-9f69-6b46e45efff4";
			const string citationSameGuid2 = "33333333-bad2-46ba-9f69-6b46e45efff4";
			const string sameSecondaryOrder1 = "ABABABAA-EEA0-4204-AC81-9B9B115E9785";
			const string sameSecondaryOrder2 = "EBEAEBEA-EEA0-4204-AC81-9B9B115E9785";
			const string diffSecondaryOrder = "FABFABFA-EEA0-4204-AC81-9B9B115E9785";

			// Verification of input.
			var testFile = Path.Combine(testPath, "BasicFixup.fwdata");
			// It's important that one of the entries needing a non-zero number does not have one at all to begin with.
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexEntry\" and @guid=\"" + lexEntry_dinding1Guid + "\" and not(HomographNumber)]", 1);
			// It's also important that one of them has allmorphs
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class=\"LexEntry\" and @guid=\"" + lexEntry_dinding2Guid + "\" and AlternateForms/objsur/@guid='bb464c5d-de15-494b-bc05-1ee92c3028e6']", 1);

			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='LexEntry' and @guid='" + lexEntry_dinding1Guid + "']", 1);

			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='LexEntry' and @guid='" + "08fc938e-110e-44f4-8660-165d26030124" + "']", 1);

			// Group of four entries that have empty lexeme form. We want them to end up with no homograph numbers.
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='LexEntry' and @guid='" + emptyEntry1Guid + "']/HomographNumber[@val='1']", 1);
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='LexEntry' and @guid='" + emptyEntry2Guid + "']/HomographNumber[@val='2']", 1);
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='LexEntry' and @guid='" + emptyEntry3Guid + "']/HomographNumber[@val='3']", 1);
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='LexEntry' and @guid='" + emptyEntry4Guid + "']", 1);
			// The third has no LexemeForm field at all.
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='LexEntry' and @guid='" + emptyEntry3Guid + "']/LexemeForm", 0);
			// The fourth has no homograph number field at all.
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='LexEntry' and @guid='" + emptyEntry4Guid + "']/HomographNumber", 0);

			// Two LexEntries have different citation forms but are otherwise identical starting with 0's should end with 0's
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='LexEntry' and @guid='" + citationDiffGuid1 + "']/HomographNumber[@val='0']", 1);
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='LexEntry' and @guid='" + citationDiffGuid2 + "']/HomographNumber[@val='0']", 1);
			// Two LexEntries with the same citation forms but otherwise different starting with 0's should end with '1', '2'
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='LexEntry' and @guid='" + citationSameGuid1 + "']/HomographNumber[@val='0']", 1);
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='LexEntry' and @guid='" + citationSameGuid2 + "']/HomographNumber[@val='0']", 1);
			// Two LexEntries have different SecondaryOrder in the MoMorphType but are otherwise identical
			// starting with 0's should end with a 0 and a the different should get a 1
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='LexEntry' and @guid='" + sameSecondaryOrder1 + "']/HomographNumber[@val='0']", 1);
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='LexEntry' and @guid='" + sameSecondaryOrder2 + "']/HomographNumber[@val='0']", 1);
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='LexEntry' and @guid='" + diffSecondaryOrder + "']/HomographNumber[@val='0']", 1);

			// stems for multilingual ones:
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='MoStemAllomorph' and @guid='" + "F417EEF7-8B30-4ED5-BF5A-BBD3A3FFB4C2" + "']/Form/AUni[@ws='de' and text()='irrelevant']", 1);
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='MoStemAllomorph' and @guid='" + "F417EEF7-8B30-4ED5-BF5A-BBD3A3FFB4C2" + "']/Form/AUni[@ws='fr' and text()='else']", 1);
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='MoStemAllomorph' and @guid='" + "AF2154B3-493E-47A7-B58F-285E2C39A16A" + "']/Form/AUni[@ws='de' and text()='homo']", 1);
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='MoStemAllomorph' and @guid='" + "AF2154B3-493E-47A7-B58F-285E2C39A16A" + "']/Form/AUni[@ws='fr' and text()='else']", 1);
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='MoStemAllomorph' and @guid='" + "0F4FB8BF-AA48-4315-A244-B3B367DD0159" + "']/Form/AUni[@ws='de' and text()='homo']", 1);
			AssertThatXmlIn.File(testFile).HasSpecifiedNumberOfMatchesForXpath(
				"//rt[@class='MoStemAllomorph' and @guid='" + "0F4FB8BF-AA48-4315-A244-B3B367DD0159" + "']/Form/AUni[@ws='fr' and text()='something']", 1);

			_errors.Clear();
			Assert.DoesNotThrow(() =>
			{
				var data = new FwDataFixer(Path.Combine(testPath, "BasicFixup.fwdata"), new DummyProgressDlg(),
										   LogErrors, ErrorCount);

				// SUT
				data.FixErrorsAndSave();
			}, "Exception running the data fixer on the sequence test data.");

			Assert.That(_errors.Count, Is.GreaterThan(0), "fixing anything should log an error");

			var xmlDoc = GetResult(testFile);
			var entries = VerifyEntryExists(xmlDoc, "//rt[@class='LexEntry' and @guid='" + lexEntry_dinding1Guid + "']");
			XmlNode entry = entries[0];
			var homographEl = entry.SelectSingleNode("HomographNumber");
			Assert.IsNotNull(homographEl);
			var homographAttribute = homographEl.Attributes[0];
			Assert.IsTrue(homographAttribute.Name.ToString().Equals("val"));
			var homographVal1 = homographAttribute.Value;

			entries = VerifyEntryExists(xmlDoc, "//rt[@class='LexEntry' and @guid='" + lexEntry_dinding2Guid + "']");
			entry = entries[0];
			homographEl = entry.SelectSingleNode("HomographNumber");
			Assert.IsNotNull(homographEl);
			homographAttribute = homographEl.Attributes[0];
			Assert.IsTrue(homographAttribute.Name.ToString().Equals("val"));

			var homographVal2 = homographAttribute.Value;

			Assert.That((homographVal1 == "1" && homographVal2 == "2") || (homographVal1 == "2" && homographVal2 == "1"),"The homograph numbers were both zero for these LexEntries and should now be 1 and 2");

			// Non-homograph should have HN corrected to zero.
			VerifyHn(xmlDoc, "08fc938e-110e-44f4-8660-165d26030124", "0");
			// Set of messed up homographs should be fixed, and as far as possible, existing HNs should be preserved.
			// Technically, other outcomes are valid besides the one indicated here: the other entry that was previously 2 could keep that number,
			// and either the old zero or the other old 2 could be assigned 3 (and the other 4). But this is what currently happens.
			// Any changed algorithm should minimally (a) not change HN1; (b) keep HN2 for at least one of the entries that initially has it;
			// (c) produce HNs 1,2,3, and 4.
			VerifyHn(xmlDoc, "bb42e0a5-2131-4e2b-9c96-19a11c5a5081", "2"); // homo that was previously 2 should still be
			VerifyHn(xmlDoc, "bb4042c7-47b2-422f-ae66-a09293d16ed8", "1"); // homo that was previously 1 should still be
			VerifyHn(xmlDoc, "bb3d3349-5cea-4920-a2bc-672d5d927875", "3"); // homo that was previously 0 should take next value
			VerifyHn(xmlDoc, "bb3be3bc-e4e6-4e72-9b1b-d81c0298b4e7", "4"); // homo that was previously the second 2 should be changed
			//Two entries with different citation forms previously 0, should still be.
			VerifyHn(xmlDoc, citationSameGuid1, "1"); // former 0 should be 1
			VerifyHn(xmlDoc, citationSameGuid2, "2"); // former 0 should be 2
			VerifyHn(xmlDoc, citationDiffGuid1, "0"); // homograph# should be unchanged
			VerifyHn(xmlDoc, citationDiffGuid2, "0"); // homograph# should be unchanged
			VerifyHn(xmlDoc, sameSecondaryOrder1, "1"); // former 0 should be 1
			VerifyHn(xmlDoc, diffSecondaryOrder, "0"); // homograph# should be unchanged
			VerifyHn(xmlDoc, sameSecondaryOrder2, "2"); // former 0 should be 2
			VerifyHn(xmlDoc, emptyEntry1Guid, "0"); // entry with empty LF should have no HN
			VerifyHn(xmlDoc, emptyEntry2Guid, "0"); // entry with empty LF should have no HN
			VerifyHn(xmlDoc, emptyEntry3Guid, "0"); // entry with no LF should have no HN
			VerifyHn(xmlDoc, emptyEntry4Guid, null); // entry with empty LF and no previous HN element should still have none
			VerifyHn(xmlDoc, homoSomethingGuid, "0"); // not a homograph in french, though it is in the first AUni ws
			VerifyHn(xmlDoc, homoElseGuid, "1"); // a homograph in french
			VerifyHn(xmlDoc, irrelevantElseGuid, "2"); // a homograph in french (though not in the first AUni ws)
		}

		private void VerifyHn(XmlDocument xmlDoc, string guid, string expectedHn)
		{
			XmlNodeList entries;
			XmlNode entry;
			XmlNode homographEl;
			XmlAttribute homographAttribute;
			entries = VerifyEntryExists(xmlDoc, "//rt[@class='LexEntry' and @guid='" + guid + "']");
			entry = entries[0];
			homographEl = entry.SelectSingleNode("HomographNumber");
			if (expectedHn == null)
			{
				Assert.That(homographEl, Is.Null);
				return;
			}
			Assert.IsNotNull(homographEl);
			homographAttribute = homographEl.Attributes[0];
			Assert.That(homographAttribute.Name, Is.EqualTo("val"));
			Assert.That(homographAttribute.Value, Is.EqualTo(expectedHn), "Homograph# is unexpectedly different.");
		}
	}
}
