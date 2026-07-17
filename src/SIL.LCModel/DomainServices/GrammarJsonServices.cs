// Copyright (c) 2026 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.WritingSystems;

namespace SIL.LCModel.DomainServices
{
	/// <summary>
	/// Exports the parser-relevant subset of a project — phonology, morphology, and lexicon —
	/// as "LCM Grammar JSON": a deterministic, GUID-keyed JSON document with the envelope
	/// <c>{"format":"lcm-grammar","version":1,...}</c>. This is the interchange format consumed
	/// by external morphological-parser tooling (grammar verification, conformance fixtures,
	/// field deployment); it is a read-only projection, not an editing or synchronization format.
	///
	/// Determinism rules (so that two exports of the same data are byte-identical, and so that an
	/// independent implementation reading the raw .fwdata XML can reproduce the same bytes):
	/// - Owning/reference sequences (OS/RS properties) are written in model order, which is
	///   semantically meaningful (rule order, slot order, allomorph disjunctive order, ...).
	/// - Unordered collections (OC/RC properties, repository instances) are sorted by the ordinal
	///   comparison of their lowercase-hyphenated GUID string.
	/// - Multi-writing-system string values are written as arrays of {"ws":tag,"form":text},
	///   sorted ordinally by writing-system tag; empty alternatives are skipped.
	/// - Optional values that are absent, and empty arrays, are omitted entirely (exception:
	///   a lexical entry's "allomorphs" array is always present, even when empty).
	///
	/// Known limitation: because this exporter walks resolved LCM objects, a dangling reference
	/// (e.g. an ad hoc rule whose primary morpheme was deleted) surfaces as null and the original
	/// GUID is unrecoverable, so such records are skipped with a warning rather than exported
	/// with the stale reference preserved.
	/// </summary>
	public static class GrammarJsonServices
	{
		/// <summary>The value of the envelope "format" field.</summary>
		public const string FormatName = "lcm-grammar";

		/// <summary>The value of the envelope "version" field.</summary>
		public const int FormatVersion = 1;

		/// <summary>
		/// Exports the grammar of the given project and returns it as a JSON string.
		/// </summary>
		public static string ExportGrammar(LcmCache cache)
		{
			return ExportGrammar(cache, null);
		}

		/// <summary>
		/// Exports the grammar of the given project and returns it as a JSON string, adding a
		/// message to <paramref name="warnings"/> (when non-null) for each piece of data that
		/// could not be represented and was skipped.
		/// </summary>
		public static string ExportGrammar(LcmCache cache, ICollection<string> warnings)
		{
			var sb = new StringBuilder();
			using (var writer = new StringWriter(sb))
				ExportGrammar(cache, writer, warnings);
			return sb.ToString();
		}

		/// <summary>
		/// Exports the grammar of the given project as JSON to the given writer.
		/// </summary>
		public static void ExportGrammar(LcmCache cache, TextWriter textWriter, ICollection<string> warnings = null)
		{
			if (cache == null)
				throw new ArgumentNullException(nameof(cache));
			if (textWriter == null)
				throw new ArgumentNullException(nameof(textWriter));
			new Exporter(cache, textWriter, warnings).Export();
		}

		private sealed class Exporter
		{
			// FieldWorks stores a literal "***" to mean "no value" in some gloss fields; a missing
			// multistring alternative is also rendered as "***" by the Best*Alternative accessors.
			private const string MissingValueSentinel = "***";

			private static readonly Dictionary<Guid, string> MorphTypeNames = new Dictionary<Guid, string>
			{
				{ MoMorphTypeTags.kguidMorphStem, "stem" },
				{ MoMorphTypeTags.kguidMorphBoundStem, "boundStem" },
				{ MoMorphTypeTags.kguidMorphRoot, "root" },
				{ MoMorphTypeTags.kguidMorphBoundRoot, "boundRoot" },
				{ MoMorphTypeTags.kguidMorphPrefix, "prefix" },
				{ MoMorphTypeTags.kguidMorphSuffix, "suffix" },
				{ MoMorphTypeTags.kguidMorphInfix, "infix" },
				{ MoMorphTypeTags.kguidMorphCircumfix, "circumfix" },
				{ MoMorphTypeTags.kguidMorphProclitic, "proclitic" },
				{ MoMorphTypeTags.kguidMorphEnclitic, "enclitic" },
				{ MoMorphTypeTags.kguidMorphClitic, "clitic" },
				{ MoMorphTypeTags.kguidMorphParticle, "particle" },
				{ MoMorphTypeTags.kguidMorphPhrase, "phrase" },
				{ MoMorphTypeTags.kguidMorphDiscontiguousPhrase, "discontigPhrase" },
				{ MoMorphTypeTags.kguidMorphPrefixingInterfix, "prefixingInterfix" },
				{ MoMorphTypeTags.kguidMorphInfixingInterfix, "infixingInterfix" },
				{ MoMorphTypeTags.kguidMorphSuffixingInterfix, "suffixingInterfix" }
			};

			private readonly LcmCache m_cache;
			private readonly ILangProject m_langProject;
			private readonly JsonTextWriter m_json;
			private readonly ICollection<string> m_warnings;

			internal Exporter(LcmCache cache, TextWriter textWriter, ICollection<string> warnings)
			{
				m_cache = cache;
				m_langProject = cache.LanguageProject;
				m_warnings = warnings;
				m_json = new JsonTextWriter(textWriter)
				{
					Formatting = Formatting.Indented,
					Indentation = 2,
					IndentChar = ' '
				};
			}

			internal void Export()
			{
				m_json.WriteStartObject();
				WriteProp("format", FormatName);
				m_json.WritePropertyName("version");
				m_json.WriteValue(FormatVersion);
				WriteProject();
				WriteFeatureSystems();
				WritePhonology();
				WriteMorphology();
				WriteLexicon();
				m_json.WriteEndObject();
				m_json.Flush();
			}

			#region Shared helpers

			private void Warn(string message)
			{
				m_warnings?.Add(message);
			}

			private static string GuidStr(ICmObject obj)
			{
				return obj.Guid.ToString();
			}

			private static IEnumerable<T> ByGuid<T>(IEnumerable<T> objs) where T : ICmObject
			{
				return objs.OrderBy(o => o.Guid.ToString(), StringComparer.Ordinal);
			}

			/// <summary>
			/// The best text of a multistring name-like field — analysis writing systems first,
			/// falling back to vernacular (phoneme names, for example, are usually authored only
			/// in a vernacular writing system) — or "" when the field has no usable value.
			/// </summary>
			private static string Best(IMultiAccessorBase multiString)
			{
				if (multiString == null || multiString.StringCount == 0)
					return string.Empty;
				string text = multiString.BestAnalysisVernacularAlternative?.Text;
				return text == null || text == MissingValueSentinel ? string.Empty : text;
			}

			private string WsTag(int wsHandle)
			{
				// GetStrFromWs returns null (rather than throwing) for a handle that no longer
				// resolves to a writing system; callers skip such alternatives.
				return m_cache.ServiceLocator.WritingSystemManager.GetStrFromWs(wsHandle);
			}

			private static string StripDottedCircles(string text)
			{
				return text?.Replace("◌", string.Empty);
			}

			private void WriteProp(string name, string value)
			{
				m_json.WritePropertyName(name);
				// LCM holds strings NFD in memory, but the .fwdata at-rest form is NFC; exports
				// are NFC so that an independent reader of the raw XML reproduces the same bytes.
				m_json.WriteValue((value ?? string.Empty).Normalize(NormalizationForm.FormC));
			}

