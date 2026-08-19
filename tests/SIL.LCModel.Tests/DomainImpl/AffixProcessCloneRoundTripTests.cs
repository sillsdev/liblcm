// Copyright (c) 2026 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.IO;
using NUnit.Framework;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Infrastructure;
using SIL.TestUtilities;

namespace SIL.LCModel.DomainImpl
{
	/// <summary>
	/// Case 5 from doc/bugs/affix-process-split-sense-stale-clone.md: a save/reload round trip
	/// after LexEntry.MoveSenseToCopy, to check whether the affix-process clone bug is only an
	/// in-memory artifact (masked by a warm cache) or actually persists to (and is reproduced
	/// from) disk -- which is what the user's reported "comes back wrong later" symptom requires.
	/// This uses a real file-backed cache (kXMLWithMemoryOnlyWsMgr), unlike the in-memory-only
	/// fixture used by LexEntryTests.
	/// </summary>
	[TestFixture]
	public class AffixProcessCloneRoundTripTests
	{
		private TemporaryFolder m_projectsFolder;
		private ILcmDirectories m_lcmDirectories;

		/// <summary />
		[SetUp]
		public void TestSetup()
		{
			m_projectsFolder = new TemporaryFolder("AffixProcessCloneRoundTrip" + Guid.NewGuid().ToString("N"));
			m_lcmDirectories = new TestLcmDirectories(m_projectsFolder.Path);
		}

		/// <summary />
		[TearDown]
		public void TestTeardown()
		{
			m_projectsFolder.Dispose();
		}

		/// <summary>
		/// Build an entry with a non-trivial affix-process LexemeFormOA and two senses, split one
		/// sense off with MoveSenseToCopy, save to disk, close the cache, reopen it from disk, and
		/// re-examine the cloned affix process. If PostClone's repair is only cosmetically correct
		/// in memory (e.g. because a UI slice renders the leaked default identically to real
		/// content until a reload forces a rebuild), this is the test that would catch it; if the
		/// in-memory clone is already wrong, this test proves the corruption is not transient.
		/// </summary>
		[Test]
		public void MoveSenseToCopy_AffixProcessClone_SurvivesSaveAndReload()
		{
			var projectName = "AffixProcessCloneRoundTrip" + new Random().Next(1000000);
			var path = Path.Combine(m_projectsFolder.Path, LcmFileHelper.GetXmlDataFileName(projectName));
			var projectId = new TestProjectId(BackendProviderType.kXMLWithMemoryOnlyWsMgr, path);

			Guid newEntryGuid;
			int expectedInputCount = 0;
			int expectedOutputCount = 0;

			using (var cache = LcmCache.CreateCacheWithNewBlankLangProj(projectId, "en", "fr", "en",
				new DummyLcmUI(), m_lcmDirectories, new LcmSettings()))
			{
				ILexEntry entry = null;
				ILexSense senseToMove = null;
				UndoableUnitOfWorkHelper.Do("doit", "undoit", cache.ActionHandlerAccessor, () =>
				{
					var ws = cache.DefaultVernWs;
					var entryFactory = cache.ServiceLocator.GetInstance<ILexEntryFactory>();
					var senseFactory = cache.ServiceLocator.GetInstance<ILexSenseFactory>();

					entry = entryFactory.Create();
					var process = cache.ServiceLocator.GetInstance<IMoAffixProcessFactory>().Create();
					entry.LexemeFormOA = process;
					process.Form.set_String(ws, TsStringUtils.MakeString("ed", ws));
					process.MorphTypeRA = cache.ServiceLocator.GetInstance<IMoMorphTypeRepository>()
						.GetObject(MoMorphTypeTags.kguidMorphSuffix);

					// Non-trivial rule: real content the user would have entered, replacing the
					// SetDefaultValuesAfterInit defaults.
					process.InputOS.Clear();
					process.OutputOS.Clear();
					var ctxt = cache.ServiceLocator.GetInstance<IPhSimpleContextNCFactory>().Create();
					process.InputOS.Add(ctxt);
					var var1 = cache.ServiceLocator.GetInstance<IPhVariableFactory>().Create();
					process.InputOS.Add(var1);
					var copy = cache.ServiceLocator.GetInstance<IMoCopyFromInputFactory>().Create();
					process.OutputOS.Add(copy);
					copy.ContentRA = ctxt;
					var modify = cache.ServiceLocator.GetInstance<IMoModifyFromInputFactory>().Create();
					process.OutputOS.Add(modify);
					modify.ContentRA = var1;

					expectedInputCount = process.InputOS.Count;
					expectedOutputCount = process.OutputOS.Count;

					var sense1 = senseFactory.Create();
					entry.SensesOS.Add(sense1);
					senseToMove = senseFactory.Create();
					entry.SensesOS.Add(senseToMove);
				});

				entry.MoveSenseToCopy(senseToMove);
				newEntryGuid = senseToMove.Entry.Guid;

				cache.ServiceLocator.GetInstance<IUndoStackManager>().Save();
			}

			using (var reloaded = LcmCache.CreateCacheFromExistingData(projectId, "en", new DummyLcmUI(),
				m_lcmDirectories, new LcmSettings(), new DummyProgressDlg()))
			{
				var newEntry = (ILexEntry)reloaded.ServiceLocator.GetObject(newEntryGuid);
				var clonedProcess = newEntry.LexemeFormOA as IMoAffixProcess;
				Assert.That(clonedProcess, Is.Not.Null, "reloaded clone should still be an affix process");

				Assert.That(clonedProcess.InputOS.Count, Is.EqualTo(expectedInputCount),
					"after save/reload, the clone's InputOS should match what was created, with no leaked " +
					"default and no real content lost");
				Assert.That(clonedProcess.OutputOS.Count, Is.EqualTo(expectedOutputCount),
					"after save/reload, the clone's OutputOS should match what was created, with no leaked " +
					"default and no real content lost");
				Assert.That(clonedProcess.InputOS[0].ClassID, Is.EqualTo(PhSimpleContextNCTags.kClassId),
					"first input after reload should be the real natural-class context, not a leaked default PhVariable");
				Assert.That(clonedProcess.InputOS[1].ClassID, Is.EqualTo(PhVariableTags.kClassId));
				Assert.That(clonedProcess.OutputOS[0].ClassID, Is.EqualTo(MoCopyFromInputTags.kClassId));
				Assert.That(clonedProcess.OutputOS[1].ClassID, Is.EqualTo(MoModifyFromInputTags.kClassId));
			}
		}
	}
}
