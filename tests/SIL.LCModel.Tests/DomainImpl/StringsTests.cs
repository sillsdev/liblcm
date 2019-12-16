﻿// Copyright (c) 2005-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Linq;
using NUnit.Framework;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.Infrastructure;

namespace SIL.LCModel.DomainImpl
{
	/// <summary>
	/// Test the ITsMultiString implementation on MultiAccessor.
	/// </summary>
	[TestFixture]
	public class MultiAccessorTests : MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{
		/// <summary>
		/// Set up class.
		/// </summary>
		public override void FixtureSetup()
		{
			base.FixtureSetup();

			// Add strings needed for tests.
			var englishWsHvo = Cache.WritingSystemFactory.GetWsFromStr("en");
			var spanishWsHvo = Cache.WritingSystemFactory.GetWsFromStr("es");
			var lp = Cache.LangProject;

			NonUndoableUnitOfWorkHelper.Do(m_actionHandler, ()=>
			{
				// Set LP's WorldRegion.
				lp.WorldRegion.set_String(
					englishWsHvo,
					TsStringUtils.MakeString("Stateful LCM Test Project: World Region", englishWsHvo));
				lp.WorldRegion.set_String(
					spanishWsHvo,
					TsStringUtils.MakeString("Proyecto de prueba: LCM: Región del Mundo ", spanishWsHvo));

				// Set LP's Description.
				lp.Description.set_String(
					englishWsHvo,
					TsStringUtils.MakeString("Stateful LCM Test Language Project: Desc", englishWsHvo));
				lp.Description.set_String(
					spanishWsHvo,
					TsStringUtils.MakeString("Proyecto de prueba: LCM: desc", spanishWsHvo));

				// Add Spanish as Anal WS.
				CoreWritingSystemDefinition span = Cache.ServiceLocator.WritingSystemManager.Get(spanishWsHvo);
				lp.AddToCurrentAnalysisWritingSystems(span);
			});
		}

		/// <summary>
		///Make sure it has the right number of strings.
		/// </summary>
		[Test]
		public void StringCountTests1()
		{
			Assert.AreEqual(2, Cache.LangProject.WorldRegion.StringCount);
		}

		/// <summary>
		///Make sure it has the right number of strings.
		/// </summary>
		[Test]
		public void StringCountTests2()
		{
			Assert.AreEqual(0, Cache.LangProject.FieldWorkLocation.StringCount);
		}

		/// <summary>
		/// Make sure we can spin through the collection of strings,
		/// and get each one two ways, and that each retursn the same string.
		/// </summary>
		[Test]
		public void GetStringFromIndexAndget_StringTests()
		{
			var msa = Cache.LangProject.WorldRegion;
			Assert.AreEqual(2, msa.StringCount);
			for (var i = 0; i < msa.StringCount; ++i)
			{
				int ws;
				var tss = msa.GetStringFromIndex(i, out ws);
				var tss2 = msa.get_String(ws);
				Assert.AreSame(tss2, tss);
			}
		}

		/// <summary>
		/// Make sure it returns null for ws that is not present.
		/// </summary>
		[Test]
		public void MissingWsTest()
		{
			CoreWritingSystemDefinition fr = Cache.ServiceLocator.WritingSystemManager.Get("fr");
			var phantom = Cache.LangProject.WorldRegion.get_String(fr.Handle);
			Assert.IsTrue(string.IsNullOrEmpty(phantom.Text));

			// Make sure a made up ws is missing.
			phantom = Cache.LangProject.Description.get_String(1000);
			Assert.IsTrue(string.IsNullOrEmpty(phantom.Text));
		}

		/// <summary>
		/// Make sure it blows up on bad index.
		/// </summary>
		[Test]
		public void Bad0IndexTest()
		{
			Assert.That(() => Cache.LangProject.FieldWorkLocation.GetStringFromIndex(0, out var ws),
				Throws.TypeOf<IndexOutOfRangeException>());
		}

		/// <summary>
		///Make sure it blows up on bad index.
		/// </summary>
		[Test]
		public void BadHighIndexTest()
		{
			Assert.That(() => Cache.LangProject.WorldRegion.GetStringFromIndex(Cache.LangProject.WorldRegion.StringCount, out var ws),
				Throws.TypeOf<IndexOutOfRangeException>());
		}

