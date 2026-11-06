// Copyright (c) 2026 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Schema;
using NUnit.Framework;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Utils;

namespace SIL.LCModel.Core.Text
{
	/// <summary>
	/// Checks that the text property XML writer and readers agree: every text property liblcm
	/// defines (every member of <see cref="FwTextPropType"/>) either survives a write then read
	/// through both <see cref="TsPropsSerializer"/> (StyleRules) and <see cref="TsStringSerializer"/>
	/// (string runs), or is deliberately not persisted, in which case the writer must leave it
	/// out. Whatever the writer emits, the reader must load: a record the reader rejects is
	/// reported as corrupted and cannot be opened.
	/// </summary>
	[TestFixture]
	public class TextPropsRoundTripTests : TsSerializerTestsBase
	{
		#region Property classification
		// Ws handles are only known once the fixture's writing system manager exists, so the
		// ws values are produced lazily.
		private const int EnWsMarker = int.MinValue + 1;
		private const int EsWsMarker = int.MinValue + 2;

		private static readonly int[] Toggles =
			{ (int)FwTextToggleVal.kttvOff, (int)FwTextToggleVal.kttvForceOn, (int)FwTextToggleVal.kttvInvert };
		private static readonly int[] ComBools = { 0, 1 };
		private static readonly int[] Lengths = { 0, 1, 12000, int.MaxValue };
		private static readonly int[] SignedLengths = { int.MinValue, -12000, -1, 0, 1, 12000, int.MaxValue };
		private static readonly int[] Counts = { 0, 1, 7, int.MaxValue };
		private static readonly int[] Colors =
		{
			(int)FwTextColor.kclrWhite, (int)FwTextColor.kclrBlack, (int)FwTextColor.kclrRed,
			(int)FwTextColor.kclrGreen, (int)FwTextColor.kclrBlue, (int)FwTextColor.kclrYellow,
			(int)FwTextColor.kclrMagenta, (int)FwTextColor.kclrCyan, (int)FwTextColor.kclrTransparent,
			0x05F1F8, 0x000001
		};

		private sealed class IntCase
		{
			public FwTextPropVar Var;
			public int[] Values;
		}

		private static IntCase Int(FwTextPropVar var, params int[] values)
		{
			return new IntCase { Var = var, Values = values };
		}

