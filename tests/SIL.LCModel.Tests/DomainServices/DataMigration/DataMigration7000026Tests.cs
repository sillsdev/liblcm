// Copyright (c) 2015-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using NUnit.Framework;

namespace SIL.LCModel.DomainServices.DataMigration
{
	/// <summary>
	/// Test framework for migration from version 7000025 to 7000026.
	/// </summary>
	[TestFixture]
	public sealed class DataMigrationTests7000026 : DataMigrationTestsBase
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test the migration from version 7000025 to 7000026.
		/// (Merge the Sense Status list into the Status list)
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void DataMigration7000026Test()
		{
			//Bring in data from xml file.
			var dtos = DataMigrationTestServices.ParseProjectFile("DataMigration7000026Tests.xml");


			var mockMDC = new MockMDCForDataMigration();
			mockMDC.AddClass(1, "CmObject", null, new List<string> { "CmProject" });
			mockMDC.AddClass(2, "CmProject", "CmObject", new List<string> { "LangProject" });
			mockMDC.AddClass(3, "LangProject", "CmProject", new List<string>());

			IDomainObjectDTORepository dtoRepos = new DomainObjectDtoRepository(7000025, dtos, mockMDC, null,
				TestDirectoryFinder.LcmDirectories);

			//Before the migration there should be a ExtLinkRootDir element in the project.
			var langProjDto = dtoRepos.AllInstancesSansSubclasses("LangProject").First();
			var langProjElement = XElement.Parse(langProjDto.Xml);
			var langProjExtLinkRootDir = langProjElement.XPathSelectElement("ExtLinkRootDir");
			Assert.That(langProjExtLinkRootDir, Is.Not.Null, "Before the migration we should have a 'ExtLinkRootDir' element on LangProj");

			m_dataMigrationManager.PerformMigration(dtoRepos, 7000026, new DummyProgressDlg());

			//This object should contain a 'LinkedFilesRootDir' property
			langProjDto = dtoRepos.AllInstancesSansSubclasses("LangProject").First();
			langProjElement = XElement.Parse(langProjDto.Xml);
			var langProjLinkedFilesRootDir = langProjElement.XPathSelectElement("LinkedFilesRootDir");
			Assert.That(langProjLinkedFilesRootDir, Is.Not.Null, "We should now have a 'LinkedFilesRootDir' element on LangProj");
			//This object should not contain an 'AnalysysStatus' property
			langProjExtLinkRootDir = langProjElement.XPathSelectElement("ExtLinkRootDir");
			Assert.That(langProjExtLinkRootDir, Is.Null, "LangProject ExtLinkRootDir Property should not exist any more");


			Assert.AreEqual(7000026, dtoRepos.CurrentModelVersion, "Wrong updated version.");
		}
	}
}