			private void WriteProp(string name, bool value)
			{
				m_json.WritePropertyName(name);
				m_json.WriteValue(value);
			}

			private void WriteProp(string name, int value)
			{
				m_json.WritePropertyName(name);
				m_json.WriteValue(value);
			}

			private void WriteGuidProp(string name, ICmObject obj)
			{
				if (obj == null)
					return;
				WriteProp(name, GuidStr(obj));
			}

			/// <summary>Writes an array of GUID strings; omitted entirely when empty.</summary>
			private void WriteGuidArray<T>(string name, IEnumerable<T> objs, bool ordered) where T : ICmObject
			{
				var list = (ordered ? objs : ByGuid(objs)).ToList();
				if (list.Count == 0)
					return;
				m_json.WritePropertyName(name);
				m_json.WriteStartArray();
				foreach (T obj in list)
					m_json.WriteValue(GuidStr(obj));
				m_json.WriteEndArray();
			}

			/// <summary>
			/// Writes a multistring as an array of {"ws","form"}, sorted by writing-system tag;
			/// empty alternatives skipped; omitted entirely when nothing remains (unless
			/// <paramref name="alwaysEmit"/>).
			/// </summary>
			private void WriteWsForms(string name, IMultiAccessorBase multiString, bool alwaysEmit = false)
			{
				var forms = GetWsForms(multiString);
				if (forms.Count == 0 && !alwaysEmit)
					return;
				m_json.WritePropertyName(name);
				WriteWsFormArray(forms);
			}

			private List<KeyValuePair<string, string>> GetWsForms(IMultiAccessorBase multiString, bool stripDottedCircles = false)
			{
				var forms = new List<KeyValuePair<string, string>>();
				if (multiString == null)
					return forms;
				foreach (int ws in multiString.AvailableWritingSystemIds)
				{
					string text = multiString.get_String(ws)?.Text;
					if (stripDottedCircles)
						text = StripDottedCircles(text);
					if (string.IsNullOrEmpty(text))
						continue;
					string tag = WsTag(ws);
					if (string.IsNullOrEmpty(tag))
					{
						Warn($"writing system handle {ws} does not resolve to a writing system; alternative skipped");
						continue;
					}
					forms.Add(new KeyValuePair<string, string>(tag, text));
				}
				forms.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
				return forms;
			}

			private void WriteWsFormArray(List<KeyValuePair<string, string>> forms)
			{
				m_json.WriteStartArray();
				foreach (var form in forms)
				{
					m_json.WriteStartObject();
					WriteProp("ws", form.Key);
					WriteProp("form", form.Value);
					m_json.WriteEndObject();
				}
				m_json.WriteEndArray();
			}

			#endregion

			#region project

			private void WriteProject()
			{
				m_json.WritePropertyName("project");
				m_json.WriteStartObject();
				WriteProp("name", m_cache.ProjectId.Name);
				IWritingSystemContainer wsContainer = m_cache.ServiceLocator.WritingSystems;
				WriteWsList("vernacularWritingSystems", wsContainer.DefaultVernacularWritingSystem,
					wsContainer.CurrentVernacularWritingSystems);
				WriteWsList("analysisWritingSystems", wsContainer.DefaultAnalysisWritingSystem,
					wsContainer.CurrentAnalysisWritingSystems);
				m_json.WriteEndObject();
			}

			private void WriteWsList(string name, CoreWritingSystemDefinition defaultWs,
				IEnumerable<CoreWritingSystemDefinition> currentWss)
			{
				var tags = new List<string>();
				if (defaultWs != null)
					tags.Add(defaultWs.Id);
				foreach (CoreWritingSystemDefinition ws in currentWss)
				{
					if (!tags.Contains(ws.Id))
						tags.Add(ws.Id);
				}
				if (tags.Count == 0)
					return;
				m_json.WritePropertyName(name);
				m_json.WriteStartArray();
				foreach (string tag in tags)
					m_json.WriteValue(tag);
				m_json.WriteEndArray();
			}

			#endregion

			#region featureSystems

			private void WriteFeatureSystems()
			{
				m_json.WritePropertyName("featureSystems");
				m_json.WriteStartObject();
				WriteFeatureSystem("phonological", m_langProject.PhFeatureSystemOA);
				WriteFeatureSystem("morphosyntactic", m_langProject.MsFeatureSystemOA);
				m_json.WriteEndObject();
			}

			private void WriteFeatureSystem(string name, IFsFeatureSystem system)
			{
				m_json.WritePropertyName(name);
				m_json.WriteStartObject();
				if (system != null)
				{
					var closed = ByGuid(system.FeaturesOC.OfType<IFsClosedFeature>()).ToList();
					if (closed.Count > 0)
					{
						m_json.WritePropertyName("closedFeatures");
						m_json.WriteStartArray();
						foreach (IFsClosedFeature feature in closed)
							WriteClosedFeature(feature);
						m_json.WriteEndArray();
					}
					var complex = ByGuid(system.FeaturesOC.OfType<IFsComplexFeature>()).ToList();
					if (complex.Count > 0)
					{
						m_json.WritePropertyName("complexFeatures");
						m_json.WriteStartArray();
						foreach (IFsComplexFeature feature in complex)
							WriteComplexFeature(feature);
						m_json.WriteEndArray();
					}
				}
				m_json.WriteEndObject();
			}

			private void WriteClosedFeature(IFsClosedFeature feature)
			{
				m_json.WriteStartObject();
				WriteProp("guid", GuidStr(feature));
				WriteProp("name", Best(feature.Name));
				WriteProp("abbreviation", Best(feature.Abbreviation));
				var values = ByGuid(feature.ValuesOC).ToList();
				if (values.Count > 0)
				{
					m_json.WritePropertyName("values");
					m_json.WriteStartArray();
					foreach (IFsSymFeatVal value in values)
					{
						m_json.WriteStartObject();
						WriteProp("guid", GuidStr(value));
						WriteProp("name", Best(value.Name));
						WriteProp("abbreviation", Best(value.Abbreviation));
						m_json.WriteEndObject();
					}
					m_json.WriteEndArray();
				}
				m_json.WriteEndObject();
			}

			private void WriteComplexFeature(IFsComplexFeature feature)
			{
				m_json.WriteStartObject();
				WriteProp("guid", GuidStr(feature));
				WriteProp("name", Best(feature.Name));
				WriteProp("abbreviation", Best(feature.Abbreviation));
				WriteGuidProp("featureType", feature.TypeRA);
				m_json.WriteEndObject();
			}

			/// <summary>
			/// Writes a feature structure property; omitted entirely when the structure is null or
			/// empty and <paramref name="alwaysEmit"/> is false.
			/// </summary>
			private void WriteFeatureStructure(string name, IFsFeatStruc fs, bool alwaysEmit = false)
			{
				if ((fs == null || fs.FeatureSpecsOC.Count == 0) && !alwaysEmit)
					return;
				m_json.WritePropertyName(name);
				WriteFeatureStructureValue(fs);
			}

			private void WriteFeatureStructureValue(IFsFeatStruc fs)
			{
				m_json.WriteStartObject();
				var specs = fs == null
					? new List<IFsFeatureSpecification>()
					: ByGuid(fs.FeatureSpecsOC).Where(IsWritableFeatureSpec).ToList();
				if (specs.Count > 0)
				{
					m_json.WritePropertyName("values");
					m_json.WriteStartArray();
					foreach (IFsFeatureSpecification spec in specs)
					{
						m_json.WriteStartObject();
						WriteProp("feature", GuidStr(spec.FeatureRA));
						m_json.WritePropertyName("value");
						m_json.WriteStartObject();
						if (spec is IFsClosedValue closedValue)
						{
							WriteProp("kind", "closed");
							WriteProp("value", GuidStr(closedValue.ValueRA));
						}
						else
						{
							var complexValue = (IFsComplexValue)spec;
							WriteProp("kind", "complex");
							m_json.WritePropertyName("value");
							WriteFeatureStructureValue((IFsFeatStruc)complexValue.ValueOA);
						}
						m_json.WriteEndObject();
						m_json.WriteEndObject();
					}
					m_json.WriteEndArray();
				}
				m_json.WriteEndObject();
			}

