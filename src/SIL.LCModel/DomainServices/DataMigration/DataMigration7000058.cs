﻿// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Xml.Linq;
using System.Xml.XPath;

namespace SIL.LCModel.DomainServices.DataMigration
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Migrate data from 7000057 to 7000058.
	///
	/// Data migration to change Irregularly Inflected Form variant types to class LexEntryInflType (for LT-7581).
	/// </summary>
	/// <remarks>
	/// Actually, this DM will try to delete the old one and
	/// </remarks>
	/// ----------------------------------------------------------------------------------------
	internal class DataMigration7000058 : IDataMigration
	{
		#region IDataMigration Members

		public void PerformMigration(IDomainObjectDTORepository domainObjectDtoRepository)
		{
			DataMigrationServices.CheckVersionNumber(domainObjectDtoRepository, 7000057);
			{
				// LT-13312 Note some projects may not have these guids.
				DomainObjectXMLDTO dtoVariantType;
				if (domainObjectDtoRepository.TryGetValue(LexEntryTypeTags.kguidLexTypPluralVar.ToString(), out dtoVariantType))
					AddGlossAppendIfEmpty(domainObjectDtoRepository, dtoVariantType, ".pl");
				if (domainObjectDtoRepository.TryGetValue(LexEntryTypeTags.kguidLexTypPastVar.ToString(), out dtoVariantType))
					AddGlossAppendIfEmpty(domainObjectDtoRepository, dtoVariantType, ".pst");
			}
			DataMigrationServices.IncrementVersionNumber(domainObjectDtoRepository);
		}

		static private void AddGlossAppendIfEmpty(IDomainObjectDTORepository dtoRepo, DomainObjectXMLDTO dtoToChange, string glossAppend)
		{
			XElement dtoToChangeElt = XElement.Parse(dtoToChange.Xml);
			XElement glossAppendElt = dtoToChangeElt.XPathSelectElement("GlossAppend");
			if (glossAppendElt == null)
			{
				dtoToChangeElt.Add(XElement.Parse("<GlossAppend/>"));
				glossAppendElt = dtoToChangeElt.XPathSelectElement("GlossAppend");
			}
			XElement aUniElt = glossAppendElt.XPathSelectElement("AUni[@ws='en']");
			if (aUniElt == null)
			{
				glossAppendElt.Add(XElement.Parse("<AUni ws='en'/>"));
				aUniElt = glossAppendElt.XPathSelectElement("AUni[@ws='en']");
			}
			if (aUniElt.Value.Trim().Length == 0)
			{
				aUniElt.Value = glossAppend;
			}

			DataMigrationServices.UpdateDTO(dtoRepo, dtoToChange, dtoToChangeElt.ToString());
		}

		#endregion
	}
}
