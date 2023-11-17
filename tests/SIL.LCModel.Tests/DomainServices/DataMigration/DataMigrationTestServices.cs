// Copyright (c) 2015-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace SIL.LCModel.DomainServices.DataMigration
{
	/// <summary>
	/// Services for data migration tests.
	/// </summary>
	internal static class DataMigrationTestServices
	{
		internal static void CheckDtoRemoved(IDomainObjectDTORepository dtoRepos, DomainObjectXMLDTO goner)
		{
			DomainObjectXMLDTO dto;
			if (dtoRepos.TryGetValue(goner.Guid, out dto))
			{
				Assert.Fail("Still has deleted (or zombie) DTO.");
			}
			Assert.IsTrue(((DomainObjectDtoRepository)dtoRepos).Goners.Contains(goner));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Parses a project file and generates a set of DTO objects contained in the project.
		/// It looks in the TestData directory for the specified project file.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		internal static HashSet<DomainObjectXMLDTO> ParseProjectFile(string filename)
		{
			var lpElement = XElement.Load(Path.Combine(TestDirectoryFinder.TestDataDirectory, filename));
			return new HashSet<DomainObjectXMLDTO>(
				from elem in lpElement.Elements("rt")
				select new DomainObjectXMLDTO(elem.Attribute("guid").Value, elem.Attribute("class").Value,
										   elem.ToString()));
		}
	}
}