			private bool IsWritableFeatureSpec(IFsFeatureSpecification spec)
			{
				if (spec.FeatureRA == null)
				{
					Warn($"feature specification {GuidStr(spec)}: no feature; skipped");
					return false;
				}
				switch (spec)
				{
					case IFsClosedValue closedValue when closedValue.ValueRA != null:
						return true;
					case IFsComplexValue complexValue when complexValue.ValueOA is IFsFeatStruc:
						return true;
					default:
						Warn($"feature specification {GuidStr(spec)}: unsupported or empty value; skipped");
						return false;
				}
			}

			#endregion

			#region phonology

			private void WritePhonology()
			{
				m_json.WritePropertyName("phonology");
				m_json.WriteStartObject();
				IPhPhonData phonData = m_langProject.PhonologicalDataOA;
				if (phonData != null)
				{
					IPhPhonemeSet phonemeSet = phonData.PhonemeSetsOS.FirstOrDefault();
					if (phonData.PhonemeSetsOS.Count > 1)
						Warn("project has multiple phoneme sets; only the first is exported");
					if (phonemeSet != null)
					{
						WritePhonemes(phonemeSet);
						WriteBoundaryMarkers(phonemeSet);
					}
					WriteNaturalClasses(phonData);
					WriteEnvironments(phonData);
					WritePhonologicalRules(phonData);
					WriteFeatureConstraints(phonData);
				}
				m_json.WriteEndObject();
			}

			private void WritePhonemes(IPhPhonemeSet phonemeSet)
			{
				var phonemes = ByGuid(phonemeSet.PhonemesOC).ToList();
				if (phonemes.Count == 0)
					return;
				m_json.WritePropertyName("phonemes");
				m_json.WriteStartArray();
				foreach (IPhPhoneme phoneme in phonemes)
				{
					m_json.WriteStartObject();
					WriteProp("guid", GuidStr(phoneme));
					WriteProp("name", Best(phoneme.Name));
					WriteRepresentations(phoneme);
					WriteFeatureStructure("features", phoneme.FeaturesOA);
					string ipa = phoneme.BasicIPASymbol?.Text;
					if (!string.IsNullOrEmpty(ipa))
						WriteProp("basicIpaSymbol", ipa);
					m_json.WriteEndObject();
				}
				m_json.WriteEndArray();
			}

			/// <summary>
			/// Writes a terminal unit's "representations": each code's per-writing-system forms
			/// (dotted circles stripped), codes in model order, forms within a code sorted by tag.
			/// </summary>
			private void WriteRepresentations(IPhTerminalUnit unit)
			{
				var forms = new List<KeyValuePair<string, string>>();
				foreach (IPhCode code in unit.CodesOS)
					forms.AddRange(GetWsForms(code.Representation, stripDottedCircles: true));
				if (forms.Count == 0)
				{
					Warn($"phoneme or boundary marker {GuidStr(unit)} ({Best(unit.Name)}): no usable representations");
					return;
				}
				m_json.WritePropertyName("representations");
				WriteWsFormArray(forms);
			}

			private void WriteBoundaryMarkers(IPhPhonemeSet phonemeSet)
			{
				var markers = ByGuid(phonemeSet.BoundaryMarkersOC
					.Where(marker => marker.Guid != LangProjectTags.kguidPhRuleWordBdry)).ToList();
				if (markers.Count == 0)
					return;
				m_json.WritePropertyName("boundaryMarkers");
				m_json.WriteStartArray();
				foreach (IPhBdryMarker marker in markers)
				{
					m_json.WriteStartObject();
					WriteProp("guid", GuidStr(marker));
					WriteProp("name", Best(marker.Name));
					WriteRepresentations(marker);
					m_json.WriteEndObject();
				}
				m_json.WriteEndArray();
			}

			private bool IsWritableNaturalClass(IPhNaturalClass naturalClass)
			{
				if (naturalClass is IPhNCSegments || naturalClass is IPhNCFeatures)
					return true;
				Warn($"natural class {GuidStr(naturalClass)}: unsupported class {naturalClass.ClassName}; skipped");
				return false;
			}

			private void WriteNaturalClasses(IPhPhonData phonData)
			{
				var naturalClasses = phonData.NaturalClassesOS.Where(IsWritableNaturalClass).ToList();
				if (naturalClasses.Count == 0)
					return;
				m_json.WritePropertyName("naturalClasses");
				m_json.WriteStartArray();
				foreach (IPhNaturalClass naturalClass in naturalClasses)
				{
					switch (naturalClass)
					{
						case IPhNCSegments segments:
							m_json.WriteStartObject();
							WriteProp("kind", "segments");
							WriteProp("guid", GuidStr(segments));
							WriteProp("name", Best(segments.Abbreviation));
							WriteGuidArray("phonemes", segments.SegmentsRC, ordered: false);
							m_json.WriteEndObject();
							break;
						case IPhNCFeatures features:
							m_json.WriteStartObject();
							WriteProp("kind", "features");
							WriteProp("guid", GuidStr(features));
							WriteProp("name", Best(features.Abbreviation));
							WriteFeatureStructure("features", features.FeaturesOA, alwaysEmit: true);
							m_json.WriteEndObject();
							break;
					}
				}
				m_json.WriteEndArray();
			}

			private void WriteEnvironments(IPhPhonData phonData)
			{
				if (phonData.EnvironmentsOS.Count == 0)
					return;
				m_json.WritePropertyName("environments");
				m_json.WriteStartArray();
				foreach (IPhEnvironment environment in phonData.EnvironmentsOS)
				{
					m_json.WriteStartObject();
					WriteProp("guid", GuidStr(environment));
					WriteProp("name", Best(environment.Name));
					WriteProp("representation", environment.StringRepresentation?.Text);
					m_json.WriteEndObject();
				}
				m_json.WriteEndArray();
			}

			private bool IsWritablePhonologicalRule(IPhSegmentRule rule)
			{
				if (rule is IPhRegularRule || rule is IPhMetathesisRule)
					return true;
				Warn($"phonological rule {GuidStr(rule)}: unsupported class {rule.ClassName}; skipped");
				return false;
			}

			private void WritePhonologicalRules(IPhPhonData phonData)
			{
				var rules = phonData.PhonRulesOS.Where(rule => !rule.Disabled)
					.OrderBy(rule => rule.OrderNumber).Where(IsWritablePhonologicalRule).ToList();
				if (rules.Count == 0)
					return;
				m_json.WritePropertyName("rules");
				m_json.WriteStartArray();
				foreach (IPhSegmentRule rule in rules)
				{
					switch (rule)
					{
						case IPhRegularRule regularRule:
							WriteRewriteRule(regularRule);
							break;
						case IPhMetathesisRule metathesisRule:
							WriteMetathesisRule(metathesisRule);
							break;
					}
				}
				m_json.WriteEndArray();
			}

			private static string DirectionName(int direction)
			{
				switch (direction)
				{
					case 1:
						return "rightToLeft";
					case 2:
						return "simultaneous";
					default:
						return "leftToRight";
				}
			}

