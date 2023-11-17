// Copyright (c) 2010-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Xml.XPath;
using NUnit.Framework;

namespace SIL.LCModel.DomainServices.DataMigration
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Test framework for migration from version 7000015 to 7000016.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[TestFixture]
	public sealed class DataMigrationTests7000016 : DataMigrationTestsBase
	{
		// GUID of the Record Types list.
		readonly string ksguidRecTypesList = RnResearchNbkTags.kguidRecTypesList.ToString("D").ToLowerInvariant();
		// GUIDs of newly created record types, which will all be owned by the list
		readonly string ksguidEvent = RnResearchNbkTags.kguidRecEvent.ToString("D").ToLowerInvariant();
		readonly string ksguidMethodology = RnResearchNbkTags.kguidRecMethodology.ToString("D").ToLowerInvariant();
		readonly string ksguidWeather = RnResearchNbkTags.kguidRecWeather.ToString("D").ToLowerInvariant();
		// GUIDs of record types to be owned by Event.
		readonly string ksguidObservation = RnResearchNbkTags.kguidRecObservation.ToString("D").ToLowerInvariant();
		readonly string ksguidConversation = RnResearchNbkTags.kguidRecConversation.ToString("D").ToLowerInvariant();
		readonly string ksguidInterview = RnResearchNbkTags.kguidRecInterview.ToString("D").ToLowerInvariant();
		readonly string ksguidPerformance = RnResearchNbkTags.kguidRecPerformance.ToString("D").ToLowerInvariant();

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test the migration from version 7000015 to 7000016.
		/// (Add some record types for Data Notebook, and rearrange the list hierarchy.  See
		/// FWR-643 for details.)
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void DataMigration7000016Test()
		{
			var dtos = DataMigrationTestServices.ParseProjectFile("DataMigration7000016.xml");

			var mockMdc = SetupMdc();

			IDomainObjectDTORepository repoDTO = new DomainObjectDtoRepository(7000015, dtos, mockMdc, null,
				TestDirectoryFinder.LcmDirectories);

			// SUT: Do the migration.
			m_dataMigrationManager.PerformMigration(repoDTO, 7000016, new DummyProgressDlg());

			// Verification Phase
			Assert.AreEqual(7000016, repoDTO.CurrentModelVersion, "Wrong updated version.");

			DomainObjectXMLDTO nbkDto = null;
			foreach (DomainObjectXMLDTO dot in repoDTO.AllInstancesSansSubclasses("RnResearchNbk"))
			{
				nbkDto = dot;
				break;
			}
			Assert.NotNull(nbkDto);
			XElement nbkElem = XElement.Parse(nbkDto.Xml);
			var recTypesGuid = (string)nbkElem.XPathSelectElement("RecTypes/objsur").Attribute("guid");
			Assert.AreEqual(recTypesGuid.ToLowerInvariant(), ksguidRecTypesList);

			// All we can guarantee being able to check are those items that we create, and to a
			// limited degree, those items that are moved to belong to a created item.
			bool fFoundEvent = false;
			bool fFoundMethod = false;
			bool fFoundWeather = false;
			foreach (DomainObjectXMLDTO dto in repoDTO.GetDirectlyOwnedDTOs(ksguidRecTypesList))
			{
				string sguid = dto.Guid.ToLowerInvariant();
				Assert.AreNotEqual(sguid, ksguidObservation);
				Assert.AreNotEqual(sguid, ksguidConversation);
				Assert.AreNotEqual(sguid, ksguidInterview);
				Assert.AreNotEqual(sguid, ksguidPerformance);
				if (sguid == ksguidEvent)
				{
					fFoundEvent = true;
					CheckEventSubTypes(repoDTO);
				}
				else if (sguid == ksguidMethodology)
				{
					fFoundMethod = true;
				}
				else if (sguid == ksguidWeather)
				{
					fFoundWeather = true;
				}
			}
			Assert.IsTrue(fFoundEvent);
			Assert.IsTrue(fFoundMethod);
			Assert.IsTrue(fFoundWeather);
		}

		/// <summary></summary>
		[Test]
		public void CheckOnNoPossibilitiesInListAtStart()
		{
			var dtos = new HashSet<DomainObjectXMLDTO>
			{
				new DomainObjectXMLDTO("D9D55B12-EA5E-11DE-95EF-0013722F8DEC".ToLowerInvariant(), "CmPossibilityList",
					@"<rt class='CmPossibilityList' guid='D9D55B12-EA5E-11DE-95EF-0013722F8DEC' ownerguid='D739CBEA-EA5E-11DE-85BE-0013722F8DEC'></rt>")
			};
			var mockMdc = SetupMdc();
			IDomainObjectDTORepository repoDto = new DomainObjectDtoRepository(7000015, dtos, mockMdc, null, TestDirectoryFinder.LcmDirectories);
			// SUT: Do the migration.
			m_dataMigrationManager.PerformMigration(repoDto, 7000016, new DummyProgressDlg());
			// Verification Phase
			Assert.AreEqual(7000016, repoDto.CurrentModelVersion, "Wrong updated version.");
			var survivingItems = new List<string> {ksguidEvent, ksguidMethodology, ksguidWeather};
			foreach (var dto in repoDto.GetDirectlyOwnedDTOs(ksguidRecTypesList))
				Assert.IsTrue(survivingItems.Contains(dto.Guid));
		}

		private void CheckEventSubTypes(IDomainObjectDTORepository repoDTO)
		{
			DomainObjectXMLDTO dtoObs = GetDTOIfItExists(repoDTO, ksguidObservation);
			if (dtoObs != null)
				VerifyOwner(repoDTO, dtoObs, ksguidEvent);
			DomainObjectXMLDTO dtoCon = GetDTOIfItExists(repoDTO, ksguidConversation);
			if (dtoCon != null)
				VerifyOwner(repoDTO, dtoCon, ksguidEvent);
			DomainObjectXMLDTO dtoInt = GetDTOIfItExists(repoDTO, ksguidInterview);
			if (dtoInt != null)
				VerifyOwner(repoDTO, dtoInt, ksguidEvent);
			DomainObjectXMLDTO dtoPer = GetDTOIfItExists(repoDTO, ksguidPerformance);
			if (dtoPer != null)
				VerifyOwner(repoDTO, dtoPer, ksguidEvent);
		}

		private void VerifyOwner(IDomainObjectDTORepository repoDTO, DomainObjectXMLDTO dtoTest, string sguidOwner)
		{
			bool fFound = false;
			foreach (DomainObjectXMLDTO dto in repoDTO.GetDirectlyOwnedDTOs(sguidOwner))
			{
				if (dto.Guid == dtoTest.Guid && dto.Xml == dtoTest.Xml)
				{
					fFound = true;
					break;
				}
			}
			Assert.IsTrue(fFound);
		}

		private static DomainObjectXMLDTO GetDTOIfItExists(IDomainObjectDTORepository dtoRepository, string sGuid)
		{
			try
			{
				return dtoRepository.GetDTO(sGuid);
			}
			catch (ArgumentException)
			{
				return null;
			}
		}

		private static MockMDCForDataMigration SetupMdc()
		{
			var mockMdc = new MockMDCForDataMigration();
			mockMdc.AddClass(1, "CmObject", null, new List<string> { "CmProject", "CmMajorObject", "CmPossibility" });
			mockMdc.AddClass(2, "CmProject", "CmObject", new List<string> { "LangProject" });
			mockMdc.AddClass(3, "CmMajorObject", "CmObject", new List<string> { "RnResearchNbk", "CmPossibilityList" });
			mockMdc.AddClass(4, "CmPossibility", "CmObject", new List<string>());
			mockMdc.AddClass(5, "LangProject", "CmProject", new List<string>());
			mockMdc.AddClass(6, "RnResearchNbk", "CmMajorObject", new List<string>());
			mockMdc.AddClass(7, "CmPossibilityList", "CmMajorObject", new List<string>());

			return mockMdc;
		}
	}
}
