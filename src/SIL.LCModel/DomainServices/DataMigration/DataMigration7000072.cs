// Copyright (c) 2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Diagnostics;
using System.Xml.Linq;

namespace SIL.LCModel.DomainServices.DataMigration
{
	/// <inheritdoc />
	internal class DataMigration7000072 : IDataMigration
	{
		/// <summary>
		/// 1) Add the reference collection of Senses to each reversal index and
		/// fill it with the Senses that referenced the ReversalIndexEntry
		/// 2) Remove the collection of ReversalIndexEntry from each Sense
		/// 3) Migrate any VirtualOrdering objects if necessary
		/// </summary>
		public void PerformMigration(IDomainObjectDTORepository repoDto)
		{
			DataMigrationServices.CheckVersionNumber(repoDto, 7000071);

			AddSensesToReversalIndexEntry(repoDto);

			RemoveReversalEntriesFromSenses(repoDto);

			ChangeReferringSensesToSenses(repoDto);

			DataMigrationServices.IncrementVersionNumber(repoDto);
		}

		/// <summary>
		/// Add the reference collection of Senses to each reversal index
		/// and fill it with the Senses that referenced the ReversalIndexEntry
		/// </summary>
		public void AddSensesToReversalIndexEntry(IDomainObjectDTORepository repoDto)
		{
			// Get all the LexSense Classes
			var allLexSenses = repoDto.AllInstancesWithSubclasses("LexSense");
			foreach (var aLexSense in allLexSenses)
			{
				var lexSenseElement = XElement.Parse(aLexSense.Xml);
				// Get the ReversalEntries within the LexSense
				var reversalEntries = lexSenseElement.Element("ReversalEntries");
				if (reversalEntries == null)
					continue;

				// Get the Current LexSense's Guid
				var lexSenseGuid = lexSenseElement.Attribute("guid")?.Value;
				Debug.Assert(lexSenseGuid != null, "lexSenseGuid != null");

				// Loop through the ReversalEntries within the LexSense
				var reversalEntriesList = reversalEntries.Elements("objsur");
				foreach (var aReversalEntry in reversalEntriesList)
				{
					// Get the Current ReversalEntries (objsur) Guid from the objsur
					var reversalEntryGuid = aReversalEntry.Attribute("guid")?.Value;

					// Get the Reversal Entry Object, that has the specified Guid, that needs to be modified
					var aReversalIndexEntry = repoDto.GetDTO(reversalEntryGuid);

					// Add Senses to the Reversal Entry
					var reversalIndexElement = XElement.Parse(aReversalIndexEntry.Xml);
					var sensesElement = reversalIndexElement.Element("Senses");
					if (sensesElement == null)
					{
						sensesElement = new XElement("Senses");
						reversalIndexElement.Add(sensesElement);
					}
					var objsur = new XElement("objsur", new XAttribute("guid", lexSenseGuid), new XAttribute("t", "r"));
					sensesElement.Add(objsur);

					// Update the Reversal Entry
					DataMigrationServices.UpdateDTO(repoDto, aReversalIndexEntry, reversalIndexElement.ToString());
				}
			}
		}

		/// <summary>
		/// Remove the collection of ReversalIndexEntry from each Sense
		/// </summary>
		public void RemoveReversalEntriesFromSenses(IDomainObjectDTORepository repoDto)
		{
			// Get all the LexSense Classes
			var allLexSenses = repoDto.AllInstancesWithSubclasses("LexSense");
			foreach (var aLexSense in allLexSenses)
			{
				var lexSenseElement = XElement.Parse(aLexSense.Xml);

				// Remove Reversal Entries if exists
				lexSenseElement.Element("ReversalEntries")?.Remove();

				// Update the LexSense Element
				DataMigrationServices.UpdateDTO(repoDto, aLexSense, lexSenseElement.ToString());
			}
		}

		/// <summary>
		/// Change ReferringSenses To Senses under VirtualOrdering objects, if present
		/// </summary>
		public void ChangeReferringSensesToSenses(IDomainObjectDTORepository repoDto)
		{
			// Get all the VirtualOrdering Classes
			var allOrderings = repoDto.AllInstancesWithSubclasses("VirtualOrdering");
			foreach (var anOrdering in allOrderings)
			{
				var orderingElement = XElement.Parse(anOrdering.Xml);
				var uniValue = orderingElement.Element("Field")?.Element("Uni");

				// Change ReferringSenses Value as Senses
				if (uniValue?.Value == "ReferringSenses")
				{
					uniValue.Value = "Senses";
				}

				// Update the VirtualOrdering Element
				DataMigrationServices.UpdateDTO(repoDto, anOrdering, orderingElement.ToString());
			}
		}
	}
}
