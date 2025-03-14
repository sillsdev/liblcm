﻿// Copyright (c) 2010-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using NUnit.Framework;

namespace SIL.LCModel.DomainServices.DataMigration
{
	/// <summary>
	/// Test framework for migration from version 7000026 to 7000027.
	/// </summary>
	[TestFixture]
	public sealed class DataMigrationTests7000027 : DataMigrationTestsBase
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test the migration from version 7000026 to 7000027.
		/// (Set ParaContainingOrc property in ScrFootnote)
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void DataMigration7000027Test()
		{
			var dtos = new HashSet<DomainObjectXMLDTO>();
			var sb = new StringBuilder();
			// 1. Add barebones LP.
			sb.Append("<rt class=\"LangProject\" guid=\"9719A466-2240-4DEA-9722-9FE0746A30A6\">");
			sb.Append("<Texts>");
			StTextAndParaInfo lpTextsGuids = new StTextAndParaInfo("9719A466-2240-4DEA-9722-9FE0746A30A6", "Normal", false, false);
			sb.Append("<objsur guid=\"" + lpTextsGuids.textGuid + "\" t=\"o\" />");
			sb.Append("</Texts>");
			sb.Append("<TranslatedScripture>");
			sb.Append("<objsur guid=\"2c5c1f5f-1f08-41d7-99fe-23893ee4ceef\" t=\"o\" />");
			sb.Append("</TranslatedScripture>");
			sb.Append("</rt>");
			var lpDto = new DomainObjectXMLDTO("9719A466-2240-4DEA-9722-9FE0746A30A6",
				"LangProject", sb.ToString());
			dtos.Add(lpDto);

			// Add text dto.
			var txtDto = new DomainObjectXMLDTO(lpTextsGuids.textGuid.ToString(),  "StText",
				lpTextsGuids.textXml);
			dtos.Add(txtDto);
			// Add text para dto.
			var txtParaDto = new DomainObjectXMLDTO(lpTextsGuids.paraGuid.ToString(), "ScrTxtPara",
				lpTextsGuids.paraXml);
			dtos.Add(txtParaDto);

			// 2. Add Scripture
			sb = new StringBuilder();
			sb.Append("<rt class=\"Scripture\" guid=\"2c5c1f5f-1f08-41d7-99fe-23893ee4ceef\" ownerguid=\"9719A466-2240-4DEA-9722-9FE0746A30A6\" owningflid=\"6001040\" owningord=\"0\">");
			sb.Append("<Books>");
			sb.Append("<objsur guid=\"f213db11-7007-4a2f-9b94-06d6c96014ca\" t=\"o\" />");
			sb.Append("</Books>");
			sb.Append("</rt>");
			var scrDto = new DomainObjectXMLDTO("2c5c1f5f-1f08-41d7-99fe-23893ee4ceef", "Scripture",
				sb.ToString());
			dtos.Add(scrDto);

			// 3. Add a ScrBook
			sb = new StringBuilder();
			sb.Append("<rt class=\"ScrBook\" guid=\"f213db11-7007-4a2f-9b94-06d6c96014ca\" ownerguid=\"2c5c1f5f-1f08-41d7-99fe-23893ee4ceef\" owningflid=\"3001001\" owningord=\"0\">");
			sb.Append("<Name>");
			sb.Append("<AUni ws=\"fr\">Genesis</AUni>");
			sb.Append("</Name>");
			sb.Append("<Title>");
			StTextAndParaInfo titleTextGuids = new StTextAndParaInfo("f213db11-7007-4a2f-9b94-06d6c96014ca", "Title Main", true, false);
			sb.Append("<objsur guid=\"" + titleTextGuids.textGuid + "\" t=\"o\" />");
			sb.Append("</Title>");
			sb.Append("<Sections>");
			sb.Append("<objsur guid=\"834e1bf8-3a25-47d6-9f92-806b38b5f815\" t=\"o\" />");
			sb.Append("</Sections>");
			sb.Append("<Footnotes>");
			StTextAndParaInfo footnoteGuids = new StTextAndParaInfo("ScrFootnote", "f213db11-7007-4a2f-9b94-06d6c96014ca", "Title Main", null, true, false);
			sb.Append("<objsur guid=\"" + footnoteGuids.textGuid + "\" t=\"o\" />");
			sb.Append("</Footnotes>");
			sb.Append("</rt>");
			var bookDto = new DomainObjectXMLDTO("f213db11-7007-4a2f-9b94-06d6c96014ca", "ScrBook", sb.ToString());
			dtos.Add(bookDto);

			// Add title
			var titleDto = new DomainObjectXMLDTO(titleTextGuids.textGuid.ToString(), "StText",
				titleTextGuids.textXml);
			dtos.Add(titleDto);
			// Title para
			var titleParaDto = new DomainObjectXMLDTO(titleTextGuids.paraGuid.ToString(), "ScrTxtPara",
				titleTextGuids.paraXml);
			dtos.Add(titleParaDto);

			// Add footnote
			var footnoteDto = new DomainObjectXMLDTO(footnoteGuids.textGuid.ToString(), "ScrFootnote",
				footnoteGuids.textXml);
			dtos.Add(footnoteDto);
			// Footnote para
			var footnoteParaDto = new DomainObjectXMLDTO(footnoteGuids.paraGuid.ToString(), "ScrTxtPara",
				footnoteGuids.paraXml);
			dtos.Add(footnoteParaDto);

			// 4. Add a section to the book
			sb = new StringBuilder();
			sb.Append("<rt class=\"ScrSection\" guid=\"834e1bf8-3a25-47d6-9f92-806b38b5f815\" ownerguid=\"f213db11-7007-4a2f-9b94-06d6c96014ca\" owningflid=\"3002001\" owningord=\"0\">");
			sb.Append("<Content>");
			StTextAndParaInfo contentsTextGuids = new StTextAndParaInfo("StText", "834e1bf8-3a25-47d6-9f92-806b38b5f815", "Paragraph",
				"<Run ws=\"fr\" ownlink=\"" + footnoteGuids.textGuid + "\"></Run>", true, false);
			sb.Append("<objsur guid=\"" + contentsTextGuids.textGuid + "\" t=\"o\" />");
			sb.Append("</Content>");
			sb.Append("<Heading>");
			StTextAndParaInfo headingTextGuids = new StTextAndParaInfo("834e1bf8-3a25-47d6-9f92-806b38b5f815", "Section Head", true, false);
			sb.Append("<objsur guid=\"" + headingTextGuids.textGuid + "\" t=\"o\" />");
			sb.Append("</Heading>");
			sb.Append("</rt>");
			var sectionDto = new DomainObjectXMLDTO("834e1bf8-3a25-47d6-9f92-806b38b5f815", "ScrSection",
				sb.ToString());
			dtos.Add(sectionDto);

			// Add the contents
			var contentsDto = new DomainObjectXMLDTO(contentsTextGuids.textGuid.ToString(), "StText",
				contentsTextGuids.textXml);
			dtos.Add(contentsDto);
			// Contents para
			var contentsParaDto = new DomainObjectXMLDTO(contentsTextGuids.paraGuid.ToString(), "ScrTxtPara",
				contentsTextGuids.paraXml);
			dtos.Add(contentsParaDto);

			// Add the heading to the xml
			var headingDto = new DomainObjectXMLDTO(headingTextGuids.textGuid.ToString(), "StText",
				headingTextGuids.textXml);
			dtos.Add(headingDto);
			// heading para
			var headingParaDto = new DomainObjectXMLDTO(headingTextGuids.paraGuid.ToString(), "ScrTxtPara",
				headingTextGuids.paraXml);
			dtos.Add(headingParaDto);

			// Set up mock MDC.
			var mockMDC = new MockMDCForDataMigration();
			mockMDC.AddClass(1, "CmObject", null, new List<string> { "LangProject", "StText", "Scripture",
				"ScrBook", "StFootnote", "ScrSection", "StPara" });
			mockMDC.AddClass(2, "LangProject", "CmObject", new List<string>());
			mockMDC.AddClass(3, "StText", "CmObject", new List<string> { "StFootnote" });
			mockMDC.AddClass(4, "Scripture", "CmObject", new List<string>());
			mockMDC.AddClass(5, "ScrBook", "CmObject", new List<string>());
			mockMDC.AddClass(6, "StFootnote", "StText", new List<string> { "ScrFootnote" });
			mockMDC.AddClass(7, "ScrSection", "CmObject", new List<string>());
			mockMDC.AddClass(8, "StTxtPara", "StPara", new List<string> { "ScrTxtPara" });
			mockMDC.AddClass(9, "ScrFootnote", "StFootnote", new List<string>());
			mockMDC.AddClass(10, "ScrTxtPara", "StTxtPara", new List<string>());
			mockMDC.AddClass(11, "StPara", "CmObject", new List<string> { "StTxtPara" });
			IDomainObjectDTORepository dtoRepos = new DomainObjectDtoRepository(7000026, dtos, mockMDC, null,
				TestDirectoryFinder.LcmDirectories);

			m_dataMigrationManager.PerformMigration(dtoRepos, 7000027, new DummyProgressDlg());
			Assert.AreEqual(7000027, dtoRepos.CurrentModelVersion, "Wrong updated version.");

			// Check that the ParaContainingOrc property in footnotes is set
			DomainObjectXMLDTO footnoteDTO = dtoRepos.GetDTO(footnoteGuids.textGuid.ToString());
			XElement footnote = XElement.Parse(footnoteDTO.Xml);
			XElement paraContainingOrc = footnote.Element("ParaContainingOrc");
			Assert.IsNotNull(paraContainingOrc);
			XElement objRef = paraContainingOrc.Element("objsur");
			Assert.IsNotNull(objRef);
			Assert.AreEqual(contentsTextGuids.paraGuid.ToString(), objRef.Attribute("guid").Value);
			Assert.AreEqual("r", objRef.Attribute("t").Value);
		}
	}
}