		/// <summary>
		/// Make sure we can add a good string.
		/// </summary>
		[Test]
		public void CountTest()
		{
			// Start with expected information.
			Assert.AreEqual(2, Cache.LangProject.Description.StringCount, "Wrong number of alternatives for Cache.LangProject.DescriptionAccessor");

			// Create a good string.
			CoreWritingSystemDefinition german = Cache.ServiceLocator.WritingSystemManager.Get("de");

			ITsIncStrBldr tisb = TsStringUtils.MakeIncStrBldr();
			tisb.SetIntPropValues((int)FwTextPropType.ktptWs, 0, german.Handle);
			tisb.Append("Deutchland");
			Cache.LangProject.Description.set_String(german.Handle, tisb.GetString());
			//// Make sure it is in there now.
			Assert.AreEqual(3, Cache.LangProject.Description.StringCount, "Wrong number of alternatives for Cache.LangProject.DescriptionAccessor");

			//// Add the same ws string, but with different text.
			tisb = TsStringUtils.MakeIncStrBldr();
			tisb.SetIntPropValues((int)FwTextPropType.ktptWs, 0, german.Handle);
			tisb.Append("heilige");
			Cache.LangProject.Description.set_String(german.Handle, tisb.GetString());
			//// Make sure it is in there now.
			Assert.AreEqual(3, Cache.LangProject.Description.StringCount, "Wrong number of alternatives for Cache.LangProject.DescriptionAccessor");
		}

		/// <summary>
		/// Make sure we can add a good string.
		/// </summary>
		[Test]
		public void GoodMultiUnicodeTest()
		{
			// Start with expected information.
			Assert.AreEqual(0, Cache.LangProject.MainCountry.StringCount, "Wrong number of alternatives for Cache.LangProject.MainCountryAccessor");

			// Create a good string.
			var english = Cache.LangProject.CurrentAnalysisWritingSystems.First();
			ITsIncStrBldr tisb = TsStringUtils.MakeIncStrBldr();
			tisb.SetIntPropValues((int)FwTextPropType.ktptWs, 0, english.Handle);
			tisb.Append("Mexico");
			Cache.LangProject.MainCountry.set_String(english.Handle, tisb.GetString());

			// Make sure it is in there now.
			Assert.AreEqual(1, Cache.LangProject.MainCountry.StringCount, "Wrong number of alternatives for Cache.LangProject.MainCountryAccessor");
			int ws;
			var mexico = Cache.LangProject.MainCountry.GetStringFromIndex(0, out ws);
			Assert.AreEqual(english.Handle, ws, "Wrong writing system.");
			Assert.AreEqual("Mexico", mexico.Text, "Wrong text.");

			// Add the same ws string, but with different text.
			tisb = TsStringUtils.MakeIncStrBldr();
			tisb.SetIntPropValues((int)FwTextPropType.ktptWs, 0, english.Handle);
			tisb.Append("Guatemala");
			Cache.LangProject.MainCountry.set_String(english.Handle, tisb.GetString());

			// Make sure it is in there now.
			Assert.AreEqual(1, Cache.LangProject.MainCountry.StringCount, "Wrong number of alternatives for Cache.LangProject.MainCountryAccessor");
			var guatemala = Cache.LangProject.MainCountry.GetStringFromIndex(0, out ws);
			Assert.AreEqual(english.Handle, ws, "Wrong writing system.");
			Assert.AreEqual("Guatemala", guatemala.Text, "Wrong text.");
		}

		/// <summary>
		/// Test a regular single ITsString property (not a multi-).
		/// </summary>
		[Test]
		public void PlainStringTest()
		{
			var le = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
			var irOriginalValue = TsStringUtils.MakeString("import residue",
				Cache.WritingSystemFactory.UserWs);
			le.ImportResidue = irOriginalValue;
			Assert.AreSame(irOriginalValue, le.ImportResidue, "Wrong string.");
			var irNewValue = TsStringUtils.MakeString("New import residue",
				Cache.WritingSystemFactory.UserWs);
			le.ImportResidue = irNewValue;
			Assert.AreSame(irNewValue, le.ImportResidue, "Wrong string.");
		}

