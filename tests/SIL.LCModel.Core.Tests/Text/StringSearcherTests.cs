// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Linq;
using NUnit.Framework;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.WritingSystems;

namespace SIL.LCModel.Core.Text
{
	/// <summary>
	/// StringSearcher tests
	/// </summary>
	[TestFixture]
	public class StringSearcherTests
	{
		private WritingSystemManager m_wsManager;
		private int m_enWs;
		private int m_frWs;

		/// <summary>
		/// Setup the test fixture.
		/// </summary>
		[OneTimeSetUp]
		public void FixtureSetup()
		{
			m_wsManager = new WritingSystemManager();
			CoreWritingSystemDefinition enWs;
			m_wsManager.GetOrSet("en", out enWs);
			m_enWs = enWs.Handle;
			CoreWritingSystemDefinition frWs;
			m_wsManager.GetOrSet("fr", out frWs);
			m_frWs = frWs.Handle;
		}

		private static void CheckSearch(StringSearcher<int> searcher, ITsString tss, int[] expectedResults)
		{
			Assert.AreEqual(expectedResults.Length, searcher.Search(0, tss).Intersect(expectedResults).Count());
		}

		private static void CheckNoResultsSearch(StringSearcher<int> searcher, ITsString tss)
		{
			Assert.AreEqual(0, searcher.Search(0, tss).Count());
		}

		/// <summary>
		/// Tests exact matching.
		/// </summary>
		[Test]
		public void ExactSearchTest()
		{
			var searcher = new StringSearcher<int>(SearchType.Exact, m_wsManager);
			searcher.Add(0, 0, TsStringUtils.MakeString("test", m_enWs));
			searcher.Add(1, 0, TsStringUtils.MakeString("Hello", m_enWs));
			searcher.Add(2, 0, TsStringUtils.MakeString("c'est une phrase", m_frWs));
			searcher.Add(3, 0, TsStringUtils.MakeString("hello", m_enWs));
			searcher.Add(4, 0, TsStringUtils.MakeString("zebra", m_enWs));

			CheckSearch(searcher, TsStringUtils.MakeString("test", m_enWs), new[] {0});
			CheckSearch(searcher, TsStringUtils.MakeString("hello", m_enWs), new[] {1, 3});
			CheckSearch(searcher, TsStringUtils.MakeString("zebra", m_enWs), new[] {4});
			CheckNoResultsSearch(searcher, TsStringUtils.MakeString("c'est", m_frWs));
			CheckNoResultsSearch(searcher, TsStringUtils.MakeString("zebras", m_enWs));
		}

		/// <summary>
		/// Tests prefix matching.
		/// </summary>
		[Test]
		public void PrefixSearchTest()
		{
			var searcher = new StringSearcher<int>(SearchType.Prefix, m_wsManager);
			searcher.Add(0, 0, TsStringUtils.MakeString("test", m_enWs));
			searcher.Add(1, 0, TsStringUtils.MakeString("Hello",  m_enWs));
			searcher.Add(2, 0, TsStringUtils.MakeString("c'est une phrase", m_frWs));
			searcher.Add(3, 0, TsStringUtils.MakeString("hello", m_enWs));
			searcher.Add(4, 0, TsStringUtils.MakeString("zebra", m_enWs));

			CheckSearch(searcher, TsStringUtils.MakeString("test", m_enWs), new[] {0});
			CheckSearch(searcher, TsStringUtils.MakeString("hel", m_enWs), new[] {1, 3});
			CheckSearch(searcher, TsStringUtils.MakeString("zebra", m_enWs), new[] { 4 });
			CheckSearch(searcher, TsStringUtils.MakeString("c'est", m_frWs), new[] {2});
			CheckNoResultsSearch(searcher, TsStringUtils.MakeString("zebras", m_enWs));
		}

