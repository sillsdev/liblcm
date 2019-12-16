﻿// Copyright (c) 2012-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace SIL.LCModel.FixData
{
	/// <summary>
	/// For projects using Send/Receive, two users on separate machines can create Lexical entries which have the same form
	/// and MorphType.  When merging is done these items will have different GUIDs and their Homograph numbers will both be set to
	/// zero '0'.   If these were created on the same machine they would show as having Homograph numbers 1 and 2.
	///
	/// This RtFixer is designed to correct this problem.  In the above example the Homograph numbers should end up being 1 and 2.
	///
	/// </summary>
	internal class HomographFixer : RtFixer
	{
		Dictionary<Guid, XElement> m_MoAllomorphs = new Dictionary<Guid, XElement>();
		Dictionary<Guid, string> m_oldHomographNumbers = new Dictionary<Guid, string>();
		HashSet<Guid> m_firstAllomorphs = new HashSet<Guid>();
		Dictionary<String, List<Guid>> m_Homographs = new Dictionary<String, List<Guid>>();
		Dictionary<Guid, XElement> entriesWithCitationForm = new Dictionary<Guid, XElement>();
		Dictionary<Guid, String> m_LexEntryHomographNumbers = new Dictionary<Guid, String>();
		const string kUnknown = "<unknown>";
		private string m_homographWs = null;
		private Dictionary<Guid, string> m_MorphTypeSort = new Dictionary<Guid, string>();

		internal override void InspectElement(XElement rt)
		{
			// If this is a class I'm interested in, get the information I need.
			var guid = new Guid(rt.Attribute("guid").Value);
			var xaClass = rt.Attribute("class");
			var className = xaClass == null ? kUnknown : xaClass.Value;
			if (className == kUnknown)
				return;
			switch (className)
			{
				case "MoAffixAllomorph":
				case "MoStemAllomorph":
					m_MoAllomorphs.Add(guid, rt);
					break;
				case "MoMorphType":
					var sortelement = rt.Element("SecondaryOrder");
					if (sortelement != null)
					{
						var order = sortelement.Attribute("val");
						if (order != null)
						{
							m_MorphTypeSort.Add(guid, order.Value);
						}
					}
					break;
				case "LexEntry":
					var homographElement = rt.Element("HomographNumber");
					string homographVal = "0";
					if (homographElement != null)
					{
						var homographAttribue = homographElement.Attribute("val");
						if (homographAttribue != null)
							homographVal = homographAttribue.Value;
					}
					m_oldHomographNumbers[guid] = homographVal;

					var lf = rt.Element("LexemeForm");
					if (lf != null)
					{
						var os = lf.Element("objsur");
						if (os != null)
						{
							var lfGuid = new Guid(os.Attribute("guid").Value);
							m_firstAllomorphs.Add(lfGuid);
						}
					}
					var citationForm = rt.Element("CitationForm");
					if (citationForm != null)
					{
						entriesWithCitationForm.Add(guid, citationForm);
					}
					break;
				case "LangProject":
					var homoWsElt = rt.Element("HomographWs");
					if (homoWsElt == null)
						break;
					var uniElt = homoWsElt.Element("Uni");
					if (uniElt == null)
						break;
					m_homographWs = uniElt.Value;
					break;
				default:
					break;
			}
		}

		internal override void FinalFixerInitialization(Dictionary<Guid, Guid> owners, HashSet<Guid> guids,
			Dictionary<string, HashSet<string>> parentToOwnedObjsur, HashSet<string> rtElementsToDelete)
		{
			base.FinalFixerInitialization(owners, guids, parentToOwnedObjsur, rtElementsToDelete); // Sets base class member variables

			// Create a dictionary with the Form and MorphType guid as the key and a list of ownerguid's as the value.  This
			// will show us which LexEntries should have homograph numbers.  If the list of ownerguids has only one entry then
			// it's homograph number should be zero. If the list of owerguids has more than one guid then the LexEntries
			// associated with those guids are homographs and will require unique homograph numbers.
			foreach (var morphKvp in m_MoAllomorphs)
			{
				List<Guid> guidsForHomograph;
				var rtElem = morphKvp.Value;
				var morphGuid = new Guid(rtElem.Attribute("guid").Value);
				// We don't assign homographs based on allomorphs.
				if (!m_firstAllomorphs.Contains(morphGuid))
					continue;
				string rtFormText;
				if (entriesWithCitationForm.Keys.Contains(owners[morphGuid]))
				{
					var entryGuid = owners[morphGuid];
					var cfElement = entriesWithCitationForm[entryGuid];
					rtFormText = GetStringInHomographWritingSystem(cfElement);
					if (string.IsNullOrWhiteSpace(rtFormText))
						continue;
					rtFormText = rtFormText.Trim();
				}
				else
				{
					var rtForm = rtElem.Element("Form");
					if (rtForm == null)
						continue;
					rtFormText = GetStringInHomographWritingSystem(rtForm);
					if (string.IsNullOrWhiteSpace(rtFormText))
						continue; // entries with no lexeme form are not considered homographs.
				}

				var rtMorphType = rtElem.Element("MorphType");
				if (rtMorphType == null)
					continue;
				var rtObjsur = rtMorphType.Element("objsur");
				if (rtObjsur == null)
					continue;
				var guid = rtObjsur.Attribute("guid").Value;

				// if there was a citation form which matches the form of this MoStemAllomorph the MorphType
				// is not important to the homograph determination.
				var key = m_Homographs.ContainsKey(rtFormText) ? rtFormText : rtFormText + m_MorphTypeSort[new Guid(guid)];

				var ownerguid = new Guid(rtElem.Attribute("ownerguid").Value);
				if (m_Homographs.TryGetValue(key, out guidsForHomograph))
				{
					guidsForHomograph.Add(ownerguid);
				}
				else
				{
					guidsForHomograph = new List<Guid> { ownerguid };
					m_Homographs.Add(key, guidsForHomograph);
				}
			}

			//Now assign a homograph number to each LexEntry that needs one.
			foreach (List<Guid> lexEntryGuids in m_Homographs.Values)
			{
				if (lexEntryGuids.Count > 1)
				{
					var orderedGuids = new Guid[lexEntryGuids.Count];
					var mustChange = new List<Guid>();
					foreach (var guid in lexEntryGuids)
					{
						// if it can keep its current HN, put it in orderedGuids at the correct position to give it that HN.
						// otherwise, remember that it must be put somewhere else.
						string oldHn;
						if (!m_oldHomographNumbers.TryGetValue(guid, out oldHn))
							oldHn = "0";
						int index;
						if (int.TryParse(oldHn, out index) && index > 0 && index <= orderedGuids.Length &&
							orderedGuids[index - 1] == Guid.Empty)
						{
							orderedGuids[index - 1] = guid;
						}
						else
						{
							mustChange.Add(guid);
						}
					}
					// The ones that have to change get slotted into whatever slots are still empty.
					// There must be enough slots because we made the array just big enough.
					foreach (var guid in mustChange)
					{
						for (int j = 0; j < orderedGuids.Length; j++)
						{
							if (orderedGuids[j] == Guid.Empty)
							{
								orderedGuids[j] = guid;
								break;
							}
						}
					}
					var i = 1;
					foreach (var lexEntryGuid in orderedGuids)
					{
						m_LexEntryHomographNumbers.Add(lexEntryGuid, i.ToString());
						i++;
					}
				}
			}

		}

		private string GetStringInHomographWritingSystem(XElement rtForm)
		{
			var alternateFormElement =  rtForm.Elements("AUni").FirstOrDefault(form => form.Attribute("ws") != null && form.Attribute("ws").Value == m_homographWs);
			return alternateFormElement == null ? null : alternateFormElement.Value;
		}

		//For each LexEntry element, set the homograph number as determined in the FinalFixerInitialization method.
		internal override bool FixElement(XElement rt, FwDataFixer.ErrorLogger logger)
		{
			var guidString = rt.Attribute("guid").Value;
			var guid = new Guid(guidString);
			var xaClass = rt.Attribute("class");
			var className = xaClass == null ? kUnknown : xaClass.Value;
			switch (className)
			{
				case "LexEntry":
					string homographNum;
					if (!m_LexEntryHomographNumbers.TryGetValue(guid, out homographNum))
						homographNum = "0"; // If it's not a homograph, its HN must be zero.
					var homographElement = rt.Element("HomographNumber");
					if (homographElement == null)
					{
						if (homographNum != "0") // no need to make a change if already implicitly zero.
						{
							rt.Add(new XElement("HomographNumber", new XAttribute("val", homographNum)));
							logger(String.Format(Strings.ksAdjustedHomograph, guidString, m_oldHomographNumbers[guid], homographNum), true);
						}
						break;
					}
					if (homographElement.Attribute("val") == null || homographElement.Attribute("val").Value != homographNum)
					{
						homographElement.SetAttributeValue("val", homographNum);
						logger(String.Format(Strings.ksAdjustedHomograph, guidString, m_oldHomographNumbers[guid], homographNum), true);
					}
					break;
				default:
					break;
			}
			return true;
		}

		internal override void Reset()
		{
			m_MoAllomorphs.Clear();
			m_oldHomographNumbers.Clear();
			m_firstAllomorphs.Clear();
			m_Homographs.Clear();
			entriesWithCitationForm.Clear();
			m_LexEntryHomographNumbers.Clear();
			m_homographWs = null;
			m_MorphTypeSort.Clear();
			base.Reset();
		}
	}
}