			private void WriteRewriteRule(IPhRegularRule rule)
			{
				m_json.WriteStartObject();
				WriteProp("kind", "rewrite");
				WriteProp("guid", GuidStr(rule));
				WriteProp("name", Best(rule.Name));
				WriteProp("direction", DirectionName(rule.Direction));
				WritePhonContexts("structuralDescription", rule.StrucDescOS);
				// The order of this enumeration is the order in which alpha variables are assigned
				// Greek letters downstream, so it is preserved as-is.
				WriteGuidArray("featureConstraintVariables", rule.FeatureConstraints, ordered: true);
				if (rule.RightHandSidesOS.Count > 0)
				{
					m_json.WritePropertyName("rightHandSides");
					m_json.WriteStartArray();
					foreach (IPhSegRuleRHS rhs in rule.RightHandSidesOS)
					{
						m_json.WriteStartObject();
						WritePhonContexts("structuralChange", rhs.StrucChangeOS);
						WritePhonContextProp("leftContext", rhs.LeftContextOA);
						WritePhonContextProp("rightContext", rhs.RightContextOA);
						WriteGuidArray("requiredPartsOfSpeech", rhs.InputPOSesRC, ordered: false);
						WriteRuleFeatures("requiredRuleFeatures", rhs.ReqRuleFeatsRC);
						WriteRuleFeatures("excludedRuleFeatures", rhs.ExclRuleFeatsRC);
						m_json.WriteEndObject();
					}
					m_json.WriteEndArray();
				}
				m_json.WriteEndObject();
			}

			private void WriteRuleFeatures(string name, IEnumerable<IPhPhonRuleFeat> ruleFeats)
			{
				// Each IPhPhonRuleFeat is a wrapper; the reference target is the wrapped item
				// (an inflection class or an exception-feature possibility).
				var items = new List<ICmObject>();
				foreach (IPhPhonRuleFeat ruleFeat in ruleFeats)
				{
					if (ruleFeat.ItemRA == null)
						Warn($"phonological rule feature {GuidStr(ruleFeat)}: no referenced item; skipped");
					else
						items.Add(ruleFeat.ItemRA);
				}
				WriteGuidArray(name, items, ordered: false);
			}

			private void WriteMetathesisRule(IPhMetathesisRule rule)
			{
				m_json.WriteStartObject();
				WriteProp("kind", "metathesis");
				WriteProp("guid", GuidStr(rule));
				WriteProp("name", Best(rule.Name));
				WriteProp("direction", DirectionName(rule.Direction));
				WritePhonContexts("structuralDescription", rule.StrucDescOS);
				WriteProp("leftSwitchIndex", rule.LeftSwitchIndex);
				WriteProp("rightSwitchIndex", rule.RightSwitchIndex);
				m_json.WriteEndObject();
			}

			private void WriteFeatureConstraints(IPhPhonData phonData)
			{
				var constraints = phonData.FeatConstraintsOS.Where(constraint =>
				{
					if (constraint.FeatureRA != null)
						return true;
					Warn($"feature constraint {GuidStr(constraint)}: no feature; skipped");
					return false;
				}).ToList();
				if (constraints.Count == 0)
					return;
				m_json.WritePropertyName("featureConstraints");
				m_json.WriteStartArray();
				foreach (IPhFeatureConstraint constraint in constraints)
				{
					m_json.WriteStartObject();
					WriteProp("guid", GuidStr(constraint));
					WriteProp("feature", GuidStr(constraint.FeatureRA));
					m_json.WriteEndObject();
				}
				m_json.WriteEndArray();
			}

			#endregion

			#region PhonContext

			/// <summary>Writes an array of pattern contexts; omitted when nothing is writable.</summary>
			private void WritePhonContexts(string name, IEnumerable<IPhContextOrVar> contexts)
			{
				var writable = contexts.Where(IsWritableContext).ToList();
				if (writable.Count == 0)
					return;
				m_json.WritePropertyName(name);
				m_json.WriteStartArray();
				foreach (IPhContextOrVar context in writable)
					WritePhonContextValue(context);
				m_json.WriteEndArray();
			}

			private void WritePhonContextProp(string name, IPhPhonContext context)
			{
				if (context == null || !IsWritableContext(context))
					return;
				m_json.WritePropertyName(name);
				WritePhonContextValue(context);
			}

			private bool IsWritableContext(IPhContextOrVar context)
			{
				switch (context)
				{
					case IPhSequenceContext sequence:
						// A sequence is only faithful if every member can be written; dropping
						// members would silently change what the pattern matches.
						if (sequence.MembersRS.All(IsWritableContext))
							return true;
						Warn($"sequence context {GuidStr(context)}: unrepresentable member; skipped");
						return false;
					case IPhVariable _:
						return true;
					case IPhIterationContext iteration:
						if (iteration.MemberRA != null && IsWritableContext(iteration.MemberRA))
							return true;
						Warn($"iteration context {GuidStr(context)}: no member; skipped");
						return false;
					case IPhSimpleContextSeg segment:
						if (segment.FeatureStructureRA != null)
							return true;
						Warn($"segment context {GuidStr(context)}: no phoneme; skipped");
						return false;
					case IPhSimpleContextNC naturalClass:
						if (naturalClass.FeatureStructureRA != null)
							return true;
						Warn($"natural-class context {GuidStr(context)}: no natural class; skipped");
						return false;
					case IPhSimpleContextBdry boundary:
						if (boundary.FeatureStructureRA != null)
							return true;
						Warn($"boundary context {GuidStr(context)}: no marker; skipped");
						return false;
					default:
						Warn($"pattern context {GuidStr(context)}: unsupported class {context.ClassName}; skipped");
						return false;
				}
			}

			private void WritePhonContextValue(IPhContextOrVar context)
			{
				m_json.WriteStartObject();
				switch (context)
				{
					case IPhSequenceContext sequence:
						WriteProp("kind", "sequence");
						m_json.WritePropertyName("members");
						m_json.WriteStartArray();
						foreach (IPhPhonContext member in sequence.MembersRS.Where(IsWritableContext))
							WritePhonContextValue(member);
						m_json.WriteEndArray();
						break;
					case IPhIterationContext iteration:
						WriteProp("kind", "iteration");
						WriteProp("min", iteration.Minimum);
						WriteProp("max", iteration.Maximum);
						m_json.WritePropertyName("member");
						WritePhonContextValue(iteration.MemberRA);
						break;
					case IPhSimpleContextSeg segment:
						WriteProp("kind", "segment");
						WriteProp("phoneme", GuidStr(segment.FeatureStructureRA));
						break;
					case IPhSimpleContextNC naturalClass:
						WriteProp("kind", "naturalClass");
						WriteProp("naturalClass", GuidStr(naturalClass.FeatureStructureRA));
						WriteGuidArray("plusVariables", naturalClass.PlusConstrRS, ordered: true);
						WriteGuidArray("minusVariables", naturalClass.MinusConstrRS, ordered: true);
						break;
					case IPhSimpleContextBdry boundary:
						if (boundary.FeatureStructureRA.Guid == LangProjectTags.kguidPhRuleWordBdry)
						{
							WriteProp("kind", "wordBoundary");
						}
						else
						{
							WriteProp("kind", "boundary");
							WriteProp("marker", GuidStr(boundary.FeatureStructureRA));
						}
						break;
					case IPhVariable _:
						WriteProp("kind", "variable");
						break;
				}
				m_json.WriteEndObject();
			}

			#endregion

			#region morphology

