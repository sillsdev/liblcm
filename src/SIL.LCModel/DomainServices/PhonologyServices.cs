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
using static Icu.Normalization.Normalizer2;
using SIL.LCModel.Core.KernelInterfaces;

namespace SIL.LCModel.DomainServices
{
	public class PhonologyServices
	{
		public PhonologyServices(LcmCache cache, string wsVernId = null)
		{
			Cache = cache;
			m_wsVernId = wsVernId;
		}

		public static string VersionId = "1";

		LcmCache Cache { get; set; }

		private readonly string m_wsVernId;

		/// <summary>
		/// Export the phonology to a file in the Phonology format.
		/// The Phonology format is similar to M3Dump except that strings are exported as multi-strings.
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
			// Import the Phonology format using ImportData.
			// ImportData has been extended to accept the Phonology format.
			XmlImportData xid = new XmlImportData(Cache, true);
			xid.ImportData(sFilename, null);
		}

		/// <summary>
		/// Import phonology from the given text reader.
		/// </summary>
		/// <param name="rdr"></param>
		public void ImportPhonologyFromXml(TextReader rdr)
		{
			// Import the Phonology format using ImportData.
			// ImportData has been extended to accept the Phonology format.
			XmlImportData xid = new XmlImportData(Cache, true);
			xid.ImportData(rdr, null, null);
			// NonUndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW(Cache.ActionHandlerAccessor,
				// () => AssignVernacularWritingSystemToDefaultPhPhonemes(Cache));
		}

		/// <summary>
		/// Clear PhonologicalData and Phonological Features.
		/// Don't clear boundary markers.
		/// </summary>
		public void DeletePhonology()
		{
			NonUndoableUnitOfWorkHelper.Do(Cache.ServiceLocator.GetInstance<IActionHandler>(), () =>
			{
				IPhPhonData phonData = Cache.LangProject.PhonologicalDataOA;
				// Delete what is covered by ImportPhonology.
				phonData.ContextsOS.Clear();
				phonData.EnvironmentsOS.Clear();
				phonData.FeatConstraintsOS.Clear();
				phonData.NaturalClassesOS.Clear();
				phonData.GetPhonemeSet().PhonemesOC.Clear();
				// Don't clear phonData.GetPhonemeSet().BoundaryMarkersOC!
				// They have GUIDs known to the code.
				phonData.PhonRulesOS.Clear();
				Cache.LanguageProject.PhFeatureSystemOA.TypesOC.Clear();
				Cache.LanguageProject.PhFeatureSystemOA.FeaturesOC.Clear();
			});
		}
	}
}
