// Copyright (c) 2016-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NUnit.Framework;

namespace SIL.LCModel.DomainServices.DataMigration
{
	/// <summary>
	/// Unit tests for DataMigration7000072
	/// </summary>
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
			IDomainObjectDTORepository dtoRepos = new DomainObjectDtoRepository(7000071, dtos, mockMdc, null,
				TestDirectoryFinder.LcmDirectories);
			m_dataMigrationManager.PerformMigration(dtoRepos, 7000072, new DummyProgressDlg());

			var allOrderings = dtoRepos.AllInstancesWithSubclasses("VirtualOrdering");
			foreach (var anOrdering in allOrderings)
			{
				var orderingElement = XElement.Parse(anOrdering.Xml);
				var field = orderingElement.Element("Field");
				var uniValue = field.Element("Uni");
				
				Assert.AreEqual("Senses", uniValue.Value);
			}

			var allReversalIndexEntries = dtoRepos.AllInstancesWithSubclasses("ReversalIndexEntry");
			foreach (var aReversalIndexEntry in allReversalIndexEntries)
			{
				var reversalIndexElement = XElement.Parse(aReversalIndexEntry.Xml);
				var reversalIndexEntryGuid = reversalIndexElement.Attribute("guid")?.Value;
				if (reversalIndexEntryGuid == "2d1fb89c-5671-43de-8cdd-4624f4648ef4")
				{
					var sensesElement = reversalIndexElement.Element("Senses");
					if (sensesElement != null)
					{
						var objsurElementsList = sensesElement.Elements("objsur");
						var guidList = new ArrayList();
						foreach (var objsurElement in objsurElementsList)
						{
							guidList.Add(objsurElement.Attribute("guid")?.Value);
						}

						Assert.AreEqual(guidList[0], "c836e945-92d3-4560-9622-bfd9656551c8");
						Assert.AreEqual(guidList[1], "d3d19eae-d840-484e-8de2-0100336808ed");

						//Assert.Equals(guidList[0].ToString(), "c836e945-92d3-4560-9622-bfd9656551c8");
						//Assert.Equals(guidList[1].ToString(), "d3d19eae-d840-484e-8de2-0100336808ed");

					}
				}
			}

			var allLexSenses = dtoRepos.AllInstancesWithSubclasses("LexSense");
			foreach (var aLexSense in allLexSenses)
			{
				var lexSenseElement = XElement.Parse(aLexSense.Xml);
				var reveralEntriesElement = lexSenseElement.Element("ReversalEntries");
				Assert.AreEqual(reveralEntriesElement,null);
			}
		}
	}
}
