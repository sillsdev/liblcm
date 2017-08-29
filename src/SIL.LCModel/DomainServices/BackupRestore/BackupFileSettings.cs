// Copyright (c) 2010-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ICSharpCode.SharpZipLib;
using ICSharpCode.SharpZipLib.Zip;
using SIL.LCModel.Infrastructure.Impl;
using SIL.LCModel.Utils;

namespace SIL.LCModel.DomainServices.BackupRestore
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Settings representing an existing backup file. These settings are needed to populate
	/// RestoreProjectDlg with the information related to the project backed up in the zip file.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[DataContract]
	[Serializable]
	public class BackupFileSettings : IBackupSettings
	{
		#region Data members
		private readonly string m_sZipFileName;
		private DateTime m_backupTime;
		private string m_comment;
		private string m_fwVersion;
		private string m_projectName;
		private string m_projectPathPersisted;
		private string m_linkedFilesPathRelative;
		private string m_linkedFilesPathActual;
		private int m_dbVersion;
		private bool m_configurationSettings;
		private bool m_linkedFiles;
		private bool m_supportingFiles;
		private bool m_spellCheckAdditions;
		#endregion

		#region Constructors
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="BackupFileSettings"/> class.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private BackupFileSettings()
		{
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="BackupFileSettings"/> class, performing
		/// validation on the zip file (will throw an exception if invalid).
		/// </summary>
		/// <param name="sZipFileName">Path of the zip file.</param>
		/// <exception cref="IOException">File does not exist, or some such problem</exception>
		/// <exception cref="InvalidBackupFileException">XML deserialization problem or required
		/// files not found in zip file</exception>
		/// ------------------------------------------------------------------------------------
		public BackupFileSettings(string sZipFileName) : this(sZipFileName, true)
		{
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="BackupFileSettings"/> class.
		/// </summary>
		/// <param name="sZipFileName">Path of the zip file.</param>
		/// <param name="doValidation">if set to <c>true</c> do validation to ensure the zip
		/// file exists, is readable, and contains the basic components needed to perform a
		/// restore.</param>
		/// <exception cref="IOException">File does not exist, or some such problem</exception>
		/// <exception cref="InvalidBackupFileException">XML deserialization problem or required
		/// files not found in zip file</exception>
		/// ------------------------------------------------------------------------------------
		public BackupFileSettings(string sZipFileName, bool doValidation)
		{
			m_sZipFileName = sZipFileName;
			if (doValidation)
				Validate();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="BackupFileSettings"/> class, which
		/// does not do validation because it is intended to just represent a backup file until
		/// later, when someone requests a property that requires the file to be read.
		/// </summary>
		/// <param name="sZipFileName">Path of the zip file.</param>
		/// <param name="backupTime">The backup time.</param>
		/// <param name="comment">The comment (typically gleaned from the file name)</param>
		/// ------------------------------------------------------------------------------------
		internal BackupFileSettings(string sZipFileName, DateTime backupTime, string comment) :
			this(sZipFileName, false)
		{
			m_backupTime = backupTime;
			m_comment = comment;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="BackupFileSettings"/> class from an
		/// existing BackupProjectSettings object
		/// </summary>
		/// <param name="settings">The BackupProjectSettings.</param>
		/// ------------------------------------------------------------------------------------
		private BackupFileSettings(BackupProjectSettings settings)
		{
			m_backupTime = settings.BackupTime;
			m_comment = settings.Comment;
			m_configurationSettings = settings.IncludeConfigurationSettings;
			m_supportingFiles = settings.IncludeSupportingFiles;
			m_linkedFiles = settings.IncludeLinkedFiles;
			m_projectName = settings.ProjectName;
			m_projectPathPersisted = LcmFileHelper.GetPathWithoutRoot(settings.ProjectPath);
			m_spellCheckAdditions = settings.IncludeSpellCheckAdditions;
			m_dbVersion = settings.DbVersion;
			m_fwVersion = settings.FwVersion;
			m_linkedFilesPathRelative = LinkedFilesRelativePathHelper.GetLinkedFilesRelativePathFromFullPath(settings.ProjectsRootFolder, settings.LinkedFilesPath, settings.ProjectPath, settings.ProjectName);
			m_linkedFilesPathActual = settings.LinkedFilesPath;
		}
		#endregion

		#region Serialization and Deserialization
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Writes out the given backup settings to the given stream, which will normally
		/// be a file, but for testing we can use a memory stream.
		/// </summary>
		/// <param name="settings">The settings to persist.</param>
		/// <param name="stream">The persistence stream.</param>
		/// ------------------------------------------------------------------------------------
		public static void SaveToStream(BackupProjectSettings settings, Stream stream)
		{
			DataContractSerializer serializer = new DataContractSerializer(typeof(BackupFileSettings));
			serializer.WriteObject(stream, new BackupFileSettings(settings));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates a new BackupFileSettings object, initialized from the stream, which must
		/// be the same format as created by SaveToStream
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <returns>A new BackupFileSettings object, initialized from the stream</returns>
		/// ------------------------------------------------------------------------------------
		public static BackupFileSettings CreateFromStream(Stream stream)
		{
			DataContractSerializer serializer = new DataContractSerializer(typeof(BackupFileSettings));
			return (BackupFileSettings)serializer.ReadObject(stream);
		}

		/// <summary>
		/// Creates a new BackupFileSettings object, initialized from the Xml of a Backup Settings
		/// file. This handles the case where Backup Settings files are created before FLEx 9.0
		/// </summary>
		/// <param name="backupInXml">The string containing the Backup Settings Xml</param>
		/// <returns></returns>
		public static BackupFileSettings CreateFromXml(string backupInXml)
		{
			XDocument backupXDocument = XDocument.Parse(backupInXml);
			XmlReader xmlReader = backupXDocument.CreateReader();
			DataContractSerializer serializer = new DataContractSerializer(typeof(BackupFileSettings));
			return (BackupFileSettings) serializer.ReadObject(xmlReader);
		}
		#endregion

		#region Properties
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the path for the zip File for the backup represented by these settings.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public string File
		{
			get { return m_sZipFileName; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets a value indicating whether linked files are available in the backup represented
		/// by these settings.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public bool LinkedFilesAvailable
		{
			get
			{
				return IncludeLinkedFiles && !string.IsNullOrEmpty(LinkedFilesPathActualPersisted) &&
					!string.IsNullOrEmpty(LinkedFilesPathRelativePersisted);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// The version of the database in the backup
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[DataMember(IsRequired = true)]
		private int DbVersion
		{
			get { PopulateSettingsFromZipFileIfNeeded(); return m_dbVersion; }
			set { m_dbVersion = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// The FieldWorks version used to backup the file
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[DataMember(IsRequired = true)]
		private string FwVersion
		{
			get { PopulateSettingsFromZipFileIfNeeded(); return m_fwVersion; }
			set { m_fwVersion = value; }
		}
		#endregion

		#region Public methods
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Validates this instance.
		/// </summary>
		/// <exception cref="IOException">File does not exist, or some such problem</exception>
		/// <exception cref="InvalidBackupFileException">XML deserialization problem or required
		/// files not found in zip file</exception>
		/// ------------------------------------------------------------------------------------
		public void Validate()
		{
			PopulateSettingsFromZipFileIfNeeded();
			if (DbVersion > BackendProvider.ModelVersion)
			{
				throw new InvalidBackupFileException(string.Format(Strings.ksBackupFileCreatedByNewerFwVersion,
					m_sZipFileName, FwVersion));
			}
		}
		#endregion

		#region IBackupSettings Members
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// The date and time of the backup
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[DataMember]
		public DateTime BackupTime
		{
			get
			{
				if (m_backupTime == DateTime.MinValue)
					PopulateSettingsFromZipFileIfNeeded();
				return m_backupTime.ToTheMinute();
			}
			private set { m_backupTime = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// User's description of a particular back-up instance
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[DataMember]
		public string Comment
		{
			get
			{
				if (m_comment == null)
					PopulateSettingsFromZipFileIfNeeded();
				return m_comment;
			}
			private set { m_comment = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// This is the name of the backed up project.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[DataMember(IsRequired = true)]
		public string ProjectName
		{
			get { PopulateSettingsFromZipFileIfNeeded(); return m_projectName; }
			private set { m_projectName = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// This is the name of the backed up project.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[DataMember] // Can't be required for backwards compatability with older backups
		public string ProjectPathPersisted
		{
			get { PopulateSettingsFromZipFileIfNeeded(); return m_projectPathPersisted; }
			private set { m_projectPathPersisted = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// This will be needed to restore files.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[DataMember] // Can't be required for backwards compatability with older backups (FWR-2245)
		public string LinkedFilesPathActualPersisted
		{
			get { PopulateSettingsFromZipFileIfNeeded(); return m_linkedFilesPathActual; }
			private set { m_linkedFilesPathActual = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// This will be needed to restore files.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[DataMember] // Can't be required for backwards compatability with older backups (FWR-2245)
		public string LinkedFilesPathRelativePersisted
		{
			get { PopulateSettingsFromZipFileIfNeeded(); return m_linkedFilesPathRelative; }
			private set { m_linkedFilesPathRelative = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Whether or not field visibilities, columns, dictionary layout, interlinear, etc.
		/// settings are included in the backup.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[DataMember(Name = "ConfigurationSettings")]
		public bool IncludeConfigurationSettings
		{
			get { PopulateSettingsFromZipFileIfNeeded(); return m_configurationSettings; }
			private set { m_configurationSettings = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Whether or not pictures and sound files are included in the backup.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[DataMember(Name = "LinkedFiles")]
		public bool IncludeLinkedFiles
		{
			get { PopulateSettingsFromZipFileIfNeeded(); return m_linkedFiles; }
			private set { m_linkedFiles = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Whether or not spell checking additions are included in the backup.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[DataMember(Name = "SpellCheckAdditions")]
		public bool IncludeSpellCheckAdditions
		{
			get { PopulateSettingsFromZipFileIfNeeded(); return m_spellCheckAdditions; }
			private set { m_spellCheckAdditions = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Whether or not the files in the SupportingFiles folder are included in the backup/restore.
		/// </summary>
		/// <value></value>
		/// ------------------------------------------------------------------------------------
		[DataMember(Name = "SupportingFiles")]
		public bool IncludeSupportingFiles
		{
			get { PopulateSettingsFromZipFileIfNeeded(); return m_supportingFiles; }
			private set { m_supportingFiles = value; }
		}
		#endregion

		#region Private helper methods
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Extract the backup settings from the zipfile.
		/// </summary>
		/// <exception cref="IOException">File does not exist, or some such problem</exception>
		/// <exception cref="InvalidBackupFileException">XML deserialization problem or required
		/// files not found in zip file</exception>
		/// ------------------------------------------------------------------------------------
		private void PopulateSettingsFromZipFileIfNeeded()
		{
			if (m_projectName != null)
				return;

			string extension = Path.GetExtension(m_sZipFileName).ToLowerInvariant();
			if (extension == LcmFileHelper.ksFw60BackupFileExtension)
			{
				ProcessOldZipFile();
				return;
			}
			if (extension == ".xml")
			{
				ProcessOldXmlFile();
				return;
			}

			using (var zipIn = new ZipInputStream(FileUtils.OpenStreamForRead(m_sZipFileName)))
			{
				try
				{
					ZipEntry entry;
					bool foundBackupSettingsFile = false;
					bool foundWritingSystemFiles = false;
					string dataFileName = null;

					while ((entry = zipIn.GetNextEntry()) != null)
					{
						string fileName = Path.GetFileName(entry.Name);

						if (String.IsNullOrEmpty(fileName))
							continue;
						if (fileName.Equals(LcmFileHelper.ksBackupSettingsFilename))
						{
							if (foundBackupSettingsFile)
								throw new InvalidOperationException("Zip file " + m_sZipFileName + " contained multiple " +
									LcmFileHelper.ksBackupSettingsFilename + " files.");
							foundBackupSettingsFile = true;
							try
							{
								InitializeFromStream(zipIn);
							}
							// handles the case where the backup settings xml file was create before FLEx 9.0
							catch (SerializationException)
							{
								InitializeFromZipEntry(entry);
							}
						}
						else if (Path.GetExtension(fileName) == LcmFileHelper.ksFwDataXmlFileExtension)
						{
							if (dataFileName != null)
								throw new InvalidOperationException("Zip file " + m_sZipFileName +
									" contained multiple project data files.");
							dataFileName = fileName;
						}
						else if (!entry.Name.EndsWith("/") && entry.Name.Contains(LcmFileHelper.ksWritingSystemsDir + "/"))
							foundWritingSystemFiles = true;
					}

					if (!foundBackupSettingsFile)
						throw new InvalidOperationException("Zip file " + m_sZipFileName + " did not contain the " +
							LcmFileHelper.ksBackupSettingsFilename + " file.");
					if (m_projectName == null)
						throw new InvalidOperationException(LcmFileHelper.ksBackupSettingsFilename + " in " +
							m_sZipFileName + " did not contain a project name.");
					string expectedProjectFile = LcmFileHelper.GetXmlDataFileName(m_projectName);
					if (dataFileName == null || dataFileName != expectedProjectFile)
						throw new InvalidOperationException("Zip file " + m_sZipFileName + " did not contain the " +
							expectedProjectFile + " file.");
					if (!foundWritingSystemFiles)
						throw new InvalidOperationException("Zip file " + m_sZipFileName +
							" did not contain any writing system files.");
				}
				catch (Exception e)
				{
					if (e is SharpZipBaseException || e is InvalidOperationException || e is SerializationException)
						throw new InvalidBackupFileException(m_sZipFileName, e);
					throw;
				}
			}
		}

		private void ProcessOldZipFile()
		{
			int cBakFiles = 0;
			int cHeaderFiles = 0;
			int cXml = 0;
			int cUnknown = 0;
			try
			{
				using (var zipFile = new ZipFile(m_sZipFileName))
				{
				foreach (ZipEntry entry in zipFile)
				{
					if (Path.HasExtension(entry.Name))
					{
						string extension = Path.GetExtension(entry.Name).ToLower();
						if (extension == ".bak")
						{
							++cBakFiles;
							m_projectName = Path.GetFileNameWithoutExtension(entry.Name);
							m_backupTime = entry.DateTime;
							m_fwVersion = null;
							m_dbVersion = 0;
							m_comment = Properties.Resources.kstidFw60OrEarlierBackupComment;
						}
						else if (extension == ".xml")
						{
							++cXml;
						}
						else
						{
							++cUnknown;
						}
					}
					else
					{
						if (entry.Name == "header")
							++cHeaderFiles;
						else
							++cUnknown;
					}
				}
			}
			}
			catch (Exception e)
			{
				if (e is SharpZipBaseException || e is InvalidOperationException || e is SerializationException)
					throw new InvalidBackupFileException(m_sZipFileName, e);
			}
			if (cBakFiles != 1 || cHeaderFiles != 1 || cXml > 1 || cUnknown > 0)
				throw new InvalidBackupFileException(m_sZipFileName, null);
		}

		private void ProcessOldXmlFile()
		{
			int version = DataMigration.ImportFrom6_0.FieldWorksXmlVersion(m_sZipFileName);
			if (version < DataMigration.ImportFrom6_0.FieldWorks6DbVersion)
				throw new InvalidBackupFileException(m_sZipFileName, null);

			m_projectName = Path.GetFileNameWithoutExtension(m_sZipFileName);
			m_backupTime = System.IO.File.GetCreationTime(m_sZipFileName);
			m_fwVersion = null;
			m_dbVersion = version;
			m_comment = Properties.Resources.kstidFw60XmlBackupComment;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Reads in backup settings from the given stream, which will normally be a file,
		/// but for testing we can use a memory stream.
		/// </summary>
		/// <param name="persistenceStream">The persistence stream.</param>
		/// <exception cref="InvalidOperationException">An error occurred during deserialization.
		/// </exception>
		/// <exception cref="SerializationException">Deserialization issue, likely from a mismatching namespace.
		/// </exception>
		/// ------------------------------------------------------------------------------------
		private void InitializeFromStream(Stream persistenceStream)
		{
			BackupFileSettings settings = CreateFromStream(persistenceStream);
			CompleteInitialization(settings);
		}

		/// <summary>
		/// Completes initialization after deserializing the backup settings xml
		/// </summary>
		private void CompleteInitialization(BackupFileSettings settings)
		{
			m_backupTime = settings.BackupTime;
			m_comment = settings.Comment;
			m_projectName = settings.ProjectName;
			m_linkedFilesPathRelative = LinkedFilesRelativePathHelper.FixPathSlashesIfNeeded(settings.LinkedFilesPathRelativePersisted);
			m_linkedFilesPathActual = LinkedFilesRelativePathHelper.FixPathSlashesIfNeeded(settings.LinkedFilesPathActualPersisted);
			m_projectPathPersisted = LinkedFilesRelativePathHelper.FixPathSlashesIfNeeded(settings.ProjectPathPersisted);
			m_configurationSettings = settings.IncludeConfigurationSettings;
			m_linkedFiles = settings.IncludeLinkedFiles;
			m_supportingFiles = settings.IncludeSupportingFiles;
			m_spellCheckAdditions = settings.IncludeSpellCheckAdditions;
			m_dbVersion = settings.DbVersion;
			m_fwVersion = settings.FwVersion;
		}

		/// <summary>
		/// Reads in the backup settings from a given archive entry, usually from a fwbackup archive.
		/// This includes a replacement operation on the xml namespace attribute to the expected namespace.
		/// </summary>
		/// <param name="zipEntry">The entry for the zip fwbackup folder.</param>
		/// <exception cref="InvalidOperationException">An error occurred during deserialization.
		/// </exception>
		private void InitializeFromZipEntry(ZipEntry zipEntry)
		{
			using (FileStream fs = System.IO.File.OpenRead(m_sZipFileName))
			{
				ZipFile zf = new ZipFile(fs);
				Stream zipStream = zf.GetInputStream(zipEntry);
				XDocument xDoc = XDocument.Load(zipStream);
				string backupXml = xDoc.ToString();
				string xmlNamespace = @"\s+xmlns="".+\.BackupRestore""";
				string expectedNamespace =
					@" xmlns=""http://schemas.datacontract.org/2004/07/SIL.LCModel.DomainServices.BackupRestore""";
				backupXml = Regex.Replace(backupXml, xmlNamespace, expectedNamespace);
				BackupFileSettings settings = CreateFromXml(backupXml);
				CompleteInitialization(settings);
			}
		}
		#endregion
	}

	#region class InvalidBackupFileException
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Exception type to encapsulate the various kinds of validity problems in a backup file.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public class InvalidBackupFileException : Exception
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="InvalidBackupFileException"/> class.
		/// </summary>
		/// <param name="zipFile">Path of the backup zip file.</param>
		/// <param name="inner">The inner exception</param>
		/// ------------------------------------------------------------------------------------
		public InvalidBackupFileException(string zipFile, Exception inner) :
			base(Strings.ksInvalidFwBackupFile + Environment.NewLine + zipFile, inner)
		{
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="InvalidBackupFileException"/> class.
		/// </summary>
		/// <param name="message">The message.</param>
		/// ------------------------------------------------------------------------------------
		public InvalidBackupFileException(string message) : base(message)
		{
		}
	}
	#endregion
}