			private void WriteMorphology()
			{
				m_json.WritePropertyName("morphology");
				m_json.WriteStartObject();
				WritePartsOfSpeech();
				IMoMorphData morphData = m_langProject.MorphologicalDataOA;
				if (morphData != null)
					WriteCompoundRules(morphData);
				WriteAdhocProhibitions();
				WriteExceptionFeatures(morphData);
				WriteLexEntryInflTypes();
				WriteParserParameters(morphData);
				m_json.WriteEndObject();
			}

			private void WritePartsOfSpeech()
			{
				var topLevel = m_langProject.PartsOfSpeechOA?.PossibilitiesOS.OfType<IPartOfSpeech>().ToList();
				if (topLevel == null || topLevel.Count == 0)
					return;
				m_json.WritePropertyName("partsOfSpeech");
				m_json.WriteStartArray();
				foreach (IPartOfSpeech pos in topLevel)
					WritePartOfSpeech(pos);
				m_json.WriteEndArray();
			}

			private void WritePartOfSpeech(IPartOfSpeech pos)
			{
				m_json.WriteStartObject();
				WriteProp("guid", GuidStr(pos));
				WriteProp("name", Best(pos.Name));
				WriteProp("abbreviation", Best(pos.Abbreviation));
				var children = pos.SubPossibilitiesOS.OfType<IPartOfSpeech>().ToList();
				if (children.Count > 0)
				{
					m_json.WritePropertyName("children");
					m_json.WriteStartArray();
					foreach (IPartOfSpeech child in children)
						WritePartOfSpeech(child);
					m_json.WriteEndArray();
				}
				var inflectionClasses = ByGuid(pos.InflectionClassesOC).ToList();
				if (inflectionClasses.Count > 0)
				{
					m_json.WritePropertyName("inflectionClasses");
					m_json.WriteStartArray();
					foreach (IMoInflClass inflectionClass in inflectionClasses)
						WriteInflectionClass(inflectionClass);
					m_json.WriteEndArray();
				}
				WriteGuidProp("defaultInflectionClass", pos.DefaultInflectionClassRA);
				WriteGuidArray("inflectableFeatures", pos.InflectableFeatsRC, ordered: false);
				WriteStemNames(pos);
				WriteAffixSlots(pos);
				WriteAffixTemplates(pos);
				m_json.WriteEndObject();
			}

			private void WriteInflectionClass(IMoInflClass inflectionClass)
			{
				m_json.WriteStartObject();
				WriteProp("guid", GuidStr(inflectionClass));
				WriteProp("name", Best(inflectionClass.Name));
				WriteProp("abbreviation", Best(inflectionClass.Abbreviation));
				var children = ByGuid(inflectionClass.SubclassesOC).ToList();
				if (children.Count > 0)
				{
					m_json.WritePropertyName("children");
					m_json.WriteStartArray();
					foreach (IMoInflClass child in children)
						WriteInflectionClass(child);
					m_json.WriteEndArray();
				}
				m_json.WriteEndObject();
			}

			private void WriteStemNames(IPartOfSpeech pos)
			{
				var stemNames = ByGuid(pos.StemNamesOC).ToList();
				if (stemNames.Count == 0)
					return;
				m_json.WritePropertyName("stemNames");
				m_json.WriteStartArray();
				foreach (IMoStemName stemName in stemNames)
				{
					m_json.WriteStartObject();
					WriteProp("guid", GuidStr(stemName));
					WriteProp("name", Best(stemName.Name));
					string abbreviation = Best(stemName.Abbreviation);
					if (abbreviation.Length > 0)
						WriteProp("abbreviation", abbreviation);
					var regions = ByGuid(stemName.RegionsOC.Where(region => region.FeatureSpecsOC.Count > 0)).ToList();
					if (regions.Count > 0)
					{
						m_json.WritePropertyName("regions");
						m_json.WriteStartArray();
						foreach (IFsFeatStruc region in regions)
							WriteFeatureStructureValue(region);
						m_json.WriteEndArray();
					}
					m_json.WriteEndObject();
				}
				m_json.WriteEndArray();
			}

			private void WriteAffixSlots(IPartOfSpeech pos)
			{
				var slots = ByGuid(pos.AffixSlotsOC).ToList();
				if (slots.Count == 0)
					return;
				m_json.WritePropertyName("affixSlots");
				m_json.WriteStartArray();
				foreach (IMoInflAffixSlot slot in slots)
				{
					m_json.WriteStartObject();
					WriteProp("guid", GuidStr(slot));
					WriteProp("name", Best(slot.Name));
					WriteProp("optional", slot.Optional);
					m_json.WriteEndObject();
				}
				m_json.WriteEndArray();
			}

			private void WriteAffixTemplates(IPartOfSpeech pos)
			{
				if (pos.AffixTemplatesOS.Count == 0)
					return;
				m_json.WritePropertyName("affixTemplates");
				m_json.WriteStartArray();
				foreach (IMoInflAffixTemplate template in pos.AffixTemplatesOS)
				{
					m_json.WriteStartObject();
					WriteProp("guid", GuidStr(template));
					WriteProp("name", Best(template.Name));
					WriteProp("disabled", template.Disabled);
					WriteGuidArray("prefixSlots", template.PrefixSlotsRS, ordered: true);
					WriteGuidArray("suffixSlots", template.SuffixSlotsRS, ordered: true);
					WriteProp("isFinal", template.Final);
					m_json.WriteEndObject();
				}
				m_json.WriteEndArray();
			}

			private bool IsWritableCompoundRule(IMoCompoundRule rule)
			{
				if (rule is IMoEndoCompound || rule is IMoExoCompound)
					return true;
				Warn($"compound rule {GuidStr(rule)}: unsupported class {rule.ClassName}; skipped");
				return false;
			}

			private void WriteCompoundRules(IMoMorphData morphData)
			{
				var compoundRules = morphData.CompoundRulesOS.Where(IsWritableCompoundRule).ToList();
				if (compoundRules.Count == 0)
					return;
				m_json.WritePropertyName("compoundRules");
				m_json.WriteStartArray();
				foreach (IMoCompoundRule rule in compoundRules)
				{
					switch (rule)
					{
						case IMoEndoCompound endo:
							m_json.WriteStartObject();
							WriteProp("kind", "endocentric");
							WriteProp("guid", GuidStr(endo));
							WriteProp("name", Best(endo.Name));
							WriteProp("disabled", endo.Disabled);
							WriteProp("headLast", endo.HeadLast);
							WriteCompoundConstituent("left", endo.LeftMsaOA);
							WriteCompoundConstituent("right", endo.RightMsaOA);
							WriteCompoundOutcome("overriding", endo.OverridingMsaOA);
							m_json.WriteEndObject();
							break;
						case IMoExoCompound exo:
							m_json.WriteStartObject();
							WriteProp("kind", "exocentric");
							WriteProp("guid", GuidStr(exo));
							WriteProp("name", Best(exo.Name));
							WriteProp("disabled", exo.Disabled);
							WriteCompoundConstituent("left", exo.LeftMsaOA);
							WriteCompoundConstituent("right", exo.RightMsaOA);
							WriteCompoundOutcome("to", exo.ToMsaOA);
							m_json.WriteEndObject();
							break;
					}
				}
				m_json.WriteEndArray();
			}

			private void WriteCompoundConstituent(string name, IMoStemMsa msa)
			{
				m_json.WritePropertyName(name);
				m_json.WriteStartObject();
				if (msa != null)
				{
					WriteGuidProp("partOfSpeech", msa.PartOfSpeechRA);
					WriteGuidArray("exceptionFeatures", msa.ProdRestrictRC, ordered: false);
				}
				m_json.WriteEndObject();
			}

