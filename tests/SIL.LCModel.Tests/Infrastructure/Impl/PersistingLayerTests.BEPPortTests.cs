// Copyright (c) 2014-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using SIL.LCModel.Utils;
using SIL.TestUtilities;

namespace SIL.LCModel.Infrastructure.Impl
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Test migrating data from each type of BEP to all others.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[TestFixture]
	public sealed class BEPPortTests
	{
		/// <summary>Random number generator to prevent filename conflicts</summary>
		private readonly Random m_random;

		private TemporaryFolder m_projectsFolder;
		private ILcmDirectories m_lcmDirectories;

		/// <summary />
		public BEPPortTests()
		{
			m_random = new Random((int)DateTime.Now.Ticks);
		}

		/// <summary />
		[SetUp]
		public void TestSetup()
		{
			m_projectsFolder = new TemporaryFolder("BEPPortTests");
			m_lcmDirectories = new TestLcmDirectories(m_projectsFolder.Path);
		}

		/// <summary />
		[TearDown]
		public void TestTeardown()
		{
			m_projectsFolder.Dispose();
		}

		#region Non-test methods.

		private BackendStartupParameter GenerateBackendStartupParameters(bool isTarget, BackendProviderType type)
		{
			var nameSuffix = (isTarget ? "_New" : "") + m_random.Next(1000);
			string name = null;
			switch (type)
			{
				case BackendProviderType.kXMLWithMemoryOnlyWsMgr:
					name = Path.Combine(m_projectsFolder.Path, LcmFileHelper.GetXmlDataFileName("TLP" + nameSuffix));
					break;
				case BackendProviderType.kCRDTWithMemoryOnlyWsMgr:
					name = Path.Combine(m_projectsFolder.Path, $"TLP{nameSuffix}.json");
					break;
			}

			return new BackendStartupParameter(true, BackendBulkLoadDomain.All, new TestProjectId(type, name));
		}

		/// <summary>
		/// Actually do the test between the source data in 'sourceGuids'
		/// and the target data in 'targetCache'.
		/// </summary>
		/// <param name="sourceGuids"></param>
		/// <param name="targetCache"></param>
		private static void CompareResults(ICollection<Guid> sourceGuids, LcmCache targetCache)
		{
			var allTargetObjects = GetAllCmObjects(targetCache);
			foreach (var obj in allTargetObjects)
				Assert.IsTrue(sourceGuids.Contains(obj.Guid), "Missing guid in target DB.: " + obj.Guid);
			var targetGuids = allTargetObjects.Select(obj => obj.Guid).ToList();
			foreach (var guid in sourceGuids)
			{
				Assert.IsTrue(targetGuids.Contains(guid), "Missing guid in source DB.: " + guid);
			}
			Assert.AreEqual(sourceGuids.Count, allTargetObjects.Length, "Wrong number of objects in target DB.");
		}

		/// <summary>
		/// Get the ICmObjectRepository from the cache.
		/// </summary>
		/// <param name="cache"></param>
		/// <returns></returns>
		private static ICmObject[] GetAllCmObjects(LcmCache cache)
		{
			return cache.ServiceLocator.GetInstance<ICmObjectRepository>().AllInstances().ToArray();
		}

		/// <summary>
		/// Get the IDataSetup from the cache.
		/// </summary>
		/// <param name="cache"></param>
		/// <returns></returns>
		private static IDataSetup GetMainBEPInterface(LcmCache cache)
		{
			return cache.ServiceLocator.GetInstance<IDataSetup>();
		}

		/// <summary>
		/// Wipe out the current BEP's file(s), since it is about to be created ex-nihilo.
		/// </summary>
		private static void DeleteDatabase(BackendStartupParameter backendParameters, bool assertThatDbWasDeleted = true)
		{
			string pathname = string.Empty;
			if(backendParameters.ProjectId.Type != BackendProviderType.kMemoryOnly)
				pathname = backendParameters.ProjectId.Path;
			if(backendParameters.ProjectId.Type != BackendProviderType.kMemoryOnly &&
				File.Exists(pathname))
			{
				try
				{
					File.Delete(pathname);
					//The File.Delete command returns before the OS has actually removed the file,
					//this causes re-creation of the file to fail intermittently so we'll wait a bit for it to be gone.
					for (var i = 0; File.Exists(pathname) && i < 5; ++i)
					{
						Thread.Sleep(10);
					}
				}
				catch (IOException)
				{
					// Don't crash, fail the assert if we couldn't delete the file
				}
				// We want to assert during the setup of test conditions because the test isn't valid if we don't start clean
				// If we fail to delete the files after the test (beause the OS hangs on to the handle too long for instance)
				//  this is not cause to fail the test.
				if (assertThatDbWasDeleted)
					Assert.That(!File.Exists(pathname), "Database file failed to be deleted.");
			}
		}

		#endregion Non-test methods.

		#region Tests
		/// <summary>
		/// Make sure each BEP type migrates to all other BEP types,
		/// including memory only just to be complete.
		///
		/// This test uses an already opened BEP for the source,
		/// so it tests the BEP method that accepts the source LcmCache
		/// and creates a new target.
		/// </summary>
		[Test]
		[Combinatorial]
		public void PortAllBEPsTestsUsingAnAlreadyOpenedSource(
			[Values(BackendProviderType.kXMLWithMemoryOnlyWsMgr, BackendProviderType.kMemoryOnly, BackendProviderType.kCRDTWithMemoryOnlyWsMgr)]
			BackendProviderType sourceType,
			[Values(BackendProviderType.kXMLWithMemoryOnlyWsMgr, BackendProviderType.kMemoryOnly)]
			BackendProviderType targetType)
		{
			var sourceBackendStartupParameters = GenerateBackendStartupParameters(false, sourceType);
			var targetBackendStartupParameters = GenerateBackendStartupParameters(true, targetType);

			// Set up data source, but only do it once.
			var sourceGuids = new List<Guid>();
			var sourceProjectId = new TestProjectId(sourceBackendStartupParameters.ProjectId.Type,
				sourceBackendStartupParameters.ProjectId.Path);
			using (var sourceCache = LcmCache.CreateCacheWithNewBlankLangProj(sourceProjectId, "en", "fr", "en", new DummyLcmUI(),
				m_lcmDirectories, new LcmSettings()))
			{
				// BEP is a singleton, so we shouldn't call Dispose on it. This will be done
				// by service locator.
				var sourceDataSetup = GetMainBEPInterface(sourceCache);
				// The source is created ex nihilo.
				sourceDataSetup.LoadDomain(sourceBackendStartupParameters.BulkLoadDomain);
				sourceGuids.AddRange(GetAllCmObjects(sourceCache).Select(obj => obj.Guid)); // Collect all source Guids

				DeleteDatabase(targetBackendStartupParameters);

				// Migrate source data to new BEP.
				var targetProjectId = new TestProjectId(targetBackendStartupParameters.ProjectId.Type,
					targetBackendStartupParameters.ProjectId.Path);
				using (var targetCache = LcmCache.CreateCacheCopy(targetProjectId, "en", new DummyLcmUI(),
					m_lcmDirectories, new LcmSettings(), sourceCache))
				{
					// BEP is a singleton, so we shouldn't call Dispose on it. This will be done
					// by service locator.
					var targetDataSetup = GetMainBEPInterface(targetCache);
					targetDataSetup.LoadDomain(BackendBulkLoadDomain.All);

					CompareResults(sourceGuids, targetCache);
				}
			}
		}

		/// <summary>
		/// Make sure each BEP type migrates to all other BEP types,
		/// including memory only just to be complete.
		///
		/// This test uses an un-opened BEP for the source,
		/// so it tests the BEP method that starts up the source and creates the target.
		/// </summary>
		/// <remarks>
		/// The Memory only source BEP can't tested here, since it can't be deleted, created,
		/// and restarted which is required of all source BEPs in this test.
		/// The source memory BEP is tested in 'PortAllBEPsTestsUsingAnAlreadyOpenedSource',
		/// since source BEPs are only created once and the open connection is reused for
		/// all targets.</remarks>
		[Test]
		[Combinatorial]
		public void PortAllBEPsTestsUsingAnUnopenedSource(
			[Values(BackendProviderType.kXMLWithMemoryOnlyWsMgr, BackendProviderType.kCRDTWithMemoryOnlyWsMgr)]
			BackendProviderType sourceType,
			[Values(BackendProviderType.kXMLWithMemoryOnlyWsMgr, BackendProviderType.kMemoryOnly)]
			BackendProviderType targetType)
		{
			var path = Path.Combine(Path.GetTempPath(), "FieldWorksTest");
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
			var sourceBackendStartupParameters = GenerateBackendStartupParameters(false, sourceType);
			var targetBackendStartupParameters = GenerateBackendStartupParameters(true, targetType);

			var sourceGuids = new List<Guid>();

			// Set up data source
			var sourceProjectId = new TestProjectId(sourceBackendStartupParameters.ProjectId.Type,
				sourceBackendStartupParameters.ProjectId.Path);
			using (LcmCache sourceCache = LcmCache.CreateCacheWithNewBlankLangProj(sourceProjectId, "en", "fr", "en", new DummyLcmUI(),
				m_lcmDirectories, new LcmSettings()))
			{
				// BEP is a singleton, so we shouldn't call Dispose on it. This will be done
				// by service locator.
				var sourceDataSetup = GetMainBEPInterface(sourceCache);
				sourceCache.ServiceLocator.GetInstance<IUndoStackManager>().Save(); // persist the new db so we can reopen it.
				sourceDataSetup.LoadDomain(BackendBulkLoadDomain.All);
				sourceGuids.AddRange(GetAllCmObjects(sourceCache).Select(obj => obj.Guid)); // Collect all source Guids
			}

			// Migrate source data to new BEP.
			IThreadedProgress progressDlg = new DummyProgressDlg();
			var targetProjectId = new TestProjectId(targetBackendStartupParameters.ProjectId.Type, null);
			using (var targetCache = LcmCache.CreateCacheWithNoLangProj(targetProjectId, "en", new DummyLcmUI(),
				m_lcmDirectories, new LcmSettings()))
			{
				// BEP is a singleton, so we shouldn't call Dispose on it. This will be done
				// by service locator.
				var targetDataSetup = GetMainBEPInterface(targetCache);
				targetDataSetup.InitializeFromSource(new TestProjectId(targetBackendStartupParameters.ProjectId.Type,
					targetBackendStartupParameters.ProjectId.Path), sourceBackendStartupParameters, "en", progressDlg);
				targetDataSetup.LoadDomain(BackendBulkLoadDomain.All);
				CompareResults(sourceGuids, targetCache);
			}
			sourceGuids.Clear();
		}
		#endregion
	}
}