		/// <summary>
		/// Integer properties that persist, keyed by constant name, with the variation the reader
		/// assigns (the XML does not store a variation for most properties) and representative
		/// valid values, including boundaries and every enumeration value.
		/// </summary>
		private static readonly Dictionary<string, IntCase[]> IntProps = new Dictionary<string, IntCase[]>
		{
			{ nameof(FwTextPropType.ktptWs), new[] { Int(FwTextPropVar.ktpvDefault, EnWsMarker, EsWsMarker) } },
			{ nameof(FwTextPropType.ktptItalic), new[] { Int(FwTextPropVar.ktpvEnum, Toggles) } },
			{ nameof(FwTextPropType.ktptBold), new[] { Int(FwTextPropVar.ktpvEnum, Toggles) } },
			{ nameof(FwTextPropType.ktptSuperscript), new[] { Int(FwTextPropVar.ktpvEnum,
				(int)FwSuperscriptVal.kssvOff, (int)FwSuperscriptVal.kssvSuper, (int)FwSuperscriptVal.kssvSub) } },
			{ nameof(FwTextPropType.ktptUnderline), new[] { Int(FwTextPropVar.ktpvEnum,
				Enum.GetValues(typeof(FwUnderlineType)).Cast<int>().Where(v => v < (int)FwUnderlineType.kuntLim).Distinct().ToArray()) } },
			{ nameof(FwTextPropType.ktptFontSize), new[] { Int(FwTextPropVar.ktpvMilliPoint, Lengths),
				Int(FwTextPropVar.ktpvRelative, 0, 5000, int.MaxValue) } },
			{ nameof(FwTextPropType.ktptOffset), new[] { Int(FwTextPropVar.ktpvMilliPoint, SignedLengths),
				Int(FwTextPropVar.ktpvRelative, -5000, 0, 5000) } },
			{ nameof(FwTextPropType.ktptForeColor), new[] { Int(FwTextPropVar.ktpvDefault, Colors) } },
			{ nameof(FwTextPropType.ktptBackColor), new[] { Int(FwTextPropVar.ktpvDefault, Colors) } },
			{ nameof(FwTextPropType.ktptUnderColor), new[] { Int(FwTextPropVar.ktpvDefault, Colors) } },
			{ nameof(FwTextPropType.ktptBaseWs), new[] { Int(FwTextPropVar.ktpvDefault, EnWsMarker, EsWsMarker) } },
			{ nameof(FwTextPropType.ktptAlign), new[] { Int(FwTextPropVar.ktpvEnum,
				Enum.GetValues(typeof(FwTextAlign)).Cast<int>().Where(v => v < (int)FwTextAlign.ktalLim).Distinct().ToArray()) } },
			{ nameof(FwTextPropType.ktptFirstIndent), new[] { Int(FwTextPropVar.ktpvMilliPoint, SignedLengths) } },
			{ nameof(FwTextPropType.ktptLeadingIndent), new[] { Int(FwTextPropVar.ktpvMilliPoint, SignedLengths) } },
			{ nameof(FwTextPropType.ktptTrailingIndent), new[] { Int(FwTextPropVar.ktpvMilliPoint, SignedLengths) } },
			{ nameof(FwTextPropType.ktptSpaceBefore), new[] { Int(FwTextPropVar.ktpvMilliPoint, SignedLengths) } },
			{ nameof(FwTextPropType.ktptSpaceAfter), new[] { Int(FwTextPropVar.ktpvMilliPoint, SignedLengths) } },
			{ nameof(FwTextPropType.ktptLineHeight), new[] {
				// negative millipoint means "exact", positive "at least"
				Int(FwTextPropVar.ktpvMilliPoint, int.MinValue, -12000, -1, 0, 1, 12000, int.MaxValue),
				Int(FwTextPropVar.ktpvRelative, 0, 10000, 15000, int.MaxValue) } },
			{ nameof(FwTextPropType.ktptParaColor), new[] { Int(FwTextPropVar.ktpvDefault, Colors) } },
			{ nameof(FwTextPropType.ktptSpellCheck), new[] { Int(FwTextPropVar.ktpvEnum,
				(int)SpellingModes.ksmNormalCheck, (int)SpellingModes.ksmDoNotCheck, (int)SpellingModes.ksmForceCheck) } },
			{ nameof(FwTextPropType.ktptMarginTop), new[] { Int(FwTextPropVar.ktpvMilliPoint, SignedLengths) } },
			{ nameof(FwTextPropType.ktptRightToLeft), new[] { Int(FwTextPropVar.ktpvEnum, ComBools) } },
			{ nameof(FwTextPropType.ktptDirectionDepth), new[] { Int(FwTextPropVar.ktpvDefault, Counts) } },
			{ nameof(FwTextPropType.ktptPadLeading), new[] { Int(FwTextPropVar.ktpvMilliPoint, SignedLengths) } },
			{ nameof(FwTextPropType.ktptPadTrailing), new[] { Int(FwTextPropVar.ktpvMilliPoint, SignedLengths) } },
			{ nameof(FwTextPropType.ktptPadTop), new[] { Int(FwTextPropVar.ktpvMilliPoint, SignedLengths) } },
			{ nameof(FwTextPropType.ktptPadBottom), new[] { Int(FwTextPropVar.ktpvMilliPoint, SignedLengths) } },
			{ nameof(FwTextPropType.ktptBorderTop), new[] { Int(FwTextPropVar.ktpvMilliPoint, SignedLengths) } },
			{ nameof(FwTextPropType.ktptBorderBottom), new[] { Int(FwTextPropVar.ktpvMilliPoint, SignedLengths) } },
			{ nameof(FwTextPropType.ktptBorderLeading), new[] { Int(FwTextPropVar.ktpvMilliPoint, SignedLengths) } },
			{ nameof(FwTextPropType.ktptBorderTrailing), new[] { Int(FwTextPropVar.ktpvMilliPoint, SignedLengths) } },
			{ nameof(FwTextPropType.ktptBorderColor), new[] { Int(FwTextPropVar.ktpvDefault, Colors) } },
			{ nameof(FwTextPropType.ktptBulNumScheme), new[] { Int(FwTextPropVar.ktpvEnum, 0, 1, 10, 101, int.MaxValue) } },
			// int.MinValue is written as 0 on purpose (it means "not set"), so it is not a round-trip value.
			{ nameof(FwTextPropType.ktptBulNumStartAt), new[] { Int(FwTextPropVar.ktpvDefault, -1, 0, 1, 99, int.MaxValue) } },
			{ nameof(FwTextPropType.ktptKeepWithNext), new[] { Int(FwTextPropVar.ktpvEnum, ComBools) } },
			{ nameof(FwTextPropType.ktptKeepTogether), new[] { Int(FwTextPropVar.ktpvEnum, ComBools) } },
			{ nameof(FwTextPropType.ktptHyphenate), new[] { Int(FwTextPropVar.ktpvEnum, ComBools) } },
			{ nameof(FwTextPropType.ktptMaxLines), new[] { Int(FwTextPropVar.ktpvDefault, Counts) } },
			{ nameof(FwTextPropType.ktptCellBorderWidth), new[] { Int(FwTextPropVar.ktpvMilliPoint, Lengths) } },
			{ nameof(FwTextPropType.ktptCellSpacing), new[] { Int(FwTextPropVar.ktpvMilliPoint, Lengths) } },
			{ nameof(FwTextPropType.ktptCellPadding), new[] { Int(FwTextPropVar.ktpvMilliPoint, Lengths) } },
			{ nameof(FwTextPropType.ktptSetRowDefaults), new[] { Int(FwTextPropVar.ktpvEnum, ComBools) } },
			{ nameof(FwTextPropType.ktptRelLineHeight), new[] { Int(FwTextPropVar.ktpvMilliPoint, 0, 10000, int.MaxValue) } },
			{ nameof(FwTextPropType.ktptWidowOrphanControl), new[] { Int(FwTextPropVar.ktpvEnum, ComBools) } },
		};

