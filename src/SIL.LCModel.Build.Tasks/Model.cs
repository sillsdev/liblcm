// Copyright (c) 2006-2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Xml;

namespace SIL.LCModel.Build.Tasks
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

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="Model"/> class.
		/// </summary>
		/// <param name="node">The node.</param>
		/// ------------------------------------------------------------------------------------
		public Model(XmlElement node)
		{
			m_node = node;
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
