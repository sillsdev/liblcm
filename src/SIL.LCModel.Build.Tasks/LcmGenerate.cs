// Copyright (c) 2006-2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SIL.LCModel.Build.Tasks
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	///
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public class LcmGenerate: Task
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the XML file.
		/// </summary>
		/// <value>The XML file.</value>
		/// ------------------------------------------------------------------------------------
		[Required]
		public string XmlFile { get; set; }

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the base dir for the output.
		/// </summary>
		/// <value>The output directory.</value>
		/// ------------------------------------------------------------------------------------
		[Required]
		public string OutputDir { get; set; }

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the file for the output.
		/// </summary>
		/// <value>The output file name.</value>
		/// ------------------------------------------------------------------------------------
		[Required]
		public string OutputFile { get; set; }

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the template file.
		/// </summary>
		/// <value>The template file.</value>
		/// ------------------------------------------------------------------------------------
		[Required]
		public string TemplateFile { get; set; }

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the template file.
		/// </summary>
		/// <value>The template file.</value>
		/// ------------------------------------------------------------------------------------
		public string BackendTemplateFiles { get; set; }

		/// <summary>
		/// Gets or sets the working directory.
		/// </summary>
		/// <value>The working directory.</value>
		public string WorkingDirectory { get; set; }

		/// <summary>
		/// Gets or sets the directory that contains HandGenerated.xml and IntPropTypeOverrides.xml
		/// </summary>
		public string HandGeneratedDir { get; set; }

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Executes the task.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public override bool Execute()
		{
			string origDir = Directory.GetCurrentDirectory();
			string oldDir;
			if (!String.IsNullOrEmpty(WorkingDirectory))
				oldDir = WorkingDirectory;
			else
				oldDir = origDir;
			try
			{
				var doc = new XmlDocument();
				string xmlPath = XmlFile;
				if (!Path.IsPathRooted(xmlPath))
					xmlPath = Path.Combine(oldDir, XmlFile);
				try
				{
					Log.LogMessage(MessageImportance.Low, "Loading XML file {0}.", xmlPath);
					doc.Load(xmlPath);
				}
				catch (XmlException e)
				{
					Log.LogMessage(MessageImportance.High, $"Error loading XML file {xmlPath} {e.Message}");
					return false;
				}

				var handGeneratedFilesDir = string.IsNullOrEmpty(HandGeneratedDir)
					? Path.Combine(oldDir, "LcmGenerate")
					: HandGeneratedDir;

				var config = new XmlDocument();
				var handGeneratedClasses = new Dictionary<string, List<string>>();
				try
				{
					var handGeneratedFile = Path.Combine(handGeneratedFilesDir, "HandGenerated.xml");
					Log.LogMessage(MessageImportance.Low, $"Loading hand generated classes from \"{handGeneratedFile}\".");
					config.Load(handGeneratedFile);
					foreach (XmlElement node in config.GetElementsByTagName("Class"))
					{
						var props = new List<string>();
// ReSharper disable PossibleNullReferenceException
						foreach (XmlNode propertyNode in node.SelectNodes("property"))
// ReSharper restore PossibleNullReferenceException
						{
							props.Add(propertyNode.Attributes["name"].Value);
						}
						if (props.Count > 0)
						{
							handGeneratedClasses.Add(node.Attributes["id"].Value, props);
						}
					}
				}
				catch (XmlException e)
				{
					Log.LogMessage(MessageImportance.High, $"Error loading hand generated classes {e.Message}");
					return false;
				}

				// Dictionary<ClassName, Property>
				var intPropTypeOverridesClasses = new Dictionary<string, Dictionary<string, string>>();
				try
				{
					var handGeneratedFile = Path.Combine(handGeneratedFilesDir, "IntPropTypeOverrides.xml");
					Log.LogMessage(MessageImportance.Low,
						$"Loading hand generated classes from \"{handGeneratedFile}\".");
					config.Load(handGeneratedFile);
					foreach (XmlElement node in config.GetElementsByTagName("Class"))
					{
						// Dictionary<PropertyName, PropertyType>
						var props = new Dictionary<string, string>();
// ReSharper disable PossibleNullReferenceException
						foreach (XmlNode propertyNode in node.SelectNodes("property"))
// ReSharper restore PossibleNullReferenceException
						{
							props.Add(propertyNode.Attributes["name"].Value,
								propertyNode.Attributes["type"].Value);
						}
						if (props.Count > 0)
						{
							intPropTypeOverridesClasses.Add(node.Attributes["id"].Value, props);
						}
					}
				}
				catch (XmlException e)
				{
					Log.LogMessage(MessageImportance.High, $"Error loading IntPropTypeOverrides classes {e.Message}");
					return false;
				}


				try
				{
					// Remember current directory.
					var originalCurrentDirectory = Directory.GetCurrentDirectory();

					Log.LogMessage(MessageImportance.Low, "Processing template {0}.", TemplateFile);
					string outputDirPath = OutputDir;
					if (!Path.IsPathRooted(OutputDir))
						outputDirPath = Path.Combine(oldDir, OutputDir);
					var lcmGenerate = new LcmGenerateImpl(doc, outputDirPath)
										{
											Overrides = handGeneratedClasses,
											IntPropTypeOverrides = intPropTypeOverridesClasses
										};
					string outputPath = OutputFile;
					if (!Path.IsPathRooted(outputPath))
						outputPath = Path.Combine(outputDirPath, OutputFile);
					// Generate the main code.
					if (Path.GetDirectoryName(TemplateFile).Length > 0)
						Directory.SetCurrentDirectory(Path.GetDirectoryName(TemplateFile));
					lcmGenerate.SetOutput(outputPath);
					lcmGenerate.Process(Path.GetFileName(TemplateFile));

					// Generate the backend provider(s) code.
					if (!string.IsNullOrEmpty(BackendTemplateFiles))
					{
						foreach (var backendDir in BackendTemplateFiles.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
						{
							var beDir = backendDir.Trim();
							if (beDir == string.Empty) continue;

							var curDir = Path.Combine(Path.Combine(OutputDir, "LcmGenerate"), beDir);
							Directory.SetCurrentDirectory(curDir);
							lcmGenerate.SetOutput(Path.Combine(beDir, beDir + @"Generated.cs"));
							lcmGenerate.Process("Main" + beDir + ".vm.cs");
						}
					}

					// Restore original directory.
					Directory.SetCurrentDirectory(originalCurrentDirectory);
				}
				catch (Exception e)
				{
					Log.LogMessage(MessageImportance.High, "Error processing template" + " " + e.Message);
					return false;
				}
			}
			finally
			{
				Directory.SetCurrentDirectory(origDir);
			}
			return true;
		}
	}
}
