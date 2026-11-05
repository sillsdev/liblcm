// Copyright (c) 2026 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using NUnit.Framework;
using SIL.LCModel.Core.KernelInterfaces;

namespace SIL.LCModel.Core.Text
{
	/// <summary>
	/// An unknown writing system handle used to be written as ws="", which no reader accepts.
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

		[TestCase(FwTextPropType.ktptWs, "ws")]
		[TestCase(FwTextPropType.ktptBaseWs, "wsBase")]
		public void SerializeProps_UnknownWs_OmitsAttributeAndRoundTrips(FwTextPropType wsType, string attrName)
		{
			string xml = TsPropsSerializer.SerializePropsToXml(MakeProps(wsType, UnknownWs), WritingSystemManager);

			Assert.That(xml, Does.Not.Contain(attrName + "=\"\""));
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

		[Test]
		public void SerializeString_RunWithUnknownBaseWs_RoundTrips()
		{
			ITsStrBldr bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "text", TsStringUtils.MakeProps("Emphasis", EnWS));
			bldr.SetIntPropValues(0, 4, (int)FwTextPropType.ktptBaseWs, 0, UnknownWs);

			string xml = TsStringSerializer.SerializeTsStringToXml(bldr.GetString(), WritingSystemManager);

			Assert.That(xml, Does.Not.Contain("wsBase"));
			ITsString result = TsStringSerializer.DeserializeTsStringFromXml(xml, WritingSystemManager);
			Assert.That(result.Text, Is.EqualTo("text"));
			Assert.That(result.get_WritingSystem(0), Is.EqualTo(EnWS));
		}

		[Test]
		public void SerializeString_RunWithUnknownWs_FailsInsteadOfWritingBadXml()
		{
			// A run of text must have a ws, so there is nothing safe to write for it.
			ITsString tss = TsStringUtils.MakeString("text", UnknownWs);
			Assert.That(() => TsStringSerializer.SerializeTsStringToXml(tss, WritingSystemManager),
				Throws.TypeOf<InvalidOperationException>());
		}

		[Test]
		public void SerializeString_AStrWithUnknownWs_Fails()
		{
			ITsString tss = TsStringUtils.MakeString("text", EnWS);
			Assert.That(() => TsStringSerializer.SerializeTsStringToXml(tss, WritingSystemManager, UnknownWs),
				Throws.TypeOf<ArgumentException>());
		}

		[TestCase("<Prop ws=\"\" namedStyle=\"Emphasis\"/>")]
		[TestCase("<Prop wsBase=\"\" namedStyle=\"Emphasis\"/>")]
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

		[TestCase("<Str><Run ws=\"\">text</Run></Str>")]
		[TestCase("<Str><Run ws=\"\" namedStyle=\"Emphasis\">text</Run></Str>")]
		[TestCase("<Str><Run ws=\"en\" wsBase=\"\">text</Run></Str>")]
		[TestCase("<Str><Run ws=\"\">te</Run><Run ws=\"en\">xt</Run></Str>")]
		[TestCase("<AStr ws=\"en\"><Run ws=\"\">text</Run></AStr>")]
		public void DeserializeString_EmptyWs_IsDropped(string xml)
		{
			ITsString result = null;
			Assert.That(() => result = TsStringSerializer.DeserializeTsStringFromXml(xml, WritingSystemManager),
				Throws.Nothing);
			Assert.That(result.Text, Is.EqualTo("text"));
		}
	}
}
