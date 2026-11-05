// Copyright (c) 2026 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using NUnit.Framework;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;

namespace SIL.LCModel.DomainServices
{
	/// <summary>
	/// WordMaker must not throw on text containing an unpaired surrogate (built in code, since NUnit
	/// attributes would turn it into U+FFFD).
	/// </summary>
	[TestFixture]
	public class WordMakerLoneSurrogateTests
	{
		[Test]
		public void IsWhite_LoneSurrogate_DoesNotThrow_AndIsNotWhite()
		{
			var wsManager = new WritingSystemManager();
			CoreWritingSystemDefinition enWs;
			wsManager.GetOrSet("en", out enWs);
			var tss = TsStringUtils.MakeString("a\uD800 \uDC00", enWs.Handle);
			var maker = new WordMaker(tss, wsManager);
			Assert.That(maker.IsWhite(1), Is.False); // lone high surrogate
			Assert.That(maker.IsWhite(2), Is.True);  // the space
			Assert.That(maker.IsWhite(3), Is.False); // lone low surrogate at end
		}
	}
}
