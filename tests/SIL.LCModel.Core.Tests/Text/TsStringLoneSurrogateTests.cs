// Copyright (c) 2026 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using NUnit.Framework;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Utils;

namespace SIL.LCModel.Core.Text
{
	/// <summary>
	/// Text can contain unpaired UTF-16 surrogates (pasted or imported text). Normalization, serialization and
	/// the character accessors must not throw on it; the model replaces such a surrogate with U+FFFD on input
	/// (so stored text always equals in-memory text) and the serializer does the same as a backstop.
	/// NOTE: the strings are built in code with \u escapes; NUnit attributes would turn lone surrogates into U+FFFD.
	/// </summary>
	[TestFixture]
	public class TsStringLoneSurrogateTests : TsSerializerTestsBase
	{
		private const char High = '\uD800';
		private const char Low = '\uDC00';
		private const string Fffd = "�";
		private const string Acute = "́";

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
			new[] { "CombiningMarkAfterLoneHigh", "e" + Acute + High + Acute },
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

		private static string Replaced(string s)
		{
			var sb = new StringBuilder();
			for (int i = 0; i < s.Length; i++)
			{
				if (char.IsSurrogatePair(s, i))
				{
					sb.Append(s[i]).Append(s[i + 1]);
					i++;
				}
				else
					sb.Append(char.IsSurrogate(s[i]) ? '�' : s[i]);
			}
			return sb.ToString();
		}

		private static int CountLone(string s)
		{
			int n = 0;
			for (int i = 0; i < s.Length; i++)
			{
				if (char.IsSurrogatePair(s, i))
					i++;
				else if (char.IsSurrogate(s[i]))
					n++;
			}
			return n;
		}

		private static string Escape(string s)
		{
			var sb = new StringBuilder();
			foreach (char c in s)
				sb.AppendFormat(" U+{0:X4}", (int)c);
			return sb.ToString();
		}

		#region Serializer backstop

		[TestCaseSource(nameof(LoneSurrogateCases))]
		public void SerializeTsStringToXml_ReplacesLoneSurrogate_AndReloads(string text)
		{
			ITsString tss = TsStringUtils.MakeString(text, EnWS);
			string xml = null;
			Assert.DoesNotThrow(() => xml = TsStringSerializer.SerializeTsStringToXml(tss, WritingSystemManager));
			Assert.DoesNotThrow(() => XElement.Parse(xml), Escape(xml)); // well-formed
			ITsString reloaded = TsStringSerializer.DeserializeTsStringFromXml(xml, WritingSystemManager);
			// Lone surrogates become U+FFFD, so nothing is lost and lengths do not change (compared in NFC, since loading yields NFD).
			string expected = Replaced(text).Normalize(NormalizationForm.FormC);
			Assert.That(reloaded.get_NormalizedForm(FwNormalizationMode.knmNFC).Text, Is.EqualTo(expected), Escape(xml));
		}

		[TestCaseSource(nameof(LoneSurrogateCases))]
		public void SerializeTsStringToXml_LoneSurrogate_ResaveIsStable(string text)
		{
			ITsString tss = TsStringUtils.MakeString(text, EnWS);
			string xml1 = TsStringSerializer.SerializeTsStringToXml(tss, WritingSystemManager);
			ITsString reloaded = TsStringSerializer.DeserializeTsStringFromXml(xml1, WritingSystemManager);
			Assert.That(TsStringSerializer.SerializeTsStringToXml(reloaded, WritingSystemManager), Is.EqualTo(xml1));
		}

		[Test]
		public void SerializeTsStringToXml_RunOfOnlyLoneSurrogates_IsNotWrittenEmpty()
		{
			var bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "word", TsStringUtils.MakeProps(null, EsWS));
			bldr.Replace(bldr.Length, bldr.Length, "" + Low + High, TsStringUtils.MakeProps(null, EnWS));
			bldr.Replace(bldr.Length, bldr.Length, "word", TsStringUtils.MakeProps(null, EsWS));
			string xml = TsStringSerializer.SerializeTsStringToXml(bldr.GetString(), WritingSystemManager);
			ITsString reloaded = TsStringSerializer.DeserializeTsStringFromXml(xml, WritingSystemManager);
			Assert.That(reloaded.Text, Is.EqualTo("word" + Fffd + Fffd + "word"));
			Assert.That(reloaded.RunCount, Is.EqualTo(3));
			Assert.That(TsStringSerializer.SerializeTsStringToXml(reloaded, WritingSystemManager), Is.EqualTo(xml));
		}