			private void WriteCompoundOutcome(string name, IMoStemMsa msa)
			{
				m_json.WritePropertyName(name);
				m_json.WriteStartObject();
				if (msa != null)
				{
					WriteGuidProp("partOfSpeech", msa.PartOfSpeechRA);
					WriteGuidProp("inflectionClass", msa.InflectionClassRA);
				}
				m_json.WriteEndObject();
			}

			// Mirrors HCLoader.GetAdjacency (FieldWorks ParserCore): 0 anywhere,
			// 1 somewhereToLeft, 2 somewhereToRight, 3 adjacentToLeft, 4 adjacentToRight.
			private static string AdjacencyName(int adjacency)
			{
				switch (adjacency)
				{
					case 1:
						return "somewhereToLeft";
					case 2:
						return "somewhereToRight";
					case 3:
						return "adjacentToLeft";
					case 4:
						return "adjacentToRight";
					default:
						return "anywhere";
				}
			}

			private void WriteAdhocProhibitions()
			{
				var allo = ByGuid(m_cache.ServiceLocator.GetInstance<IMoAlloAdhocProhibRepository>().AllInstances())
					.Cast<IMoAdhocProhib>();
				var morpheme = ByGuid(m_cache.ServiceLocator.GetInstance<IMoMorphAdhocProhibRepository>().AllInstances())
					.Cast<IMoAdhocProhib>();
				var prohibitions = allo.Concat(morpheme).ToList();
				bool wroteAny = false;
				foreach (IMoAdhocProhib prohibition in prohibitions)
				{
					switch (prohibition)
					{
						case IMoAlloAdhocProhib alloProhib:
							if (alloProhib.FirstAllomorphRA == null)
							{
								Warn($"allomorph ad hoc rule {GuidStr(prohibition)}: no primary allomorph; skipped");
								continue;
							}
							EnsureAdhocArrayStarted(ref wroteAny);
							m_json.WriteStartObject();
							WriteProp("kind", "allomorph");
							WriteProp("guid", GuidStr(prohibition));
							WriteProp("disabled", prohibition.Disabled);
							WriteProp("primary", GuidStr(alloProhib.FirstAllomorphRA));
							WriteGuidArray("others", alloProhib.RestOfAllosRS, ordered: true);
							WriteProp("adjacency", AdjacencyName(prohibition.Adjacency));
							m_json.WriteEndObject();
							break;
						case IMoMorphAdhocProhib morphProhib:
							if (morphProhib.FirstMorphemeRA == null)
							{
								Warn($"morpheme ad hoc rule {GuidStr(prohibition)}: no primary morpheme; skipped");
								continue;
							}
							EnsureAdhocArrayStarted(ref wroteAny);
							m_json.WriteStartObject();
							WriteProp("kind", "morpheme");
							WriteProp("guid", GuidStr(prohibition));
							WriteProp("disabled", prohibition.Disabled);
							WriteProp("primary", GuidStr(morphProhib.FirstMorphemeRA));
							WriteGuidArray("others", morphProhib.RestOfMorphsRS, ordered: true);
							WriteProp("adjacency", AdjacencyName(prohibition.Adjacency));
							m_json.WriteEndObject();
							break;
					}
				}
				if (wroteAny)
					m_json.WriteEndArray();
			}

			private void EnsureAdhocArrayStarted(ref bool wroteAny)
			{
				if (wroteAny)
					return;
				m_json.WritePropertyName("adhocProhibitions");
				m_json.WriteStartArray();
				wroteAny = true;
			}

			private void WriteExceptionFeatures(IMoMorphData morphData)
			{
				var features = new Dictionary<Guid, ICmPossibility>();
				if (morphData?.ProdRestrictOA != null)
				{
					foreach (ICmPossibility possibility in morphData.ProdRestrictOA.ReallyReallyAllPossibilities)
						features[possibility.Guid] = possibility;
				}
				IPhPhonData phonData = m_langProject.PhonologicalDataOA;
				if (phonData?.PhonRuleFeatsOA != null)
				{
					foreach (IPhPhonRuleFeat ruleFeat in phonData.PhonRuleFeatsOA.PossibilitiesOS.OfType<IPhPhonRuleFeat>())
					{
						// Inflection-class items are represented by morphology.partsOfSpeech's
						// inflection-class hierarchy; only possibility items are exception features.
						if (ruleFeat.ItemRA is ICmPossibility possibility && !(ruleFeat.ItemRA is IMoInflClass))
							features[possibility.Guid] = possibility;
					}
				}
				if (features.Count == 0)
					return;
				m_json.WritePropertyName("exceptionFeatures");
				m_json.WriteStartArray();
				foreach (ICmPossibility possibility in ByGuid(features.Values))
				{
					m_json.WriteStartObject();
					WriteProp("guid", GuidStr(possibility));
					WriteProp("name", Best(possibility.Name));
					WriteProp("abbreviation", Best(possibility.Abbreviation));
					m_json.WriteEndObject();
				}
				m_json.WriteEndArray();
			}

			private void WriteLexEntryInflTypes()
			{
				var inflTypes = ByGuid(m_cache.ServiceLocator.GetInstance<ILexEntryInflTypeRepository>().AllInstances()).ToList();
				if (inflTypes.Count == 0)
					return;
				m_json.WritePropertyName("lexEntryInflTypes");
				m_json.WriteStartArray();
				foreach (ILexEntryInflType inflType in inflTypes)
				{
					m_json.WriteStartObject();
					WriteProp("guid", GuidStr(inflType));
					WriteProp("name", Best(inflType.Name));
					WriteProp("abbreviation", Best(inflType.Abbreviation));
					WriteProp("glossPrepend", Best(inflType.GlossPrepend));
					WriteProp("glossAppend", Best(inflType.GlossAppend));
					WriteGuidArray("slots", inflType.SlotsRC, ordered: false);
					WriteFeatureStructure("inflectionFeatures", inflType.InflFeatsOA);
					m_json.WriteEndObject();
				}
				m_json.WriteEndArray();
			}

			private void WriteParserParameters(IMoMorphData morphData)
			{
				XElement hcElem = null;
				XElement compoundRulesElem = null;
				string parserParams = morphData?.ParserParameters;
				if (!string.IsNullOrWhiteSpace(parserParams))
				{
					try
					{
						XElement root = XElement.Parse(parserParams);
						hcElem = root.Element("HC");
						compoundRulesElem = root.Element("CompoundRules");
					}
					catch (System.Xml.XmlException e)
					{
						Warn($"parser parameters could not be parsed as XML ({e.Message}); defaults used");
					}
				}
				m_json.WritePropertyName("parserParameters");
				m_json.WriteStartObject();
				// Note the default-true polarity of notOnClitics: absent means true.
				WriteProp("notOnClitics", hcElem == null || ((bool?)hcElem.Element("NotOnClitics") ?? true));
				WriteProp("acceptUnspecifiedGraphemes", hcElem != null && ((bool?)hcElem.Element("AcceptUnspecifiedGraphemes") ?? false));
				WriteProp("noDefaultCompounding", hcElem != null && ((bool?)hcElem.Element("NoDefaultCompounding") ?? false));
				string strata = (string)hcElem?.Element("Strata");
				if (!string.IsNullOrEmpty(strata))
					WriteProp("strata", strata);
				if (compoundRulesElem != null)
				{
					bool wroteAny = false;
					foreach (XElement ruleElem in compoundRulesElem.Elements())
					{
						string guidValue = (string)ruleElem.Attribute("guid");
						string maxAppsValue = (string)ruleElem.Attribute("maxApps");
						if (!Guid.TryParse(guidValue, out Guid ruleGuid) || !int.TryParse(maxAppsValue, out int maxApps))
						{
							Warn($"parser parameters: malformed compound-rule max-applications entry ({ruleElem}); skipped");
							continue;
						}
						if (!wroteAny)
						{
							m_json.WritePropertyName("compoundRuleMaxApplications");
							m_json.WriteStartArray();
							wroteAny = true;
						}
						m_json.WriteStartObject();
						WriteProp("compoundRule", ruleGuid.ToString());
						WriteProp("maxApplications", maxApps);
						m_json.WriteEndObject();
					}
					if (wroteAny)
						m_json.WriteEndArray();
				}
				m_json.WriteEndObject();
			}