		/// <summary>
		/// Test the MergeAlternatives method.
		/// </summary>
		[Test]
		public void MergeAlternativesTest()
		{
			var english = Cache.LangProject.CurrentAnalysisWritingSystems.ElementAt(0);
			var spanish = Cache.LangProject.CurrentAnalysisWritingSystems.ElementAt(1);
			ITsIncStrBldr tisb = TsStringUtils.MakeIncStrBldr();
			tisb.SetIntPropValues((int)FwTextPropType.ktptWs, 0, english.Handle);
			tisb.Append("Mexico");
			Cache.LangProject.MainCountry.set_String(english.Handle, tisb.GetString());

			tisb = TsStringUtils.MakeIncStrBldr();
			tisb.SetIntPropValues((int)FwTextPropType.ktptWs, 0, spanish.Handle);
			tisb.Append("Mejico");
			Cache.LangProject.MainCountry.set_String(spanish.Handle, tisb.GetString());

			tisb = TsStringUtils.MakeIncStrBldr();
			tisb.SetIntPropValues((int)FwTextPropType.ktptWs, 0, english.Handle);
			tisb.Append("Saltillo");
			Cache.LangProject.FieldWorkLocation.set_String(english.Handle, tisb.GetString());

			Cache.LangProject.FieldWorkLocation.MergeAlternatives(Cache.LangProject.MainCountry);
			Assert.AreEqual("Saltillo", Cache.LangProject.FieldWorkLocation.get_String(english.Handle).Text);
			Assert.AreEqual("Mejico", Cache.LangProject.FieldWorkLocation.get_String(spanish.Handle).Text);

			tisb = TsStringUtils.MakeIncStrBldr();
			tisb.SetIntPropValues((int)FwTextPropType.ktptWs, 0, spanish.Handle);
			tisb.Append("Saltillo");
			Cache.LangProject.FieldWorkLocation.set_String(spanish.Handle, tisb.GetString());

			Cache.LangProject.FieldWorkLocation.MergeAlternatives(Cache.LangProject.MainCountry, true, ", ");
			Assert.AreEqual("Saltillo, Mexico", Cache.LangProject.FieldWorkLocation.get_String(english.Handle).Text);
			Assert.AreEqual("Saltillo, Mejico", Cache.LangProject.FieldWorkLocation.get_String(spanish.Handle).Text);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test the AppendAlternatives method.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void AppendAlternativesTest()
		{
			var english = Cache.LangProject.CurrentAnalysisWritingSystems.ElementAt(0);
			var spanish = Cache.LangProject.CurrentAnalysisWritingSystems.ElementAt(1);
			ITsIncStrBldr tisb = TsStringUtils.MakeIncStrBldr();
			tisb.SetIntPropValues((int)FwTextPropType.ktptWs, 0, english.Handle);
			tisb.Append("Mexico");
			Cache.LangProject.MainCountry.set_String(english.Handle, tisb.GetString());

			tisb = TsStringUtils.MakeIncStrBldr();
			tisb.SetIntPropValues((int)FwTextPropType.ktptWs, 0, spanish.Handle);
			tisb.Append("Mejico");
			Cache.LangProject.MainCountry.set_String(spanish.Handle, tisb.GetString());

			tisb = TsStringUtils.MakeIncStrBldr();
			tisb.SetIntPropValues((int)FwTextPropType.ktptWs, 0, english.Handle);
			tisb.Append("Saltillo");
			Cache.LangProject.FieldWorkLocation.set_String(english.Handle, tisb.GetString());

			Cache.LangProject.FieldWorkLocation.AppendAlternatives(Cache.LangProject.MainCountry);
			Assert.AreEqual("Saltillo Mexico", Cache.LangProject.FieldWorkLocation.get_String(english.Handle).Text);
			Assert.AreEqual("Mejico", Cache.LangProject.FieldWorkLocation.get_String(spanish.Handle).Text);

			tisb = TsStringUtils.MakeIncStrBldr();
			tisb.SetIntPropValues((int)FwTextPropType.ktptWs, 0, spanish.Handle);
			tisb.Append("Saltillo");
			Cache.LangProject.FieldWorkLocation.set_String(spanish.Handle, tisb.GetString());

			((ITsMultiString)Cache.LangProject.MainCountry).set_String(english.Handle, null);

			Cache.LangProject.FieldWorkLocation.AppendAlternatives(Cache.LangProject.MainCountry);
			Assert.AreEqual("Saltillo Mexico", Cache.LangProject.FieldWorkLocation.get_String(english.Handle).Text);
			Assert.AreEqual("Saltillo Mejico", Cache.LangProject.FieldWorkLocation.get_String(spanish.Handle).Text);
		}
	}

	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Summary description for StringsTests.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[TestFixture]
	public class StringsTests : MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{
		#region data members
		IMultiAccessorBase m_multi;
		IText m_text;
		private CoreWritingSystemDefinition m_wsGerman;
		private CoreWritingSystemDefinition m_wsFrench;
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
			Cache.ServiceLocator.WritingSystemManager.GetOrSet("fr", out m_wsFrench);
			Cache.ServiceLocator.WritingSystemManager.GetOrSet("es", out m_wsSpanish);
			NonUndoableUnitOfWorkHelper.Do(m_actionHandler, () =>
			{
				ChangeDefaultAnalWs(m_wsGerman);
				Cache.LangProject.AddToCurrentAnalysisWritingSystems(m_wsSpanish);
			});
	}

