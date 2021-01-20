// Copyright (c) 2013-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.IO;
using SIL.LCModel.Utils;
using SIL.PlatformUtilities;

namespace SIL.LCModel
{
	/// <summary>
	/// This class is designed for converting between relative paths and full paths for the LinkedFiles of a FW project
	/// </summary>
	public static class LinkedFilesRelativePathHelper
	{
		/// <summary>Substitution string for a path that is under the LinkedFiles directory.</summary>
		public const string ksLFrelPath = "%lf%";
		/// <summary>Substitution string for a path that is under the project's directory.</summary>
		public const string ksProjectRelPath = "%proj%";
		/// <summary>Substitution string for a path that is under the default directory for projects.</summary>
		public const string ksProjectsRelPath = "%Projects%";
		/// <summary>Substitution string for a path that is under the My Documents directory.</summary>
		public const string ksMyDocsRelPath = "%MyDocuments%";
		/// <summary>Substitution string for a path that is under the Shared Application Data directory.</summary>
		public const string ksCommonAppDataRelPath = "%CommonApplicationData%";

		#region Methods to covert between RelativePaths and FullPaths

		/// <summary>
		/// If a filePath is stored in the format  %lf%\path\filename then this method returns the full path.
		/// Otherwise return null
		/// </summary>
		/// <param name="relativePath"></param>
		/// <param name="projectLinkedFilesPath"></param>
		/// <returns></returns>
		public static string GetFullFilePathFromRelativeLFPath(string relativePath, string projectLinkedFilesPath)
		{
			string fullfilePath = GetFullPathForRelativePath(relativePath, ksLFrelPath, projectLinkedFilesPath);

			if (string.IsNullOrEmpty(fullfilePath))
				return null;
			return FixPathSlashesIfNeeded(fullfilePath);
		}

