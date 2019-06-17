// Copyright (c) 2015-2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SIL.LCModel.Utils;
using SIL.WritingSystems;

namespace SIL.LCModel.Core.WritingSystems
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Tests for the ValidCharaters class
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[TestFixture]
	public class ValidCharactersTests
	{
		private WritingSystemManager m_wsManager;

		/// <summary/>
		[TestFixtureSetUp]
		public void FixtureSetup()
		{
			m_wsManager = new WritingSystemManager();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Class to facilitate getting at private members of the ValidCharacters class using
		/// Reflection
		/// REVIEW (Hasso) 2019.06: all of the reflectively retrieved members are accessible by public properties on <see cref="ValidCharacters"/>
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private class ValidCharsWrapper
		{
			readonly ValidCharacters m_validChars;

			/// --------------------------------------------------------------------------------
			/// <summary>
			/// Initializes a new instance of the <see cref="ValidCharsWrapper"/> class.
			/// </summary>
			/// <param name="validCharacters">An instance of the valid characters class.</param>
			/// --------------------------------------------------------------------------------
			public ValidCharsWrapper(ValidCharacters validCharacters)
			{
				m_validChars = validCharacters;
			}

			/// --------------------------------------------------------------------------------
			/// <summary>
			/// Gets the word forming characters list.
			/// </summary>
			/// --------------------------------------------------------------------------------
			public List<string> WordFormingCharacters
			{
				get
				{
					return (List<string>)m_validChars.WordFormingCharacters;
				}
			}

			/// --------------------------------------------------------------------------------
			/// <summary>
			/// Gets the punctuation/symbols/etc. characters list.
			/// </summary>
			/// --------------------------------------------------------------------------------
			public List<string> OtherCharacters
			{
				get
				{
					return (List<string>)m_validChars.OtherCharacters;
				}
			}
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests initialization of valid characters.
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void Load_Nonempty()
		{
			CoreWritingSystemDefinition ws1 = m_wsManager.Create("en");
			ws1.CharacterSets.Add(new CharacterSetDefinition("main") {Characters = {"e", "f", "g", "h"}});
			ws1.CharacterSets.Add(new CharacterSetDefinition("punctuation") {Characters = {",", "!", "*"}});
			ValidCharacters validChars = ValidCharacters.Load(ws1);
			var validCharsW = new ValidCharsWrapper(validChars);
			Assert.AreEqual(4, validCharsW.WordFormingCharacters.Count);
			Assert.IsTrue(validCharsW.WordFormingCharacters.Contains("e"));
			Assert.IsTrue(validCharsW.WordFormingCharacters.Contains("f"));
			Assert.IsTrue(validCharsW.WordFormingCharacters.Contains("g"));
			Assert.IsTrue(validCharsW.WordFormingCharacters.Contains("h"));
			Assert.AreEqual(3, validCharsW.OtherCharacters.Count);
			Assert.IsTrue(validCharsW.OtherCharacters.Contains(","));
			Assert.IsTrue(validCharsW.OtherCharacters.Contains("!"));
			Assert.IsTrue(validCharsW.OtherCharacters.Contains("*"));
			CoreWritingSystemDefinition ws2 = m_wsManager.Create("en");
			validChars.SaveTo(ws2);
			Assert.That(ws1.ValueEquals(ws2), Is.True);
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests initialization which defines no valid characters.
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void Load_Empty()
		{
			CoreWritingSystemDefinition ws1 = m_wsManager.Create("en");
			ValidCharacters validChars = ValidCharacters.Load(ws1);
			Assert.That(validChars.WordFormingCharacters, Is.Empty);
			Assert.That(validChars.OtherCharacters, Is.Empty);
			CoreWritingSystemDefinition ws2 = m_wsManager.Create("en");
			validChars.SaveTo(ws2);
			Assert.That(ws1.ValueEquals(ws2), Is.True);
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests initialization from valid characters containing U+2028 (Line Separator/ Hard
		/// Line Break) in the "Other" list. LT-9985
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void Load_AllowHardLineBreakCharacter()
		{
			CoreWritingSystemDefinition ws1 = m_wsManager.Create("en");
			ws1.CharacterSets.Add(new CharacterSetDefinition("punctuation") {Characters = {"\u2028"}});
			ValidCharacters validChars = ValidCharacters.Load(ws1);
			var validCharsW = new ValidCharsWrapper(validChars);
			Assert.AreEqual(1, validCharsW.OtherCharacters.Count);
			Assert.IsTrue(validCharsW.OtherCharacters.Contains("\u2028"));
		}

		[Test]
		public void Load_AllowMultigraphs()
		{
			CoreWritingSystemDefinition ws1 = m_wsManager.Create("en");
			ws1.CharacterSets.Add(new CharacterSetDefinition("punctuation") {Characters = {"Ll", "Sch", "Th"}});
			var validChars = ValidCharacters.Load(ws1).OtherCharacters.ToList();
			Assert.AreEqual(3, validChars.Count);
			CollectionAssert.Contains(validChars, "Ll");
			CollectionAssert.Contains(validChars, "Sch");
			CollectionAssert.Contains(validChars, "Th");
		}

		/// <summary/>
		[Test]
		public void Load_AllowLoneCombiningDiacritics()
		{
			CoreWritingSystemDefinition ws = m_wsManager.Create("en-US");
			ws.CharacterSets.Add(new CharacterSetDefinition("main") { Characters = { "a", "\u0301" } }); // combining acute accent
			var validChars = ValidCharacters.Load(ws).WordFormingCharacters.ToList();
			Assert.AreEqual(2, validChars.Count);
			CollectionAssert.Contains(validChars, "a");
			CollectionAssert.Contains(validChars, "\u0301");
		}

		/// <summary/>
		[Test]
		public void Load_AllowGroupedCombiningDiacritics()
		{
			CoreWritingSystemDefinition ws = m_wsManager.Create("en-US");
			ws.CharacterSets.Add(new CharacterSetDefinition("main") { Characters = { "a\u0301" } }); // combining acute accent
			var validChars = ValidCharacters.Load(ws).WordFormingCharacters.ToList();
			Assert.AreEqual(1, validChars.Count);
			CollectionAssert.Contains(validChars, "a\u0301");
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests initialization where the same character occurs in both the word-forming and
		/// punctuation lists.
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void Load_SameCharacterInWordFormingAndPunctuationLists()
		{
			CoreWritingSystemDefinition ws = m_wsManager.Create("en-US");
			ws.CharacterSets.Add(new CharacterSetDefinition("main") {Characters = {"'"}});
			ws.CharacterSets.Add(new CharacterSetDefinition("punctuation") {Characters = {"'"}});
			ValidCharacters validChars = ValidCharacters.Load(ws);
			var validCharsW = new ValidCharsWrapper(validChars);
			Assert.AreEqual(1, validCharsW.WordFormingCharacters.Count);
			Assert.IsTrue(validCharsW.WordFormingCharacters.Contains("'"));
			Assert.AreEqual(0, validCharsW.OtherCharacters.Count);
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests initialization where the same character occurs more than once in the same list.
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void Load_DuplicateCharacters()
		{
			CoreWritingSystemDefinition ws = m_wsManager.Create("en-US");
			ws.CharacterSets.Add(new CharacterSetDefinition("main") {Characters = {"a", "a"}});
			ws.CharacterSets.Add(new CharacterSetDefinition("punctuation") {Characters = {"'", "'"}});
			ValidCharacters validChars = ValidCharacters.Load(ws);
			var validCharsW = new ValidCharsWrapper(validChars);
			Assert.AreEqual(1, validCharsW.WordFormingCharacters.Count);
			Assert.IsTrue(validCharsW.WordFormingCharacters.Contains("a"));
			Assert.AreEqual(1, validCharsW.OtherCharacters.Count);
			Assert.IsTrue(validCharsW.OtherCharacters.Contains("'"));
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the AddCharacter method when attempting to add a duplicate character.
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void AddCharacter_Duplicate()
		{
			CoreWritingSystemDefinition ws1 = m_wsManager.Create("en-US");
			ValidCharacters validChars = ValidCharacters.Load(ws1);
			var validCharsW = new ValidCharsWrapper(validChars);
			validChars.AddCharacter("a");
			validChars.AddCharacter("a");
			Assert.AreEqual(1, validCharsW.WordFormingCharacters.Count);
			Assert.IsTrue(validCharsW.WordFormingCharacters.Contains("a"));
			Assert.AreEqual(0, validCharsW.OtherCharacters.Count);
			validChars.SaveTo(ws1);
			CoreWritingSystemDefinition ws2 = m_wsManager.Create("en-US");
			ws2.CharacterSets.Add(new CharacterSetDefinition("main") {Characters = {"a"}});
			Assert.That(ws1.ValueEquals(ws2), Is.True);
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the AddCharacter method when attempting to add a punctuation character which
		/// is already in the list of word-forming characters (as an override).
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void AddCharacter_DuplicateOfOverriddenWordFormingChar()
		{
			CoreWritingSystemDefinition ws = m_wsManager.Create("en-US");
			ws.CharacterSets.Add(new CharacterSetDefinition("main") {Characters = {"a", "-"}});
			ws.CharacterSets.Add(new CharacterSetDefinition("punctuation") {Characters = {"{"}});
			ValidCharacters validChars = ValidCharacters.Load(ws);
			var validCharsW = new ValidCharsWrapper(validChars);
			Assert.AreEqual(2, validCharsW.WordFormingCharacters.Count);
			Assert.AreEqual(1, validCharsW.OtherCharacters.Count);
			Assert.IsTrue(validChars.IsWordForming("-"));
			Assert.IsFalse(validChars.IsWordForming("{"));
			validChars.AddCharacter("-");
			Assert.AreEqual(2, validCharsW.WordFormingCharacters.Count);
			Assert.IsTrue(validCharsW.WordFormingCharacters.Contains("a"));
			Assert.IsTrue(validCharsW.WordFormingCharacters.Contains("-"));
			Assert.IsTrue(validChars.IsWordForming("-"));
			Assert.AreEqual(1, validCharsW.OtherCharacters.Count);
			Assert.IsTrue(validCharsW.OtherCharacters.Contains("{"));
			Assert.IsFalse(validChars.IsWordForming("{"));
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the AddCharacter method when adding a superscripted numeric character (i.e., a
		/// word-forming tone mark that ICU doesn't normally consider to be a letter). TE-8384
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void AddCharacter_SuperscriptedToneNumber()
		{
			CoreWritingSystemDefinition ws = m_wsManager.Create("en-US");
			ValidCharacters validChars = ValidCharacters.Load(ws);
			var validCharsW = new ValidCharsWrapper(validChars);
			validChars.AddCharacter("\u00b9");
			validChars.AddCharacter("\u2079");
			Assert.AreEqual(2, validCharsW.WordFormingCharacters.Count);
			Assert.IsTrue(validCharsW.WordFormingCharacters.Contains("\u00b9"));
			Assert.IsTrue(validCharsW.WordFormingCharacters.Contains("\u2079"));
			Assert.AreEqual(0, validCharsW.OtherCharacters.Count);
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the GetNaturalCharType method.
		/// </summary>
		///--------------------------------------------------------------------------------------
		[TestCase('a', ValidCharacterType.WordForming)]
		[TestCase('-', ValidCharacterType.WordForming)]
		[TestCase('\'', ValidCharacterType.WordForming)]
		[TestCase(0xA78B, ValidCharacterType.WordForming)] // Capital Saltillo
		[TestCase(0x00B2, ValidCharacterType.WordForming)] // Superscript 2
		[TestCase(0x2079, ValidCharacterType.WordForming)] // Superscript 9
		[TestCase('1', ValidCharacterType.Other)]
		[TestCase(' ', ValidCharacterType.Other)]
		[TestCase(',', ValidCharacterType.Other)]
		public void GetNaturalCharType(int codepoint, ValidCharacterType type)
		{
			CoreWritingSystemDefinition ws = m_wsManager.Create("en-US");
			ValidCharacters validChars = ValidCharacters.Load(ws);
			Assert.AreEqual(type, ReflectionHelper.GetResult(validChars, "GetNaturalCharType", codepoint));
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the IsWordForming method when using a symbol not defined as word forming in ICU
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void IsWordFormingChar()
		{
			CoreWritingSystemDefinition ws = m_wsManager.Create("en-US");
			ws.CharacterSets.Add(new CharacterSetDefinition("main") {Characters = {"a", "b", "c", "d", "e", "#"}});
			ValidCharacters validChars = ValidCharacters.Load(ws);
			Assert.IsTrue(validChars.IsWordForming('#'));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Verifies that lists are sorted after adding characters one-at-a-time.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void SortAfterAddSingles()
		{
			CoreWritingSystemDefinition ws = m_wsManager.Create("en");
			ValidCharacters validChars = ValidCharacters.Load(ws);
			validChars.AddCharacter("z");
			validChars.AddCharacter("c");
			validChars.AddCharacter("t");
			validChars.AddCharacter("b");
			validChars.AddCharacter("8");
			validChars.AddCharacter("7");
			validChars.AddCharacter("6");
			validChars.AddCharacter("5");
			VerifySortOrder(validChars);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Verifies that lists are sorted after adding a range of characters.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void SortAfterAddRange()
		{
			CoreWritingSystemDefinition ws = m_wsManager.Create("en");
			ValidCharacters validChars = ValidCharacters.Load(ws);
			validChars.AddCharacters(new[] { "z", "c", "t", "b", "8", "7", "6", "5" });
			VerifySortOrder(validChars);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Verifies the sort order of characters added to the specified valid characters
		/// object.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void VerifySortOrder(ValidCharacters validChars)
		{
			var validCharsW = new ValidCharsWrapper(validChars);
			Assert.AreEqual("b", validCharsW.WordFormingCharacters[0]);
			Assert.AreEqual("c", validCharsW.WordFormingCharacters[1]);
			Assert.AreEqual("t", validCharsW.WordFormingCharacters[2]);
			Assert.AreEqual("z", validCharsW.WordFormingCharacters[3]);

			validChars.AddCharacter("8");
			validChars.AddCharacter("7");
			validChars.AddCharacter("6");
			validChars.AddCharacter("5");

			Assert.AreEqual("5", validCharsW.OtherCharacters[0]);
			Assert.AreEqual("6", validCharsW.OtherCharacters[1]);
			Assert.AreEqual("7", validCharsW.OtherCharacters[2]);
			Assert.AreEqual("8", validCharsW.OtherCharacters[3]);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Verifies the default word forming characters.
		/// </summary>
		/// <param name="validChars">The valid chars.</param>
		/// ------------------------------------------------------------------------------------
		private static void VerifyDefaultWordFormingCharacters(ValidCharacters validChars)
		{
			var expectedWordFormingChars = (string[]) ReflectionHelper.GetField(
				typeof(ValidCharacters), "DefaultWordformingChars");
			Assert.AreEqual(expectedWordFormingChars, validChars.WordFormingCharacters.ToArray(),
				"We expect the load method to have a fallback to the default word-forming characters");
			Assert.That(validChars.OtherCharacters, Is.Empty);
		}
	}
}