		/// ----------------------------------------------------------------------------------------
		/// <summary>
		/// Create a MultiUnicodeAccessor
		/// </summary>
		/// ----------------------------------------------------------------------------------------
		protected override void CreateTestData()
		{
			m_text = Cache.ServiceLocator.GetInstance<ITextFactory>().Create();
			//Cache.LangProject.TextsOC.Add(m_text);
			IStText stText = Cache.ServiceLocator.GetInstance<IStTextFactory>().Create();
			m_text.ContentsOA = stText;
			m_multi = stText.Title;
		}

		/// ----------------------------------------------------------------------------------------
		/// <summary>
		/// Test of the GetBestAnalysisAlternative method when the MultiUnicodeAccessor has an
		/// alternative stored for the default analysis WS.
		/// </summary>
		/// ----------------------------------------------------------------------------------------
		[Test]
		public void TestGetBestAnalysisAlternative_DefaultAnalExists()
		{
			m_text.Name.SetAnalysisDefaultWritingSystem("Hallo");
			m_text.Name.set_String(m_wsSpanish.Handle, "Hola");
			m_text.Name.SetUserWritingSystem("YeeHaw");
			m_text.Name.set_String(m_wsSpanish.Handle, "Hello");
			Assert.AreEqual("Hallo", m_multi.BestAnalysisAlternative.Text);
		}

		/// ----------------------------------------------------------------------------------------
		/// <summary>
		/// Test of the GetBestAnalysisAlternative method when the MultiUnicodeAccessor has an
		/// alternative stored for the default analysis WS.
		/// </summary>
		/// ----------------------------------------------------------------------------------------
		[Test]
		public void TestGetBestAnalysisAlternative_EnglishExists()
		{
			m_text.Name.set_String(m_wsSpanish.Handle, "Hello");
			Assert.AreEqual("Hello", m_multi.BestAnalysisAlternative.Text);
		}

		/// ----------------------------------------------------------------------------------------
		/// <summary>
		/// Test of the GetBestAnalysisAlternative method when the MultiUnicodeAccessor has no
		/// alternatives stored.
		/// </summary>
		/// ----------------------------------------------------------------------------------------
		[Test]
		public void TestGetBestAnalysisAlternative_NoAlternativesExist()
		{
			Assert.AreEqual("***", m_multi.BestAnalysisAlternative.Text);
		}

		/// ----------------------------------------------------------------------------------------
		/// <summary>
		/// Test of the GetBestAnalysisAlternative method when the MultiUnicodeAccessor has an
		/// alternative stored analysis WS's other than the default.
		/// </summary>
		/// ----------------------------------------------------------------------------------------
		[Test]
		public void TestGetBestAnalysisAlternative_OtherAnalExists()
		{
			m_text.Name.set_String(m_wsSpanish.Handle, "Hola");
			m_text.Name.SetUserWritingSystem("YeeHaw");
			m_text.Name.set_String(Cache.ServiceLocator.WritingSystemManager.GetWsFromStr("en"), "Hello");
			Assert.AreEqual("Hola", m_multi.BestAnalysisAlternative.Text);
		}

		/// ----------------------------------------------------------------------------------------
		/// <summary>
		/// Test of the GetBestAnalysisAlternative method when the MultiUnicodeAccessor has an
		/// alternative stored for the UI writing system, but none of the analysis WS's.
		/// </summary>
		/// ----------------------------------------------------------------------------------------
		[Test]
		public void TestGetBestAnalysisAlternative_UIExists()
		{
			m_text.Name.SetUserWritingSystem("YeeHaw");
			m_text.Name.set_String(m_wsFrench.Handle, "Hello");
			Assert.AreEqual("YeeHaw", m_multi.BestAnalysisAlternative.Text);
		}
	}
}