		/// <summary>
		/// String properties that persist, keyed by constant name, with representative values.
		/// Produced lazily because some values need the fixture's writing systems.
		/// </summary>
		private Dictionary<string, string[]> StrProps()
		{
			Guid g1 = new Guid("397F43AE-E2B2-4f20-928A-1DF193C07674");
			Guid g2 = new Guid("85EE15C6-0799-46c6-8769-F9B3CE313AE2");
			Guid g3 = new Guid("644DF48A-3B60-45f4-80C7-739BE6E56A96");
			string ObjData(FwObjDataTypes type, Guid guid) => (char)type + MiscUtils.GetObjDataFromGuid(guid);
			return new Dictionary<string, string[]>
			{
				{ nameof(FwTextPropType.ktptFontFamily), new[] { "Times New Roman", "<default font>", "Charis SIL" } },
				{ nameof(FwTextPropType.ktptTags), new[] {
					MiscUtils.GetObjDataFromGuid(g1),
					MiscUtils.GetObjDataFromGuid(g1) + MiscUtils.GetObjDataFromGuid(g2) + MiscUtils.GetObjDataFromGuid(g3) } },
				{ nameof(FwTextPropType.ktptObjData), new[] {
					ObjData(FwObjDataTypes.kodtNameGuidHot, g1),
					ObjData(FwObjDataTypes.kodtOwnNameGuidHot, g2),
					ObjData(FwObjDataTypes.kodtContextString, g3),
					ObjData(FwObjDataTypes.kodtGuidMoveableObjDisp, g1),
					(char)FwObjDataTypes.kodtExternalPathName + "https://example.org/a b.html",
					(char)FwObjDataTypes.kodtEmbeddedObjectData + "<Footnote/>" } },
				{ nameof(FwTextPropType.ktptCustomBullet), new[] { "»", "*", "→ " } },
				{ nameof(FwTextPropType.ktptFontVariations), new[] { "smcp=1,liga=0", "185=1,851=2,57=12" } },
				{ nameof(FwTextPropType.ktptNamedStyle), new[] { "Normal", "Emphasized Text" } },
				{ nameof(FwTextPropType.ktptBulNumTxtBef), new[] { "(", "Chapter " } },
				{ nameof(FwTextPropType.ktptBulNumTxtAft), new[] { ")", ". " } },
				{ nameof(FwTextPropType.ktptBulNumFontInfo), new[] {
					// ints only, in the order the reader builds them (by property number)
					Bul(FwTextPropType.ktptItalic, 1) + Bul(FwTextPropType.ktptBold, 1) + Bul(FwTextPropType.ktptSuperscript, 2)
						+ Bul(FwTextPropType.ktptUnderline, 2) + Bul(FwTextPropType.ktptFontSize, 20000) + Bul(FwTextPropType.ktptOffset, -3000)
						+ Bul(FwTextPropType.ktptForeColor, 0xFF0000) + Bul(FwTextPropType.ktptBackColor, 0xFFFFFF)
						+ Bul(FwTextPropType.ktptUnderColor, 0x0000FF),
					BulStr(FwTextPropType.ktptFontFamily, "Times New Roman"),
					// BulletInfo writes font name then font features (LT-22351)
					Bul(FwTextPropType.ktptBold, 1) + BulStr(FwTextPropType.ktptFontFamily, "Charis SIL")
						+ BulStr(FwTextPropType.ktptFontVariations, "smcp=1"),
					BulStr(FwTextPropType.ktptFontVariations, "smcp=1"),
					// an integer property with no attribute name of its own is written as prop_<number>
					Bul(FwTextPropType.ktptBold, 1) + Bul(FwTextPropType.ktptBorderTop, 500) } },
				{ nameof(FwTextPropType.ktptWsStyle), new[] {
					WsStyle(EnWS, "", null, WsInt(FwTextPropType.ktptFontSize, FwTextPropVar.ktpvMilliPoint, 80),
						WsInt(FwTextPropType.ktptForeColor, FwTextPropVar.ktpvDefault, 0xFF602F)),
					WsStyle(EnWS, "Times New Roman", "185=1,851=2,57=12",
						WsInt(FwTextPropType.ktptFontSize, FwTextPropVar.ktpvMilliPoint, 80)),
					WsStyle(EnWS, "Charis SIL", null, WsInt(FwTextPropType.ktptBold, FwTextPropVar.ktpvEnum, 1))
						+ WsStyle(EsWS, "Doulos SIL", null, WsInt(FwTextPropType.ktptOffset, FwTextPropVar.ktpvMilliPoint, -2000)) } },
			};
		}

		/// <summary>Properties persisted in StyleRules only; a string run has no place for them.</summary>
		private static readonly HashSet<string> StyleRulesOnly = new HashSet<string>
		{
			nameof(FwTextPropType.ktptBulNumFontInfo), // written as a child element of <Prop>
			nameof(FwTextPropType.ktptWsStyle), // written as a child element of <Prop>
		};

		/// <summary>
		/// Properties deliberately not persisted. The writer must leave them out (and a reader
		/// must still load an old file that has them, see <see cref="OldFileAttributes_Load"/>).
		/// </summary>
		private static readonly Dictionary<string, (bool IsString, string Reason)> NotPersisted = new Dictionary<string, (bool, string)>
		{
			{ nameof(FwTextPropType.ktptCharStyle), (true, "obsolete, superseded by namedStyle; the reader has always ignored it") },
			{ nameof(FwTextPropType.ktptParaStyle), (true, "obsolete, superseded by namedStyle; the reader has always ignored it") },
			{ nameof(FwTextPropType.ktptTabList), (true, "tabs are not supported; the reader has always ignored it") },
			{ nameof(FwTextPropType.ktptTabDef), (false, "tabs are not supported; the reader has always ignored it") },
			{ nameof(FwTextPropType.ktptEditable), (false, "a view-construction property; the run reader has always ignored it, so persisting it would make existing text read-only") },
			{ nameof(FwTextPropType.ktptTableRule), (false, "a view-construction property never written; a new attribute name would make older readers reject the record") },
		};

		/// <summary>Members that are not stored as text property attributes at all.</summary>
		private static readonly Dictionary<string, string> NotAttributes = new Dictionary<string, string>
		{
			{ nameof(FwTextPropType.ktptFieldName), "export-only: written as a <Field> element around runs" },
			{ nameof(FwTextPropType.ktptMarkItem), "export-only: written as an <Item> element around a run" },
		};

		/// <summary>Constants that share a number with another constant of the same kind.</summary>
		private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
		{
			{ nameof(FwTextPropType.ktptMarginLeading), nameof(FwTextPropType.ktptLeadingIndent) },
			{ nameof(FwTextPropType.ktptMarginTrailing), nameof(FwTextPropType.ktptTrailingIndent) },
			{ nameof(FwTextPropType.ktptMswMarginTop), nameof(FwTextPropType.ktptSpaceBefore) },
			{ nameof(FwTextPropType.ktptMarginBottom), nameof(FwTextPropType.ktptSpaceAfter) },
		};
		#endregion

