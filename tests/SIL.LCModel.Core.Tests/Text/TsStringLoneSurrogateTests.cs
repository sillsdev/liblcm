// Copyright (c) 2026 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using NUnit.Framework;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Utils;

namespace SIL.LCModel.Core.Text
{
	/// <summary>
	/// Run text can contain unpaired UTF-16 surrogates (pasted or imported text). Such text must
	/// not make normalization, serialization or the character accessors throw.
	/// NOTE: the strings are built in code; NUnit attributes would turn lone surrogates into U+FFFD.
	/// </summary>
	[TestFixture]
	public class TsStringLoneSurrogateTests : TsSerializerTestsBase
	{
		private const char High = '\uD800';
		private const char Low = '\uDC00';

		private static readonly string[][] NamedCases =
		{
			new[] { "LoneHighMiddle", "a" + High + "b" },
			new[] { "LoneLowMiddle", "a" + Low + "b" },
			new[] { "LoneHighAtStart", High + "ab" },
			new[] { "LoneLowAtStart", Low + "ab" },
			new[] { "LoneHighAtEnd", "ab" + High },
			new[] { "LoneLowAtEnd", "ab" + Low },
			new[] { "ReversedPair", "" + Low + High },
			new[] { "LoneHighThenValidPair", "" + High + High + Low },
			new[] { "CombiningMarkAfterLoneHigh", "é" + High + "́" },
			new[] { "OnlyLoneHigh", "" + High },
		};

		private static string[] LoneSurrogateStrings()
		{
			return NamedCases.Select(c => c[1]).ToArray();
		}

		// Named with ASCII so the cases are filterable and readable in logs.
		private static IEnumerable<TestCaseData> LoneSurrogateCases()
		{
			return NamedCases.Select(c => new TestCaseData(c[1]).SetName("LoneSurrogate_" + c[0]));
		}
		private static string StrippedOfLoneSurrogates(string s)
		{
			var sb = new System.Text.StringBuilder();
			for (int i = 0; i < s.Length; i++)
			{
				if (char.IsSurrogatePair(s, i))
				{
					sb.Append(s[i]).Append(s[i + 1]);
					i++;
				}
				else if (!char.IsSurrogate(s[i]))
					sb.Append(s[i]);
			}
			return sb.ToString();
		}

		[TestCaseSource(nameof(LoneSurrogateCases))]
		public void SerializeTsStringToXml_DoesNotThrow_AndReloads(string text)
		{
			ITsString tss = TsStringUtils.MakeString(text, EnWS);
			string xml = null;
			Assert.DoesNotThrow(() => xml = TsStringSerializer.SerializeTsStringToXml(tss, WritingSystemManager));
			Assert.DoesNotThrow(() => XElement.Parse(xml), Escape(xml)); // well-formed
			ITsString reloaded = TsStringSerializer.DeserializeTsStringFromXml(xml, WritingSystemManager);
			// Lone surrogates are dropped on write; everything else survives (compared in NFC, since loading yields NFD).
			Assert.That(reloaded.get_NormalizedForm(FwNormalizationMode.knmNFC).Text ?? string.Empty, Is.EqualTo(StrippedOfLoneSurrogates(text).Normalize(System.Text.NormalizationForm.FormC)), Escape(xml));
		}

		[Test]
		public void SerializeTsStringToXml_LoneSurrogateInOneOfSeveralRuns()
		{
			var bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "one" + High, TsStringUtils.MakeProps(null, EnWS));
			bldr.Replace(bldr.Length, bldr.Length, "two", TsStringUtils.MakeProps(null, EsWS));
			string xml = TsStringSerializer.SerializeTsStringToXml(bldr.GetString(), WritingSystemManager);
			ITsString reloaded = TsStringSerializer.DeserializeTsStringFromXml(xml, WritingSystemManager);
			Assert.That(reloaded.Text, Is.EqualTo("onetwo"));
			Assert.That(reloaded.RunCount, Is.EqualTo(2));
		}

		[Test]
		public void SerializeTsStringToXml_ValidSurrogatePairNextToLoneSurrogate_KeepsPair()
		{
			ITsString tss = TsStringUtils.MakeString("\U0001F98A" + High + "\U0001F98A", EnWS);
			string xml = TsStringSerializer.SerializeTsStringToXml(tss, WritingSystemManager);
			Assert.That(TsStringSerializer.DeserializeTsStringFromXml(xml, WritingSystemManager).Text,
				Is.EqualTo("\U0001F98A\U0001F98A"));
		}

		[Test]
		public void StripInvalidXmlChars_RemovesOnlyUnpairedSurrogates()
		{
			Assert.That(TsStringSerializer.StripInvalidXmlChars("a" + High + "b" + Low + "c\U0001F98A"),
				Is.EqualTo("abc\U0001F98A"));
		}

