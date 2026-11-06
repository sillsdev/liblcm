// --------------------------------------------------------------------------------------------
// Copyright (c) 2011-2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
//
// File: Surrogates.cs
// --------------------------------------------------------------------------------------------

namespace SIL.LCModel.Utils
{
	/// <summary>
	/// A home for functions related to Unicode surrogate pairs.
	/// (Some of these unfortunately are duplicated in FwUtils.)
	/// </summary>
	public static class Surrogates
	{
		public const char kMinLeadSurrogate = '\xD800';
		public const char kMaxLeadSurrogate = '\xDBFF';

		/// <summary>
		/// Returns a string representation of the codepoint, handling surrogate pairs if necessary.
		/// </summary>
		/// <param name="codepoint">The codepoint</param>
		/// <returns>The string representation of the codepoint</returns>
		public static string StringFromCodePoint(int codepoint)
		{
			return char.ConvertFromUtf32(codepoint);
		}

		/// <summary>
		/// Return the full code point starting at index ich of the string. A high surrogate followed by a
		/// low surrogate gives the supplementary code point. Anything else gives the char itself: that includes
		/// an unpaired surrogate, and also a low surrogate that is the second half of a pair when ich points at
		/// it (so the pair is not re-read from its middle). Unlike char.ConvertToUtf32(string, int), this never
		/// throws on an unpaired surrogate (which can occur in pasted or imported text).
		/// </summary>
		public static int CodePointAt(string st, int ich)
		{
			char ch = st[ich];
			if (IsLeadSurrogate(ch) && ich < st.Length - 1 && IsTrailSurrogate(st[ich + 1]))
				return char.ConvertToUtf32(ch, st[ich + 1]);
			return ch;
		}

		/// <summary>
		/// Gives the string for a code point as returned by <see cref="CodePointAt"/>: unlike
		/// <see cref="StringFromCodePoint"/>, an unpaired surrogate code unit gives that one char instead of throwing.
		/// </summary>
		public static string StringFromCodePointOrLoneSurrogate(int codepoint)
		{
			if (codepoint >= kMinLeadSurrogate && codepoint <= 0xDFFF)
				return ((char)codepoint).ToString();
			return char.ConvertFromUtf32(codepoint);
		}

		/// <summary>
		/// Whether the string contains a surrogate that is not half of a valid high+low pair.
		/// </summary>
		public static bool HasLoneSurrogate(string st)
		{
			if (st == null)
				return false;
			for (int i = 0; i < st.Length; i++)
			{
				if (IsLeadSurrogate(st[i]) && i < st.Length - 1 && IsTrailSurrogate(st[i + 1]))
					i++;
				else if (IsLeadSurrogate(st[i]) || IsTrailSurrogate(st[i]))
					return true;
			}
			return false;
		}

		/// <summary>
		/// Replace each surrogate that is not half of a valid pair with U+FFFD REPLACEMENT CHARACTER.
		/// The length of the string never changes, so offsets into it stay valid.
		/// </summary>
		public static string ReplaceLoneSurrogates(string st)
		{
			if (!HasLoneSurrogate(st))
				return st;
			var chars = st.ToCharArray();
			for (int i = 0; i < chars.Length; i++)
			{
				if (IsLeadSurrogate(chars[i]) && i < chars.Length - 1 && IsTrailSurrogate(chars[i + 1]))
					i++;
				else if (IsLeadSurrogate(chars[i]) || IsTrailSurrogate(chars[i]))
					chars[i] = '\uFFFD';
			}
			return new string(chars);
		}

		/// <summary>
		/// Return a full 32-bit character value from the surrogate pair.
		/// </summary>
		public static int Int32FromSurrogates(char ch1, char ch2)
		{
			return char.ConvertToUtf32(ch1, ch2);
		}
		/// <summary>
		/// Whether the character is the first of a surrogate pair.
		///  This was copied from SIL.FieldWorks.IText
		/// </summary>
		/// <param name="ch">The character</param>
		/// <returns></returns>
		public static bool IsLeadSurrogate(char ch)
		{
			// could also use char.IsHighSurrogate(ch)
			return ch >= kMinLeadSurrogate && ch <= kMaxLeadSurrogate;
		}
		/// <summary>
		/// Whether the character is the second of a surrogate pair.
		///  This was copied from SIL.FieldWorks.IText
		/// </summary>
		/// <param name="ch">The character</param>
		/// <returns></returns>
		public static bool IsTrailSurrogate(char ch)
		{
			// could also use char.IsLowSurrogate(ch)
			const char minTrailSurrogate = '\xDC00';
			const char maxTrailSurrogate = '\xDFFF';
			return ch >= minTrailSurrogate && ch <= maxTrailSurrogate;
		}
		/// <summary>
		/// Increment an index into a string, allowing for surrogates.
		/// This was copied from SIL.FieldWorks.IText
		/// </summary>
		/// <param name="st"></param>
		/// <param name="ich"></param>
		/// <returns></returns>
		public static int NextChar(string st, int ich)
		{
			if (IsLeadSurrogate(st[ich]) && ich < st.Length - 1 && IsTrailSurrogate(st[ich+1]))
				return ich + 2;
			return ich + 1;
		}

		/// <summary>
		/// Decrement an index into a string, allowing for surrogates.
		/// Assumes ich is pointing at the START of a character; will move back two if the two characters
		/// at ich-1 and ich-2 are a pair.
		/// </summary>
		public static int PrevChar(string st, int ich)
		{
			if (ich >= 2 && IsLeadSurrogate(st[ich - 2]) && IsTrailSurrogate(st[ich - 1]))
				return ich - 2;
			return ich - 1;
		}
	}
}
