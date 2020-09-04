// Copyright (c) 2004-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Xml;
using NUnit.Framework;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.Utils;

namespace SIL.LCModel.Core.Text
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// TsStringUtils tests.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[TestFixture]
	[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Our test methods have their own naming convention")]
	public class TsStringUtilsTests
	{
		private ILgWritingSystemFactory m_wsf;

		#region Setup and Teardown
		/// ------------------------------------------------------------------------------------
		/// <summary>
		///
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[TestFixtureSetUp]
		public void TestFixtureSetup()
		{
			m_wsf = new WritingSystemManager();
		}

		#endregion

		#region Get(Owned)GuidFromRun tests
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests getting a Guid from a structured text string when there is an OwnNameGuidHot
		/// ORC.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetOwnedGuidFromRun_WithOwnNameGuidHotORC()
		{
			var testGuid = Guid.NewGuid();
			var tss = TsStringUtils.CreateOrcFromGuid(testGuid,
				FwObjDataTypes.kodtOwnNameGuidHot, 1);
			FwObjDataTypes odt;

			var returnGuid = TsStringUtils.GetOwnedGuidFromRun(tss, 0, out odt);

			Assert.AreEqual(testGuid, returnGuid);
			Assert.AreEqual(FwObjDataTypes.kodtOwnNameGuidHot, odt);
			int var;
			var ws = tss.get_Properties(0).GetIntPropValues((int)FwTextPropType.ktptWs, out var);
			Assert.AreEqual(1, ws);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests getting a Guid from a structured text string when there is a
		/// GuidMoveableObjDisp ORC.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetOwnedGuidFromRun_WithGuidMoveableObjDispORC()
		{
			var testGuid = Guid.NewGuid();
			var tss = TsStringUtils.CreateOrcFromGuid(testGuid,
				FwObjDataTypes.kodtGuidMoveableObjDisp, 1);
			FwObjDataTypes odt;

			var returnGuid = TsStringUtils.GetOwnedGuidFromRun(tss, 0, out odt);

			Assert.AreEqual(testGuid, returnGuid);
			Assert.AreEqual(FwObjDataTypes.kodtGuidMoveableObjDisp, odt);
			int var;
			var ws = tss.get_Properties(0).GetIntPropValues((int)FwTextPropType.ktptWs, out var);
			Assert.AreEqual(1, ws);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests not :-) getting a Guid from a structured text string when there isn't an
		/// owned ORC.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetOwnedGuidFromRun_ORCForUnownedObject()
		{
			var testGuid = Guid.NewGuid();
			var tss = TsStringUtils.CreateOrcFromGuid(testGuid,
				FwObjDataTypes.kodtPictOddHot, 1);
			FwObjDataTypes odt;

			var returnGuid = TsStringUtils.GetOwnedGuidFromRun(tss, 0, out odt);

			Assert.AreEqual(Guid.Empty, returnGuid);
			int var;
			var ws = tss.get_Properties(0).GetIntPropValues((int)FwTextPropType.ktptWs, out var);
			Assert.AreEqual(1, ws);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests getting a Guid from a structured text string when the ORC is the type
		/// requested.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetGuidFromRun_ORCMatchesSpecifiedType()
		{
			var testGuid = Guid.NewGuid();
			var tss = TsStringUtils.CreateOrcFromGuid(testGuid,
				FwObjDataTypes.kodtOwnNameGuidHot, 1);

			var returnGuid = TsStringUtils.GetGuidFromRun(tss, 0,
				FwObjDataTypes.kodtOwnNameGuidHot);

			Assert.AreEqual(testGuid, returnGuid);
			int var;
			var ws = tss.get_Properties(0).GetIntPropValues((int)FwTextPropType.ktptWs, out var);
			Assert.AreEqual(1, ws);
		}
		#endregion

		#region ORC-handling tests
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests getting owned ORCs from a structured text string.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetOwnedORCs_TssHasORCs()
		{
			// Create two owned ORCs
			var testGuid1 = Guid.NewGuid();
			var testGuid2 = Guid.NewGuid();
			var tssORC1 = TsStringUtils.CreateOrcFromGuid(testGuid1, FwObjDataTypes.kodtOwnNameGuidHot, 1);
			var tssORC2 = TsStringUtils.CreateOrcFromGuid(testGuid2, FwObjDataTypes.kodtOwnNameGuidHot, 1);

			// Embed the ORCs in an ITsString
			ITsStrBldr tssBldr = TsStringUtils.MakeStrBldr();
			var plainProps = StyleUtils.CharStyleTextProps(null, 1);
			tssBldr.ReplaceRgch(0, 0, "String start", 12, plainProps);
			tssBldr.ReplaceTsString(tssBldr.Length, tssBldr.Length, tssORC1);
			tssBldr.ReplaceRgch(tssBldr.Length, tssBldr.Length, " middle", 7, plainProps);
			tssBldr.ReplaceTsString(tssBldr.Length, tssBldr.Length, tssORC2);
			tssBldr.ReplaceRgch(tssBldr.Length, tssBldr.Length, " End", 4, plainProps);
			var tss = tssBldr.GetString();
			Assert.AreEqual("String start" + StringUtils.kChObject + " middle" + StringUtils.kChObject + " End", tss.Text);
			Assert.AreEqual(5, tss.RunCount);

			// Test GetOwnedORCs
			var orcTss = TsStringUtils.GetOwnedORCs(tss);

			// Confirm that the ORCs were returned correctly.
			Assert.AreEqual(2, orcTss.Length);
			Assert.AreEqual(StringUtils.kChObject.ToString() + StringUtils.kChObject, orcTss.Text);
			Assert.AreEqual(testGuid1, TsStringUtils.GetGuidFromRun(orcTss, 0));
			Assert.AreEqual(testGuid2, TsStringUtils.GetGuidFromRun(orcTss, 1));
			int var;
			var ws = orcTss.get_Properties(0).GetIntPropValues((int)FwTextPropType.ktptWs, out var);
			Assert.AreEqual(1, ws);

			ws = orcTss.get_Properties(1).GetIntPropValues((int)FwTextPropType.ktptWs, out var);
			Assert.AreEqual(1, ws);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests removing owned ORCs from a structured text string.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetCleanTsString_TssHasORCs()
		{
			// Create two owned ORCs
			var testGuid1 = Guid.NewGuid();
			var testGuid2 = Guid.NewGuid();
			var tssORC1 = TsStringUtils.CreateOrcFromGuid(testGuid1, FwObjDataTypes.kodtOwnNameGuidHot, 1);
			var tssORC2 = TsStringUtils.CreateOrcFromGuid(testGuid2, FwObjDataTypes.kodtOwnNameGuidHot, 1);

			// Embed the ORCs in an ITsString
			ITsStrBldr tssBldr = TsStringUtils.MakeStrBldr();
			var plainProps = StyleUtils.CharStyleTextProps(null, 1);
			tssBldr.ReplaceRgch(0, 0, "String start", 12, plainProps);
			tssBldr.ReplaceTsString(tssBldr.Length, tssBldr.Length, tssORC1);
			tssBldr.ReplaceRgch(tssBldr.Length, tssBldr.Length, " middle", 7, plainProps);
			tssBldr.ReplaceTsString(tssBldr.Length, tssBldr.Length, tssORC2);
			tssBldr.ReplaceRgch(tssBldr.Length, tssBldr.Length, " End", 4, plainProps);
			var tss = tssBldr.GetString();
			Assert.AreEqual("String start" + StringUtils.kChObject + " middle" + StringUtils.kChObject + " End", tss.Text);
			Assert.AreEqual(5, tss.RunCount);

			// Test RemoveOwnedORCs
			var noORCText = TsStringUtils.GetCleanTsString(tss, null);

			// Confirm that the ORCs were removed.
			Assert.IsFalse(noORCText.Text.Contains(new string(StringUtils.kChObject, 1)));
			Assert.AreEqual("String start middle End", noORCText.Text);
			Assert.AreEqual(1, noORCText.RunCount);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests GetCleanTsString when an ITsString begins and ends with spaces and
		/// contains an ORC. Just within the spaces are numbers and punctuation (TE-7795).
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetCleanTsString_NumbersWithinSpaces()
		{
			// Create two owned ORCs
			var testGuid1 = Guid.NewGuid();
			var testGuid2 = Guid.NewGuid();
			var tssORC1 = TsStringUtils.CreateOrcFromGuid(testGuid1, FwObjDataTypes.kodtOwnNameGuidHot, 1);
			var tssORC2 = TsStringUtils.CreateOrcFromGuid(testGuid2, FwObjDataTypes.kodtOwnNameGuidHot, 1);

			// Embed the ORCs in an ITsString
			ITsStrBldr tssBldr = TsStringUtils.MakeStrBldr();
			var plainProps = StyleUtils.CharStyleTextProps(null, 1);
			tssBldr.Replace(0, 0, " 55String start", plainProps);
			tssBldr.ReplaceTsString(tssBldr.Length, tssBldr.Length, tssORC1);
			tssBldr.Replace(tssBldr.Length, tssBldr.Length, " middle", plainProps);
			tssBldr.ReplaceTsString(tssBldr.Length, tssBldr.Length, tssORC2);
			tssBldr.Replace(tssBldr.Length, tssBldr.Length, "End!22 ", plainProps);
			var tss = tssBldr.GetString();
			Assert.AreEqual(" 55String start" + StringUtils.kChObject + " middle" + StringUtils.kChObject + "End!22 ", tss.Text);
			Assert.AreEqual(5, tss.RunCount);

			var tssORCsRemoved = TsStringUtils.GetCleanTsString(tss, null);

			// We expect that the text would include the numbers, but not the leading and trailing spaces
			// nor the ORCs.
			Assert.AreEqual("55String start middleEnd!22", tssORCsRemoved.Text);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests GetCleanTsString when an ITsString consists of a single space (TE-7795).
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetCleanTsString_SingleSpace()
		{
			var tssClean = TsStringUtils.GetCleanTsString(TsStringUtils.MakeString(" ", 42), null);
			Assert.AreEqual(0, tssClean.Length);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests GetCleanTsString when an ITsString is initially empty.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetCleanTsString_Empty()
		{
			var tssClean = TsStringUtils.GetCleanTsString(
				TsStringUtils.MakeString(String.Empty, 42), null);
			Assert.AreEqual(0, tssClean.Length);
			Assert.AreEqual(42, tssClean.get_WritingSystemAt(0));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests GetCleanTsString when an ITsString consists of a single ORC.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetCleanTsString_SingleUnknownORC_Remove()
		{
			var tssClean = TsStringUtils.GetCleanTsString(
				TsStringUtils.MakeString(StringUtils.kChObject.ToString(), 42), null);
			Assert.AreEqual(0, tssClean.Length);
			Assert.AreEqual(42, tssClean.get_WritingSystemAt(0));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests GetCleanTsString when an ITsString consists of a single ORC.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetCleanTsString_SingleUnknownORC_Preserve()
		{
			var tss = TsStringUtils.MakeString(StringUtils.kChObject.ToString(), 42);
			var tssClean = TsStringUtils.GetCleanTsString(tss, null, false, true, false);
			Assert.AreEqual(1, tssClean.Length);
			Assert.AreEqual(StringUtils.kChObject, tssClean.get_RunText(0)[0]);
			Assert.AreEqual(42, tssClean.get_WritingSystemAt(0));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests GetCleanTsString with a null ITsString (TE-8225).
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetCleanTsString_NullTsString()
		{
			Assert.IsNull(TsStringUtils.GetCleanTsString(null));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests getting a Guid from a structured text string when the ORC is not the type
		/// requested.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetGuidFromRun_ORCDoesNotMatchSpecifiedType()
		{
			var testGuid = Guid.NewGuid();
			var tss = TsStringUtils.CreateOrcFromGuid(testGuid,
				FwObjDataTypes.kodtOwnNameGuidHot, 1);

			var returnGuid = TsStringUtils.GetGuidFromRun(tss, 0,
				FwObjDataTypes.kodtGuidMoveableObjDisp);

			Assert.AreEqual(Guid.Empty, returnGuid);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests getting a Guid from a structured text string when there is an owning ORC.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetGuidFromRun_WithOwningORC()
		{
			var testGuid = Guid.NewGuid();
			var tss = TsStringUtils.CreateOrcFromGuid(testGuid,
				FwObjDataTypes.kodtOwnNameGuidHot, 1);

			var returnGuid = TsStringUtils.GetGuidFromRun(tss, 0);
			Assert.AreEqual(testGuid, returnGuid);
			int var;
			var ws = tss.get_Properties(0).GetIntPropValues((int)FwTextPropType.ktptWs, out var);
			Assert.AreEqual(1, ws);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests getting a Guid from a structured text string when there is a reference ORC.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetGuidFromRun_WithRefORC()
		{
			var testGuid = Guid.NewGuid();
			var tss = TsStringUtils.CreateOrcFromGuid(testGuid,
				FwObjDataTypes.kodtNameGuidHot, 1);

			var returnGuid = TsStringUtils.GetGuidFromRun(tss, 0);
			Assert.AreEqual(testGuid, returnGuid);
			int var;
			var ws = tss.get_Properties(0).GetIntPropValues((int)FwTextPropType.ktptWs, out var);
			Assert.AreEqual(1, ws);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests not :-) getting a Guid from a structured text string when there isn't any
		/// ORC.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetGuidFromRun_NoORC()
		{
			var tss = TsStringUtils.MakeString("This string has no ORCS", 1);

			var returnGuid = TsStringUtils.GetGuidFromRun(tss, 0);
			Assert.AreEqual(Guid.Empty, returnGuid);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the TurnOwnedOrcIntoUnownedOrc with a kodtOwnNameGuidHot ORC type
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void TurnOwnedOrcIntoUnownedOrc_OwnedOrc_Run0()
		{
			var expectedGuid = Guid.NewGuid();
			ITsStrBldr bldr = TsStringUtils.MakeStrBldr();
			TsStringUtils.InsertOrcIntoPara(expectedGuid, FwObjDataTypes.kodtOwnNameGuidHot, bldr, 0, 0, 5);
			TsStringUtils.TurnOwnedOrcIntoUnownedOrc(bldr, 0);

			FwObjDataTypes odt;
			var guid = TsStringUtils.GetGuidFromProps(bldr.get_Properties(0), null, out odt);
			Assert.AreEqual(1, bldr.RunCount);
			Assert.AreEqual(StringUtils.kChObject.ToString(), bldr.Text);
			Assert.AreEqual(1, bldr.get_Properties(0).StrPropCount);
			Assert.AreEqual(1, bldr.get_Properties(0).IntPropCount);
			Assert.AreEqual(expectedGuid, guid);
			Assert.AreEqual(FwObjDataTypes.kodtNameGuidHot, odt);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the TurnOwnedOrcIntoUnownedOrc with a kodtOwnNameGuidHot ORC type in a run
		/// greater than zero
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void TurnOwnedOrcIntoUnownedOrc_OwnedOrc_DifferentRun()
		{
			var expectedGuid = Guid.NewGuid();
			ITsStrBldr bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "monkey", StyleUtils.CharStyleTextProps(null, 5));
			TsStringUtils.InsertOrcIntoPara(expectedGuid, FwObjDataTypes.kodtOwnNameGuidHot, bldr, 6, 6, 5);
			TsStringUtils.TurnOwnedOrcIntoUnownedOrc(bldr, 1);

			Assert.AreEqual(2, bldr.RunCount);
			Assert.AreEqual("monkey" + StringUtils.kChObject, bldr.Text);
			Assert.AreEqual(0, bldr.get_Properties(0).StrPropCount);
			Assert.AreEqual(1, bldr.get_Properties(0).IntPropCount);

			FwObjDataTypes odt;
			var guid = TsStringUtils.GetGuidFromProps(bldr.get_Properties(1), null, out odt);
			Assert.AreEqual(1, bldr.get_Properties(1).StrPropCount);
			Assert.AreEqual(1, bldr.get_Properties(1).IntPropCount);
			Assert.AreEqual(expectedGuid, guid);
			Assert.AreEqual(FwObjDataTypes.kodtNameGuidHot, odt);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the TurnOwnedOrcIntoUnownedOrc with a kodtNameGuidHot ORC type -- should be no
		/// change
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void TurnOwnedOrcIntoUnownedOrc_UnownedOrc()
		{
			var expectedGuid = Guid.NewGuid();
			ITsStrBldr bldr = TsStringUtils.MakeStrBldr();
			TsStringUtils.InsertOrcIntoPara(expectedGuid, FwObjDataTypes.kodtNameGuidHot, bldr, 0, 0, 5);
			TsStringUtils.TurnOwnedOrcIntoUnownedOrc(bldr, 0);

			FwObjDataTypes odt;
			var guid = TsStringUtils.GetGuidFromProps(bldr.get_Properties(0), null, out odt);
			Assert.AreEqual(1, bldr.RunCount);
			Assert.AreEqual(StringUtils.kChObject.ToString(), bldr.Text);
			Assert.AreEqual(1, bldr.get_Properties(0).StrPropCount);
			Assert.AreEqual(1, bldr.get_Properties(0).IntPropCount);
			Assert.AreEqual(expectedGuid, guid);
			Assert.AreEqual(FwObjDataTypes.kodtNameGuidHot, odt);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the TurnOwnedOrcIntoUnownedOrc with a kodtGuidMoveableObjDisp ORC type -- need
		/// to decide what this should do, since for now we don't have an owned/unowned
		/// distinction for this kind of ORC.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void TurnOwnedOrcIntoUnownedOrc_PictureOrc()
		{
			var expectedGuid = Guid.NewGuid();
			ITsStrBldr bldr = TsStringUtils.MakeStrBldr();
			TsStringUtils.InsertOrcIntoPara(expectedGuid, FwObjDataTypes.kodtGuidMoveableObjDisp, bldr, 0, 0, 5);
			TsStringUtils.TurnOwnedOrcIntoUnownedOrc(bldr, 0);

			FwObjDataTypes odt;
			var guid = TsStringUtils.GetGuidFromProps(bldr.get_Properties(0), null, out odt);
			Assert.AreEqual(1, bldr.RunCount);
			Assert.AreEqual(StringUtils.kChObject.ToString(), bldr.Text);
			Assert.AreEqual(1, bldr.get_Properties(0).StrPropCount);
			Assert.AreEqual(1, bldr.get_Properties(0).IntPropCount);
			Assert.AreEqual(expectedGuid, guid);
			Assert.AreEqual(FwObjDataTypes.kodtGuidMoveableObjDisp, odt);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the TurnOwnedOrcIntoUnownedOrc on a run with no ORC -- should be no change
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void TurnOwnedOrcIntoUnownedOrc_NoOrc()
		{
			ITsStrBldr bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "test", StyleUtils.CharStyleTextProps(null, 5));
			TsStringUtils.TurnOwnedOrcIntoUnownedOrc(bldr, 0);

			Assert.AreEqual(1, bldr.RunCount);
			Assert.AreEqual("test", bldr.Text);
			Assert.AreEqual(0, bldr.get_Properties(0).StrPropCount);
			Assert.AreEqual(1, bldr.get_Properties(0).IntPropCount);
		}
		#endregion

		#region Intprop-related tests
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the RemoveIntProp to remove a font size property.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void RemoveIntProp()
		{
			// Set up a ITsString with font property set.
			ITsStrBldr tssBldr = TsStringUtils.MakeStrBldr();
			ITsPropsBldr tppBldr = TsStringUtils.MakePropsBldr();
			tppBldr.SetIntPropValues((int)FwTextPropType.ktptFontSize, (int)FwTextPropVar.ktpvMilliPoint, 9250);
			tppBldr.SetIntPropValues((int)FwTextPropType.ktptWs, 0, 17);
			tssBldr.Replace(tssBldr.Length, tssBldr.Length, "This string has a font size property.", tppBldr.GetTextProps());
			var tss = tssBldr.GetString();
			// Confirm that the ITsString has the font size property set.
			int nvar;
			int value = tppBldr.GetIntPropValues((int)FwTextPropType.ktptWs, out nvar);
			Assert.AreEqual(17, value);
			Assert.IsTrue(FindIntPropInTss(tss, (int)FwTextPropType.ktptFontSize));

			var newTss = TsStringUtils.RemoveIntProp(tss, (int)FwTextPropType.ktptFontSize);

			// Confirm that the ITsString has had the font size property removed.
			Assert.IsFalse(FindIntPropInTss(newTss, (int)FwTextPropType.ktptFontSize));
			// Confirm that the writing system property is still the same.
			value = newTss.get_PropertiesAt(0).GetBldr().GetIntPropValues((int)FwTextPropType.ktptWs, out nvar);
			Assert.AreEqual(17, value);
			Assert.AreEqual("This string has a font size property.", newTss.Text);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the RemoveIntProp to remove a font size property when the string is empty.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void RemoveIntProp_EmptyTss()
		{
			// Set up a ITsString with font property set.
			ITsStrBldr tssBldr = TsStringUtils.MakeStrBldr();
			ITsPropsBldr tppBldr = TsStringUtils.MakePropsBldr();
			tppBldr.SetIntPropValues((int)FwTextPropType.ktptFontSize, (int)FwTextPropVar.ktpvMilliPoint, 9250);
			tppBldr.SetIntPropValues((int)FwTextPropType.ktptWs, 0, 17);
			tssBldr.Replace(0, 0, string.Empty, tppBldr.GetTextProps());
			var tss = tssBldr.GetString();
			// Confirm that the ITsString has the font size property set.
			int nvar;
			int value = tppBldr.GetIntPropValues((int)FwTextPropType.ktptWs, out nvar);
			Assert.AreEqual(17, value);
			Assert.IsTrue(FindIntPropInTss(tss, (int)FwTextPropType.ktptFontSize));

			var newTss = TsStringUtils.RemoveIntProp(tss, (int)FwTextPropType.ktptFontSize);

			// Confirm that the ITsString has had the font size property removed.
			Assert.IsFalse(FindIntPropInTss(newTss, (int)FwTextPropType.ktptFontSize));
			// Confirm that the writing system property is still the same.
			value = newTss.get_PropertiesAt(0).GetBldr().GetIntPropValues((int)FwTextPropType.ktptWs, out nvar);
			Assert.AreEqual(17, value);
			Assert.IsNull(newTss.Text);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Determines if the specified integer property is used in the specified ITsString.
		/// </summary>
		/// <param name="tss">The ITsString.</param>
		/// <param name="intProp"></param>
		/// <returns><c>true</c> if any run in the tss uses the specified property; <c>false</c>
		/// otherwise</returns>
		/// ------------------------------------------------------------------------------------
		private bool FindIntPropInTss(ITsString tss, int intProp)
		{
			for (var iRun = 0; iRun < tss.RunCount; iRun++)
			{
				// Check the integer properties of each run.
				var tpp = tss.get_PropertiesAt(iRun);

				for (var iProp = 0; iProp < tpp.IntPropCount; iProp++)
				{
					int var;
					int propType;
					tpp.GetIntProp(iProp, out propType, out var);
					if (propType == intProp)
						return true;
				}
			}
			return false;
		}
		#endregion

		#region TrimNonWordFormingChars tests
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test that non-word forming characters are trimmed from a character string.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void TrimNonWordFormingCharsTest()
		{
			Assert.AreEqual("angel", TrimNonWordFormingChars("angel", m_wsf));
			Assert.AreEqual(null, TrimNonWordFormingChars(string.Empty, m_wsf));
			Assert.AreEqual(null, TrimNonWordFormingChars("123.90", m_wsf));
			Assert.AreEqual("angel", TrimNonWordFormingChars("angel!", m_wsf));
			Assert.AreEqual("angel", TrimNonWordFormingChars(" : angel!", m_wsf));
			Assert.AreEqual("angel", TrimNonWordFormingChars(":angel!", m_wsf));
			Assert.AreEqual("angel", TrimNonWordFormingChars("!angel : ", m_wsf));
			Assert.AreEqual("angel", TrimNonWordFormingChars("1angel2", m_wsf));
			Assert.AreEqual("angel baby", TrimNonWordFormingChars("angel baby", m_wsf));
			Assert.AreEqual("angel", TrimNonWordFormingChars("angel" + StringUtils.kChObject, m_wsf));
			Assert.AreEqual("angel\uFF40", TrimNonWordFormingChars("{angel\uFF40}", m_wsf));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test that non-word forming characters are trimmed from a character string when the
		/// tss contains a newline character (which has a magic WS). (TE-8335)
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void TrimNonWordFormingCharsTest_WithNewLine()
		{
			var ws = m_wsf.get_Engine("en");
			ITsStrBldr bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "This is my text", StyleUtils.CharStyleTextProps(null, ws.Handle));
			bldr.Replace(0, 0, Environment.NewLine, StyleUtils.CharStyleTextProps(null, -1));

			var result = TsStringUtils.TrimNonWordFormingChars(bldr.GetString(), m_wsf);
			Assert.AreEqual("This is my text", result.Text);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test that non-word forming characters are trimmed from the start of a character string.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void TrimNonWordFormingCharsTest_AtStart()
		{
			Assert.AreEqual("angel", TrimNonWordFormingChars("angel", m_wsf, true, false));
			Assert.AreEqual(null, TrimNonWordFormingChars("123.90", m_wsf, true, false));
			Assert.AreEqual("angel!", TrimNonWordFormingChars("angel!", m_wsf, true, false));
			Assert.AreEqual("angel!", TrimNonWordFormingChars(" : angel!", m_wsf, true, false));
			Assert.AreEqual("angel!", TrimNonWordFormingChars(":angel!", m_wsf, true, false));
			Assert.AreEqual("angel : ", TrimNonWordFormingChars("!angel : ", m_wsf, true, false));
			Assert.AreEqual("angel2", TrimNonWordFormingChars("1angel2", m_wsf, true, false));
			Assert.AreEqual("angel" + StringUtils.kChObject, TrimNonWordFormingChars(
								StringUtils.kChObject + "angel" + StringUtils.kChObject, m_wsf, true, false));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test that non-word forming characters are trimmed from the end of a character string.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void TrimNonWordFormingCharsTest_AtEnd()
		{
			Assert.AreEqual("a", TrimNonWordFormingChars("a ", m_wsf, false, true));
			Assert.AreEqual("angel", TrimNonWordFormingChars("angel", m_wsf, false, true));
			Assert.AreEqual(null, TrimNonWordFormingChars("123.90", m_wsf, false, true));
			Assert.AreEqual("angel", TrimNonWordFormingChars("angel!", m_wsf, false, true));
			Assert.AreEqual(" : angel", TrimNonWordFormingChars(" : angel!", m_wsf, false, true));
			Assert.AreEqual(":angel", TrimNonWordFormingChars(":angel!", m_wsf, false, true));
			Assert.AreEqual("!angel", TrimNonWordFormingChars("!angel : ", m_wsf, false, true));
			Assert.AreEqual("1angel", TrimNonWordFormingChars("1angel2", m_wsf, false, true));
			Assert.AreEqual(StringUtils.kChObject + "angel", TrimNonWordFormingChars(
								StringUtils.kChObject + "angel" + StringUtils.kChObject, m_wsf, false, true));
		}

		bool FindWordFormInString(string wordForm, string source,
			ILgWritingSystemFactory wsf, out int ichMin, out int ichLim)
		{
			var ws = wsf.get_Engine("en").Handle;
			var tssWordForm = TsStringUtils.MakeString(wordForm, ws);
			var tssSource = TsStringUtils.MakeString(source, ws);
			return TsStringUtils.FindWordFormInString(tssWordForm, tssSource, wsf, out ichMin, out ichLim);
		}
		#endregion

		#region FindWordFormInString tests
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordFormInString method - basic test
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordFormInString_Basic()
		{
			int ichStart, ichEnd;
			// single word when it is the only thing in the string
			Assert.IsTrue(FindWordFormInString("Hello", "Hello", m_wsf, out ichStart, out ichEnd));
			Assert.AreEqual(0, ichStart);
			Assert.AreEqual(5, ichEnd);

			// single word in the middle
			Assert.IsTrue(FindWordFormInString("hello", "Say hello to someone you know.", m_wsf, out ichStart, out ichEnd));
			Assert.AreEqual(4, ichStart);
			Assert.AreEqual(9, ichEnd);

			// single word at the start
			Assert.IsTrue(FindWordFormInString("hello", "hello there", m_wsf, out ichStart, out ichEnd));
			Assert.AreEqual(0, ichStart);
			Assert.AreEqual(5, ichEnd);

			// single word at the end
			Assert.IsTrue(FindWordFormInString("hello", "hey, hello", m_wsf, out ichStart, out ichEnd));
			Assert.AreEqual(5, ichStart);
			Assert.AreEqual(10, ichEnd);

			// word does not exist
			Assert.IsFalse(FindWordFormInString("hello", "What? I can't hear you!", m_wsf, out ichStart, out ichEnd));

			// word does not match case
			Assert.IsFalse(FindWordFormInString("say", "Say hello to someone you know.", m_wsf, out ichStart, out ichEnd));

			// word occurs as the start of another word
			Assert.IsFalse(FindWordFormInString("me", "I meant to say hello.", m_wsf, out ichStart, out ichEnd));

			// word occurs as the end of another word
			Assert.IsFalse(FindWordFormInString("me", "I want to go home", m_wsf, out ichStart, out ichEnd));

			// word occurs in the middle of another word
			Assert.IsFalse(FindWordFormInString("me", "I say amen!", m_wsf, out ichStart, out ichEnd));

			// word occurs in the middle of another word, then later as a stand-alone word
			Assert.IsTrue(FindWordFormInString("me", "I say amen and me!", m_wsf, out ichStart, out ichEnd));
			Assert.AreEqual(15, ichStart);
			Assert.AreEqual(17, ichEnd);

			// empty source string
			Assert.IsFalse(FindWordFormInString("me", string.Empty, m_wsf, out ichStart, out ichEnd));

			// empty word form string
			Assert.IsFalse(FindWordFormInString(string.Empty, "I say amen!", m_wsf, out ichStart, out ichEnd));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordFormInString method when the wordform contains punctuation.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordFormInString_PunctuationInWordForm()
		{
			int ichStart, ichEnd;
			// word has word-forming "punctuation"
			Assert.IsTrue(FindWordFormInString("what's", "hello, what's your name?", m_wsf, out ichStart, out ichEnd));
			Assert.AreEqual(7, ichStart);
			Assert.AreEqual(13, ichEnd);

			// wordform with non word-forming medial (the only kind allowed) punctuation
			Assert.IsTrue(FindWordFormInString("ngel-baby", "Hello there, @ngel-baby!", m_wsf, out ichStart, out ichEnd));
			Assert.AreEqual(14, ichStart);
			Assert.AreEqual(23, ichEnd);

			// wordform with non-matching punctuation
			Assert.IsFalse(FindWordFormInString("ngel-baby", "Hello there, ngel=baby!", m_wsf, out ichStart, out ichEnd));

			// wordform with non-matching punctuation
			Assert.IsFalse(FindWordFormInString("ngel-baby", "Hello there, ngel-=-baby!", m_wsf, out ichStart, out ichEnd));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordFormInString method when the source contains punctuation next to
		/// the matching form.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordFormInString_PunctuationInSource()
		{
			int ichStart, ichEnd;

			// single word with punctuation at end of word
			Assert.IsTrue(FindWordFormInString("hello", "hello, I am fine", m_wsf, out ichStart, out ichEnd));
			Assert.AreEqual(0, ichStart);
			Assert.AreEqual(5, ichEnd);

			// single word with punctuation at beginning of word
			Assert.IsTrue(FindWordFormInString("hello", "\"hello shmello,\" said Bill.", m_wsf, out ichStart, out ichEnd));
			Assert.AreEqual(1, ichStart);
			Assert.AreEqual(6, ichEnd);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordFormInString method when the word form consists of multiple words.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordFormInString_MultipleWordWordForm()
		{
			int ichStart, ichEnd;

			// multiple words
			Assert.IsTrue(FindWordFormInString("hello there", "Well, hello there, who are you?", m_wsf, out ichStart, out ichEnd));
			Assert.AreEqual(6, ichStart);
			Assert.AreEqual(17, ichEnd);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordFormInString method when the source strings that have different
		/// normalized forms.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordFormInString_DifferentNormalizedForms()
		{
			int ichStart, ichEnd;

			// searching for an accented E in a string that contains decomposed Unicode characters.
			Assert.IsTrue(FindWordFormInString("h\u00c9llo", "hE\u0301llo",
				m_wsf, out ichStart, out ichEnd));
			Assert.AreEqual(0, ichStart);
			Assert.AreEqual(6, ichEnd);

			// searching for an accented E with decomposed Unicode characters in a string that has it composed.
			Assert.IsTrue(FindWordFormInString("hE\u0301llo", "h\u00c9llo",
				m_wsf, out ichStart, out ichEnd));
			Assert.AreEqual(0, ichStart);
			Assert.AreEqual(5, ichEnd);

			// searching for non-matching diacritics (decomposed).
			Assert.IsFalse(FindWordFormInString("hE\u0301llo", "hE\u0300llo",
				m_wsf, out ichStart, out ichEnd));

			// searching for non-matching diacritics (composed).
			Assert.IsFalse(FindWordFormInString("h\u00c9llo", "hE\u00c8llo",
				m_wsf, out ichStart, out ichEnd));

			// searching for non-matching diacritics (wordform composed, source decomposed).
			Assert.IsFalse(FindWordFormInString("h\u00c9llo", "hE\u0300llo",
				m_wsf, out ichStart, out ichEnd));

			// searching for non-matching diacritics (wordform decomposed, source composed).
			Assert.IsFalse(FindWordFormInString("hE\u0300llo", "h\u00c9llo",
				m_wsf, out ichStart, out ichEnd));

			// searching for non-matching diacritics (decomposed) at end of source.
			Assert.IsFalse(FindWordFormInString("hE\u0301", "I say hE\u0300",
				m_wsf, out ichStart, out ichEnd));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordFormInString method when the source string contains ORCs.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordFormInString_WithORCs()
		{
			int ichStart, ichEnd;

			// single word with ORC at end of word (TE-3673)
			Assert.IsTrue(FindWordFormInString("hello", "hello" + StringUtils.kChObject,
				m_wsf, out ichStart, out ichEnd));
			Assert.AreEqual(0, ichStart);
			Assert.AreEqual(5, ichEnd);

			// single word with ORC embedded in word (TE-5309)
			Assert.IsTrue(FindWordFormInString("hello", "he" + StringUtils.kChObject + "llo",
				m_wsf, out ichStart, out ichEnd));
			Assert.AreEqual(0, ichStart);
			Assert.AreEqual(6, ichEnd);

			// multiple embedded ORCs in word (TE-5309)
			Assert.IsTrue(FindWordFormInString("hello", "first words, then he" +
				StringUtils.kChObject + "ll" + StringUtils.kChObject + "o" + StringUtils.kChObject,
				m_wsf, out ichStart, out ichEnd));
			Assert.AreEqual(18, ichStart);
			Assert.AreEqual(25, ichEnd);

			// single word with multiple embedded ORCs in word (TE-5309)
			Assert.IsTrue(FindWordFormInString("hello", StringUtils.kChObject + "he" +
				StringUtils.kChObject + "ll" + StringUtils.kChObject + "o" + StringUtils.kChObject,
				m_wsf, out ichStart, out ichEnd));
			Assert.AreEqual(1, ichStart);
			Assert.AreEqual(8, ichEnd);

			// multiple ORCs preceeding word
			Assert.IsTrue(FindWordFormInString("hello", StringUtils.kChObject + "first " +
				StringUtils.kChObject + "hello world", m_wsf, out ichStart, out ichEnd));
			Assert.AreEqual(8, ichStart);
			Assert.AreEqual(13, ichEnd);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordFormInString method when the source string contains multiple
		/// occurrences of the word form.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordFormInString_MultipleMatches()
		{
			int ichStart, ichEnd;

			Assert.IsTrue(FindWordFormInString("hello", "Say hello to someone who said hello to you.", m_wsf, out ichStart, out ichEnd));
			Assert.AreEqual(4, ichStart);
			Assert.AreEqual(9, ichEnd);
		}
		#endregion

		#region FindWordBoundary tests
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordBoundary method when ich is negative or greater than the length
		/// of the string.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordBoundary_IchOutOfRange()
		{
			ITsString tss = TsStringUtils.MakeString("funky munky", m_wsf.UserWs);
			Assert.Throws(typeof(ArgumentOutOfRangeException), () => tss.FindWordBoundary(4000));
			Assert.Throws(typeof(ArgumentOutOfRangeException), () => tss.FindWordBoundary(-1));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordBoundary method when already at the start of a word
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordBoundary_AlreadyAtStartOfWord()
		{
			ITsString tss = TsStringUtils.MakeString("A munky", m_wsf.UserWs);
			Assert.AreEqual(2, tss.FindWordBoundary(2));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordBoundary method when at the start of the string
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordBoundary_AtStartOfString()
		{
			ITsString tss = TsStringUtils.MakeString("Another munky", m_wsf.UserWs);
			Assert.AreEqual(0, tss.FindWordBoundary(0));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordBoundary method when at the end of the string
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordBoundary_AtEndOfString()
		{
			ITsString tss = TsStringUtils.MakeString("One guy", m_wsf.UserWs);
			Assert.AreEqual(7, tss.FindWordBoundary(7));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordBoundary method when in the middle of a word
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordBoundary_MiddleOfWord()
		{
			ITsString tss = TsStringUtils.MakeString("Happiness is good.", m_wsf.UserWs);
			Assert.AreEqual(0, tss.FindWordBoundary(4));
			Assert.AreEqual(13, tss.FindWordBoundary(tss.Length - 3));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordBoundary method when at the end of a word (in the middle of the
		/// string)
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordBoundary_EndOfWord()
		{
			ITsString tss = TsStringUtils.MakeString("Gold is good.", m_wsf.UserWs);
			Assert.AreEqual(5, tss.FindWordBoundary(4));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordBoundary method when around punctuation
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordBoundary_AroundPunctuation()
		{
			ITsString tss = TsStringUtils.MakeString("God 'is good.'", m_wsf.UserWs);
			Assert.AreEqual(tss.Length, tss.FindWordBoundary(tss.Length - 2));
			Assert.AreEqual(tss.Length, tss.FindWordBoundary(tss.Length - 1));
			Assert.AreEqual(tss.Length, tss.FindWordBoundary(tss.Length));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordBoundary method when at the end of a sentence (before sentence-
		/// ending punctuation)
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordBoundary_EndOfSentence()
		{
			ITsString tss = TsStringUtils.MakeString("Good. Yeah!", m_wsf.UserWs);
			Assert.AreEqual(6, tss.FindWordBoundary(4));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordBoundary method when around numbers
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordBoundary_AroundNumbers()
		{
			ITsString tss = TsStringUtils.MakeString("Gideon had 300 men.", m_wsf.UserWs);
			Assert.AreEqual(11, tss.FindWordBoundary(11));
			Assert.AreEqual(11, tss.FindWordBoundary(12));
			Assert.AreEqual(15, tss.FindWordBoundary(14));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordBoundary method when around a valid chapter number
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordBoundary_AroundChapterNumber_Valid()
		{
			ITsStrBldr bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "12", StyleUtils.CharStyleTextProps("Chap Num", m_wsf.UserWs));
			bldr.Replace(bldr.Length, bldr.Length, "Some text", StyleUtils.CharStyleTextProps(null, m_wsf.UserWs));
			ITsString tss = bldr.GetString();
			Assert.AreEqual(0, tss.FindWordBoundary(0, "Chap Num"), "Failed to find position following chapter number when ich == 0");
			for (var ich = 1; ich < 4; ich++)
				Assert.AreEqual(2, tss.FindWordBoundary(ich, "Chap Num"), "Failed to find position following chapter number when ich == " + ich);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordBoundary method when before a valid chapter number that is not at
		/// the start of the string
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordBoundary_BeforeChapterNumber_MidString()
		{
			ITsStrBldr bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "Preceding text. ", StyleUtils.CharStyleTextProps(null, m_wsf.UserWs));
			var ichEndOfPrecedingText = bldr.Length;
			bldr.Replace(bldr.Length, bldr.Length, "2", StyleUtils.CharStyleTextProps("Chap Num", m_wsf.UserWs));
			bldr.Replace(bldr.Length, bldr.Length, "Following text", StyleUtils.CharStyleTextProps(null, m_wsf.UserWs));
			ITsString tss = bldr.GetString();
			Assert.AreEqual(ichEndOfPrecedingText - 6, tss.FindWordBoundary(ichEndOfPrecedingText - 3, "Chap Num"));
			Assert.AreEqual(ichEndOfPrecedingText, tss.FindWordBoundary(ichEndOfPrecedingText - 2, "Chap Num"));
			Assert.AreEqual(ichEndOfPrecedingText, tss.FindWordBoundary(ichEndOfPrecedingText - 1, "Chap Num"));
			Assert.AreEqual(ichEndOfPrecedingText, tss.FindWordBoundary(ichEndOfPrecedingText, "Chap Num"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordBoundary method when around an invalid chapter number
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordBoundary_AroundChapterNumber_Invalid()
		{
			ITsStrBldr bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "a2b", StyleUtils.CharStyleTextProps("Chap Num", m_wsf.UserWs));
			bldr.Replace(bldr.Length, bldr.Length, "Some text", StyleUtils.CharStyleTextProps(null, m_wsf.UserWs));
			ITsString tss = bldr.GetString();
			Assert.AreEqual(0, tss.FindWordBoundary(0, "Chap Num"), "Failed to find position following invalid chapter number when ich == 0");
			for (var ich = 1; ich < 5; ich++)
				Assert.AreEqual(3, tss.FindWordBoundary(ich, "Chap Num"), "Failed to find position following invalid chapter number when ich == " + ich);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordBoundary method when bewteen a chapter number and a verse number
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordBoundary_BetweenChapterAndVerseNumbers()
		{
			ITsStrBldr bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "2", StyleUtils.CharStyleTextProps("Chap Num", m_wsf.UserWs));
			bldr.Replace(1, 1, "5", StyleUtils.CharStyleTextProps("Vers Num", m_wsf.UserWs));
			bldr.Replace(bldr.Length, bldr.Length, "Some text", StyleUtils.CharStyleTextProps(null, m_wsf.UserWs));
			ITsString tss = bldr.GetString();
			Assert.AreEqual(0, tss.FindWordBoundary(0, "Chap Num", "Vers Num"));
			Assert.AreEqual(1, tss.FindWordBoundary(1, "Chap Num", "Vers Num"));
			Assert.AreEqual(2, tss.FindWordBoundary(2, "Chap Num", "Vers Num"));
			Assert.AreEqual(2, tss.FindWordBoundary(3, "Chap Num", "Vers Num"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordBoundary method when around a valid simple verse number
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordBoundary_AroundVerseNumber_Valid()
		{
			ITsStrBldr bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "51", StyleUtils.CharStyleTextProps("Vers Num", m_wsf.UserWs));
			bldr.Replace(bldr.Length, bldr.Length, "Some text", StyleUtils.CharStyleTextProps(null, m_wsf.UserWs));
			ITsString tss = bldr.GetString();
			Assert.AreEqual(0, tss.FindWordBoundary(0, "Vers Num"), "Failed to find position following verse number when ich == 0");
			for (var ich = 1; ich < 4; ich++)
				Assert.AreEqual(2, tss.FindWordBoundary(ich, "Vers Num"), "Failed to find position following verse number when ich == " + ich);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordBoundary method when around an invalid verse number
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordBoundary_AroundVerseNumber_Invalid()
		{
			ITsStrBldr bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "a1b", StyleUtils.CharStyleTextProps("Vers Num", m_wsf.UserWs));
			bldr.Replace(bldr.Length, bldr.Length, "Some text", StyleUtils.CharStyleTextProps(null, m_wsf.UserWs));
			ITsString tss = bldr.GetString();
			Assert.AreEqual(0, tss.FindWordBoundary(0, "Vers Num"), "Failed to find position following invalid verse number when ich == 0");
			for (var ich = 1; ich < 5; ich++)
				Assert.AreEqual(3, tss.FindWordBoundary(ich, "Vers Num"), "Failed to find position following invalid verse number when ich == " + ich);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the FindWordBoundary method when around a valid verse bridge
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindWordBoundary_AroundVerseNumberBridge()
		{
			ITsStrBldr bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "5-8", StyleUtils.CharStyleTextProps("Vers Num", m_wsf.UserWs));
			bldr.Replace(bldr.Length, bldr.Length, "Some text", StyleUtils.CharStyleTextProps(null, m_wsf.UserWs));
			ITsString tss = bldr.GetString();
			Assert.AreEqual(0, tss.FindWordBoundary(0, "Vers Num"), "Failed to find position following verse bridge when ich == 0");
			for (var ich = 1; ich < 5; ich++)
				Assert.AreEqual(3, tss.FindWordBoundary(ich, "Vers Num"), "Failed to find position following verse bridge when ich == " + ich);
		}
		#endregion

		#region WriteHref tests
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the WriteHref method when passed a text prop type other than
		/// FwTextPropType.ktptObjData.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void WriteHref_WrongTextPropType()
		{
			using (var stream = new StringWriter())
			{
				using (var writer = new XmlTextWriter(stream))
				{
					Assert.IsFalse(TsStringUtils.WriteHref(-56, new string(new[] {
						Convert.ToChar((int)FwObjDataTypes.kodtExternalPathName), 'a', 'b', 'c'}),
						writer));
					Assert.AreEqual(String.Empty, stream.ToString());
				}
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the WriteHref method when passed string prop with no URL.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void WriteHref_NoURL()
		{
			using (var stream = new StringWriter())
			{
				using (var writer = new XmlTextWriter(stream))
				{
					Assert.IsFalse(TsStringUtils.WriteHref((int)FwTextPropType.ktptObjData, new string(
						Convert.ToChar((int)FwObjDataTypes.kodtExternalPathName), 1),
						writer));
					Assert.AreEqual(String.Empty, stream.ToString());
				}
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the WriteHref method when passed a string prop whose first character is not
		/// FwObjDataTypes.kodtExternalPathName.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void WriteHref_WrongStringPropObjDataType()
		{
			using (var stream = new StringWriter())
			{
				using (var writer = new XmlTextWriter(stream))
				{
					Assert.IsFalse(TsStringUtils.WriteHref((int)FwTextPropType.ktptObjData, "abc", writer));
					Assert.AreEqual(String.Empty, stream.ToString());
				}
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the WriteHref method when passed a null string prop.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void WriteHref_NullStringProp()
		{
			using (var stream = new StringWriter())
			{
				using (var writer = new XmlTextWriter(stream))
				{
					Assert.IsFalse(TsStringUtils.WriteHref((int)FwTextPropType.ktptObjData, null, writer));
					Assert.AreEqual(String.Empty, stream.ToString());
				}
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the WriteHref method when passed a file URL.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void WriteHref_File()
		{
			using (var stream = new StringWriter())
			{
				using (var writer = new XmlTextWriter(stream))
				{
					writer.WriteStartElement("span");

					var strBldr = new StringBuilder("c:\\autoexec.bat");
					strBldr.Insert(0, Convert.ToChar((int)FwObjDataTypes.kodtExternalPathName));

					Assert.IsTrue(TsStringUtils.WriteHref((int)FwTextPropType.ktptObjData,
						strBldr.ToString(), writer));
					writer.WriteEndElement();

					Assert.AreEqual("<span href=\"file://c:/autoexec.bat\" />", stream.ToString());
				}
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the WriteHref method when passed a normal URL.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void WriteHref_NormalURL()
		{
			using (var stream = new StringWriter())
			{
				using (var writer = new XmlTextWriter(stream))
				{
					writer.WriteStartElement("span");

					var strBldr = new StringBuilder("http://www.myspace.com");
					strBldr.Insert(0, Convert.ToChar((int)FwObjDataTypes.kodtExternalPathName));

					Assert.IsTrue(TsStringUtils.WriteHref((int)FwTextPropType.ktptObjData,
						strBldr.ToString(), writer));
					writer.WriteEndElement();

					Assert.AreEqual("<span href=\"http://www.myspace.com\" />", stream.ToString());
				}
			}
		}
		#endregion

		#region Valid Character tests
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Validates a character sequence consisting of a single line separator character
		/// (U+2028).
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ValidateCharacterSequence_SingleLineSeparator()
		{
			Assert.AreEqual("\u2028", TsStringUtils.ValidateCharacterSequence("\u2028"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Validates a character sequence consisting of a single space character.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ValidateCharacterSequence_SingleSpace()
		{
			Assert.AreEqual(" ", TsStringUtils.ValidateCharacterSequence(" "));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Validates a character sequence consisting of a single format (other) character.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ValidateCharacterSequence_SingleFormatCharacter()
		{
			Assert.AreEqual("\u200c", TsStringUtils.ValidateCharacterSequence("\u200c"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Validates a character sequence consisting of a single word-forming character.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ValidateCharacterSequence_SingleLetter()
		{
			Assert.AreEqual("c", TsStringUtils.ValidateCharacterSequence("c"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Validates a character sequence consisting of a single numeric character.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ValidateCharacterSequence_SingleNumber()
		{
			Assert.AreEqual("2", TsStringUtils.ValidateCharacterSequence("2"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Validates a character sequence consisting of a single PUA character.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ValidateCharacterSequence_SinglePUA()
		{
			Assert.AreEqual("\uE000", TsStringUtils.ValidateCharacterSequence("\uE000"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Validates a character sequence consisting of a single undefined character.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ValidateCharacterSequence_SingleUndefinedChar()
		{
			Assert.AreEqual(string.Empty, TsStringUtils.ValidateCharacterSequence("\u2065"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Validates a character sequence consisting of a single punctuation character.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ValidateCharacterSequence_SinglePunctuation()
		{
			Assert.AreEqual("(", TsStringUtils.ValidateCharacterSequence("("));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Validates a character sequence consisting of a single symbol character.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ValidateCharacterSequence_SingleSymbol()
		{
			Assert.AreEqual("$", TsStringUtils.ValidateCharacterSequence("$"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Validates a character sequence consisting of a letter with a diacritic.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ValidateCharacterSequence_BaseCharacterPlusDiacritic()
		{
			Assert.AreEqual("n\u0301", TsStringUtils.ValidateCharacterSequence("n\u0301"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Validates a character sequence consisting of a letter with three diacritics (Hebrew).
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ValidateCharacterSequence_BaseCharacterPlusMultipleDiacritics()
		{
			Assert.AreEqual("\u05E9\u05C1\u05B4\u0596",
				TsStringUtils.ValidateCharacterSequence("\u05E9\u05C1\u05B4\u0596"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Validates a character sequence consisting of a single diacritic.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ValidateCharacterSequence_SingleDiacritic()
		{
			Assert.AreEqual(string.Empty, TsStringUtils.ValidateCharacterSequence("\u0301"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Validates a character sequence consisting of two word-forming base characters.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ValidateCharacterSequence_MultipleLetters()
		{
			Assert.AreEqual("n", TsStringUtils.ValidateCharacterSequence("no"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Validates a character sequence consisting of a single diacritic followed by a letter.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ValidateCharacterSequence_DiacriticBeforeLetter()
		{
			Assert.AreEqual("o", TsStringUtils.ValidateCharacterSequence("\u0301o"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Validates a character sequence consisting of three (Korean) base characters that can
		/// be composed to form a single (syllabic) base character (U+AC10).
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ValidateCharacterSequence_MultipleBaseCharsThatComposeIntoASingleBaseChar()
		{
			Assert.AreEqual("\uAC10",
				TsStringUtils.ValidateCharacterSequence("\u1100\u1161\u11B7"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Validates a (Korean) character sequence consisting of a single (syllabic) base
		/// character that can be decomposed to form three (phonemic) base characters.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void IsValidChar_SingleBaseCharThatDecomposesIntoMultipleBaseChars()
		{
			Assert.That(TsStringUtils.IsValidChar("\uAC10"), Is.True);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Validates a character sequence consisting of three (Korean) base characters that can
		/// be composed to form a single (syllabic) base character.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void IsValidChar_MultipleBaseCharsThatComposeIntoASingleBaseChar()
		{
			Assert.That(TsStringUtils.IsValidChar("\u1100\u1161\u11B7"), Is.True);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Validates a character sequence consisting of a base character followed by multiple
		/// (Hebrew) diacritics joined by the zero-width joiner (U+200D).
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ValidateCharacterSequence_MultipleBaseCharsJoinedByZWJ()
		{
			Assert.AreEqual("\u05E9\u05C1\u05B4\u200D\u0596",
				TsStringUtils.ValidateCharacterSequence("\u05E9\u05C1\u05B4\u200D\u0596"));

		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Validates a character sequence consisting of a base character followed by multiple
		/// (Hebrew) diacritics joined by the zero-width non-joiner (U+200C).
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ValidateCharacterSequence_MultipleBaseCharsJoinedByZWNJ()
		{
			Assert.AreEqual("\u05E9\u05C1\u05B4\u200C\u0596",
				TsStringUtils.ValidateCharacterSequence("\u05E9\u05C1\u05B4\u200C\u0596"));
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests that the Zero-width Non-joiner (U+200C)character is considered valid. TE-8318
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void ValidateCharacterSequence_AllowZwnj()
		{
			Assert.AreEqual("\u200C",
				TsStringUtils.ValidateCharacterSequence("\u200C"));
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests that the Zero-width Joiner (U+200D) character is considered valid. TE-8318
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void ValidateCharacterSequence_AllowZwnjAndZwj()
		{
			Assert.AreEqual("\u200D",
				TsStringUtils.ValidateCharacterSequence("\u200D"));
		}
		#endregion

		#region ParseCharString tests
		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests that ParseCharString method works when passed a simple string of space-
		/// delimited letters.
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void ParseCharString_Simple()
		{
			List<string> invalidChars;
			List<string> validChars = TsStringUtils.ParseCharString("a b c", " ", out invalidChars);
			Assert.AreEqual(3, validChars.Count);
			Assert.AreEqual("a", validChars[0]);
			Assert.AreEqual("b", validChars[1]);
			Assert.AreEqual("c", validChars[2]);
			Assert.AreEqual(0, invalidChars.Count);
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests that ParseCharString method works when passed a simple string of space-
		/// delimited letters that also has a leading space.
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void ParseCharString_LeadingSpace()
		{
			List<string> invalidChars;
			List<string> validChars = TsStringUtils.ParseCharString("  a b", " ", out invalidChars);
			Assert.AreEqual(3, validChars.Count);
			Assert.AreEqual(" ", validChars[0]);
			Assert.AreEqual("a", validChars[1]);
			Assert.AreEqual("b", validChars[2]);
			Assert.AreEqual(0, invalidChars.Count);
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests that ParseCharString method works when passed a string containing a single
		/// isolated diacritic.
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		//[ExpectedException(typeof(ArgumentException),
		//	ExpectedMessage = "The character \u0301 (U+0301) is not valid\r\nParameter name: chars")]
		public void ParseCharString_BogusCharacter()
		{
			List<string> invalidChars;
			List<string> validChars = TsStringUtils.ParseCharString("\u0301", " ", out invalidChars);
			Assert.AreEqual(0, validChars.Count);
			Assert.AreEqual(1, invalidChars.Count);
			Assert.AreEqual("\u0301", invalidChars[0]);
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests that ParseCharString method works when when passed a string of space-
		/// delimited letters that contains an illegal digraph
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void ParseCharString_BogusDigraph()
		{
			List<string> invalidChars;
			List<string> validChars = TsStringUtils.ParseCharString("ch a b c", " ", out invalidChars);
			Assert.AreEqual(3, validChars.Count);
			Assert.AreEqual("a", validChars[0]);
			Assert.AreEqual("b", validChars[1]);
			Assert.AreEqual("c", validChars[2]);
			Assert.AreEqual(1, invalidChars.Count);
			Assert.AreEqual("ch", invalidChars[0]);
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests that ParseCharString method works when passed a string of space-
		/// delimited letters that contains an illegal digraph in the mode where we ignore
		/// bogus characters (i.e. when we don't pass an empty list of invalid characters).
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void ParseCharString_IgnoreDigraph()
		{
			List<string> validChars = TsStringUtils.ParseCharString("ch a c", " ");
			Assert.AreEqual(2, validChars.Count);
			Assert.AreEqual("a", validChars[0]);
			Assert.AreEqual("c", validChars[1]);
		}
		#endregion

		#region Words tests
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the TsString Words extension method when the TsString contains only one run
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void Words_OneRun()
		{
			var tss = TsStringUtils.MakeString("   This is  some text.  ", 1);
			var expectedWords = new[] { "This", "is", "some", "text." };

			var i = 0;
			foreach (var word in tss.Words())
				Assert.AreEqual(expectedWords[i++], word.Text);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the TsString Words extension method when the TsString contains multiple runs
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void Words_MultipleRuns()
		{
			ITsStrBldr bldr = TsStringUtils.MakeStrBldr();
			bldr.Append("   This  is", StyleUtils.CharStyleTextProps("Monkey", 1));
			bldr.Append("some text.  ", StyleUtils.CharStyleTextProps("Soup", 1));
			var expectedWords = new[] { "This", "is", "some", "text." };

			var i = 0;
			foreach (var word in bldr.GetString().Words())
				Assert.AreEqual(expectedWords[i++], word.Text);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the TsString LastWord extension method when the TsString contains only one run
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void LastWord_OneRun()
		{
			var tss = TsStringUtils.MakeString("  This is  some text. ", 1);
			Assert.AreEqual("text.", tss.LastWord().Text);
			tss = TsStringUtils.MakeString("  text. ", 1);
			Assert.AreEqual("text.", tss.LastWord().Text);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the TsString LastWord extension method when the TsString contains multiple runs
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void LastWord_MultipleRuns()
		{
			ITsStrBldr bldr = TsStringUtils.MakeStrBldr();
			bldr.Append("This is", StyleUtils.CharStyleTextProps("Monkey", 1));
			bldr.Append("text.", StyleUtils.CharStyleTextProps("Soup", 1));
			Assert.AreEqual("text.", bldr.GetString().LastWord().Text);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the TsString LastWord extension method when the TsString ends with runs that
		/// are all whitespace
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void LastWord_TrailingSpaceInDifferentRun()
		{
			ITsStrBldr bldr = TsStringUtils.MakeStrBldr();
			bldr.Append("This is", StyleUtils.CharStyleTextProps("Monkey", 1));
			bldr.Append("text.", StyleUtils.CharStyleTextProps("Soup", 1));
			bldr.Append(" ", StyleUtils.CharStyleTextProps(null, 1));
			bldr.Append("     ", StyleUtils.CharStyleTextProps("Guppies", 1));
			Assert.AreEqual("text.", bldr.GetString().LastWord().Text);
		}
		#endregion

		#region Helper methods
		private static void VerifyStringDiffs(ITsString tss1, ITsString tss2,
			int ichMinExpected, int cchInsExpected, int cchDelExpected, bool fAdjustAnalyses, string id)
		{
			VerifyStringDiffs(tss1, tss2, new TsStringDiffInfo(ichMinExpected, cchInsExpected, cchDelExpected, fAdjustAnalyses), id);
		}

		private static void VerifyNoStringDiffs(ITsString tss1, ITsString tss2, string id)
		{
			VerifyStringDiffs(tss1, tss2, null, id);
		}

		private static void VerifyStringDiffs(ITsString tss1, ITsString tss2, TsStringDiffInfo diffInfoExpected, string id)
		{
			var diffInfo = TsStringUtils.GetDiffsInTsStrings(tss1, tss2);
			if (diffInfoExpected == null)
				Assert.Null(diffInfo, id);
			else
			{
				Assert.NotNull(diffInfo, id);
				Assert.AreEqual(diffInfoExpected.IchFirstDiff, diffInfo.IchFirstDiff, id + " ichMin");
				Assert.AreEqual(diffInfoExpected.CchInsert, diffInfo.CchInsert, id + " cchIns");
				Assert.AreEqual(diffInfoExpected.CchDeleteFromOld, diffInfo.CchDeleteFromOld, id + " cchDel");
				Assert.AreEqual(diffInfoExpected.FAdjustAnalyses, diffInfo.FAdjustAnalyses, id + " forceAdjustAna");
			}
		}

		private static void VerifyConcatenate(string first, string second, string output)
		{
			var firstInput = TsStringUtils.MakeString(first, 1);
			var secondInput = TsStringUtils.MakeString(second, 1);

			Assert.AreEqual(output, firstInput.ConcatenateWithSpaceIfNeeded(secondInput).Text,
				"Concatenating '" + first + "' and '" + second + "' did not produce correct result.");
		}

		string TrimNonWordFormingChars(string test, ILgWritingSystemFactory wsf)
		{
			return TsStringUtils.TrimNonWordFormingChars(TsStringUtils.MakeString(test, wsf.get_Engine("en").Handle), wsf).Text;
		}

		string TrimNonWordFormingChars(string test, ILgWritingSystemFactory wsf, bool atStart, bool atEnd)
		{
			return TsStringUtils.TrimNonWordFormingChars(TsStringUtils.MakeString(test, wsf.get_Engine("en").Handle), wsf, atStart, atEnd).Text;
		}
		#endregion

		#region Misc tests
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests that the string stored as ObjData has the right length
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ObjDataCorrect()
		{
			var guid = Guid.NewGuid();
			var objData = TsStringUtils.GetObjData(guid, FwObjDataTypes.kodtNameGuidHot);

			Assert.AreEqual(18, objData.Length);
			Assert.AreEqual(FwObjDataTypes.kodtNameGuidHot, (FwObjDataTypes) objData[0]);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests getting a string returned in normalized form, decomposed.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetAbbreviationsNameOnly()
		{
			var decomposed = "E\u0324\u0301PI\u0302TRE";
			var composed = TsStringUtils.Compose(decomposed);
			Assert.IsFalse(decomposed == composed);
			Assert.AreEqual("\u00c9\u0324P\u00CETRE", composed);

			composed = TsStringUtils.Compose("A\u030A\u0301");
			Assert.AreEqual("\u01FA", composed);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Create an ITsString from an xml source. The source XML is in composed form. When
		/// creating the tss, the text should be decomposed. (FWR-148)
		/// </summary>
		/// <remarks>
		/// The TsStringUtils.GetTsString() method for converting from XML to an ITsString
		/// has been replaced by a new method on TsStringSerializer, as shown in the test below.
		/// </remarks>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void CreateTsStringFromXml()
		{
			const string threeRunString = "<Str><Run ws=\"en\" namedStyle=\"Chapter Number\">1</Run><Run ws=\"en\" namedStyle=\"Verse Number\">1</Run><Run ws=\"en\">Laa yra la m\u00E9n ne nak xpenkwlal Jesucrist nee ne z\u00EB\u00EBd xn\u00EBz rey David ne z\u00EB\u00EBd xn\u00EBz Abraham.</Run></Str>";
			// This works sans the chars with diacritics. var threeRunString = "<Str><Run ws=\"en\" namedStyle=\"Chapter Number\">1</Run><Run ws=\"en\" namedStyle=\"Verse Number\">1</Run><Run ws=\"en\">Laa yra la men ne nak xpenkwlal Jesucrist nee ne zeed xnez rey David ne zeed xnez Abraham.</Run></Str>";
			var tss = TsStringSerializer.DeserializeTsStringFromXml(threeRunString, m_wsf);
			Assert.AreEqual("11Laa yra la me\u0301n ne nak xpenkwlal Jesucrist nee ne ze\u0308e\u0308d xne\u0308z rey David ne ze\u0308e\u0308d xne\u0308z Abraham.",
				tss.Text);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test the GetDiffsInTsStrings method.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FindStringDiffs()
		{
			VerifyNoStringDiffs(null, null, "null string equals itself");
			var tssEmpty1 = TsStringUtils.EmptyString(1);
			VerifyNoStringDiffs(tssEmpty1, tssEmpty1, "empty string equals itself");
			var tssAbc1 = TsStringUtils.MakeString("abc", 1);
			VerifyNoStringDiffs(tssAbc1, tssAbc1, "one-run string equals itself");
			VerifyStringDiffs(null, tssEmpty1, 0, 0, 0, true, "null and empty");
			VerifyStringDiffs(tssEmpty1, null, 0, 0, 0, true, "empty and null");
			VerifyStringDiffs(null, tssAbc1, 0, 3, 0, true, "added 3 chars to null string");
			VerifyStringDiffs(tssAbc1, null, 0, 0, 3, true, "nullified 3-char string");
			VerifyStringDiffs(tssEmpty1, tssAbc1, 0, 3, 0, true, "added 3 chars to empty string");
			var tssEmpty2 = TsStringUtils.MakeString("", 2);
			VerifyStringDiffs(tssEmpty1, tssEmpty2, 0, 0, 0, false, "two empty strings in different wss are not equal");
			var tssAbc2 = TsStringUtils.MakeString("abc", 2);
			VerifyStringDiffs(tssAbc1, tssAbc2, 0, 3, 3, false, "two non-empty strings in different wss are not equal");
			var tssAbc1b = TsStringUtils.MakeString("abc", 1);
			VerifyNoStringDiffs(tssAbc1, tssAbc1b, "one-run string equals an identical string");

			var props1 = TsStringUtils.PropsForWs(1);
			var props2 = TsStringUtils.PropsForWs(2);
			var props3 = TsStringUtils.PropsForWs(3);

			var bldr = tssAbc1.GetBldr();
			bldr.Replace(3, 3, "def", props2);
			var tssAbc1Def2 = bldr.GetString();
			VerifyNoStringDiffs(tssAbc1Def2, tssAbc1Def2, "two-run string equals itself");
			VerifyNoStringDiffs(tssAbc1Def2, tssAbc1Def2.GetBldr().GetString(), "two-run string equals identical string");
			VerifyStringDiffs(tssAbc1Def2, tssAbc1, 3, 0, 3, true, "two-run string shortened to one-run");
			VerifyStringDiffs(tssAbc1, tssAbc1Def2, 3, 3, 0, true, "one-run string added second run");

			var tssAbd1 = TsStringUtils.MakeString("abd", 1);
			VerifyStringDiffs(tssAbc1, tssAbd1, 2, 1, 1, true, "one-run string different last character");
			var tssAb1 = TsStringUtils.MakeString("ab", 1);
			VerifyStringDiffs(tssAbc1, tssAb1, 2, 0, 1, true, "one-run string remove last character");
			VerifyStringDiffs(tssAb1, tssAbc1, 2, 1, 0, true, "one-run string add last character");

			bldr = tssAbc1Def2.GetBldr();
			bldr.Replace(6, 6, "ghi", props1);
			var tssAbc1Def2Ghi1 = bldr.GetString();

			bldr = tssAbc1Def2Ghi1.GetBldr();
			bldr.SetProperties(3, 6, props3);
			var tssAbc1Def3Ghi1 = bldr.GetString();
			VerifyStringDiffs(tssAbc1Def2Ghi1, tssAbc1Def3Ghi1, 3, 3, 3, false, "three-run string differs by middle WS");

			VerifyStringDiffs(tssAbc1Def2, tssAbc1Def2Ghi1, 6, 3, 0, true, "two-run string added run at end");
			VerifyStringDiffs(tssAbc1Def2Ghi1, tssAbc1Def2, 6, 0, 3, true, "three-run string deleted run at end");

			bldr = tssAbc1Def2Ghi1.GetBldr();
			bldr.SetProperties(6, 9, props3);
			var tssAbc1Def2Ghi3 = bldr.GetString();
			VerifyStringDiffs(tssAbc1Def2Ghi1, tssAbc1Def2Ghi3, 6, 3, 3, false, "three-run string differs by final WS");

			bldr = tssAbc1Def2Ghi3.GetBldr();
			bldr.SetProperties(0, 3, props3);
			var tssAbc3Def2Ghi3 = bldr.GetString();
			VerifyStringDiffs(tssAbc1Def2Ghi1, tssAbc3Def2Ghi3, 0, 9, 9, false, "three-run string differs by sandwich WS");

			bldr = tssAbc1Def2Ghi3.GetBldr();
			bldr.Replace(3, 6, null, null);
			var tssAbc1Ghi3 = bldr.GetString();
			VerifyStringDiffs(tssAbc1Ghi3, tssAbc1Def2Ghi3, 3, 3, 0, true, "two-run string added run middle");
			VerifyStringDiffs(tssAbc1Def2Ghi3, tssAbc1Ghi3, 3, 0, 3, true, "three-run string deleted run middle");

			VerifyStringDiffs(tssAbc1, tssAbc1Def2Ghi3, 3, 6, 0, true, "one-run string added two runs end");
			VerifyStringDiffs(tssAbc1Def2Ghi3, tssAbc1, 3, 0, 6, true, "three-run string deleted last two runs");

			bldr = tssAbc1Def2Ghi3.GetBldr();
			bldr.Replace(0, 3, null, null);
			var tssDef2Ghi3 = bldr.GetString();
			VerifyStringDiffs(tssDef2Ghi3, tssAbc1Def2Ghi3, 0, 3, 0, true, "two-run string added run start");
			VerifyStringDiffs(tssAbc1Def2Ghi3, tssDef2Ghi3, 0, 0, 3, true, "three-run string deleted run start");

			var tssAxc1 = TsStringUtils.MakeString("axc", 1);
			VerifyStringDiffs(tssAbc1, tssAxc1, 1, 1, 1, true, "one-run string different mid character");
			var tssAc1 = TsStringUtils.MakeString("ac", 1);
			VerifyStringDiffs(tssAbc1, tssAc1, 1, 0, 1, true, "one-run string remove mid character");
			VerifyStringDiffs(tssAc1, tssAbc1, 1, 1, 0, true, "one-run string add mid character");

			var tssXbc1 = TsStringUtils.MakeString("xbc", 1);
			VerifyStringDiffs(tssAbc1, tssXbc1, 0, 1, 1, true, "one-run string different first character");
			var tssBc1 = TsStringUtils.MakeString("bc", 1);
			VerifyStringDiffs(tssAbc1, tssBc1, 0, 0, 1, true, "one-run string remove first character");
			VerifyStringDiffs(tssBc1, tssAbc1, 0, 1, 0, true, "one-run string add first character");

			bldr = tssAbc1Def2Ghi3.GetBldr();
			bldr.Replace(0, 1, "x", null);
			var tssXbc1Def2Ghi3 = bldr.GetString();
			VerifyStringDiffs(tssAbc1Def2Ghi3, tssXbc1Def2Ghi3, 0, 1, 1, true, "three-run string different first character");
			bldr = tssAbc1Def2Ghi3.GetBldr();
			bldr.Replace(0, 1, "", null);
			var tssBc1Def2Ghi3 = bldr.GetString();
			VerifyStringDiffs(tssAbc1Def2Ghi3, tssBc1Def2Ghi3, 0, 0, 1, true, "three-run string delete first character");
			VerifyStringDiffs(tssBc1Def2Ghi3, tssAbc1Def2Ghi3, 0, 1, 0, true, "three-run string insert first character");

			bldr = tssAbc1Def2Ghi3.GetBldr();
			bldr.Replace(8, 9, "x", null);
			var tssAbc1Def2Ghx3 = bldr.GetString();
			VerifyStringDiffs(tssAbc1Def2Ghi3, tssAbc1Def2Ghx3, 8, 1, 1, true, "three-run string different last character");
			bldr = tssAbc1Def2Ghi3.GetBldr();
			bldr.Replace(8, 9, "", null);
			var tssAbc1Def2Gh3 = bldr.GetString();
			VerifyStringDiffs(tssAbc1Def2Ghi3, tssAbc1Def2Gh3, 8, 0, 1, true, "three-run string delete last character");
			VerifyStringDiffs(tssAbc1Def2Gh3, tssAbc1Def2Ghi3, 8, 1, 0, true, "three-run string insert last character");

			bldr = tssAbc1Def2Ghi3.GetBldr();
			bldr.Replace(4, 5, "x", null);
			var tssAbc1Dxf2Ghi3 = bldr.GetString();
			VerifyStringDiffs(tssAbc1Def2Ghi3, tssAbc1Dxf2Ghi3, 4, 1, 1, true, "three-run string different mid character");
			bldr = tssAbc1Def2Ghi3.GetBldr();
			bldr.Replace(4, 5, "", null);
			var tssAbc1Df2Ghi3 = bldr.GetString();
			VerifyStringDiffs(tssAbc1Def2Ghi3, tssAbc1Df2Ghi3, 4, 0, 1, true, "three-run string delete mid character");
			VerifyStringDiffs(tssAbc1Df2Ghi3, tssAbc1Def2Ghi3, 4, 1, 0, true, "three-run string insert mid character");

			bldr = tssAbc1Def2Ghi3.GetBldr();
			bldr.Replace(3, 4, "x", null);
			var tssAbc1Xef2Ghi3 = bldr.GetString();
			VerifyStringDiffs(tssAbc1Def2Ghi3, tssAbc1Xef2Ghi3, 3, 1, 1, true, "three-run string replace first char of mid run");
			bldr = tssAbc1Def2Ghi3.GetBldr();
			bldr.Replace(3, 4, "", null);
			var tssAbc1Ef2Ghi3 = bldr.GetString();
			VerifyStringDiffs(tssAbc1Def2Ghi3, tssAbc1Ef2Ghi3, 3, 0, 1, true, "three-run string delete first char of mid run");
			VerifyStringDiffs(tssAbc1Ef2Ghi3, tssAbc1Def2Ghi3, 3, 1, 0, true, "three-run string insert first char of mid run");

			bldr = tssAbc1Def2Ghi3.GetBldr();
			bldr.Replace(5, 6, "x", null);
			var tssAbc1Dex2Ghi3 = bldr.GetString();
			VerifyStringDiffs(tssAbc1Def2Ghi3, tssAbc1Dex2Ghi3, 5, 1, 1, true, "three-run string replace last char of mid run");
			bldr = tssAbc1Def2Ghi3.GetBldr();
			bldr.Replace(5, 6, "", null);
			var tssAbc1De2Ghi3 = bldr.GetString();
			VerifyStringDiffs(tssAbc1Def2Ghi3, tssAbc1De2Ghi3, 5, 0, 1, true, "three-run string delete last char of mid run");
			VerifyStringDiffs(tssAbc1De2Ghi3, tssAbc1Def2Ghi3, 5, 1, 0, true, "three-run string insert last char of mid run");

			// Different numbers of runs, part of each border run the same.
			bldr = tssAbc1Def2Ghi3.GetBldr();
			bldr.Replace(4, 5, "x", null);
			bldr.Replace(6, 6, "xyz", props1);
			bldr.Replace(9, 9, "xyf", props2);
			var tssAbc1Dxf2Xyz1Xyf2Ghi3 = bldr.GetString();
			VerifyStringDiffs(tssAbc1Def2Ghi3, tssAbc1Dxf2Xyz1Xyf2Ghi3, 4, 7, 1, true, "three-run string replace runs and text mid");
			VerifyStringDiffs(tssAbc1Dxf2Xyz1Xyf2Ghi3, tssAbc1Def2Ghi3, 4, 1, 7, true, "five-run string replace runs and text mid");

			VerifyStringDiffs(tssAbc1Def2Ghi3, tssXbc1, 0, 3, 9, true, "three-run string replace all one run");
			VerifyStringDiffs(tssXbc1, tssAbc1Def2Ghi3, 0, 9, 3, true, "one-run string replace all three runs");

			bldr = tssAbc1Def2Ghi3.GetBldr();
			bldr.Replace(5, 9, "x", null);
			var tssAbc1Dex2 = bldr.GetString();
			VerifyStringDiffs(tssAbc1Def2Ghi3, tssAbc1Dex2, 5, 1, 4, true, "three-run string replace last and part of mid");
			VerifyStringDiffs(tssAbc1Dex2, tssAbc1Def2Ghi3, 5, 4, 1, true, "two-run string replace text and add run at end");

			bldr = tssAbc1Def2Ghi3.GetBldr();
			bldr.Replace(0, 4, "", null);
			var tssEf2Ghi3 = bldr.GetString();
			VerifyStringDiffs(tssAbc1Def2Ghi3, tssEf2Ghi3, 0, 0, 4, true, "three-run string delete first and part of mid");
			VerifyStringDiffs(tssEf2Ghi3, tssAbc1Def2Ghi3, 0, 4, 0, true, "two-run string insert run and text at start");

			var s1 = TsStringUtils.MakeString("abc. def.", 1);
			var s2 = TsStringUtils.MakeString("abc. insert. def.", 1);
			VerifyStringDiffs(s1, s2, 5, 8, 0, true, "insert with dup material before and in insert");
			VerifyStringDiffs(s2, s1, 5, 0, 8, true, "delete with dup material before and at and of stuff deleted.");

			s1 = TsStringUtils.MakeString("xxxabc xxxdef.", 1);
			s2 = TsStringUtils.MakeString("xxxdef.", 1);
			VerifyStringDiffs(s1, s2, 0, 0, 7, true, "delete whole word ambiguous with delete part of two words");
			VerifyStringDiffs(s2, s1, 0, 7, 0, true, "insert whole word ambiguous with insert part of two words");

			s1 = TsStringUtils.MakeString("pus pus yalola.", 1);
			s2 = TsStringUtils.MakeString("pus yalola.", 1);
			VerifyStringDiffs(s1, s2, 4, 0, 4, true, "delete first word ambiguous with delete second word");
			VerifyStringDiffs(s2, s1, 4, 4, 0, true, "insert first word ambiguous with insert second words");

		}

		[Test]
		public void ChangesRequireAnalysisAdjustment()
		{
			var abc1 = TsStringUtils.MakeString("abc", 1);
			var abc2 = TsStringUtils.MakeString("abc", 2);
			Assert.False(TsStringUtils.ChangesRequireAnalysisAdjustment(abc1, abc2, 0),
				"only the WS changed; the responsible parties don't need our help to notice (LT-20344)");
			var abg1 = TsStringUtils.MakeString("abg", 1);
			Assert.True(TsStringUtils.ChangesRequireAnalysisAdjustment(abc1, abg1, 0), "text changed");
			Assert.True(TsStringUtils.ChangesRequireAnalysisAdjustment(abc2, abg1, 0), "text changed (WS, too)");

			var bldr = abc1.GetBldr();
			bldr.SetIntPropValues(2, 3, (int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, 3);
			var ab1c3 = bldr.GetString();
			bldr.SetIntPropValues(2, 3, (int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, 4);
			var ab1c4 = bldr.GetString();
			Assert.False(TsStringUtils.ChangesRequireAnalysisAdjustment(ab1c3, ab1c4, 1), "only the WS changed");

			bldr = abc1.GetBldr();
			bldr.SetStrPropValue(0, 3, (int)FwTextPropType.ktptFontFamily, "Charis SIL");
			var abc1Charis = bldr.GetString();
			bldr.SetStrPropValue(0, 3, (int)FwTextPropType.ktptFontFamily, "Duolos SIL");
			var abc1Duolos = bldr.GetString();
			Assert.False(TsStringUtils.ChangesRequireAnalysisAdjustment(abc1Charis, abc1Duolos, 0), "only the font changed");
			Assert.False(TsStringUtils.ChangesRequireAnalysisAdjustment(abc1Charis, abc1, 0), "font removed");
			Assert.False(TsStringUtils.ChangesRequireAnalysisAdjustment(abc1, abc1Duolos, 0), "font added");

			bldr = abc1.GetBldr();
			bldr.SetStrPropValue(0, 3, (int)FwTextPropType.ktptObjData, "gobble");
			var abc1gobble = bldr.GetString();
			bldr.SetStrPropValue(0, 3, (int)FwTextPropType.ktptObjData, "d gook");
			var abc1d_gook = bldr.GetString();
			bldr.SetStrPropValue(0, 3, (int)FwTextPropType.ktptObjData, "gobble");
			var abc1gobble_same = bldr.GetString();
			Assert.True(TsStringUtils.ChangesRequireAnalysisAdjustment(abc1gobble, abc1d_gook, 0), "special object changed");
			Assert.True(TsStringUtils.ChangesRequireAnalysisAdjustment(abc1gobble, abc1, 0), "special object removed");
			Assert.True(TsStringUtils.ChangesRequireAnalysisAdjustment(abc1, abc1d_gook, 0), "special object added");
			Assert.False(TsStringUtils.ChangesRequireAnalysisAdjustment(abc1gobble, abc1gobble_same, 0), "same gobbledygook");

			bldr = abc1gobble.GetBldr();
			bldr.SetStrPropValue(0, 3, (int)FwTextPropType.ktptFontFamily, "Duolos SIL");
			var abc1gobble_duolos = bldr.GetString();
			Assert.False(TsStringUtils.ChangesRequireAnalysisAdjustment(abc1gobble, abc1gobble_duolos, 0),
				"same gobbledygook; only the font changed");

			bldr = abc1d_gook.GetBldr();
			bldr.SetStrPropValue(0, 3, (int)FwTextPropType.ktptFontFamily, "Duolos SIL");
			var abc1d_gook_duolos = bldr.GetString();
			bldr.SetStrPropValue(0, 3, (int)FwTextPropType.ktptFontFamily, "Charis SIL");
			bldr.SetIntPropValues(0, 3, (int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, 2);
			bldr.Replace(2, 3, "u", null);
			var abu2d_gook_charis = bldr.GetString();
			Assert.True(TsStringUtils.ChangesRequireAnalysisAdjustment(abc1gobble_duolos, abc1d_gook_duolos, 0),
				"different gobbledygook");
			Assert.True(TsStringUtils.ChangesRequireAnalysisAdjustment(abc1gobble_duolos, abu2d_gook_charis, 0), "many things changed");
		}

		[Test]
		public void ConcatenateWithSpaceIfNeeded()
		{
			VerifyConcatenate("", "", null);
			VerifyConcatenate("A", "B", "A B");
			VerifyConcatenate("A ", "B", "A B");
			VerifyConcatenate("A", " B", "A B");
			VerifyConcatenate("A ", " B", "A  B");
			VerifyConcatenate("A", "", "A");
			VerifyConcatenate("", "B", "B");
			VerifyConcatenate("A\x3000", "B", "A\x3000B"); // ideographic space
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests that result of GetXmlRep with a Empty TsString.
		/// Confirming that a ws info is produced
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetXmlRep_EmptyTss()
		{
			// Setup an empty TsString
			var tssClean = TsStringUtils.GetCleanTsString(TsStringUtils.MakeString(String.Empty, m_wsf.UserWs), null);
			Assert.AreEqual(null, tssClean.Text);
			Assert.AreEqual(1, tssClean.RunCount);
			Assert.AreEqual(m_wsf.UserWs, tssClean.get_WritingSystem(0));

			// Test method GetXmlRep
			var result = TsStringUtils.GetXmlRep(tssClean, m_wsf, 0); // 0 means Str not AStr

			// Confirm that the xml output has 'ws' information in it.
			Assert.AreEqual(String.Format("<Str>{0}<Run ws=\"en\"></Run>{0}</Str>", Environment.NewLine), result);
		}

		/// <summary>
		/// Test various cases of TsStringUtils.RemoveIllegalXmlChars().
		/// </summary>
		[Test]
		public void RemoveIllegalXmlChars()
		{
			var ws = m_wsf.UserWs;
			var empty = TsStringUtils.EmptyString(ws);
			Assert.That(TsStringUtils.RemoveIllegalXmlChars(empty), Is.EqualTo(empty));
			var good = TsStringUtils.MakeString("good", ws);
			Assert.That(TsStringUtils.RemoveIllegalXmlChars(good), Is.EqualTo(good));
			var controlChar = TsStringUtils.MakeString("ab\x001ecd", ws);
			Assert.That(TsStringUtils.RemoveIllegalXmlChars(controlChar).Text, Is.EqualTo("abcd"));
			var twoBadChars = TsStringUtils.MakeString("\x000eabcde\x001f", ws);
			Assert.That(TsStringUtils.RemoveIllegalXmlChars(twoBadChars).Text, Is.EqualTo("abcde"));
			var allBad = TsStringUtils.MakeString("\x0000\x0008\x000b\x000c\xfffe\xffff", ws);
			Assert.That(string.IsNullOrEmpty(TsStringUtils.RemoveIllegalXmlChars(allBad).Text));
			var goodSpecial = TsStringUtils.MakeString("\x0009\x000a\x000d \xfffd", ws);
			Assert.That(TsStringUtils.RemoveIllegalXmlChars(goodSpecial), Is.EqualTo(goodSpecial));
			var badIsolatedLeadingSurrogate = TsStringUtils.MakeString("ab\xd800c\xdbff", ws);
			Assert.That(TsStringUtils.RemoveIllegalXmlChars(badIsolatedLeadingSurrogate).Text, Is.EqualTo("abc"));
			var goodSurrogates = TsStringUtils.MakeString("\xd800\xdc00 \xdbff\xdfff", ws);
			Assert.That(TsStringUtils.RemoveIllegalXmlChars(goodSurrogates), Is.EqualTo(goodSurrogates));
			var badIsolatedTrailingSurrogate = TsStringUtils.MakeString("\xdc00xy\xdcffz", ws);
			Assert.That(TsStringUtils.RemoveIllegalXmlChars(badIsolatedTrailingSurrogate).Text, Is.EqualTo("xyz"));
			var outOfOrderSurrogates = TsStringUtils.MakeString("\xd800\xdc00\xdc00\xdbffz", ws);
			Assert.That(TsStringUtils.RemoveIllegalXmlChars(outOfOrderSurrogates).Text, Is.EqualTo("\xd800\xdc00z"));
		}

		#endregion
	}
}