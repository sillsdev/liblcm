// Copyright (c) 2010-2013 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
//
// File: TsStringSerializer.cs
// Responsibility: FW Team
//
// <remarks>
// </remarks>

using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Icu;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Utils;

namespace SIL.LCModel.Core.Text
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Class for serializing and deserializing TsStrings stored in XML format
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public static class TsStringSerializer
	{
		#region Public Serialization Methods

		/// <summary>
		/// Serializes the <see cref="ITsString"/> to XML.
		/// </summary>
		public static string SerializeTsStringToXml(ITsString tss, ILgWritingSystemFactory lgwsf, int ws = 0, bool writeObjData = true, bool indent = false)
		{
			// We export only the NFSC form (NFC with exceptions for the parallel style information)
			ITsString normalizedTss = tss.get_NormalizedForm(FwNormalizationMode.knmNFSC);

			var xml = new StringBuilder();
			var settings = new XmlWriterSettings
			{
				OmitXmlDeclaration = true,
				Indent = true,
				IndentChars = indent ? "  " : string.Empty,
				NewLineChars = Environment.NewLine
			};
			using (var writer = XmlWriter.Create(xml, settings))
			{
				if (ws > 0)
				{
					string id = lgwsf.GetStrFromWs(ws);
					if (string.IsNullOrEmpty(id))
						throw new ArgumentException("Unknown writing system handle " + ws + "; cannot write an <AStr> without a ws.", "ws");
					writer.WriteStartElement("AStr");
					writer.WriteAttributeString("ws", Normalizer.Normalize(id, Normalizer.UNormalizationMode.UNORM_NFC));
				}
				else
				{
					writer.WriteStartElement("Str");
				}

				// Write the properties and text for each run
				string fieldName = null;
				int previousWs = 0;
				for (int i = 0; i < normalizedTss.RunCount; i++)
				{
					TsRunInfo tri;
					ITsTextProps textProps = normalizedTss.FetchRunInfo(i, out tri);
					string objDataStr;
					if (textProps.TryGetStringValue(FwTextPropType.ktptObjData, out objDataStr) && !writeObjData)
					{
						var chType = (FwObjDataTypes) objDataStr[0];
						if (chType == FwObjDataTypes.kodtPictEvenHot || chType == FwObjDataTypes.kodtPictOddHot
							|| chType == FwObjDataTypes.kodtNameGuidHot || chType == FwObjDataTypes.kodtOwnNameGuidHot)
						{
							continue;
						}
					}

					string runFieldName;
					if (textProps.TryGetStringValue(FwTextPropType.ktptFieldName, out runFieldName) && fieldName != runFieldName)
					{
						if (!string.IsNullOrEmpty(fieldName))
							writer.WriteEndElement();
						if (!string.IsNullOrEmpty(runFieldName))
						{
							writer.WriteStartElement("Field");
							writer.WriteAttributeString("name", runFieldName);
						}
						fieldName = runFieldName;
					}

					bool markItem;
					FwTextPropVar var;
					int markItemValue;
					if (textProps.TryGetIntValue(FwTextPropType.ktptMarkItem, out var, out markItemValue)
						&& var == FwTextPropVar.ktpvEnum && markItemValue == (int) FwTextToggleVal.kttvForceOn)
					{
						writer.WriteStartElement("Item");
						writer.WriteStartElement("Run");
						markItem = true;
					}
					else
					{
						writer.WriteStartElement("Run");
						markItem = false;
					}

					for (int j = 0; j < textProps.IntPropCount; j++)
					{
						FwTextPropType tpt;
						int value = textProps.GetIntProperty(j, out tpt, out var);
						if (tpt != FwTextPropType.ktptMarkItem)
							TsPropsSerializer.WriteIntProperty(writer, lgwsf, tpt, var, value);
					}

					byte[] pict = null;
					bool hotGuid = false;
					for (int j = 0; j < textProps.StrPropCount; j++)
					{
						FwTextPropType tpt;
						string value = textProps.GetStringProperty(j, out tpt);
						TsPropsSerializer.WriteStringProperty(writer, tpt, value);
						if (tpt == FwTextPropType.ktptObjData && !string.IsNullOrEmpty(value))
						{
							switch ((FwObjDataTypes) value[0])
							{
								// The element data associated with a picture is the actual picture data
								// since it is much too large to want embedded as an XML attribute value.
								// (This is an antique kludge that isn't really used in practice, but some
								// of our test data still exercises it.)
								case FwObjDataTypes.kodtPictEvenHot:
								case FwObjDataTypes.kodtPictOddHot:
									pict = Encoding.Unicode.GetBytes(value.Substring(1));
									break;
								// The generated XML contains both the link value as an attribute and the
								// (possibly edited) display string as the run's element data.
								case FwObjDataTypes.kodtExternalPathName:
									break;
								// used ONLY in the clipboard...contains XML representation of (currently) a footnote.
								case FwObjDataTypes.kodtEmbeddedObjectData:
									break;
								// The string data associated with this run is assumed to be a dummy magic
								// character that flags (redundantly for XML) that the actual data to
								// display is based on the ktptObjData attribute.
								case FwObjDataTypes.kodtNameGuidHot:
								case FwObjDataTypes.kodtOwnNameGuidHot:
								case FwObjDataTypes.kodtContextString:
								case FwObjDataTypes.kodtGuidMoveableObjDisp:
									hotGuid = true;
									break;
							}
						}
					}

					// Work out the text that will actually be written, so that we can tell whether the reader
					// will require a ws on this run.
					string writtenText;
					bool preserveSpace = false;
					if (pict != null)
					{
						// Write the bytes of the picture data
						var sb = new StringBuilder();
						for (int j = 0; j < pict.Length; j++)
						{
							sb.Append(pict[j].ToString("X2"));
							if (j % 32 == 31)
								sb.AppendLine();
						}
						writtenText = sb.ToString();
					}
					else if (hotGuid)
					{
						writtenText = string.Empty;
					}
					else
					{
						string runText = normalizedTss.get_RunText(i) ?? string.Empty;
						preserveSpace = runText != string.Empty && runText.All(char.IsWhiteSpace);
						// TODO: should we escape quotation marks? this is not necessary but different than the behavior of the C++ implementation
						writtenText = StripInvalidXmlChars(Normalizer.Normalize(runText, Normalizer.UNormalizationMode.UNORM_NFC));
					}

					// A run that needs a ws but has none we can write (an unknown handle, or no ws at all) gets a
					// fallback ws. Otherwise we would write a record that no reader can load, and throwing here
					// would stop the whole project from being saved.
					int runWs;
					FwTextPropVar wsVar;
					bool hasKnownWs = textProps.TryGetIntValue(FwTextPropType.ktptWs, out wsVar, out runWs) && runWs > 0
						&& !string.IsNullOrEmpty(lgwsf.GetStrFromWs(runWs));
					if (hasKnownWs)
					{
						previousWs = runWs;
					}
					else if (RunNeedsWs(writtenText))
					{
						int fallbackWs = GetFallbackWs(lgwsf, ws, previousWs);
						if (fallbackWs > 0)
						{
							Trace.TraceWarning("TsStringSerializer: run has no usable writing system (handle " + runWs +
								"); writing it with fallback ws '" + lgwsf.GetStrFromWs(fallbackWs) + "'.");
							writer.WriteAttributeString("ws", Normalizer.Normalize(lgwsf.GetStrFromWs(fallbackWs), Normalizer.UNormalizationMode.UNORM_NFC));
							previousWs = fallbackWs;
						}
					}

					if (preserveSpace)
						writer.WriteAttributeString("xml", "space", "", "preserve");
					writer.WriteString(writtenText);

					writer.WriteEndElement();
					if (markItem)
						writer.WriteEndElement();
				}
				if (!string.IsNullOrEmpty(fieldName))
					writer.WriteEndElement();
				writer.WriteEndElement();
			}
			return xml.ToString();
		}

		private static readonly Regex InvalidXmlRegex = new Regex(@"[\x00-\x08\x0B\x0C\x0E-\x1F\uFFFE\uFFFF]", RegexOptions.Compiled);
		public static string StripInvalidXmlChars(string text)
		{
			// Remove characters not allowed in XML
			// Documented here: https://en.wikipedia.org/wiki/Valid_characters_in_XML
			return InvalidXmlRegex.Replace(text, string.Empty);
		}

		#endregion

		#region Serialization Helper Methods

		#endregion

		#region Public Deserialization Methods
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Deserializes a TsString from the specified XML data.
		/// NOTE: This overload is slower then using the overload that takes an XElement.
		/// </summary>
		/// <param name="xml">The XML data.</param>
		/// <param name="lgwsf">The writing system factory.</param>
		/// <returns>The created TsString. Will never be <c>null</c> because an exception is
		/// thrown if anything is invalid.
		/// </returns>
		/// ------------------------------------------------------------------------------------
		public static ITsString DeserializeTsStringFromXml(string xml, ILgWritingSystemFactory lgwsf)
		{
			return DeserializeTsStringFromXml(XElement.Parse(xml, LoadOptions.PreserveWhitespace), lgwsf);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Deserializes a TsString from the XML node.
		/// </summary>
		/// <param name="xml">The XML node.</param>
		/// <param name="lgwsf">The writing system factory.</param>
		/// <returns>The created TsString. Will never be <c>null</c> because an exception is
		/// thrown if anything is invalid.</returns>
		/// ------------------------------------------------------------------------------------
		public static ITsString DeserializeTsStringFromXml(XElement xml, ILgWritingSystemFactory lgwsf)
		{
			if (xml != null)
			{
				switch (xml.Name.LocalName)
				{
					case "AStr":
					case "Str":
						return HandleSimpleString(xml, lgwsf) ?? HandleComplexString(xml, lgwsf);
					default:
						throw new XmlSchemaException("TsString XML must contain a <Str> or <AStr> root element");
				}
			}
			return null;
		}

		#endregion

		#region Deserialization Helper Methods
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Handles a complex string that contains multiple runs with optional multiple
		/// text props applied.
		/// </summary>
		/// <param name="xml">The XML.</param>
		/// <param name="lgwsf">The writing system factory.</param>
		/// <returns>The created TsString</returns>
		/// ------------------------------------------------------------------------------------
		private static ITsString HandleComplexString(XElement xml, ILgWritingSystemFactory lgwsf)
		{
			var runs = xml.Elements("Run");
			if (runs.Count() == 0)
			{
				if (xml.Name.LocalName == "AStr" && xml.Attributes().Count() == 1)
				{
					// This duplicates a little bit of code from HandleSimpleRun, but I wanted to keep that really simple
					// and fast, and this case hardly ever happens...maybe not at all in real life.
					XAttribute wsAttribute = xml.Attributes().First();
					if (wsAttribute.Name.LocalName != "ws")
						return null; // we handle only single runs with only the ws attribute.
					// Make sure the text is in the decomposed form (FWR-148)
					string runText = Normalizer.Normalize(xml.Value, Normalizer.UNormalizationMode.UNORM_NFD);
					return TsStringUtils.MakeString(runText, GetWsForId(wsAttribute.Value, lgwsf));
				}
				return null;	// If we don't have any runs, we don't have a string!
			}

			var strBldr = TsStringUtils.MakeIncStrBldr();

			// The ws of the enclosing <AStr>, if it has one: the best fallback for a run whose ws is empty.
			int enclosingWs = 0;
			bool enclosingWsMissing = false;
			if (xml.Name.LocalName == "AStr")
			{
				XAttribute astrWs = xml.Attribute("ws");
				if (astrWs != null && astrWs.Value.Length > 0)
					enclosingWs = GetWsForId(astrWs.Value, lgwsf);
				else
					enclosingWsMissing = true;
			}
			int previousWs = 0;

			foreach (XElement runElement in runs)
			{
				if (runElement == null)
					throw new XmlSchemaException("TsString XML must contain a <Run> element contained in a <" + xml.Name.LocalName + "> element");
				string runText = runElement.Value;
				XAttribute runWsAttr = runElement.Attribute("ws");
				if (runWsAttr == null && RunNeedsWs(runText))
					throw new XmlSchemaException("Run element must contain a ws attribute. Run text: " + runElement.Value);

				bool needsFallbackWs = runWsAttr != null && runWsAttr.Value.Length == 0 && RunNeedsWs(runText);
				// Make sure the text is in the decomposed form (FWR-148)
				runText = Normalizer.Normalize(runText, Normalizer.UNormalizationMode.UNORM_NFD);
				bool isOrcNeeded = TsPropsSerializer.GetPropAttributesForElement(runElement, lgwsf, strBldr);

				if (runWsAttr != null && runWsAttr.Value.Length > 0)
				{
					previousWs = GetWsForId(runWsAttr.Value, lgwsf);
				}
				else if (needsFallbackWs)
				{
					// An empty ws was written by older versions for an unknown handle. Give the run a known ws so that
					// it can be saved again. If the enclosing <AStr> has no ws either, the run's ws would become the key
					// of the alternative (see MultiAccessor.ReadAstrElementOfMultiString), so do not invent one then:
					// that could overwrite a real alternative.
					int fallbackWs = enclosingWs > 0 ? enclosingWs : previousWs > 0 ? previousWs :
						(enclosingWsMissing ? 0 : GetFallbackWs(lgwsf, 0, 0));
					Trace.TraceWarning("TsStringSerializer: run has an empty ws; " + (fallbackWs > 0
						? "using fallback ws '" + lgwsf.GetStrFromWs(fallbackWs) + "'." : "leaving it without a ws."));
					if (fallbackWs > 0)
					{
						strBldr.SetIntPropValues((int)FwTextPropType.ktptWs, 0, fallbackWs);
						previousWs = fallbackWs;
					}
				}

				// Add an ORC character, if needed, for the run
				if (runText.Length == 0 && isOrcNeeded)
					runText = StringUtils.kszObject;

				// Add the text with the properties to the builder
				strBldr.Append(runText);
			}

			return strBldr.GetString();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Handles creating simple TsStrings that contain one run with only a writing system.
		/// </summary>
		/// <param name="rootXml">The element including the Str or AStr tag</param>
		/// <param name="lgwsf">The writing system factory.</param>
		/// <returns>The created TsString or null if the XML was too complext to be handled
		/// by this method.</returns>
		/// ------------------------------------------------------------------------------------
		private static ITsString HandleSimpleString(XElement rootXml, ILgWritingSystemFactory lgwsf)
		{
			if (rootXml.Elements().Count() != 1)
				return null;

			XElement textElement = rootXml.Elements().First();
			if (textElement.Name.LocalName != "Run")
				return null; // probably an error, anyway not simple case we are optimizing.

			int cTextElementAtribs = textElement.Attributes().Count();
			if (cTextElementAtribs != 1)
				return null; // Way too complex for this simple case

			XAttribute wsAttribute = textElement.Attributes().First();
			if (wsAttribute.Name.LocalName != "ws" || wsAttribute.Value.Length == 0)
				return null; // we handle only single runs with only a (non-empty) ws attribute; an empty ws, written by older versions for an unknown handle, is dropped by the general path.
			// Make sure the text is in the decomposed form (FWR-148)
			string runText = Normalizer.Normalize(textElement.Value, Normalizer.UNormalizationMode.UNORM_NFD);
			return TsStringUtils.MakeString(runText, GetWsForId(wsAttribute.Value, lgwsf));
		}
		#endregion

		#region Internal methods
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Whether the reader requires a ws attribute on a run whose text, as written to XML, is
		/// <paramref name="writtenText"/>. A run that is empty (e.g. an ORC or link run) or starts with an ordinary
		/// character needs one; a run starting with a control character up to U+000D (tab, newline...) does not.
		/// </summary>
		internal static bool RunNeedsWs(string writtenText)
		{
			return writtenText.Length == 0 || writtenText[0] > 13;
		}

		/// <summary>
		/// Chooses a known ws for a run that has none: the enclosing ws, else the previous run's ws, else the user ws.
		/// Returns 0 if none is known.
		/// </summary>
		internal static int GetFallbackWs(ILgWritingSystemFactory lgwsf, int enclosingWs, int previousWs)
		{
			foreach (int candidate in new[] { enclosingWs, previousWs })
			{
				if (candidate > 0 && !string.IsNullOrEmpty(lgwsf.GetStrFromWs(candidate)))
					return candidate;
			}
			int userWs = lgwsf.UserWs;
			return userWs > 0 && !string.IsNullOrEmpty(lgwsf.GetStrFromWs(userWs)) ? userWs : 0;
		}

		/// <summary>
		/// Gets the ws handle for specified writing system identifier.
		/// </summary>
		/// <param name="wsId">The writing system identifier.</param>
		/// <param name="lgwsf">The writing system factory.</param>
		/// ------------------------------------------------------------------------------------
		internal static int GetWsForId(string wsId, ILgWritingSystemFactory lgwsf)
		{
			try
			{
				ILgWritingSystem ws = lgwsf.get_Engine(wsId);
				return ws.Handle;
			}
			catch (Exception e)
			{
				throw new XmlSchemaException("Unable to create writing system: " + wsId, e);
			}
		}
		#endregion
	}
}
