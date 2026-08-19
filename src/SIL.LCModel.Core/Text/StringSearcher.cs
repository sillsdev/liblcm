// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Icu;
using Icu.Collation;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.Utils;

namespace SIL.LCModel.Core.Text
{
	/// <summary>
	/// Type of string searching.
	/// </summary>
	public enum SearchType
	{
		/// <summary>
		/// Matches the entire string
		/// </summary>
		Exact,
		/// <summary>
		/// Matches at the beginning of a string
		/// </summary>
		Prefix,
		/// <summary>
		/// Matches any words in a string.
		/// </summary>
		FullText,
		/// <summary>
		/// Matches any portion within a string.
		/// </summary>
		Substring
	}

	/// <summary>
	/// This class is used to do fast searching of strings. Searching is case-insensitive.
	/// </summary>
	public class StringSearcher<T>
	{
		private const int SortKeyFactor = 5;

		#region SortKeyComparer class

		private class SortKeyComparer : IComparer<byte[]>
		{
			public int Compare(byte[] x, byte[] y)
			{
				// this code mimics the strcmp function in C
				if (x.Length == 0)
					return -y.Length; // zero if equal, neg if b is longer (considered larger)

				if (y.Length == 0)
					return 1; // ka is longer and considered larger.

				// Normal case, null termination should be present.
				int ib;
				for (ib = 0; x[ib] == y[ib] && x[ib] != 0; ++ib)
				{
					// skip merrily along until strings differ or end.
				}
				return x[ib] - y[ib];
			}
		}

		#endregion SortKeyComparer class

		#region SortKeyIndex class

		/// <summary>
		/// SortKeyIndex associates one or more items (of class T) with a key.
		/// It is optimized for the common case of only ONE item per key.
		/// </summary>
		private class SortKeyIndex
		{
			// The value may be either a single T, if only one is associated with the key, or a HashSet<T> if more than
			// one Add call has been received for the specified key.
			private readonly TreeDictionary<byte[], object > m_index = new TreeDictionary<byte[], object>(new SortKeyComparer());

			public void Add(byte[] sortKey, T item)
			{
				object oldVal;
				if (m_index.TryGetValue(sortKey, out oldVal))
				{
					// Seen this item before. Have we already changed to storing a set?
					var items = oldVal as HashSet<T>;
					if (items != null)
						items.Add(item); // already called twice or more with this key; just add to set.
					else
					{
						// second call with this key: make a set and store in the dictionary.
						items = new HashSet<T>();
						m_index[sortKey] = items;
						items.Add((T)oldVal);
						items.Add(item);
					}
				}
				else // first item for this key, store the item itself as a singleton.
					m_index.Add(sortKey, item);
			}

			public IEnumerable<T> GetItems(byte[] lower, byte[] upper)
			{
				foreach (var pair in m_index.GetRange(lower, upper))
				{
					var items = pair.Value as HashSet<T>;
					if (items != null)
					{
						foreach (T item in items)
							yield return item;
					}
					else
					{
						yield return (T) pair.Value;
					}
				}
			}
		}

		#endregion SortKeyIndex class

		#region SubstringEntry struct

		/// <summary>
		/// Pairs an indexed item with the raw text scanned for substring matches. Used by
		/// <see cref="SearchType.Substring"/>.
		/// </summary>
		private struct SubstringEntry
		{
			private readonly T m_item;
			private readonly string m_text;

			public SubstringEntry(T item, string text)
			{
				m_item = item;
				m_text = text;
			}

			public T Item { get { return m_item; } }
			public string Text { get { return m_text; } }
		}

		#endregion SubstringEntry struct

		private readonly Dictionary<Tuple<int, int>, SortKeyIndex> m_indices = new Dictionary<Tuple<int, int>, SortKeyIndex>();
		private readonly Dictionary<Tuple<int, int>, List<SubstringEntry>> m_rawIndices = new Dictionary<Tuple<int, int>, List<SubstringEntry>>();
		private readonly SearchType m_type;
		private readonly Func<int, string, byte[]> m_sortKeySelector;
		private readonly Func<int, string, IEnumerable<string>> m_tokenizer;

