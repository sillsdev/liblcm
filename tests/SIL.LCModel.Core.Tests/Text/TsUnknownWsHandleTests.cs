// Copyright (c) 2026 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using NUnit.Framework;
using SIL.LCModel.Core.KernelInterfaces;

namespace SIL.LCModel.Core.Text
{
	/// <summary>
	/// An unknown writing system handle used to be written as ws="", which no reader accepts. A run of text
	/// cannot be written without a ws either, so such runs are written (and loaded) with a fallback ws.
	/// </summary>
	[TestFixture]
	public class TsUnknownWsHandleTests : TsSerializerTestsBase
	{
		private const int UnknownWs = 987654;

		private ITsTextProps MakeProps(FwTextPropType wsType, int ws)
		{
			ITsPropsBldr bldr = TsStringUtils.MakePropsBldr();
			bldr.SetStrPropValue((int)FwTextPropType.ktptNamedStyle, "Emphasis");
			bldr.SetIntPropValues((int)wsType, 0, ws);
			return bldr.GetTextProps();
		}

		private static int WsOfRun(ITsString tss, int irun)
		{
			int var;
			return tss.get_Properties(irun).GetIntPropValues((int)FwTextPropType.ktptWs, out var);
		}

		/// <summary>Asserts that every run has a ws the factory knows.</summary>
		private void AssertAllRunsHaveKnownWs(ITsString tss)
		{
			for (int i = 0; i < tss.RunCount; i++)
			{
				int ws = WsOfRun(tss, i);
				Assert.That(ws, Is.GreaterThan(0), "run " + i);
				Assert.That(WritingSystemManager.GetStrFromWs(ws), Is.Not.Null.And.Not.Empty, "run " + i);
			}
		}

		/// <summary>
		/// Serializes, reads, re-saves and reads again, checking that no step throws, that no empty ws is written
		/// and that every run of the final string has a known ws.
		/// </summary>
		private ITsString SaveAndReloadTwice(ITsString tss, int astrWs = 0)
		{
			string xml1 = TsStringSerializer.SerializeTsStringToXml(tss, WritingSystemManager, astrWs);
			Assert.That(xml1, Does.Not.Contain("ws=\"\""));
			ITsString read1 = TsStringSerializer.DeserializeTsStringFromXml(xml1, WritingSystemManager);
			string xml2 = TsStringSerializer.SerializeTsStringToXml(read1, WritingSystemManager, astrWs);
			Assert.That(xml2, Does.Not.Contain("ws=\"\""));
			ITsString read2 = TsStringSerializer.DeserializeTsStringFromXml(xml2, WritingSystemManager);
			Assert.That(read2.Text ?? string.Empty, Is.EqualTo(tss.Text ?? string.Empty).Or.EqualTo(
				(tss.Text ?? string.Empty).Replace("\u0001", string.Empty)));
			AssertAllRunsHaveKnownWs(read2);
			return read2;
		}

		#region Props
		[TestCase(FwTextPropType.ktptWs, "ws")]
		[TestCase(FwTextPropType.ktptBaseWs, "wsBase")]
		public void SerializeProps_UnknownWs_OmitsAttributeAndRoundTrips(FwTextPropType wsType, string attrName)
		{
			string xml = TsPropsSerializer.SerializePropsToXml(MakeProps(wsType, UnknownWs), WritingSystemManager);

			Assert.That(xml, Does.Not.Contain(attrName + "="));
			ITsTextProps result = TsPropsSerializer.DeserializePropsFromXml(xml, WritingSystemManager);
			Assert.That(result.GetStrPropValue((int)FwTextPropType.ktptNamedStyle), Is.EqualTo("Emphasis"));
			int var;
			Assert.That(result.GetIntPropValues((int)wsType, out var), Is.EqualTo(-1));
		}

		[Test]
		public void SerializeProps_KnownWs_StillWritten()
		{
			string xml = TsPropsSerializer.SerializePropsToXml(MakeProps(FwTextPropType.ktptWs, EnWS), WritingSystemManager);
			Assert.That(xml, Does.Contain("ws=\"en\""));
		}

		[TestCase("<Prop ws=\"\" namedStyle=\"Emphasis\"/>")]
		[TestCase("<Prop wsBase=\"\" namedStyle=\"Emphasis\"/>")]
		[TestCase("<Prop ws=\"\" wsBase=\"\" namedStyle=\"Emphasis\"/>")]
		public void DeserializeProps_EmptyWs_IsDropped(string xml)
		{
			ITsTextProps result = TsPropsSerializer.DeserializePropsFromXml(xml, WritingSystemManager);
			Assert.That(result.GetStrPropValue((int)FwTextPropType.ktptNamedStyle), Is.EqualTo("Emphasis"));
			int var;
			Assert.That(result.GetIntPropValues((int)FwTextPropType.ktptWs, out var), Is.EqualTo(-1));
			Assert.That(result.GetIntPropValues((int)FwTextPropType.ktptBaseWs, out var), Is.EqualTo(-1));
		}

