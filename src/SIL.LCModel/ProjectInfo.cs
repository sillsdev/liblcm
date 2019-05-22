// Copyright (c) 2004-2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SIL.LCModel
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Keeps track of information about a FieldWorks project
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public class ProjectInfo
	{
		#region Constructor
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Construct a new ProjectInfo object
		/// </summary>
		/// <param name="databaseName"></param>
		/// ------------------------------------------------------------------------------------
		public ProjectInfo(string databaseName)
		{
			InUse = false;
			DatabaseName = databaseName;
		}
		#endregion

		#region Properties
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get/Set the database name
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public string DatabaseName { get; set; }

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get/Set the indicator that tells if the database is in use
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public bool InUse { get; set; }
		#endregion

		#region Public methods
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Return a string representation of the project
		/// </summary>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public override string ToString()
		{
			return DatabaseName;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get a set of projects on the local computer.
		/// </summary>
		/// <returns>Set of ProjectInfo objects, representing the projects on local computer</returns>
		/// ------------------------------------------------------------------------------------
		public static List<ProjectInfo> GetAllProjects(string projectsDir)
		{
			// Ensure that the folder actually contains a project data file.
			List<ProjectInfo> projectList = new List<ProjectInfo>();
			string[] projectDirectories = Directory.GetDirectories(projectsDir);
			foreach (var dir in projectDirectories)
			{

				string basename = Path.GetFileName(dir);
				if (File.Exists(Path.Combine(dir, basename + LcmFileHelper.ksFwDataXmlFileExtension)))
				{
					projectList.Add(new ProjectInfo(basename));
				}
			}
			return projectList;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get the specified project info if it exists given the UI name (i.e., without path)
		/// on the local machine.
		/// </summary>
		/// <param name="projectsDir"></param>
		/// <param name="projectName">specified project name (without path)</param>
		/// <returns>the project info for the specified name; otherwise null</returns>
		/// ------------------------------------------------------------------------------------
		public static ProjectInfo GetProjectInfoByName(string projectsDir, string projectName)
		{
			return GetAllProjects(projectsDir).FirstOrDefault(info => ProjectsAreSame(projectName, info.DatabaseName));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Determines whether the given project names refer to the same project. This can be
		/// used to compare either simple names or full paths, but not a mix of the two.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static bool ProjectsAreSame(string project1, string project2)
		{
			// NOTE: Even on Linux (for now, at least), we've decided not to support projects
			// whose names differ only by case.
			return string.Equals(project1, project2, StringComparison.InvariantCultureIgnoreCase);
		}
		#endregion
	}
}