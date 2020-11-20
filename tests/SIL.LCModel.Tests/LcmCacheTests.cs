// Copyright (c) 2003-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.DomainServices;
using SIL.LCModel.Infrastructure;
using SIL.LCModel.Infrastructure.Impl;
using SIL.LCModel.Utils;
using SIL.Lexicon;

namespace SIL.LCModel
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Tests the public API of LcmCache.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[TestFixture]
	public class LcmCacheTests : MemoryOnlyBackendProviderTestBase
	{
		private ILcmUI m_ui;
		private string m_projectsDirectory;
		private ILcmDirectories m_lcmDirectories;

		/// <summary>Setup for db4o client server tests.</summary>
		public override void FixtureSetup()
		{
			base.FixtureSetup();

			m_projectsDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			Directory.CreateDirectory(m_projectsDirectory);

			m_ui = new DummyLcmUI();
			m_lcmDirectories = new TestLcmDirectories(m_projectsDirectory);
		}

		/// <summary></summary>
		public override void FixtureTeardown()
		{
			Directory.Delete(m_projectsDirectory, true);
			base.FixtureTeardown();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test when database files already exist.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void CreateNewLangProject_DbFilesExist()
		{
			var preExistingDirs = new List<string>(Directory.GetDirectories(m_projectsDirectory));
			try
			{
				// Setup: Create "pre-existing" DB filenames
				using (new DummyFileMaker(Path.Combine(m_projectsDirectory, "Gumby", LcmFileHelper.GetXmlDataFileName("Gumby"))))
				{
					Assert.That(() => LcmCache.CreateNewLangProj(new DummyProgressDlg(), "Gumby", m_lcmDirectories,
						new SingleThreadedSynchronizeInvoke(), null, null, null, null, null, null, true),
						Throws.TypeOf<ArgumentException>());
				}
			}
			finally
			{
				RemoveTestDirs(preExistingDirs, Directory.GetDirectories(m_projectsDirectory));
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test handling of single quote in language project name.
		/// JIRA Issue TE-6138.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void CreateNewLangProject_NameWithSingleQuote()
		{
			const string dbName = "!!t'st";
			string dbDir = Path.Combine(m_projectsDirectory, dbName);
			SureRemoveDb(dbName);

			var expectedDirs = new List<string>(Directory.GetDirectories(m_projectsDirectory)) { dbDir };
			var writingSystemsCommonDir = Path.Combine(m_projectsDirectory, LcmFileHelper.ksWritingSystemsDir);

			List<string> currentDirs = null;
			try
			{
				string dbFileName = LcmCache.CreateNewLangProj(new DummyProgressDlg(), dbName, m_lcmDirectories,
					new SingleThreadedSynchronizeInvoke());

				currentDirs = new List<string>(Directory.GetDirectories(m_projectsDirectory));
				if (currentDirs.Contains(writingSystemsCommonDir) && !expectedDirs.Contains(writingSystemsCommonDir))
					expectedDirs.Add(writingSystemsCommonDir);
				CollectionAssert.AreEquivalent(expectedDirs, currentDirs);
				string dbFileBase = Path.GetFileNameWithoutExtension(dbFileName);
				Assert.AreEqual(dbName, dbFileBase);
			}
			finally
			{
				if (currentDirs != null)
					RemoveTestDirs(expectedDirs, currentDirs);
			}
		}

		/// <summary>
		/// Tests that, when a new language project is created, the Anthropology Categories list is created for that project
		/// </summary>
		[Test]
		public void CreateNewLangProject_AnthropologyCategoriesExist()
		{
			const string dbName = "AnthropologicalTest";
			SureRemoveDb(dbName);
			var preExistingDirs = new List<string>(Directory.GetDirectories(m_projectsDirectory));
			try
			{
				// create project
				string dbFileName = LcmCache.CreateNewLangProj(new DummyProgressDlg(), dbName, m_lcmDirectories,
					new SingleThreadedSynchronizeInvoke());

				var projectId = new TestProjectId(BackendProviderType.kXMLWithMemoryOnlyWsMgr, dbFileName);
				using (var cache = LcmCache.CreateCacheFromExistingData(projectId, "en", m_ui, m_lcmDirectories, new LcmSettings(),
					new DummyProgressDlg()))
				{
					Assert.AreEqual(Strings.ksAnthropologyCategories, cache.LangProject.AnthroListOA.Name.UiString,
						"Anthropology Categories list was not properly initialized.");
					Assert.AreEqual(Strings.ksAnth, cache.LangProject.AnthroListOA.Abbreviation.UiString,
						"Anthropology Categories list abrv was not properly initialized.");
					Assert.AreNotEqual(0, cache.LangProject.AnthroListOA.ItemClsid,
						"Anthropology Categories list class ID was not properly initialized.");
					Assert.AreNotEqual(0, cache.LangProject.AnthroListOA.Depth,
						"Anthropology Categories list depth was not properly initialized.");
				}
			}
			finally
			{
				RemoveTestDirs(preExistingDirs, Directory.GetDirectories(m_projectsDirectory));
			}
		}

		#region Private helper methods
		/// <summary>
		/// Removes a FW DB directory, and ensures the location is promptly available for reuse
		/// </summary>
		/// <param name="dbName">name of the FW DB to remove</param>
		private void SureRemoveDb(string dbName)
		{
			string dbDir = Path.Combine(m_projectsDirectory, dbName);
			var tmpDbDir = Path.Combine(m_projectsDirectory, "..", dbName);
			if (Directory.Exists(dbDir))
			{
				// it might seem strange to move the directory first before deleting it.
				// However, this solves the problem that the Delete() returns before
				// everything is deleted.
				Directory.Move(dbDir, tmpDbDir);
				Directory.Delete(tmpDbDir, true);
			}
			Assert.IsFalse(Directory.Exists(dbDir), "Can't delete directory of test project: " + dbDir);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Removes the test dirs.
		/// </summary>
		/// <param name="preExistingDirs">The pre existing dirs.</param>
		/// <param name="postExistingDirs">The post existing dirs.</param>
		/// ------------------------------------------------------------------------------------
		private static void RemoveTestDirs(List<string> preExistingDirs, IEnumerable<string> postExistingDirs)
		{
			// Blow away the files to clean things up
			foreach (string dirName in postExistingDirs)
			{
				try
				{
					if (!preExistingDirs.Contains(dirName))
						Directory.Delete(dirName, true);
				}
				catch
				{
				}
			}
		}
		#endregion

		/// <summary>
		/// What it says.
		/// </summary>
		[Test]
		public void ChangingLangProjDefaultVernWs_ChangesCacheDefaultVernWs()
		{
			using (var cache = LcmCache.CreateCacheWithNewBlankLangProj(new TestProjectId(BackendProviderType.kMemoryOnly, null),
				"en", "fr", "en", m_ui, m_lcmDirectories, new LcmSettings()))
			{
				var wsFr = cache.DefaultVernWs;
				Assert.That(cache.LangProject.DefaultVernacularWritingSystem.Handle, Is.EqualTo(wsFr));
				CoreWritingSystemDefinition wsObjGerman = null;
				UndoableUnitOfWorkHelper.Do("undoit", "redoit", cache.ActionHandlerAccessor,
					() =>
					{
						WritingSystemServices.FindOrCreateWritingSystem(cache, TestDirectoryFinder.TemplateDirectory, "de", false, true, out wsObjGerman);
						Assert.That(cache.DefaultVernWs, Is.EqualTo(wsFr));
						cache.LangProject.DefaultVernacularWritingSystem = wsObjGerman;
						Assert.That(cache.DefaultVernWs, Is.EqualTo(wsObjGerman.Handle));
					});
				UndoableUnitOfWorkHelper.Do("undoit", "redoit", cache.ActionHandlerAccessor,
				   () =>
				   {
					   cache.LangProject.CurVernWss = "fr";
					   Assert.That(cache.DefaultVernWs, Is.EqualTo(wsFr));
				   });
				cache.ActionHandlerAccessor.Undo();
				Assert.That(cache.DefaultVernWs, Is.EqualTo(wsObjGerman.Handle));
				cache.ActionHandlerAccessor.Redo();
				Assert.That(cache.DefaultVernWs, Is.EqualTo(wsFr));
			}
		}
		/// <summary>
		/// What it says.
		/// </summary>
		[Test]
		public void ChangingLangProjDefaultAnalysisWs_ChangesCacheDefaultAnalWs()
		{
			using (var cache = LcmCache.CreateCacheWithNewBlankLangProj(new TestProjectId(BackendProviderType.kMemoryOnly, null),
				"en", "fr", "en", m_ui, m_lcmDirectories, new LcmSettings()))
			{
				var wsEn = cache.DefaultAnalWs;
				Assert.That(cache.LangProject.DefaultAnalysisWritingSystem.Handle, Is.EqualTo(wsEn));
				CoreWritingSystemDefinition wsObjGerman = null;
				UndoableUnitOfWorkHelper.Do("undoit", "redoit", cache.ActionHandlerAccessor,
					() =>
					{
						WritingSystemServices.FindOrCreateWritingSystem(cache, TestDirectoryFinder.TemplateDirectory, "de", true, false, out wsObjGerman);
						Assert.That(cache.DefaultAnalWs, Is.EqualTo(wsEn));
						cache.LangProject.DefaultAnalysisWritingSystem = wsObjGerman;
						Assert.That(cache.DefaultAnalWs, Is.EqualTo(wsObjGerman.Handle));
					});
				UndoableUnitOfWorkHelper.Do("undoit", "redoit", cache.ActionHandlerAccessor,
				   () =>
				   {
					   cache.LangProject.CurAnalysisWss = "en";
					   Assert.That(cache.DefaultAnalWs, Is.EqualTo(wsEn));
				   });
				cache.ActionHandlerAccessor.Undo();
				Assert.That(cache.DefaultAnalWs, Is.EqualTo(wsObjGerman.Handle));
				cache.ActionHandlerAccessor.Redo();
				Assert.That(cache.DefaultAnalWs, Is.EqualTo(wsEn));
			}
		}

		/// <summary>
		/// What it says.
		/// </summary>
		[Test]
		public void ChangingLangProjDefaultPronunciationWs_ChangesCacheDefaultPronunciationWs()
		{
			using (var cache = LcmCache.CreateCacheWithNewBlankLangProj(new TestProjectId(BackendProviderType.kMemoryOnly, null),
				"en", "fr", "en", m_ui, m_lcmDirectories, new LcmSettings()))
			{
				var wsFr = cache.DefaultPronunciationWs;
				Assert.That(cache.LangProject.DefaultPronunciationWritingSystem.Handle, Is.EqualTo(wsFr));
				CoreWritingSystemDefinition wsObjGerman = null;
				CoreWritingSystemDefinition wsObjSpanish = null;
				UndoableUnitOfWorkHelper.Do("undoit", "redoit", cache.ActionHandlerAccessor,
					() =>
					{
						WritingSystemServices.FindOrCreateWritingSystem(cache, TestDirectoryFinder.TemplateDirectory, "de", false, true, out wsObjGerman);
						Assert.That(cache.DefaultPronunciationWs, Is.EqualTo(wsFr));
						cache.LangProject.DefaultVernacularWritingSystem = wsObjGerman;
						cache.LangProject.CurrentPronunciationWritingSystems.Clear();
						// Now it re-evaluates to the new default vernacular.
						Assert.That(cache.DefaultPronunciationWs, Is.EqualTo(wsObjGerman.Handle));

						// This no longer works..._IPA does not make a valid WS ID.
						//IWritingSystem wsObjGermanIpa;
						//WritingSystemServices.FindOrCreateWritingSystem(cache, "de__IPA", false, true, out wsObjGermanIpa);
						//cache.LangProject.CurrentPronunciationWritingSystems.Clear();
						//// Once there is an IPA one, we should prefer that
						//Assert.That(cache.DefaultPronunciationWs, Is.EqualTo(wsObjGermanIpa.Handle));

						// Unless we clear the list it does not regenerate.
						WritingSystemServices.FindOrCreateWritingSystem(cache, TestDirectoryFinder.TemplateDirectory, "es", false, true, out wsObjSpanish);
						// Once we've found a real pronunciation WS, changing the default vernacular should not change it.
						Assert.That(cache.DefaultPronunciationWs, Is.EqualTo(wsObjGerman.Handle));
					});
				UndoableUnitOfWorkHelper.Do("undoit", "redoit", cache.ActionHandlerAccessor,
				   () =>
				   {
					   cache.LangProject.CurPronunWss = "es";
					   Assert.That(cache.DefaultPronunciationWs, Is.EqualTo(wsObjSpanish.Handle));
				   });
				cache.ActionHandlerAccessor.Undo();
				Assert.That(cache.DefaultPronunciationWs, Is.EqualTo(wsObjGerman.Handle));
				cache.ActionHandlerAccessor.Redo();
				Assert.That(cache.DefaultPronunciationWs, Is.EqualTo(wsObjSpanish.Handle));
			}
		}

		[Test]
		public void TestThatSharedSettingOpensXmlDataTypeAsSharedXml()
		{
			const string dbName = "ProjectSharingTest";
			SureRemoveDb(dbName);
			var preExistingDirs = new List<string>(Directory.GetDirectories(m_projectsDirectory));
			try
			{
				// create project
				string dbFileName = LcmCache.CreateNewLangProj(new DummyProgressDlg(), dbName, m_lcmDirectories,
					new SingleThreadedSynchronizeInvoke());
				// Set up test file for project sharing setting
				var testFileStore = new FileSettingsStore(LexiconSettingsFileHelper.GetProjectLexiconSettingsPath(Path.GetDirectoryName(dbFileName)));
				var dataMapper = new ProjectLexiconSettingsDataMapper(testFileStore);
				dataMapper.Write(new ProjectLexiconSettings { ProjectSharing = true });
				// SUT
				// Request XML backend with project settings that have ProjectSharing set to true
				var projectId = new TestProjectId(BackendProviderType.kXML, dbFileName);
				using (var cache = LcmCache.CreateCacheFromExistingData(projectId, "en", m_ui, m_lcmDirectories, new LcmSettings(),
					new DummyProgressDlg()))
				{
					var dataSetup = cache.ServiceLocator.GetInstance<IDataSetup>();
					Assert.IsTrue(dataSetup is SharedXMLBackendProvider, "The project should have been opened as shared xml.");
				}
			}
			finally
			{
				RemoveTestDirs(preExistingDirs, Directory.GetDirectories(m_projectsDirectory));
			}
		}

		[Test]
		[TestCase("", "", "", "", "", "")]
		[TestCase("NewEnId", "", "", "", "NewEnId", "")]
		[TestCase("", "NewFrId", "NewEnId", "", "NewEnId", "NewFrId")]
		public void UpdateWritingSystemsFromGlobalStore_CopiesNewerWsOnly(
			string globalEn, string globalFr,
			string localEn, string localFr,
			string localEnResult, string localFrResult)
		{
			const string dbName = "UpdateWsFromGsTest";
			SureRemoveDb(dbName);
			var preExistingDirs = new List<string>(Directory.GetDirectories(m_projectsDirectory));
			try
			{
				// create project
				var dbFileName = LcmCache.CreateNewLangProj(new DummyProgressDlg(), dbName, m_lcmDirectories,
					new SingleThreadedSynchronizeInvoke());
				// SUT
				// Request XML backend with project settings that have ProjectSharing set to true
				var projectId = new TestProjectId(BackendProviderType.kXML, dbFileName);
				using (var cache = LcmCache.CreateCacheFromExistingData(projectId, "en", m_ui, m_lcmDirectories, new LcmSettings(),
					new DummyProgressDlg()))
				{
					var globalPath = Path.Combine(m_projectsDirectory,
						$"{Path.GetFileNameWithoutExtension(dbFileName)}_GlobalWss");
					var globalPathWithVersion = CoreGlobalWritingSystemRepository.CurrentVersionPath(globalPath);
					Directory.CreateDirectory(globalPathWithVersion);
					var storePath = Path.Combine(cache.ProjectId.ProjectFolder, LcmFileHelper.ksWritingSystemsDir);
					File.Copy(Path.Combine(storePath, "en.ldml"), Path.Combine(globalPathWithVersion, "en.ldml"));
					File.Copy(Path.Combine(storePath, "fr.ldml"), Path.Combine(globalPathWithVersion, "fr.ldml"));
					var wsManager = cache.ServiceLocator.WritingSystemManager;

				   // Add new Ws for French and English in global repo
				   var globalRepoForTest = new CoreGlobalWritingSystemRepository(globalPath);

					// Set up WritingSystemStore for test
				   wsManager.WritingSystemStore = new CoreLdmlInFolderWritingSystemRepository(storePath,
						cache.ServiceLocator.DataSetup.ProjectSettingsStore,
						cache.ServiceLocator.DataSetup.UserSettingsStore,
						globalRepoForTest);

					var enWs = globalRepoForTest.Get("en");
					var frWs = globalRepoForTest.Get("fr");
					Assert.That(string.IsNullOrEmpty(enWs.SpellCheckingId), Is.True);
					Assert.That(string.IsNullOrEmpty(cache.WritingSystemFactory.get_Engine("en").SpellCheckingId), Is.True);
					Assert.That(string.IsNullOrEmpty(frWs.SpellCheckingId), Is.True);
					Assert.That(string.IsNullOrEmpty(cache.WritingSystemFactory.get_Engine("fr").SpellCheckingId), Is.True);

					// Update the spellCheckIds in the global repository
					if (globalEn != null)
					{
						enWs.SpellCheckingId = globalEn;
					}

					if (globalFr != null)
					{
						frWs.SpellCheckingId = globalFr;
					}
					globalRepoForTest.Set(enWs);
					globalRepoForTest.Set(frWs);
					globalRepoForTest.Save();
					// Update the cache version of the repository
					var enWsFromCache = cache.ServiceLocator.WritingSystemManager.Get("en");
					var frWsFromCache = cache.ServiceLocator.WritingSystemManager.Get("fr");
					enWsFromCache.SpellCheckingId = localEn;
					frWsFromCache.SpellCheckingId = localFr;
					cache.ServiceLocator.WritingSystemManager.Set(enWsFromCache);
					cache.ServiceLocator.WritingSystemManager.Set(frWsFromCache);
					cache.ServiceLocator.WritingSystemManager.Save();
					enWs = globalRepoForTest.Get("en");
					frWs = globalRepoForTest.Get("fr");

					// Verify preconditions
					Assert.That(enWs.SpellCheckingId, Is.StringMatching(globalEn));
					Assert.That(frWs.SpellCheckingId, Is.StringMatching(globalFr));
					Assert.That(cache.WritingSystemFactory.get_Engine("en").SpellCheckingId, Is.StringMatching(localEn));
					Assert.That(cache.WritingSystemFactory.get_Engine("fr").SpellCheckingId, Is.StringMatching(localFr));

					// SUT
					cache.UpdateWritingSystemsFromGlobalStore("en");
					Assert.That(cache.WritingSystemFactory.get_Engine("en").SpellCheckingId, Is.StringMatching(localEnResult));
					Assert.That(cache.WritingSystemFactory.get_Engine("fr").SpellCheckingId, Is.StringMatching(localFr));
					
					cache.UpdateWritingSystemsFromGlobalStore("fr");
					Assert.That(cache.WritingSystemFactory.get_Engine("fr").SpellCheckingId, Is.StringMatching(localFrResult));
				}
			}
			finally
			{
				RemoveTestDirs(preExistingDirs, Directory.GetDirectories(m_projectsDirectory));
			}
		}
   }

	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Tests the Disposed related methods on LcmCache.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[TestFixture]
	public class LcmCacheDisposedTests
	{
		private readonly ILcmUI m_ui = new DummyLcmUI();

		/// <summary>
		/// Make sure the CheckDisposed method works.
		/// </summary>
		[Test]
		public void CacheCheckDisposedTest()
		{
			// This can't be in the minimalist class, because it disposes the cache.
			var cache = LcmCache.CreateCacheWithNewBlankLangProj(new TestProjectId(BackendProviderType.kMemoryOnly, null),
				"en", "fr", "en", m_ui, TestDirectoryFinder.LcmDirectories, new LcmSettings());
			// Init backend data provider
			var dataSetup = cache.ServiceLocator.GetInstance<IDataSetup>();
			dataSetup.LoadDomain(BackendBulkLoadDomain.All);
			cache.Dispose();
			Assert.That(() => cache.CheckDisposed(), Throws.TypeOf<ObjectDisposedException>());
		}

		/// <summary>
		/// Make sure the IsDisposed method works.
		/// </summary>
		[Test]
		public void CacheIsDisposedTest()
		{
			// This can't be in the minimalist class, because it disposes the cache.
			var cache = LcmCache.CreateCacheWithNewBlankLangProj(new TestProjectId(BackendProviderType.kMemoryOnly, null),
				"en", "fr", "en", m_ui, TestDirectoryFinder.LcmDirectories, new LcmSettings());
			// Init backend data provider
			var dataSetup = cache.ServiceLocator.GetInstance<IDataSetup>();
			dataSetup.LoadDomain(BackendBulkLoadDomain.All);
			Assert.IsFalse(cache.IsDisposed, "Should not have been disposed.");
			cache.Dispose();
			Assert.IsTrue(cache.IsDisposed, "Should have been disposed.");
		}

		/// <summary>
		/// Make sure an LCM can't be used, after its LcmCache has been disposed.
		/// </summary>
		[Test]
		public void CacheDisposedForLcmObject()
		{
			var cache = LcmCache.CreateCacheWithNewBlankLangProj(new TestProjectId(BackendProviderType.kMemoryOnly, null),
				"en", "fr", "en", m_ui, TestDirectoryFinder.LcmDirectories, new LcmSettings());
			// Init backend data provider
			var dataSetup = cache.ServiceLocator.GetInstance<IDataSetup>();
			dataSetup.LoadDomain(BackendBulkLoadDomain.All);
			var lp = cache.LanguageProject;
			cache.Dispose();
			Assert.IsFalse(lp.IsValidObject);
		}

		/// <summary>
		/// Make sure an LCM can't be used, after its LcmCache has been disposed.
		/// </summary>
		[Test]
		public void LcmObjectDeleted()
		{
			using (var cache = LcmCache.CreateCacheWithNewBlankLangProj(new TestProjectId(BackendProviderType.kMemoryOnly, null),
				"en", "fr", "en", m_ui, TestDirectoryFinder.LcmDirectories, new LcmSettings()))
			{
				// Init backend data provider
				var dataSetup = cache.ServiceLocator.GetInstance<IDataSetup>();
				dataSetup.LoadDomain(BackendBulkLoadDomain.All);
				var lp = cache.LanguageProject;
				cache.ActionHandlerAccessor.BeginNonUndoableTask();
				var peopleList = cache.ServiceLocator.GetInstance<ICmPossibilityListFactory>().Create();
				lp.PeopleOA = peopleList;
				lp.PeopleOA = null;
				cache.ActionHandlerAccessor.EndNonUndoableTask();
				Assert.IsFalse(peopleList.IsValidObject);
			}
		}
	}
}
