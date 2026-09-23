// Copyright (c) 2006-2025 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SIL.LCModel.ModelGeneration;

namespace SIL.LCModel.Build.Tasks
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Renders a single NVelocity template (e.g. a C++ header such as CellarConstants.vm.h) from
	/// the LCM model. The LCM domain-model C# is produced by the SIL.LCModel.SourceGenerators
	/// source generator; this task remains for consumers (notably the FieldWorks native build)
	/// that render other artifacts from the same model through the shared engine.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public class LcmGenerate : Task
	{
		/// <summary>The model XML file (MasterLCModel.xml).</summary>
		[Required]
		public string XmlFile { get; set; }

		/// <summary>The base directory for the output.</summary>
		[Required]
		public string OutputDir { get; set; }

		/// <summary>The output file, relative to <see cref="OutputDir"/>.</summary>
		[Required]
		public string OutputFile { get; set; }

		/// <summary>The template file to render.</summary>
		[Required]
		public string TemplateFile { get; set; }

		/// <summary>
		/// Retained for backwards compatibility with existing call sites; no longer used now that
		/// the domain model is produced by the source generator.
		/// </summary>
		public string BackendTemplateFiles { get; set; }

		/// <summary>
		/// Retained for backwards compatibility with existing call sites. The engine no longer
		/// relies on the current directory (see <c>EmbeddedTemplateLoader</c>), so this is ignored.
		/// </summary>
		public string WorkingDirectory { get; set; }

		/// <summary>
		/// Retained for backwards compatibility with existing call sites; not needed for
		/// single-template rendering.
		/// </summary>
		public string HandGeneratedDir { get; set; }

		public override bool Execute()
		{
			try
			{
				var baseDir = string.IsNullOrEmpty(WorkingDirectory)
					? Directory.GetCurrentDirectory()
					: WorkingDirectory;

				var xmlPath = Path.IsPathRooted(XmlFile) ? XmlFile : Path.Combine(baseDir, XmlFile);
				var templatePath = Path.IsPathRooted(TemplateFile) ? TemplateFile : Path.Combine(baseDir, TemplateFile);
				var outputDirPath = Path.IsPathRooted(OutputDir) ? OutputDir : Path.Combine(baseDir, OutputDir);

				var doc = new XmlDocument();
				Log.LogMessage(MessageImportance.Low, "Loading XML file {0}.", xmlPath);
				doc.Load(xmlPath);

				// The engine resolves templates (including any #parse siblings) by bare file name,
				// so key the map on the template's file name and include its directory siblings.
				var templateName = Path.GetFileName(templatePath);
				var templates = LoadTemplates(templatePath);

				var impl = new LcmGenerateImpl(doc, templates)
				{
					// Empty override maps: the rendered artifacts (e.g. C++ headers) do not use the
					// hand-generated / int-type-override metadata, but set them so the model
					// wrappers never dereference a null.
					Overrides = new Dictionary<string, List<string>>(),
					IntPropTypeOverrides = new Dictionary<string, Dictionary<string, string>>()
				};

				Log.LogMessage(MessageImportance.Low, "Processing template {0}.", templateName);
				impl.SetOutput(OutputFile);
				impl.Process(templateName);

				Directory.CreateDirectory(outputDirPath);
				File.WriteAllText(Path.Combine(outputDirPath, OutputFile), impl.Outputs[OutputFile]);
				return true;
			}
			catch (Exception e)
			{
				Log.LogError("LcmGenerate failed: {0}", e.Message);
				return false;
			}
		}

		/// <summary>
		/// Loads the target template plus any sibling "*.vm.*" templates in the same directory,
		/// keyed by bare file name, so <c>#parse</c> references resolve without touching the
		/// current directory.
		/// </summary>
		private static IReadOnlyDictionary<string, string> LoadTemplates(string templatePath)
		{
			var result = new Dictionary<string, string>(StringComparer.Ordinal);
			var dir = Path.GetDirectoryName(templatePath);
			if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
			{
				foreach (var file in Directory.GetFiles(dir, "*.vm.*"))
					result[Path.GetFileName(file)] = File.ReadAllText(file);
			}
			// Ensure the target template is present even if its extension is not "*.vm.*".
			result[Path.GetFileName(templatePath)] = File.ReadAllText(templatePath);
			return result;
		}
	}
}
