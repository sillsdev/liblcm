// Copyright (c) 2006-2025 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using NVelocity;
using NVelocity.App;
using NVelocity.Runtime;

namespace SIL.LCModel.SourceGenerators
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// LCM code generator. Runs the NVelocity templates over the LCM model and collects the
	/// generated sources in memory (keyed by their logical output path) instead of writing files,
	/// so it can be hosted inside a Roslyn source generator.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	internal class LcmGenerateImpl
	{
		/// <summary></summary>
		public static LcmGenerateImpl Generator;

		private string m_OutputFileName;
		private readonly VelocityEngine m_Engine;
		private readonly VelocityContext m_Context;
		private Dictionary<string, List<string>> m_OverrideList;
		private Dictionary<string, Dictionary<string, string>> m_IntPropTypeOverrideList;
		private readonly Model m_Model;
		private readonly XmlDocument m_Document;

		/// <summary>Collected outputs, keyed by the logical output path passed to SetOutput
		/// (e.g. "GeneratedConstants.cs", "DomainImpl/GeneratedClasses.cs").</summary>
		private readonly Dictionary<string, string> m_Outputs =
			new Dictionary<string, string>(StringComparer.Ordinal);

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="LcmGenerateImpl"/> class.
		/// </summary>
		/// <param name="doc">The model document.</param>
		/// <param name="templates">The templates, keyed by bare file name (e.g. "main.vm.cs").</param>
		/// ------------------------------------------------------------------------------------
		public LcmGenerateImpl(XmlDocument doc, IReadOnlyDictionary<string, string> templates)
		{
			Generator = this;
			m_Document = doc;
			var entireModel = (XmlElement)doc.GetElementsByTagName("EntireModel")[0];
			m_Model = new Model(entireModel);

			m_Engine = new VelocityEngine();
			// Serve templates from the embedded-resource loader rather than the file system.
			// NVelocity's ExtendedProperties treats commas as list separators, so the loader class
			// name must use a semicolon between the type and assembly names (getLoader converts the
			// ';' back to a ',' before calling Type.GetType). The simple "Type, Assembly" form is
			// enough for Type.GetType to locate the already-loaded generator assembly.
			var loaderType = typeof(EmbeddedTemplateLoader);
			m_Engine.SetProperty("resource.loader", "embedded");
			m_Engine.SetProperty("embedded.resource.loader.class",
				loaderType.FullName + "; " + loaderType.Assembly.GetName().Name);
			m_Engine.SetProperty("embedded.resource.loader.cache", "true");
			m_Engine.SetProperty("embedded.resource.loader.modificationCheckInterval", "0");
			m_Engine.SetProperty($"embedded.resource.loader.{EmbeddedTemplateLoader.TemplatesKey}", templates);
			m_Engine.Init();

			m_Context = new VelocityContext();
			m_Context.Put("lcmgenerate", this);
			m_Context.Put("model", m_Model);

			// The model wrappers (e.g. Property) look up the override lists through this global
			// attribute, so keep publishing it. Generation is serialized by the host, so the use
			// of the process-global RuntimeSingleton is safe.
			RuntimeSingleton.RuntimeServices.SetApplicationAttribute("LcmGenerate.Engine", m_Engine);
			RuntimeSingleton.RuntimeServices.SetApplicationAttribute("LcmGenerate.Context", m_Context);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the list with the class names that we want to override.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public Dictionary<string, List<string>> Overrides
		{
			get { return m_OverrideList; }
			set { m_OverrideList = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the list with the names and types of integer properties we want to
		/// override.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public Dictionary<string, Dictionary<string, string>> IntPropTypeOverrides
		{
			get { return m_IntPropTypeOverrideList; }
			set { m_IntPropTypeOverrideList = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>Gets the model.</summary>
		/// ------------------------------------------------------------------------------------
		public Model Model
		{
			get { return (Model)m_Context.Get("model"); }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>Gets the modules.</summary>
		/// ------------------------------------------------------------------------------------
		public StringKeyCollection<CellarModule> Modules
		{
			get { return Model.Modules; }
		}

		/// <summary>The generated outputs, keyed by their logical output path.</summary>
		public IReadOnlyDictionary<string, string> Outputs => m_Outputs;

		/// ------------------------------------------------------------------------------------
		/// <summary>Sets the (logical) output name that the next Process call writes to.</summary>
		/// ------------------------------------------------------------------------------------
		public void SetOutput(string outputFile)
		{
			m_OutputFileName = outputFile;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Merges the specified template and stores the result under the output name that was in
		/// effect when this call started. The output name is captured up front because the
		/// template being processed (notably main.vm.cs) mutates it via nested SetOutput/Process
		/// calls while this merge is still running.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void Process(string templateName)
		{
			var outputName = m_OutputFileName;
			using (var writer = new StringWriter())
			{
				m_Engine.MergeTemplate(templateName, "UTF-8", m_Context, writer);
				if (!string.IsNullOrEmpty(outputName))
					m_Outputs[outputName] = writer.ToString();
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>Gets the module.</summary>
		/// ------------------------------------------------------------------------------------
		public CellarModule GetModule(string moduleName)
		{
			var query = string.Format("//CellarModule[@id='{0}']", moduleName);
			var iterator = m_Document.CreateNavigator().Select(query);
			if (iterator.MoveNext())
			{
				var module = (XmlElement)iterator.Current.UnderlyingObject;
				return new CellarModule(module, m_Model);
			}
			return null;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>Gets the class.</summary>
		/// ------------------------------------------------------------------------------------
		public IClass GetClass(string className)
		{
			var query = string.Format("//CellarModule[class/@id='{0}']", className);
			var iterator = m_Document.CreateNavigator().Select(query);
			if (iterator.MoveNext())
			{
				var module = (XmlElement)iterator.Current.UnderlyingObject;
				var cellarModule = new CellarModule(module, m_Model);
				return cellarModule.Classes[className];
			}

			return new DummyClass();
		}

		/// <summary>
		/// Put '/// ' at the start of each line in <paramref name="commentData"/>,
		/// including at the start of the string.
		/// </summary>
		public string StringAsMSComment(string commentData)
		{
			var chunks = commentData.Trim().Split(new[]
													{
														'\n', '\r'
													}, StringSplitOptions.RemoveEmptyEntries
				);
			var retval = "";
			foreach (var chunk in chunks)
				retval += "\t/// " + chunk + "\r\n";

			return retval;
		}
	}
}