		[Test]
		public void DeserializeProps_EmptyWsInWsProp_IsDropped()
		{
			string xml = "<Prop namedStyle=\"Emphasis\"><WsStyles9999><WsProp ws=\"\" fontFamily=\"Arial\"/>"
				+ "<WsProp ws=\"en\" fontFamily=\"Times\"/></WsStyles9999></Prop>";
			ITsTextProps result = TsPropsSerializer.DeserializePropsFromXml(xml, WritingSystemManager);
			string wsStyles = result.GetStrPropValue((int)FwTextPropType.ktptWsStyle);
			Assert.That(wsStyles, Does.Contain("Times"));
			Assert.That(wsStyles, Does.Not.Contain("Arial"));
		}
		#endregion

		#region Runs: writing
		[Test]
		public void SerializeString_RunWithUnknownBaseWs_RoundTrips()
		{
			ITsStrBldr bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "text", TsStringUtils.MakeProps("Emphasis", EnWS));
			bldr.SetIntPropValues(0, 4, (int)FwTextPropType.ktptBaseWs, 0, UnknownWs);

			string xml = TsStringSerializer.SerializeTsStringToXml(bldr.GetString(), WritingSystemManager);

			Assert.That(xml, Does.Not.Contain("wsBase"));
			ITsString result = SaveAndReloadTwice(bldr.GetString());
			Assert.That(result.get_WritingSystem(0), Is.EqualTo(EnWS));
		}

		[Test]
		public void SerializeString_RunWithUnknownWs_UsesUserWs()
		{
			ITsString result = SaveAndReloadTwice(TsStringUtils.MakeString("text", UnknownWs));
			Assert.That(result.Text, Is.EqualTo("text"));
			Assert.That(result.get_WritingSystem(0), Is.EqualTo(WritingSystemManager.UserWs));
		}

		[Test]
		public void SerializeString_RunWithUnknownWs_UsesPreviousRunWs()
		{
			ITsStrBldr bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "one", TsStringUtils.MakeProps(null, EsWS));
			bldr.Replace(3, 3, "two", TsStringUtils.MakeProps(null, UnknownWs));

			ITsString result = SaveAndReloadTwice(bldr.GetString());

