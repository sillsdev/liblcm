// Copyright (c) 2003-2022 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using NUnit.Framework;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.Utils;
// ReSharper disable StringLiteralTypo - spell check is case-hypersensitive

namespace SIL.LCModel.Core.Text
{
	/// <summary>
	/// Test the CaseFunctions class.
	/// </summary>
	[TestFixture]
	public class CaseFunctionsTest
	{
		/// <summary/>
		[TestCase("en", null, ExpectedResult = "en", TestName = "Normal")]
		[TestCase("qaa", null, ExpectedResult = "root", TestName = "Private Use")]
		[TestCase("tkr", "az", ExpectedResult = "az", TestName = "Case Alias")]
		public string WritingSystemCtor(string locale, string caseAlias)
		{
			return new CaseFunctions(new CoreWritingSystemDefinition(locale) { CaseAlias = caseAlias }).IcuLocale;
		}

		/// <summary/>
		[TestCase("en", "Igloo", ExpectedResult = "igloo")]
		[TestCase("tur", "I'm NOT dotted", ExpectedResult = "\u0131'm not dotted")]
		public string ToLower_UsesIcuLocale(string locale, string input)
		{
			return new CaseFunctions(locale).ToLower(input);
		}

		/// <summary/>
		[TestCase("en", "intp", ExpectedResult = "INTP")]
		[TestCase("tur", "Dotted i", ExpectedResult = "DOTTED \u0130")]
		public string ToUpper_UsesIcuLocale(string locale, string input)
		{
			return new CaseFunctions(locale).ToUpper(input);
		}

		/// <summary/>
		[TestCase("en", "intrepID", ExpectedResult = "Intrepid")]
		[TestCase("tur", "inDIA", ExpectedResult = "\u0130nd\u0131a")]
		public string ToTitle_UsesIcuLocale(string locale, string input)
		{
			return new CaseFunctions(locale).ToTitle(input);
		}

		/// <summary/>
		[Test]
		public void TestToLower()
		{
			CaseFunctions cf = new CaseFunctions("en");
			Assert.AreEqual("abc", cf.ToLower("ABC"));
		}

		/// <summary/>
		[Test]
		public void TestStringCase()
		{
			CaseFunctions cf = new CaseFunctions("en");
			Assert.AreEqual(StringCaseStatus.allLower, cf.StringCase("abc"));
			Assert.AreEqual(StringCaseStatus.allLower, cf.StringCase(""));
			Assert.AreEqual(StringCaseStatus.allLower, cf.StringCase(null));
			Assert.AreEqual(StringCaseStatus.title, cf.StringCase("Abc"));
			Assert.AreEqual(StringCaseStatus.title, cf.StringCase("A"));
			Assert.AreEqual(StringCaseStatus.mixed, cf.StringCase("AbC"));
			Assert.AreEqual(StringCaseStatus.mixed, cf.StringCase("ABC"));
			Assert.AreEqual(StringCaseStatus.mixed, cf.StringCase("aBC"));
			int surrogateUc = 0x10400; // DESERET CAPITAL LETTER LONG I
			int surrogateLc = 0x10428; // DESERET SMALL LETTER LONG I
			string strUcSurrogate = Surrogates.StringFromCodePoint(surrogateUc);
			string strLcSurrogate = Surrogates.StringFromCodePoint(surrogateLc);
			// A single upper case surrogate is treated as title.
			Assert.AreEqual(StringCaseStatus.title, cf.StringCase(strUcSurrogate));
			Assert.AreEqual(StringCaseStatus.title, cf.StringCase(strUcSurrogate + "bc"));
			Assert.AreEqual(StringCaseStatus.mixed, cf.StringCase(strUcSurrogate + "bC"));
			Assert.AreEqual(StringCaseStatus.allLower, cf.StringCase(strLcSurrogate + "bc"));
		}
	}
}
