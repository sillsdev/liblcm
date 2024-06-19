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
		}
	}
}
