// Copyright (c) 2009-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Text;
using Icu;
using NUnit.Framework;

namespace SIL.LCModel.Core.Text
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Tests ICU wrapper
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[TestFixture]
	public class CustomIcuTests
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		///
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[OneTimeSetUp]
		public void TestFixtureSetup()
		{
			CustomIcu.InitIcuDataDir();
		}

		/// <summary>
		/// Can't easily check the correctness, but make sure we can at least get this.
		/// </summary>
		[Test]
		public void CanGetUnicodeVersion()
		{
			var result = Wrapper.UnicodeVersion;
			Assert.That(result.Length >= 3);
			Assert.That(result.IndexOf(".", StringComparison.InvariantCulture), Is.GreaterThan(0));
			int major;
			Assert.True(int.TryParse(result.Substring(0, result.IndexOf(".")), out major));
			Assert.That(major >= 6);
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the Normalize method: input is NFC, normalize to NFC
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void Normalize_NFC2NFC()
		{
			var normalizedString = Normalizer.Normalize("t\u00E9st", Normalizer.UNormalizationMode.UNORM_NFC);
			Assert.AreEqual("t\u00E9st", normalizedString);
			Assert.IsTrue(normalizedString.IsNormalized(NormalizationForm.FormC));
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the Normalize method: input is NFC, normalize to NFD
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void Normalize_NFC2NFD()
		{
			var normalizedString = Normalizer.Normalize("t\u00E9st", Normalizer.UNormalizationMode.UNORM_NFD);
			var i=0;
			foreach (var c in normalizedString.ToCharArray())
				Console.WriteLine("pos {0}: {1} ({1:x})", i++, c);
			Assert.AreEqual(0x0301, normalizedString[2]);
			Assert.AreEqual("te\u0301st", normalizedString);
			Assert.IsTrue(normalizedString.IsNormalized(NormalizationForm.FormD));
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the Normalize method: input is NFD, normalize to NFC
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void Normalize_NFD2NFC()
		{
			var normalizedString = Normalizer.Normalize("te\u0301st", Normalizer.UNormalizationMode.UNORM_NFC);
			Assert.AreEqual("t\u00E9st", normalizedString);
			Assert.IsTrue(normalizedString.IsNormalized(NormalizationForm.FormC));
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the Normalize method: input is NFD, normalize to NFD
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void Normalize_NFD2NFD()
		{
			var normalizedString = Normalizer.Normalize("te\u0301st", Normalizer.UNormalizationMode.UNORM_NFD);
			Assert.AreEqual("te\u0301st", normalizedString);
			Assert.IsTrue(normalizedString.IsNormalized(NormalizationForm.FormD));
		}

		/// <summary>
		/// Tests the Split method.
		/// </summary>
		[TestCase(BreakIterator.UBreakIteratorType.WORD, "en", "word",
			ExpectedResult = new[] {"word"})]
		[TestCase(BreakIterator.UBreakIteratorType.WORD, "en", "This is some text, and some more text.",
			ExpectedResult = new[] {"This", " ", "is", " ", "some", " ", "text", ",", " ", "and", " ", "some", " ", "more", " ", "text", "."})]
		[TestCase(BreakIterator.UBreakIteratorType.SENTENCE, "en", "Sentence one. Sentence two.",
			ExpectedResult = new[] {"Sentence one. ", "Sentence two."})]
		[TestCase(BreakIterator.UBreakIteratorType.CHARACTER, "en", "word",
			ExpectedResult = new[] {"w", "o", "r", "d"})]
		[TestCase(BreakIterator.UBreakIteratorType.LINE, "en", "This is some hyphenated-text.",
			ExpectedResult = new[] {"This ", "is ", "some ", "hyphenated-", "text."})]
		public IEnumerable<string> Split(BreakIterator.UBreakIteratorType type, string locale,
			string text)
		{
			using (var breakIterator = new RuleBasedBreakIterator(type, locale))
			{
				breakIterator.SetText(text);
				return breakIterator;
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests GetExemplarCharacters for en.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetExemplarCharacters_English()
		{
			Assert.That(CustomIcu.GetExemplarCharacters("en"), Is.EqualTo("[a b c d e f g h i j k l m n o p q r s t u v w x y z]"));
		}

		/// <summary>
		/// Make sure our initialization of the character property engine works.
		/// (This test is important...it's the only one that verifies that our ICU overrides are
		/// working when the ICU directory is initialized from C#.)
		/// </summary>
		[Test]
		public void CharacterPropertyOverrides()
		{
			CustomIcu.InitIcuDataDir();
			var result = Character.GetCharType('\xF171');
			Assert.That(result, Is.EqualTo(Character.UCharCategory.NON_SPACING_MARK));
		}

	}
}
