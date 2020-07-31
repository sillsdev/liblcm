// Copyright (c) 2015-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SIL.LCModel.Infrastructure;

namespace SIL.LCModel.DomainServices.DataMigration
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Test the IDomainObjectDTORepository implementation.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[TestFixture]
	public sealed class IDomainObjectDTORepositoryTests
	{
		private IFwMetaDataCacheManaged m_mdc;

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Add mock MDC..
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[OneTimeSetUp]
		public void FixtureSetup()
		{
			var mockMDC = new MockMDCForDataMigration();
			// Set it up for CmObject, LangProject, LexDb, and LexEntry.
			var clsid = 0;
			mockMDC.AddClass(++clsid, "CmObject", null, new List<string> { "LangProject", "LexDb", "LexEntry" });
			mockMDC.AddClass(++clsid, "LangProject", "CmObject", new List<string>());
			mockMDC.AddClass(++clsid, "LexDb", "CmObject", new List<string>());
			mockMDC.AddClass(++clsid, "LexEntry", "CmObject", new List<string>());

			m_mdc = mockMDC;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure a null 'dtos' parameter throws.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void NulldtosParamTest()
		{
			Assert.That(() => new DomainObjectDtoRepository(1, null, m_mdc, null, TestDirectoryFinder.LcmDirectories),
				Throws.TypeOf<ArgumentNullException>());
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure a null 'mdc' parameter throws.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void NullmdcParamTest()
		{
			Assert.That(() => new DomainObjectDtoRepository(1, new HashSet<DomainObjectDTO>(), null, null, TestDirectoryFinder.LcmDirectories),
				Throws.TypeOf<ArgumentNullException>());
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure a non-existant guid throws.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void NonExistantGuidTest()
		{
			IDomainObjectDTORepository dtoRepos = new DomainObjectDtoRepository(
				1,
				new HashSet<DomainObjectDTO>(),
				m_mdc,
				null, TestDirectoryFinder.LcmDirectories);
			Assert.That(() => dtoRepos.GetDTO(Guid.NewGuid().ToString()),
				Throws.TypeOf<ArgumentException>());
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure an extant guid can be found.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void ExtantGuidFindsDTOTest()
		{
			var dtos = new HashSet<DomainObjectDTO>();
			// 1. Add barebones LP.
			const string lpGuid = "9719A466-2240-4DEA-9722-9FE0746A30A6";
			var lpDto = CreatoDTO(dtos, lpGuid, "LangProject", null);
			IDomainObjectDTORepository dtoRepos = new DomainObjectDtoRepository(1, dtos, m_mdc, null, TestDirectoryFinder.LcmDirectories);
			var resultDto = dtoRepos.GetDTO(lpGuid);
			Assert.AreSame(lpDto, resultDto, "Wrong DTO.");
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure a null 'mdc' parameter throws.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void TryGetValueTests()
		{
			var dtos = new HashSet<DomainObjectDTO>();
			// 1. Add barebones LP.
			const string lpGuid = "9719A466-2240-4DEA-9722-9FE0746A30A6";
			CreatoDTO(dtos, lpGuid, "LangProject", null);
			IDomainObjectDTORepository dtoRepos = new DomainObjectDtoRepository(1, dtos, m_mdc, null, TestDirectoryFinder.LcmDirectories);
			DomainObjectDTO dto;
			var retval = dtoRepos.TryGetValue(Guid.NewGuid().ToString().ToLower(), out dto);
			Assert.IsNull(dto, "Oops.It does exist.");
			Assert.IsFalse(retval, "Reportedly, it does exist.");

			retval = dtoRepos.TryGetValue("9719A466-2240-4DEA-9722-9FE0746A30A6", out dto);
			Assert.IsNotNull(dto, "LP does not exist???");
			Assert.IsTrue(retval, "Reportedly, it does not exist.");
		}

		private static DomainObjectDTO CreatoDTO(ICollection<DomainObjectDTO> dtos, string guid, string classname, string ownerGuid)
		{
			var xml = ownerGuid == null
						? string.Format("<rt class=\"{0}\" guid=\"{1}\" />", classname, guid)
						: string.Format("<rt class=\"{0}\" guid=\"{1}\" ownerguid=\"{2}\" />", classname, guid, ownerGuid);
			var dto = new DomainObjectDTO(
				guid,
				classname,
				xml);
			dtos.Add(dto);

			return dto;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure DTOs can be found by class (no subclasses).
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void DtosByClassButNoSubclassesTest()
		{
			var dtos = new HashSet<DomainObjectDTO>();
			// 1. Add barebones LP.
			const string lpGuid = "9719A466-2240-4DEA-9722-9FE0746A30A6";
			CreatoDTO(dtos, lpGuid, "LangProject", null);
			IDomainObjectDTORepository dtoRepos = new DomainObjectDtoRepository(1, dtos, m_mdc, null, TestDirectoryFinder.LcmDirectories);
			var result = new List<DomainObjectDTO>(dtoRepos.AllInstancesSansSubclasses("CmObject"));
			Assert.AreEqual(0, result.Count, "Wrong number of DTOs (expected 0).");
			result = new List<DomainObjectDTO>(dtoRepos.AllInstancesSansSubclasses("LangProject"));
			Assert.AreEqual(1, result.Count, "Wrong number of DTOs (expected 1).");
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure DTOs can be found by class (with subclasses).
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void DtosByClassWithSubclassesTest()
		{
			var dtos = new HashSet<DomainObjectDTO>();
			// 1. Add barebones LP.
			const string lpGuid = "9719A466-2240-4DEA-9722-9FE0746A30A6";
			CreatoDTO(dtos, lpGuid, "LangProject", null);
			const string lexDbGuid = "6C84F84A-5B99-4CF5-A7D5-A308DDC604E0";
			CreatoDTO(dtos, lexDbGuid, "LexDb", null);
			IDomainObjectDTORepository dtoRepos = new DomainObjectDtoRepository(1, dtos, m_mdc, null, TestDirectoryFinder.LcmDirectories);
			var result = new List<DomainObjectDTO>(dtoRepos.AllInstancesWithSubclasses("CmObject"));
			Assert.AreEqual(2, result.Count, "Wrong number of DTOs (expected 2).");
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure owning DTOs can be found, but only if they have an owner.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void OwningDtoTest()
		{
			var dtos = new HashSet<DomainObjectDTO>();
			// 1. Add barebones LP.
			const string lpGuid = "9719A466-2240-4DEA-9722-9FE0746A30A6";
			var lpDto = CreatoDTO(dtos, lpGuid, "LangProject", null);
			const string lexDbGuid = "6C84F84A-5B99-4CF5-A7D5-A308DDC604E0";
			var ldbDto = CreatoDTO(dtos, lexDbGuid, "LexDb", lpGuid);
			IDomainObjectDTORepository dtoRepos = new DomainObjectDtoRepository(1, dtos, m_mdc, null, TestDirectoryFinder.LcmDirectories);

			Assert.IsNull(dtoRepos.GetOwningDTO(lpDto), "LP has owner?");
			var ldbOwner = dtoRepos.GetOwningDTO(ldbDto);
			Assert.IsNotNull(ldbOwner, "LexDB has no owner?");
			Assert.AreSame(lpDto, ldbOwner, "LP not owner of LexDB?");
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure new DTOs can be found.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void AddNewDtoTest()
		{
			var dtos = new HashSet<DomainObjectDTO>();
			// 1. Add barebones LP.
			const string lpGuid = "9719A466-2240-4DEA-9722-9FE0746A30A6";
			CreatoDTO(dtos, lpGuid, "LangProject", null);
			const string lexDbGuid = "6C84F84A-5B99-4CF5-A7D5-A308DDC604E0";
			CreatoDTO(dtos, lexDbGuid, "LexDb", null);
			IDomainObjectDTORepository dtoRepos = new DomainObjectDtoRepository(1, dtos, m_mdc, null, TestDirectoryFinder.LcmDirectories);

			// Create new DTO and add it.
			var newGuid = Guid.NewGuid();
			var newby = new DomainObjectDTO(newGuid.ToString(), "LexEntry", "<rt />");
			dtoRepos.Add(newby);
			Assert.AreSame(newby, dtoRepos.GetDTO(newGuid.ToString()), "Wrong new DTO from guid.");
			Assert.AreSame(newby, dtoRepos.AllInstancesSansSubclasses("LexEntry").First(), "Wrong new DTO from class.");
			Assert.IsTrue(((DomainObjectDtoRepository)dtoRepos).Newbies.Contains(newby), "Newby not in newbies set.");
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure deleted DTOs cannot be found.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void RemoveDtoTest()
		{
			var dtos = new HashSet<DomainObjectDTO>();
			// 1. Add barebones LP.
			const string lpGuid = "9719A466-2240-4DEA-9722-9FE0746A30A6";
			var lpDto = CreatoDTO(dtos, lpGuid, "LangProject", null);
			IDomainObjectDTORepository dtoRepos = new DomainObjectDtoRepository(1, dtos, m_mdc, null, TestDirectoryFinder.LcmDirectories);

			dtoRepos.Remove(lpDto);
			Assert.IsTrue(((DomainObjectDtoRepository)dtoRepos).Goners.Contains(lpDto), "Goner not in goners set.");
			Assert.IsNull(dtoRepos.AllInstancesSansSubclasses("LexEntry").FirstOrDefault(), "Found goner by class.");
			Assert.That(() => dtoRepos.GetDTO(lpDto.Guid),
				Throws.ArgumentException, "Found deleted DTO by guid.");
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure updated DTOs can be found.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void UpdateDtoTest()
		{
			var dtos = new HashSet<DomainObjectDTO>();
			// 1. Add barebones LP.
			const string lpGuid = "9719A466-2240-4DEA-9722-9FE0746A30A6";
			var lpDto = CreatoDTO(dtos, lpGuid, "LangProject", null);
			IDomainObjectDTORepository dtoRepos = new DomainObjectDtoRepository(1, dtos, m_mdc, null, TestDirectoryFinder.LcmDirectories);

			dtoRepos.Update(lpDto);
			Assert.IsTrue(((DomainObjectDtoRepository)dtoRepos).Dirtballs.Contains(lpDto), "Dirty DTO not in dirtball set.");
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure unknown updated DTOs is rejected.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void UpdateUnknownDtoTest()
		{
			var dtos = new HashSet<DomainObjectDTO>();
			// 1. Add barebones LP.
			const string lpGuid = "9719A466-2240-4DEA-9722-9FE0746A30A6";
			CreatoDTO(dtos, lpGuid, "LangProject", null);
			IDomainObjectDTORepository dtoRepos = new DomainObjectDtoRepository(1, dtos, m_mdc, null, TestDirectoryFinder.LcmDirectories);

			var newGuid = Guid.NewGuid();
			var newby = new DomainObjectDTO(newGuid.ToString(), "LexEntry", "<rt />");
			Assert.That(() => dtoRepos.Update(newby), Throws.TypeOf<InvalidOperationException>());
		}
	}
}