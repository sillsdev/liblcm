// Copyright (c) 2002-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
//
// <remarks>
// Implements VectorTests.
// </remarks>

using System;
using System.Linq;
using NUnit.Framework;
using Rhino.Mocks;
using SIL.LCModel.Core.Scripture;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Infrastructure;

namespace SIL.LCModel.DomainImpl
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Test public properties and methods on the abstract LcmVector class.
	/// Some properties and methods are abstract, so the 'test'
	/// will end up testing those subclass implementations.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[TestFixture]
	public class MainVectorTests : ScrInMemoryLcmTestBase
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test the Count property.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void CountPropertyTests()
		{
			ILcmServiceLocator servLoc = Cache.ServiceLocator;
			ILangProject lp = Cache.LanguageProject;
			ILexDb lexDb = lp.LexDbOA;
			ILexEntry le = servLoc.GetInstance<ILexEntryFactory>().Create();
			ILexSense sense = servLoc.GetInstance<ILexSenseFactory>().Create();
			le.SensesOS.Add(sense);

			// LcmReferenceCollection
			int originalCount = lexDb.LexicalFormIndexRC.Count;
			lexDb.LexicalFormIndexRC.Add(le);
			Assert.AreEqual(originalCount + 1, lexDb.LexicalFormIndexRC.Count);
			lexDb.LexicalFormIndexRC.Remove(le);
			Assert.AreEqual(originalCount, lexDb.LexicalFormIndexRC.Count);

			// LcmReferenceSequence
			originalCount = le.MainEntriesOrSensesRS.Count;
			le.MainEntriesOrSensesRS.Add(sense);
			Assert.AreEqual(originalCount + 1, le.MainEntriesOrSensesRS.Count);
			le.MainEntriesOrSensesRS.RemoveAt(le.MainEntriesOrSensesRS.Count - 1);
			Assert.AreEqual(originalCount, le.MainEntriesOrSensesRS.Count);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test the Owning sequence Item ([idx]) method.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void TestOwningSequenceIndexOverwriteDeletesObject()
		{
			var servLoc = Cache.ServiceLocator;
			var entry = servLoc.GetInstance<ILexEntryFactory>().Create();
			var senseFactory = servLoc.GetInstance<ILexSenseFactory>();
			var sense1 = senseFactory.Create();
			entry.SensesOS.Add(sense1);
			var sense2 = senseFactory.Create();
			entry.SensesOS[0] = sense2;

			Assert.AreEqual((int)SpecialHVOValues.kHvoObjectDeleted, sense1.Hvo, "Sense not deleted.");
			Assert.AreSame(sense2, entry.SensesOS[0], "Sense2 not in the right place.");
			Assert.AreEqual(1, entry.SensesOS.Count, "Wrong number of senses.");
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test that OwnOrd gets updated properly when an Owning sequence is modified.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void OwningSequence_OwnOrd_Tests()
		{
			var servLoc = Cache.ServiceLocator;
			var lp = Cache.LanguageProject;
			var leFactory = servLoc.GetInstance<ILexEntryFactory>();
			var le = leFactory.Create();

			var alloFactory = servLoc.GetInstance<IMoStemAllomorphFactory>();
			var allo1 = alloFactory.Create();
			le.AlternateFormsOS.Add(allo1);
			var allo2 = alloFactory.Create();
			le.AlternateFormsOS.Add(allo2);
			var allo3 = alloFactory.Create();
			le.AlternateFormsOS.Add(allo3);

			le.AlternateFormsOS.Add(allo1);
			Assert.IsTrue(le.AlternateFormsOS.SequenceEqual(new IMoForm[] { allo2, allo3, allo1 }));
			Assert.AreEqual(2, allo1.OwnOrd);
			Assert.AreEqual(0, allo2.OwnOrd);
			Assert.AreEqual(1, allo3.OwnOrd);

			le.AlternateFormsOS.Insert(0, allo1);
			Assert.IsTrue(le.AlternateFormsOS.SequenceEqual(new IMoForm[] { allo1, allo2, allo3 }));
			Assert.AreEqual(0, allo1.OwnOrd);
			Assert.AreEqual(1, allo2.OwnOrd);
			Assert.AreEqual(2, allo3.OwnOrd);

			le.AlternateFormsOS.Insert(1, allo1);
			Assert.IsTrue(le.AlternateFormsOS.SequenceEqual(new IMoForm[] { allo1, allo2, allo3 }));
			Assert.AreEqual(0, allo1.OwnOrd);
			Assert.AreEqual(1, allo2.OwnOrd);
			Assert.AreEqual(2, allo3.OwnOrd);

			le.AlternateFormsOS.Insert(2, allo1);
			Assert.IsTrue(le.AlternateFormsOS.SequenceEqual(new IMoForm[] { allo2, allo1, allo3 }));
			Assert.AreEqual(1, allo1.OwnOrd);
			Assert.AreEqual(0, allo2.OwnOrd);
			Assert.AreEqual(2, allo3.OwnOrd);

			le.AlternateFormsOS.Remove(allo1);
			Assert.IsTrue(le.AlternateFormsOS.SequenceEqual(new IMoForm[] { allo2, allo3 }));
			Assert.AreEqual(0, allo2.OwnOrd);
			Assert.AreEqual(1, allo3.OwnOrd);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// If an item is in an owning sequence and it is inserted into a different sequence,
		/// the own ord values of all items in both lists should be maintained properly.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void InsertUsedToMoveObjBetweenOwningSequences()
		{
			var servLoc = Cache.ServiceLocator;
			var lp = Cache.LanguageProject;
			var leFactory = servLoc.GetInstance<ILexEntryFactory>();
			var le1 = leFactory.Create();
			var le2 = leFactory.Create();

			var alloFactory = servLoc.GetInstance<IMoStemAllomorphFactory>();
			var allo1 = alloFactory.Create();
			le1.AlternateFormsOS.Add(allo1);
			var allo2 = alloFactory.Create();
			le1.AlternateFormsOS.Add(allo2);
			var allo3 = alloFactory.Create();
			le1.AlternateFormsOS.Add(allo3);
			var allo4 = alloFactory.Create();
			le2.AlternateFormsOS.Add(allo4);
			var allo5 = alloFactory.Create();
			le2.AlternateFormsOS.Add(allo5);
			var allo6 = alloFactory.Create();
			le2.AlternateFormsOS.Add(allo6);

			// Move allo1 from first entry's list to second entry's list
			le2.AlternateFormsOS.Insert(0, allo1);
			Assert.IsTrue(le1.AlternateFormsOS.SequenceEqual(new IMoForm[] { allo2, allo3 }));
			Assert.IsTrue(le2.AlternateFormsOS.SequenceEqual(new IMoForm[] { allo1, allo4, allo5, allo6 }));
			Assert.AreEqual(0, allo1.OwnOrd);
			Assert.AreEqual(0, allo2.OwnOrd);
			Assert.AreEqual(1, allo3.OwnOrd);
			Assert.AreEqual(1, allo4.OwnOrd);
			Assert.AreEqual(2, allo5.OwnOrd);
			Assert.AreEqual(3, allo6.OwnOrd);

			le1.AlternateFormsOS.Insert(1, allo4);
			Assert.IsTrue(le1.AlternateFormsOS.SequenceEqual(new IMoForm[] { allo2, allo4, allo3 }));
			Assert.IsTrue(le2.AlternateFormsOS.SequenceEqual(new IMoForm[] { allo1, allo5, allo6 }));
			Assert.AreEqual(0, allo1.OwnOrd);
			Assert.AreEqual(0, allo2.OwnOrd);
			Assert.AreEqual(2, allo3.OwnOrd);
			Assert.AreEqual(1, allo4.OwnOrd);
			Assert.AreEqual(1, allo5.OwnOrd);
			Assert.AreEqual(2, allo6.OwnOrd);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// If an item is in an owning sequence and the indexed setter (this[]) is used to move
		/// it into a different sequence, the own ord values of all items in both lists should
		/// be maintained properly.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void IndexedSetterUsedToMoveObjBetweenOwningSequences()
		{
			var servLoc = Cache.ServiceLocator;
			var lp = Cache.LanguageProject;
			var leFactory = servLoc.GetInstance<ILexEntryFactory>();
			var le1 = leFactory.Create();
			var le2 = leFactory.Create();

			var alloFactory = servLoc.GetInstance<IMoStemAllomorphFactory>();
			var allo1 = alloFactory.Create();
			le1.AlternateFormsOS.Add(allo1);
			var allo2 = alloFactory.Create();
			le1.AlternateFormsOS.Add(allo2);
			var allo3 = alloFactory.Create();
			le1.AlternateFormsOS.Add(allo3);
			var allo4 = alloFactory.Create();
			le2.AlternateFormsOS.Add(allo4);
			var allo5 = alloFactory.Create();
			le2.AlternateFormsOS.Add(allo5);
			var allo6 = alloFactory.Create();
			le2.AlternateFormsOS.Add(allo6);

			// Move allo1 from first entry's list to second entry's list
			le2.AlternateFormsOS[0] = allo1;
			Assert.IsTrue(le1.AlternateFormsOS.SequenceEqual(new IMoForm[] { allo2, allo3 }));
			Assert.IsTrue(le2.AlternateFormsOS.SequenceEqual(new IMoForm[] { allo1, allo5, allo6 }));
			Assert.AreEqual(0, allo1.OwnOrd);
			Assert.AreEqual(0, allo2.OwnOrd);
			Assert.AreEqual(1, allo3.OwnOrd);
			Assert.AreEqual(1, allo5.OwnOrd);
			Assert.AreEqual(2, allo6.OwnOrd);

			le1.AlternateFormsOS[1] = allo6;
			Assert.IsTrue(le1.AlternateFormsOS.SequenceEqual(new IMoForm[] { allo2, allo6 }));
			Assert.IsTrue(le2.AlternateFormsOS.SequenceEqual(new IMoForm[] { allo1, allo5 }));
			Assert.AreEqual(0, allo1.OwnOrd);
			Assert.AreEqual(0, allo2.OwnOrd);
			Assert.AreEqual(1, allo5.OwnOrd);
			Assert.AreEqual(1, allo6.OwnOrd);

			le1.AlternateFormsOS[1] = allo5;
			Assert.IsTrue(le1.AlternateFormsOS.SequenceEqual(new IMoForm[] { allo2, allo5 }));
			Assert.IsTrue(le2.AlternateFormsOS.SequenceEqual(new IMoForm[] { allo1 }));
			Assert.AreEqual(0, allo1.OwnOrd);
			Assert.AreEqual(0, allo2.OwnOrd);
			Assert.AreEqual(1, allo5.OwnOrd);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test the Contains method.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ContainsMethodTests()
		{
			ILcmServiceLocator servLoc = Cache.ServiceLocator;
			ILangProject lp = Cache.LanguageProject;
			ILexDb lexDb = lp.LexDbOA;
			ILexEntry le = servLoc.GetInstance<ILexEntryFactory>().Create();

			// LcmReferenceCollection
			Assert.IsFalse(lexDb.LexicalFormIndexRC.Contains(le));
			lexDb.LexicalFormIndexRC.Add(le);
			Assert.IsTrue(lexDb.LexicalFormIndexRC.Contains(le));
			lexDb.LexicalFormIndexRC.Remove(le);
		}

		/// <summary>
		/// Test that the replace method in the LCM vectors can be undone.
		/// </summary>
		[Test]
		public void ReplaceUndoTest()
		{
			ILexEntry kick = null;
			ILexEntry bucket = null;
			ILexEntry the = null;
			ILexEntry kickBucket = null;

			kick = MakeEntryWithForm("kick");
			bucket = MakeEntryWithForm("bucket");
			the = MakeEntryWithForm("the");
			kickBucket = MakeEntryWithForm("kick the bucket");
			kickBucket.AddComponent(kick);
			kickBucket.AddComponent(the);
			kickBucket.AddComponent(bucket);
			var entryRef = kickBucket.EntryRefsOS[0];
			Assert.That(entryRef.PrimaryLexemesRS[0], Is.EqualTo(kick));
			m_actionHandler.EndUndoTask();
			UndoableUnitOfWorkHelper.Do("doit", "undoit", Cache.ActionHandlerAccessor,
				() => entryRef.ComponentLexemesRS.Replace(0, 3, new[] { bucket, the, kick }));
			Assert.That(entryRef.ComponentLexemesRS[0], Is.EqualTo(bucket)); //test that the replace made the proper order change
			m_actionHandler.Undo();
			Assert.That(entryRef.ComponentLexemesRS[0], Is.EqualTo(kick)); //test that the order change was reversed

			UndoableUnitOfWorkHelper.Do("doit", "undoit", Cache.ActionHandlerAccessor,
			() => entryRef.ComponentLexemesRS.Replace(0, 3, new[] { kick, the }));
			Assert.That(entryRef.ComponentLexemesRS.Count, Is.EqualTo(2)); //test that the replace removed the last item
			Assert.That(entryRef.ComponentLexemesRS[0], Is.EqualTo(kick)); //should be unchanged

			m_actionHandler.Undo();
			Assert.That(entryRef.ComponentLexemesRS.Count, Is.EqualTo(3)); //test that the undo restored the last item
			Assert.That(entryRef.ComponentLexemesRS[2], Is.EqualTo(bucket)); //test that the order change was reversed

			m_actionHandler.Redo();
			Assert.That(entryRef.ComponentLexemesRS.Count, Is.EqualTo(2)); //test that the replace removed the last item
			Assert.That(entryRef.ComponentLexemesRS[0], Is.EqualTo(kick)); //should be unchanged

			UndoableUnitOfWorkHelper.Do("doit", "undoit", Cache.ActionHandlerAccessor,
			() => entryRef.ComponentLexemesRS.Replace(0, 0, new[] { bucket }));
			Assert.That(entryRef.ComponentLexemesRS.Count, Is.EqualTo(3)); //we inserted one
			Assert.That(entryRef.ComponentLexemesRS[0], Is.EqualTo(bucket)); //the inserted one
			Assert.That(entryRef.ComponentLexemesRS[1], Is.EqualTo(kick)); //old first item now in slot 1

			m_actionHandler.Undo();
			Assert.That(entryRef.ComponentLexemesRS.Count, Is.EqualTo(2)); //bucket is back out
			Assert.That(entryRef.ComponentLexemesRS[0], Is.EqualTo(kick)); //moved back to index 0

			UndoableUnitOfWorkHelper.Do("doit", "undoit", Cache.ActionHandlerAccessor,
			() => entryRef.ComponentLexemesRS.Replace(1, 1, new[] { bucket, the }));
			Assert.That(entryRef.ComponentLexemesRS.Count, Is.EqualTo(3)); //should now be kick bucket the
			Assert.That(entryRef.ComponentLexemesRS[0], Is.EqualTo(kick)); //unchanged
			Assert.That(entryRef.ComponentLexemesRS[1], Is.EqualTo(bucket)); //inserted
			Assert.That(entryRef.ComponentLexemesRS[2], Is.EqualTo(the)); //moved

			UndoableUnitOfWorkHelper.Do("doit", "undoit", Cache.ActionHandlerAccessor,
			() => entryRef.ComponentLexemesRS.Replace(1, 1, new[] { bucket, the, bucket }));
			Assert.That(entryRef.ComponentLexemesRS.Count, Is.EqualTo(5)); //should now be kick bucket the bucket the
			Assert.That(entryRef.ComponentLexemesRS[0], Is.EqualTo(kick)); //unchanged
			Assert.That(entryRef.ComponentLexemesRS[1], Is.EqualTo(bucket)); //replaced with itself
			Assert.That(entryRef.ComponentLexemesRS[2], Is.EqualTo(the)); //inserted
			Assert.That(entryRef.ComponentLexemesRS[3], Is.EqualTo(bucket)); //inserted
			Assert.That(entryRef.ComponentLexemesRS[4], Is.EqualTo(the)); //moved

			m_actionHandler.Undo();
			Assert.That(entryRef.ComponentLexemesRS.Count, Is.EqualTo(3)); //should now be back to kick bucket the
			Assert.That(entryRef.ComponentLexemesRS[0], Is.EqualTo(kick));
			Assert.That(entryRef.ComponentLexemesRS[1], Is.EqualTo(bucket));
			Assert.That(entryRef.ComponentLexemesRS[2], Is.EqualTo(the));

		}

		/// <summary>
		/// Test that the replace method in the LCM vectors can be undone.
		/// </summary>
		[Test]
		public void TestReplaceLastItem()
		{
			ILexEntry kick = null;
			ILexEntry bucket = null;
			ILexEntry can = null;
			ILexEntry kickBucket = null;

			kick = MakeEntryWithForm("kick");
			bucket = MakeEntryWithForm("bucket");
			can = MakeEntryWithForm("can");
			kickBucket = MakeEntryWithForm("kick the bucket");
			kickBucket.AddComponent(kick);
			kickBucket.AddComponent(bucket);
			var entryRef = kickBucket.EntryRefsOS[0];
			Assert.That(entryRef.ComponentLexemesRS[0], Is.EqualTo(kick));
			m_actionHandler.EndUndoTask();
			UndoableUnitOfWorkHelper.Do("doit", "undoit", Cache.ActionHandlerAccessor,
				() => entryRef.ComponentLexemesRS.Replace(0, 2, new[] { kick, can }));
			Assert.That(entryRef.ComponentLexemesRS[1], Is.EqualTo(can)); //test that the replace made the proper order change
			m_actionHandler.Undo();
			Assert.That(entryRef.ComponentLexemesRS[1], Is.EqualTo(bucket)); //test that the order change was reversed
		}

		#region private support methods
		private IMoStemAllomorph MakeLexemeForm(ILexEntry entry)
		{
			var form = Cache.ServiceLocator.GetInstance<IMoStemAllomorphFactory>().Create();
			entry.LexemeFormOA = form;
			return form;
		}

		private ILexSense MakeSense(ILexEntry owningEntry)
		{
			var sense = Cache.ServiceLocator.GetInstance<ILexSenseFactory>().Create();
			owningEntry.SensesOS.Add(sense);
			return sense;
		}

		private ILexEntry MakeEntry(string form, string gloss)
		{
			var result = MakeEntryWithForm(form);
			var sense = MakeSense(result);
			sense.Gloss.AnalysisDefaultWritingSystem = TsStringUtils.MakeString(gloss, Cache.DefaultAnalWs);
			return result;
		}

		private ILexEntry MakeEntryWithForm(string form)
		{
			var entry = MakeEntry();
			var lexform = MakeLexemeForm(entry);
			lexform.Form.VernacularDefaultWritingSystem = TsStringUtils.MakeString(form, Cache.DefaultVernWs);
			return entry;
		}
		private ILexEntry MakeEntry()
		{
			var entry = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
			return entry;
		}
		#endregion


		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the CopyTo method.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void CopyTo_OneItemInLongListTest()
		{
			ILcmServiceLocator servLoc = Cache.ServiceLocator;
			IScrBookFactory bookFact = servLoc.GetInstance<IScrBookFactory>();

			// Setup the source sequence using the scripture books sequence.
			IScrBook book0 = bookFact.Create(1);

			IScrBook[] bookArray = new IScrBook[5];
			m_scr.ScriptureBooksOS.CopyTo(bookArray, 3);

			Assert.IsNull(bookArray[0]);
			Assert.IsNull(bookArray[1]);
			Assert.IsNull(bookArray[2]);
			Assert.AreEqual(book0, bookArray[3]);
			Assert.IsNull(bookArray[4]);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the CopyTo method when we are copying into a one-item list.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void CopyTo_OneItemInOneItemListTest()
		{
			ILcmServiceLocator servLoc = Cache.ServiceLocator;
			IScrBookFactory bookFact = servLoc.GetInstance<IScrBookFactory>();

			// Setup the source sequence using the scripture books sequence.
			IScrBook book0 = bookFact.Create(1);

			IScrBook[] bookArray = new IScrBook[1];
			m_scr.ScriptureBooksOS.CopyTo(bookArray, 0);

			Assert.AreEqual(book0, bookArray[0]);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the CopyTo method when we are copying no items into an empty list.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void CopyTo_NoItemsInEmptyItemListTest()
		{
			ILcmServiceLocator servLoc = Cache.ServiceLocator;

			IScrBook[] bookArray = new IScrBook[0];
			m_scr.ScriptureBooksOS.CopyTo(bookArray, 0);
			// This test makes sure that an exception is not thrown when the array is empty.
			// This fixes creating a new List<> when giving a LcmVector as the parameter.
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the CopyTo method when we are copying from one reference sequence to another.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void CopyTo_RefSeqToRefSeq()
		{
			ILcmServiceLocator servLoc = Cache.ServiceLocator;
			ILangProject lp = Cache.LanguageProject;
			ILexEntry le1 = servLoc.GetInstance<ILexEntryFactory>().Create();
			ILexSense sense1 = servLoc.GetInstance<ILexSenseFactory>().Create();
			le1.SensesOS.Add(sense1);
			le1.MainEntriesOrSensesRS.Add(sense1);
			ILexSense sense2 = servLoc.GetInstance<ILexSenseFactory>().Create();
			le1.SensesOS.Add(sense2);
			le1.MainEntriesOrSensesRS.Add(sense2);

			ILexEntry le2 = servLoc.GetInstance<ILexEntryFactory>().Create();

			le1.MainEntriesOrSensesRS.CopyTo(le2.MainEntriesOrSensesRS, 0);

			Assert.AreEqual(2, le2.MainEntriesOrSensesRS.Count);
			Assert.AreEqual(sense1, le2.MainEntriesOrSensesRS[0]);
			Assert.AreEqual(sense2, le2.MainEntriesOrSensesRS[1]);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the AddTo method when we are copying from one reference collection to another.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void AddTo_RefColToRefCol()
		{
			ILcmServiceLocator servLoc = Cache.ServiceLocator;
			ILangProject lp = Cache.LanguageProject;
			ILexDb lexDb = lp.LexDbOA;
			ILexAppendix app1 = servLoc.GetInstance<ILexAppendixFactory>().Create();
			lexDb.AppendixesOC.Add(app1);
			ILexAppendix app2 = servLoc.GetInstance<ILexAppendixFactory>().Create();
			lexDb.AppendixesOC.Add(app2);
			ILexEntry le1 = servLoc.GetInstance<ILexEntryFactory>().Create();
			ILexSense sense1 = servLoc.GetInstance<ILexSenseFactory>().Create();
			le1.SensesOS.Add(sense1);
			ILexSense sense2 = servLoc.GetInstance<ILexSenseFactory>().Create();
			le1.SensesOS.Add(sense2);

			sense1.AppendixesRC.Add(app1);
			sense1.AppendixesRC.Add(app2);

			sense1.AppendixesRC.AddTo(sense2.AppendixesRC);

			Assert.AreEqual(2, sense2.AppendixesRC.Count);
			Assert.IsTrue(sense2.AppendixesRC.Contains(app1));
			Assert.IsTrue(sense2.AppendixesRC.Contains(app2));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the Insert method with a null object.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void Insert_Null()
		{
			ILcmServiceLocator servLoc = Cache.ServiceLocator;

			servLoc.GetInstance<IScrBookFactory>().Create(1);
			Assert.That(() => m_scr.ScriptureBooksOS.Insert(0, null),
				Throws.TypeOf<ArgumentNullException>());
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the Insert method with a null object.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void Insert_Deleted()
		{
			ILcmServiceLocator servLoc = Cache.ServiceLocator;

			IScrBook book0 = servLoc.GetInstance<IScrBookFactory>().Create(1);
			m_scr.ScriptureBooksOS.Remove(book0);
			Assert.That(() => m_scr.ScriptureBooksOS.Insert(0, book0),
				Throws.TypeOf<LcmObjectDeletedException>().With.Message.EqualTo("Owned object has been deleted."));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the Insert method with a null object.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void InsertIntoRefSequence_Uninitialized()
		{
			ILcmServiceLocator servLoc = Cache.ServiceLocator;
			ILangProject lp = Cache.LanguageProject;
			ILexEntry le = servLoc.GetInstance<ILexEntryFactory>().Create();

			var senseUninitialized = MockRepository.GenerateStub<ILexSense>();
			senseUninitialized.Stub(x => x.Hvo).Return((int)SpecialHVOValues.kHvoUninitializedObject);
			Assert.That(() => le.MainEntriesOrSensesRS.Insert(0, senseUninitialized),
				Throws.TypeOf<LcmObjectUninitializedException>().With.Message.EqualTo("Object has not been initialized."));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests inserting an object into an owning sequence that can not be owned
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void Insert_UnownableObject()
		{
			ILcmServiceLocator servLoc = Cache.ServiceLocator;
			IScrBookFactory bookFact = servLoc.GetInstance<IScrBookFactory>();

			// Setup the source sequence using the scripture books sequence.
			IStText text;
			bookFact.Create(1, out text);
			IStTxtPara para = text.AddNewTextPara(ScrStyleNames.MainBookTitle);
			IScrRefSystem systemToAdd = servLoc.GetInstance<IScrRefSystemRepository>().Singleton;
			Assert.That(() => para.AnalyzedTextObjectsOS.Insert(0, systemToAdd),
				Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("ScrRefSystem can not be owned!"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the MoveTo method.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void MoveTo_EmptyListTest()
		{
			ILcmServiceLocator servLoc = Cache.ServiceLocator;
			IScrBookFactory bookFact = servLoc.GetInstance<IScrBookFactory>();

			// Setup the source sequence using the scripture books sequence.
			bookFact.Create(1);
			IScrBook book1 = bookFact.Create(2);
			IScrBook book2 = bookFact.Create(3);

			// Setup the target sequence so it's able to have items moved to it.
			IScrDraft targetSeq = servLoc.GetInstance<IScrDraftFactory>().Create("MoveTo_EmptyListTest");

			m_scr.ScriptureBooksOS.MoveTo(1, 2, targetSeq.BooksOS, 0);

			Assert.AreEqual(2, targetSeq.BooksOS.Count);
			Assert.AreEqual(1, m_scr.ScriptureBooksOS.Count);
			Assert.AreEqual(book1, targetSeq.BooksOS[0]);
			Assert.AreEqual(book2, targetSeq.BooksOS[1]);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the MoveTo method.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void MoveTo_PopulatedListTest()
		{
			ILcmServiceLocator servLoc = Cache.ServiceLocator;
			IScrBookFactory bookFact = servLoc.GetInstance<IScrBookFactory>();

			// Setup the source sequence using the scripture books sequence.
			bookFact.Create(1);
			IScrBook book1 = bookFact.Create(2);
			IScrBook book2 = bookFact.Create(3);

			// Setup the target sequence so it's able to have items moved to it.
			IScrDraft targetSeq = servLoc.GetInstance<IScrDraftFactory>().Create("MoveTo_PopulatedListTest");
			IScrBook bookDest0 = bookFact.Create(targetSeq.BooksOS, 1);
			IScrBook bookDest1 = bookFact.Create(targetSeq.BooksOS, 2);

			m_scr.ScriptureBooksOS.MoveTo(1, 2, targetSeq.BooksOS, 1);

			Assert.AreEqual(4, targetSeq.BooksOS.Count);
			Assert.AreEqual(1, m_scr.ScriptureBooksOS.Count);
			Assert.AreEqual(bookDest0, targetSeq.BooksOS[0]);
			Assert.AreEqual(book1, targetSeq.BooksOS[1]);
			Assert.AreEqual(book2, targetSeq.BooksOS[2]);
			Assert.AreEqual(bookDest1, targetSeq.BooksOS[3]);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the MoveTo method where the old position should go through
		/// RemoveObjectSideEffects().
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void MoveTo_RemoveSideEffects()
		{
			// Setup a chart on a text
			var servLoc = Cache.ServiceLocator;
			var ws = Cache.DefaultVernWs;
			var chart = SetupChart();
			var dummyPoss = chart.TemplateRA;
			var rowFact = servLoc.GetInstance<IConstChartRowFactory>();
			var chartTagFact = servLoc.GetInstance<IConstChartTagFactory>();

			// Setup the Cell sequence in a row using chart tag objects.
			var row0 = rowFact.Create(chart, 0, TsStringUtils.MakeString("1a", ws));
			var row1 = rowFact.Create(chart, 1, TsStringUtils.MakeString("1b", ws));
			var tag1 = chartTagFact.Create(row0, 0, dummyPoss, dummyPoss);
			var tag2 = chartTagFact.Create(row0, 1, dummyPoss, dummyPoss);
			var tag3 = chartTagFact.Create(row1, 0, dummyPoss, dummyPoss);

			// SUT
			row0.CellsOS.MoveTo(0, 1, row1.CellsOS, 0);

			Assert.AreEqual(3, row1.CellsOS.Count);
			Assert.AreEqual(tag1, row1.CellsOS[0]);
			Assert.AreEqual(tag2, row1.CellsOS[1]);
			Assert.AreEqual(tag3, row1.CellsOS[2]);
			Assert.AreEqual((int)SpecialHVOValues.kHvoObjectDeleted, row0.Hvo, "Should delete first row.");
		}

		private IDsConstChart SetupChart()
		{
			var servLoc = Cache.ServiceLocator;
			var text = servLoc.GetInstance<ITextFactory>().Create();
			//Cache.LangProject.TextsOC.Add(text);
			var stText = servLoc.GetInstance<IStTextFactory>().Create();
			text.ContentsOA = stText;
			var data = servLoc.GetInstance<IDsDiscourseDataFactory>().Create();
			Cache.LangProject.DiscourseDataOA = data;
			var dummyList = servLoc.GetInstance<ICmPossibilityListFactory>().Create();
			Cache.LangProject.DiscourseDataOA.ConstChartTemplOA = dummyList;
			var dummy = servLoc.GetInstance<ICmPossibilityFactory>().Create();
			dummyList.PossibilitiesOS.Add(dummy);
			return servLoc.GetInstance<IDsConstChartFactory>().Create(data, stText, dummy);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the MoveTo method.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void MoveTo_DestListLargerThenSrcTest()
		{
			ILcmServiceLocator servLoc = Cache.ServiceLocator;
			IScrBookFactory bookFact = servLoc.GetInstance<IScrBookFactory>();

			// Setup the source sequence using the scripture books sequence.
			IScrBook book0 = bookFact.Create(1);

			// Setup the target sequence so it's able to have items moved to it.
			IScrDraft targetSeq = servLoc.GetInstance<IScrDraftFactory>().Create("MoveTo_DestListLargerThenSrcTest");
			IScrBook bookD0 = bookFact.Create(targetSeq.BooksOS, 1);
			IScrBook bookD1 = bookFact.Create(targetSeq.BooksOS, 2);

			m_scr.ScriptureBooksOS.MoveTo(0, 0, targetSeq.BooksOS, 2);

			Assert.AreEqual(3, targetSeq.BooksOS.Count);
			Assert.AreEqual(0, m_scr.ScriptureBooksOS.Count);
			Assert.AreEqual(bookD0, targetSeq.BooksOS[0]);
			Assert.AreEqual(bookD1, targetSeq.BooksOS[1]);
			Assert.AreEqual(book0, targetSeq.BooksOS[2]);
		}

		//-------------------------------------------------------------------------------
		/// <summary>
		/// Test the 'Count' vector method on a collection.
		/// </summary>
		//-------------------------------------------------------------------------------
		[Test]
		public void VectorCount()
		{
			ILexEntryFactory factory = Cache.ServiceLocator.GetInstance<ILexEntryFactory>();
			factory.Create();
			ILexEntry entry = factory.Create();
			Assert.AreEqual(0, entry.AlternateFormsOS.Count);

			entry.Delete();
			Assert.IsTrue(0 < Cache.LanguageProject.LexDbOA.Entries.Count());
		}

		////-------------------------------------------------------------------------------
		/// <summary>
		/// Test re-adding an object to its owning collection. This should work, but it
		/// won't actually be moved anywhere.
		/// </summary>
		////-------------------------------------------------------------------------------
		[Test]
		public void LcmOwningCollection_ReAddToOC()
		{
			var oc = Cache.LanguageProject.MorphologicalDataOA.AdhocCoProhibitionsOC;
			var acp = Cache.ServiceLocator.GetInstance<IMoAlloAdhocProhibFactory>().Create();
			oc.Add(acp);
			int count = oc.Count();
			oc.Add(acp);    // Try adding it again.
			Assert.AreEqual(count, oc.Count);
		}

		//-------------------------------------------------------------------------------
		/// <summary>
		/// Tests the Contains method on an owning collection
		/// </summary>
		//-------------------------------------------------------------------------------
		[Test]
		public void OwningCollectionContainsObject()
		{
			var factory = Cache.ServiceLocator.GetInstance<ICmResourceFactory>();
			var resource = factory.Create();
			Assert.IsFalse(Cache.LanguageProject.LexDbOA.ResourcesOC.Contains(resource));
			Cache.LangProject.LexDbOA.ResourcesOC.Add(resource);
			Assert.IsTrue(Cache.LanguageProject.LexDbOA.ResourcesOC.Contains(resource));
		}

		//-------------------------------------------------------------------------------
		/// <summary>
		/// Test the 'ReallyReallyAllPossibilities' method on a possibility list.
		/// </summary>
		//-------------------------------------------------------------------------------
		[Test]
		public void GetAllPossibilities()
		{
			// We use the following hierarchy:
			// top - 1 -  1.1 - 1.1.1
			//     \ 2 \- 1.2
			//          \ 1.3
			// which are 7 CmPossibility objects
			var factory = Cache.ServiceLocator.GetInstance<ICmPossibilityFactory>();
			ICmPossibility top = factory.Create();
			Cache.LanguageProject.PartsOfSpeechOA.PossibilitiesOS.Add(top);
			ICmPossibility one = factory.Create();
			top.SubPossibilitiesOS.Add(one);
			ICmPossibility two = factory.Create();
			top.SubPossibilitiesOS.Add(two);
			ICmPossibility oneone = factory.Create();
			one.SubPossibilitiesOS.Add(oneone);
			ICmPossibility onetwo = factory.Create();
			one.SubPossibilitiesOS.Add(onetwo);
			ICmPossibility onethree = factory.Create();
			one.SubPossibilitiesOS.Add(onethree);
			ICmPossibility oneoneone = factory.Create();
			oneone.SubPossibilitiesOS.Add(oneoneone);

			Assert.AreEqual(7, Cache.LanguageProject.PartsOfSpeechOA.ReallyReallyAllPossibilities.Count);
		}

	}

	#region LcmMinimalVectorTests class
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Test most basic public properties and methods on the abstract LcmVector class.
	/// Some properties and methods are abstract, so the 'test'
	/// will end up testing those subclass implementations.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[TestFixture]
	public class LcmMinimalVectorTests : MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Try to add a null item to a vector.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void AddNullItemToVectorTest()
		{
			Assert.That(() => Cache.LanguageProject.AnalyzingAgentsOC.Add(null),
				Throws.TypeOf<ArgumentNullException>());
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Try to add a null item to a vector.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void AddNullItem2ToVectorTest()
		{
			ILcmServiceLocator servLoc = Cache.ServiceLocator;
			ILangProject lp = Cache.LanguageProject;
			ILexEntry le = servLoc.GetInstance<ILexEntryFactory>().Create();
			ILexSense sense = servLoc.GetInstance<ILexSenseFactory>().Create();
			le.SensesOS.Add(sense);

			le.MainEntriesOrSensesRS.Add(sense);
			Assert.That(() => le.MainEntriesOrSensesRS[0] = null,
				Throws.TypeOf<NotSupportedException>());
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Try to add a deleted item to a vector.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void AddDeletedItemToVectorTest()
		{
			// Make new annotation.
			var ann = Cache.ServiceLocator.GetInstance<ICmBaseAnnotationFactory>().Create();
			Cache.LanguageProject.AnnotationsOC.Add(ann);
			Assert.AreNotEqual((int)SpecialHVOValues.kHvoObjectDeleted, ann.Hvo);
			Assert.AreNotEqual((int)SpecialHVOValues.kHvoUninitializedObject, ann.Hvo);

			Cache.LanguageProject.AnnotationsOC.Remove(ann);
			Assert.AreEqual((int)SpecialHVOValues.kHvoObjectDeleted, ann.Hvo);

			Assert.That(() => Cache.LanguageProject.AnnotationsOC.Add(ann),
				Throws.TypeOf<LcmObjectDeletedException>());
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Try to add a duplicate item to an owing collection.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void AddDuplicateOwnedItemToVectorTest()
		{
			// Make new annotation.
			var ann = Cache.ServiceLocator.GetInstance<ICmBaseAnnotationFactory>().Create();
			Cache.LanguageProject.AnnotationsOC.Add(ann);
			Assert.AreNotEqual((int)SpecialHVOValues.kHvoObjectDeleted, ann.Hvo);
			Assert.AreNotEqual((int)SpecialHVOValues.kHvoUninitializedObject, ann.Hvo);

			Cache.LanguageProject.AnnotationsOC.Add(ann); // Should be happy.
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Attempting to set the nth (even if n == 0) element of an empty list should throw an
		/// ArgumentOutOfRangeException. If this seems too obvious to need a test, note that the
		/// implementation in LcmList has been wrong for a long time.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void IndexedSetter_EmptyList()
		{
			ILcmServiceLocator servLoc = Cache.ServiceLocator;
			Cache.LangProject.CheckListsOC.Add(servLoc.GetInstance<ICmPossibilityListFactory>().Create());
			Assert.That(() => Cache.LangProject.CheckListsOC.First().PossibilitiesOS[0] =
				servLoc.GetInstance<ICmPossibilityFactory>().Create(),
				Throws.TypeOf<ArgumentOutOfRangeException>());
		}
	}
	#endregion

}