			Assert.That(WsOfRun(result, result.RunCount - 1), Is.EqualTo(EsWS));
			Assert.That(result.Text, Is.EqualTo("onetwo"));
		}

		[Test]
		public void SerializeString_AStrRunWithUnknownWs_UsesAStrWs()
		{
			ITsString result = SaveAndReloadTwice(TsStringUtils.MakeString("text", UnknownWs), EsWS);
			Assert.That(result.get_WritingSystem(0), Is.EqualTo(EsWS));
		}

		[Test]
		public void SerializeString_EmptyRunWithUnknownWs_Loads()
		{
			// The reader requires a ws on an empty run too.
			string xml = TsStringSerializer.SerializeTsStringToXml(TsStringUtils.MakeString(string.Empty, UnknownWs), WritingSystemManager);
			Assert.That(xml, Does.Not.Contain("ws=\"\""));
			Assert.That(() => TsStringSerializer.DeserializeTsStringFromXml(xml, WritingSystemManager), Throws.Nothing);
		}

		[Test]
		public void SerializeString_RunStartingWithStrippedControlCharAndUnknownWs_Loads()
		{
			// U+0001 is dropped when written, so the run text the reader sees starts with 'a' and needs a ws.
			ITsString result = SaveAndReloadTwice(TsStringUtils.MakeString("\u0001abc", UnknownWs));
			Assert.That(result.Text, Is.EqualTo("abc"));
			Assert.That(result.get_WritingSystem(0), Is.EqualTo(WritingSystemManager.UserWs));
		}

		[Test]
		public void SerializeString_RunStartingWithTabAndUnknownWs_NeedsNoWs()
		{
			string xml = TsStringSerializer.SerializeTsStringToXml(TsStringUtils.MakeString("\tabc", UnknownWs), WritingSystemManager);
			Assert.That(xml, Does.Not.Contain("ws="));
			Assert.That(TsStringSerializer.DeserializeTsStringFromXml(xml, WritingSystemManager).Text, Is.EqualTo("\tabc"));
		}

		[TestCase(FwObjDataTypes.kodtOwnNameGuidHot)]
		[TestCase(FwObjDataTypes.kodtNameGuidHot)]
		public void SerializeString_OrcRunWithUnknownWs_Loads(FwObjDataTypes type)
		{
			ITsString orc = TsStringUtils.CreateOrcFromGuid(Guid.NewGuid(), type, UnknownWs);
			ITsString result = null;
			string xml = TsStringSerializer.SerializeTsStringToXml(orc, WritingSystemManager);
			Assert.That(xml, Does.Not.Contain("ws=\"\""));
			Assert.That(() => result = TsStringSerializer.DeserializeTsStringFromXml(xml, WritingSystemManager), Throws.Nothing);
			AssertAllRunsHaveKnownWs(result);
		}

		[Test]
		public void SerializeString_KnownWs_IsUnchanged()
		{
			string xml = TsStringSerializer.SerializeTsStringToXml(TsStringUtils.MakeString("text", EsWS), WritingSystemManager);
			Assert.That(xml, Does.Contain("<Run ws=\"es\">text</Run>"));
		}

		[Test]
		public void SerializeString_AStrWithUnknownWs_Fails()
		{
			// There is no ws we could safely use as the key of the alternative. LCM skips such alternatives
			// before they get here.
			ITsString tss = TsStringUtils.MakeString("text", EnWS);
			Assert.That(() => TsStringSerializer.SerializeTsStringToXml(tss, WritingSystemManager, UnknownWs),
				Throws.TypeOf<ArgumentException>());
		}
		#endregion

		#region Runs: reading legacy ws=""
		[TestCase("<Str><Run ws=\"\">text</Run></Str>", "en")]
		[TestCase("<Str><Run ws=\"\" namedStyle=\"Emphasis\">text</Run></Str>", "en")]
		[TestCase("<Str><Run ws=\"es\">te</Run><Run ws=\"\">xt</Run></Str>", "es")]
		[TestCase("<Str><Run ws=\"\">te</Run><Run ws=\"es\">xt</Run></Str>", "es")]
		[TestCase("<AStr ws=\"es\"><Run ws=\"\">text</Run></AStr>", "es")]
		[TestCase("<Str><Run ws=\"en\" wsBase=\"\">text</Run></Str>", "en")]
		public void DeserializeString_EmptyWs_GetsFallbackWsAndResaves(string xml, string expectedWsOfLastRun)
		{
			ITsString read1 = TsStringSerializer.DeserializeTsStringFromXml(xml, WritingSystemManager);

			Assert.That(read1.Text, Is.EqualTo("text"));
			AssertAllRunsHaveKnownWs(read1);
			Assert.That(WsOfRun(read1, read1.RunCount - 1), Is.EqualTo(WritingSystemManager.GetWsFromStr(expectedWsOfLastRun)));
			// and the result can be saved and loaded again
			bool isAStr = xml.StartsWith("<AStr");
			int astrWs = isAStr ? read1.get_WritingSystem(0) : 0;
			string xml2 = TsStringSerializer.SerializeTsStringToXml(read1, WritingSystemManager, astrWs);
			Assert.That(xml2, Does.Not.Contain("ws=\"\""));
			ITsString read2 = TsStringSerializer.DeserializeTsStringFromXml(xml2, WritingSystemManager);
			Assert.That(read2.Text, Is.EqualTo("text"));
			AssertAllRunsHaveKnownWs(read2);
		}

		[Test]
		public void DeserializeString_EmptyRunWithEmptyWs_ResavesAndLoads()
		{
			ITsString read1 = TsStringSerializer.DeserializeTsStringFromXml("<Str><Run ws=\"\"></Run></Str>", WritingSystemManager);
			string xml2 = TsStringSerializer.SerializeTsStringToXml(read1, WritingSystemManager);
			Assert.That(xml2, Does.Not.Contain("ws=\"\""));
			Assert.That(() => TsStringSerializer.DeserializeTsStringFromXml(xml2, WritingSystemManager), Throws.Nothing);
		}

		[Test]
		public void DeserializeString_EmptyWsInAStrWithoutWs_IsNotGivenAWs()
		{
			// The run's ws is the key of the alternative when <AStr> has no ws, so inventing one could replace a real
			// alternative. Such a string stays without a ws, as before.
			ITsString read = TsStringSerializer.DeserializeTsStringFromXml("<AStr ws=\"\"><Run ws=\"\">text</Run></AStr>", WritingSystemManager);
			Assert.That(read.get_WritingSystem(0), Is.LessThanOrEqualTo(0));
		}
		#endregion
	}
}