		#region Completeness
		/// <summary>
		/// Every constant liblcm defines must be classified, so a new text property cannot be
		/// added without deciding (and testing) how it persists.
		/// </summary>
		[Test]
		public void EveryTextPropertyIsClassified()
		{
			var strNames = StrProps().Keys;
			var unclassified = Enum.GetNames(typeof(FwTextPropType))
				.Where(n => !IntProps.ContainsKey(n) && !strNames.Contains(n) && !NotPersisted.ContainsKey(n)
					&& !NotAttributes.ContainsKey(n) && !Aliases.ContainsKey(n))
				.ToList();
			Assert.That(unclassified, Is.Empty,
				"Text properties with no round-trip test; add them to IntProps/StrProps (or NotPersisted with a reason)");
			foreach (KeyValuePair<string, string> alias in Aliases)
				Assert.That(Enum.Parse(typeof(FwTextPropType), alias.Key), Is.EqualTo(Enum.Parse(typeof(FwTextPropType), alias.Value)));
		}
		#endregion

		#region Round trip of every valid value
		/// <summary>One case per integer property, variation and value.</summary>
		public static IEnumerable<TestCaseData> IntCases()
		{
			foreach (KeyValuePair<string, IntCase[]> prop in IntProps)
			{
				foreach (IntCase c in prop.Value)
				{
					foreach (int value in c.Values)
						yield return new TestCaseData(prop.Key, c.Var, value).SetName($"{{m}}({prop.Key},{c.Var},{ValueName(value)})");
				}
			}
		}

		/// <summary>One case per string property and value (values indexed; see StrProps).</summary>
		public static IEnumerable<TestCaseData> StrCases()
		{
			var fixture = new TextPropsRoundTripTests();
			// The writing system handles are not needed to count the cases.
			foreach (KeyValuePair<string, string[]> prop in fixture.StrPropsForEnumeration())
			{
				for (int i = 0; i < prop.Value.Length; i++)
					yield return new TestCaseData(prop.Key, i).SetName($"{{m}}({prop.Key},{i})");
			}
		}

		private Dictionary<string, string[]> StrPropsForEnumeration()
		{
			// EnWS/EsWS are 0 before fixture setup; the values built here are only counted.
			return StrProps();
		}

		[TestCaseSource(nameof(IntCases))]
		public void IntProp_RoundTripsThroughStyleRules(string name, FwTextPropVar var, int value)
		{
			ITsPropsBldr bldr = TsStringUtils.MakePropsBldr();
			bldr.SetIntPropValues((int)Tpt(name), (int)var, ResolveWs(value));
			AssertStyleRulesRoundTrip(bldr.GetTextProps());
		}

		[TestCaseSource(nameof(IntCases))]
		public void IntProp_RoundTripsThroughStringRun(string name, FwTextPropVar var, int value)
		{
			ITsPropsBldr bldr = TsStringUtils.MakePropsBldr();
			bldr.SetIntPropValues((int)FwTextPropType.ktptWs, 0, EnWS);
			bldr.SetIntPropValues((int)Tpt(name), (int)var, ResolveWs(value));
			AssertRunRoundTrip(bldr.GetTextProps(), "text");
		}

		[TestCaseSource(nameof(StrCases))]
		public void StrProp_RoundTripsThroughStyleRules(string name, int index)
		{
			ITsPropsBldr bldr = TsStringUtils.MakePropsBldr();
			bldr.SetStrPropValue((int)Tpt(name), StrProps()[name][index]);
			AssertStyleRulesRoundTrip(bldr.GetTextProps());
		}

		[TestCaseSource(nameof(StrCases))]
		public void StrProp_RoundTripsThroughStringRun(string name, int index)
		{
			if (StyleRulesOnly.Contains(name))
			{
				// Not representable on a run: the writer leaves it out, and the string loads.
				ITsPropsBldr b = TsStringUtils.MakePropsBldr();
				b.SetIntPropValues((int)FwTextPropType.ktptWs, 0, EnWS);
				b.SetStrPropValue((int)Tpt(name), StrProps()[name][index]);
				ITsString tss = RunRoundTrip(b.GetTextProps(), "text", out _);
				Assert.That(tss.get_Properties(0).StrPropCount, Is.EqualTo(0));
				return;
			}
			string value = StrProps()[name][index];
			ITsPropsBldr bldr = TsStringUtils.MakePropsBldr();
			bldr.SetIntPropValues((int)FwTextPropType.ktptWs, 0, EnWS);
			bldr.SetStrPropValue((int)Tpt(name), value);
			string text = "text";
			if (name == nameof(FwTextPropType.ktptObjData))
			{
				switch ((FwObjDataTypes)value[0])
				{
					case FwObjDataTypes.kodtNameGuidHot:
					case FwObjDataTypes.kodtOwnNameGuidHot:
					case FwObjDataTypes.kodtContextString:
					case FwObjDataTypes.kodtGuidMoveableObjDisp:
						text = StringUtils.kszObject;
						break;
				}
			}
			AssertRunRoundTrip(bldr.GetTextProps(), text);
		}

		[Test]
		public void AllPropertiesAtOnce_RoundTripThroughStyleRules()
		{
			ITsPropsBldr bldr = TsStringUtils.MakePropsBldr();
			foreach (KeyValuePair<string, IntCase[]> prop in IntProps)
			{
				IntCase c = prop.Value[0];
				bldr.SetIntPropValues((int)Tpt(prop.Key), (int)c.Var, ResolveWs(c.Values[c.Values.Length - 1]));
			}
			foreach (KeyValuePair<string, string[]> prop in StrProps())
				bldr.SetStrPropValue((int)Tpt(prop.Key), prop.Value[prop.Value.Length - 1]);
			AssertStyleRulesRoundTrip(bldr.GetTextProps());
		}
		#endregion