			#endregion

			#region lexicon

			private void WriteLexicon()
			{
				m_json.WritePropertyName("lexicon");
				m_json.WriteStartObject();
				var entries = ByGuid(m_cache.ServiceLocator.GetInstance<ILexEntryRepository>().AllInstances()).ToList();
				if (entries.Count > 0)
				{
					m_json.WritePropertyName("entries");
					m_json.WriteStartArray();
					foreach (ILexEntry entry in entries)
						WriteLexEntry(entry);
					m_json.WriteEndArray();
				}
				m_json.WriteEndObject();
			}

			private void WriteLexEntry(ILexEntry entry)
			{
				m_json.WriteStartObject();
				WriteProp("guid", GuidStr(entry));
				WriteWsForms("citationForm", entry.CitationForm);
				WriteProp("lexemeMorphType", GetLexemeMorphType(entry));
				// AlternateForms first, LexemeForm last: this order carries the disjunctive-order
				// semantics of allomorph selection.
				var allomorphs = entry.AlternateFormsOS.Concat(
					entry.LexemeFormOA == null ? Enumerable.Empty<IMoForm>() : new[] { entry.LexemeFormOA });
				m_json.WritePropertyName("allomorphs");
				m_json.WriteStartArray();
				foreach (IMoForm form in allomorphs)
					WriteAllomorph(form);
				m_json.WriteEndArray();
				var msas = ByGuid(entry.MorphoSyntaxAnalysesOC).Where(IsWritableMsa).ToList();
				if (msas.Count > 0)
				{
					m_json.WritePropertyName("msas");
					m_json.WriteStartArray();
					foreach (IMoMorphSynAnalysis msa in msas)
						WriteMsa(msa);
					m_json.WriteEndArray();
				}
				// AllSenses deliberately flattens the subsense tree (pre-order, parent before its
				// subsenses): the parser consumes senses as a flat list, and the reference
				// implementation of this format does the same.
				var senses = entry.AllSenses;
				if (senses.Count > 0)
				{
					m_json.WritePropertyName("senses");
					m_json.WriteStartArray();
					foreach (ILexSense sense in senses)
						WriteSense(sense);
					m_json.WriteEndArray();
				}
				if (entry.EntryRefsOS.Count > 0)
				{
					m_json.WritePropertyName("entryRefs");
					m_json.WriteStartArray();
					foreach (ILexEntryRef entryRef in entry.EntryRefsOS)
						WriteEntryRef(entryRef);
					m_json.WriteEndArray();
				}
				m_json.WriteEndObject();
			}

			private string GetLexemeMorphType(ILexEntry entry)
			{
				IMoMorphType morphType = entry.LexemeFormOA?.MorphTypeRA;
				if (morphType != null && MorphTypeNames.TryGetValue(morphType.Guid, out string name))
					return name;
				Warn($"entry {GuidStr(entry)}: no usable lexeme-form morph type; defaulting to stem");
				return "stem";
			}

			private void WriteAllomorph(IMoForm form)
			{
				if (form.MorphTypeRA == null || !MorphTypeNames.TryGetValue(form.MorphTypeRA.Guid, out string morphType))
				{
					string typeName = form.MorphTypeRA == null ? "(none)" : Best(form.MorphTypeRA.Name);
					Warn($"allomorph {GuidStr(form)}: unsupported morph type {typeName}; skipped");
					return;
				}
				// An affix process's output mappings reference input parts positionally
				// (1-based position in InputOS), so the whole allomorph must be skipped if any
				// input part cannot be written (dropping one would misalign the indices) or any
				// output mapping cannot be written (dropping one would change the recipe).
				if (form is IMoAffixProcess processForm &&
					(!processForm.InputOS.All(IsWritableContext) ||
						!processForm.OutputOS.All(mapping => IsWritableRuleMapping(mapping, processForm))))
				{
					Warn($"affix process {GuidStr(form)}: unrepresentable input or output part; allomorph skipped");
					return;
				}
				m_json.WriteStartObject();
				WriteProp("guid", GuidStr(form));
				WriteProp("morphType", morphType);
				WriteProp("isAbstract", form.IsAbstract);
				WriteWsForms("forms", form.Form);
				switch (form)
				{
					case IMoStemAllomorph stem:
						WriteGuidArray("environments", stem.PhoneEnvRC, ordered: false);
						WriteGuidProp("stemName", stem.StemNameRA);
						break;
					case IMoAffixAllomorph affix:
						WriteGuidArray("environments", affix.PhoneEnvRC, ordered: false);
						WriteGuidArray("positions", affix.PositionRS, ordered: true);
						WriteGuidArray("inflectionClasses", affix.InflectionClassesRC, ordered: false);
						WriteFeatureStructure("msEnvFeatures", affix.MsEnvFeaturesOA);
						WriteGuidProp("msEnvPartOfSpeech", affix.MsEnvPartOfSpeechRA);
						break;
					case IMoAffixProcess process:
						WriteGuidArray("inflectionClasses", process.InflectionClassesRC, ordered: false);
						WriteAffixProcess(process);
						break;
				}
				m_json.WriteEndObject();
			}

			private void WriteAffixProcess(IMoAffixProcess process)
			{
				m_json.WritePropertyName("process");
				m_json.WriteStartObject();
				WritePhonContexts("input", process.InputOS);
				if (process.OutputOS.Count > 0)
				{
					m_json.WritePropertyName("output");
					m_json.WriteStartArray();
					foreach (IMoRuleMapping mapping in process.OutputOS)
						WriteRuleMapping(mapping);
					m_json.WriteEndArray();
				}
				m_json.WriteEndObject();
			}

			/// <summary>
			/// Whether a rule mapping can be represented. Copy/modify mappings must reference a
			/// direct member of the owning process's input sequence — "part" is that member's
			/// 1-based position, so a reference to anything else (a nested context, another
			/// process's input) would emit a silently wrong index.
			/// </summary>
			private bool IsWritableRuleMapping(IMoRuleMapping mapping, IMoAffixProcess process)
			{
				switch (mapping)
				{
					case IMoInsertNC insertNC:
						if (insertNC.ContentRA != null)
							return true;
						Warn($"rule mapping {GuidStr(mapping)}: no natural class to insert; skipped");
						return false;
					case IMoCopyFromInput copy:
						if (copy.ContentRA != null && copy.ContentRA.Owner == process)
							return true;
						Warn($"rule mapping {GuidStr(mapping)}: input-part reference missing or not a top-level input part; skipped");
						return false;
					case IMoInsertPhones insertPhones:
						if (GetInsertPhonesText(insertPhones).Length > 0)
							return true;
						Warn($"rule mapping {GuidStr(mapping)}: no insertable segments; skipped");
						return false;
					case IMoModifyFromInput modify:
						if (modify.ContentRA != null && modify.ContentRA.Owner == process && modify.ModificationRA != null)
							return true;
						Warn($"rule mapping {GuidStr(mapping)}: incomplete modification or non-top-level input-part reference; skipped");
						return false;
					default:
						Warn($"rule mapping {GuidStr(mapping)}: unsupported class {mapping.ClassName}; skipped");
						return false;
				}
			}