		[Test]
		public void SerializeTsStringToXml_WhitespaceRunPreservedWhenControlCharStripped()
		{
			// " " + U+0002 is written as " ": xml:space must be decided on what is written, or the run is lost.
			var bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "word", TsStringUtils.MakeProps(null, EsWS));
			bldr.Replace(bldr.Length, bldr.Length, " \u0002", TsStringUtils.MakeProps(null, EnWS));
			bldr.Replace(bldr.Length, bldr.Length, "word", TsStringUtils.MakeProps(null, EsWS));
			string xml = TsStringSerializer.SerializeTsStringToXml(bldr.GetString(), WritingSystemManager);
			Assert.That(xml, Does.Contain("xml:space=\"preserve\""));
			Assert.That(TsStringSerializer.DeserializeTsStringFromXml(xml, WritingSystemManager).Text, Is.EqualTo("word word"));
		}

		[Test]
		public void SerializeTsStringToXml_LoneSurrogateInOneOfSeveralRuns()
		{
			var bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "one" + High, TsStringUtils.MakeProps(null, EnWS));
			bldr.Replace(bldr.Length, bldr.Length, "two", TsStringUtils.MakeProps(null, EsWS));
			string xml = TsStringSerializer.SerializeTsStringToXml(bldr.GetString(), WritingSystemManager);
			ITsString reloaded = TsStringSerializer.DeserializeTsStringFromXml(xml, WritingSystemManager);
			Assert.That(reloaded.Text, Is.EqualTo("one" + Fffd + "two"));
			Assert.That(reloaded.RunCount, Is.EqualTo(2));
		}

		[Test]
		public void SerializeTsStringToXml_ValidSurrogatePairNextToLoneSurrogate_KeepsPair()
		{
			ITsString tss = TsStringUtils.MakeString("\U0001F98A" + High + "\U0001F98A", EnWS);
			string xml = TsStringSerializer.SerializeTsStringToXml(tss, WritingSystemManager);
			Assert.That(TsStringSerializer.DeserializeTsStringFromXml(xml, WritingSystemManager).Text,
				Is.EqualTo("\U0001F98A" + Fffd + "\U0001F98A"));
		}

		#endregion

		#region Input sanitizing (the choke point for model setters)

		[TestCaseSource(nameof(LoneSurrogateCases))]
		public void NormalizeNfd_ReplacesLoneSurrogateWithFffd_KeepingLength(string text)
		{
			ITsString tss = TsStringUtils.MakeString(text, EnWS);
			ITsString result = TsStringUtils.NormalizeNfd(tss);
			Assert.That(CountLone(result.Text), Is.EqualTo(0), Escape(result.Text));
			Assert.That(result.Text, Is.EqualTo(Replaced(text).Normalize(NormalizationForm.FormD)));
		}

		[Test]
		public void NormalizeNfd_ReplacementKeepsRunProperties()
		{
			var bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "ab", TsStringUtils.MakeProps(null, EnWS));
			bldr.Replace(bldr.Length, bldr.Length, "c" + High + "d", TsStringUtils.MakeProps(null, EsWS));
			ITsString result = TsStringUtils.NormalizeNfd(bldr.GetString());
			Assert.That(result.Text, Is.EqualTo("abc" + Fffd + "d"));
			Assert.That(result.RunCount, Is.EqualTo(2));
			Assert.That(result.get_WritingSystemAt(3), Is.EqualTo(EsWS));
			Assert.That(result.get_WritingSystemAt(1), Is.EqualTo(EnWS));
		}

		[Test]
		public void NormalizeNfd_ValidPairAndNormalStringUnchanged()
		{
			ITsString pair = TsStringUtils.MakeString("a\U0001F98Ab", EnWS);
			Assert.That(TsStringUtils.NormalizeNfd(pair), Is.SameAs(pair));
			Assert.That(TsStringUtils.ReplaceLoneSurrogates(pair), Is.SameAs(pair));
			Assert.That(TsStringUtils.ReplaceLoneSurrogates(null), Is.Null);
		}

		#endregion

		#region Normalization passes lone surrogates through (as ICU does)

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
			ITsString tss = TsStringUtils.MakeString("e" + Acute + High + "ö", EnWS);
			Assert.That(tss.get_NormalizedForm(FwNormalizationMode.knmNFC).Text, Is.EqualTo("é" + High + "ö"));
			ITsString composed = TsStringUtils.MakeString("é" + Low + "ö", EnWS);
			Assert.That(composed.get_NormalizedForm(FwNormalizationMode.knmNFD).Text, Is.EqualTo("e" + Acute + Low + "ö"));
		}

		[Test]
		public void NormalizedForm_LoneSurrogate_AcrossRuns_NFD()
		{
			var bldr = TsStringUtils.MakeStrBldr();
			bldr.Replace(0, 0, "é" + High, TsStringUtils.MakeProps(null, EnWS));
			bldr.Replace(bldr.Length, bldr.Length, Acute + "x", TsStringUtils.MakeProps(null, EsWS));
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
				Assert.That(result.Text, Is.EqualTo("e" + Acute + High + "b"));
				Assert.That(Marshal.ReadInt32(offset), Is.EqualTo(3));
			}
			finally
			{
				Marshal.FreeHGlobal(offset);
				Marshal.FreeHGlobal(table);
			}
		}

		#endregion

		#region Character accessors

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
		public void IsCharWordForming_LoneLowSurrogateAtStart_DoesNotThrow_AndIsNotWordForming()
		{
			ITsString tss = TsStringUtils.MakeString("" + Low + "ab", EnWS);
			Assert.That(tss.IsCharWordForming(0, WritingSystemManager), Is.False);
			Assert.That(tss.IsCharWordForming(1, WritingSystemManager), Is.True);
		}

		[Test]
		public void IsCharWordForming_LoneLowSurrogateAfterLetter_IsNotWordForming()
		{
			// It must not be classified by the letter before it.
			ITsString tss = TsStringUtils.MakeString("b" + Low + "c", EnWS);
			Assert.That(tss.IsCharWordForming(0, WritingSystemManager), Is.True);
			Assert.That(tss.IsCharWordForming(1, WritingSystemManager), Is.False);
			Assert.That(tss.IsCharWordForming(2, WritingSystemManager), Is.True);
		}

		[Test]
		public void IsCharWordForming_LoneHighSurrogate_IsNotWordForming()
		{
			ITsString tss = TsStringUtils.MakeString("b" + High + "c" + High, EnWS);
			Assert.That(tss.IsCharWordForming(1, WritingSystemManager), Is.False);
			Assert.That(tss.IsCharWordForming(3, WritingSystemManager), Is.False);
		}

		[Test]
		public void IsCharWordForming_ValidPair_StillClassifiedByTheFullCharacter()
		{
			// U+10400 DESERET CAPITAL LETTER LONG I is a letter; both halves of the pair report that.
			ITsString tss = TsStringUtils.MakeString("a\U00010400b", EnWS);
			Assert.That(tss.IsCharWordForming(1, WritingSystemManager), Is.True);
			Assert.That(tss.IsCharWordForming(2, WritingSystemManager), Is.True);
		}

		[Test]
		public void CharacterAccessors_Fuzz_NeverThrow()
		{
			// Lone highs and lows at the start, middle and end of random text.
			var rng = new Random(414);
			string[] alphabet = { "a", "b", " ", ".", "é", Acute, "\U0001F98A", "" + High, "" + Low, "\uDBFF", "\uDFFF" };
			for (int n = 0; n < 2000; n++)
			{
				var sb = new StringBuilder();
				int len = rng.Next(1, 9);
				for (int i = 0; i < len; i++)
					sb.Append(alphabet[rng.Next(alphabet.Length)]);
				string text = sb.ToString();
				ITsString tss = TsStringUtils.MakeString(text, EnWS);
				string failure = Escape(text);
				foreach (FwNormalizationMode mode in new[] { FwNormalizationMode.knmNFC, FwNormalizationMode.knmNFD, FwNormalizationMode.knmNFSC })
					Assert.DoesNotThrow(() => tss.get_NormalizedForm(mode), failure);
				Assert.DoesNotThrow(() => TsStringSerializer.DeserializeTsStringFromXml(
					TsStringSerializer.SerializeTsStringToXml(tss, WritingSystemManager), WritingSystemManager), failure);
				ITsString clean = TsStringUtils.NormalizeNfd(tss);
				Assert.That(clean.Text ?? string.Empty, Is.EqualTo(Replaced(text).Normalize(NormalizationForm.FormD)), failure);
				for (int i = 0; i < tss.Length; i++)
				{
					int ich = i;
					Assert.DoesNotThrow(() => tss.CharAt(ich), failure);
					Assert.DoesNotThrow(() => tss.IsCharWordForming(ich, WritingSystemManager), failure);
					Assert.DoesNotThrow(() => tss.NextCharIndex(ich), failure);
					Assert.DoesNotThrow(() => tss.PrevCharIndex(ich), failure);
				}
				// A re-save of what we wrote is identical.
				string xml1 = TsStringSerializer.SerializeTsStringToXml(tss, WritingSystemManager);
				string xml2 = TsStringSerializer.SerializeTsStringToXml(
					TsStringSerializer.DeserializeTsStringFromXml(xml1, WritingSystemManager), WritingSystemManager);
				Assert.That(xml2, Is.EqualTo(xml1), failure);
			}
		}

		[Test]
		public void Surrogates_CodePointAt()
		{
			Assert.That(Surrogates.CodePointAt("a" + High + Low, 1), Is.EqualTo(0x10000));
			Assert.That(Surrogates.CodePointAt("a" + High + Low, 2), Is.EqualTo((int)Low)); // second half of a pair, read from its middle
			Assert.That(Surrogates.CodePointAt("a" + High, 1), Is.EqualTo((int)High));
		}

		[Test]
		public void Surrogates_HasLoneSurrogate_And_ReplaceLoneSurrogates()
		{
			Assert.That(Surrogates.HasLoneSurrogate(null), Is.False);
			Assert.That(Surrogates.HasLoneSurrogate("a\U0001F98Ab"), Is.False);
			Assert.That(Surrogates.HasLoneSurrogate("a" + High), Is.True);
			Assert.That(Surrogates.HasLoneSurrogate("" + Low + High), Is.True);
			Assert.That(Surrogates.ReplaceLoneSurrogates("a" + High + "b" + Low + "\U0001F98A"), Is.EqualTo("a" + Fffd + "b" + Fffd + "\U0001F98A"));
			Assert.That(Surrogates.ReplaceLoneSurrogates(null), Is.Null);
		}

		#endregion
	}
}