		/// <summary>
		/// Initializes a new instance of the <see cref="StringSearcher&lt;T&gt;"/> class.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="wsManager">The writing system store.</param>
		public StringSearcher(SearchType type, WritingSystemManager wsManager)
		{
			if (wsManager == null)
				throw new ArgumentNullException("wsManager");

			m_type = type;
			m_sortKeySelector = (ws, text) => wsManager.Get(ws).DefaultCollation.Collator.GetSortKey(text).KeyData;
			m_tokenizer = (ws, text) => BreakIterator.Split(BreakIterator.UBreakIteratorType.WORD,
				wsManager.Get(ws).IcuLocale, text);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="StringSearcher{T}"/> class.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="sortKeySelector">The sort key selector.</param>
		/// <param name="tokenizer">The text tokenizer</param>
		public StringSearcher(SearchType type, Func<int, string, byte[]> sortKeySelector, Func<int, string, IEnumerable<string>> tokenizer)
		{
			if (sortKeySelector == null)
				throw new ArgumentNullException("sortKeySelector");
			if (type == SearchType.FullText && tokenizer == null)
				throw new ArgumentNullException("tokenizer");

			m_type = type;
			m_sortKeySelector = sortKeySelector;
			m_tokenizer = tokenizer;
		}

		/// <summary>
		/// Adds the specified item to an index using the specified string.
		/// </summary>
		public void Add(T item, int indexId, ITsString tss)
		{
			if (tss.RunCount == 1) // VERY common special case
			{
				Add(item, indexId, tss.get_WritingSystemAt(0), tss.Text);
			}
			else
			{
				foreach (Tuple<int, string> wsStr in GetWsStrings(tss))
				{
					var wsId = wsStr.Item1;
					var text = wsStr.Item2;
					Add(item, indexId, wsId, text);
				}
			}
		}

		/// <summary>
		/// Adds the specified item to an index using the specified string.
		/// </summary>
		public void Add(T item, int indexId, int wsId, string text)
		{
			if (string.IsNullOrEmpty(text))
				return;

			switch (m_type)
			{
				case SearchType.Exact:
				case SearchType.Prefix:
					GetIndex(indexId, wsId).Add(m_sortKeySelector(wsId, text), item);
					break;

				case SearchType.FullText:
				{
					SortKeyIndex index = GetIndex(indexId, wsId);
					foreach (string token in RemoveWhitespaceAndPunctTokens(m_tokenizer(wsId, text)))
						index.Add(m_sortKeySelector(wsId, token), item);
					break;
				}

				case SearchType.Substring:
					GetRawIndex(indexId, wsId).Add(new SubstringEntry(item, text));
					break;
			}
		}

		/// <summary>
		/// Searches an index for the specified string.
		/// </summary>
		/// <param name="indexId">The index ID.</param>
		/// <param name="tss">The string.</param>
		/// <returns>The search results.</returns>
		public IEnumerable<T> Search(int indexId, ITsString tss)
		{
			if (tss == null || string.IsNullOrEmpty(tss.Text))
				return Enumerable.Empty<T>();

			if (tss.RunCount == 1) // VERY common special case
				return Search(indexId, tss.get_WritingSystemAt(0), tss.Text) ?? Enumerable.Empty<T>();

			IEnumerable<T> results = null;
			foreach (Tuple<int, string> wsStr in GetWsStrings(tss))
			{
				IEnumerable<T> items = Search(indexId, wsStr.Item1, wsStr.Item2);
				results = results == null ? items : results.Intersect(items);
			}
			return results ?? Enumerable.Empty<T>();
		}

		/// <summary>
		/// Searches an index for the specified string.
		/// </summary>
		/// <param name="indexId">The index id.</param>
		/// <param name="wsId">The ws id.</param>
		/// <param name="text">The text.</param>
		/// <returns>The search results.</returns>
		public IEnumerable<T> Search(int indexId, int wsId, string text)
		{
			if (string.IsNullOrEmpty(text))
				return Enumerable.Empty<T>();

			switch (m_type)
			{
				case SearchType.Exact:
				case SearchType.Prefix:
					{
						SortKeyIndex index = GetIndex(indexId, wsId);
						byte[] sortKey = m_sortKeySelector(wsId, text);
						var lower = new byte[text.Length * SortKeyFactor];
						Collator.GetSortKeyBound(sortKey, UColBoundMode.UCOL_BOUND_LOWER, ref lower);
						var upper = new byte[text.Length * SortKeyFactor];
						Collator.GetSortKeyBound(sortKey,
											m_type == SearchType.Exact
												? UColBoundMode.UCOL_BOUND_UPPER
												: UColBoundMode.UCOL_BOUND_UPPER_LONG, ref upper);

						return index.GetItems(lower, upper);
					}

				case SearchType.FullText:
					{
						SortKeyIndex index = GetIndex(indexId, wsId);
						IEnumerable<T> results = null;
						string[] tokens = RemoveWhitespaceAndPunctTokens(m_tokenizer(wsId, text)).ToArray();
						for (int i = 0; i < tokens.Length; i++)
						{
							byte[] sortKey = m_sortKeySelector(wsId, tokens[i]);
							var lower = new byte[tokens[i].Length*SortKeyFactor];
							Collator.GetSortKeyBound(sortKey, UColBoundMode.UCOL_BOUND_LOWER, ref lower);
							var upper = new byte[tokens[i].Length*SortKeyFactor];
							Collator.GetSortKeyBound(sortKey,
												i < tokens.Length - 1
													? UColBoundMode.UCOL_BOUND_UPPER
													: UColBoundMode.UCOL_BOUND_UPPER_LONG, ref upper);
							IEnumerable<T> items = index.GetItems(lower, upper);
							results = results == null ? items : results.Intersect(items);
						}
						return results;
					}

				case SearchType.Substring:
					{
						List<SubstringEntry> raw;
						if (!m_rawIndices.TryGetValue(Tuple.Create(indexId, wsId), out raw))
							return Enumerable.Empty<T>();
						CompareInfo ci = CultureInfo.InvariantCulture.CompareInfo;
						// Fold diacritics only when the search term itself has none: an unmarked query
						// matches accented text ("cafe" finds "café"), but a query that includes an accent
						// is treated as specific ("café" does not match a bare "cafe").
						CompareOptions options = ContainsDiacritic(text)
							? CompareOptions.IgnoreCase
							: CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
						return raw.Where(entry => ci.IndexOf(entry.Text, text, options) >= 0).Select(entry => entry.Item);
					}
			}

			return Enumerable.Empty<T>();
		}

		private static IEnumerable<string> RemoveWhitespaceAndPunctTokens(IEnumerable<string> tokens)
		{
			return tokens.Where(t => !t.All(c => Character.IsSpace(c) || Character.IsPunct(c)));
		}

		/// <summary>
		/// True if the string contains a diacritic.
		/// </summary>
		private static bool ContainsDiacritic(string value)
		{
			return value.Normalize(NormalizationForm.FormD)
				.Any(ch => Character.GetCharType(ch) == Character.UCharCategory.NON_SPACING_MARK);
		}

		/// <summary>
		/// Clears all of the indices.
		/// </summary>
		public void Clear()
		{
			m_indices.Clear();
			m_rawIndices.Clear();
		}

		private SortKeyIndex GetIndex(int indexId, int ws)
		{
			var key = Tuple.Create(indexId, ws);
			SortKeyIndex index;
			if (!m_indices.TryGetValue(key, out index))
			{
				index = new SortKeyIndex();
				m_indices[key] = index;
				return index;
			}
			return index;
		}

		private List<SubstringEntry> GetRawIndex(int indexId, int ws)
		{
			var key = Tuple.Create(indexId, ws);
			List<SubstringEntry> list;
			if (!m_rawIndices.TryGetValue(key, out list))
			{
				list = new List<SubstringEntry>();
				m_rawIndices[key] = list;
			}
			return list;
		}

		private static IEnumerable<Tuple<int, string>> GetWsStrings(ITsString tss)
		{
			var sb = new StringBuilder();
			int curWs = -1;
			for (int i = 0; i < tss.RunCount; i++)
			{
				int var;
				int ws = tss.get_Properties(i).GetIntPropValues((int)FwTextPropType.ktptWs, out var);
				if (curWs == -1)
				{
					curWs = ws;
				}
				else if (ws != curWs)
				{
					yield return Tuple.Create(curWs, sb.ToString());
					sb = new StringBuilder();
					curWs = ws;
				}
				sb.Append(tss.get_RunText(i));
			}
			yield return Tuple.Create(curWs, sb.ToString());
		}
	}
}