			/// <summary>Writes one rule mapping, which IsWritableRuleMapping has already vetted.</summary>
			private void WriteRuleMapping(IMoRuleMapping mapping)
			{
				switch (mapping)
				{
					case IMoInsertNC insertNC:
						m_json.WriteStartObject();
						WriteProp("kind", "insertNaturalClass");
						WriteProp("naturalClass", GuidStr(insertNC.ContentRA));
						m_json.WriteEndObject();
						break;
					case IMoCopyFromInput copy:
						m_json.WriteStartObject();
						WriteProp("kind", "copyFromInput");
						WriteProp("part", copy.ContentRA.IndexInOwner + 1);
						m_json.WriteEndObject();
						break;
					case IMoInsertPhones insertPhones:
						m_json.WriteStartObject();
						WriteProp("kind", "insertSegments");
						WriteProp("text", GetInsertPhonesText(insertPhones));
						m_json.WriteEndObject();
						break;
					case IMoModifyFromInput modify:
						m_json.WriteStartObject();
						WriteProp("kind", "modifyFromInput");
						WriteProp("part", modify.ContentRA.IndexInOwner + 1);
						WriteProp("naturalClass", GuidStr(modify.ModificationRA));
						m_json.WriteEndObject();
						break;
				}
			}

			private string GetInsertPhonesText(IMoInsertPhones insertPhones)
			{
				var sb = new StringBuilder();
				foreach (IPhTerminalUnit unit in insertPhones.ContentRS)
				{
					IPhCode code = unit.CodesOS.FirstOrDefault();
					if (code == null)
						continue;
					// Boundary markers may only have a representation in a non-default vernacular
					// writing system, so fall back across vernacular writing systems for them.
					string text = unit is IPhBdryMarker
						? code.Representation.BestVernacularAlternative?.Text
						: code.Representation.VernacularDefaultWritingSystem?.Text;
					if (text == MissingValueSentinel)
						text = null;
					text = StripDottedCircles(text)?.Trim();
					if (!string.IsNullOrEmpty(text))
						sb.Append(text);
				}
				return sb.ToString();
			}

			private bool IsWritableMsa(IMoMorphSynAnalysis msa)
			{
				if (msa is IMoStemMsa || msa is IMoInflAffMsa || msa is IMoDerivAffMsa || msa is IMoUnclassifiedAffixMsa)
					return true;
				Warn($"MSA {GuidStr(msa)}: unsupported class {msa.ClassName}; skipped");
				return false;
			}

			/// <summary>Writes one MSA, which IsWritableMsa has already vetted.</summary>
			private void WriteMsa(IMoMorphSynAnalysis msa)
			{
				switch (msa)
				{
					case IMoStemMsa stem:
						m_json.WriteStartObject();
						WriteProp("kind", "stem");
						WriteProp("guid", GuidStr(stem));
						WriteGuidProp("partOfSpeech", stem.PartOfSpeechRA);
						WriteGuidProp("inflectionClass", stem.InflectionClassRA);
						WriteFeatureStructure("features", stem.MsFeaturesOA);
						WriteGuidArray("exceptionFeatures", stem.ProdRestrictRC, ordered: false);
						WriteGuidArray("fromPartsOfSpeech", stem.FromPartsOfSpeechRC, ordered: false);
						WriteGuidArray("slots", stem.SlotsRC, ordered: false);
						m_json.WriteEndObject();
						break;
					case IMoInflAffMsa inflectional:
						m_json.WriteStartObject();
						WriteProp("kind", "inflectional");
						WriteProp("guid", GuidStr(inflectional));
						WriteGuidProp("partOfSpeech", inflectional.PartOfSpeechRA);
						WriteGuidArray("slots", inflectional.SlotsRC, ordered: false);
						WriteFeatureStructure("features", inflectional.InflFeatsOA);
						WriteGuidArray("exceptionFeatures", inflectional.FromProdRestrictRC, ordered: false);
						m_json.WriteEndObject();
						break;
					case IMoDerivAffMsa derivational:
						m_json.WriteStartObject();
						WriteProp("kind", "derivational");
						WriteProp("guid", GuidStr(derivational));
						WriteGuidProp("fromPartOfSpeech", derivational.FromPartOfSpeechRA);
						WriteGuidProp("toPartOfSpeech", derivational.ToPartOfSpeechRA);
						WriteFeatureStructure("fromFeatures", derivational.FromMsFeaturesOA);
						WriteFeatureStructure("toFeatures", derivational.ToMsFeaturesOA);
						WriteGuidProp("fromInflectionClass", derivational.FromInflectionClassRA);
						WriteGuidProp("toInflectionClass", derivational.ToInflectionClassRA);
						WriteGuidArray("fromExceptionFeatures", derivational.FromProdRestrictRC, ordered: false);
						WriteGuidArray("toExceptionFeatures", derivational.ToProdRestrictRC, ordered: false);
						WriteGuidProp("fromStemName", derivational.FromStemNameRA);
						m_json.WriteEndObject();
						break;
					case IMoUnclassifiedAffixMsa unclassified:
						m_json.WriteStartObject();
						WriteProp("kind", "unclassified");
						WriteProp("guid", GuidStr(unclassified));
						WriteGuidProp("partOfSpeech", unclassified.PartOfSpeechRA);
						m_json.WriteEndObject();
						break;
				}
			}

			private void WriteSense(ILexSense sense)
			{
				m_json.WriteStartObject();
				WriteProp("guid", GuidStr(sense));
				WriteWsForms("gloss", sense.Gloss);
				WriteWsForms("definition", sense.Definition);
				// Normally a sense's MSA belongs to its own entry, but stray references into
				// another entry's msas exist in real projects; carry them, but never silently.
				if (sense.MorphoSyntaxAnalysisRA != null && sense.MorphoSyntaxAnalysisRA.Owner != sense.Entry)
				{
					Warn($"sense {GuidStr(sense)}: its MSA {GuidStr(sense.MorphoSyntaxAnalysisRA)} " +
						"belongs to a different entry; exported as-is (resolve msa references document-wide)");
				}
				WriteGuidProp("msa", sense.MorphoSyntaxAnalysisRA);
				m_json.WriteEndObject();
			}

			private void WriteEntryRef(ILexEntryRef entryRef)
			{
				m_json.WriteStartObject();
				// The FieldWorks UI treats variant and complex-form types as mutually exclusive;
				// a ref is a complex form only when it has complex-form types and no variant types.
				// Anything else — variant types only, both kinds, or neither — is a variant.
				if (entryRef.ComplexEntryTypesRS.Count > 0 && entryRef.VariantEntryTypesRS.Count == 0)
				{
					WriteProp("kind", "complexForm");
					WriteProp("guid", GuidStr(entryRef));
					WriteGuidArray("componentLexemes", entryRef.ComponentLexemesRS, ordered: true);
					WriteGuidArray("complexEntryTypes", entryRef.ComplexEntryTypesRS, ordered: true);
				}
				else
				{
					if (entryRef.ComplexEntryTypesRS.Count > 0)
						Warn($"entry ref {GuidStr(entryRef)}: has both variant and complex-form types; exported as a variant, complex-form types dropped");
					WriteProp("kind", "variant");
					WriteProp("guid", GuidStr(entryRef));
					WriteGuidArray("componentLexemes", entryRef.ComponentLexemesRS, ordered: true);
					WriteGuidArray("variantEntryTypes", entryRef.VariantEntryTypesRS, ordered: true);
				}
				m_json.WriteEndObject();
			}

			#endregion
		}
	}
}
