// Copyright (c) 2026 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using NUnit.Framework;
using SIL.LCModel.Core.Text;

namespace SIL.LCModel.DomainImpl
{
	/// <summary>
	/// Plain-string (not ITsString) model paths must replace a lone surrogate with U+FFFD, not throw.
	/// NOTE: strings are built with \u escapes; NUnit attributes would turn lone surrogates into U+FFFD.
	/// </summary>
	[TestFixture]
	public class LoneSurrogatePlainStringTests : MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{
		/// <summary>Plain Unicode property setter (the generated accessor used to call string.Normalize).</summary>
		[Test]
		public void PlainUnicodeProperty_LoneSurrogate_DoesNotThrow()
		{
			var entry = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
			Assert.That(() => entry.LiftResidue = "a\uD800b", Throws.Nothing);
			Assert.That(entry.LiftResidue, Is.EqualTo("a�b"));
			entry.LiftResidue = "\uDC00x\uD800";
			Assert.That(entry.LiftResidue, Is.EqualTo("�x�"));
		}

		/// <summary>WfiWordformRepository.TryGetObject used to normalize the target text with string.Normalize.</summary>
		[Test]
		public void WordformTryGetObject_LoneSurrogateTarget_DoesNotThrow()
		{
			var wf = Cache.ServiceLocator.GetInstance<IWfiWordformFactory>().Create(
				TsStringUtils.MakeString("abc", Cache.DefaultVernWs));
			var repo = Cache.ServiceLocator.GetInstance<IWfiWordformRepository>();
			IWfiWordform found;
			Assert.That(() => repo.TryGetObject(TsStringUtils.MakeString("b\uDC00c", Cache.DefaultVernWs), out found),
				Throws.Nothing);
			Assert.That(repo.TryGetObject(TsStringUtils.MakeString("abc", Cache.DefaultVernWs), out found), Is.True);
			Assert.That(found, Is.SameAs(wf));
		}
	}
}
