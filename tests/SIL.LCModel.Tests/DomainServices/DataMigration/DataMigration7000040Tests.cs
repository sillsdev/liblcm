// Copyright (c) 2015-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using SIL.LCModel.Utils;

namespace SIL.LCModel.DomainServices.DataMigration
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Test framework for migration from version 7000036 to 7000037.  This migration fixes a
	/// data conversion problem for externalLink attributes in Run elements coming from
	/// FieldWorks 6.0 into FieldWorks 7.0+.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[TestFixture]
	public class DataMigration7000040Tests : DataMigrationTestsBase
	{
		///--------------------------------------------------------------------------------------
		/// <summary>
		///  Test the migration from version 7000039 to 7000040.
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void DataMigration7000040Test()
		{
			// Bring in data from xml file.
			var dtos = DataMigrationTestServices.ParseProjectFile("DataMigration7000040.xml");

			// Create all the Mock classes for the classes in my test data.
			var mockMDC = new MockMDCForDataMigration();
			mockMDC.AddClass(1, "CmObject", null, new List<string> { "LexEntryRef"});
			mockMDC.AddClass(2, "LexEntryRef", "CmObject", new List<string>());
			mockMDC.AddClass(3, "LexEntry", "CmObject", new List<string>());

			IDomainObjectDTORepository dtoRepos = new DomainObjectDtoRepository(7000039, dtos, mockMDC,
				FileUtils.ChangePathToPlatform("C:\\WW\\DistFiles\\Projects\\TokPisin"), TestDirectoryFinder.LcmDirectories);
			// Do Migration
			m_dataMigrationManager.PerformMigration(dtoRepos, 7000040, new DummyProgressDlg());

			// Check that the version was updated.
			Assert.AreEqual(7000040, dtoRepos.CurrentModelVersion, "Wrong updated version.");

			// The empty element should have stayed that way.
			VerifyEntryRef(dtoRepos, "82CB0EC6-B542-4FB8-B5B6-41398790DDF9", 0, 0);

			// The one with no primary lexemes should have no ShowComplexFormsIn
			VerifyEntryRef(dtoRepos, "c1ecaa73-e382-11de-8a39-0800200c9a66", 1, 0);

			// The one with one primary lexemes should have one ShowComplexFormsIn (and ComponentLexemes and PrimaryLexemes unchanged)
			var osElements = VerifyEntryRef(dtoRepos, "BF274D15-406E-4816-A81F-9B8C70AEF8E5", 2, 1);
			Assert.That(osElements.Count(), Is.EqualTo(1));
			VerifyObjSur(osElements, 0, "BFC76313-B9E4-4A31-8408-F854D7709E68");

			// The one with two primary lexemes should have two in ShowComplexFormsIn (and ComponentLexemes and PrimaryLexemes unchanged)
			osElements = VerifyEntryRef(dtoRepos, "B854113C-4B99-46FF-A9F6-ED3F0245E259", 2, 2);
			VerifyObjSur(osElements, 0, "6704EB7A-EFEA-42E4-BFAA-9B99B1D45D6F");
			VerifyObjSur(osElements, 1, "BFC76313-B9E4-4A31-8408-F854D7709E68");
		}

		private XElement[] VerifyEntryRef(IDomainObjectDTORepository dtoRepos, string guid, int cfCount, int showComplexFormsInCount)
		{
			var dto = dtoRepos.GetDTO(guid);
			var xElt = XElement.Parse(dto.Xml);
			SurrogatesIn(xElt, "ComponentLexemes", cfCount);
			SurrogatesIn(xElt, "PrimaryLexemes", showComplexFormsInCount);
			var cfElements = SurrogatesIn(xElt, "ShowComplexFormsIn", showComplexFormsInCount);
			return cfElements.ToArray();
		}

		/// <summary>
		/// Look for a child of xElt of the given name. If count is zero, it is allowed not to exist.
		/// Otherwise, it must have the specified number of children of name objsur; return them.
		/// </summary>
		IEnumerable<XElement> SurrogatesIn(XElement xElt, string name, int count)
		{
			var parent = xElt.Elements(name);
			if (parent.Count() == 0)
			{
				Assert.That(0, Is.EqualTo(count));
				return new XElement[0];
			}
			var objSurElts = parent.Elements("objsur");
			Assert.That(objSurElts.Count(), Is.EqualTo(count));
			return objSurElts;
		}

		private void VerifyObjSur(IEnumerable<XElement> osElements, int index, string guid)
		{
			var objsur = osElements.ToArray()[index];
			Assert.That(objsur.Attribute("guid").Value, Is.EqualTo(guid));
			Assert.That(objsur.Attribute("t").Value, Is.EqualTo("r"));
		}
	}
}