		/// <summary>
		/// Builds the shared multi-writing-system corpus used by both <see cref="FullTextSearchTest"/>
		/// and <see cref="SubstringResultsIncludeAllFullTextResults"/>. Item 2 deliberately mixes a
		/// French run and an English run.
		/// </summary>
		private StringSearcher<int> BuildMultiRunCorpus(SearchType type)
		{
			var searcher = new StringSearcher<int>(type, m_wsManager);
			searcher.Add(0, 0, TsStringUtils.MakeString("test", m_enWs));
			searcher.Add(1, 0, TsStringUtils.MakeString("c'est une phrase", m_frWs));
			ITsIncStrBldr tisb = TsStringUtils.MakeIncStrBldr();
			tisb.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, m_frWs);
			tisb.Append("C'est une sentence. ");
			tisb.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, m_enWs);
			tisb.Append("We use it for testing purposes.");
			searcher.Add(2, 0, tisb.GetString());
			searcher.Add(3, 0, TsStringUtils.MakeString("Hello, how are you doing? I am doing fine. That is good to know.", m_enWs));
			return searcher;
		}

		/// <summary>
		/// The queries exercised by <see cref="FullTextSearchTest"/>, so the substring-superset test
		/// covers exactly the same scenarios. These are all single tokens or contiguous, in-order
		/// phrases, and that is deliberate: substring is a superset of full-text ONLY for those shapes
		/// (full-text ANDs word tokens regardless of order, while substring needs the whole query to
		/// appear contiguously). Adding an out-of-order multi-word query here would make
		/// <see cref="SubstringResultsIncludeAllFullTextResults"/> fail; that boundary is demonstrated
		/// by <see cref="Substring_isNotASupersetForOutOfOrderMultiWordQueries"/>.
		/// </summary>
		private ITsString[] FullTextQueries()
		{
			return new[]
			{
				TsStringUtils.MakeString("test", m_enWs),
				TsStringUtils.MakeString("c'est une", m_frWs),
				TsStringUtils.MakeString("t", m_enWs),
				TsStringUtils.MakeString("testing purpose", m_enWs)
			};
		}

		/// <summary>
		/// Tests full-text (word/prefix) matching.
		/// </summary>
		[Test]
		public void FullTextSearchTest()
		{
			var searcher = BuildMultiRunCorpus(SearchType.FullText);

			CheckSearch(searcher, TsStringUtils.MakeString("test", m_enWs), new[] {0, 2});
			CheckSearch(searcher, TsStringUtils.MakeString("c'est une", m_frWs), new[] {1, 2});
			CheckSearch(searcher, TsStringUtils.MakeString("t", m_enWs), new[] {0, 2, 3});
			CheckSearch(searcher, TsStringUtils.MakeString("testing purpose", m_enWs), new[] {2});
		}

		/// <summary>
		/// Tests substring (match-anywhere) matching, including infix, case- and diacritic-insensitivity.
		/// </summary>
		[Test]
		public void SubstringSearchTest()
		{
			var searcher = new StringSearcher<int>(SearchType.Substring, m_wsManager);
			searcher.Add(0, 0, TsStringUtils.MakeString("language", m_enWs));
			searcher.Add(1, 0, TsStringUtils.MakeString("gauge", m_enWs));
			searcher.Add(2, 0, TsStringUtils.MakeString("résumé", m_frWs));
			searcher.Add(3, 0, TsStringUtils.MakeString("zebra", m_enWs));

			// infix match: "uage" is not a prefix of "language" but is a substring (fails under Prefix/FullText).
			CheckSearch(searcher, TsStringUtils.MakeString("uage", m_enWs), new[] {0});
			// interior substring
			CheckSearch(searcher, TsStringUtils.MakeString("gua", m_enWs), new[] {0});
			CheckSearch(searcher, TsStringUtils.MakeString("aug", m_enWs), new[] {1});
			// case-insensitive
			CheckSearch(searcher, TsStringUtils.MakeString("LANG", m_enWs), new[] {0});
			// diacritic-insensitive
			CheckSearch(searcher, TsStringUtils.MakeString("resume", m_frWs), new[] {2});
			// whole-string still matches
			CheckSearch(searcher, TsStringUtils.MakeString("zebra", m_enWs), new[] {3});
			// no match anywhere
			CheckNoResultsSearch(searcher, TsStringUtils.MakeString("xyz", m_enWs));
		}

		/// <summary>
		/// Substring search must not miss anything a full-text search would find on the same corpus and
		/// queries: its result set is a near superset 
		/// (see <see cref="Substring_isNotASupersetForOutOfOrderMultiWordQueries"/>) 
		/// of the full-text result set. This guards the promise that switching Find Lexical Entry to 
		/// substring never drops a result that used to appear.
		/// (This is a superset, not equality: substring also returns extra infix matches.)
		/// </summary>
		[Test]
		public void SubstringResultsIncludeAllFullTextResults()
		{
			var fullText = BuildMultiRunCorpus(SearchType.FullText);
			var substring = BuildMultiRunCorpus(SearchType.Substring);

			foreach (ITsString query in FullTextQueries())
			{
				// StringSearcher.Search can return the same item several times (once per matching word);
				// the real consumer (SearchEngine) dedupes via a HashSet, so compare as sets here too.
				int[] fullTextResults = fullText.Search(0, query).Distinct().ToArray();
				Assert.That(fullTextResults, Is.Not.Empty,
					"query '" + query.Text + "' should match something under full-text (otherwise the check is vacuous)");
				Assert.That(substring.Search(0, query).Distinct(), Is.SupersetOf(fullTextResults),
					"substring dropped a full-text match for query '" + query.Text + "'");
			}
		}

		/// <summary>
		/// Pins the boundary of the superset guarantee: it holds only for single-token or contiguous,
		/// in-order queries. A multi-word query whose words appear OUT OF ORDER matches under full-text
		/// (which ANDs the word tokens regardless of order) but NOT under substring (which needs the
		/// whole query to appear contiguously). This is the concrete case behind the scoping note on
		/// <see cref="FullTextQueries"/>.
		/// </summary>
		[Test]
		public void Substring_isNotASupersetForOutOfOrderMultiWordQueries()
		{
			var fullText = new StringSearcher<int>(SearchType.FullText, m_wsManager);
			var substring = new StringSearcher<int>(SearchType.Substring, m_wsManager);
			ITsString text = TsStringUtils.MakeString("alpha beta gamma", m_enWs);
			fullText.Add(0, 0, text);
			substring.Add(0, 0, text);

			// Words present but in a different order than the text.
			ITsString outOfOrder = TsStringUtils.MakeString("gamma alpha", m_enWs);

			Assert.That(fullText.Search(0, outOfOrder), Does.Contain(0),
				"full-text ANDs the word tokens, so it matches the words in any order");
			Assert.That(substring.Search(0, outOfOrder), Does.Not.Contain(0),
				"substring needs the query contiguous, so out-of-order words do not match");
		}
	}
}
