// Copyright (c) 2025 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace SIL.LCModel.SourceGenerators
{
	/// <summary>
	/// Hosts the NVelocity-based LCM code generation independent of any particular driver
	/// (MSBuild task, console harness, or Roslyn source generator). Loads the embedded templates,
	/// parses the override configuration, and returns the generated sources keyed by logical
	/// output path.
	/// </summary>
	internal static class LcmModelRunner
	{
		// The embedded template set is immutable, so cache it once (thread-safely) and share it.
		// Everything else in a generation run is instance-local: each call gets its own
		// VelocityEngine, model wrappers and output dictionary, and the model reaches its override
		// lists through the object graph rather than any process-global state, so concurrent
		// generations do not interfere and no lock is needed.
		private static readonly Lazy<IReadOnlyDictionary<string, string>> s_templates =
			new Lazy<IReadOnlyDictionary<string, string>>(LoadEmbeddedTemplates);

		/// <summary>
		/// Runs the generator over the given model inputs and returns the generated sources,
		/// keyed by the logical output path used inside the templates
		/// (e.g. "GeneratedConstants.cs", "DomainImpl/GeneratedClasses.cs").
		/// </summary>
		/// <param name="masterModelXml">Contents of MasterLCModel.xml.</param>
		/// <param name="handGeneratedXml">Contents of HandGenerated.xml.</param>
		/// <param name="intPropTypeOverridesXml">Contents of IntPropTypeOverrides.xml.</param>
		public static IReadOnlyDictionary<string, string> Generate(
			string masterModelXml, string handGeneratedXml, string intPropTypeOverridesXml)
		{
			var doc = new XmlDocument();
			doc.LoadXml(masterModelXml);

			var handGenerated = ParseHandGenerated(handGeneratedXml);
			var intPropOverrides = ParseIntPropTypeOverrides(intPropTypeOverridesXml);

			var impl = new LcmGenerateImpl(doc, s_templates.Value)
			{
				Overrides = handGenerated,
				IntPropTypeOverrides = intPropOverrides
			};

			// main.vm.cs writes the class implementations to this output and, via nested
			// SetOutput/Process calls, produces the other eight outputs.
			impl.SetOutput("DomainImpl/GeneratedClasses.cs");
			impl.Process("main.vm.cs");

			return impl.Outputs;
		}

		/// <summary>Loads every embedded "*.vm.cs" template, keyed by its bare file name.</summary>
		private static IReadOnlyDictionary<string, string> LoadEmbeddedTemplates()
		{
			var result = new Dictionary<string, string>(StringComparer.Ordinal);
			var asm = typeof(LcmModelRunner).Assembly;
			foreach (var name in asm.GetManifestResourceNames())
			{
				if (!name.EndsWith(".vm.cs", StringComparison.Ordinal))
					continue;
				using (var stream = asm.GetManifestResourceStream(name))
				using (var reader = new StreamReader(stream))
				{
					result[name] = reader.ReadToEnd();
				}
			}
			return result;
		}

		/// <summary>Parses HandGenerated.xml into a map of class name to hand-generated property names.</summary>
		private static Dictionary<string, List<string>> ParseHandGenerated(string xml)
		{
			var result = new Dictionary<string, List<string>>();
			var config = new XmlDocument();
			config.LoadXml(xml);
			foreach (XmlElement node in config.GetElementsByTagName("Class"))
			{
				var props = new List<string>();
				foreach (XmlNode propertyNode in node.SelectNodes("property"))
					props.Add(propertyNode.Attributes["name"].Value);
				if (props.Count > 0)
					result.Add(node.Attributes["id"].Value, props);
			}
			return result;
		}

		/// <summary>Parses IntPropTypeOverrides.xml into a map of class name to (property name to type).</summary>
		private static Dictionary<string, Dictionary<string, string>> ParseIntPropTypeOverrides(string xml)
		{
			var result = new Dictionary<string, Dictionary<string, string>>();
			var config = new XmlDocument();
			config.LoadXml(xml);
			foreach (XmlElement node in config.GetElementsByTagName("Class"))
			{
				var props = new Dictionary<string, string>();
				foreach (XmlNode propertyNode in node.SelectNodes("property"))
					props.Add(propertyNode.Attributes["name"].Value, propertyNode.Attributes["type"].Value);
				if (props.Count > 0)
					result.Add(node.Attributes["id"].Value, props);
			}
			return result;
		}
	}
}
