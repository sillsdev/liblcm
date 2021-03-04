// Copyright (c) 2011-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Icu;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Utils;

namespace SIL.LCModel.Core.Text
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// TextExtensions is a collection of extension methods for use in LCM. Includes methods
	/// for working with COM interfaces defined in KernelInterfaces.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public static class TextExtensions
	{
		#region Extensions for TsStrings
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets all embedded object guids.
		/// </summary>
		/// <param name="tss">The TS String.</param>
		/// <param name="desiredOrcType">Type of the desired ORC.</param>
		/// <returns>Enumeration of GUIDs for all ORC runs of the requested type</returns>
		/// ------------------------------------------------------------------------------------
		public static IEnumerable<Guid> GetAllEmbeddedObjectGuids(this ITsString tss, FwObjDataTypes desiredOrcType)
		{
			for (int iRun = 0; iRun < tss.RunCount; iRun++)
			{
				Guid guid = TsStringUtils.GetGuidFromRun(tss, iRun, desiredOrcType);
				if (guid != Guid.Empty)
					yield return guid;
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Return the string that results from removing length characters at position ichMin from tss.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static ITsString Remove(this ITsString tss, int startIndex, int length)
		{
			int cch = tss.Length;
			Debug.Assert(startIndex + length <= cch);
			ITsStrBldr bldr = tss.GetBldr();
			bldr.Replace(startIndex, startIndex + length, "", null);
			return bldr.GetString();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Insert the specified string into the first argument/recipient at the specified position.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static ITsString Insert(this ITsString tss, int startIndex, ITsString tssInsert)
		{
			int cch = tss.Length;
			Debug.Assert(startIndex >= 0 && startIndex <= cch);
			if (tssInsert.Length == 0)
				return tss;
			ITsStrBldr bldr = tss.GetBldr();
			bldr.ReplaceTsString(startIndex, startIndex, tssInsert);
			return bldr.GetString();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Replaces the specified portion of the original string with the new string.
		/// </summary>
		/// <param name="tss">The original string.</param>
		/// <param name="startIndex">The start index.</param>
		/// <param name="length">The length.</param>
		/// <param name="tssReplace">The new string.</param>
		/// <returns>The new string with the requested replacements made</returns>
		/// ------------------------------------------------------------------------------------
		public static ITsString Replace(this ITsString tss, int startIndex, int length, ITsString tssReplace)
		{
			int cch = tss.Length;
			Debug.Assert(startIndex >= 0 && startIndex + length <= cch);
			ITsStrBldr bldr = tss.GetBldr();
			bldr.ReplaceTsString(startIndex, startIndex + length, tssReplace);
			return bldr.GetString();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Extract the specified substring from the ITsString. (Returns null if input is null.)
		/// If length is too large, return all of the string from ichMin on.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static ITsString Substring(this ITsString tss, int ichMin, int length1)
		{
			return tss == null ? null : tss.GetSubstring(ichMin, ichMin + length1);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Extract the specified substring from the ITsString, from ichMin on.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static ITsString Substring(this ITsString tss, int ichMin)
		{
			return tss == null ? null : tss.GetSubstring(ichMin, tss.Length);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Return a string that can be stored in a MultiUnicodeAccessor, where the only properties
		/// allowed are the writing system.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static ITsString ToWsOnlyString(this ITsString tss)
		{
			var props = tss.get_Properties(0);
			if (tss.RunCount == 1 && props.IntPropCount == 1 && props.StrPropCount == 0)
				return tss; // assume the one property is the writing system.
			int dummy;
			var ws = props.GetIntPropValues((int)FwTextPropType.ktptWs, out dummy);
			var text = tss.Text;
			if (ws != -1)
			{
				return TsStringUtils.MakeString(text, ws);
			}
			// pathologically, the string may include a leading newline where no WS is specified.
			// Rather than fail, just drop any part of the string that has no WS. Typically we drop at
			// most a leading newline, which is also not appropriate in this kind of field anyway.
			for (int i = 1; i < tss.Length; i++)
			{
				ws = tss.get_WritingSystemAt(i);
				if (ws != -1)
					return TsStringUtils.MakeString(text.Substring(i), ws);
			}
			// This probably never happens.
			Debug.Assert(false, "trying to paste a TsString that has no WS anywhere!");
			return null; // May well crash, but what can we do? This method has no access to any valid WS.
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Given a TS string and an index, find the closest start of a word before that
		/// position (or after that position if at a word boundary already or in the last word
		/// of the string. If in a run marked using one of the special styles, always returns
		/// the position at the end of that run. Runs having these special styles are also
		/// always regarded as word boundaries.
		/// </summary>
		/// <param name="tss">the structured string of the paragraph or translation</param>
		/// <param name="ich">the given index</param>
		/// <param name="specialStyles">The special styles.</param>
		/// <returns>adjusted character index</returns>
		/// ------------------------------------------------------------------------------------
		public static int FindWordBoundary(this ITsString tss, int ich, params string[] specialStyles)
		{
			if (ich < 0 || ich > tss.Length)
				throw new ArgumentOutOfRangeException("ich");

			if (ich == 0 || ich == tss.Length)
				return ich;

			string text = tss.Text;
			string startingStyle = tss.StyleAt(ich);
			string prevStyle = ich > 0 ? tss.StyleAt(ich - 1) : startingStyle;
			if (!specialStyles.Contains(startingStyle) || prevStyle == null)
				startingStyle = null;
			else if (startingStyle != null)
				startingStyle = prevStyle;

			// Advance to the next word boundary if appropriate)
			while (ich < text.Length)
			{
				// if the current character is space...
				if (Character.IsSeparator(text[ich]))
					ich++;
				else if (Character.IsPunct(text[ich]) && ich > 0 && !Character.IsSeparator(text[ich - 1]))
				{
					// if word-final punctuation advance
					ich++;
				}
				else if (startingStyle != null && tss.StyleAt(ich) == startingStyle)
					ich++;
				else
					break;
			}

			// NEVER move backward if at the end of the paragraph.
			if (ich < text.Length)
			{
				// While the insertion point is in the middle of a word then back up to the
				// start of the word or the start of a paragraph.
				while (ich > 0 && !Character.IsSeparator(text[ich - 1]) && !specialStyles.Contains(tss.StyleAt(ich - 1)))
				{
					ich--;
				}
			}

			return ich;
		}

		/// <summary>
		/// Concatenate the two strings. If there is no white space at the end of the first or the start of the second,
		/// and neither is all-white, add some.
		/// Enhance JohnT: this cannot consider any special white space characters unique to a particular
		/// writing system, and does not allow for the possibility of white space surrogate pairs, since
		/// I don't believe there are any in Unicode. Don't use where absolute consistency with a particular
		/// writing system is vital.
		/// </summary>
		public static ITsString ConcatenateWithSpaceIfNeeded(this ITsString first, ITsString second)
		{
			if (first.Length == 0)
				return second;
			if (second.Length == 0)
				return first;

			ITsStrBldr tsb = first.GetBldr();
			if (!(IsWhite(second.Text[0]) || IsWhite(first.Text.Last())))
				tsb.Replace(first.Length, first.Length, " ", null);

			tsb.ReplaceTsString(tsb.Length, tsb.Length, second);

			return tsb.GetString();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets an enumeration for all of the runs of this TsString
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static IEnumerable<TsRunPart> Runs(this ITsString tss)
		{
			for (int i = 0; i < tss.RunCount; i++)
				yield return new TsRunPart(tss, i);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Breaks a TsString into words where a word is defined as separated by whitespace or
		/// by run breaks, whichever is shorter.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static IEnumerable<TsRunPart> Words(this ITsString tss)
		{
			return tss.Runs().SelectMany(run => run.Words());
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Returns the last word in this TsString where a word is defined as separated by
		/// whitespace or run breaks, whichever is shorter.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static TsRunPart LastWord(this ITsString tss)
		{
			for (int irun = tss.RunCount - 1; irun >= 0; irun--)
			{
				TsRunPart lastWord = (new TsRunPart(tss, irun)).LastWord();
				if (lastWord != null)
					return lastWord;
			}
			return null;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Check the given text props to see if they specify the given style
		/// </summary>
		/// <param name="tss">The string containing the run.</param>
		/// <param name="iRun">The run from the string.</param>
		/// <param name="sStyle">Style</param>
		/// <returns>
		/// true if the given text at the specified run uses the given named style
		/// </returns>
		/// ------------------------------------------------------------------------------------
		public static bool IsStyle(this ITsString tss, int iRun, string sStyle)
		{
			return (tss.Style(iRun) == sStyle);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get the named style from the specified run of the given TS String.
		/// </summary>
		/// <param name="tss">The string</param>
		/// <param name="iRun">The run index.</param>
		/// <returns>the named style or <c>null</c> if the run doesn't have a style</returns>
		/// ------------------------------------------------------------------------------------
		public static string Style(this ITsString tss, int iRun)
		{
			return tss.get_StringProperty(iRun, (int)FwTextPropType.ktptNamedStyle);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get the named style from the specified character position of the given TS String. If
		/// the position is right between two runs, this gets the style of the following run.
		/// </summary>
		/// <param name="tss">The string</param>
		/// <param name="ich">The character index.</param>
		/// <returns>the named style or <c>null</c> if the run at ich doesn't have a style</returns>
		/// ------------------------------------------------------------------------------------
		public static string StyleAt(this ITsString tss, int ich)
		{
			return tss.get_StringPropertyAt(ich, (int)FwTextPropType.ktptNamedStyle);
		}

		/// <summary>
		/// Gets the string property value.
		/// </summary>
		public static string get_StringProperty(this ITsString tss, int iRun, int tpt)
		{
			return tss.get_Properties(iRun).GetStrPropValue(tpt);
		}

		/// <summary>
		/// Gets the string property value at the specified character offset.
		/// </summary>
		public static string get_StringPropertyAt(this ITsString tss, int ich, int tpt)
		{
			return tss.get_StringProperty(tss.get_RunAt(ich), tpt);
		}

		/// <summary>
		/// Gets the writing system.
		/// </summary>
		public static int get_WritingSystem(this ITsString tss, int irun)
		{
			int var;
			return tss.get_Properties(irun).GetIntPropValues((int) FwTextPropType.ktptWs, out var);
		}

		/// <summary>
		/// Gets the writing system at the specified character offset.
		/// </summary>
		public static int get_WritingSystemAt(this ITsString tss, int ich)
		{
			return tss.get_WritingSystem(tss.get_RunAt(ich));
		}

		/// <summary>
		/// Determines if the specified run is an ORC character.
		/// </summary>
		public static bool get_IsRunOrc(this ITsString tss, int iRun)
		{
			return tss.get_RunText(iRun) == "\ufffc";
		}

		/// <summary>
		/// Determines whether the character at the specified index in the string is wordforming.
		/// </summary>
		public static bool IsCharWordForming(this ITsString tss, int ich, ILgWritingSystemFactory wsf)
		{
			if (ich < 0 || ich >= tss.Length)
				throw new ArgumentOutOfRangeException("ich");

			if (char.IsLowSurrogate(tss.Text[ich]))
				--ich;
			int wsHandle = tss.get_WritingSystemAt(ich);
			int ch = char.ConvertToUtf32(tss.Text, ich);
			return TsStringUtils.IsWordForming(ch, wsf, wsHandle);
		}

		/// <summary>
		/// Gets the UTF-32 character at the specified index.
		/// </summary>
		public static int CharAt(this ITsString tss, int ich)
		{
			if (ich < 0 || ich >= tss.Length)
				throw new ArgumentOutOfRangeException("ich");

			return char.ConvertToUtf32(tss.Text, ich);
		}

		/// <summary>
		/// Gets the index of the next full character.
		/// </summary>
		public static int NextCharIndex(this ITsString tss, int ich)
		{
			if (ich < 0 || ich >= tss.Length)
				throw new ArgumentOutOfRangeException("ich");

			return Surrogates.NextChar(tss.Text, ich);
		}

		/// <summary>
		/// Gets the index of the previous full character.
		/// </summary>
		public static int PrevCharIndex(this ITsString tss, int ich)
		{
			if (ich < 0 || ich >= tss.Length)
				throw new ArgumentOutOfRangeException("ich");

			return Surrogates.PrevChar(tss.Text, ich);
		}
		#endregion

		#region Extensions for TsStrBldr
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Appends the specified text to the TS string builder.
		/// </summary>
		/// <param name="bldr">TS string builder.</param>
		/// <param name="textToAppend">The text to append.</param>
		/// <param name="ttp">The properties to apply.</param>
		/// ------------------------------------------------------------------------------------
		public static ITsStrBldr Append(this ITsStrBldr bldr, string textToAppend, ITsTextProps ttp)
		{
			var length = bldr.Length;
			bldr.Replace(length, length, textToAppend, ttp);
			return bldr;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Appends the specified text to the TS string builder.
		/// </summary>
		/// <param name="bldr">TS string builder.</param>
		/// <param name="textToAppend">The text to append.</param>
		/// <param name="ws">The writing system to apply.</param>
		/// ------------------------------------------------------------------------------------
		public static ITsStrBldr Append(this ITsStrBldr bldr, string textToAppend, int ws)
		{
			var length = bldr.Length;
			bldr.ReplaceTsString(length, length, TsStringUtils.MakeString(textToAppend, ws));
			return bldr;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Appends the specified text to the TS string builder.
		/// </summary>
		/// <param name="bldr">TS string builder.</param>
		/// <param name="tssToAppend">The ITsString to append.</param>
		/// ------------------------------------------------------------------------------------
		public static ITsStrBldr Append(this ITsStrBldr bldr, ITsString tssToAppend)
		{
			var length = bldr.Length;
			bldr.ReplaceTsString(length, length, tssToAppend);
			return bldr;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Appends the specified text to the TS string builder.
		/// </summary>
		/// <param name="bldr">TS string builder.</param>
		/// <param name="guid">The GUID representing the embedded object</param>
		/// <param name="objDataType">The type of embedding</param>
		/// <param name="ws">The ID of the writing system to use for the inserted run, or 0
		/// to leave unspecified</param>
		/// ------------------------------------------------------------------------------------
		public static ITsStrBldr AppendOrc(this ITsStrBldr bldr, Guid guid, FwObjDataTypes objDataType, int ws)
		{
			TsStringUtils.InsertOrcIntoPara(guid, objDataType, bldr, bldr.Length, bldr.Length, ws);
			return bldr;
		}
		#endregion

		#region Extensions for TsTextProps

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get the named style (FwTextPropType.ktptNamedStyle) from the specified text props
		/// </summary>
		/// <param name="ttp">Text props</param>
		/// <returns>the named style or <c>null</c> if the props don't specify a style</returns>
		/// ------------------------------------------------------------------------------------
		public static string Style(this ITsTextProps ttp)
		{
			return ttp.GetStrPropValue((int)FwTextPropType.ktptNamedStyle);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Check the given text props to see if they specify the given style
		/// </summary>
		/// <param name="ttp">Text props</param>
		/// <param name="sStyle">Style</param>
		/// <returns>true if the given text props use the given named style; false otherwise</returns>
		/// ------------------------------------------------------------------------------------
		public static bool IsStyle(this ITsTextProps ttp, string sStyle)
		{
			return (ttp.Style() == sStyle);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Returns the object data (FwTextPropType.ktptObjData) from the specified text props
		/// </summary>
		/// <param name="ttp">Text props</param>
		/// <returns>The object data or <c>null</c> if the props don't specify object data</returns>
		/// ------------------------------------------------------------------------------------
		public static string ObjData(this ITsTextProps ttp)
		{
			return ttp.GetStrPropValue((int)FwTextPropType.ktptObjData);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Returns the writing system (FwTextPropType.ktptWs) of the specified text props
		/// </summary>
		/// <param name="charStyleTextProps">Text props</param>
		/// <returns>The writing system value</returns>
		/// ------------------------------------------------------------------------------------
		public static int GetWs(this ITsTextProps charStyleTextProps)
		{
			int dummy;
			return charStyleTextProps.GetIntPropValues((int)FwTextPropType.ktptWs, out dummy);
		}


		/// <summary>
		/// Attempts to get the value of the specified int property.
		/// </summary>
		public static bool TryGetIntValue(this ITsTextProps textProps, FwTextPropType tpt, out FwTextPropVar var, out int value)
		{
			int v;
			value = textProps.GetIntPropValues((int) tpt, out v);
			if (v == -1 && value == -1)
			{
				var = FwTextPropVar.ktpvDefault;
				return false;
			}

			var = (FwTextPropVar) v;
			return true;
		}

		/// <summary>
		/// Gets the int property at the specified index.
		/// </summary>
		public static int GetIntProperty(this ITsTextProps textProps, int index, out FwTextPropType tpt, out FwTextPropVar var)
		{
			int t, v;
			int value = textProps.GetIntProp(index, out t, out v);
			tpt = (FwTextPropType) t;
			var = (FwTextPropVar) v;
			return value;
		}

		/// <summary>
		/// Attempts to get the value of the specified string property.
		/// </summary>
		public static bool TryGetStringValue(this ITsTextProps textProps, FwTextPropType tpt, out string value)
		{
			value = textProps.GetStrPropValue((int) tpt);
			return value != null;
		}

		/// <summary>
		/// Gets the string property at the specified index.
		/// </summary>
		public static string GetStringProperty(this ITsTextProps textProps, int index, out FwTextPropType tpt)
		{
			int t;
			string value = textProps.GetStrProp(index, out t);
			tpt = (FwTextPropType) t;
			return value;
		}
		#endregion

		#region ITsStrFactory methods

		/// <summary>
		/// Creates an <see cref="ITsString"/> with the specified text and properties.
		/// </summary>
		public static ITsString MakeStringWithProps(this ITsStrFactory tsf, string text, ITsTextProps textProps)
		{
			return tsf.MakeStringWithPropsRgch(text, text == null ? 0 : text.Length, textProps);
		}

		#endregion

		#region ITsPropsBldr methods

		/// <summary>
		/// Sets the specified property to the specified int value.
		/// </summary>
		public static void SetIntValue(this ITsPropsBldr tpb, FwTextPropType tpt, FwTextPropVar var, int value)
		{
			tpb.SetIntPropValues((int) tpt, (int) var, value);
		}

		/// <summary>
		/// Sets the specified property to the specified string value.
		/// </summary>
		public static void SetStringValue(this ITsPropsBldr tpb, FwTextPropType tpt, string value)
		{
			tpb.SetStrPropValue((int) tpt, value);
		}

		/// <summary>
		/// Sets the specified property to the string value represented by the specified byte array.
		/// </summary>
		public static void SetStringValue(this ITsPropsBldr tpb, FwTextPropType tpt, byte[] value)
		{
			tpb.SetStrPropValueRgch((int) tpt, value, value == null ? 0 : value.Length);
		}

		#endregion

		#region ITsIncStrBldr methods

		/// <summary>
		/// Sets the specified property to the specified int value.
		/// </summary>
		public static void SetIntValue(this ITsIncStrBldr tisb, FwTextPropType tpt, FwTextPropVar var, int value)
		{
			tisb.SetIntPropValues((int) tpt, (int) var, value);
		}

		/// <summary>
		/// Sets the specified property to the specified string value.
		/// </summary>
		public static void SetStringValue(this ITsIncStrBldr tisb, FwTextPropType tpt, string value)
		{
			tisb.SetStrPropValue((int) tpt, value);
		}

		/// <summary>
		/// Sets the specified property to the string value represented by the specified byte array.
		/// </summary>
		public static void SetStringValue(this ITsIncStrBldr tisb, FwTextPropType tpt, byte[] value)
		{
			tisb.SetStrPropValueRgch((int) tpt, value, value == null ? 0 : value.Length);
		}

		#endregion

		#region Private helper methods
		private static bool IsWhite(char ch)
		{
			return Character.GetCharType(ch) == Character.UCharCategory.SPACE_SEPARATOR;
		}
		#endregion
	}
}
