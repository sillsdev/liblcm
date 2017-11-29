﻿// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.DomainImpl;
using SIL.LCModel.DomainServices;
using LexEntry = SIL.LCModel.DomainImpl.LexEntry;
using LexSense = SIL.LCModel.DomainImpl.LexSense;
using ReversalIndexEntry = SIL.LCModel.DomainImpl.ReversalIndexEntry;
using RnGenericRec = SIL.LCModel.DomainImpl.RnGenericRec;

namespace SIL.LCModel.Infrastructure.Impl
{
	/// <summary>
	/// Tests that virtual property PropChanged notifications are sent when the properties they depend on are updated.
	/// </summary>
	[TestFixture]
	public class VirtualPropertyPropChangedTests : MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{
		private Notifiee m_notifiee;
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Override to end the undoable UOW started in the base TestSetup.
		/// Tests in this group need to manage their own UOWs.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public override void TestSetup()
		{
			base.TestSetup();

			m_actionHandler.EndUndoTask();
		}

		/// <summary>
		/// What it says.
		/// </summary>
		[Test]
		public void ShortNameTssDependsOnNameOfPossibility()
		{
			ICmPossibilityList acDomains = EnsureAcDomainsList();
			var domain = MakeAcDomain(acDomains, "a name");
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("set domain name", "redo", m_actionHandler,
										() =>
										{
											domain.Name.AnalysisDefaultWritingSystem =
												TsStringUtils.MakeString("new name", Cache.DefaultAnalWs);
										});
			CheckChange(CmPossibilityTags.kClassId, domain, "ShortNameTSS", 0, "new name".Length, "a name".Length, "Short name not notified when name changed");
		}

		/// <summary>
		/// Test of the simple cases of ReversalIndex.AllEntries getting PropChanged when Entries. changes.
		/// </summary>
		[Test]
		public void AllReversalEntriesDependsOnEntries()
		{
			var riFact = Cache.ServiceLocator.GetInstance<IReversalIndexFactory>();
			var rieFact = Cache.ServiceLocator.GetInstance<IReversalIndexEntryFactory>();
			IReversalIndex ri = null;
			IReversalIndexEntry rie1 = null;
			UndoableUnitOfWorkHelper.Do("make reversal index", "redo", m_actionHandler,
				() =>
				{
					ri = riFact.Create();
					Cache.LangProject.LexDbOA.ReversalIndexesOC.Add(ri);
				});
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("make reversal entry", "redo", m_actionHandler,
				() =>
					{
						rie1 = rieFact.Create();
						ri.EntriesOC.Add(rie1);
					});
			CheckChange(ReversalIndexTags.kClassId, ri, "AllEntries", 0, 1, 0, "No PropChanged on ReversalIndex.AllEntries when one created");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("delete reversal entry", "redo", m_actionHandler,
				() => ri.EntriesOC.Remove(rie1));
			CheckChange(ReversalIndexTags.kClassId, ri, "AllEntries", 0, 0, 0, "No PropChanged on ReversalIndex.AllEntries when one deleted");
		}

		/// <summary>
		/// Check that the proper PropChanged message is sent when a LexEntry is created.
		/// </summary>
		[Test]
		public void CreateEntrySendsPropChanged()
		{
			var fact = Cache.ServiceLocator.GetInstance<ILexEntryFactory>();
			var repo = Cache.ServiceLocator.GetInstance<ILexEntryRepository>();
			UndoableUnitOfWorkHelper.Do("set domain name", "redo", m_actionHandler,
				() => fact.Create());
			PrepareToTrackPropChanged();
			ILexEntry entry2 = null;
			UndoableUnitOfWorkHelper.Do("set domain name", "redo", m_actionHandler,
				() => entry2 = fact.Create());
			int index = repo.AllInstances().ToList().IndexOf(entry2);
			CheckChange(LexDbTags.kClassId, Cache.LangProject.LexDbOA, "Entries", index, 1, 0, "No PropChanged on Entries when one created");
		}
		/// <summary>
		/// Check that there is a change involving the specified dirtball, indicating a change to its property indentified
		/// by classId and fieldName.
		/// </summary>
		/// <param name="classId">Class of the dirtball</param>
		/// <param name="dirtball">The object that has been updated</param>
		/// <param name="fieldName">The field we expect that m_notifiee has been told has changed.</param>
		/// <param name="ivMin">The index where the change begins</param>
		/// <param name="cvIns">Number of things inserted (items from ivMin to ivMin + cvIns are new in the output)</param>
		/// <param name="cvDel">Number of things deleted (items frim ivMin to ivMin + cvDel from the original have been removed)</param>
		/// <param name="label">Message on error</param>
		private void CheckChange(int classId, ICmObject dirtball, string fieldName, int ivMin, int cvIns, int cvDel, string label)
		{
			var flid = Cache.MetaDataCacheAccessor.GetFieldId2(classId, fieldName, true);
			m_notifiee.CheckChange(new ChangeInformationTest(dirtball.Hvo, flid, ivMin, cvIns, cvDel), label);
		}

		private void PrepareToTrackPropChanged()
		{
			if (m_notifiee == null)
			{
				m_notifiee = new Notifiee();
				Cache.DomainDataByFlid.AddNotification(m_notifiee);
			}
			else
			{
				m_notifiee.ClearChanges();
			}
		}

		private ICmPossibilityList EnsureAcDomainsList()
		{
			var acDomains = Cache.LangProject.LexDbOA.DomainTypesOA;
			if (acDomains == null)
				UndoableUnitOfWorkHelper.Do("undo make ac dom list", "redo", m_actionHandler,
											()=>
												{
													acDomains = Cache.ServiceLocator.GetInstance<ICmPossibilityListFactory>().Create();
													Cache.LangProject.LexDbOA.DomainTypesOA = acDomains;
												});
			return acDomains;
		}

		private ICmPossibility MakeAcDomain(ICmPossibilityList acDomains, string name)
		{
			ICmPossibility domain = null;
			UndoableUnitOfWorkHelper.Do("undo make ac dom", "redo", m_actionHandler,
										() =>
										{
											domain = Cache.ServiceLocator.GetInstance<ICmPossibilityFactory>().Create();
											acDomains.PossibilitiesOS.Add(domain);
											domain.Name.AnalysisDefaultWritingSystem =
												TsStringUtils.MakeString(name, Cache.DefaultAnalWs);
										});
			return domain;
		}

		/// <summary>
		/// Updating MoForm.Form shoulld update the MlHeadword virtual property.
		/// </summary>
		[Test]
		public void MlHeadwordDependsOnMoFormForm()
		{
			var entry = MakeEntry("form", "gloss");
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo set form", "redo", m_actionHandler,
				() =>
				{
					entry.LexemeFormOA.Form.VernacularDefaultWritingSystem = TsStringUtils.MakeString("formX", Cache.DefaultVernWs);
				});
			CheckChange(LexEntryTags.kClassId, entry, "MLHeadWord", Cache.DefaultVernWs, 0, 0, "MlHeadword not updated when Form changed");
		}

