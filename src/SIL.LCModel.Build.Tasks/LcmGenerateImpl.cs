// Copyright (c) 2006-2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using NVelocity;
using NVelocity.App;
using NVelocity.Runtime;

namespace SIL.LCModel.Build.Tasks
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// LCM code generator
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	internal class LcmGenerateImpl
	{
		/// <summary></summary>
		public static LcmGenerateImpl Generator;

		private readonly string m_OutputDir;
		private string m_OutputFileName;
		private readonly VelocityEngine m_Engine;
		private readonly VelocityContext m_Context;
		private Dictionary<string, List<string>> m_OverrideList;
		private Dictionary<string, Dictionary<string, string>> m_IntPropTypeOverrideList;
		private readonly Model m_Model;
		private readonly XmlDocument m_Document;

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="LcmGenerateImpl"/> class.
		/// </summary>
		/// <param name="doc">The XMI document.</param>
		/// <param name="outputDir">The output dir.</param>
		/// ------------------------------------------------------------------------------------
		public LcmGenerateImpl(XmlDocument doc, string outputDir)
			: this(doc, outputDir, null)
		{
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="LcmGenerateImpl"/> class.
		/// </summary>
		/// <param name="doc">The XMI document.</param>
		/// <param name="outputDir">The output dir.</param>
		/// <param name="outputFile">The output file name.</param>
		/// ------------------------------------------------------------------------------------
		public LcmGenerateImpl(XmlDocument doc, string outputDir, string outputFile)
		{
			Generator = this;
			m_Document = doc;
			m_OutputDir = outputDir;
			m_OutputFileName = outputFile;
			var entireModel = (XmlElement)doc.GetElementsByTagName("EntireModel")[0];
			m_Model = new Model(entireModel);

			m_Engine = new VelocityEngine();
			m_Engine.Init();

			m_Context = new VelocityContext();
			m_Context.Put("lcmgenerate", this);
			m_Context.Put("model", m_Model);

			RuntimeSingleton.RuntimeServices.SetApplicationAttribute("LcmGenerate.Engine", m_Engine);
			RuntimeSingleton.RuntimeServices.SetApplicationAttribute("LcmGenerate.Context", m_Context);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the list with the class names that we want to override.
		/// </summary>
		/// <value>The list of override class names.</value>
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
		/// <value>The list of override class names.</value>
		/// ------------------------------------------------------------------------------------
		public Dictionary<string, Dictionary<string, string>> IntPropTypeOverrides
		{
			get { return m_IntPropTypeOverrideList; }
			set { m_IntPropTypeOverrideList = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the model.
		/// </summary>
		/// <value>The model.</value>
		/// ------------------------------------------------------------------------------------
		public Model Model
		{
			get { return (Model)m_Context.Get("model"); }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the modules.
		/// </summary>
		/// <value>The modules.</value>
		/// ------------------------------------------------------------------------------------
		public StringKeyCollection<CellarModule> Modules
		{
			get { return Model.Modules; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Sets the output file name.
		/// </summary>
		/// <param name="outputFile">The output file name.</param>
		/// ------------------------------------------------------------------------------------
		public void SetOutput(string outputFile)
		{
			m_OutputFileName = outputFile;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Processes the specified template.
		/// </summary>
		/// <param name="templateName">Name of the template.</param>
		/// ------------------------------------------------------------------------------------
		public void Process(string templateName)
		{
			Stream stream = null;
			try
			{
				stream = string.IsNullOrEmpty(m_OutputFileName)
							? (Stream) new MemoryStream()
							: new FileStream(Path.Combine(m_OutputDir, m_OutputFileName),
											 FileMode.Create, FileAccess.Write);

				using (var writer = new StreamWriter(stream))
				{
					m_Engine.MergeTemplate(templateName, "UTF-8", m_Context, writer);
				}
			}
			finally
			{
				if (stream != null)
					stream.Dispose();
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the module.
		/// </summary>
		/// <param name="moduleName">Name of the module.</param>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public CellarModule GetModule(string moduleName)
		{
			var query = string.Format("//CellarModule[@id='{0}']", moduleName);
// ReSharper disable PossibleNullReferenceException
			var iterator = m_Document.CreateNavigator().Select(query);
// ReSharper restore PossibleNullReferenceException
			if (iterator.MoveNext())
			{
				var module = (XmlElement)iterator.Current.UnderlyingObject;
				return new CellarModule(module, m_Model);
			}
			return null;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the class.
		/// </summary>
		/// <param name="className">Name of the class.</param>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public IClass GetClass(string className)
		{
			var query = string.Format("//CellarModule[class/@id='{0}']", className);
// ReSharper disable PossibleNullReferenceException
			var iterator = m_Document.CreateNavigator().Select(query);
// ReSharper restore PossibleNullReferenceException
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
		/// <param name="commentData"></param>
		/// <returns></returns>
		public string StringAsMSComment(string commentData)
		{
			//var chunks = Regex.Split(commentData, "\r\n");

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