		#region Not persisted
		public static IEnumerable<TestCaseData> NotPersistedCases()
		{
			foreach (string name in NotPersisted.Keys)
				yield return new TestCaseData(name).SetName($"NotPersisted_IsLeftOutAndLoads({name})");
		}

		[TestCaseSource(nameof(NotPersistedCases))]
		public void NotPersisted_IsLeftOutAndLoads(string name)
		{
			FwTextPropType tpt = Tpt(name);
			bool isString = NotPersisted[name].IsString;

			ITsPropsBldr bldr = TsStringUtils.MakePropsBldr();
			bldr.SetIntPropValues((int)FwTextPropType.ktptWs, 0, EnWS);
			if (isString)
				bldr.SetStrPropValue((int)tpt, "x");
			else
				bldr.SetIntPropValues((int)tpt, (int)FwTextPropVar.ktpvEnum, 1);
			ITsTextProps props = bldr.GetTextProps();

			string rulesXml = TsPropsSerializer.SerializePropsToXml(props, WritingSystemManager);
			ITsTextProps rules = TsPropsSerializer.DeserializePropsFromXml(rulesXml, WritingSystemManager);
			Assert.That(rules.IntPropCount, Is.EqualTo(1), rulesXml);
			Assert.That(rules.StrPropCount, Is.EqualTo(0), rulesXml);

			ITsString run = RunRoundTrip(props, "text", out string runXml);
			Assert.That(run.get_Properties(0).IntPropCount, Is.EqualTo(1), runXml);
			Assert.That(run.get_Properties(0).StrPropCount, Is.EqualTo(0), runXml);
		}
		#endregion

		#region Old files
		/// <summary>
		/// Attribute forms that earlier versions of liblcm wrote must still load.
		/// </summary>
		[TestCase("MarginTop='1500'", FwTextPropType.ktptMarginTop, 1500, FwTextPropVar.ktpvMilliPoint)]
		[TestCase("marginTop='1500'", FwTextPropType.ktptMarginTop, 1500, FwTextPropVar.ktpvMilliPoint)]
		[TestCase("leadingIndent='-1000'", FwTextPropType.ktptLeadingIndent, -1000, FwTextPropVar.ktpvMilliPoint)]
		[TestCase("padTop='4294967295'", FwTextPropType.ktptPadTop, -1, FwTextPropVar.ktpvMilliPoint)]
		[TestCase("bold='7'", FwTextPropType.ktptBold, 7, FwTextPropVar.ktpvEnum)]
		[TestCase("align='99'", FwTextPropType.ktptAlign, 99, FwTextPropVar.ktpvEnum)]
		[TestCase("offset='-3000' offsetUnit='mpt'", FwTextPropType.ktptOffset, -3000, FwTextPropVar.ktpvMilliPoint)]
		[TestCase("offset='5' offsetUnit='0'", FwTextPropType.ktptOffset, 5, FwTextPropVar.ktpvDefault)]
		[TestCase("hyphenate='1'", FwTextPropType.ktptHyphenate, 1, FwTextPropVar.ktpvEnum)]
		[TestCase("cellPadding='300'", FwTextPropType.ktptCellPadding, 300, FwTextPropVar.ktpvMilliPoint)]
		public void OldFileIntAttribute_Loads(string attrs, FwTextPropType tpt, int value, FwTextPropVar var)
		{
			ITsTextProps rules = TsPropsSerializer.DeserializePropsFromXml($"<Prop {attrs}/>", WritingSystemManager);
			Assert.That(rules.GetIntPropValues((int)tpt, out int rulesVar), Is.EqualTo(value));
			Assert.That(rulesVar, Is.EqualTo((int)var));

			ITsString tss = TsStringSerializer.DeserializeTsStringFromXml($"<Str><Run ws='en' {attrs}>text</Run></Str>", WritingSystemManager);
			Assert.That(tss.get_Properties(0).GetIntPropValues((int)tpt, out int runVar), Is.EqualTo(value));
			Assert.That(runVar, Is.EqualTo((int)var));
		}

		/// <summary>
		/// Legacy attributes that earlier versions wrote but that are not persisted any more
		/// must load (and be ignored) in both readers.
		/// </summary>
		[TestCase("tabList='abc'")]
		[TestCase("tabDef='36000'")]
		[TestCase("charStyle='Emphasis'")]
		[TestCase("paraStyle='Normal'")]
		[TestCase("editable='not'")]
		public void OldFileAttributes_Load(string attrs)
		{
			ITsTextProps rules = TsPropsSerializer.DeserializePropsFromXml($"<Prop {attrs}/>", WritingSystemManager);
			Assert.That(rules.IntPropCount + rules.StrPropCount, Is.EqualTo(0));

			ITsString tss = TsStringSerializer.DeserializeTsStringFromXml($"<Str><Run ws='en' {attrs}>text</Run></Str>", WritingSystemManager);
			Assert.That(tss.get_Properties(0).IntPropCount, Is.EqualTo(1)); // ws only
			Assert.That(tss.get_Properties(0).StrPropCount, Is.EqualTo(0));
		}

		[Test]
		public void MarginTop_IsWrittenWithTheNameOlderReadersAccept()
		{
			ITsPropsBldr bldr = TsStringUtils.MakePropsBldr();
			bldr.SetIntPropValues((int)FwTextPropType.ktptMarginTop, (int)FwTextPropVar.ktpvMilliPoint, 1500);
			string xml = TsPropsSerializer.SerializePropsToXml(bldr.GetTextProps(), WritingSystemManager);
			Assert.That(StripNewLines(xml), Is.EqualTo("<Prop marginTop=\"1500\"></Prop>"));
		}
		#endregion

