// Copyright (c) 2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections;
using System.Xml.Linq;
using NUnit.Framework;

namespace SIL.LCModel.DomainServices.DataMigration
{
	/// <inheritdoc />
	[TestFixture]
	public class DataMigration7000072Tests : DataMigrationTestsBase
	{
		/// <summary>
		/// Test the migration from version 7000071 to 7000072.
		/// </summary>
		[Test]
		public void DataMigration7000072Test()
		{
			var dtos = DataMigrationTestServices.ParseProjectFile("DataMigration7000072.xml");
			var mockMdc = new MockMDCForDataMigration();
			IDomainObjectDTORepository dtoRepos = new DomainObjectDtoRepository(7000071, dtos, mockMdc, null, TestDirectoryFinder.LcmDirectories);
			m_dataMigrationManager.PerformMigration(dtoRepos, 7000072, new DummyProgressDlg());

			// Test to Check whether the given Reversal Index Entry has the added senses
			// Get the Reversal Index Entry with the specified guid
			var aReversalIndexEntry = dtoRepos.GetDTO("2d1fb89c-5671-43de-8cdd-4624f4648ef4");
			var reversalIndexElement = XElement.Parse(aReversalIndexEntry.Xml);

			// Check for the Senses Tag
			var sensesElement = reversalIndexElement.Element("Senses");
			Assert.NotNull(sensesElement, "No senses‽");
			var objsurElementsList = sensesElement.Elements("objsur");
			var guidList = new ArrayList();
			foreach (var objsurElement in objsurElementsList)
			{
				guidList.Add(objsurElement.Attribute("guid")?.Value);
			}

			Assert.AreEqual(guidList[0], "c836e945-92d3-4560-9622-bfd9656551c8");
			Assert.AreEqual(guidList[1], "d3d19eae-d840-484e-8de2-0100336808ed");

			// Test to check whether Reversal Entries collection has been removed from LexSense
			var allLexSenses = dtoRepos.AllInstancesWithSubclasses("LexSense");
			foreach (var aLexSense in allLexSenses)
			{
				var lexSenseElement = XElement.Parse(aLexSense.Xml);
				var reveralEntriesElement = lexSenseElement.Element("ReversalEntries");
				Assert.AreEqual(reveralEntriesElement,null);
			}

			// Test to check whether the Referring Senses value has been changed to Senses under VirtualOrdering
			var allOrderings = dtoRepos.AllInstancesWithSubclasses("VirtualOrdering");
			foreach (var anOrdering in allOrderings)
			{
				var orderingElement = XElement.Parse(anOrdering.Xml);
				var field = orderingElement.Element("Field");
				Assert.NotNull(field, "field should not be null");
				var uniValue = field.Element("Uni");
				Assert.NotNull(uniValue, "uniValue should not be null");
				Assert.AreEqual("Senses", uniValue.Value);
			}
		}
	}
}
