using SIL.LCModel.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using SIL.LCModel.Application.ApplicationServices;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Infrastructure.Impl;

namespace SIL.LCModel.DomainServices
{
	public class PhonologyServices
	{
		public PhonologyServices(LcmCache cache)
		{
			Cache = cache;
		}

		LcmCache Cache { get; set; }

		/// <summary>
		/// Export the phonology to a file.
		/// </summary>
		/// <param name="filename"></param>
		public void ExportPhonologyAsXml(string filename)
		{
			var xml = ExportPhonologyAsXml();
			xml.Save(filename);
		}

		/// <summary>
		/// Export the phonology as an XML Document.
		/// </summary>
		/// <returns></returns>
		public XDocument ExportPhonologyAsXml()
		{
			return M3ModelExportServices.ExportPhonology(Cache.LanguageProject);
		}

		/// <summary>
		/// Import phonology from the give XML file.
		/// </summary>
		/// <param name="sFilename"></param>
		public void ImportPhonologyFromXml(string sFilename)
		{
			var streamReader = new StreamReader(sFilename, Encoding.UTF8);
			ImportPhonologyFromXml(streamReader);
		}

		/// <summary>
		/// Import phonology from the given text reader.
		/// </summary>
		/// <param name="rdr"></param>
		public void ImportPhonologyFromXml(TextReader rdr)
		{
			XmlImportData xid = new XmlImportData(Cache, true);
			xid.ImportData(rdr, null, null);
			NonUndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW(Cache.ActionHandlerAccessor,
				() => AssignVernacularWritingSystemToDefaultPhPhonemes(Cache));
		}

		private void AssignVernacularWritingSystemToDefaultPhPhonemes(LcmCache cache)
		{
			// For all PhCodes in the default phoneme set, change the writing system from "en" to icuLocale
			if (cache.LanguageProject.PhonologicalDataOA.PhonemeSetsOS.Count == 0)
				return;
			var phSet = cache.LanguageProject.PhonologicalDataOA.PhonemeSetsOS[0];
			int wsVern = cache.DefaultVernWs;
			foreach (var phone in phSet.PhonemesOC)
			{
				foreach (var code in phone.CodesOS)
				{

					code.Representation.VernacularDefaultWritingSystem =
						TsStringUtils.MakeString(code.Representation.UserDefaultWritingSystem.Text, wsVern);
				}
				phone.Name.VernacularDefaultWritingSystem =
					TsStringUtils.MakeString(phone.Name.UserDefaultWritingSystem.Text, wsVern);
			}
			foreach (var mrkr in phSet.BoundaryMarkersOC)
			{
				foreach (var code in mrkr.CodesOS)
				{
					code.Representation.VernacularDefaultWritingSystem =
						TsStringUtils.MakeString(code.Representation.UserDefaultWritingSystem.Text, wsVern);
				}
				mrkr.Name.VernacularDefaultWritingSystem =
					TsStringUtils.MakeString(mrkr.Name.UserDefaultWritingSystem.Text, wsVern);
			}
		}


	}
}
