// Copyright (c) 2025 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SIL.LCModel.SourceGenerators
{
	/// <summary>
	/// Roslyn incremental source generator that produces the LCM domain-model sources from
	/// MasterLCModel.xml (plus HandGenerated.xml and IntPropTypeOverrides.xml). It replaces the
	/// former <c>LcmGenerate</c> MSBuild task and its child-process build step.
	/// </summary>
	/// <remarks>
	/// Wire the model files into the consuming project as AdditionalFiles marked with the
	/// <c>LcmModel</c> metadata, e.g.:
	/// <code>
	/// &lt;AdditionalFiles Include="MasterLCModel.xml" LcmModel="true" /&gt;
	/// &lt;CompilerVisibleItemMetadata Include="AdditionalFiles" MetadataName="LcmModel" /&gt;
	/// </code>
	/// </remarks>
	[Generator(LanguageNames.CSharp)]
	public sealed class LcmModelGenerator : IIncrementalGenerator
	{
		private const string MasterModelFileName = "MasterLCModel.xml";
		private const string HandGeneratedFileName = "HandGenerated.xml";
		private const string IntPropTypeOverridesFileName = "IntPropTypeOverrides.xml";

		private static readonly DiagnosticDescriptor s_generationFailed = new DiagnosticDescriptor(
			id: "LCM001",
			title: "LCM model generation failed",
			messageFormat: "LCM model generation failed: {0}",
			category: "SIL.LCModel.SourceGenerators",
			DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		private static readonly DiagnosticDescriptor s_missingInput = new DiagnosticDescriptor(
			id: "LCM002",
			title: "LCM model input missing",
			messageFormat: "LCM model generation requires an AdditionalFiles entry named '{0}' with LcmModel=\"true\"",
			category: "SIL.LCModel.SourceGenerators",
			DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			// Project each LcmModel-marked AdditionalFile to (fileName, contents). Using the
			// string contents as the pipeline value lets Roslyn cache when nothing changed.
			var modelFiles = context.AdditionalTextsProvider
				.Combine(context.AnalyzerConfigOptionsProvider)
				.Where(pair =>
				{
					var options = pair.Right.GetOptions(pair.Left);
					return options.TryGetValue("build_metadata.AdditionalFiles.LcmModel", out var flag)
						&& flag.Equals("true", StringComparison.OrdinalIgnoreCase);
				})
				.Select((pair, ct) =>
				{
					var fileName = System.IO.Path.GetFileName(pair.Left.Path);
					var text = pair.Left.GetText(ct)?.ToString() ?? string.Empty;
					return new ModelInput(fileName, text);
				})
				.Collect();

			context.RegisterSourceOutput(modelFiles, Execute);
		}

		private static void Execute(SourceProductionContext context, ImmutableArray<ModelInput> inputs)
		{
			var master = Find(inputs, MasterModelFileName);
			var handGenerated = Find(inputs, HandGeneratedFileName);
			var intPropOverrides = Find(inputs, IntPropTypeOverridesFileName);

			// The master model must be present for the generator to do anything. The two override
			// files are optional in principle but expected in the LCM build; treat a missing one
			// as empty so generation still runs.
			if (master == null)
			{
				// Only report if some LcmModel file was supplied (avoids noise in unrelated projects).
				if (!inputs.IsDefaultOrEmpty)
					context.ReportDiagnostic(Diagnostic.Create(s_missingInput, Location.None, MasterModelFileName));
				return;
			}

			try
			{
				var outputs = LcmModelRunner.Generate(
					master,
					handGenerated ?? "<HandGenerated/>",
					intPropOverrides ?? "<IntPropTypeOverrides/>");

				foreach (var kvp in outputs)
				{
					var hintName = kvp.Key.Replace('/', '.').Replace('\\', '.');
					context.AddSource(hintName, SourceText.From(kvp.Value, System.Text.Encoding.UTF8));
				}
			}
			catch (Exception ex)
			{
				context.ReportDiagnostic(Diagnostic.Create(s_generationFailed, Location.None, ex.Message));
			}
		}

		private static string Find(ImmutableArray<ModelInput> inputs, string fileName)
		{
			foreach (var input in inputs)
			{
				if (string.Equals(input.FileName, fileName, StringComparison.OrdinalIgnoreCase))
					return input.Contents;
			}
			return null;
		}

		private readonly struct ModelInput : IEquatable<ModelInput>
		{
			public ModelInput(string fileName, string contents)
			{
				FileName = fileName;
				Contents = contents;
			}

			public string FileName { get; }
			public string Contents { get; }

			public bool Equals(ModelInput other) =>
				FileName == other.FileName && Contents == other.Contents;

			public override bool Equals(object obj) => obj is ModelInput other && Equals(other);

			public override int GetHashCode() =>
				unchecked((FileName?.GetHashCode() ?? 0) * 397 ^ (Contents?.GetHashCode() ?? 0));
		}
	}
}
