// Copyright (c) 2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using NUnit.Framework;

namespace SIL.LCModel.Core.WritingSystems
{
	[TestFixture]
	class CoreWritingSystemDefinitionTests
	{
		[TestCase("en", "English")]
		[TestCase("en-Arab", "English \\(Arabic\\)")]
		[TestCase("en-Latn", "English \\(Latin\\)")]
		[TestCase("en-Latn-US", "English \\(Latin, United States\\)")]
		[TestCase("en-Latn-US-x-special", "English \\(Latin, United States, special\\)")]
		[TestCase("en-fonipa", "English \\(International Phonetic Alphabet\\)")]
		[TestCase("en-Latn-fonipa", "English \\(Latin, International Phonetic Alphabet\\)")]
		[TestCase("en-Zxxx-x-audio", "English \\(Audio\\)")]
		public void CoreWritingDefinition_DisplayLabel(string langTag, string expectedLabel)
		{
			var ws = new CoreWritingSystemDefinition(langTag);
			Assert.That(ws.DisplayLabel, Is.StringMatching(expectedLabel));
		}
	}
}
