// Copyright (c) 2013-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using SIL.LCModel.Utils;
using SIL.PlatformUtilities;
using SIL.WritingSystems;

namespace SIL.LCModel
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Static class to hold a few constant LCM file extensions.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public static class LcmFileHelper
	{
		/// <summary>Default extension for FieldWorks XML data files (with the period)</summary>
		public const string ksFwDataXmlFileExtension = ".fwdata";
		/// <summary>Default extension for FieldWorks backup files (with the period).</summary>
		public const string ksFwBackupFileExtension = ".fwbackup";
		/// <summary>Default extension for FieldWorks 6.0 and earlier backup files (with the period).</summary>
		public const string ksFw60BackupFileExtension = ".zip";
		/// <summary>Default extension for FieldWorks TEMPORARY fallback data files (with the period).</summary>
		public const string ksFwDataFallbackFileExtension = ".bak";


		/// <summary>The name of the folder containing FLEx configuration settings</summary>
		public const string ksConfigurationSettingsDir = "ConfigurationSettings";
		/// <summary>The name of the folder containing FLEx backup settings</summary>
		public const string ksBackupSettingsDir = "BackupSettings";
		/// <summary>The name of the folder where the user can copy files for backup such as fonts and keyboards</summary>
		public const string ksSupportingFilesDir = "SupportingFiles";
		/// <summary>The default name of the folder containing LinkedFiles (media, pictures, etc) for a project</summary>
		public const string ksLinkedFilesDir = "LinkedFiles";
		/// <summary>The name of the subfolder containing media for a project</summary>
		public const string ksMediaDir = "AudioVisual";
		/// <summary>The name of the subfolder containing pictures for a project</summary>
		public const string ksPicturesDir = "Pictures";
		/// <summary>The name of the subfolder containing other LinkedFiles for a project</summary>
		public const string ksOtherLinkedFilesDir = "Others";
		/// <summary>The name of the folder containing writing systems for a project</summary>
		public const string ksWritingSystemsDir = "WritingSystemStore";
		/// <summary>The name of the folder containing temporary persisted sort sequence info for a project</summary>
		public const string ksSortSequenceTempDir = "Temp";

		/// <summary>Constant for locating the other repositories path of a project</summary>
		public const string OtherRepositories = @"OtherRepositories";

		/// <summary>The filename of the backup settings file</summary>
		public const string ksBackupSettingsFilename = "BackupSettings.xml";

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the path to the configuration settings for the specified project.
		/// </summary>
		/// <param name="projectFolder">The path to the project folder.</param>
		/// ------------------------------------------------------------------------------------
		public static string GetConfigSettingsDir(string projectFolder)
		{
			return Path.Combine(projectFolder, ksConfigurationSettingsDir);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the path to the backup settings for the specified project
		/// </summary>
		/// <param name="projectFolder">The path to the project folder.</param>
		/// ------------------------------------------------------------------------------------
		public static string GetBackupSettingsDir(string projectFolder)
		{
			return Path.Combine(projectFolder, ksBackupSettingsDir);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the path to the fonts for the specified project.
		/// </summary>
		/// <param name="projectFolder">The path to the project folder.</param>
		/// ------------------------------------------------------------------------------------
		public static string GetSupportingFilesDir(string projectFolder)
		{
			return Path.Combine(projectFolder, ksSupportingFilesDir);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the path to the writing systems for the specified project.
		/// </summary>
		/// <param name="projectFolder">The path to the project folder.</param>
		/// ------------------------------------------------------------------------------------
		public static string GetWritingSystemDir(string projectFolder)
		{
			return Path.Combine(projectFolder, ksWritingSystemsDir);
		}

		/// <summary>
		/// Gets the path to the standard eternal linked files directory for the specified project.
		/// </summary>
		/// <param name="projectPath">The path to the project.</param>
		/// <returns></returns>
		public static string GetDefaultLinkedFilesDir(string projectPath)
		{
			return Path.Combine(projectPath, ksLinkedFilesDir);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the path to the standard media files directory for the specified project. Note
		/// that if this project keepes its externally linked files in a separate folder from
		/// the rest of the project files (such as a shared folder common to multiple projects
		/// on a server), the directory returned by this method will not actually contain any
		/// files.
		/// </summary>
		/// <param name="projectPath">The path to the project.</param>
		/// ------------------------------------------------------------------------------------
		public static string GetDefaultMediaDir(string projectPath)
		{
			return Path.Combine(projectPath, Path.Combine(ksLinkedFilesDir, ksMediaDir));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the path to the standard pictures directory for the specified project. Note
		/// that if this project keepes its externally linked files in a separate folder from
		/// the rest of the project files (such as a shared folder common to multiple projects
		/// on a server), the directory returned by this method will not actually contain any
		/// files.
		/// </summary>
		/// <param name="projectPath">The path to the project.</param>
		/// ------------------------------------------------------------------------------------
		public static string GetDefaultPicturesDir(string projectPath)
		{
			return Path.Combine(projectPath, Path.Combine(ksLinkedFilesDir, ksPicturesDir));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the path to the standard directory for other externally linked project files.
		/// Note that if this project keepes its externally linked files in a separate folder
		/// from the rest of the project files (such as a shared folder common to multiple
		/// projects on a server), the directory returned by this method will not actually
		/// contain any files.
		/// </summary>
		/// <param name="projectPath">The path to the project.</param>
		/// ------------------------------------------------------------------------------------
		public static string GetDefaultOtherExternalFilesDir(string projectPath)
		{
			return Path.Combine(projectPath, Path.Combine(ksLinkedFilesDir, ksOtherLinkedFilesDir));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the path to the other repositories for the specified project.
		/// </summary>
		/// <param name="projectFolder">The path to the project folder.</param>
		/// ------------------------------------------------------------------------------------
		public static string GetOtherRepositoriesDir(string projectFolder)
		{
			return Path.Combine(projectFolder, OtherRepositories);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the path to the media files directory for the project.
		/// </summary>
		/// <param name="projectLinkedFilesPath">The project's LinkedFiles path. (eg. m_cache.LangProject.LinkedFilesRootDir)</param>
		/// ------------------------------------------------------------------------------------
		public static string GetMediaDir(string projectLinkedFilesPath)
		{
			return Path.Combine(projectLinkedFilesPath, ksMediaDir);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the path to the pictures directory for the project.
		/// </summary>
		/// <param name="projectLinkedFilesPath">The project's LinkedFiles path. (eg. m_cache.LangProject.LinkedFilesRootDir)</param>
		/// ------------------------------------------------------------------------------------
		public static string GetPicturesDir(string projectLinkedFilesPath)
		{
			return Path.Combine(projectLinkedFilesPath, ksPicturesDir);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the path to the directory for other externally linked project files.
		/// </summary>
		/// <param name="projectLinkedFilesPath">The project's LinkedFiles path. (eg. m_cache.LangProject.LinkedFilesRootDir)</param>
		/// ------------------------------------------------------------------------------------
		public static string GetOtherExternalFilesDir(string projectLinkedFilesPath)
		{
			return Path.Combine(projectLinkedFilesPath, ksOtherLinkedFilesDir);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the name of the XML data file, given a project name (basically just adds the
		/// FW data XML file extension).
		/// </summary>
		/// <param name="projectName">Name of the project (not a filename).</param>
		/// ------------------------------------------------------------------------------------
		public static string GetXmlDataFileName(string projectName)
		{
			Debug.Assert(Path.GetExtension(projectName) != ksFwDataXmlFileExtension,
				$"There is a faint chance the user might have specified a real project name ending in {ksFwDataXmlFileExtension} (in which case, sorry, but we're going to trim it off), but probably this is a programming error");
			// Do not use Path.ChangeExtension because it will strip off anything following a period in the project name!
			return projectName.EndsWith(ksFwDataXmlFileExtension) ? projectName : projectName + ksFwDataXmlFileExtension;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the path without the root directory (i.e. make it un-rooted).
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string GetPathWithoutRoot(string pathWithRoot)
		{
			string pathRoot = Path.GetPathRoot(pathWithRoot);
			return pathWithRoot.Substring(pathRoot.Length);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Takes a windows path and returns it in the format which our backup zip files
		/// stores them in.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string GetZipfileFormattedPath(string path)
		{
			StringBuilder strBldr = new StringBuilder(path);
			string pathRoot = Path.GetPathRoot(path);
			strBldr.Remove(0, pathRoot.Length);
			// replace back slashes with forward slashes (for Windows)
			if (!Platform.IsUnix && !Platform.IsMac)
				strBldr.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			return strBldr.ToString();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the path for storing common application data that might be shared between
		/// multiple applications and multiple users on the same machine.
		///
		/// On Windows this returns Environment.SpecialFolder.CommonApplicationData
		/// (C:\ProgramData),on Linux /var/lib/fieldworks.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string CommonApplicationData
		{
			get
			{
				if (Platform.IsUnix)
				{
					// allow to override the /var/lib/fieldworks path by setting the
					// environment variable FW_CommonAppData. Is this is needed on our CI
					// build machines.
					return Environment.GetEnvironmentVariable("FW_CommonAppData") ?? "/var/lib/fieldworks";
				}

				return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the global writing system store directory. The directory is guaranteed to exist.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string GlobalWritingSystemStoreDirectory
			=> GlobalWritingSystemRepository.CurrentVersionPath(GlobalWritingSystemRepository.DefaultBasePath);

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the old global writing system store directory.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string OldGlobalWritingSystemStoreDirectory => Path.Combine(CommonApplicationData, "SIL", "WritingSystemStore");
	}
}
