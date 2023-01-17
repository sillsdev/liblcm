// Copyright (c) 2005-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.DomainServices;
using SIL.LCModel.Utils;
using SIL.PlatformUtilities;

namespace SIL.LCModel.DomainImpl
{
	/// -----------------------------------------------------------------------------------------
	/// <summary>
	/// Tests the CmPicture class
	/// </summary>
	/// -----------------------------------------------------------------------------------------
	[TestFixture]
	public class CmPictureTests: MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{
		#region Data members
		private MockFileOS m_fileOs;
		private ICmPictureFactory m_pictureFactory;
		private ICmPicture m_pict;
		private string m_internalPath = Path.DirectorySeparatorChar + Path.GetRandomFileName();
		private CoreWritingSystemDefinition m_wsGerman;
		private CoreWritingSystemDefinition m_wsSpanish;
		#endregion

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Does setup for all the tests
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public override void FixtureSetup()
		{
			base.FixtureSetup();
			Cache.ServiceLocator.WritingSystemManager.GetOrSet("de", out m_wsGerman);
			Cache.ServiceLocator.WritingSystemManager.GetOrSet("es", out m_wsSpanish);
		}

		/// -------------------------------------------------------------------------------------
		/// <summary>
		/// Create a CmPicture from a dummy file.
		/// </summary>
		/// -------------------------------------------------------------------------------------
		protected override void CreateTestData()
		{
			base.CreateTestData();

			m_fileOs = new MockFileOS();
			FileUtils.Manager.SetFileAdapter(m_fileOs);

			m_fileOs.AddFile(m_internalPath, "123", Encoding.Default);

			m_pictureFactory = Cache.ServiceLocator.GetInstance<ICmPictureFactory>();
			m_pict = m_pictureFactory.Create(m_internalPath,
				TsStringUtils.MakeString("Test picture", Cache.DefaultVernWs),
				CmFolderTags.LocalPictures);

			Assert.IsNotNull(m_pict);
			Assert.AreEqual("Test picture", m_pict.Caption.VernacularDefaultWritingSystem.Text);
			Assert.AreEqual(m_internalPath, m_pict.PictureFileRA.InternalPath, "Internal path not set correctly");
			Assert.AreEqual(m_internalPath, m_pict.PictureFileRA.AbsoluteInternalPath, "Files outside LangProject.LinkedFilesRootDir are stored as absolute paths");
		}