		#region Values outside the valid range
		/// <summary>
		/// Whatever value a property holds, the writer must not produce XML the reader rejects:
		/// a rejected record cannot be opened. Values outside a property's documented range are
		/// tolerated by the reader (and kept), malformed binary values are left out by the writer.
		/// </summary>
		public static IEnumerable<TestCaseData> AnyIntValueCases()
		{
			int[] values = { int.MinValue, -12000, -1, 0, 1, 2, 3, 7, 99, 0xFFFFFF, 0x7F000000, int.MaxValue };
			FwTextPropVar[] vars = { FwTextPropVar.ktpvDefault, FwTextPropVar.ktpvMilliPoint, FwTextPropVar.ktpvRelative, FwTextPropVar.ktpvEnum };
			IEnumerable<int> numbers = Enum.GetValues(typeof(FwTextPropType)).Cast<int>().Distinct()
				.Where(n => n != (int)FwTextPropType.ktptWs && n != (int)FwTextPropType.ktptBaseWs)
				.Concat(new[] { 1000, 30000 });
			foreach (int tpt in numbers)
			{
				foreach (FwTextPropVar var in vars)
					yield return new TestCaseData(tpt, var, values).SetName($"AnyIntValue_Loads({tpt},{var})");
			}
		}

		[TestCase(FwTextPropType.ktptWs)]
		[TestCase(FwTextPropType.ktptBaseWs)]
		public void AnyWsValue_Loads(FwTextPropType tpt)
		{
			// An unknown handle is not covered: it is written as ws="", which the readers reject.
			foreach (int value in new[] { EnWS, EsWS, 0, -1, int.MinValue })
			{
				foreach (FwTextPropVar var in new[] { FwTextPropVar.ktpvDefault, FwTextPropVar.ktpvEnum })
				{
					ITsPropsBldr bldr = TsStringUtils.MakePropsBldr();
					bldr.SetIntPropValues((int)FwTextPropType.ktptWs, 0, EnWS);
					try
					{
						bldr.SetIntPropValues((int)tpt, (int)var, value);
					}
					catch (ArgumentOutOfRangeException)
					{
						continue; // text props cannot hold this value, so it can never be written
					}
					AssertWrittenXmlLoads(bldr.GetTextProps(), $"{tpt}, var {var}, value {value}");
				}
			}
		}

		[TestCaseSource(nameof(AnyIntValueCases))]
		public void AnyIntValue_Loads(int tpt, FwTextPropVar var, int[] values)
		{
			foreach (int value in values)
			{
				ITsPropsBldr bldr = TsStringUtils.MakePropsBldr();
				bldr.SetIntPropValues((int)FwTextPropType.ktptWs, 0, EnWS);
				bldr.SetIntPropValues(tpt, (int)var, value);
				AssertWrittenXmlLoads(bldr.GetTextProps(), $"tpt {tpt}, var {var}, value {value}");
			}
		}

		public static IEnumerable<TestCaseData> AnyStrValueCases()
		{
			string[] values =
			{
				"x", "ab", "abc", "\u0001", "\u0001\u0002\u0003", "\u0006\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008",
				"\u0003\u0001", "\u0003\u0001\u0002\u0003\u0004\u0005\u0006\u0007", "\uFFFF\uFFFF\uFFFF", "\u0002\u0002\u0002\u0002",
				"a\uD800b", "\uDC00x", "x\uD800", "\U0001F600 paired"
			};
			IEnumerable<int> numbers = Enum.GetValues(typeof(FwTextPropType)).Cast<int>().Distinct().Concat(new[] { 1000, 30000 });
			foreach (int tpt in numbers)
				yield return new TestCaseData(tpt, values).SetName($"AnyStrValue_Loads({tpt})");
		}

		[TestCaseSource(nameof(AnyStrValueCases))]
		public void AnyStrValue_Loads(int tpt, string[] values)
		{
			foreach (string value in values)
			{
				ITsPropsBldr bldr = TsStringUtils.MakePropsBldr();
				bldr.SetIntPropValues((int)FwTextPropType.ktptWs, 0, EnWS);
				bldr.SetStrPropValue(tpt, value);
				AssertWrittenXmlLoads(bldr.GetTextProps(), $"tpt {tpt}, value {Escape(value)}");
			}
		}

		[Test]
		public void NegativeValue_RoundTripsWhenCultureRedefinesNegativeSign()
		{
			CultureInfo oldCulture = Thread.CurrentThread.CurrentCulture;
			try
			{
				var culture = new CultureInfo("fi");
				culture.NumberFormat.NegativeSign = "+";
				Thread.CurrentThread.CurrentCulture = culture;
				ITsPropsBldr bldr = TsStringUtils.MakePropsBldr();
				bldr.SetIntPropValues((int)FwTextPropType.ktptWs, 0, EnWS);
				bldr.SetIntPropValues((int)FwTextPropType.ktptLeadingIndent, (int)FwTextPropVar.ktpvMilliPoint, -1000);
				bldr.SetIntPropValues((int)FwTextPropType.ktptOffset, (int)FwTextPropVar.ktpvMilliPoint, -3000);
				bldr.SetIntPropValues((int)FwTextPropType.ktptLineHeight, (int)FwTextPropVar.ktpvMilliPoint, -12000);
				AssertStyleRulesRoundTrip(bldr.GetTextProps());
				AssertRunRoundTrip(bldr.GetTextProps(), "text");
			}
			finally
			{
				Thread.CurrentThread.CurrentCulture = oldCulture;
			}
		}