		/// <summary>
		/// Updating LexEntry.CitationForm shoulld update the MlHeadword virtual property.
		/// </summary>
		[Test]
		public void MlHeadwordDependsOnCitationForm()
		{
			var entry = MakeEntry("form", "gloss");
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo set form", "redo", m_actionHandler,
				() =>
				{
					entry.CitationForm.VernacularDefaultWritingSystem = TsStringUtils.MakeString("formX", Cache.DefaultVernWs);
				});
			CheckChange(LexEntryTags.kClassId, entry, "MLHeadWord", Cache.DefaultVernWs, 0, 0, "MlHeadword not updated when Form changed");
		}

		/// <summary>
		/// CmSemanticDomain.ReferringSenses is updated when the SemanticDomains of the sense is changed.
		/// </summary>
		[Test]
		public void ReferringSensesDependsOnSenseDomains()
		{
			var entry = MakeEntry("form", "gloss");
			var semDomains = EnsureSemanticDomainsList();
			var domain = MakeSemDomain(semDomains, "happiness") as CmSemanticDomain;

			Assert.IsNotNull(domain, "the expected semantic domain object was created");
			Assert.AreEqual(0, domain.ReferringSenses.Count(),
				"nothing should refer to the semantic domain initially");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo set semantic domain", "redo", m_actionHandler,
										() =>
										{
											entry.SensesOS[0].SemanticDomainsRC.Add(domain);
										});
			CheckChange(CmSemanticDomainTags.kClassId, domain, "ReferringSenses", 0, 1, 0, "ReferringSenses not notified when sense domain added");

			Assert.AreEqual(1, domain.ReferringSenses.Count(),
				"one sense should be referring to the semantic domain after Add");
			Assert.AreSame(entry.SensesOS[0], domain.ReferringSenses.ToArray()[0],
				"the known sense should be referring to the semantic domain");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo remove semantic domain", "redo", m_actionHandler,
										() =>
										{
											entry.SensesOS[0].SemanticDomainsRC.Remove(domain);
										});
			// It's surprising that the number deleted is zero. However, the code that builds the virtual property change has no way to
			// know the old value, so just assumes it is empty.
			CheckChange(CmSemanticDomainTags.kClassId, domain, "ReferringSenses", 0, 0, 0, "ReferringSenses not notified when sense domain removed");

			Assert.AreEqual(0, domain.ReferringSenses.Count(),
				"nothing should refer to the semantic domain after Remove");
		}

		/// <summary>
		/// ReversalIndexEntry.ReferringSenses is updated when the ReversalEntries of the sense is changed.
		/// </summary>
		[Test]
		public void ReferringSensesDependOnReversalEntries()
		{
			var entry = MakeEntry("form", "gloss");
			var reversal = MakeReversalEntry("gloss") as ReversalIndexEntry;

			Assert.IsNotNull(reversal, "the expected reversal object was created");
			Assert.AreEqual(0, reversal.ReferringSenses.Count(),
				"nothing should refer to the reversal entry initially");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo set reversal entry", "redo", m_actionHandler,
				() =>
				{
					entry.SensesOS[0].ReversalEntriesRC.Add(reversal);
				});

			CheckChange(ReversalIndexEntryTags.kClassId, reversal, "ReferringSenses", 0, 1, 0, "ReferringSenses not notified when sense reversal added");

			Assert.AreEqual(1, reversal.ReferringSenses.Count(),
				"one sense should be referring to the reversal entry after Add");
			Assert.AreSame(entry.SensesOS[0], reversal.ReferringSenses.ToArray()[0],
				"the known sense should be referring to the reversal entry");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo remove reversal entry", "redo", m_actionHandler,
				() =>
				{
					entry.SensesOS[0].ReversalEntriesRC.Remove(reversal);
				});

			// It's surprising that the number deleted is zero. However, the code that builds the virtual property change has no way to
			// know the old value, so just assumes it is empty.
			CheckChange(ReversalIndexEntryTags.kClassId, reversal, "ReferringSenses", 0, 0, 0, "ReferringSenses not notified when sense reversal removed");

			Assert.AreEqual(0, reversal.ReferringSenses.Count(),
				"nothing should refer to the reversal entry after Remove");
		}

		/// <summary>
		/// Test that LexEntryRef.MorphoSyntaxAnalyses depends on LexEntry.Senses.
		/// </summary>
		[Test]
		public void MorphoSyntaxAnalysesDependsOnSenses()
		{
			var entryKickBucket = MakeEntry("kick the bucket", "die");
			var stemMsa = MakeEntryMorphSynAnalysis(entryKickBucket);
			var sense = (from s in entryKickBucket.SensesOS select s).First();
			var entryRef = MakeComplexFormLexEntryRef(entryKickBucket);
			Assert.That(entryRef.MorphoSyntaxAnalyses, Is.Empty, "The one sense has no MSA yet");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo set morphosyntacticananlysis", "redo", m_actionHandler,
										() =>
										{
											sense.MorphoSyntaxAnalysisRA = stemMsa;
										});
			Assert.That(entryRef.MorphoSyntaxAnalyses.ToList(), Has.Member(stemMsa).And.Count.EqualTo(1));
			CheckChange(LexEntryRefTags.kClassId, entryRef, "MorphoSyntaxAnalyses", 0, 1, 0,
						"MSA not added to sense.");

			var entry2 = MakeEntry("kick", "strike with foot");
			var stemMsa2 = MakeEntryMorphSynAnalysis(entry2);
			var sense2_1 = MakeSubSense(entry2.SensesOS[0], "perish");
			UndoableUnitOfWorkHelper.Do("undo set morphosyntacticananlysis", "redo", m_actionHandler,
									   () =>
									   {
										   sense2_1.MorphoSyntaxAnalysisRA = stemMsa2;
									   });

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo move sense", "redo", m_actionHandler,
										() =>
										{
											entryKickBucket.SensesOS.Add(sense2_1);
										});
			Assert.That(entryRef.MorphoSyntaxAnalyses.ToList(), Has.Member(stemMsa2).And.Count.EqualTo(2));
			CheckChange(LexEntryRefTags.kClassId, entryRef, "MorphoSyntaxAnalyses", 0, 2, 0,
						"New sense produces extra MSA in MorphoSyntaxAnalyses.");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo delete sense", "redo", m_actionHandler,
										() =>
										{
											entryKickBucket.SensesOS.Remove(sense2_1);
										});
			Assert.That(entryRef.MorphoSyntaxAnalyses.ToList(), Has.Member(stemMsa).And.Count.EqualTo(1));
			CheckChange(LexEntryRefTags.kClassId, entryRef, "MorphoSyntaxAnalyses", 0, 1, 0,
						"MSA not removed when deleting sense.");

		}

		private ILexEntry MakeEntry(string lf, string gloss)
		{
			ILexEntry entry = null;
			UndoableUnitOfWorkHelper.Do("undo make lexical entry", "redo", m_actionHandler,
										() =>
										{
											entry = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
											var form = Cache.ServiceLocator.GetInstance<IMoStemAllomorphFactory>().Create();
											entry.LexemeFormOA = form;
											form.Form.VernacularDefaultWritingSystem =
												TsStringUtils.MakeString(lf, Cache.DefaultVernWs);
											var sense = Cache.ServiceLocator.GetInstance<ILexSenseFactory>().Create();
											entry.SensesOS.Add(sense);
											sense.Gloss.AnalysisDefaultWritingSystem = TsStringUtils.MakeString(gloss, Cache.DefaultAnalWs);
										});
			return entry;
		}
		private ICmPossibilityList EnsureSemanticDomainsList()
		{
			var semDomains = Cache.LangProject.SemanticDomainListOA;
			if (semDomains == null)
				UndoableUnitOfWorkHelper.Do("undo make sem dom list", "redo", m_actionHandler,
											() =>
											{
												semDomains = Cache.ServiceLocator.GetInstance<ICmPossibilityListFactory>().Create();
												Cache.LangProject.SemanticDomainListOA = semDomains;
											});
			return semDomains;
		}
		private ICmSemanticDomain MakeSemDomain(ICmPossibilityList semDomains, string name)
		{
			ICmSemanticDomain domain = null;
			UndoableUnitOfWorkHelper.Do("undo make sem dom", "redo", m_actionHandler,
										() =>
										{
											domain = Cache.ServiceLocator.GetInstance<ICmSemanticDomainFactory>().Create();
											semDomains.PossibilitiesOS.Add(domain);
											domain.Name.AnalysisDefaultWritingSystem =
												TsStringUtils.MakeString(name, Cache.DefaultAnalWs);
										});
			return domain;
		}
		private ILexEntryRef MakeComplexFormLexEntryRef(ILexEntry ownerEntry)
		{
			ILexEntryRef result = null;
			UndoableUnitOfWorkHelper.Do("undo make ler", "redo", m_actionHandler,
										() =>
										{
											result = Cache.ServiceLocator.GetInstance<ILexEntryRefFactory>().Create();
											ownerEntry.EntryRefsOS.Add(result);
											result.RefType = LexEntryRefTags.krtComplexForm;
										});
			return result;
		}
		private IMoMorphSynAnalysis MakeEntryMorphSynAnalysis(ILexEntry ownerEntry)
		{
			IMoMorphSynAnalysis stemMsa = null;
			UndoableUnitOfWorkHelper.Do("undo make msa", "redo", m_actionHandler,
										() =>
										{
											stemMsa = Cache.ServiceLocator.GetInstance<IMoStemMsaFactory>().Create();
											ownerEntry.MorphoSyntaxAnalysesOC.Add(stemMsa);
										});
			return stemMsa;
		}
		private IReversalIndex EnsureReversalIndex()
		{
			if (Cache.LangProject.LexDbOA.ReversalIndexesOC.Count == 0)
			{
				UndoableUnitOfWorkHelper.Do("undo make reversal index", "redo make reversal index", m_actionHandler,
					() =>
					{
						IReversalIndex index = Cache.ServiceLocator.GetInstance<IReversalIndexFactory>().Create();
						Cache.LangProject.LexDbOA.ReversalIndexesOC.Add(index);
						index.WritingSystem = Cache.WritingSystemFactory.GetStrFromWs(Cache.DefaultAnalWs);
					});
			}
			return Cache.LangProject.LexDbOA.ReversalIndexesOC.ToArray()[0];
		}
		private IReversalIndexEntry MakeReversalEntry(string form)
		{
			IReversalIndex index = EnsureReversalIndex();
			IReversalIndexEntry entry = null;
			UndoableUnitOfWorkHelper.Do("undo make reversal entry", "redo make reversal entry", m_actionHandler,
				() =>
				{
					entry = Cache.ServiceLocator.GetInstance<IReversalIndexEntryFactory>().Create();
					index.EntriesOC.Add(entry);
					entry.ReversalForm.AnalysisDefaultWritingSystem = TsStringUtils.MakeString(form, Cache.DefaultAnalWs);
				});
			return entry;
		}

		/// <summary>
		/// A Replace on the target of a LexReference should result in the deletion of the lex reference,
		/// if it reduces the count of targets to one.
		/// (Per LT-15389: check in particular the case where more than one target is removed.)
		/// </summary>
		[Test]
		public void LexRefTargets_Replace_ShortensToOneItem_Deletes()
		{
			var hot = MakeEntry("hot", "high temp");
			var warm = MakeEntry("warm", "rather high temp");
			var boiling = MakeEntry("boiling", "very high temp");
			var molten = MakeEntry("molten", "extremely high temp");
			var refType = MakeLexRefType("synonym");
			var sut = MakeLexReference(refType, hot.SensesOS[0]);
			UndoableUnitOfWorkHelper.Do("setup", "redo", m_actionHandler,
				() =>
				{
					sut.TargetsRS.Add(warm.SensesOS[0]);
					sut.TargetsRS.Add(boiling.SensesOS[0]);
					sut.TargetsRS.Add(molten.SensesOS[0]);
					PrepareToTrackPropChanged();
				});
			UndoableUnitOfWorkHelper.Do("undo make kick a component of kickBucket", "redo", m_actionHandler,
				() => sut.TargetsRS.Replace(0, 4, new[] {hot.SensesOS[0]}));
			Assert.That(sut.IsValidObject, Is.False, "reducing length of targets to 1 should have deleted Lex Ref");
			CheckChange(LexSenseTags.kClassId, hot.SensesOS[0], "MinimalLexReferences", 0, 1, 0,
				"changing Targets of LexReference should update MinimalLexReferences of targets");
			CheckChange(LexSenseTags.kClassId, warm.SensesOS[0], "MinimalLexReferences", 0, 0, 0,
						"Removing target from should update MinimalLexReferences of removed target");
			CheckChange(LexSenseTags.kClassId, boiling.SensesOS[0], "MinimalLexReferences", 0, 0, 0,
						"Removing target from should update MinimalLexReferences of removed target");
			CheckChange(LexSenseTags.kClassId, molten.SensesOS[0], "MinimalLexReferences", 0, 0, 0,
						"Removing target from should update MinimalLexReferences of removed target");
		}

		/// <summary>
		/// Tests (one of) the dependencies of LexEntry.VisibleComplexFormBackRefs and VisibleCompleFormEntries.
		/// </summary>
		[Test]
		public void VisibleComplexFormBackRefsDependsOnShowComplexFormIn()
		{
			var kick = MakeEntry("kick", "strike with foot") as LexEntry;
			var kickBucket = MakeEntry("kick the bucket", "die");
			var ler = MakeComplexFormLexEntryRef(kickBucket);

			Assert.IsNotNull(kick, "the expected entry kick object was created");
			Assert.AreEqual(0, kick.VisibleComplexFormBackRefs.Count(),
				"nothing should refer to the entry kick initially");
			Assert.AreEqual(0, kick.ComplexFormsNotSubentries.Count(),
				"no ComplexFormsNotSubentries initially");
			Assert.AreEqual(0, kick.VisibleComplexFormEntries.Count(),
				"absolutely nothing should refer to the entry kick initially");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo make kick a component of kickBucket", "redo", m_actionHandler,
										() =>
										{
											ler.ComponentLexemesRS.Add(kick);
											ler.ShowComplexFormsInRS.Add(kick);
										});
			CheckChange(LexEntryTags.kClassId, kick, "VisibleComplexFormBackRefs", 0, 1, 0,
				"Making a lex entry ref point at a lex entry should change its VisibleComplexFormBackRefs");
			Assert.AreEqual(1, kick.VisibleComplexFormBackRefs.Count(),
				"the entry kick should now refer to one item in VisibleComplexFormBackRefs");
			Assert.AreSame(ler, kick.VisibleComplexFormBackRefs.ToArray()[0],
				"the entry kick should refer to the lex entry reference in VisibleComplexFormBackRefs");

			CheckChange(LexEntryTags.kClassId, kick, "ComplexFormsNotSubentries", 0, 1, 0,
				"Making a lex entry ref point at a lex entry should change its ComplexFormsNotSubentries");
			Assert.AreEqual(1, kick.ComplexFormsNotSubentries.Count(),
				"the entry kick should now refer to one item in ComplexFormsNotSubentries");
			Assert.AreSame(ler, kick.ComplexFormsNotSubentries.First(),
				"the entry kick should refer to the lex entry reference in ComplexFormsNotSubentries");

			Assert.AreEqual(1, kick.VisibleComplexFormEntries.Count(),
				"the entry kick should now refer to one item in VisibleComplexFormBackRefs");
			Assert.AreSame(kickBucket, kick.VisibleComplexFormEntries.ToArray()[0],
				"the entry kick should refer to the complex form in VisibleComplexFormEntries");
			CheckChange(LexEntryTags.kClassId, kick, "VisibleComplexFormEntries", 0, 1, 0,
				"Making a lex entry ref point at a lex entry should change its VisibleComplexFormEntries");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo add kick to primary lexemes of kickBucket", "redo", m_actionHandler,
										() =>
										{
											ler.PrimaryLexemesRS.Add(kick);
										});
			CheckChange(LexEntryTags.kClassId, kick, "ComplexFormsNotSubentries", 0, 0, 0, // as above, delete count is inaccurate.
				"Adding an entry to PrimaryLexemes should change ComplexFormsNotSubentries");
			Assert.AreEqual(0, kick.ComplexFormsNotSubentries.Count(),
				"Adding an entry to PrimaryLexemes should remove it from ComplexFormsNotSubentries");
			Assert.AreEqual(1, kick.VisibleComplexFormEntries.Count(),
				"changes to PrimaryLexemes should not affect VisibleComplexFormBackRefs");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo remove kick from primary lexemes of kickBucket", "redo", m_actionHandler,
										() =>
										{
											ler.PrimaryLexemesRS.Remove(kick);
										});
			CheckChange(LexEntryTags.kClassId, kick, "ComplexFormsNotSubentries", 0, 1, 0,
				"Removing an entry from primaryLexemes should change its ComplexFormsNotSubentries");
			Assert.AreEqual(1, kick.ComplexFormsNotSubentries.Count(),
				"Removing an entry from primaryLexemes should make it refer to one item in ComplexFormsNotSubentries");
			Assert.AreSame(ler, kick.ComplexFormsNotSubentries.First(),
				"the entry kick should refer to the lex entry reference in ComplexFormsNotSubentries");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo remove kick from components of kickBucket", "redo", m_actionHandler,
										() =>
										{
											ler.ShowComplexFormsInRS.Remove(kick);
										});
			CheckChange(LexEntryTags.kClassId, kick, "VisibleComplexFormBackRefs", 0, 0, 0, // as above, delete count is inaccurate.
				"Removing a lex entry from a lex entry ref pointing at it should change its VisibleComplexFormBackRefs");
			Assert.AreEqual(0, kick.VisibleComplexFormBackRefs.Count(),
				"nothing should refer to the entry kick after remove kick");

			CheckChange(LexEntryTags.kClassId, kick, "ComplexFormsNotSubentries", 0, 0, 0, // as above, delete count is inaccurate.
				"Removing a lex entry from a lex entry ref pointing at it should change its ComplexFormsNotSubentries");
			Assert.AreEqual(0, kick.ComplexFormsNotSubentries.Count(),
				"nothing should refer to the entry kick after remove kick");

			Assert.AreEqual(0, kick.VisibleComplexFormEntries.Count(),
				"the entry kick should be back to no VisibleComplexFormEntries");
			CheckChange(LexEntryTags.kClassId, kick, "VisibleComplexFormEntries", 0, 0, 0,
				"Removing a lex entry ref pointing at a lex entry should change its VisibleComplexFormEntries");

			PrepareToTrackPropChanged();
			LexSense sense = kick.SensesOS[0] as LexSense;
			Assert.IsNotNull(sense, "the expected underlying sense type was created");

			UndoableUnitOfWorkHelper.Do("undo make kick's first sense a component of kickBucket", "redo", m_actionHandler,
										() =>
										{
											ler.ShowComplexFormsInRS.Add(sense);
										});
			CheckChange(LexSenseTags.kClassId, sense, "VisibleComplexFormBackRefs", 0, 1, 0,
				"Making a lex entry ref point at a lex sense should change the sense's VisibleComplexFormBackRefs");
			Assert.AreEqual(1, sense.VisibleComplexFormBackRefs.Count(),
				"the sense should now refer to one item in VisibleComplexFormBackRefs");
			Assert.AreSame(ler, sense.VisibleComplexFormBackRefs.ToArray()[0],
				"the sense should refer to the lex entry reference in VisibleComplexFormBackRefs");
			Assert.AreEqual(0, kick.VisibleComplexFormBackRefs.Count(),
				"nothing should refer to the entry kick after adding sense");

			CheckChange(LexSenseTags.kClassId, sense, "ComplexFormsNotSubentries", 0, 1, 0,
				"Making a lex entry ref point at a lex sense should change the sense's ComplexFormsNotSubentries");
			Assert.AreEqual(1, sense.ComplexFormsNotSubentries.Count(),
				"the sense should now refer to one item in ComplexFormsNotSubentries");
			Assert.AreSame(ler, sense.ComplexFormsNotSubentries.ToArray()[0],
				"the sense should refer to the lex entry reference in ComplexFormsNotSubentries");
			Assert.AreEqual(0, kick.ComplexFormsNotSubentries.Count(),
				"nothing should refer to the entry kick after adding sense");

			Assert.AreEqual(1, sense.VisibleComplexFormEntries.Count(),
				"the entry kick should now refer to one item in VisibleComplexFormBackRefs");
			Assert.AreSame(kickBucket, sense.VisibleComplexFormEntries.ToArray()[0],
				"the entry kick should refer to the complex form in VisibleComplexFormEntries");
			CheckChange(LexSenseTags.kClassId, sense, "VisibleComplexFormEntries", 0, 1, 0,
				"Making a lex entry ref point at a lex sense should change its VisibleComplexFormEntries");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo add sense to primary lexemes of kickBucket", "redo", m_actionHandler,
										() =>
										{
											ler.PrimaryLexemesRS.Add(sense);
										});
			CheckChange(LexSenseTags.kClassId, sense, "ComplexFormsNotSubentries", 0, 0, 0, // as above, delete count is inaccurate.
				"Adding a sense to PrimaryLexemes should change ComplexFormsNotSubentries");
			Assert.AreEqual(0, sense.ComplexFormsNotSubentries.Count(),
				"Adding a sense to PrimaryLexemes should remove it from ComplexFormsNotSubentries");
			Assert.AreEqual(1, sense.VisibleComplexFormEntries.Count(),
				"changes to PrimaryLexemes should not affect VisibleComplexFormBackRefs");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo remove sense from primary lexemes of kickBucket", "redo", m_actionHandler,
										() =>
										{
											ler.PrimaryLexemesRS.Remove(sense);
										});
			CheckChange(LexSenseTags.kClassId, sense, "ComplexFormsNotSubentries", 0, 1, 0,
				"Removing a sense from primaryLexemes should change its ComplexFormsNotSubentries");
			Assert.AreEqual(1, sense.ComplexFormsNotSubentries.Count(),
				"Removing a sense from primaryLexemes should make it refer to one item in ComplexFormsNotSubentries");
			Assert.AreSame(ler, sense.ComplexFormsNotSubentries.First(),
				"the sense should refer to the lex entry reference in ComplexFormsNotSubentries");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo remove kick from components of kickBucket", "redo", m_actionHandler,
										() =>
										{
											ler.ShowComplexFormsInRS.Remove(sense);
										});
			CheckChange(LexSenseTags.kClassId, sense, "VisibleComplexFormBackRefs", 0, 0, 0, // as above, delete count is inaccurate.
				"Removing a lex sense from a lex entry ref pointing at it should change its VisibleComplexFormBackRefs");
			Assert.AreEqual(0, sense.VisibleComplexFormBackRefs.Count(),
				"nothing should refer to the sense after removing it");
			Assert.AreEqual(0, kick.VisibleComplexFormBackRefs.Count(),
				"nothing should still refer to the entry kick after removing sense");
			Assert.AreEqual(0, sense.VisibleComplexFormEntries.Count(),
				"the entry kick should be back to no VisibleComplexFormBackRefs");

			CheckChange(LexSenseTags.kClassId, sense, "ComplexFormsNotSubentries", 0, 0, 0, // as above, delete count is inaccurate.
				"Removing a lex sense from a lex entry ref pointing at it should change its ComplexFormsNotSubentries");
			Assert.AreEqual(0, sense.ComplexFormsNotSubentries.Count(),
				"nothing should refer to the sense after removing it");
			Assert.AreEqual(0, kick.ComplexFormsNotSubentries.Count(),
				"nothing should still refer to the entry kick after removing sense");
			Assert.AreEqual(0, sense.ComplexFormsNotSubentries.Count(),
				"the entry kick should be back to no VisibleComplexFormBackRefs");

			CheckChange(LexSenseTags.kClassId, sense, "VisibleComplexFormEntries", 0, 0, 0,
				"Removing a lex entry ref pointing at a lex sense should change its VisibleComplexFormEntries");
		}

		/// <summary>
		/// Tests (one of) the dependencies of LexEntry.AllSubentries.
		/// </summary>
		[Test]
		public void AllSubEntriesDependsOnPrimaryLexemes()
		{
			var kick = MakeEntry("kick", "strike with foot") as LexEntry;
			var kickBucket = MakeEntry("kick the bucket", "die");
			var ler = MakeComplexFormLexEntryRef(kickBucket);

			Assert.IsNotNull(kick, "the expected entry kick object was created");
			Assert.AreEqual(0, kick.AllSubentries.Count(),
				"absolutely nothing should refer to the entry kick initially");
			Assert.AreEqual(0, kick.Subentries.Count(), "no subentries initially");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo make kick a component of kickBucket", "redo", m_actionHandler,
										() =>
										{
											ler.PrimaryLexemesRS.Add(kick);
										});
			CheckChange(LexEntryTags.kClassId, kick, "AllSubentries", 0, 1, 0,
				"Making a lex entry ref point at a lex entry should change its AllSubentries");
			CheckChange(LexEntryTags.kClassId, kick, "Subentries", 0, 1, 0,
				"Making a lex entry ref point at a lex entry should change its Subentries");
			Assert.AreEqual(1, kick.AllSubentries.Count(),
				"the entry kick should now refer to one item in AllSubentries");
			Assert.AreEqual(1, kick.Subentries.Count(), "one subentry after adding");
			Assert.AreSame(ler, kick.AllSubentries.ToArray()[0],
				"the entry kick should refer to the lex entry reference in AllSubentries");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo remove kick from components of kickBucket", "redo", m_actionHandler,
										() =>
										{
											ler.PrimaryLexemesRS.Remove(kick);
										});
			CheckChange(LexEntryTags.kClassId, kick, "AllSubentries", 0, 0, 0, // as above, delete count is inaccurate.
				"Removing a lex entry from a lex entry ref pointing at it should change its AllSubentries");
			CheckChange(LexEntryTags.kClassId, kick, "Subentries", 0, 0, 0, // as above, delete count is inaccurate.
				"Removing a lex entry from a lex entry ref pointing at it should change its AllSubentries");
			Assert.AreEqual(0, kick.AllSubentries.Count(),
				"absolutely nothing should refer to the entry kick after remove kick");
			Assert.AreEqual(0, kick.Subentries.Count(), "no subentries after removing entry");

			PrepareToTrackPropChanged();
			LexSense sense = kick.SensesOS[0] as LexSense;
			Assert.IsNotNull(sense, "the expected underlying sense type was created");

			UndoableUnitOfWorkHelper.Do("undo make kick's first sense a component of kickBucket", "redo", m_actionHandler,
										() =>
										{
											ler.PrimaryLexemesRS.Add(sense);
										});
			Assert.AreEqual(1, kick.AllSubentries.Count(),
				"the entry should refer to one item in AllSubentries after adding sense");
			Assert.AreSame(ler, kick.AllSubentries.ToArray()[0],
				"the entry kick should refer to the lex entry reference in AllSubentries after adding sense");
			Assert.AreEqual(0, kick.Subentries.Count(), "entry subentries is not changed by primarylexemes pointing at sense");
			Assert.AreEqual(1, sense.Subentries.Count(), "sense subentries changed by making it one");
			CheckChange(LexEntryTags.kClassId, kick, "AllSubentries", 0, 1, 0,
				"Making a lex entry ref point at a lex sense should change the AllSubentries of its entry");
			CheckChange(LexSenseTags.kClassId, sense, "Subentries", 0, 1, 0,
				"Making a lex entry ref point at a lex sense should change its Subentries");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo remove kick from components of kickBucket", "redo", m_actionHandler,
										() =>
										{
											ler.PrimaryLexemesRS.Remove(sense);
										});
			Assert.AreEqual(0, kick.AllSubentries.Count(),
				"absolutely nothing should refer to the entry kick after removing sense");
			Assert.AreEqual(0, sense.Subentries.Count(), "sense subentries returned to empty");
			CheckChange(LexEntryTags.kClassId, kick, "AllSubentries", 0, 0, 0,
				"Making a lex entry ref stop pointing at a lex sense should change the AllSubentries of its entry");
			CheckChange(LexSenseTags.kClassId, sense, "Subentries", 0, 0, 0,
				"Making a lex entry ref stop pointing at a lex sense should change its Subentries");
		}

		/// <summary>
		/// LexEntry.PicturesOfSenses should be refreshed when LexSense.Pictures changes.
		/// </summary>
		[Test]
		public void PicturesOfSensesDependsOnPictures()
		{
			var entry = MakeEntry("snail", "creepy thing");
			var sense = entry.SensesOS[0];
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo add picture to sense", "redo", m_actionHandler,
										() =>
										{
											var picture = Cache.ServiceLocator.GetInstance<ICmPictureFactory>().Create();
											sense.PicturesOS.Add(picture);
										});
			CheckChange(LexEntryTags.kClassId, entry, "PicturesOfSenses", 0, 1, 0,
						"Adding picture to sense should update PicturesOfSenses");
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo remove picture from sense", "redo", m_actionHandler,
										() =>
										{
											sense.PicturesOS.RemoveAt(0);
										});
			CheckChange(LexEntryTags.kClassId, entry, "PicturesOfSenses", 0, 0, 0, // as above, delete count is inaccurate.
						"Removing picture from sense should update PicturesOfSenses");
		}

		private ILexReference MakeLexReference(ILexRefType owner, ICmObject firstTarget)
		{
			ILexReference result = null;
			UndoableUnitOfWorkHelper.Do("undo make ler", "redo", m_actionHandler,
				() =>
					{
						result = Cache.ServiceLocator.GetInstance<ILexReferenceFactory>().Create();
						owner.MembersOC.Add(result);
						result.TargetsRS.Add(firstTarget);
					});
			return result;
		}

		private ILexRefType MakeLexRefType(string name)
		{
			ILexRefType result = null;
			UndoableUnitOfWorkHelper.Do("undo make let", "redo", m_actionHandler,
				() =>
				{
					if (Cache.LangProject.LexDbOA.ReferencesOA == null)
						Cache.LangProject.LexDbOA.ReferencesOA = Cache.ServiceLocator.GetInstance<ICmPossibilityListFactory>().Create();
					result = Cache.ServiceLocator.GetInstance<ILexRefTypeFactory>().Create();
					Cache.LangProject.LexDbOA.ReferencesOA.PossibilitiesOS.Add(result);
					result.MappingType = (int)LexRefTypeTags.MappingTypes.kmtSenseSequence;
					result.Name.AnalysisDefaultWritingSystem = TsStringUtils.MakeString(name, Cache.DefaultAnalWs);
				});
			return result;

		}

		/// <summary>
		/// LexEntry.MinimalLexReferences depends on LexReference.Targets
		/// Also LexSense.MinimalLexReferences, LexSense.LexSenseReferences.
		/// </summary>
		[Test]
		public void VariousPropsDependOnLexRefTargets()
		{
			var entry1 = MakeEntry("kick", "strike with foot");
			var entry2 = MakeEntry("kick the bucket", "die");
			var sense1 = entry1.SensesOS[0];
			var sense2 = entry2.SensesOS[0];
			var lexRefType = MakeLexRefType("TestRelation");
			var lexRef = MakeLexReference(lexRefType, entry1);
			Assert.AreEqual(1, (entry1 as LexEntry).MinimalLexReferences.Count,
				"entry1 has one MinimalLexReference after creating LexReference with entry1");
			Assert.AreEqual(0, (sense1 as LexSense).MinimalLexReferences.Count,
				"sense1 has no MinimalLexReferences after creating LexReference with entry1");
			Assert.AreEqual(0, (entry2 as LexEntry).MinimalLexReferences.Count,
				"entry2 has no MinimalLexReferences after creating LexReference with entry1");
			Assert.AreEqual(0, (sense2 as LexSense).MinimalLexReferences.Count,
				"sense2 has no MinimalLexReferences after creating LexReference with entry1");


			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo add target", "redo", m_actionHandler,
				() =>
				{
					lexRef.TargetsRS.Add(entry2);
				});
			CheckChange(LexEntryTags.kClassId, entry1, "MinimalLexReferences", 0, 1, 0,
						"Adding target to LexReference should update MinimalLexReferences of existing target");
			CheckChange(LexEntryTags.kClassId, entry2, "MinimalLexReferences", 0, 1, 0,
						"Adding target to LexReference should update MinimalLexReferences of new target");
			Assert.AreEqual(1, (entry1 as LexEntry).MinimalLexReferences.Count,
				"entry1 still has one MinimalLexReference after adding entry2");
			Assert.AreEqual(0, (sense1 as LexSense).MinimalLexReferences.Count,
				"sense1 still has no MinimalLexReferences after adding entry2");
			Assert.AreEqual(1, (entry2 as LexEntry).MinimalLexReferences.Count,
				"entry2 has one MinimalLexReference after adding entry2");
			Assert.AreEqual(0, (sense2 as LexSense).MinimalLexReferences.Count,
				"sense2 still has no MinimalLexReferences after adding entry2");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo remove targer", "redo", m_actionHandler,
				() =>
				{
					lexRef.TargetsRS.Remove(entry2);
				});
			// The numbers added and removed here are strange, reflecting that the update code doesn't know the old
			// value of the virtual property and pretends it was previously empty.
			// When a LexReference has only two items in it, and one is removed, then the entire LexReference is deleted,
			// therefore the MinimalLexReferences.Count for each entry should be 0.
			CheckChange(LexEntryTags.kClassId, entry1, "MinimalLexReferences", 0, 0, 0,
						"Removing target from LexReference should update MinimalLexReferences of unchanged target");
			CheckChange(LexEntryTags.kClassId, entry2, "MinimalLexReferences", 0, 0, 0,
						"Removing target from should update MinimalLexReferences of removed target");
			Assert.AreEqual(0, (entry1 as LexEntry).MinimalLexReferences.Count,
				"entry1 has one MinimalLexReference after removing entry2");
			Assert.AreEqual(0, (sense1 as LexSense).MinimalLexReferences.Count,
				"sense1 has no MinimalLexReferences after removing entry2");
			Assert.AreEqual(0, (entry2 as LexEntry).MinimalLexReferences.Count,
				"entry2 has no MinimalLexReferences after removing entry2");
			Assert.AreEqual(0, (sense2 as LexSense).MinimalLexReferences.Count,
				"sense2 has no MinimalLexReferences after removing entry2");

			PrepareToTrackPropChanged();
			lexRef = MakeLexReference(lexRefType, entry1); //we need to create the LexReference again since the preceeding steps deleted it.
			UndoableUnitOfWorkHelper.Do("undo add sense target", "redo", m_actionHandler,
				() =>
				{
					lexRef.TargetsRS.Add(sense1);
				});
			CheckChange(LexEntryTags.kClassId, entry1, "MinimalLexReferences", 0, 1, 0,
						"Adding sense to LexReference should update MinimalLexReferences of existing target");
			CheckChange(LexSenseTags.kClassId, sense1, "MinimalLexReferences", 0, 1, 0,
						"Adding sense to LexReference should update MinimalLexReferences of new sense");
			CheckChange(LexSenseTags.kClassId, sense1, "LexSenseReferences", 0, 1, 0,
						"Adding sense to LexReference should update LexSenseReferences of new sense");
			Assert.AreEqual(1, (entry1 as LexEntry).MinimalLexReferences.Count,
				"entry1 has one MinimalLexReference after adding sense1");
			Assert.AreEqual(1, (sense1 as LexSense).MinimalLexReferences.Count,
				"sense1 has one MinimalLexReference after adding sense1");
			Assert.AreEqual(0, (entry2 as LexEntry).MinimalLexReferences.Count,
				"entry2 has no MinimalLexReferences after adding sense1");
			Assert.AreEqual(0, (sense2 as LexSense).MinimalLexReferences.Count,
				"sense2 has no MinimalLexReferences after adding sense1");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo add target", "redo", m_actionHandler,
				() =>
				{
					lexRef.TargetsRS.Add(sense2);
				});
			CheckChange(LexEntryTags.kClassId, entry1, "MinimalLexReferences", 0, 1, 0,
						"Adding sense to LexReference should update MinimalLexReferences of existing entry");
			CheckChange(LexSenseTags.kClassId, sense1, "MinimalLexReferences", 0, 1, 0,
						"Adding sense to LexReference should update MinimalLexReferences of existing sense");
			CheckChange(LexSenseTags.kClassId, sense2, "MinimalLexReferences", 0, 1, 0,
						"Adding sense to LexReference should update MinimalLexReferences of new sense");
			CheckChange(LexSenseTags.kClassId, sense2, "LexSenseReferences", 0, 1, 0,
						"Adding sense to LexReference should update LexSenseReferences of new sense");
			Assert.AreEqual(1, (entry1 as LexEntry).MinimalLexReferences.Count,
				"entry1 still has one MinimalLexReference after adding sense2");
			Assert.AreEqual(1, (sense1 as LexSense).MinimalLexReferences.Count,
				"sense1 still has one MinimalLexReference after adding sense2");
			Assert.AreEqual(0, (entry2 as LexEntry).MinimalLexReferences.Count,
				"entry2 still has no MinimalLexReferences after adding sense2");
			Assert.AreEqual(1, (sense2 as LexSense).MinimalLexReferences.Count,
				"sense2 has one MinimalLexReference after adding sense2");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo add target", "redo", m_actionHandler,
				() =>
				{
					lexRef.TargetsRS.Add(sense2);
				});

			//Previously two references were added this error has been fixed
			CheckChange(LexSenseTags.kClassId, sense2, "LexSenseReferences", 0, 1, 0,
						"Adding second sense from LexReference should update LexSenseReferences of added sense");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo remove target", "redo", m_actionHandler,
				() =>
				{
					lexRef.TargetsRS.Remove(sense2);
				});
			// The numbers added and removed here are strange, reflecting that the update code doesn't know the old
			// value of the virtual property and pretends it was previously empty.
			CheckChange(LexEntryTags.kClassId, entry1, "MinimalLexReferences", 0, 1, 0,
						"Removing sense from LexReference should update MinimalLexReferences of existing entry");
			CheckChange(LexSenseTags.kClassId, sense1, "MinimalLexReferences", 0, 1, 0,
						"Removing sense from LexReference should update MinimalLexReferences of a different sense that is now the only one");
			CheckChange(LexSenseTags.kClassId, sense2, "MinimalLexReferences", 0, 0, 0,
						"Removing sense from LexReference should update MinimalLexReferences of removed sense");
			CheckChange(LexSenseTags.kClassId, sense2, "LexSenseReferences", 0, 0, 0,
						"Removing sense from LexReference should update LexSenseReferences of removed sense");
			Assert.AreEqual(1, (entry1 as LexEntry).MinimalLexReferences.Count,
				"entry1 still has one MinimalLexReference after removing sense2");
			Assert.AreEqual(1, (sense1 as LexSense).MinimalLexReferences.Count,
				"sense1 still has one MinimalLexReference after removing sense2");
			Assert.AreEqual(0, (entry2 as LexEntry).MinimalLexReferences.Count,
				"entry2 has no MinimalLexReferences after removing sense2");
			Assert.AreEqual(0, (sense2 as LexSense).MinimalLexReferences.Count,
				"sense2 has no MinimalLexReferences after removing sense2");
		}

		private ILexSense MakeSubSense(ILexSense owner, string name)
		{
			ILexSense result = null;
			UndoableUnitOfWorkHelper.Do("undo make let", "redo", m_actionHandler,
				() =>
				{
					result = Cache.ServiceLocator.GetInstance<ILexSenseFactory>().Create();
					owner.SensesOS.Add(result);
					result.Gloss.AnalysisDefaultWritingSystem = TsStringUtils.MakeString(name, Cache.DefaultAnalWs);
				});
			return result;

		}
		/// <summary>
		/// MLOwnerOutlineName depends on anything that changes the headword or sense number!
		/// </summary>
		[Test]
		public void MLOwnerOutlineNameDependsOnManyThings()
		{
			var entry1 = MakeEntry("kick", "strike with foot");
			var sense1 = entry1.SensesOS[0];
			var sense2 = MakeSubSense(sense1, "kickA");

			// lexeme form
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo set LF", "redo", m_actionHandler,
				() => entry1.LexemeFormOA.Form.set_String(Cache.DefaultVernWs, TsStringUtils.MakeString("boot", Cache.DefaultVernWs)));
			CheckChange(LexSenseTags.kClassId, sense1, "MLOwnerOutlineName", Cache.DefaultVernWs, 0, 0,
						"Changing lexeme form should update MLOwnerOutlineName of sense");
			CheckChange(LexSenseTags.kClassId, sense2, "MLOwnerOutlineName", Cache.DefaultVernWs, 0, 0,
						"Changing lexeme form should update MLOwnerOutlineName of subsense");
			// citation form
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo set CF", "redo", m_actionHandler,
				() => entry1.CitationForm.set_String(Cache.DefaultVernWs, TsStringUtils.MakeString("bootA", Cache.DefaultVernWs)));
			CheckChange(LexSenseTags.kClassId, sense1, "MLOwnerOutlineName", Cache.DefaultVernWs, 0, 0,
						"Changing citation form should update MLOwnerOutlineName of sense");
			CheckChange(LexSenseTags.kClassId, sense2, "MLOwnerOutlineName", Cache.DefaultVernWs, 0, 0,
						"Changing citation form should update MLOwnerOutlineName of subsense");
			// sense number
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo insert sense", "redo", m_actionHandler,
				() =>
					{
						var newSense = Cache.ServiceLocator.GetInstance<ILexSenseFactory>().Create();
						entry1.SensesOS.Insert(0, newSense);
					});
			CheckChange(LexSenseTags.kClassId, sense1, "MLOwnerOutlineName", Cache.DefaultVernWs, 0, 0,
						"Inserting sense should update MLOwnerOutlineName of following sense");
			CheckChange(LexSenseTags.kClassId, sense2, "MLOwnerOutlineName", Cache.DefaultVernWs, 0, 0,
						"Inserting sense form should update MLOwnerOutlineName of subsense of following sense");
			m_actionHandler.Undo(); // inserting sense
			m_actionHandler.Undo(); // setting cf
			m_actionHandler.Undo(); // setting LF
			m_actionHandler.Undo(); // adding sense2
			// Inserting a sense AFTER causes a change to previously un-numbered (only) sense.
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo insert sense after", "redo", m_actionHandler,
				() =>
				{
					var newSense = Cache.ServiceLocator.GetInstance<ILexSenseFactory>().Create();
					entry1.SensesOS.Insert(1, newSense);
				});
			CheckChange(LexSenseTags.kClassId, sense1, "MLOwnerOutlineName", Cache.DefaultVernWs, 0, 0,
						"Inserting sense after should update MLOwnerOutlineName of unique sense");
		}

		/// <summary>
		/// RnGenericRecord.ShortNameTSS depends on Type, Title, and DateModified. DateModified is changed
		/// if anything is!
		/// Arguably we should update if someone renames the record type but that seems to rare to bother with.
		/// </summary>
		[Test]
		public void ShortNameTSSDependsOnVariousThings()
		{
			var record = MakeRnRecord("Someone goes fishing");
			PrepareToTrackPropChanged();
			WaitForTimeToChange(DateTime.Now);
			UndoableUnitOfWorkHelper.Do("undo change date modified", "redo", m_actionHandler,
				() =>
				{
					record.SubRecordsOS.Add(Cache.ServiceLocator.GetInstance<IRnGenericRecFactory>().Create());
				});
			var len = record.ShortNameTSS.Length;
			CheckChange(RnGenericRecTags.kClassId, record, "ShortNameTSS", 0, len, 0,
						"any change to generic record should update DateModified and therefore ShortNameTSS");
			PrepareToTrackPropChanged();
			WaitForTimeToChange(DateTime.Now);
			UndoableUnitOfWorkHelper.Do("undo change title", "redo", m_actionHandler,
				() =>
				{
					record.Title = TsStringUtils.MakeString("Joe goes fishing", Cache.DefaultAnalWs);
				});
			len = record.ShortNameTSS.Length;
			CheckChange(RnGenericRecTags.kClassId, record, "ShortNameTSS", 0, len, 0,
						"any change to generic record should update DateModified and therefore ShortNameTSS");

		}

		/// <summary>
		/// This test also checks the actual implementation of SubrecordOf.
		/// </summary>
		[Test]
		public void SubrecordOfDependsOnTitleAndPosition()
		{
			var record = (RnGenericRec)MakeRnRecord("Someone goes fishing");
			var subrecord = (RnGenericRec)MakeSubRecord(record, "Preparing the boat", 0);
			var subsubrecord = (RnGenericRec) MakeSubRecord(subrecord, "finding an anchor", 0);
			Assert.That(record.SubrecordOf.Length, Is.EqualTo(0));
			Assert.That(subrecord.SubrecordOf.Text, Is.EqualTo("1 of Someone goes fishing"));
			Assert.That(subsubrecord.SubrecordOf.Text, Is.EqualTo("1.1 of Someone goes fishing"));
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo change title", "redo", m_actionHandler,
				() =>
				{
					record.Title = TsStringUtils.MakeString("Joe goes fishing", Cache.DefaultAnalWs);
				});
			var len = subrecord.SubrecordOf.Length;
			CheckChange(RnGenericRecTags.kClassId, subrecord, "SubrecordOf", 0, len, 0,
						"changing the title of RnGenericRec should cause PropChanged on SubrecordOf in child");
			len = subsubrecord.SubrecordOf.Length;
			CheckChange(RnGenericRecTags.kClassId, subsubrecord, "SubrecordOf", 0, len, 0,
						"changing the title of RnGenericRec should cause PropChanged on SubrecordOf in grandchild");

			// Now try inserting a record.
			PrepareToTrackPropChanged();
			MakeSubRecord(record, "Preparing the rods", 0);
			len = subrecord.SubrecordOf.Length;
			CheckChange(RnGenericRecTags.kClassId, subrecord, "SubrecordOf", 0, len, 0,
						"inserting a record before a subrecord should cause PropChanged on SubrecordOf");
			len = subsubrecord.SubrecordOf.Length;
			CheckChange(RnGenericRecTags.kClassId, subsubrecord, "SubrecordOf", 0, len, 0,
						"inserting a record before a subrecord should cause PropChanged on SubrecordOf for descendant");
			Assert.That(subrecord.SubrecordOf.Text, Is.EqualTo("2 of Joe goes fishing"));
			Assert.That(subsubrecord.SubrecordOf.Text, Is.EqualTo("2.1 of Joe goes fishing"));
			// And deleting a record.
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo change title", "redo", m_actionHandler,
				() =>
				{
					record.SubRecordsOS.RemoveAt(0);
				});
			len = subrecord.SubrecordOf.Length;
			CheckChange(RnGenericRecTags.kClassId, subrecord, "SubrecordOf", 0, len, 0,
						"deleting a record before a subrecord should cause PropChanged on SubrecordOf");
			len = subsubrecord.SubrecordOf.Length;
			CheckChange(RnGenericRecTags.kClassId, subsubrecord, "SubrecordOf", 0, len, 0,
						"deleting a record before a subrecord should cause PropChanged on SubrecordOf for descendant");
			// Incidentally this is the only current test for the SubrecordOf virtual property.
			// Make sure that it will work with a format string in the opposite order.
			Assert.That(subrecord.FormatNumberOfParent("Child of {1} at {0}").Text,
				Is.EqualTo("Child of Joe goes fishing at 1"));
		}
		/// <summary>
		/// Busy-wait until we can be sure Now will be a later time than the input.
		/// </summary>
		/// <param name="old"></param>
		private void WaitForTimeToChange(DateTime old)
		{
			while (DateTime.Now == old)
			{ }
		}

		private IRnGenericRec MakeRnRecord(string title)
		{
			var typeList = EnsureRecTypesList();
			IRnGenericRec entry = null;
			UndoableUnitOfWorkHelper.Do("undo make record", "redo", m_actionHandler,
				() =>
					{
						entry = Cache.ServiceLocator.GetInstance<IRnGenericRecFactory>().Create();
						Cache.LangProject.ResearchNotebookOA.RecordsOC.Add(entry);
						entry.Title = TsStringUtils.MakeString(title, Cache.DefaultAnalWs);
						entry.TypeRA = typeList.PossibilitiesOS[0];
					});
			return entry;
		}

		private IRnGenericRec MakeSubRecord(IRnGenericRec parent, string title, int index)
		{
			var typeList = EnsureRecTypesList();
			IRnGenericRec entry = null;
			UndoableUnitOfWorkHelper.Do("undo make sub record", "redo", m_actionHandler,
				() =>
				{
					entry = Cache.ServiceLocator.GetInstance<IRnGenericRecFactory>().Create();
					parent.SubRecordsOS.Insert(index, entry);
					entry.Title = TsStringUtils.MakeString(title, Cache.DefaultAnalWs);
					entry.TypeRA = typeList.PossibilitiesOS[0];
				});
			return entry;
		}

		/// <summary>
		/// Ensure there is a record types possibility list with at least one type.
		/// </summary>
		/// <returns></returns>
		private ICmPossibilityList EnsureRecTypesList()
		{
			var recTypes = Cache.LangProject.ResearchNotebookOA.RecTypesOA;
			if (recTypes == null)
				UndoableUnitOfWorkHelper.Do("undo make rec types list", "redo", m_actionHandler,
					() =>
						{
							recTypes = Cache.ServiceLocator.GetInstance<ICmPossibilityListFactory>().Create();
							Cache.LangProject.ResearchNotebookOA.RecTypesOA = recTypes;
						});
			if (recTypes.PossibilitiesOS.Count == 0)
			{
				UndoableUnitOfWorkHelper.Do("undo make a rec type", "redo", m_actionHandler,
					() =>
					{
						var aType = Cache.ServiceLocator.GetInstance<ICmPossibilityFactory>().Create();
						recTypes.PossibilitiesOS.Add(aType);
						aType.Name.AnalysisDefaultWritingSystem = TsStringUtils.MakeString("test type", Cache.DefaultAnalWs);
					});
			}
			return recTypes;
		}

		/// <summary>
		/// The title of an StText depends on the title of the owning Text.
		/// (It depends on other things for Scripture texts...one day we may support that...but those change much less.)
		/// </summary>
		[Test]
		public void StTextTitleDependsOnTextTitle()
		{
			var text = MakeText("my text");
			var stText = text.ContentsOA;
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo change title", "redo", m_actionHandler,
				() =>
				{
					text.Name.AnalysisDefaultWritingSystem = TsStringUtils.MakeString("renamed", Cache.DefaultAnalWs);
				});
			var len = "renamed".Length;
			CheckChange(StTextTags.kClassId, stText, "Title", Cache.DefaultAnalWs, 0, 0,
						"changing title of Text should change Title of StText");
		}

		private IText MakeText(string title)
		{
			IText result = null;
			UndoableUnitOfWorkHelper.Do("undo make text", "redo", m_actionHandler,
				() =>
				{
					result = Cache.ServiceLocator.GetInstance<ITextFactory>().Create();
					//Cache.LangProject.TextsOC.Add(result);
					result.Name.AnalysisDefaultWritingSystem = TsStringUtils.MakeString(title, Cache.DefaultAnalWs);
					result.ContentsOA = Cache.ServiceLocator.GetInstance<IStTextFactory>().Create();
				});
			return result;
		}

		/// <summary>
		/// The comment of an StText depends on the comment of the owning Text.
		/// </summary>
		[Test]
		public void StTextCommentDependsOnTextDescription()
		{
			var text = MakeText("my text");
			var stText = text.ContentsOA;
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo change comment", "redo", m_actionHandler,
				() =>
				{
					text.Description.AnalysisDefaultWritingSystem = TsStringUtils.MakeString("new description", Cache.DefaultAnalWs);
				});
			var len = "new description".Length;
			CheckChange(StTextTags.kClassId, stText, "Comment", Cache.DefaultAnalWs, 0, 0,
						"changing Description of Text should change Comment of StText");
		}

		/// <summary>
		/// The comment of an StText depends on the Abbreviation of the owning Text.
		/// </summary>
		[Test]
		public void StTextTitleAbbrDependsOnTextAbbreviation()
		{
			var text = MakeText("my text");
			var stText = text.ContentsOA;
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo change abbr", "redo", m_actionHandler,
				() =>
				{
					text.Abbreviation.AnalysisDefaultWritingSystem = TsStringUtils.MakeString("myT", Cache.DefaultAnalWs);
				});
			var len = "myT".Length;
			CheckChange(StTextTags.kClassId, stText, "TitleAbbreviation", Cache.DefaultAnalWs, 0, 0,
						"changing Abbreviation of Text should change TitleAbbreviation of StText");
		}

		/// <summary>
		/// The GenreCategories of an StText depends on the Genres of the owning Text.
		/// </summary>
		[Test]
		public void StTextGenreCategoriesDependsOnTextGenres()
		{
			var text = MakeText("my text");
			var stText = text.ContentsOA;
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo add genres", "redo", m_actionHandler,
				() =>
					{
						var genreList = Cache.LangProject.GenreListOA;
						if (genreList == null)
						{
							genreList = Cache.ServiceLocator.GetInstance<ICmPossibilityListFactory>().Create();
							Cache.LangProject.GenreListOA = genreList;
						}
						if (genreList.PossibilitiesOS.Count == 0)
						{
							genreList.PossibilitiesOS.Add(Cache.ServiceLocator.GetInstance<ICmPossibilityFactory>().Create());
						}
						var genre = genreList.PossibilitiesOS[0];
						text.GenresRC.Add(genre);
					});
			CheckChange(StTextTags.kClassId, stText, "GenreCategories", 0, 1, 0,
				"adding Genres to Text should change GenreCategories of StText");
			// Unfortunately removing one involves a different code path.
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo remve genre", "redo", m_actionHandler,
				() => text.GenresRC.Remove(text.GenresRC.First()));
			CheckChange(StTextTags.kClassId, stText, "GenreCategories", 0, 0, 0,
				"removing Genres from Text should change GenreCategories of StText");
		}

		/// <summary>
		/// Tests that a PropChanged is generated when a Text is created using a fixed guid.
		/// </summary>
		[Test]
		public void LangProjTextsGetsPropChangedForCreateWithGuid()
		{
			var guid = Guid.NewGuid();

			var sut = Cache.ServiceLocator.GetInstance<ITextFactory>();
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo", "redo", m_actionHandler,
				() => sut.Create(Cache, guid));
			CheckChange(LangProjectTags.kClassId, Cache.LangProject, "Texts", 0, 1, 0,
				"Creating a text should generate a PropChanged on LangProj");

		}

		/// <summary>
		/// We get a PropChanged for "FullConcordanceCount" when changing the Analyses of a segment.
		/// </summary>
		[Test]
		public void FullConcordanceCountDependsOnAnalyses()
		{
			var text = MakeText("my text");
			var stText = text.ContentsOA;
			IStTxtPara para0 = null;
			UndoableUnitOfWorkHelper.Do("undo add segment", "redo", m_actionHandler,
				() =>
					{
						para0 = Cache.ServiceLocator.GetInstance<IStTxtParaFactory>().Create();
						stText.ParagraphsOS.Add(para0);
					});
			UndoableUnitOfWorkHelper.Do("undo add segment", "redo", m_actionHandler,
				() =>
					{
						var seg0 = MakeSegment(stText, "hello world.");
						MakeWordformAnalysis(seg0, "hello");
						MakeWordformAnalysis(seg0, "world");
						para0.ParseIsCurrent = true; // so we will auto-parse on mods.
					});
			var wfHello = WfiWordformServices.FindOrCreateWordform(Cache, "hello",Cache.ServiceLocator.WritingSystems.DefaultVernacularWritingSystem);
			WfiWordformServices.FindOrCreateWordform(Cache, "world", Cache.ServiceLocator.WritingSystems.DefaultVernacularWritingSystem);
			// This is not just to test it, but to cause the count data structure to be set up.
			Assert.That(wfHello.FullConcordanceCount, Is.EqualTo(1));
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo add segment", "redo", m_actionHandler,
				() => MakeSegment(stText, "the world says hello."));
			CheckChange(WfiWordformTags.kClassId, wfHello, "FullConcordanceCount", 0, 0, 0,
				"Adding a segment should cause notification of wordform count");
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
			var para = (IStTxtPara)text.ParagraphsOS[0];
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

		/// <summary>
		/// The PublishAsMinorEntry property depends on various things.
		/// </summary>
		[Test]
		public void PublishAsMinorEntryDependsOnEntryRefsAndHideMinorEntry()
		{
			var kickBucket = MakeEntry("kick the bucket", "die") as LexEntry;
			// We don't really care about its value when there are no EntryRefs. However the current default is false,
			// and we want it to CHANGE when we add one.
			Assert.That(kickBucket.PublishAsMinorEntry, Is.False);

			PrepareToTrackPropChanged();
			ILexEntryRef ler = MakeComplexFormLexEntryRef(kickBucket);
			// Now it has one LER, and its HideMinorEntry defaults to 0. Thus it's not hidden, and should be published.
			Assert.That(kickBucket.PublishAsMinorEntry, Is.True);
			CheckChange(kickBucket.ClassID, kickBucket, "PublishAsMinorEntry", 0, 0, 0,
				"Adding a LER to a lex entry should change PublishAsMinorEntry");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo set HideMinorEntry", "redo", m_actionHandler,
				() => ler.HideMinorEntry = 1);
			Assert.That(kickBucket.PublishAsMinorEntry, Is.False);
			CheckChange(kickBucket.ClassID, kickBucket, "PublishAsMinorEntry", 0, 0, 0,
				"Changing HideMinorEntry should change PublishAsMinorEntry");
			m_actionHandler.Undo();
			Assert.That(kickBucket.PublishAsMinorEntry, Is.True);

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo delete LER", "redo", m_actionHandler,
				() => kickBucket.EntryRefsOS.RemoveAt(0));
			// Now it has one LER, and its HideMinorEntry defaults to 0. Thus it's not hidden, and should be published.
			Assert.That(kickBucket.PublishAsMinorEntry, Is.False);
			CheckChange(kickBucket.ClassID, kickBucket, "PublishAsMinorEntry", 0, 0, 0,
				"Removing a LER from a lex entry should change PublishAsMinorEntry");

		}
		/// <summary>
		/// The various PublishIn properties depend on their DoNotPublishIn real properties.
		/// </summary>
		[Test]
		public void PublishInDependsOnDoNotPublishIn()
		{
			var kick = MakeEntry("kick", "strike with foot");
			var kickS = kick.SensesOS[0];
			var heKicked = MakeExample(kickS, "he kicked the ball");

			TestPublishIn(kick, () => kick.PublishIn, kick.DoNotPublishInRC);
			TestPublishIn(kickS, () => kickS.PublishIn, kickS.DoNotPublishInRC);
			TestPublishIn(heKicked, () => heKicked.PublishIn, heKicked.DoNotPublishInRC);
		}

		/// <summary>
		/// For correct handling of some virtual properties, we must not allow the type of a LER
		/// to be changed once it has components.
		/// (If we need to change this, note that various virtual properties, such as VariantFormEntries,
		/// need to update automatically when type changes and components is non-empty. See LT-12671.
		/// </summary>
		[Test]
		public void LexEntryRefTypeThrowsIfComponentsNonEmpty()
		{
			var kickbucket = MakeEntry("kick the bucket", "die");
			var kick = MakeEntry("kick", "strike with foot");
			UndoableUnitOfWorkHelper.Do("undo", "redo", m_actionHandler,
				() =>
					{
						var ler = Cache.ServiceLocator.GetInstance<ILexEntryRefFactory>().Create();
						kickbucket.EntryRefsOS.Add(ler);
						ler.ComponentLexemesRS.Add(kick);
						Assert.Throws(typeof (InvalidOperationException), () => ler.RefType = LexEntryRefTags.krtComplexForm);
					});
		}

		void TestPublishIn<T>(ICmObject lexItem, Func<IEnumerable<T>> reader, ILcmReferenceCollection<ICmPossibility> doNotPublish)
		{
			var mainDict = Cache.LangProject.LexDbOA.PublicationTypesOA.PossibilitiesOS[0];
			Assert.That(reader().Count(), Is.EqualTo(1));
			Assert.That(reader().ToArray()[0], Is.EqualTo(mainDict));
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo set DoNotPublish", "redo", m_actionHandler,
				() => doNotPublish.Add(mainDict));
			Assert.That(reader().Count(), Is.EqualTo(0));
			CheckChange(lexItem.ClassID, lexItem, "PublishIn", 0, 0, 0,
				"Adding a publication to DoNoPublishIn should remove it from PublishIn");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo clear DoNotPublish", "redo", m_actionHandler,
				() => doNotPublish.Remove(mainDict));
			Assert.That(reader().Count(), Is.EqualTo(1));
			CheckChange(lexItem.ClassID, lexItem, "PublishIn", 0, 1, 0,
				"Removing a publication from DoNoPublishIn should add it to PublishIn");

			var pocket = MakePossibility(Cache.LangProject.LexDbOA.PublicationTypesOA, "pocket");
			// Enhance JohnT: possibly making a new publication should generate PropChanged for
			// all PublishIn lists in the system! But it can't be relevant to do so, because
			// no window can be displaying the new publication. It's also probably rare enough
			// that using Refresh is acceptable, if it makes a difference somewhere (e.g., when one
			// is deleted).
			Assert.That(reader().Count(), Is.EqualTo(2));
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo set DoNotPublish", "redo", m_actionHandler,
				() => doNotPublish.Add(pocket));
			Assert.That(reader().Count(), Is.EqualTo(1));
			Assert.That(reader().ToArray()[0], Is.EqualTo(mainDict));
			CheckChange(lexItem.ClassID, lexItem, "PublishIn", 0, 1, 0,
				"Adding a publication to DoNoPublishIn should remove it from PublishIn (but leave the other)");
			UndoableUnitOfWorkHelper.Do("do", "undo", m_actionHandler,
				() => Cache.LangProject.LexDbOA.PublicationTypesOA.PossibilitiesOS.Remove(pocket));
		}

		/// <summary>
		/// The various ShowMainEntryIn properties depend on their DoNotShowMainEntryIn real properties.
		/// </summary>
		[Test]
		public void ShowMainEntryInDependsOnDoNotShowMainEntryIn()
		{
			var kick = MakeEntry("kick", "strike with foot");

			TestShowMainEntryIn(kick, () => kick.ShowMainEntryIn, kick.DoNotShowMainEntryInRC);
		}

		void TestShowMainEntryIn<T>(ICmObject lexItem, Func<IEnumerable<T>> reader, ILcmReferenceCollection<ICmPossibility> doNotShowMainEntry)
		{
			var mainDict = Cache.LangProject.LexDbOA.PublicationTypesOA.PossibilitiesOS[0];
			Assert.That(reader().Count(), Is.EqualTo(1));
			Assert.That(reader().ToArray()[0], Is.EqualTo(mainDict));
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo set doNotShowMainEntry", "redo", m_actionHandler,
				() => doNotShowMainEntry.Add(mainDict));
			Assert.That(reader().Count(), Is.EqualTo(0));
			CheckChange(lexItem.ClassID, lexItem, "ShowMainEntryIn", 0, 0, 0,
				"Adding a publication to DoNotShowMainEntryIn should remove it from ShowMainEntryIn");

			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo clear DoNotShowMainEntryIn", "redo", m_actionHandler,
				() => doNotShowMainEntry.Remove(mainDict));
			Assert.That(reader().Count(), Is.EqualTo(1));
			CheckChange(lexItem.ClassID, lexItem, "ShowMainEntryIn", 0, 1, 0,
				"Removing a publication from DoNotShowMainEntryIn should add it to ShowMainEntryIn");

			var pocket = MakePossibility(Cache.LangProject.LexDbOA.PublicationTypesOA, "pocket");
			// Enhance JohnT: possibly making a new publication should generate PropChanged for
			// all ShowMainEntryIn lists in the system! But it can't be relevant to do so, because
			// no window can be displaying the new publication. It's also probably rare enough
			// that using Refresh is acceptable, if it makes a difference somewhere (e.g., when one
			// is deleted).
			Assert.That(reader().Count(), Is.EqualTo(2));
			PrepareToTrackPropChanged();
			UndoableUnitOfWorkHelper.Do("undo set DoNotShowMainEntry", "redo", m_actionHandler,
				() => doNotShowMainEntry.Add(pocket));
			Assert.That(reader().Count(), Is.EqualTo(1));
			Assert.That(reader().ToArray()[0], Is.EqualTo(mainDict));
			CheckChange(lexItem.ClassID, lexItem, "ShowMainEntryIn", 0, 1, 0,
				"Adding a publication to DoNotShowMainEntryIn should remove it from ShowMainEntryIn (but leave the other)");
			UndoableUnitOfWorkHelper.Do("do", "undo", m_actionHandler,
				() => Cache.LangProject.LexDbOA.PublicationTypesOA.PossibilitiesOS.Remove(pocket));
		}

		private ICmPossibility MakePossibility(ICmPossibilityList list, string name)
		{
			ICmPossibility result = null;
			UndoableUnitOfWorkHelper.Do("do", "undo", m_actionHandler,
				() =>
				{
					result = Cache.ServiceLocator.GetInstance<ICmPossibilityFactory>().Create();
					list.PossibilitiesOS.Add(result);
					result.Name.AnalysisDefaultWritingSystem = AnalysisTss(name);
				});
			return result;
		}

		private ITsString AnalysisTss(string form)
		{
			return TsStringUtils.MakeString(form, Cache.DefaultAnalWs);
		}

		ILexExampleSentence MakeExample(ILexSense sense, string text)
		{
			ILexExampleSentence result = null;
			UndoableUnitOfWorkHelper.Do("do", "undo", m_actionHandler,
				() =>
					{
						result = Cache.ServiceLocator.GetInstance<ILexExampleSentenceFactory>().Create();
						sense.ExamplesOS.Add(result);
						result.Example.VernacularDefaultWritingSystem = VernacularTss(text);
					});
			return result;
		}

		private ITsString VernacularTss(string form)
		{
			return TsStringUtils.MakeString(form, Cache.DefaultVernWs);
		}
	}
}
