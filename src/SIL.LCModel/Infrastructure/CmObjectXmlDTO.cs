// // Copyright (c) $year$ SIL International
// // This software is licensed under the LGPL, version 2.1 or later
// // (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

namespace SIL.LCModel.Infrastructure
{
	internal class CmObjectXmlDTO : ICmObjectDTO
	{

		private byte[] m_xml;
		public CmObjectXmlDTO(string xml)
		{
			m_xml = Encoding.UTF8.GetBytes(xml);
		}

		public CmObjectXmlDTO(byte[] xml)
		{
			m_xml = xml;
		}

		public byte[] XMLBytes => m_xml;

		public string XML
		{
			get
			{
				byte[] xmlBytes = m_xml; // Use local variable to prevent race condition
				return xmlBytes == null ? null : Encoding.UTF8.GetString(xmlBytes);
			}
		}

		public ICmObject Transfer(LcmCache cache, string className)
		{
			var rtElement = XElement.Parse(XML);
			var cmObject = (ICmObject)SurrogateConstructorInfo.ClassToConstructorInfo[className].Invoke(null);
			try
			{
				((ICmObjectInternal)cmObject).LoadFromDataStore(
					cache,
					rtElement,
					((IServiceLocatorInternal)cache.ServiceLocator).LoadingServices);
			}
			catch (InvalidOperationException)
			{
				// Asserting just so developers know that this is happening
				Debug.Assert(false, "See LT-13574: something is corrupt in this database.");
				// LT-13574 had a m_classname that was different from the that in rtElement.
				// That causes attributes to be leftover or missing - hence the exception.
				rtElement = XElement.Parse(XML); // rtElement is consumed in loading, so re-init
				var xmlClassName = rtElement.Attribute("class").Value;
				if (xmlClassName != className)
				{
					cmObject = (ICmObject)SurrogateConstructorInfo.ClassToConstructorInfo[xmlClassName].Invoke(null);
					((ICmObjectInternal)cmObject).LoadFromDataStore(
						cache,
						rtElement,
						((IServiceLocatorInternal)cache.ServiceLocator).LoadingServices);
				}
			}

			return cmObject;
		}
	}
}