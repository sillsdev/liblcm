// Copyright (c) 2004-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Icu;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Utils;

namespace SIL.LCModel.Core.Text
{
	#region TsStringUtils class
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// TsStringUtils is a collection of static methods for working with TS strings.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public static class TsStringUtils
	{
		/// <summary>Regular expression used to check whether a string appears to be a URL (as
		/// opposed to a file path)</summary>
		public static readonly Regex RegexUrl = new Regex("^[a-z]+://", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private static readonly RecentItemsCache<Tuple<XElement, ILgWritingSystemFactory>, ITsTextProps> RecentTextProps = new RecentItemsCache<Tuple<XElement, ILgWritingSystemFactory>, ITsTextProps>(100);
		private static readonly FwObjDataTypes[] HotObjectTypes = { FwObjDataTypes.kodtNameGuidHot, FwObjDataTypes.kodtOwnNameGuidHot };
		private static readonly FwObjDataTypes[] OwnedObjectTypes = { FwObjDataTypes.kodtGuidMoveableObjDisp, FwObjDataTypes.kodtOwnNameGuidHot };
		private static readonly FwObjDataTypes[] FootnoteAndPicObjectTypes = { FwObjDataTypes.kodtGuidMoveableObjDisp, FwObjDataTypes.kodtOwnNameGuidHot, FwObjDataTypes.kodtNameGuidHot };

		static TsStringUtils()
		{
			// this is the one and only ITsStrFactory for all of FW
			TsStrFactory = new TsStrFactory();
			// this is the one and only ITsPropsFactory for all of FW
			TsPropsFactory = new TsPropsFactory();
		}

		/// <summary>
		/// Gets or sets the singleton <see cref="ITsString"/> factory that everything uses.
		/// </summary>
		public static ITsStrFactory TsStrFactory { get; set; }

		/// <summary>
		/// Gets or sets the singleton <see cref="ITsTextProps"/> factory that everything uses.
		/// </summary>
		public static ITsPropsFactory TsPropsFactory { get; set; }

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get an XML representation of the given ITsString.
		/// </summary>
		/// <param name="tss">The ITsString object</param>
		/// <param name="wsf">Writing system factory so that we can convert writing system
		/// integer codes (which are database object ids) to the corresponding strings.</param>
		/// <param name="ws">If nonzero, the writing system for a multilingual string (&lt;AStr&gt;).
		/// If zero, then this is a monolingual string (&lt;Str&gt;).</param>
		/// <param name="fWriteObjData"> If true, then write out embedded pictures and links.  If false, ignore
		///	any runs that contain such objects. </param>
		/// <returns>
		/// The XML representation of <paramref name="tss"/>.
		/// </returns>
		/// ------------------------------------------------------------------------------------
		public static string GetXmlRep(ITsString tss, ILgWritingSystemFactory wsf, int ws, bool fWriteObjData = true)
		{
			return TsStringSerializer.SerializeTsStringToXml(tss, wsf, ws, fWriteObjData);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get an XML representation of the given ITsTextProps.
		/// </summary>
		/// <param name="ttp">The TTP.</param>
		/// <param name="wsf">The WSF.</param>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public static string GetXmlRep(ITsTextProps ttp, ILgWritingSystemFactory wsf)
		{
			return TsPropsSerializer.SerializePropsToXml(ttp, wsf);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Convert XML (produced by the above 'GetString' method) into an ITsTextProps.
		/// </summary>
		/// <param name="xml">The XML.</param>
		/// <param name="wsf">The WSF.</param>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public static ITsTextProps GetTextProps(XElement xml, ILgWritingSystemFactory wsf)
		{
			return RecentTextProps.GetItem(new Tuple<XElement, ILgWritingSystemFactory>(xml, wsf), tuple => TsPropsSerializer.DeserializePropsFromXml(tuple.Item1, tuple.Item2));
		}

		/// <summary>
		/// Determines whether the specified character is word-forming.
		/// </summary>
		public static bool IsWordForming(int ch, ILgWritingSystemFactory wsf, int wsHandle)
		{
			ILgWritingSystem ws = wsf.get_EngineOrNull(wsHandle);
			return ws == null ? IsWordForming(ch) : ws.get_IsWordForming(ch);
		}

		/// <summary>
		/// Determines whether the specified character is word-forming.
		/// </summary>
		public static bool IsWordForming(int ch)
		{
			switch (Character.GetCharType(ch))
			{
				case Character.UCharCategory.UPPERCASE_LETTER:
				case Character.UCharCategory.LOWERCASE_LETTER:
				case Character.UCharCategory.TITLECASE_LETTER:
				case Character.UCharCategory.MODIFIER_LETTER:
				case Character.UCharCategory.OTHER_LETTER:
				case Character.UCharCategory.NON_SPACING_MARK:
				case Character.UCharCategory.COMBINING_SPACING_MARK:
				case Character.UCharCategory.MODIFIER_SYMBOL:
					return true;
				default:
					return false;
			}
		}

		#region Handling ORCs
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get the string to pass as ObjData property
		/// </summary>
		/// <param name="guid">GUID of the data</param>
		/// <param name="objectDataType">Type of object (e.g. kodtNameGuidHot).</param>
		/// <returns>byte array</returns>
		/// ------------------------------------------------------------------------------------
		public static byte[] GetObjData(Guid guid, FwObjDataTypes objectDataType)
		{
			byte[] rgGuid = guid.ToByteArray();
			byte[] rgRet = new byte[rgGuid.Length + 2];
			rgRet[0] = (byte) objectDataType;
			rgRet[1] = 0;
			rgGuid.CopyTo(rgRet, 2);

			return rgRet;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Insert (or replace) an ORC run in a string builder.
		/// </summary>
		/// <param name="guid">The GUID representing the embedded object</param>
		/// <param name="objDataType">The type of embedding</param>
		/// <param name="tsStrBldr">The TS string builder</param>
		/// <param name="ichMin">The position at which the object is to be inserted</param>
		/// <param name="ichLim">If replacing an existing ORC (or other text), this is the Lim
		/// position (otherwise, should be set equal to ichMin)</param>
		/// <param name="ws">The ID of the writing system to use for the inserted run, or 0
		/// to leave unspecified</param>
		/// ------------------------------------------------------------------------------------
		public static void InsertOrcIntoPara(Guid guid, FwObjDataTypes objDataType, ITsStrBldr tsStrBldr, int ichMin, int ichLim, int ws)
		{
			byte[] objData = GetObjData(guid, objDataType);
			ITsPropsBldr propsBldr = MakePropsBldr();
			propsBldr.SetStrPropValueRgch((int)FwTextPropType.ktptObjData, objData, objData.Length);
			if (ws != 0)
				propsBldr.SetIntPropValues((int)FwTextPropType.ktptWs, 0, ws);
			tsStrBldr.Replace(ichMin, ichLim, StringUtils.kszObject, propsBldr.GetTextProps());
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates an object replacement character from a Guid
		/// </summary>
		/// <param name="guid">guid that maps to an ORC</param>
		/// <param name="type">type of ORC to return within the TsString</param>
		/// <param name="ws">The writing system.</param>
		/// <returns>ITsString</returns>
		/// ------------------------------------------------------------------------------------
		public static ITsString CreateOrcFromGuid(Guid guid, FwObjDataTypes type, int ws)
		{
			ITsStrBldr tsStrBldr = MakeStrBldr();
			byte[] objData = GetObjData(guid, type);
			ITsPropsBldr propsBldr = MakePropsBldr();
			propsBldr.SetStrPropValueRgch((int)FwTextPropType.ktptObjData, objData, objData.Length);

			propsBldr.SetIntPropValues((int)FwTextPropType.ktptWs, 0, ws);
			tsStrBldr.Replace(0, 0, StringUtils.kszObject, propsBldr.GetTextProps());
			return tsStrBldr.GetString();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Remove all reference ORCs for the specified builder
		/// </summary>
		/// <param name="tssBldr">the builder</param>
		/// <param name="guidToRemove">guid of the ORC to remove</param>
		/// <returns>the ich location of the ORC that was deleted, or -1 if no ORC was
		/// deleted</returns>
		/// ------------------------------------------------------------------------------------
		public static int DeleteOrcFromBuilder(ITsStrBldr tssBldr, Guid guidToRemove)
		{
			Debug.Assert(guidToRemove != Guid.Empty);
			if (guidToRemove == Guid.Empty)
				return -1;

			int iRun = 0;
			while (iRun < tssBldr.RunCount)
			{
				Guid guid = GetGuidFromRun(tssBldr.GetString(), iRun);

				if (guid == guidToRemove)
				{
					// ORC with same Guid found. Remove it.
					int ichMin, ichLim;
					TsRunInfo info;
					tssBldr.FetchRunInfo(iRun, out info);
					tssBldr.GetBoundsOfRun(iRun, out ichMin, out ichLim);
					ITsPropsBldr propsBldr = tssBldr.get_Properties(iRun).GetBldr();
					propsBldr.SetStrPropValue((int)FwTextPropType.ktptObjData, null);
					tssBldr.Replace(ichMin, ichLim, null, propsBldr.GetTextProps());
					return info.ichMin;
				}
				iRun++;
			}
			return -1;
		}
		#endregion

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Determines whether or not the specified character is defined in ICU.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static bool IsCharacterDefined(string chr)
		{
			if (String.IsNullOrEmpty(chr))
				return false;

			// Go through the codepoints in the character.
			return chr.All(c => IsCodePointDefined(c));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Determines whether or not the specified codepoint is defined in ICU. If it is and
		/// its a custom PUA character, then make sure the language definition's PUA character
		/// collection is updated. Return false if the codepoint is not defined in ICU.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private static bool IsCodePointDefined(int codepoint)
		{
			PUACharacter puaChar = new PUACharacter(codepoint);
			try
			{
				// If the definition doesn't exist in ICU or the character has no name then
				// it's undefined and shouldn't be allowed in the valid characters list.
				if (!puaChar.RefreshFromIcu(false) || puaChar.Name.Length == 0)
					return false;

				// If the ICU category is one of the "Other" categories, then it's
				// undefined and shouldn't be allowed in the valid characters list.
				string ucdrep = puaChar.GeneralCategory.UcdRepresentation;
				ucdrep = ucdrep.ToUpperInvariant();

				// TODO: When tabs are supported, then allow them (they are in category Cc).
				// See TE-3004.
				if (ucdrep[0] == 'C' && ucdrep[1] != 'F')
					return false;
			}
			catch
			{
				return false;
			}

			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Parses the specified string into a list of characters. The unparsed list is a
		/// string of valid characters delimited with the specified delimiter.
		/// </summary>
		/// <param name="chars">The string containing a delimited list of characters.</param>
		/// <param name="delimiter">The delimiter (passed as a string, but really just a single
		/// character).</param>
		/// <param name="invalidCharacters">The invalid characters.</param>
		/// <returns>List of unique characters</returns>
		/// ------------------------------------------------------------------------------------
		public static List<string> ParseCharString(string chars, string delimiter, out List<string> invalidCharacters)
		{
			invalidCharacters = new List<string>();
			return ParseCharString(chars, delimiter, invalidCharacters);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Parses the specified string into a list of characters, ignoring any bogus characters
		/// (digraphs, undefined Unicode characters, lone diacritics, etc.). The unparsed list
		/// is a string of valid characters delimited with the specified delimiter.
		/// </summary>
		/// <param name="chars">The string containing a delimited list of characters.</param>
		/// <param name="delimiter">The delimiter (passed as a string, but really just a single
		/// character).</param>
		/// ------------------------------------------------------------------------------------
		public static List<string> ParseCharString(string chars, string delimiter)
		{
			return ParseCharString(chars, delimiter, null);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Parses the specified string into a list of characters. The unparsed list is a
		/// string of valid characters delimited with the specified delimiter.
		/// </summary>
		/// <param name="chars">The string containing a delimited list of characters.</param>
		/// <param name="delimiter">The delimiter (passed as a string, but really just a single
		/// character).</param>
		/// <param name="invalidCharacters">List of bogus characters (digraphs, undefined
		/// Unicode characters, lone diacritics, etc.)encountered. if set to <c>null</c> ignores
		/// bogus characters.</param>
		/// <returns>List of unique characters</returns>
		/// ------------------------------------------------------------------------------------
		private static List<string> ParseCharString(string chars, string delimiter, List<string> invalidCharacters)
		{
			if (string.IsNullOrEmpty(chars))
				return new List<string>();

			if (string.IsNullOrEmpty(delimiter))
				delimiter = " ";

			string[] charsArray = chars.Split(delimiter.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

			List<string> charsList = new List<string>(charsArray.Length);

			foreach (string chr in charsArray)
			{
				if (IsValidChar(chr))
				{
					if (!charsList.Contains(chr))
						charsList.Add(chr);
				}

				else if (invalidCharacters != null)
				{
					invalidCharacters.Add(chr);
				}
			}

			// If the original list of characters started with a space and that was the delimiter,
			// then make sure to add it back into the list because it will have been lost by the
			// Split above
			if (delimiter == " " && chars[0] == delimiter[0])
				charsList.Insert(0, delimiter);

			return charsList;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Determines whether the specified string is a valid string consisting of exactly one
		/// initial base character followed by zero or more legally placed combining marks.
		/// </summary>
		/// <param name="chr">The string to check.</param>
		/// ------------------------------------------------------------------------------------
		public static bool IsValidChar(string chr)
		{
			// We need to decompose the results in order to check for equality because
			// it is possible (Korean is the only example of this we know of) for multiple base
			// letters to compose into a single base letter. In that case,
			// ValidateCharacterSequence returns the composed character because we don't really
			// want to store the decomposed sequence as a valid character since the characters
			// check always operates on individual base characters. (To make that check work
			// properly for Korean, if the valid characters list contains the composed
			// characters, the check would need to account for this and try to compose the data
			// being checked.)
			string ch = ValidateCharacterSequence(chr);
			return ch.Length != 0 && Normalizer.Normalize(ch, Normalizer.UNormalizationMode.UNORM_NFD) == Normalizer.Normalize(chr, Normalizer.UNormalizationMode.UNORM_NFD);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Finds the first base character/combining diacritic combination and removes any
		/// remaining characters.
		/// </summary>
		/// <param name="origChars">The original string of characters.</param>
		/// ------------------------------------------------------------------------------------
		public static string ValidateCharacterSequence(string origChars)
		{
			// Allow spaces (Zs), hard line breaks (Zl), and other formatting characters (Cf) in
			// isolation only.
			if (origChars.Length == 1)
			{
				if (Character.GetCharType(origChars[0]) == Character.UCharCategory.LINE_SEPARATOR || Character.GetCharType(origChars[0]) == Character.UCharCategory.SPACE_SEPARATOR
					|| Character.GetCharType(origChars[0]) == Character.UCharCategory.FORMAT_CHAR)
				{
					return origChars;
				}
			}

			var newChars = new StringBuilder();
			bool baseFound = false;
			bool fPrecedingCharWasMark = false;
			// Extract first base character and any following diacritics
			for (int ich = 0; ich < origChars.Length; ich++)
			{
				char chr = origChars[ich];

				if (!baseFound)
				{
					// If this is not a valid base character, keep looking.
					if (!Character.IsLetter(chr) && !Character.IsNumeric(chr) &&
						Character.GetCharType(chr) != Character.UCharCategory.PRIVATE_USE_CHAR &&
						!Character.IsPunct(chr) && !Character.IsSymbol(chr))
					{
						continue;
					}

					baseFound = true;
				}
				else
				{
					// If this is not a diacritic or a ZWJ or ZWNJ between diacritics,
					// discard the rest of the string.
					if (Character.IsMark(chr))
					{
						fPrecedingCharWasMark = true;
					}

					else if ((chr == '\u200C' || chr == '\u200D') && fPrecedingCharWasMark && origChars.Length > ich + 1 && Character.IsMark(origChars[ich + 1]))
					{
						fPrecedingCharWasMark = false;
					}
					else
					{
						// This handles special situations like Korean, where multiple base letters
						// (representing phonemes) can compose into a single base letter (representing a
						// syllable).
						string composed = Normalizer.Normalize(origChars, Normalizer.UNormalizationMode.UNORM_NFKC);
						if (composed.Length == 1)
							return composed;
						break;
					}
				}

				newChars.Append(chr);
			}

			return newChars.ToString();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Determines whether the given character is used to end a sentence.
		/// </summary>
		/// <param name="ch">The character.</param>
		/// <param name="cc">The general character category.</param>
		/// <returns>
		/// 	<c>true</c> if the character ends a sentence; otherwise, <c>false</c>.
		/// </returns>
		/// ------------------------------------------------------------------------------------
		public static bool IsEndOfSentenceChar(int ch, Character.UCharCategory cc)
		{
			// The preliminary check of cc is just for efficiency. All these characters have this property.
			// EXCLAMATION MARK
			// FULL STOP
			// QUESTION MARK
			// ARMENIAN EXCLAMATION MARK
			// ARMENIAN QUESTION MARK
			// ARMENIAN FULL STOP
			// ARABIC QUESTION MARK
			// ARABIC FULL STOP
			// SYRIAC END OF PARAGRAPH
			// SYRIAC SUPRALINEAR FULL STOP
			// SYRIAC SUBLINEAR FULL STOP
			// DEVANAGARI DANDA
			// DEVANAGARI DOUBLE DANDA
			// MYANMAR SIGN LITTLE SECTION
			// MYANMAR SIGN SECTION
			// ETHIOPIC FULL STOP
			// ETHIOPIC QUESTION MARK
			// ETHIOPIC PARAGRAPH SEPARATOR
			// CANADIAN SYLLABICS FULL STOP
			// MONGOLIAN FULL STOP
			// MONGOLIAN MANCHU FULL STOP
			// LIMBU EXCLAMATION MARK
			// LIMBU QUESTION MARK
			// KHMER SIGN KHAN
			// KHMER SIGN BARIYOOSAN
			// DOUBLE EXCLAMATION MARK
			// INTERROBANG
			// DOUBLE QUESTION MARK
			// QUESTION EXCLAMATION MARK
			// EXCLAMATION QUESTION MARK
			// IDEOGRAPHIC FULL STOP
			// SMALL FULL STOP
			// SMALL QUESTION MARK
			// SMALL EXCLAMATION MARK
			// FULLWIDTH EXCLAMATION MARK
			// FULLWIDTH FULL STOP
			// FULLWIDTH QUESTION MARK
			// HALFWIDTH IDEOGRAPHIC FULL STOP
			// Except this is not a normal punctuation character.
			return (cc == Character.UCharCategory.OTHER_PUNCTUATION && (ch == 0x0021 || ch == 0x002E || ch == 0x003F || ch == 0x055C || ch == 0x055E || ch == 0x0589 || ch == 0x061F || ch == 0x06D4 || ch == 0x0700 || ch == 0x0701 || ch == 0x0702 || ch == 0x0964 || ch == 0x0965 || ch == 0x104A || ch == 0x104B || ch == 0x1362 || ch == 0x1367 || ch == 0x1368 || ch == 0x166E || ch == 0x1803 || ch == 0x1809 || ch == 0x1944 || ch == 0x1945 || ch == 0x17D4 || ch == 0x17D5 || ch == 0x203C || ch == 0x203D || ch == 0x2047 || ch == 0x2048 || ch == 0x2049 || ch == 0x3002 || ch == 0xFE52 || ch == 0xFE56 || ch == 0xFE57 || ch == 0xFF01 || ch == 0xFF0E || ch == 0xFF1F || ch == 0xFF61)) || ch == 0x00A7;
			// SECTION SIGN (used for forced segment breaks w/o punctuation)
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Returns a string an NFC (normalized form, composed) form of a Unicode string.
		/// </summary>
		/// <param name="source">String to compose.</param>
		/// <returns>String in NFC form.</returns>
		/// ------------------------------------------------------------------------------------
		public static string Compose(string source)
		{
			// If the string passed in is not "valid", spit it back out.
			if (string.IsNullOrEmpty(source))
				return source;

			// TS Strings no longer allow the WS to be unspecified, so we just fake it by passing 1.
			ITsString tss = MakeString(source, 1).get_NormalizedForm(FwNormalizationMode.knmNFC);
			return tss.Text;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Returns a string an NFD (normalized form, decomposed) form of a Unicode string.
		/// Enhance JohnT: This is not a very efficient implementation; we should be able
		/// to use some ICU method directly (as the ITsString implementation does). Fix if ever
		/// used for something high-performance!
		/// </summary>
		/// <param name="source">String to compose.</param>
		/// <returns>String in NFD form.</returns>
		/// ------------------------------------------------------------------------------------
		public static string NormalizeNfd(string source)
		{
			// If the string passed in is not "valid", spit it back out.
			if (string.IsNullOrEmpty(source))
				return source;

			ITsStrBldr tssBldr = MakeStrBldr();
			tssBldr.Replace(0, 0, source, null);
			ITsString tss = tssBldr.GetString().get_NormalizedForm(FwNormalizationMode.knmNFD);
			return tss.Text;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Normalizes the specified TsString in NFD form.
		/// </summary>
		/// <param name="source">The TsString to normalize.</param>
		/// <returns>The normalized TsString</returns>
		/// ------------------------------------------------------------------------------------
		public static ITsString NormalizeNfd(ITsString source)
		{
			if (source == null || source.Length == 0 || source.get_IsNormalizedForm(FwNormalizationMode.knmNFD))
			{
				return source;
				// No need to normalize
			}
			return source.get_NormalizedForm(FwNormalizationMode.knmNFD);
		}

		#region ORC-related methods
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get an owned Guid from the given run in a structured text string.
		/// </summary>
		/// <param name="tss">given structured text string</param>
		/// <param name="iRun">given run</param>
		/// <param name="odt">object data type, 0 if no owned guid located</param>
		/// <returns>
		/// The GUID associated with the specified run of the tss if it is for an owned object;
		/// otherwise Guid.Empty
		/// </returns>
		/// ------------------------------------------------------------------------------------
		public static Guid GetOwnedGuidFromRun(ITsString tss, int iRun, out FwObjDataTypes odt)
		{
			TsRunInfo tri;
			ITsTextProps ttp;

			return GetOwnedGuidFromRun(tss, iRun, out odt, out tri, out ttp);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get an owned Guid from the given run in a structured text string.
		/// </summary>
		/// <param name="tss">given structured text string</param>
		/// <param name="iRun">given run</param>
		/// <param name="odt">object data type, 0 if no owned guid located</param>
		/// <param name="tri">run information</param>
		/// <param name="ttp">text properties of the run</param>
		/// <returns>
		/// The GUID associated with the specified run of the tss if it is for an owned object;
		/// otherwise Guid.Empty
		/// </returns>
		/// ------------------------------------------------------------------------------------
		public static Guid GetOwnedGuidFromRun(ITsString tss, int iRun, out FwObjDataTypes odt, out TsRunInfo tri, out ITsTextProps ttp)
		{
			return GetGuidFromRun(tss, iRun, out odt, out tri, out ttp, OwnedObjectTypes);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get a Guid from the given run in a structured text string.
		/// </summary>
		/// <param name="tss">given structured text string</param>
		/// <param name="iRun">given run</param>
		/// <returns>
		/// The GUID associated with the specified run of the tss, if any; otherwise Guid.Empty
		/// </returns>
		/// ------------------------------------------------------------------------------------
		public static Guid GetGuidFromRun(ITsString tss, int iRun)
		{
			TsRunInfo tri;
			ITsTextProps ttp;

			return GetGuidFromRun(tss, iRun, out tri, out ttp);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get a Guid from the given run in a structured text string.
		/// </summary>
		/// <param name="tss">given structured text string</param>
		/// <param name="iRun">given run</param>
		/// <param name="tri">run information</param>
		/// <param name="ttp">text properties of the run</param>
		/// <returns>
		/// The GUID associated with the specified run of the tss, if any; otherwise Guid.Empty
		/// </returns>
		/// ------------------------------------------------------------------------------------
		public static Guid GetGuidFromRun(ITsString tss, int iRun, out TsRunInfo tri, out ITsTextProps ttp)
		{
			FwObjDataTypes odt;
			return GetGuidFromRun(tss, iRun, out odt, out tri, out ttp, null);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get a Guid from the given run in a structured text string.
		/// </summary>
		/// <param name="tss">given structured text string</param>
		/// <param name="iRun">given run</param>
		/// <param name="desiredOrcType">The desired ORC type</param>
		/// <returns>
		/// The GUID associated with the specified run of the tss, if any; otherwise Guid.Empty
		/// </returns>
		/// ------------------------------------------------------------------------------------
		public static Guid GetGuidFromRun(ITsString tss, int iRun, FwObjDataTypes desiredOrcType)
		{
			TsRunInfo tri;
			ITsTextProps ttp;
			FwObjDataTypes odt;
			return GetGuidFromRun(tss, iRun, out odt, out tri, out ttp, new FwObjDataTypes[] { desiredOrcType });
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get a Guid from the given run in a structured text string.
		/// </summary>
		/// <param name="tss">given structured text string</param>
		/// <param name="iRun">given run</param>
		/// <param name="odt">object data type, 0 if no ORC guid located</param>
		/// <param name="tri">run information</param>
		/// <param name="ttp">text properties of the run</param>
		/// <param name="desiredOrcTypes">The desired ORC types, or null to return any type of
		/// ORC</param>
		/// <returns>
		/// The GUID associated with the specified run of the tss, if any; otherwise Guid.Empty
		/// </returns>
		/// ------------------------------------------------------------------------------------
		public static Guid GetGuidFromRun(ITsString tss, int iRun, out FwObjDataTypes odt, out TsRunInfo tri, out ITsTextProps ttp, FwObjDataTypes[] desiredOrcTypes)
		{
			ttp = null;
			return GetGuidFromRun(tss, iRun, desiredOrcTypes, out odt, out tri, ref ttp);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get a Guid from the given run in a structured text string.
		/// </summary>
		/// <param name="tss">given structured text string</param>
		/// <param name="iRun">given run</param>
		/// <param name="ttp">text properties of the run</param>
		/// <returns>
		/// The GUID associated with the specified run of the tss, if any; otherwise Guid.Empty
		/// </returns>
		/// ------------------------------------------------------------------------------------
		public static Guid GetGuidFromRun(ITsString tss, int iRun, ITsTextProps ttp)
		{
			FwObjDataTypes odt;
			TsRunInfo tri;
			return GetGuidFromRun(tss, iRun, null, out odt, out tri, ref ttp);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get a Guid from the given run in a structured text string.
		/// </summary>
		/// <param name="tss">given structured text string</param>
		/// <param name="iRun">given run</param>
		/// <param name="odt">object data type, 0 if no ORC guid located</param>
		/// <param name="tri">run information</param>
		/// <param name="ttp">text properties of the run (if incoming value is  null and the
		/// run is an object, this will be set to the run props)</param>
		/// <param name="desiredOrcTypes">The desired ORC types, or null to return any type of
		/// ORC</param>
		/// <returns>
		/// The GUID associated with the specified run of the tss, if any; otherwise Guid.Empty
		/// </returns>
		/// ------------------------------------------------------------------------------------
		private static Guid GetGuidFromRun(ITsString tss, int iRun, FwObjDataTypes[] desiredOrcTypes, out FwObjDataTypes odt, out TsRunInfo tri, ref ITsTextProps ttp)
		{
			tss.FetchRunInfo(iRun, out tri);
			if (tri.ichLim - tri.ichMin == 1 && tss.get_RunText(iRun)[0] == StringUtils.kChObject)
			{
				// determine if single-character run contains an ORC
				ttp = ttp ?? tss.get_Properties(iRun);
				return GetGuidFromProps(ttp, desiredOrcTypes, out odt);
			}
			odt = 0;
			return Guid.Empty;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get a "Hot" Guid from the given text props.
		/// </summary>
		/// <param name="ttp">The text props</param>
		/// <returns>The GUID from the text props or Guid.Empty if the props do not contain
		/// a GUID or have a non-hot ORC</returns>
		/// ------------------------------------------------------------------------------------
		public static Guid GetHotObjectGuidFromProps(ITsTextProps ttp)
		{
			FwObjDataTypes odt;
			return GetGuidFromProps(ttp, HotObjectTypes, out odt);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get a Guid from the given text props if the object type is a footnote or a picture.
		/// </summary>
		/// <param name="ttp">The text props</param>
		/// <returns>The GUID from the text props or Guid.Empty if the props do not contain
		/// a GUID or have a type of ORC that is not one of the desired kinds</returns>
		/// ------------------------------------------------------------------------------------
		public static Guid GetUsefulGuidFromProps(ITsTextProps ttp)
		{
			FwObjDataTypes odt;
			return GetGuidFromProps(ttp, FootnoteAndPicObjectTypes, out odt);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get a Guid from the given text props.
		/// </summary>
		/// <param name="ttp">The text props</param>
		/// <param name="desiredOrcTypes">Set of ORC types that we're interested in, dude; or
		/// null if it don't make no difference</param>
		/// <param name="odt">Actual object type</param>
		/// <returns>The GUID from the text props or Guid.Empty if the props do not contain
		/// a GUID or have a type of ORC that is not one of the desired kinds</returns>
		/// ------------------------------------------------------------------------------------
		public static Guid GetGuidFromProps(ITsTextProps ttp, FwObjDataTypes[] desiredOrcTypes, out FwObjDataTypes odt)
		{
			odt = 0;
			string sObjData = ttp.ObjData();

			if (sObjData != null)
			{
				odt = (FwObjDataTypes)Convert.ToByte(sObjData[0]);
				// See if it's one of the types of objects we want.
				if (desiredOrcTypes == null || desiredOrcTypes.Contains(odt))
				{
					// Get GUID for ORC
					return MiscUtils.GetGuidFromObjData(sObjData.Substring(1));
				}
			}
			return Guid.Empty;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// See if the string, typically obtained from a TsTextProps string property (int)FwTextPropType.ktptObjData,
		/// represents a hot link. If so, return the GUID of the object linked to.
		/// This overlaps some of the functionality of GetGuidFromProps and GetHotObjectGuidFromProps,
		/// for performance reasons.
		/// </summary>
		/// <param name="sObjData">The s obj data.</param>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public static Guid GetHotGuidFromObjData(string sObjData)
		{
			FwObjDataTypes odt = (FwObjDataTypes)Convert.ToByte(sObjData[0]);
			if (HotObjectTypes.Contains(odt))
			{
				return MiscUtils.GetGuidFromObjData(sObjData.Substring(1));
			}
			return Guid.Empty;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Replaces the requested run of the given builder with an identical ORC but whose type
		/// is un-owned rather than owned. If the given run is not an ORC at all or is not
		/// owned, do nothing.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void TurnOwnedOrcIntoUnownedOrc(ITsStrBldr bldr, int irun)
		{
			string value = bldr.get_Properties(irun).ObjData();
			if (!string.IsNullOrEmpty(value) && value[0] == (char)FwObjDataTypes.kodtOwnNameGuidHot)
			{
				TsRunInfo runInfo;
				bldr.FetchRunInfo(irun, out runInfo);
				bldr.SetStrPropValue(runInfo.ichMin, runInfo.ichLim, (int)FwTextPropType.ktptObjData, (char)FwObjDataTypes.kodtNameGuidHot + value.Substring(1));
			}
		}
		#endregion

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Writes an href element to the XML writer if the given string property is of the
		/// proper type.
		/// </summary>
		/// <param name="tpt">The FwTextPropType (passed as an int - grr).</param>
		/// <param name="sProp">The string property.</param>
		/// <param name="writer">The writer.</param>
		/// <returns><c>true</c> if the href attribute was written; <c>false</c> otherwise.
		/// </returns>
		/// ------------------------------------------------------------------------------------
		public static bool WriteHref(int tpt, string sProp, XmlWriter writer)
		{
			if (tpt != (int)FwTextPropType.ktptObjData)
				return false;

			string sRef = GetURL(sProp);
			if (String.IsNullOrEmpty(sRef))
				return false;
			writer.WriteAttributeString("href", sRef);
			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the URL information in a form suitable for writing out in an .
		/// </summary>
		/// <param name="sProp">The string property containing the URL info.</param>
		/// ------------------------------------------------------------------------------------
		public static string GetURL(string sProp)
		{
			if (!String.IsNullOrEmpty(sProp) && sProp[0] == Convert.ToChar((int)FwObjDataTypes.kodtExternalPathName))
			{
				string sRef = sProp.Substring(1);
				if (!String.IsNullOrEmpty(sRef))
				{
					Match matchURL = RegexUrl.Match(sRef);
					if (!matchURL.Success)
						sRef = "file://" + sRef.Replace('\\', '/');
					return sRef;
				}
			}
			return null;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Checks to see if the text properties are for a hyperlink.
		/// </summary>
		/// <param name="ttp">The text properties.</param>
		/// <returns><c>true</c> if the properties are for a hyperlink; <c>false</c> otherwise.
		/// </returns>
		/// ------------------------------------------------------------------------------------
		public static bool IsHyperlink(ITsTextProps ttp)
		{
			string objData = ttp.ObjData();
			if (!String.IsNullOrEmpty(objData) && objData.Length > 1 && objData[0] == Convert.ToChar((int)FwObjDataTypes.kodtExternalPathName))
			{
				return true;
			}
			return false;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the owned ORCs from the given ITsString.
		/// </summary>
		/// <param name="tss">The given ITsString.</param>
		/// <returns>ITsString of the ORCs in tss, if any.</returns>
		/// ------------------------------------------------------------------------------------
		public static ITsString GetOwnedORCs(ITsString tss)
		{
			ITsStrBldr tssBldr = MakeStrBldr();
			for (int iRun = 0; iRun < tss.RunCount; iRun++)
			{
				FwObjDataTypes objDataType;
				GetOwnedGuidFromRun(tss, iRun, out objDataType);
				if (objDataType == FwObjDataTypes.kodtOwnNameGuidHot || objDataType == FwObjDataTypes.kodtGuidMoveableObjDisp)
				{
					tssBldr.ReplaceRgch(tssBldr.Length, tssBldr.Length, tss.get_RunText(iRun), 1, tss.get_Properties(iRun));
				}
			}

			return tssBldr.GetString();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Removes the specified integer property from a Structured text string.
		/// </summary>
		/// <param name="tss">The structured text string.</param>
		/// <param name="intProp">integer property to remove from the tss.</param>
		/// <returns>tss without ORCs.</returns>
		/// ------------------------------------------------------------------------------------
		public static ITsString RemoveIntProp(ITsString tss, int intProp)
		{
			if (tss == null)
				return null;

			ITsStrBldr tssBldr = tss.GetBldr();
			// Note that the RunCount of the builder may CHANGE during this loop; don't cache the run count.
			for (int iRun = 0; iRun < tssBldr.RunCount; iRun++)
			{
				// Check the integer properties of each run.
				ITsTextProps tpp = tssBldr.get_PropertiesAt(iRun);

				for (int iProp = 0; iProp < tpp.IntPropCount; iProp++)
				{
					int var;
					int propType;
					tpp.GetIntProp(iProp, out propType, out var);
					if (propType == intProp)
					{
						ITsPropsBldr ttpBldr = tpp.GetBldr();
						// Remove integer property
						ttpBldr.SetIntPropValues(intProp, -1, -1);

						// Update the run using new properties.
						int ichMin, ichLim;
						tssBldr.GetBoundsOfRun(iRun, out ichMin, out ichLim);
						tssBldr.Replace(ichMin, ichLim, tssBldr.get_RunText(iRun), ttpBldr.GetTextProps());
					}
				}

			}
			return tssBldr.GetString();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets a TsString made from the ashes of the input parameter which has:
		/// 1) only one run
		/// 2) uses the ws of the 1st run of the input TsString.
		/// </summary>
		/// <param name="tss">structured text string</param>
		/// ------------------------------------------------------------------------------------
		public static ITsString GetCleanSingleRunTsString(ITsString tss)
		{
			return MakeString(tss.Text, tss.get_WritingSystem(0));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets a TsString with no: 1) ORCs corresponding to special runs, or 2) clipboard-
		/// style footnote or picture runs.
		/// </summary>
		/// <param name="tss">structured text string</param>
		/// ------------------------------------------------------------------------------------
		public static ITsString GetCleanTsString(ITsString tss)
		{
			return GetCleanTsString(tss, null, false, true, false);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets a TsString with no: 1) leading and trailing spaces, 2) ORCs, 3) clipboard-style
		/// footnote runs, or 4) runs having the specified character styles.
		/// </summary>
		/// <param name="tss">structured text string to clean up</param>
		/// <param name="stylesToRemove">List of styles to remove (null if no styles should be
		/// removed).</param>
		/// ------------------------------------------------------------------------------------
		public static ITsString GetCleanTsString(ITsString tss, IEnumerable<string> stylesToRemove)
		{
			return GetCleanTsString(tss, stylesToRemove, false, false, true);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets a TsString with no: 1) ORCs (except maybe unknown ones, if so requested),
		/// 3) clipboard-style footnote runs, or 4) runs having the specified character styles.
		/// Also, leading and trailing spaces are removed if requested.
		/// </summary>
		/// <param name="tss">structured text string to clean up</param>
		/// <param name="stylesToRemove">List of styles to remove (null if no styles should be
		/// removed).</param>
		/// <param name="fStopOnRemovedStyle">if set to <c>true</c> stop building the string
		/// when a style to remove is encountered and we have some text in the string;
		/// <c>false</c> to just skip stylesToRemove.</param>
		/// <param name="fPreserveUnknownOrcs"><c>true</c> to allow the returned string to
		/// contain ORC characters not associated with a special run.</param>
		/// <param name="fTrimSpaces">if set to <c>true</c> leading and trailing spaces are
		/// trimmed.</param>
		/// ------------------------------------------------------------------------------------
		public static ITsString GetCleanTsString(ITsString tss, IEnumerable<string> stylesToRemove, bool fStopOnRemovedStyle, bool fPreserveUnknownOrcs, bool fTrimSpaces)
		{
			if (tss == null)
				return null;

			ITsStrBldr tssBldr = MakeStrBldr();
			for (int iRun = 0; iRun < tss.RunCount; iRun++)
			{
				string runText = tss.get_RunText(iRun);
				// skip empty runs
				if (string.IsNullOrEmpty(runText))
					continue;

				// If run is an ORC, or a footnote, then skip it
				if (runText[0] == StringUtils.kChObject)
				{
					if (!fPreserveUnknownOrcs)
						continue;

					if (GetGuidFromRun(tss, iRun) != Guid.Empty)
						continue;
				}

				// Look for a footnote or picture run encoded with special formatting as
				// would be the case if this TsString came from a selection.
				ITsTextProps runProps = tss.get_Properties(iRun);
				if (!string.IsNullOrEmpty(runProps.ObjData()))
					continue;

				// Check for removed styles.
				if (stylesToRemove != null && stylesToRemove.Contains(runProps.Style()))
				{
					if (fStopOnRemovedStyle && tssBldr.Length > 0)
						break;
					continue;
				}

				// Trim the run, if needed
				if (fTrimSpaces)
				{
					if (iRun == 0)
						runText = runText.TrimStart();
					if (iRun == tss.RunCount - 1)
						runText = runText.TrimEnd();
				}

				// Append the text to the accumulated ITsString
				tssBldr.Replace(tssBldr.Length, tssBldr.Length, runText, runProps);
			}
			if (tssBldr.Length == 0)
			{
				// need to check for possibility of magic WS values - these are used for
				// added text like newline characters.
				if (GetWsOfRun(tss, 0) > 0)
					tssBldr.SetProperties(0, 0, PropsForWs(GetWsOfRun(tss, 0)));
				else
					return null;
			}

			return tssBldr.GetString();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get the text from a ITsString, removing ORCs and any text in the specified character
		/// styles. Also trims any non-wordforming characters from the beginning and the end of
		/// the text.
		/// </summary>
		/// <param name="tss">structured text string</param>
		/// <param name="stylesToRemove">List of styles to remove (null if no styles should be
		/// removed).</param>
		/// <param name="fStopOnRemovedStyle">if set to <c>true</c> stop building the string
		/// when a style to remove is encountered and we have some text in the string;
		/// <c>false</c> to just skip stylesToRemove.</param>
		/// <param name="writingSystemFactory">The writing system factory.</param>
		/// <returns>
		/// tss with ORCs and any text in the specified styles removed.
		/// </returns>
		/// ------------------------------------------------------------------------------------
		public static string GetCleanTextFromTsString(ITsString tss, IEnumerable<string> stylesToRemove, bool fStopOnRemovedStyle, ILgWritingSystemFactory writingSystemFactory)
		{
			// We have to get the result as a TsString because TrimNonWordFormingChars
			// needs a TsString to get the right CPEs to test for wordforming characters.
			ITsString newTss = GetCleanTsString(tss, stylesToRemove, fStopOnRemovedStyle, false, true);

			if (newTss == null)
				return null;
			// Trim the start and end of the string.
			string result = TrimNonWordFormingChars(newTss, writingSystemFactory).Text;
			if (result == null)
				return string.Empty;
			return result;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Split the ITsString into pieces separated by one of the strings in separator, and
		/// using the same options as String.Split().
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static List<ITsString> Split(ITsString tss, string[] separator, StringSplitOptions opt)
		{
			List<ITsString> rgtss = new List<ITsString>();
			if (tss == null || tss.Length == 0 || separator == null || separator.Length == 0)
			{
				rgtss.Add(tss);
			}

			else
			{
				int ich = 0;
				while (ich < tss.Length)
				{
					int cchMatch = 0;
					int ichEnd = tss.Text.IndexOf(separator[0], ich, StringComparison.Ordinal);
					if (ichEnd < 0)
						ichEnd = tss.Length;
					else
						cchMatch = separator[0].Length;
					for (int i = 1; i < separator.Length; ++i)
					{
						int ichEnd2 = tss.Text.IndexOf(separator[i], ich, StringComparison.Ordinal);
						if (ichEnd2 < 0)
							ichEnd2 = tss.Length;
						if (ichEnd2 < ichEnd)
						{
							ichEnd = ichEnd2;
							cchMatch = separator[i].Length;
						}
					}
					int length = ichEnd - ich;
					if (length > 0 || opt == StringSplitOptions.None)
						rgtss.Add(tss.Substring(ich, length));
					ich = ichEnd + cchMatch;
				}
			}
			return rgtss;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Trim the leading AND trailing non-word forming characters.
		/// </summary>
		/// <param name="untrimmedString">string that may contain non-word forming characters</param>
		/// <param name="writingSystemFactory">The ws factory used to get character properties</param>
		/// <returns>string with leading AND trailing non-word forming characters trimmed</returns>
		/// ------------------------------------------------------------------------------------
		public static ITsString TrimNonWordFormingChars(ITsString untrimmedString, ILgWritingSystemFactory writingSystemFactory)
		{
			return TrimNonWordFormingChars(untrimmedString, writingSystemFactory, true, true);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Trim the leading AND/OR trailing non-word forming characters.
		/// </summary>
		/// <param name="tssInput">string that may contain non-word forming characters</param>
		/// <param name="writingSystemFactory">The ws factory used to get character properties</param>
		/// <param name="fTrimLeading">if set to <c>true</c> trim leading characters.</param>
		/// <param name="fTrimTrailing">if set to <c>true</c> trim trailing characters.</param>
		/// <returns>
		/// string with leading or trailing non-word forming characters trimmed
		/// </returns>
		/// ------------------------------------------------------------------------------------
		public static ITsString TrimNonWordFormingChars(ITsString tssInput, ILgWritingSystemFactory writingSystemFactory, bool fTrimLeading, bool fTrimTrailing)
		{
			Debug.Assert(fTrimLeading || fTrimTrailing);

			if (tssInput == null)
				return null;
			if (tssInput.Length == 0)
				return tssInput;

			int ichMin;
			bool fFoundWordFormingChar = false;
			if (fTrimLeading)
			{
				// Trim leading non-word forming characters from string.
				ichMin = -1;
				for (int ich = 0; ich < tssInput.Length; ich++)
				{
					if (tssInput.IsCharWordForming(ich, writingSystemFactory))
					{
						// first word-forming character found
						ichMin = ich;
						fFoundWordFormingChar = true;
						break;
					}
				}
				if (ichMin == -1)
					return MakeString("", tssInput.get_WritingSystemAt(0));
				// no word-forming characters found in the string
			}

			else
				ichMin = 0;

			int strLength;
			if (fTrimTrailing)
			{
				// Trim trailing non-word forming characters from string.
				strLength = 1;
				int iMin = (ichMin == 0) ? -1 : ichMin;
				for (int ich = tssInput.Length - 1; ich > iMin; ich--)
				{
					if (tssInput.IsCharWordForming(ich, writingSystemFactory))
					{
						// last word-forming character found
						strLength = ich - ichMin + 1;
						fFoundWordFormingChar = true;
						break;
					}
				}

				if (!fFoundWordFormingChar)
					return MakeString("", GetWsAtOffset(tssInput, 0));
				// no word-forming characters found in the string
			}

			else
				strLength = tssInput.Length - ichMin;

			return tssInput.Substring(ichMin, strLength);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get the ws at the given (ich) char offset in the tss.
		/// </summary>
		/// <param name="tss">The TSS.</param>
		/// <param name="ich">char offset</param>
		/// ------------------------------------------------------------------------------------
		public static int GetWsAtOffset(ITsString tss, int ich)
		{
			return tss == null ? 0 : tss.get_WritingSystemAt(ich);
		}

		/// <summary>
		/// Gets the first vernacular ws.
		/// </summary>
		/// <param name="vernWsVecImage">The vern ws vec image, like "seh pt mar-fonipa".</param>
		/// <param name="wsf">The Writing System Factory.</param>
		/// <param name="text">The text.</param>
		/// <returns>
		/// The first vernacular ws used in the text or -1 if none
		/// </returns>
		public static int GetFirstVernacularWs(string vernWsVecImage, ILgWritingSystemFactory wsf, ITsString text)
		{
			int wid = -1; // writing system id
			for (int runSeq = 0; runSeq < text.RunCount; runSeq++)
			{
				wid = text.get_WritingSystem(runSeq);
				var ws = wsf.get_EngineOrNull(wid);
				if (ws != null)
				{   // ws.Id is short like "en"
					if (vernWsVecImage.IndexOf(ws.Id, StringComparison.Ordinal) >= 0)
						break; // wid > -1
				}
				wid = -1;
			}
			return wid;
		}

		/// <summary>
		/// Get the writing system of the specified run of a TsString.
		/// </summary>
		public static int GetWsOfRun(ITsString tss, int irun)
		{
			return tss.get_WritingSystem(irun);
		}

		/// <summary>
		/// Return a list of the writing systems found in the string.
		/// </summary>
		public static List<int> GetWritingSystems(ITsString tss)
		{
			List<int> result = new List<int>();
			result.Add(GetWsOfRun(tss, 0));
			int crun = tss.RunCount;
			for (int irun = 0; irun < crun; irun++)
			{
				int ws = GetWsOfRun(tss, irun);
				if (!result.Contains(ws))
					result.Add(ws);
			}
			return result;
		}

		/// <summary>
		/// Creates an empty <see cref="ITsString"/> with the specified writing system.
		/// </summary>
		public static ITsString EmptyString(int ws)
		{
			return TsStrFactory.EmptyString(ws);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates a new Tss based on the given string and ws.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static ITsString MakeString(string text, int ws)
		{
			return TsStrFactory.MakeString(text, ws);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates an ITsString in the given writing system and with the given style name.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static ITsString MakeString(string text, int ws, string styleName)
		{
			return TsStrFactory.MakeStringWithPropsRgch(text, text.Length, StyleUtils.CharStyleTextProps(styleName, ws));
		}

		/// <summary>
		/// Creates an <see cref="ITsString"/> with the specifed text properties.
		/// </summary>
		public static ITsString MakeString(string text, ITsTextProps textProps)
		{
			return TsStrFactory.MakeStringWithProps(text, textProps);
		}

		/// <summary>
		/// Creates an empty <see cref="ITsString"/> builder.
		/// </summary>
		public static ITsStrBldr MakeStrBldr()
		{
			return TsStrFactory.GetBldr();
		}

		/// <summary>
		/// Creates an empty incremental <see cref="ITsString"/> builder.
		/// </summary>
		public static ITsIncStrBldr MakeIncStrBldr()
		{
			return TsStrFactory.GetIncBldr();
		}

		/// <summary>
		/// Creates an <see cref="ITsTextProps"/> with the specified style and writing system.
		/// </summary>
		public static ITsTextProps MakeProps(string styleName, int ws)
		{
			return TsPropsFactory.MakeProps(styleName, ws, 0);
		}

		/// <summary>
		/// Creates an empty <see cref="ITsTextProps"/> builder.
		/// </summary>
		public static ITsPropsBldr MakePropsBldr()
		{
			return TsPropsFactory.GetPropsBldr();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Merges the string.
		/// </summary>
		/// <param name="source">The source.</param>
		/// <param name="dest">The dest.</param>
		/// ------------------------------------------------------------------------------------
		public static ITsString MergeString(ITsString source, ITsString dest)
		{
			return MergeString(source, dest, false);
		}

		/// <summary>
		/// Merges the string.
		/// </summary>
		/// <param name="source">The source.</param>
		/// <param name="dest">The dest.</param>
		/// <param name="fConcatenateIfBoth">If true, and if source and dest both have values that are not
		/// equal, concatenate source on end of dest. Otherwise ignore source if dest has a value.</param>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public static ITsString MergeString(ITsString source, ITsString dest, bool fConcatenateIfBoth)
		{
			string destText = (dest != null) ? dest.Text : null;
			string sourceText = (source != null) ? source.Text : null;

			if (string.IsNullOrEmpty(destText) && !string.IsNullOrEmpty(sourceText))
				return source;
			else if (fConcatenateIfBoth && !string.IsNullOrEmpty(destText) && !string.IsNullOrEmpty(sourceText) && !dest.Equals(source))
			{
				// concatenate
				ITsStrBldr tsb = dest.GetBldr();
				tsb.Replace(tsb.Length, tsb.Length, " ", null);
				tsb.ReplaceTsString(tsb.Length, tsb.Length, source);
				return tsb.GetString();
			}

			return dest;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Determine if the word form exists in the string, matching the whole word or
		/// sequence of words.
		/// </summary>
		/// <param name="wordFormTss">text of word form to look for</param>
		/// <param name="sourceTss">text to search in</param>
		/// <param name="wsf">source of char prop engines</param>
		/// <param name="ichMin">The start of the string where the text was found (undefined if
		/// this method returns false)</param>
		/// <param name="ichLim">The limit (one character position past the end) of the string
		/// where the text was found (undefined if this method returns false)</param>
		/// <returns><c>true</c> if word form is matched exactly (whole word found and matching
		/// case)
		/// </returns>
		/// <remarks>
		/// TODO: Add a parameter to make it possible to do a case-insensitive match?</remarks>
		/// ------------------------------------------------------------------------------------
		public static bool FindWordFormInString(ITsString wordFormTss, ITsString sourceTss, ILgWritingSystemFactory wsf, out int ichMin, out int ichLim)
		{
			return FindTextInString(wordFormTss, sourceTss, wsf, true, out ichMin, out ichLim);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Determine if the specified text exists in the string
		/// </summary>
		/// <param name="wordFormTss">text to look for</param>
		/// <param name="sourceTss">text to search in</param>
		/// <param name="wsf">source of char prop engines</param>
		/// <param name="fMatchWholeWord">True to match a whole word, false otherwise</param>
		/// <param name="ichMin">The start of the string where the text was found (undefined if
		/// this method returns false)</param>
		/// <param name="ichLim">The limit (one character position past the end) of the string
		/// where the text was found (undefined if this method returns false)</param>
		/// <returns><c>true</c> if the text is matched exactly (matching case)
		/// </returns>
		/// <remarks>
		/// TODO: Add a parameter to make it possible to do a case-insensitive match?</remarks>
		/// ------------------------------------------------------------------------------------
		public static bool FindTextInString(ITsString wordFormTss, ITsString sourceTss, ILgWritingSystemFactory wsf, bool fMatchWholeWord, out int ichMin, out int ichLim)
		{
			ichMin = ichLim = 0;

			if (wordFormTss == null || sourceTss == null || wordFormTss.Length == 0 || sourceTss.Length == 0)
			{
				// Nothing we can really do
				return false;
			}

			int ichWordForm = 0;
			bool fMatchInProgress = false;
			bool fWordFormAndSrcAreBothNormalized = wordFormTss.get_IsNormalizedForm(FwNormalizationMode.knmNFC) && sourceTss.get_IsNormalizedForm(FwNormalizationMode.knmNFC);
			bool fPrevCharWasWordForming = false;
			// Must use local temp variable because some callers pass same variable as both
			// ichMin and ichLim.
			int ichLimT;
			for (int ichSrc = 0; ichSrc < sourceTss.Length; ichSrc = ichLimT)
			{
				ichLimT = sourceTss.NextCharIndex(ichSrc);
				bool fWordForming = sourceTss.IsCharWordForming(ichSrc, wsf);
				if (!fMatchInProgress && fPrevCharWasWordForming && fWordForming && fMatchWholeWord)
					continue;

				fPrevCharWasWordForming = fWordForming;
				if (!fMatchInProgress && !fWordForming)
					continue;

				bool fMatch = wordFormTss.CharAt(ichWordForm) == sourceTss.CharAt(ichSrc);
				if (fMatch)
				{
					ichWordForm = wordFormTss.NextCharIndex(ichWordForm);
				}
				else
				{
					if (sourceTss.CharAt(ichSrc) == StringUtils.kChObject)
						fMatch = true;
					else if (!fWordFormAndSrcAreBothNormalized)
					{
						int ichWfCharLim = wordFormTss.NextCharIndex(ichWordForm);

						while (ichWfCharLim < wordFormTss.Length && !Character.IsLetter(wordFormTss.CharAt(ichWfCharLim)) && wordFormTss.IsCharWordForming(ichWfCharLim, wsf))
						{
							ichWfCharLim = wordFormTss.NextCharIndex(ichWfCharLim);
						}

						while (ichLimT < sourceTss.Length && !Character.IsLetter(sourceTss.CharAt(ichLimT)) && sourceTss.IsCharWordForming(ichLimT, wsf))
						{
							ichLimT = sourceTss.NextCharIndex(ichLimT);
						}

						if (wordFormTss.Text.Substring(ichWordForm, ichWfCharLim - ichWordForm).Normalize() == sourceTss.Text.Substring(ichSrc, ichLimT - ichSrc).Normalize())
						{
							ichWordForm = ichWfCharLim;
							fMatch = true;
						}
					}
				}

				if (fMatch)
				{
					// If this character is the start of a match but the previous character was
					// word-forming, then this is a bogus match, so we keep looking.
					if (!fMatchInProgress)
					{
						if (ichSrc == 0 || !sourceTss.IsCharWordForming(sourceTss.PrevCharIndex(ichSrc), wsf) || !fMatchWholeWord)
						{
							ichMin = ichSrc;
							fMatchInProgress = true;
						}

						else
							ichWordForm = 0;
					}

					if (fMatchInProgress && ichWordForm == wordFormTss.Length)
					{
						if (++ichSrc < sourceTss.Length && sourceTss.IsCharWordForming(ichSrc, wsf) && fMatchWholeWord)
						{
							ichWordForm = 0;
							fMatchInProgress = false;
						}

						else
						{
							ichLim = ichLimT;
							return true;
						}
					}
				}

				else
				{
					ichWordForm = 0;
					fMatchInProgress = false;
				}
			}
			return false;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initialize the ICU Data directory based on the ICU data directory.  It is
		/// safe to call this static method more than once, but necessary only to call it at
		/// least once.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void InitIcuDataDir()
		{
			CustomIcu.InitIcuDataDir();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Normalize the string for display.  Uniscribe requires NFC.
		/// (See LT-4202 and LT-4203.)
		/// </summary>
		/// <param name="s"></param>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public static string NormalizeToNFC(string s)
		{
			return Normalizer.Normalize(s, Normalizer.UNormalizationMode.UNORM_NFC);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Reverse the order of characters within the string (make a mirror image of it).  The
		/// tricky part, as always with UTF-16, is handling surrogate pairs correctly.
		/// TODO: Ideally this method should be in SilUtils.StringUtils. However, this method
		/// depends on the Surrogates class which is defined in BasicUtils which we didn't
		/// want to move just for this one method.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string ReverseString(string s)
		{
			if (s == null)
				return "";
			char[] rgch = s.ToCharArray();
			int ia;
			int iz;
			char chT;
			// First flip all the chars as quickly as possible.
			for (ia = 0, iz = rgch.Length - 1; ia < iz; ++ia, --iz)
			{
				chT = rgch[ia];
				rgch[ia] = rgch[iz];
				rgch[iz] = chT;
			}
			// Then fix up any surrogates that got scrambled.
			for (ia = 0, iz = rgch.Length - 2; ia < iz; ++ia)
			{
				if (Surrogates.IsTrailSurrogate(rgch[ia]))
				{
					chT = rgch[ia];
					rgch[ia] = rgch[ia + 1];
					rgch[ia++] = chT;
				}
			}

			return new string(rgch);
		}

		/// <summary>
		/// Answer a TsTextProps that specifies the given writing system (all other props have default values).
		/// </summary>
		/// <param name="ws"></param>
		/// <returns></returns>
		public static ITsTextProps PropsForWs(int ws)
		{
			ITsPropsBldr tpb = MakePropsBldr();
			tpb.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, ws);
			return tpb.GetTextProps();
		}

		/// <summary>
		/// Get an indication of the range of characters that differ between two TsStrings.
		/// Return null if they are equal.
		/// If they are not, return a TsStringDiffInfo with the index and count of changed characters
		/// as well as a flag indicating whether we need to adjust analyses.
		/// Character properties as well as values are considered.
		/// If it is ambiguous where the difference occurs, we first try to interpret the difference in a way
		/// that equates to inserting or deleting a complete word, otherwise, prefer to find the longest common
		/// string at the start.
		/// </summary>
		[SuppressMessage("ReSharper", "PossibleNullReferenceException",
			Justification = "If a tss is null, crunBoth will be 0, so we'll never try to access it.")]
		public static TsStringDiffInfo GetDiffsInTsStrings(ITsString tssOld, ITsString tssNew)
		{
			// no diff found
			var ichMin = -1;

			int cchOld = 0;
			int crunOld = 0;
			int crunNew = 0;
			int cchNew = 0;
			if (tssNew != null)
			{
				cchNew = tssNew.Length;
				crunNew = tssNew.RunCount;
			}
			if (tssOld != null)
			{
				cchOld = tssOld.Length;
				crunOld = tssOld.RunCount;
			}
			var crunBoth = Math.Min(crunOld, crunNew);

			// Set ichMin to the index of the first character that is different or has different properties.
			for (var irun = 0; irun < crunBoth; irun++)
			{
				if (tssOld.get_Properties(irun) != tssNew.get_Properties(irun))
				{
					// the first difference is at the start of this run.
					ichMin = tssNew.get_MinOfRun(irun);
					break;
				}
				var ichMinRun = StringUtils.FirstDiff(tssOld.get_RunText(irun), tssNew.get_RunText(irun));
				if (ichMinRun >= 0)
				{
					ichMin = tssNew.get_MinOfRun(irun) + ichMinRun;
					break;
				}
			}
			if (ichMin < 0)
			{
				// no difference found as far as crunBoth.
				if (crunNew > crunBoth)
				{
					// All the additional length of tssNew is inserted.
					return new TsStringDiffInfo(cchOld, cchNew - cchOld, 0);
				}
				if (crunOld > crunBoth)
				{
					// All the additional length of tssOld is deleted.
					return new TsStringDiffInfo(cchNew, 0, cchOld - cchNew);
				}
				// same number of runs are identical; strings are equal
				return null;
			}
			// There is a difference at ichMin.
			// A default assumption is that the remainder of both strings differs.
			var irunOld = crunOld - 1;
			var irunNew = crunNew - 1;
			var cchSameEnd = 0;
			for (; irunOld >= 0 && irunNew >= 0; irunOld--, irunNew--)
			{
				if (tssOld.get_Properties(irunOld) != tssNew.get_Properties(irunNew))
				{
					// difference at end of this run. All the text beyond this run (if any) must have matched.
					break;
				}
				var ichLastDiffInRun = StringUtils.LastDiff(tssOld.get_RunText(irunOld), tssNew.get_RunText(irunNew));
				if (ichLastDiffInRun >= 0)
				{
					// End of equal bit is cchSame from the ends of the two runs
					cchSameEnd = cchOld - tssOld.get_LimOfRun(irunOld) + ichLastDiffInRun;
					break;
				}
				// Same to start of this run.
				cchSameEnd = cchOld - tssOld.get_MinOfRun(irunOld);
			}
			var cvIns = cchNew - ichMin - cchSameEnd;
			var cvDel = cchOld - ichMin - cchSameEnd;
			var fAdjustAnalyses = true;
			// It's possible, e.g., with "abc. def." and "abc. insert. def.", that the matching range we find
			// starting from the end overlaps the matching range we find starting at the start (we find a match
			// at the start up to the end of "abc. ", and from the end to the start of ". def").
			// In such a case, it's ambiguous where the actual difference is (we might have inserted either
			// "insert. " or ". insert"), but we definitely don't want to return any negative numbers,
			// and we choose to make the longer match at the start.
			// On the other hand, if the two strings are "xxxabc xxxdef" and "xxxdef", we could have deleted
			// "xxxabc " or "abc xxx", and we'd prefer the first interpretation, so we choose the longer match at the end.
			ITsString longerString = null;
			// only used and valid if cvIns or cvDel < 0
			var offsetIchMin = 0;
			if (cvIns < 0)
			{
				offsetIchMin = cvIns;
				cvDel -= cvIns;
				cvIns = 0;
				longerString = tssOld;
			}
			if (cvDel < 0)
			{
				offsetIchMin = cvDel;
				cvIns -= cvDel;
				cvDel = 0;
				longerString = tssNew;
			}
			if (longerString != null)
			{
				// See if there is white space at the end of the bit that would be treated as
				// deleted if we adjusted ichMin. If so, do it....
				var ichLookForSpace = ichMin + offsetIchMin + Math.Max(cvIns, cvDel) - 1;
				if (Character.IsSeparator(longerString.GetChars(ichLookForSpace, ichLookForSpace + 1)[0]))
				{
					// ...unless it would ALSO be a whole word delete if we did not change ichMin
					if (!Character.IsSeparator(longerString.GetChars(ichLookForSpace - offsetIchMin, ichLookForSpace - offsetIchMin + 1)[0]))

						ichMin += offsetIchMin;
				}
			}
			else if (cvIns == cvDel && crunNew == crunOld)
			{
				// Determine whether the change requires an analysis adjustment
				for (var iRun = 0; iRun < crunNew; iRun++)
				{
					if (ChangesRequireAnalysisAdjustment(tssOld, tssNew, iRun))
					{
						return new TsStringDiffInfo(ichMin, cvIns, cvDel, true);
					}
				}
				fAdjustAnalyses = false;
			}
			return new TsStringDiffInfo(ichMin, cvIns, cvDel, fAdjustAnalyses);
		}

		/// <summary>
		/// Determines whether the change requires us to adjust analyses.
		/// We force analysis adjustment for changes in run text and ktptObjData.
		///
		/// The ktptObjData string property could be a footnote owner, a picture, or one of many other things;
		/// if it has changed, we must adjust analyses.
		/// If a user updates a Writing System code, adjusting analyses at the wrong time will invalidate the user's work and duplicate all analyses.
		/// if the Writing System code is the only change, we must *not* adjust analyses during the change, because this will invalidate
		/// the user's analyses and create duplicates; someone else will notice any needed adjustments later. (https://jira.sil.org/browse/LT-20344)
		/// For all other property changes (most are style changes), it doesn't matter, and it's easier not to adjust analyses.
		/// </summary>
		internal static bool ChangesRequireAnalysisAdjustment(ITsString tss1, ITsString tss2, int iRun)
		{
			if (!Equals(tss1.get_RunText(iRun), tss2.get_RunText(iRun)))
			{
				return true;
			}

			var objData1 = tss1.get_StringProperty(iRun, (int) FwTextPropType.ktptObjData);
			var objData2 = tss2.get_StringProperty(iRun, (int) FwTextPropType.ktptObjData);
			return !Equals(objData1, objData2);
		}

		/// <summary>
		/// Remove characters that are illegal in XML files. Returns the input string if nothing is wrong with it.
		/// </summary>
		/// <param name="input"></param>
		/// <returns></returns>
		public static ITsString RemoveIllegalXmlChars(ITsString input)
		{

			for (var result = input;;)
			{
				var modified = RemoveOneIllegalXmlCharacter(result);
				if (modified == result)
					return result; // nothing (more) to remove
				result = modified;
			}
		}

		/// <summary>
		/// Remove one illegal character, if one is found; otherwise, return the original string.
		/// </summary>
		/// <param name="input"></param>
		/// <returns></returns>
		private static ITsString RemoveOneIllegalXmlCharacter(ITsString input)
		{
			var text = input.Text;
			if (text == null)
				return input;
			for (int i = 0; i < text.Length; i++)
			{
				if (IsIllegalXmlCharacter(text, i))
					return input.Remove(i, 1);
			}
			return input;
		}

		/// <summary>
		/// See http://www.w3.org/TR/REC-xml/#charsets
		/// Also detects unmatched surrogates
		/// </summary>
		/// <param name="text"></param>
		/// <param name="index"></param>
		/// <returns></returns>
		private static bool IsIllegalXmlCharacter(string text, int index)
		{
			var ch = text[index];
			if (ch < '\x9' || (ch > '\xa' && ch < '\xd') || (ch > '\xd' && ch < '\x20') || ch > '\xfffd')
				return true;
			if (Surrogates.IsLeadSurrogate(ch))
			{
				if (index + 1 == text.Length)
					return true;
				if (!Surrogates.IsTrailSurrogate(text[index + 1]))
					return true;
			}
			if (Surrogates.IsTrailSurrogate(ch))
			{
				if (index == 0)
					return true;
				if (!Surrogates.IsLeadSurrogate(text[index - 1]))
					return true;
			}
			return false;
		}

		/// <summary>
		/// Mimics the String.IsNullOrEmpty method for ITsString
		/// </summary>
		public static bool IsNullOrEmpty(ITsString testMe)
		{
			return testMe == null || testMe.Length <= 0;
		}
	}
	#endregion

	#region TsStringDifference class
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Represents a difference detected between two TsStrings
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public sealed class TsStringDiffInfo
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// The character position of the first difference between the two TsStrings
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public readonly int IchFirstDiff;
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// The number of characters that were inserted starting at the location of the
		/// first difference
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public readonly int CchInsert;
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// The number of characters that were deleted starting at the location of the
		/// first difference
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public readonly int CchDeleteFromOld;
		/// <summary>
		/// Whether we need to adjust analyses for this change (true unless we're sure we don't).
		/// See comments on <see cref="TsStringUtils.ChangesRequireAnalysisAdjustment"/>
		/// </summary>
		public readonly bool FAdjustAnalyses;

		/// <param name="ichFirstDiff">The character position of the first difference between
		/// the two TsStrings</param>
		/// <param name="cchInsert">The number of characters that were inserted starting at the
		/// location of the first difference</param>
		/// <param name="cchDeleteFromOld">The number of characters that were deleted starting
		/// at the location of the first difference</param>
		/// <param name="fAdjustAnalyses">True we should adjust analyses (default: true)</param>
		public TsStringDiffInfo(int ichFirstDiff, int cchInsert, int cchDeleteFromOld, bool fAdjustAnalyses)
		{
			IchFirstDiff = ichFirstDiff;
			CchInsert = cchInsert;
			CchDeleteFromOld = cchDeleteFromOld;
			FAdjustAnalyses = fAdjustAnalyses;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="TsStringDiffInfo"/> class.
		/// </summary>
		/// <param name="ichFirstDiff">The character position of the first difference between
		/// the two TsStrings</param>
		/// <param name="cchInsert">The number of characters that were inserted starting at the
		/// location of the first difference</param>
		/// <param name="cchDeleteFromOld">The number of characters that were deleted starting
		/// at the location of the first difference</param>
		/// ------------------------------------------------------------------------------------
		public TsStringDiffInfo(int ichFirstDiff, int cchInsert, int cchDeleteFromOld)
		{
			IchFirstDiff = ichFirstDiff;
			CchInsert = cchInsert;
			CchDeleteFromOld = cchDeleteFromOld;
			FAdjustAnalyses = true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="TsStringDiffInfo"/> class representing
		/// no change.
		/// </summary>
		/// <param name="ichFirstDiff">The character position of the first difference between
		/// the two TsStrings</param>
		/// ------------------------------------------------------------------------------------
		public TsStringDiffInfo(int ichFirstDiff)
		{
			IchFirstDiff = ichFirstDiff;
			CchInsert = CchDeleteFromOld = 0;
			FAdjustAnalyses = true;
		}
	}
	#endregion
}