		[Test]
		public void NegativeRelativeLineHeight_IsWrittenWithoutSign()
		{
			// Older readers reject a negative line height, and a negative relative height means
			// nothing to Views, so the writer keeps writing the absolute value as it always has.
			ITsPropsBldr bldr = TsStringUtils.MakePropsBldr();
			bldr.SetIntPropValues((int)FwTextPropType.ktptLineHeight, (int)FwTextPropVar.ktpvRelative, -5000);
			string xml = TsPropsSerializer.SerializePropsToXml(bldr.GetTextProps(), WritingSystemManager);
			Assert.That(StripNewLines(xml), Is.EqualTo("<Prop lineHeight=\"5000\" lineHeightUnit=\"rel\"></Prop>"));
		}

		// Built in code: attribute arguments cannot carry an unpaired surrogate (it becomes U+FFFD).
		private static readonly string[][] SurrogateCases =
		{
			new[] { "a\uD800b", "ab" },
			new[] { "\uDC00a", "a" },
			new[] { "a\uD800", "a" },
			new[] { "a\uDC00\uD800b", "ab" },
			new[] { "a\U0001F600b", "a\U0001F600b" },
		};

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		[TestCase(4)]
		public void UnpairedSurrogates_AreStrippedFromStringProperties(int index)
		{
			string value = SurrogateCases[index][0], expected = SurrogateCases[index][1];
			ITsPropsBldr bldr = TsStringUtils.MakePropsBldr();
			bldr.SetStrPropValue((int)FwTextPropType.ktptCustomBullet, value);
			string xml = TsPropsSerializer.SerializePropsToXml(bldr.GetTextProps(), WritingSystemManager);
			ITsTextProps props = TsPropsSerializer.DeserializePropsFromXml(xml, WritingSystemManager);
			Assert.That(props.GetStrPropValue((int)FwTextPropType.ktptCustomBullet), Is.EqualTo(expected));
		}

		/// <summary>
		/// A string property whose value is empty, or becomes empty once the characters XML cannot hold are
		/// removed, is stored as null by liblcm. It must write, read back and write again without throwing.
		/// </summary>
		public static IEnumerable<TestCaseData> EmptyStrValueCases()
		{
			foreach (int tpt in Enum.GetValues(typeof(FwTextPropType)).Cast<int>().Distinct())
				yield return new TestCaseData(tpt).SetName($"{{m}}({tpt})");
		}

		private static readonly int[] PlainTextStrProps =
		{
			(int)FwTextPropType.ktptFontFamily, (int)FwTextPropType.ktptNamedStyle, (int)FwTextPropType.ktptBulNumTxtBef,
			(int)FwTextPropType.ktptBulNumTxtAft, (int)FwTextPropType.ktptCustomBullet, (int)FwTextPropType.ktptFontVariations
		};

		[TestCaseSource(nameof(EmptyStrValueCases))]
		public void EmptyStrValue_SavesAgainThroughStyleRules(int tpt)
		{
			foreach (string value in new[] { string.Empty, "\u0001" })
			{
				ITsPropsBldr bldr = TsStringUtils.MakePropsBldr();
				bldr.SetStrPropValue(tpt, value);
				string xml = null;
				Assert.DoesNotThrow(() =>
				{
					xml = TsPropsSerializer.SerializePropsToXml(bldr.GetTextProps(), WritingSystemManager);
					ITsTextProps back = TsPropsSerializer.DeserializePropsFromXml(xml, WritingSystemManager);
					TsPropsSerializer.SerializePropsToXml(back, WritingSystemManager);
					if (Array.IndexOf(PlainTextStrProps, tpt) >= 0)
						Assert.That(back.GetStrPropValue(tpt), Is.Null.Or.Empty);
				}, $"tpt {tpt}, value {Escape(value)}, xml {xml}");
			}
		}

		[TestCaseSource(nameof(EmptyStrValueCases))]
		public void EmptyStrValue_SavesAgainThroughStringRun(int tpt)
		{
			foreach (string value in new[] { string.Empty, "\u0001" })
			{
				ITsPropsBldr bldr = TsStringUtils.MakePropsBldr();
				bldr.SetIntPropValues((int)FwTextPropType.ktptWs, 0, EnWS);
				bldr.SetStrPropValue(tpt, value);
				string xml = null;
				Assert.DoesNotThrow(() =>
				{
					ITsString back = RunRoundTrip(bldr.GetTextProps(), "text", out xml);
					TsStringSerializer.SerializeTsStringToXml(back, WritingSystemManager);
					if (Array.IndexOf(PlainTextStrProps, tpt) >= 0)
						Assert.That(back.get_Properties(0).GetStrPropValue(tpt), Is.Null.Or.Empty);
				}, $"tpt {tpt}, value {Escape(value)}, xml {xml}");
			}
		}

		[Test]
		public void BulNumFontInfo_PropertyNumberAboveCharRange_IsSkipped()
		{
			// Only hand-made data has this; storing it would renumber it to another property.
			ITsTextProps props = TsPropsSerializer.DeserializePropsFromXml(
				"<Prop><BulNumFontInfo bold='on' prop_99999='1' prop_138='500'/></Prop>", WritingSystemManager);
			Assert.That(props.GetStrPropValue((int)FwTextPropType.ktptBulNumFontInfo),
				Is.EqualTo(Bul(FwTextPropType.ktptBold, 1) + Bul(FwTextPropType.ktptBorderTop, 500)));
		}

		[TestCase("-1")]
		[TestCase("-3")]
		public void NegativeUnit_IsRejected(string unit)
		{
			// No writer emits a negative variation, and setting one would remove the property.
			Assert.That(() => TsPropsSerializer.DeserializePropsFromXml($"<Prop fontsize='10' fontsizeUnit='{unit}'/>", WritingSystemManager),
				Throws.TypeOf<XmlSchemaException>());
		}