		/// <summary>
		/// If a file path is non rooted then return combination of the linkedFiledRootDir and the relative
		/// path.  Otherwise just return the full path passed in as an arguement.
		/// </summary>
		/// <param name="relativeLFPath"></param>
		/// <param name="linkedFilesRootDir"></param>
		/// <returns></returns>
		public static String GetFullPathFromRelativeLFPath(string relativeLFPath, string linkedFilesRootDir)
		{
			// We could just catch the exception that IsPathRooted throws if there are invalid characters,
			// or use Path.GetInvalidPathChars(). But that would pass things on Linux that will fail on Windows,
			// which both makes unit testing difficult, and also may hide from Linux users the fact that their
			// paths will cause problems on Windows.
			var invalidChars = MiscUtils.GetInvalidProjectNameChars(MiscUtils.FilenameFilterStrength.kFilterBackup);
			// relativeLFPath is allowed to include directories. And it MAY be rooted, meaning on Windows it could start X:
			invalidChars = invalidChars.Replace(@"\", "").Replace("/", "").Replace(":", "");
			// colon is allowed only as second character--such a path is probably no good on Linux, but will just be not found, not cause a crash
			int indexOfColon = relativeLFPath.IndexOf(':');
			if (relativeLFPath.IndexOfAny(invalidChars.ToCharArray()) != -1
				|| (indexOfColon != -1 && indexOfColon != 1))
			{
				// This is a fairly clumsy solution, designed as a last-resort way to avoid crashing the program.
				// Hopefully most paths for entering path names into the relevant fields do something nicer to
				// avoid getting illegal characters there.
				return FixPathSlashesIfNeeded(Path.Combine(linkedFilesRootDir, "__ILLEGALCHARS__"));
			}
			if (Path.IsPathRooted(relativeLFPath))
				return FixPathSlashesIfNeeded(relativeLFPath);
			else
				return FixPathSlashesIfNeeded(Path.Combine(linkedFilesRootDir, relativeLFPath));
		}

		/// <summary>
		/// If a path gets stored with embedded \, fix it to work away from Windows.  (FWNX-882)
		/// </summary>
		public static string FixPathSlashesIfNeeded(string path)
		{
			if (string.IsNullOrEmpty(path))
				return string.Empty;
			if (Platform.IsUnix || Platform.IsMac)
			{
				if (path.Contains("\\"))
					return path.Replace('\\', '/');
			}
			return path;
		}

		/// <summary>
		/// If the path is relative to the project's linkedFiles path then substitute %lf%
		/// and return it. Otherwise return an empty string
		/// </summary>
		/// <param name="filePath"></param>
		/// <param name="projectLinkedFilesPath"></param>
		/// <returns></returns>
		public static string GetRelativeLFPathFromFullFilePath(string filePath,
																string projectLinkedFilesPath)
		{
			if (string.IsNullOrEmpty(projectLinkedFilesPath))
				return string.Empty;

			var linkedFilesPathLowercaseRoot = GetPathWithLowercaseRoot(filePath);

			var relativePath = GetRelativePathIfExists(ksLFrelPath, linkedFilesPathLowercaseRoot,
														projectLinkedFilesPath);
			if (!string.IsNullOrEmpty(relativePath))
				return FixPathSlashesIfNeeded(relativePath);
			//Just return an empty path if we cannot find a relative path.
			return string.Empty;
		}

		/// <summary>
		/// If the specified path starts with the LinkedFiles root directory then return
		/// the part after the linkedFilesRootDir;
		/// otherwise if it is a file path at all convert it to the current platform and return it;
		/// otherwise (it's a URL, determined by containing a colon after more than one initial character)
		/// return null to indicate no change made.
		/// </summary>
		/// <param name="filePath"></param>
		/// <param name="linkedFilesRootDir"></param>
		/// <returns></returns>
		public static string GetRelativeLinkedFilesPath(string filePath,
														string linkedFilesRootDir)
		{
			if (filePath.IndexOf(':') > 1)
			{
				// It's a URL, not a path at all; don't mess with it.
				return null;
			}
			string directory = FileUtils.ChangePathToPlatform(linkedFilesRootDir);
			string relativePath = FileUtils.ChangePathToPlatform(filePath);

			// Does the specified path start with the LinkedFiles root directory?
			if (relativePath.StartsWith(directory, true, System.Globalization.CultureInfo.InvariantCulture) &&
				relativePath.Length > directory.Length + 1)
			{
				// Keep the portion of the specified path that is a subfolder of
				// the LinkedFiles folder and make sure to strip off an initial
				// path separator if there is one.
				relativePath = relativePath.Substring(directory.Length);
				if (relativePath[0] == Path.DirectorySeparatorChar)
					relativePath = relativePath.Substring(1);
			}
			return FixPathSlashesIfNeeded(relativePath);
		}

		/// <summary>
		/// Return the fullPath for a project's LinkedFiles based on the relative path that was persisted.
		/// If no match on a relativePath match is made then return the relativePath passed in assuming it
		/// is actually a full path.
		/// </summary>
		/// <param name="projectsPath"></param>
		/// <param name="relativePath"></param>
		/// <param name="projectPath"></param>
		/// <returns></returns>
		public static string GetLinkedFilesFullPathFromRelativePath(string projectsPath, string relativePath, string projectPath)
		{
			string fullPath = GetFullPathForRelativePath(relativePath, ksProjectRelPath, projectPath);

			if (string.IsNullOrEmpty(fullPath))
				fullPath = GetFullPathForRelativePath(relativePath, ksProjectsRelPath, projectsPath);
			if (string.IsNullOrEmpty(fullPath))
				fullPath = GetFullPathForRelativePath(relativePath, ksCommonAppDataRelPath, LcmFileHelper.CommonApplicationData);
			if (string.IsNullOrEmpty(fullPath))
			{
				fullPath = GetFullPathForRelativePath(relativePath, ksMyDocsRelPath,
					Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
			}
			if (string.IsNullOrEmpty(fullPath))
				return FixPathSlashesIfNeeded(relativePath);
			return FixPathSlashesIfNeeded(fullPath);
		}

		private static string GetFullPathForRelativePath(string relativePath, string relativePart, string fullPathReplacement)
		{
			if (relativePath.StartsWith(relativePart))
			{
				var length = relativePart.Length;
				var restOfPath = relativePath.Substring(length, relativePath.Length - length);
				return fullPathReplacement + restOfPath;
			}
			else
			{
				return string.Empty;
			}
		}

		/// <summary>
		/// Get a relative path for the LinkedFilesPath which we will persist to be used when
		/// restoring a project.
		/// </summary>
		/// <param name="projectsPath"></param>
		/// <param name="linkedFilesFullPath"></param>
		/// <param name="projectPath"></param>
		/// <param name="projectName"></param>
		/// <returns></returns>
		public static string GetLinkedFilesRelativePathFromFullPath(string projectsPath, string linkedFilesFullPath,
																	string projectPath, string projectName)
		{
			var linkedFilesPathLowercaseRoot = GetPathWithLowercaseRoot(linkedFilesFullPath);
			// Case where the ExternalLinks folder is located somewhere under the project folder.
			// This is the default location.
			var relativePath = GetRelativePathIfExists(ksProjectRelPath, linkedFilesPathLowercaseRoot,
														projectPath);
			if (!string.IsNullOrEmpty(relativePath))
				return FixPathSlashesIfNeeded(relativePath);
			// GetRelativePathIfExists may miss a case where, say, projectPath is
			// \\ls-thomson-0910.dallas.sil.org\Projects\MyProj, and linkedFilesFullPath is
			// C:\Documents and settings\All Users\SIL\FieldWorks\Projects\MyProj\LinkedFiles
			// Even though the MyProj directory in both paths is the same directory.
			// It's important to catch this case and return a relative path.
			var projectFolderName = Path.GetFileName(projectPath);
			var allProjectsName = Path.GetFileName(Path.GetDirectoryName(projectPath));
			var match = Path.Combine(allProjectsName, projectFolderName);
			int index = linkedFilesFullPath.IndexOf(match, StringComparison.InvariantCultureIgnoreCase);
			if (index >= 0)
			{
				// There's a very good chance these are the same folders!
				var alternateProjectPath = linkedFilesFullPath.Substring(0, index + match.Length);
				if (Directory.Exists(alternateProjectPath) &&
					Directory.GetLastWriteTime(alternateProjectPath) == Directory.GetLastWriteTime(projectPath))
				{
					// They ARE the same directory! (I suppose we could miss if someone wrote to it at the
					// exact wrong moment, but we shouldn't be changing this setting while shared, anyway.)
					return FixPathSlashesIfNeeded(ksProjectRelPath + linkedFilesFullPath.Substring(index + match.Length));
				}
			}

			//See if linkedFilesPath begins with one of the other standard paths.

			// Case where user is presumably having a LinkedFiles folder shared among a number
			// of projects under the Projects folder. That would be a good reason to put it in
			// the projects folder common to all projects.
			relativePath = GetRelativePathIfExists(ksProjectsRelPath, linkedFilesPathLowercaseRoot, projectsPath);
			if (!string.IsNullOrEmpty(relativePath))
				return FixPathSlashesIfNeeded(relativePath);

			// Case where the user has the LinkedFiles folder in a shared folder.
			relativePath = GetRelativePathIfExists(ksCommonAppDataRelPath, linkedFilesPathLowercaseRoot,
				LcmFileHelper.CommonApplicationData);
			if (!string.IsNullOrEmpty(relativePath))
				return FixPathSlashesIfNeeded(relativePath);

			// Case where the user has the LinkedFiles folder in their MyDocuments folder
			relativePath = GetRelativePathIfExists(ksMyDocsRelPath, linkedFilesPathLowercaseRoot,
				Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
			if (!string.IsNullOrEmpty(relativePath))
				return FixPathSlashesIfNeeded(relativePath);

			//Just return the complete path if we cannot find a relative path.
			return FixPathSlashesIfNeeded(linkedFilesFullPath);
		}

		private static string GetRelativePathIfExists(string relativePart, string fullPath,
													string parentPath)
		{
			var parentPathLowerCaseRoot = GetPathWithLowercaseRoot(parentPath);
			if (!string.IsNullOrEmpty(parentPathLowerCaseRoot) &&
				fullPath.StartsWith(parentPathLowerCaseRoot))
			{
				var length = parentPath.Length;
				var restOfPath = fullPath.Substring(length, fullPath.Length - length);
				return relativePart + restOfPath;
			}
			return string.Empty;
		}

		private static string GetPathWithLowercaseRoot(string path)
		{
			try
			{
				var rootOfPath = Path.GetPathRoot(path);
				return rootOfPath.ToLowerInvariant()
					+ path.Substring(rootOfPath.Length, path.Length - rootOfPath.Length);
			}
			catch (ArgumentException)
			{
				return path.ToLowerInvariant();
			}
		}


		#endregion
	}
}