		/// <summary/>
		public override void TestTearDown()
		{
			FileUtils.Manager.Reset();
			base.TestTearDown();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests whether undo really removes a CmPicture
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void UndoOfCreateCmPicture()
		{
			Guid pictGuid = m_pict.Guid;
			UndoAll();
			// Start a new task so the teardown will be happy.
			m_actionHandler.BeginUndoTask("Undo doing stuff", "Redo doing stuff");
			Assert.AreEqual(0, Cache.ServiceLocator.GetInstance<ICmPictureRepository>().Count);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the method FindOrCreateFile when the original file is no longer present
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void CmFileFinder_OrigFileMissing()
		{
			// Setup
			ICmFolder folder = DomainObjectServices.FindOrCreateFolder(Cache,
				LangProjectTags.kflidPictures, CmFolderTags.LocalPictures);
			FileUtils.Delete(m_internalPath);
			Assert.IsFalse(FileUtils.IsFileReadable(m_internalPath), "Test cannot proceed. Unable to delete Original file.");

			// Test
			Assert.DoesNotThrow(()=>DomainObjectServices.FindOrCreateFile(folder, m_internalPath));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the method FindOrCreateFile when CmFile already exists with identical original
		/// file
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void CmFileFinder_OrigFilesMatch()
		{
			// Setup
			ICmFolder folder = DomainObjectServices.FindOrCreateFolder(Cache,
				LangProjectTags.kflidPictures, CmFolderTags.LocalPictures);

			ICmFile file = DomainObjectServices.FindOrCreateFile(folder, m_internalPath);
			Assert.AreEqual(m_pict.PictureFileRA, file);
		}


		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the method FindOrCreateFile when there is no other CmFile with the same orig
		/// file name.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void CmFileFinder_NoPreExistingCmFile()
		{
			// Setup
			string sNewFile = Path.DirectorySeparatorChar + Path.GetRandomFileName();
			m_fileOs.AddFile(sNewFile, "456", Encoding.Default);

			ICmFolder folder = DomainObjectServices.FindOrCreateFolder(Cache,
				LangProjectTags.kflidPictures, CmFolderTags.LocalPictures);

			// Test
			ICmFile file = DomainObjectServices.FindOrCreateFile(folder, sNewFile);
			Assert.IsNotNull(file, "null CmFile returned");
			Assert.AreEqual(sNewFile, file.InternalPath, "Internal path not set correctly");
			Assert.AreEqual(sNewFile, file.AbsoluteInternalPath, "Files outside LangProject.LinkedFilesRootDir are stored as absolute paths");
			Assert.AreNotEqual(m_pict.PictureFileRA, file);
			FileUtils.Delete(sNewFile);
		}


		/// -------------------------------------------------------------------------------------
		/// <summary>
		/// Test ability to Create a new picture, given a text representation (e.g., from the
		/// clipboard).
		/// </summary>
		/// -------------------------------------------------------------------------------------
		[Test]
		public void CmPictureConstructor_FromTextRep()
		{
			ICmPicture pictNew = m_pictureFactory.Create(m_pict.TextRepresentation,
				CmFolderTags.LocalPictures);
			Assert.AreNotEqual(pictNew, m_pict);
			string internalPathNew = pictNew.PictureFileRA.InternalPath;
			Assert.AreEqual(m_internalPath, internalPathNew);
			Assert.AreEqual(internalPathNew, pictNew.PictureFileRA.AbsoluteInternalPath, "Files outside LangProject.LinkedFilesRootDir are stored as absolute paths");
			AssertEx.AreTsStringsEqual(m_pict.Caption.VernacularDefaultWritingSystem,
				pictNew.Caption.VernacularDefaultWritingSystem);
			Assert.AreEqual(m_pict.PictureFileRA.Owner, pictNew.PictureFileRA.Owner);
			Assert.IsNull(pictNew.Description.AnalysisDefaultWritingSystem.Text);
			// REVIEW (TE-7745): What should the default PictureLayoutPosition value be?
			Assert.AreEqual(PictureLayoutPosition.CenterInColumn, pictNew.LayoutPos);
			Assert.AreEqual(100, pictNew.ScaleFactor);
			Assert.AreEqual(PictureLocationRangeType.AfterAnchor, pictNew.LocationRangeType);
			Assert.AreEqual(0, pictNew.LocationMin);
			Assert.AreEqual(0, pictNew.LocationMax);
			Assert.IsNull(pictNew.PictureFileRA.Copyright.VernacularDefaultWritingSystem.Text);
		}

		/// -------------------------------------------------------------------------------------
		/// <summary>
		/// Test ICmPictureFactory.Create method when passing a collection of descriptions in
		/// multiple writing systems.
		/// </summary>
		/// -------------------------------------------------------------------------------------
		[Test]
		public void CmPictureConstructor_FullParamsMultipleDescriptionVariants()
		{
			Dictionary<int, string> descriptions = new Dictionary<int,string>();
			descriptions[Cache.DefaultAnalWs] = "My picture.";
			descriptions[Cache.DefaultVernWs] = "Mi foto.";
			ICmPicture pictNew = m_pictureFactory.Create(CmFolderTags.LocalPictures, 0, null, descriptions,
				m_internalPath, "left", "1-2", "Don't use this picture in your book!",
				m_pict.Caption.VernacularDefaultWritingSystem,
				PictureLocationRangeType.ParagraphRange, "62");
			Assert.AreNotEqual(pictNew, m_pict);
			string internalPathNew = pictNew.PictureFileRA.InternalPath;
			Assert.AreEqual(m_internalPath, internalPathNew);
			Assert.AreEqual(m_internalPath, pictNew.PictureFileRA.AbsoluteInternalPath, "Files outside LangProject.LinkedFilesRootDir are stored as absolute paths");
			AssertEx.AreTsStringsEqual(m_pict.Caption.VernacularDefaultWritingSystem,
				pictNew.Caption.VernacularDefaultWritingSystem);
			Assert.AreEqual(m_pict.PictureFileRA.Owner, pictNew.PictureFileRA.Owner);
			Assert.AreEqual("My picture.", pictNew.Description.AnalysisDefaultWritingSystem.Text);
			Assert.AreEqual("Mi foto.", pictNew.Description.VernacularDefaultWritingSystem.Text);
			Assert.AreEqual(PictureLayoutPosition.LeftAlignInColumn, pictNew.LayoutPos);
			Assert.AreEqual(62, pictNew.ScaleFactor);
			Assert.AreEqual(PictureLocationRangeType.ParagraphRange, pictNew.LocationRangeType);
			Assert.AreEqual(1, pictNew.LocationMin);
			Assert.AreEqual(2, pictNew.LocationMax);
			Assert.AreEqual("Don't use this picture in your book!",
				pictNew.PictureFileRA.Copyright.VernacularDefaultWritingSystem.Text);
		}

		/// -------------------------------------------------------------------------------------
		/// <summary>
		/// Test that the CmPicture contructor throws the correct exception when given a text
		/// representation (e.g., from the clipboard or import) that contains too few parameters.
		/// </summary>
		/// -------------------------------------------------------------------------------------
		[Test]
		public void CmPictureConstructor_FromTextRep_TooFewParams()
		{
			Assert.That(() => m_pictureFactory.Create("CmPicture||c:\\whatever.jpg||", CmFolderTags.LocalPictures),
				Throws.ArgumentException.With.Message.EqualTo("The clipboard format for a Picture was invalid"));
		}

		/// -------------------------------------------------------------------------------------
		/// <summary>
		/// Test that the CmPicture contructor throws the correct exception when given a text
		/// representation that does not begin with a "CmPicture" token.
		/// </summary>
		/// -------------------------------------------------------------------------------------
		[Test]
		public void CmPictureConstructor_FromTextRep_MissingCmPictureToken()
		{
			Assert.That(() => m_pictureFactory.Create("CmFile||c:\\whatever.jpg||||This is a caption||", CmFolderTags.LocalPictures),
				Throws.ArgumentException.With.Message.EqualTo("The clipboard format for a Picture was invalid"));
		}

		/// -------------------------------------------------------------------------------------
		/// <summary>
		/// Test that the CmPicture contructor throws the correct exception when given a text
		/// representation that has an empty filename token.
		/// </summary>
		/// -------------------------------------------------------------------------------------
		[Test]
		public void CmPictureConstructor_FromTextRep_MissingFilename()
		{
			Assert.That(() => m_pictureFactory.Create("CmPicture||||||This is a caption||",
				CmFolderTags.LocalPictures), Throws.ArgumentException.With.Property("Message").Matches("File path not specified.(\\r)?\\nParameter( )?name: srcFile"));
		}

		/// -------------------------------------------------------------------------------------
		/// <summary>
		/// Test that the CmPicture constructor throws the correct exception when given a text
		/// representation that has an invalid filename.
		/// </summary>
		/// -------------------------------------------------------------------------------------
		[Test]
		public void CmPictureConstructor_FromTextRep_InvalidFilename()
		{
			// Note that on Linux only NULL and slash are invalid characters in a file
			// name, and Path.GetInvalidPathChars() only even reports the NULL character.
			string sTextRepOfPicture;
			if (Platform.IsUnix)
				sTextRepOfPicture = "CmPicture||/wha<>\u0000tever.jpg||||This is a caption||";
			else
				sTextRepOfPicture = "CmPicture||c:\\wha<>tever.jpg||||This is a caption||";
			Assert.That(() => m_pictureFactory.Create(sTextRepOfPicture, CmFolderTags.LocalPictures),
				Throws.ArgumentException);
		}

		/// -------------------------------------------------------------------------------------
		/// <summary>
		/// Test that the CmPicture contructor stores a filename which is not a full path as a
		/// relative path consisting of just that filename.
		/// </summary>
		/// <remarks>
		/// Consider whether this should throw an exception if the argument is not an
		/// absolute path.
		/// </remarks>
		/// -------------------------------------------------------------------------------------
		[Test]
		[Ignore("Todo RickM (JohnT): I think we no longer throw exceptions if the picture file is not rooted? Is there something we should test instead?")]
		public void CmPictureConstructor_FromTextRep_FilenameNotFullPath()
		{
			Assert.That(m_pictureFactory.Create("CmPicture||whatever.jpg||||This is a caption||", CmFolderTags.LocalPictures), Throws.ArgumentException.With.Property("Message").Matches("File does not have a rooted pathname: whatever.jpg(\r)?\nParameter name: srcFile"));
		}

		/// <summary>
		/// Tests creating a CmPicture from a GUID
		/// </summary>
		[Test]
		public void CmPictureConstructor_FromGuid()
		{
			var guid = Guid.NewGuid();

			// Execute
			var picture = m_pictureFactory.Create(guid);

			// Verify
			Assert.That(picture.Guid, Is.EqualTo(guid));
		}

		/// <summary>
		/// Tests creating a CmPicture from a empty GUID
		/// </summary>
		[Test]
		public void CmPictureConstructor_FromEmptyGuid()
		{
			// Execute
			var picture = m_pictureFactory.Create(Guid.Empty);

			// Verify
			Assert.That(picture.Guid, Is.Not.Null);
		}

		/// -------------------------------------------------------------------------------------
		/// <summary>
		/// Test ability to get the text representation of a picture.
		/// </summary>
		/// -------------------------------------------------------------------------------------
		[Test]
		public void CmPicture_GetTextRepOfPicture()
		{
			m_pict.Description.set_String(Cache.DefaultAnalWs, "Your mom");
			m_pict.Description.set_String(m_wsSpanish.Handle, "Tu madre");
			ChangeDefaultAnalWs(m_wsGerman);

			string textRep = m_pict.GetTextRepOfPicture(false, "MyRef", null);
			string [] figParams = textRep.Split(new char[] {'|'}, StringSplitOptions.None);
			Assert.AreEqual("Your mom", figParams[0], "English Description should be exported.");
			Assert.AreEqual(m_internalPath, figParams[1]);
			Assert.AreEqual("col", figParams[2], "Layout position should be exported.");
			Assert.AreEqual(string.Empty, figParams[3], "Picture location should be empty.");
			Assert.AreEqual(string.Empty, figParams[4], "Copyright should be empty.");
			Assert.AreEqual("Test picture", figParams[5], "Caption (vernacular) should be exported.");
			Assert.AreEqual("MyRef", figParams[6], "Picture reference should be exported.");
		}

		/// -------------------------------------------------------------------------------------
		/// <summary>
		/// Test ability to update the properties of a picture, given a file, folder, etc.
		/// </summary>
		/// -------------------------------------------------------------------------------------
		[Test]
		public void UpdateCmPicture()
		{
			string sNewFile = Path.DirectorySeparatorChar + Path.GetRandomFileName();
			m_fileOs.AddFile(sNewFile, "456", Encoding.Default);

			m_pict.UpdatePicture(sNewFile,
				TsStringUtils.MakeString("Updated Picture", Cache.DefaultVernWs),
				CmFolderTags.LocalPictures, Cache.DefaultVernWs);
			Assert.AreEqual("Updated Picture", m_pict.Caption.VernacularDefaultWritingSystem.Text);
			string internalPathUpdated = m_pict.PictureFileRA.InternalPath;
			Assert.AreEqual(sNewFile, internalPathUpdated, "Internal path not set correctly");
			Assert.AreEqual(sNewFile, m_pict.PictureFileRA.AbsoluteInternalPath, "Files outside LangProject.LinkedFilesRootDir are stored as absolute paths");
		}

		/// -------------------------------------------------------------------------------------
		/// <summary>
		/// Test that a FileNotFoundException is not thrown if user attempts to update a picture,
		/// with a file that is no longer accessible.
		/// </summary>
		/// -------------------------------------------------------------------------------------
		[Test]
		public void UpdateCmPicture_FileNotFound()
		{
			string sNewFile = Path.DirectorySeparatorChar + Path.GetRandomFileName();
			m_pict.UpdatePicture(sNewFile,
				TsStringUtils.MakeString("Updated Picture", Cache.DefaultVernWs),
				CmFolderTags.LocalPictures, Cache.DefaultVernWs);
			Assert.AreEqual("Updated Picture", m_pict.Caption.VernacularDefaultWritingSystem.Text);
			string internalPathUpdated = m_pict.PictureFileRA.InternalPath;
			Assert.AreEqual(sNewFile, internalPathUpdated, "Internal path not set correctly");
			Assert.AreEqual(sNewFile, m_pict.PictureFileRA.AbsoluteInternalPath, "Files outside LangProject.LinkedFilesRootDir are stored as absolute paths");
			Assert.IsFalse(FileUtils.SimilarFileExists(sNewFile));
		}

		/// -------------------------------------------------------------------------------------
		/// <summary>
		/// Test ability to get a string representation of the picture suitable to put on the
		/// clipboard.
		/// </summary>
		/// -------------------------------------------------------------------------------------
		[Test]
		public void TextRepOfPicture()
		{
			Assert.AreEqual("CmPicture||" +
				m_pict.PictureFileRA.AbsoluteInternalPath + "|col|||Test picture|AfterAnchor|100",
				m_pict.TextRepresentation);
		}

		/// --------------------------------------------------------------------------------
		/// <summary>
		/// Test ability to Insert a CmPicture ORC into a TS string.
		/// </summary>
		/// --------------------------------------------------------------------------------
		[Test]
		public void InsertORCAt_Simple()
		{
			ILangProject lp = Cache.LangProject;
			lp.Description.set_String(Cache.DefaultAnalWs, "This is my language project");
			ITsString tss = lp.Description.AnalysisDefaultWritingSystem;
			int ichInsert = 4;
			int cchOrigStringLength = tss.Length;

			lp.Description.set_String(Cache.DefaultAnalWs, m_pict.InsertORCAt(tss, ichInsert));

			tss = lp.Description.AnalysisDefaultWritingSystem;
			Assert.AreEqual(cchOrigStringLength + 1, tss.Length);
			Assert.AreEqual(3, tss.RunCount, "ORC should split original run into 3 runs");
			Assert.AreEqual(0, tss.get_RunAt(ichInsert - 1), "First run should end before where we inserted the ORC");
			Assert.AreEqual(1, tss.get_RunAt(ichInsert), "Second run should be where we inserted the ORC");
			Assert.AreEqual(2, tss.get_RunAt(ichInsert + 1), "Third run should start after where we inserted the ORC");
			string strGuid = tss.get_Properties(1).GetStrPropValue((int)FwTextPropType.ktptObjData);
			Guid guid = MiscUtils.GetGuidFromObjData(strGuid.Substring(1));
			Assert.AreEqual(m_pict.Guid, guid, "Wrong guid was inserted");
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the ToolboxPictureInfo.ParseLayoutPos method when given valid values
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ParseLayoutPos_Valid()
		{
			Type cmpicture = typeof(CmPictureFactory);
			Assert.AreEqual(PictureLayoutPosition.CenterInColumn,
				(PictureLayoutPosition)ReflectionHelper.GetResult(cmpicture, "ParseLayoutPosition", "col"));
			Assert.AreEqual(PictureLayoutPosition.CenterInColumn,
				(PictureLayoutPosition)ReflectionHelper.GetResult(cmpicture, "ParseLayoutPosition", "CenterInColumn"));

			Assert.AreEqual(PictureLayoutPosition.CenterOnPage,
			(PictureLayoutPosition)ReflectionHelper.GetResult(cmpicture, "ParseLayoutPosition", "span"));
			Assert.AreEqual(PictureLayoutPosition.CenterOnPage,
				(PictureLayoutPosition)ReflectionHelper.GetResult(cmpicture, "ParseLayoutPosition", "CenterOnPage"));

			Assert.AreEqual(PictureLayoutPosition.RightAlignInColumn,
				(PictureLayoutPosition)ReflectionHelper.GetResult(cmpicture, "ParseLayoutPosition", "right"));
			Assert.AreEqual(PictureLayoutPosition.RightAlignInColumn,
				(PictureLayoutPosition)ReflectionHelper.GetResult(cmpicture, "ParseLayoutPosition", "RightAlignInColumn"));

			Assert.AreEqual(PictureLayoutPosition.LeftAlignInColumn,
				(PictureLayoutPosition)ReflectionHelper.GetResult(cmpicture, "ParseLayoutPosition", "left"));
			Assert.AreEqual(PictureLayoutPosition.LeftAlignInColumn,
				(PictureLayoutPosition)ReflectionHelper.GetResult(cmpicture, "ParseLayoutPosition", "LeftAlignInColumn"));

			Assert.AreEqual(PictureLayoutPosition.FillColumnWidth,
				(PictureLayoutPosition)ReflectionHelper.GetResult(cmpicture, "ParseLayoutPosition", "fillcol"));
			Assert.AreEqual(PictureLayoutPosition.FillColumnWidth,
				(PictureLayoutPosition)ReflectionHelper.GetResult(cmpicture, "ParseLayoutPosition", "FillColumnWidth"));

			Assert.AreEqual(PictureLayoutPosition.FillPageWidth,
				(PictureLayoutPosition)ReflectionHelper.GetResult(cmpicture, "ParseLayoutPosition", "fillspan"));
			Assert.AreEqual(PictureLayoutPosition.FillPageWidth,
				(PictureLayoutPosition)ReflectionHelper.GetResult(cmpicture, "ParseLayoutPosition", "FillPageWidth"));

			Assert.AreEqual(PictureLayoutPosition.FullPage,
				(PictureLayoutPosition)ReflectionHelper.GetResult(cmpicture, "ParseLayoutPosition", "fullpage"));
			Assert.AreEqual(PictureLayoutPosition.FullPage,
				(PictureLayoutPosition)ReflectionHelper.GetResult(cmpicture, "ParseLayoutPosition", "FullPage"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the ToolboxPictureInfo.ParseLayoutPos method when given invalid values
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ParseLayoutPos_Invalid()
		{
			Assert.AreEqual(PictureLayoutPosition.CenterInColumn,
				(PictureLayoutPosition)ReflectionHelper.GetResult(
				typeof(CmPictureFactory),
				"ParseLayoutPosition", "monkey brains"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the ToolboxPictureInfo.ParseScale method when given valid value
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ParseScale_Normal()
		{
			Assert.AreEqual(34, ReflectionHelper.GetIntResult(
				typeof(CmPictureFactory),
				"ParseScaleFactor", "34"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the ToolboxPictureInfo.ParseScale method when given no value
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ParseScale_Null()
		{
			Assert.AreEqual(100, ReflectionHelper.GetIntResult(
				typeof(CmPictureFactory),
				"ParseScaleFactor", new object[] {null}));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the ToolboxPictureInfo.ParseScale method when given no value
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ParseScale_Empty()
		{
			Assert.AreEqual(100, ReflectionHelper.GetIntResult(
				typeof(CmPictureFactory),
				"ParseScaleFactor", String.Empty));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the ToolboxPictureInfo.ParseScale method when given negative value
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ParseScale_Negative()
		{
			Assert.AreEqual(53, ReflectionHelper.GetIntResult(
				typeof(CmPictureFactory),
				"ParseScaleFactor", "-53"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the ToolboxPictureInfo.ParseScale method when given a value that contains both
		/// text and a number
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ParseScale_TextAndNumber()
		{
			Assert.AreEqual(53, ReflectionHelper.GetIntResult(
				typeof(CmPictureFactory),
				"ParseScaleFactor", "scale=53"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the ToolboxPictureInfo.ParseScale method when given a value that contains more
		/// than one number (we just take the first one)
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ParseScale_MultipleNumbers()
		{
			Assert.AreEqual(93, ReflectionHelper.GetIntResult(
			typeof(CmPictureFactory),
				"ParseScaleFactor", "down 93, hut1, hut2, 34, 0, hike!"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the ToolboxPictureInfo.ParseScale method when given a value of 0
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ParseScale_Zero()
		{
			Assert.AreEqual(100, ReflectionHelper.GetIntResult(
				typeof(CmPictureFactory),
				"ParseScaleFactor", "0"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the ToolboxPictureInfo.ParseScale method when given an excessively large value
		/// (we cap it at 1000%)
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ParseScale_HumongousNumber()
		{
			Assert.AreEqual(1000, ReflectionHelper.GetIntResult(
				typeof(CmPictureFactory),
				"ParseScaleFactor", "100000"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the ToolboxPictureInfo.ParseScale method when given a number with an explicit
		/// percent sign.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ParseScale_PercentSign()
		{
			Assert.AreEqual(43, ReflectionHelper.GetIntResult(
				typeof(CmPictureFactory),
				"ParseScaleFactor", "43%"));
		}
	}
}
