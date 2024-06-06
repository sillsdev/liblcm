using SIL.LCModel.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

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
		/// Export the current phonology to filename.
		/// </summary>
		/// <param name="filename"></param>
		public void ExportXml(string filename)
		{
			IPhPhonData phonology = Cache.LanguageProject.PhonologicalDataOA;
			string xml = ((ICmObjectInternal)phonology).ToXmlString();
			using (StreamWriter outputFile = new StreamWriter(filename))
			{
				outputFile.WriteLine(xml);
			}

		}

		/// <summary>
		/// Import phonology from filename.
		/// </summary>
		/// <param name="filename"></param>
		public void ImportXml(string filename)
		{
			IPhPhonData phonology = Cache.LanguageProject.PhonologicalDataOA;
			LoadingServices services = ((IServiceLocatorInternal)Cache.ServiceLocator).LoadingServices;
			XElement element = XElement.Load(filename);
			((ICmObjectInternal)phonology).LoadFromDataStore(Cache, element, services);
		}
	}
}
