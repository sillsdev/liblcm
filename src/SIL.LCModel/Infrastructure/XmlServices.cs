// Copyright (c) 2009-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.IO;
using System.Text;
using System.Xml;
using SIL.LCModel.Core.KernelInterfaces;

namespace SIL.LCModel.Infrastructure
{
	/// <summary>
	/// Handle conversion of string/Guids.
	/// </summary>
	internal static class GuidServices
	{
		/// <summary>
		/// Get a base 64 string representation of a Guid.
		/// </summary>
		/// <param name="guid"></param>
		/// <returns></returns>
		internal static string GetString(Guid guid)
		{
			return Convert.ToBase64String(guid.ToByteArray()).Replace("+", "-").Replace("/", ".");
		}

		/// <summary>
		/// Get a Guid from a base 64 string.
		/// </summary>
		/// <param name="guid"></param>
		/// <returns></returns>
		internal static Guid GetGuid(string guid)
		{
			return new Guid(Convert.FromBase64String(guid.Replace("-", "+").Replace(".", "/")));
		}
	}

	/// <summary>
	/// Services more suited to LCM than the world at large that are xml related.
	/// </summary>
	internal abstract class XmlServices
	{
		/// <summary>Default data version number if no version number was found in the data set</summary>
		public const int kDefaultDataVersion = 7000000;

		/// <summary>
		/// Create reader for an xml BEP file.
		/// </summary>
		/// <param name="xmlPathname"></param>
		/// <returns></returns>
		internal static XmlReader CreateReader(string xmlPathname)
		{
			return XmlReader.Create(xmlPathname, ReaderSettings);
		}

		/// <summary>
		/// Create reader for reconstituting surrogates.
		/// </summary>
		internal static XmlReader CreateReader(MemoryStream inputStream)
		{
			var settings = ReaderSettings;
			settings.ConformanceLevel = ConformanceLevel.Fragment; // Not whole document.
			XmlTextReader textReader = new XmlTextReader(inputStream);
			textReader.WhitespaceHandling = WhitespaceHandling.Significant;
			return XmlReader.Create(textReader, settings);
		}

		/// <summary>
		/// Create writer for a file.
		/// </summary>
		/// <param name="xmlPathname"></param>
		/// <returns></returns>
		internal static XmlWriter CreateWriter(string xmlPathname)
		{
			return XmlWriter.Create(xmlPathname, WriterSettings);
		}

		/// <summary>
		/// Create writer that uses a memory stream.
		/// </summary>
		/// <param name="outputStream"></param>
		/// <returns></returns>
		internal static XmlWriter CreateWriter(MemoryStream outputStream)
		{
			var settings = WriterSettings;
			settings.ConformanceLevel = ConformanceLevel.Fragment; // Not whole document.
			return XmlWriter.Create(outputStream, settings);
		}

		/// <summary>
		/// Get the version number from the reader.
		/// </summary>
		/// <param name="reader"></param>
		/// <returns>The model version number in the reader</returns>
		internal static int GetVersionNumber(XmlReader reader)
		{
			// Check the model version number;
			reader.MoveToAttribute("version");
			var currentDataStoreVersion = string.IsNullOrEmpty(reader.Value) ?
				kDefaultDataVersion : Int32.Parse(reader.Value);
			reader.MoveToElement();

			return currentDataStoreVersion;
		}

		internal static void WriteStartElement(XmlWriter writer, int currentVersion)
		{
			writer.WriteStartElement("languageproject");
			writer.WriteAttributeString("version", currentVersion.ToString());
		}

		/// <summary>
		/// Common settings for XML BEP reader.
		/// </summary>
		private static XmlReaderSettings ReaderSettings
		{
			get
			{
				return new XmlReaderSettings
						{
							CheckCharacters = false,
							ConformanceLevel = ConformanceLevel.Document,
							DtdProcessing = DtdProcessing.Parse,
							ValidationType = ValidationType.None,
							CloseInput = true,
							IgnoreWhitespace = true
						};
			}
		}

		/// <summary>
		/// Common writer settings for XML BEP.
		/// </summary>
		private static XmlWriterSettings WriterSettings
		{
			get
			{
				return new XmlWriterSettings
						{
							OmitXmlDeclaration = false,
							CheckCharacters = true,
							ConformanceLevel = ConformanceLevel.Document,
							Encoding = new UTF8Encoding(false),
							Indent = true,
							IndentChars = (""),
							NewLineOnAttributes = false
						};
			}
		}
	}

	/// <summary>
	/// Support class for loading data from BEPs.
	///
	/// This class simply holds commonly used objects that are found in the ServiceLocator.
	/// </summary>
	internal class LoadingServices
	{
		internal readonly IDataSetup m_dataSetup;
		internal readonly ICmObjectIdFactory m_objIdFactory;
		internal readonly IFwMetaDataCacheManaged m_mdcManaged;
		internal readonly ILgWritingSystemFactory m_wsf;
		internal readonly IUnitOfWorkService m_uowService;
		internal readonly ICmObjectSurrogateRepository m_surrRepository;
		internal readonly ICmObjectRepository m_cmObjRepository;

		/// <summary>
		/// Constructor
		/// </summary>
		internal LoadingServices(IDataSetup dataSetup, ICmObjectIdFactory objIdFactory,
			IFwMetaDataCacheManaged mdcManaged, ILgWritingSystemFactory wsf, IUnitOfWorkService uowService,
			ICmObjectSurrogateRepository surrRepository, ICmObjectRepository cmObjRepository)
		{
			m_dataSetup = dataSetup;
			m_objIdFactory = objIdFactory;
			m_mdcManaged = mdcManaged;
			m_wsf = wsf;
			m_uowService = uowService;
			m_surrRepository = surrRepository;
			m_cmObjRepository = cmObjRepository;
		}
	}
}