		[Test]
		public void PartialTags_AreLeftOutAndLoad()
		{
			// Fewer characters than one GUID needs: nothing to write.
			ITsPropsBldr bldr = TsStringUtils.MakePropsBldr();
			bldr.SetStrPropValue((int)FwTextPropType.ktptTags, "abc");
			string xml = TsPropsSerializer.SerializePropsToXml(bldr.GetTextProps(), WritingSystemManager);
			ITsTextProps props = TsPropsSerializer.DeserializePropsFromXml(xml, WritingSystemManager);
			Assert.That(props.StrPropCount, Is.EqualTo(0), xml);
			// Re-save must work too.
			TsPropsSerializer.SerializePropsToXml(props, WritingSystemManager);
		}
		#endregion

		#region Helpers
		private static FwTextPropType Tpt(string name)
		{
			return (FwTextPropType)Enum.Parse(typeof(FwTextPropType), name);
		}

		private int ResolveWs(int value)
		{
			if (value == EnWsMarker)
				return EnWS;
			if (value == EsWsMarker)
				return EsWS;
			return value;
		}

		private static string ValueName(int value)
		{
			if (value == EnWsMarker)
				return "en";
			if (value == EsWsMarker)
				return "es";
			return value.ToString();
		}

		private static string Escape(string value)
		{
			return string.Concat(value.Select(c => c < 0x20 || c > 0x7E ? $"\\u{(int)c:X4}" : c.ToString()));
		}

		private static string Bul(FwTextPropType tpt, int value)
		{
			return new string(new[] { (char)tpt, (char)(value & 0xFFFF), (char)((value >> 16) & 0xFFFF) });
		}

		private static string BulStr(FwTextPropType tpt, string value)
		{
			return (char)tpt + value + "\u0000";
		}

		private static string WsInt(FwTextPropType tpt, FwTextPropVar var, int value)
		{
			return new string(new[] { (char)tpt, (char)var, (char)(value & 0xFFFF), (char)((value >> 16) & 0xFFFF) });
		}

		private static string WsStyle(int ws, string fontFamily, string fontVariations, params string[] intProps)
		{
			var sb = new StringBuilder();
			sb.Append((char)(ws & 0xFFFF)).Append((char)((ws >> 16) & 0xFFFF));
			sb.Append((char)fontFamily.Length).Append(fontFamily);
			if (fontVariations != null)
			{
				sb.Append(unchecked((char)-1));
				sb.Append((char)FwTextPropType.ktptFontVariations).Append((char)fontVariations.Length).Append(fontVariations);
			}
			sb.Append((char)intProps.Length);
			foreach (string p in intProps)
				sb.Append(p);
			return sb.ToString();
		}

		private void AssertStyleRulesRoundTrip(ITsTextProps expected)
		{
			string xml = TsPropsSerializer.SerializePropsToXml(expected, WritingSystemManager);
			ITsTextProps actual;
			try
			{
				actual = TsPropsSerializer.DeserializePropsFromXml(xml, WritingSystemManager);
			}
			catch (Exception e)
			{
				Assert.Fail($"StyleRules written by liblcm failed to load ({e.GetType().Name}: {e.Message}): {xml}");
				return;
			}
			AssertSameProps(expected, actual, xml);
		}

		private ITsString RunRoundTrip(ITsTextProps props, string text, out string xml)
		{
			ITsStrBldr strBldr = TsStringUtils.MakeStrBldr();
			strBldr.Replace(0, 0, text, props);
			xml = TsStringSerializer.SerializeTsStringToXml(strBldr.GetString(), WritingSystemManager);
			try
			{
				return TsStringSerializer.DeserializeTsStringFromXml(xml, WritingSystemManager);
			}
			catch (Exception e)
			{
				Assert.Fail($"String written by liblcm failed to load ({e.GetType().Name}: {e.Message}): {xml}");
				return null;
			}
		}

		private void AssertRunRoundTrip(ITsTextProps expected, string text)
		{
			ITsString actual = RunRoundTrip(expected, text, out string xml);
			Assert.That(actual.RunCount, Is.EqualTo(1), xml);
			Assert.That(actual.Text, Is.EqualTo(text), xml);
			AssertSameProps(expected, actual.get_Properties(0), xml);
		}

		private void AssertWrittenXmlLoads(ITsTextProps props, string what)
		{
			string rulesXml = null, runXml = null;
			try
			{
				rulesXml = TsPropsSerializer.SerializePropsToXml(props, WritingSystemManager);
				TsPropsSerializer.DeserializePropsFromXml(rulesXml, WritingSystemManager);
				ITsStrBldr strBldr = TsStringUtils.MakeStrBldr();
				strBldr.Replace(0, 0, "text", props);
				runXml = TsStringSerializer.SerializeTsStringToXml(strBldr.GetString(), WritingSystemManager);
				TsStringSerializer.DeserializeTsStringFromXml(runXml, WritingSystemManager);
			}
			catch (Exception e)
			{
				Assert.Fail($"{what}: {e.GetType().Name}: {e.Message}\nStyleRules: {rulesXml}\nRun: {runXml}");
			}
		}

		private static void AssertSameProps(ITsTextProps expected, ITsTextProps actual, string xml)
		{
			Assert.That(Describe(actual), Is.EqualTo(Describe(expected)), xml);
		}

		private static string Describe(ITsTextProps props)
		{
			var parts = new List<string>();
			for (int i = 0; i < props.IntPropCount; i++)
			{
				int value = props.GetIntProp(i, out int tpt, out int var);
				parts.Add($"int{tpt}/{var}={value}");
			}
			for (int i = 0; i < props.StrPropCount; i++)
			{
				string value = props.GetStrProp(i, out int tpt);
				parts.Add($"str{tpt}={Escape(value)}");
			}
			parts.Sort(StringComparer.Ordinal);
			return string.Join("; ", parts);
		}
		#endregion
	}
}