		[TestCase(FwNormalizationMode.knmNFC)]
		[TestCase(FwNormalizationMode.knmNFD)]
		[TestCase(FwNormalizationMode.knmNFKC)]
		[TestCase(FwNormalizationMode.knmNFKD)]
		[TestCase(FwNormalizationMode.knmNFSC)]
		public void NormalizedForm_LoneSurrogate_DoesNotThrow_AndPassesThrough(FwNormalizationMode mode)
		{
			foreach (string text in LoneSurrogateStrings())
			{
				ITsString tss = TsStringUtils.MakeString(text, EnWS);
				ITsString normalized = null;
				Assert.DoesNotThrow(() => normalized = tss.get_NormalizedForm(mode), "mode " + mode + ", text " + Escape(text));
				// Pass-through: the lone surrogates are still there, in the same count.
				Assert.That(CountLone(normalized.Text), Is.EqualTo(CountLone(text)), Escape(text));
				Assert.DoesNotThrow(() => tss.get_IsNormalizedForm(mode));
			}
		}

		[Test]
		public void NormalizedForm_LoneSurrogate_StillNormalizesTheRest()
		{
			ITsString tss = TsStringUtils.MakeString("é" + High + "ö", EnWS);
			Assert.That(tss.get_NormalizedForm(FwNormalizationMode.knmNFC).Text, Is.EqualTo("é" + High + "ö"));
			ITsString composed = TsStringUtils.MakeString("é" + Low + "ö", EnWS);
			Assert.That(composed.get_NormalizedForm(FwNormalizationMode.knmNFD).Text, Is.EqualTo("é" + Low + "ö"));
		}

		[Test]
		public void NormalizedForm_LoneSurrogate_AcrossRuns_NFD()
		{
			var bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "é" + High, TsStringUtils.MakeProps(null, EnWS));
			bldr.Replace(bldr.Length, bldr.Length, "́x", TsStringUtils.MakeProps(null, EsWS));
			Assert.DoesNotThrow(() => bldr.GetString().get_NormalizedForm(FwNormalizationMode.knmNFD));
			Assert.DoesNotThrow(() => bldr.GetString().get_NormalizedForm(FwNormalizationMode.knmNFSC));
		}

		[Test]
		public void NfdAndFixOffsets_LoneSurrogate_FixesOffsets()
		{
			// U+00E9 decomposes to two chars, so the offset of "b" moves from 2 to 3. The lone high surrogate is kept.
			ITsString tss = TsStringUtils.MakeString("é" + High + "b", EnWS);
			IntPtr offset = Marshal.AllocHGlobal(sizeof(int));
			IntPtr table = Marshal.AllocHGlobal(IntPtr.Size);
			try
			{
				Marshal.WriteInt32(offset, 2);
				Marshal.WriteIntPtr(table, offset);
				ITsString result;
				tss.NfdAndFixOffsets(out result, new ArrayPtr(table), 1);
				Assert.That(result.Text, Is.EqualTo("é" + High + "b"));
				Assert.That(Marshal.ReadInt32(offset), Is.EqualTo(3));
			}
			finally
			{
				Marshal.FreeHGlobal(offset);
				Marshal.FreeHGlobal(table);
			}
		}

		[Test]
		public void CharAt_And_CharIndexNavigation_LoneSurrogate_DoNotThrow()
		{
			ITsString tss = TsStringUtils.MakeString("a" + High + Low + Low + High, EnWS);
			Assert.That(tss.CharAt(0), Is.EqualTo('a'));
			Assert.That(tss.CharAt(1), Is.EqualTo(0x10000)); // valid pair
			Assert.That(tss.CharAt(3), Is.EqualTo((int)Low)); // lone low
			Assert.That(tss.CharAt(4), Is.EqualTo((int)High)); // lone high at end
			Assert.That(tss.NextCharIndex(3), Is.EqualTo(4));
			Assert.That(tss.NextCharIndex(4), Is.EqualTo(5));
			Assert.That(tss.PrevCharIndex(3), Is.EqualTo(1)); // back over the valid pair
			Assert.That(tss.PrevCharIndex(4), Is.EqualTo(3));
		}

		[Test]
		public void IsCharWordForming_LoneSurrogate_DoesNotThrow()
		{
			ITsString tss = TsStringUtils.MakeString("a" + High + "b" + Low, EnWS);
			Assert.DoesNotThrow(() => tss.IsCharWordForming(1, WritingSystemManager));
			Assert.DoesNotThrow(() => tss.IsCharWordForming(3, WritingSystemManager));
		}

		[Test]
		public void Surrogates_CodePointAt()
		{
			Assert.That(Surrogates.CodePointAt("a" + High + Low, 1), Is.EqualTo(0x10000));
			Assert.That(Surrogates.CodePointAt("a" + High + Low, 2), Is.EqualTo((int)Low));
			Assert.That(Surrogates.CodePointAt("a" + High, 1), Is.EqualTo((int)High));
		}

		private static int CountLone(string s)
		{
			return s.Length - StrippedOfLoneSurrogates(s).Length;
		}

		private static string Escape(string s)
		{
			var sb = new System.Text.StringBuilder();
			foreach (char c in s)
				sb.AppendFormat(" U+{0:X4}", (int)c);
			return sb.ToString();
		}
	}
}
