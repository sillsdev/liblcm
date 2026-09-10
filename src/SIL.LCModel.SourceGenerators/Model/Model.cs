// Copyright (c) 2006-2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Xml;

namespace SIL.LCModel.SourceGenerators
{
	#region StringKeyCollection

	#endregion

	/// ----------------------------------------------------------------------------------------
	/// <summary>
	///
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	internal class Model
	{
		private StringKeyCollection<CellarModule> m_modules;
		private readonly XmlElement m_node;

		/// <summary>
		/// The generator that owns this model. Nodes reach it through the parent chain
		/// (e.g. Property -&gt; Class -&gt; CellarModule -&gt; Model) to read the override lists,
		/// which avoids any process-global state.
		/// </summary>
		public LcmGenerateImpl LcmGenerate { get; }

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="Model"/> class.
		/// </summary>
		/// <param name="node">The node.</param>
		/// <param name="lcmGenerate">The owning generator.</param>
		/// ------------------------------------------------------------------------------------
		public Model(XmlElement node, LcmGenerateImpl lcmGenerate)
		{
			m_node = node;
			LcmGenerate = lcmGenerate;
		}

		/// <summary>
		/// Get the model's version number.
		/// </summary>
		public int VersionNumber
		{
			get { return Int32.Parse(m_node.GetAttribute("version")); }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the modules.
		/// </summary>
		/// <value>The modules.</value>
		/// ------------------------------------------------------------------------------------
		public StringKeyCollection<CellarModule> Modules
		{
			get
			{
				if (m_modules == null)
				{
					m_modules = new StringKeyCollection<CellarModule>();

					foreach (XmlElement elem in m_node.ChildNodes)
					{
						// Skip FeatSys, since it is empty.
						if (elem.Name == "FeatSys") continue;

						m_modules.Add(new CellarModule(elem, this));
					}
				}
				return m_modules;
			}
		}
	}
}
