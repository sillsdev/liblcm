// Copyright (c) 2026 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SIL.LCModel.Core.Text;

namespace SIL.LCModel.DomainServices
{
	/// <summary>
	/// Tests for <see cref="GrammarJsonServices"/> (LCM Grammar JSON export).
	/// </summary>
	[TestFixture]
	public class GrammarJsonServicesTests : MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{
		private JObject Export()
		{
			return JObject.Parse(GrammarJsonServices.ExportGrammar(Cache));
		}

		private ILexEntry MakeStemEntry(string form, string gloss)
		{
			ILexEntry entry = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
			IMoStemAllomorph lexemeForm = Cache.ServiceLocator.GetInstance<IMoStemAllomorphFactory>().Create();
			entry.LexemeFormOA = lexemeForm;
			lexemeForm.MorphTypeRA = Cache.ServiceLocator.GetInstance<IMoMorphTypeRepository>()
				.GetObject(MoMorphTypeTags.kguidMorphStem);
			lexemeForm.Form.SetVernacularDefaultWritingSystem(form);
			entry.CitationForm.SetVernacularDefaultWritingSystem(form);
			ILexSense sense = Cache.ServiceLocator.GetInstance<ILexSenseFactory>().Create();
			entry.SensesOS.Add(sense);
			sense.Gloss.SetAnalysisDefaultWritingSystem(gloss);
			return entry;
		}

		private IPartOfSpeech MakePartOfSpeech(string name)
		{
			IPartOfSpeech pos = Cache.ServiceLocator.GetInstance<IPartOfSpeechFactory>().Create();
			Cache.LangProject.PartsOfSpeechOA.PossibilitiesOS.Add(pos);
			pos.Name.SetAnalysisDefaultWritingSystem(name);
			pos.Abbreviation.SetAnalysisDefaultWritingSystem(name.Substring(0, 1));
			return pos;
		}

		private IPhPhonemeSet EnsurePhonemeSet()
		{
			IPhPhonData phonData = Cache.LangProject.PhonologicalDataOA;
			if (phonData.PhonemeSetsOS.Count == 0)
				phonData.PhonemeSetsOS.Add(Cache.ServiceLocator.GetInstance<IPhPhonemeSetFactory>().Create());
			return phonData.PhonemeSetsOS[0];
		}

		/// <summary>The envelope and all sections are present even for an empty project.</summary>
		[Test]
		public void ExportGrammar_EmptyProject_WritesEnvelopeAndSections()
		{
			JObject json = Export();

			Assert.AreEqual(GrammarJsonServices.FormatName, (string)json["format"]);
			Assert.AreEqual(GrammarJsonServices.FormatVersion, (int)json["version"]);
			Assert.IsNotNull(json["project"], "project section");
			Assert.IsNotNull((string)json["project"]["name"], "project name");
			Assert.IsTrue(json["project"]["vernacularWritingSystems"].Any(), "vernacular writing systems");
			Assert.IsTrue(json["project"]["analysisWritingSystems"].Any(), "analysis writing systems");
			Assert.IsNotNull(json["featureSystems"]["phonological"], "phonological feature system");
			Assert.IsNotNull(json["featureSystems"]["morphosyntactic"], "morphosyntactic feature system");
			Assert.IsNotNull(json["phonology"], "phonology section");
			Assert.IsNotNull(json["morphology"], "morphology section");
			Assert.IsNotNull(json["lexicon"], "lexicon section");
			// Empty collections are omitted, not written as [].
			Assert.IsNull(json["lexicon"]["entries"], "empty lexicon should omit entries");
			AssertValidatesAgainstSchema(json.ToString(), "empty project");
		}

		/// <summary>Two exports of the same project are byte-identical.</summary>
		[Test]
		public void ExportGrammar_IsDeterministic()
		{
			IPartOfSpeech pos = MakePartOfSpeech("verb");
			ILexEntry entry = MakeStemEntry("kick", "kick");
			IMoStemMsa msa = Cache.ServiceLocator.GetInstance<IMoStemMsaFactory>().Create();
			entry.MorphoSyntaxAnalysesOC.Add(msa);
			msa.PartOfSpeechRA = pos;
			MakeStemEntry("sing", "sing");
			IPhPhonemeSet phonemeSet = EnsurePhonemeSet();
			IPhPhoneme phoneme = Cache.ServiceLocator.GetInstance<IPhPhonemeFactory>().Create();
			phonemeSet.PhonemesOC.Add(phoneme);
			IPhCode code = Cache.ServiceLocator.GetInstance<IPhCodeFactory>().Create();
			phoneme.CodesOS.Add(code);
			code.Representation.SetVernacularDefaultWritingSystem("a");

			string first = GrammarJsonServices.ExportGrammar(Cache);
			string second = GrammarJsonServices.ExportGrammar(Cache);

			Assert.AreEqual(first, second);
		}

		/// <summary>A stem entry round-trips its core fields.</summary>
		[Test]
		public void ExportGrammar_WritesLexEntryCoreFields()
		{
			IPartOfSpeech pos = MakePartOfSpeech("verb");
			ILexEntry entry = MakeStemEntry("kick", "kick");
			ILexSense sense = entry.SensesOS[0];
			sense.Definition.SetAnalysisDefaultWritingSystem("to strike with the foot");
			IMoStemMsa msa = Cache.ServiceLocator.GetInstance<IMoStemMsaFactory>().Create();
			entry.MorphoSyntaxAnalysesOC.Add(msa);
			msa.PartOfSpeechRA = pos;
			sense.MorphoSyntaxAnalysisRA = msa;

			JObject json = Export();

			var entries = (JArray)json["lexicon"]["entries"];
			Assert.AreEqual(1, entries.Count);
			JObject jsonEntry = (JObject)entries[0];
			Assert.AreEqual(entry.Guid.ToString(), (string)jsonEntry["guid"]);
			Assert.AreEqual("stem", (string)jsonEntry["lexemeMorphType"]);
			Assert.AreEqual("kick", (string)jsonEntry["citationForm"][0]["form"]);

			var allomorphs = (JArray)jsonEntry["allomorphs"];
			Assert.AreEqual(1, allomorphs.Count, "lexeme form should be the single allomorph");
			Assert.AreEqual("stem", (string)allomorphs[0]["morphType"]);
			Assert.AreEqual("kick", (string)allomorphs[0]["forms"][0]["form"]);

			JObject jsonMsa = (JObject)jsonEntry["msas"][0];
			Assert.AreEqual("stem", (string)jsonMsa["kind"]);
			Assert.AreEqual(msa.Guid.ToString(), (string)jsonMsa["guid"]);
			Assert.AreEqual(pos.Guid.ToString(), (string)jsonMsa["partOfSpeech"]);

			JObject jsonSense = (JObject)jsonEntry["senses"][0];
			Assert.AreEqual(sense.Guid.ToString(), (string)jsonSense["guid"]);
			Assert.AreEqual("kick", (string)jsonSense["gloss"][0]["form"]);
			Assert.AreEqual("to strike with the foot", (string)jsonSense["definition"][0]["form"]);
			Assert.AreEqual(msa.Guid.ToString(), (string)jsonSense["msa"]);
		}

		/// <summary>Lexical entries (an unordered collection) are sorted by GUID string.</summary>
		[Test]
		public void ExportGrammar_SortsEntriesByGuid()
		{
			for (int i = 0; i < 5; i++)
				MakeStemEntry("form" + i, "gloss" + i);

			JObject json = Export();

			var guids = json["lexicon"]["entries"].Select(e => (string)e["guid"]).ToList();
			var sorted = guids.OrderBy(g => g, StringComparer.Ordinal).ToList();
			CollectionAssert.AreEqual(sorted, guids);
		}

		/// <summary>Phonemes and environments are exported with their representations.</summary>
		[Test]
		public void ExportGrammar_WritesPhonology()
		{
			IPhPhonemeSet phonemeSet = EnsurePhonemeSet();
			IPhPhoneme phoneme = Cache.ServiceLocator.GetInstance<IPhPhonemeFactory>().Create();
			phonemeSet.PhonemesOC.Add(phoneme);
			phoneme.Name.SetVernacularDefaultWritingSystem("a");
			IPhCode code = Cache.ServiceLocator.GetInstance<IPhCodeFactory>().Create();
			phoneme.CodesOS.Add(code);
			code.Representation.SetVernacularDefaultWritingSystem("a");
			IPhEnvironment environment = Cache.ServiceLocator.GetInstance<IPhEnvironmentFactory>().Create();
			Cache.LangProject.PhonologicalDataOA.EnvironmentsOS.Add(environment);
			environment.StringRepresentation = TsStringUtils.MakeString("/_[C]", Cache.DefaultVernWs);

			JObject json = Export();

			JObject jsonPhoneme = (JObject)json["phonology"]["phonemes"]
				.Single(p => (string)p["guid"] == phoneme.Guid.ToString());
			Assert.AreEqual("a", (string)jsonPhoneme["name"],
				"a phoneme name authored only in a vernacular writing system must not be lost");
			Assert.AreEqual("a", (string)jsonPhoneme["representations"][0]["form"]);
			JObject jsonEnvironment = (JObject)json["phonology"]["environments"]
				.Single(e => (string)e["guid"] == environment.Guid.ToString());
			Assert.AreEqual("/_[C]", (string)jsonEnvironment["representation"]);
		}

		/// <summary>Parser parameters fall back to the documented defaults.</summary>
		[Test]
		public void ExportGrammar_ParserParameterDefaults()
		{
			Cache.LangProject.MorphologicalDataOA.ParserParameters = string.Empty;

			JObject json = Export();

			JObject parameters = (JObject)json["morphology"]["parserParameters"];
			Assert.IsTrue((bool)parameters["notOnClitics"], "notOnClitics defaults to true");
			Assert.IsFalse((bool)parameters["acceptUnspecifiedGraphemes"]);
			Assert.IsFalse((bool)parameters["noDefaultCompounding"]);
			Assert.IsNull(parameters["strata"]);
			Assert.IsNull(parameters["compoundRuleMaxApplications"]);
		}

		/// <summary>Explicit parser parameters are read from the stored XML.</summary>
		[Test]
		public void ExportGrammar_ParserParametersFromXml()
		{
			Guid ruleGuid = Guid.NewGuid();
			Cache.LangProject.MorphologicalDataOA.ParserParameters =
				"<ParserParameters><HC><NoDefaultCompounding>true</NoDefaultCompounding>" +
				"<Strata>Morphology,Phonology</Strata></HC>" +
				$"<CompoundRules><CompoundRule guid=\"{ruleGuid}\" maxApps=\"3\"/></CompoundRules></ParserParameters>";

			JObject json = Export();

			JObject parameters = (JObject)json["morphology"]["parserParameters"];
			Assert.IsTrue((bool)parameters["notOnClitics"], "absent notOnClitics still defaults to true");
			Assert.IsTrue((bool)parameters["noDefaultCompounding"]);
			Assert.AreEqual("Morphology,Phonology", (string)parameters["strata"]);
			JObject maxApps = (JObject)parameters["compoundRuleMaxApplications"][0];
			Assert.AreEqual(ruleGuid.ToString(), (string)maxApps["compoundRule"]);
			Assert.AreEqual(3, (int)maxApps["maxApplications"]);
		}

		/// <summary>
		/// An entry ref is a complex form only when it has complex-form types and no variant
		/// types; a ref carrying both kinds is a variant.
		/// </summary>
		[Test]
		public void ExportGrammar_EntryRefWithBothTypeKinds_IsVariant()
		{
			ILexEntry main = MakeStemEntry("go", "go");
			ILexEntry variant = MakeStemEntry("went", "went");
			ILexEntryRef entryRef = Cache.ServiceLocator.GetInstance<ILexEntryRefFactory>().Create();
			variant.EntryRefsOS.Add(entryRef);
			entryRef.ComponentLexemesRS.Add(main);
			ILexDb lexDb = Cache.LangProject.LexDbOA;
			if (lexDb.VariantEntryTypesOA == null)
				lexDb.VariantEntryTypesOA = Cache.ServiceLocator.GetInstance<ICmPossibilityListFactory>().Create();
			if (lexDb.ComplexEntryTypesOA == null)
				lexDb.ComplexEntryTypesOA = Cache.ServiceLocator.GetInstance<ICmPossibilityListFactory>().Create();
			ILexEntryType variantType = Cache.ServiceLocator.GetInstance<ILexEntryTypeFactory>().Create();
			lexDb.VariantEntryTypesOA.PossibilitiesOS.Add(variantType);
			entryRef.VariantEntryTypesRS.Add(variantType);
			ILexEntryType complexType = Cache.ServiceLocator.GetInstance<ILexEntryTypeFactory>().Create();
			lexDb.ComplexEntryTypesOA.PossibilitiesOS.Add(complexType);
			entryRef.ComplexEntryTypesRS.Add(complexType);

			var warnings = new List<string>();
			JObject json = JObject.Parse(GrammarJsonServices.ExportGrammar(Cache, warnings));

			JObject jsonEntry = (JObject)json["lexicon"]["entries"]
				.Single(e => (string)e["guid"] == variant.Guid.ToString());
			JObject jsonRef = (JObject)jsonEntry["entryRefs"][0];
			Assert.AreEqual("variant", (string)jsonRef["kind"]);
			Assert.AreEqual(main.Guid.ToString(), (string)jsonRef["componentLexemes"][0]);
			Assert.AreEqual(variantType.Guid.ToString(), (string)jsonRef["variantEntryTypes"][0]);
			Assert.IsTrue(warnings.Any(w => w.Contains(entryRef.Guid.ToString())),
				"dropping the complex-form types must be reported in warnings");
		}

		/// <summary>An affix process exports its input parts and output mappings.</summary>
		[Test]
		public void ExportGrammar_WritesAffixProcess()
		{
			ILexEntry entry = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
			IMoAffixProcess process = Cache.ServiceLocator.GetInstance<IMoAffixProcessFactory>().Create();
			entry.LexemeFormOA = process;
			process.MorphTypeRA = Cache.ServiceLocator.GetInstance<IMoMorphTypeRepository>()
				.GetObject(MoMorphTypeTags.kguidMorphSuffix);
			IPhVariable variable = Cache.ServiceLocator.GetInstance<IPhVariableFactory>().Create();
			process.InputOS.Add(variable);
			IMoCopyFromInput copy = Cache.ServiceLocator.GetInstance<IMoCopyFromInputFactory>().Create();
			process.OutputOS.Add(copy);
			copy.ContentRA = variable;

			JObject json = Export();

			JObject jsonProcess = (JObject)json["lexicon"]["entries"][0]["allomorphs"][0]["process"];
			Assert.AreEqual("variable", (string)jsonProcess["input"][0]["kind"]);
			Assert.AreEqual("copyFromInput", (string)jsonProcess["output"][0]["kind"]);
			Assert.AreEqual(1, (int)jsonProcess["output"][0]["part"]);
		}

		/// <summary>
		/// An affix process with an unrepresentable input part is skipped entirely: output
		/// mappings reference input parts by position, so dropping one part would silently
		/// misalign every index after it.
		/// </summary>
		[Test]
		public void ExportGrammar_SkipsAffixProcessWithUnrepresentableInput()
		{
			ILexEntry entry = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
			IMoAffixProcess process = Cache.ServiceLocator.GetInstance<IMoAffixProcessFactory>().Create();
			entry.LexemeFormOA = process;
			process.MorphTypeRA = Cache.ServiceLocator.GetInstance<IMoMorphTypeRepository>()
				.GetObject(MoMorphTypeTags.kguidMorphSuffix);
			// A segment context with no phoneme reference cannot be written.
			IPhSimpleContextSeg broken = Cache.ServiceLocator.GetInstance<IPhSimpleContextSegFactory>().Create();
			process.InputOS.Add(broken);
			process.InputOS.Add(Cache.ServiceLocator.GetInstance<IPhVariableFactory>().Create());

			var warnings = new List<string>();
			JObject json = JObject.Parse(GrammarJsonServices.ExportGrammar(Cache, warnings));

			Assert.AreEqual(0, ((JArray)json["lexicon"]["entries"][0]["allomorphs"]).Count);
			Assert.IsTrue(warnings.Any(w => w.Contains(process.Guid.ToString()) && w.Contains("allomorph skipped")),
				"skipped process should be reported in warnings");
		}

		/// <summary>A morpheme ad hoc rule exports its members and adjacency.</summary>
		[Test]
		public void ExportGrammar_WritesMorphemeAdhocProhibition()
		{
			ILexEntry first = MakeStemEntry("kick", "kick");
			IMoStemMsa firstMsa = Cache.ServiceLocator.GetInstance<IMoStemMsaFactory>().Create();
			first.MorphoSyntaxAnalysesOC.Add(firstMsa);
			ILexEntry other = MakeStemEntry("sing", "sing");
			IMoStemMsa otherMsa = Cache.ServiceLocator.GetInstance<IMoStemMsaFactory>().Create();
			other.MorphoSyntaxAnalysesOC.Add(otherMsa);
			IMoMorphAdhocProhib prohibition = Cache.ServiceLocator.GetInstance<IMoMorphAdhocProhibFactory>().Create();
			Cache.LangProject.MorphologicalDataOA.AdhocCoProhibitionsOC.Add(prohibition);
			prohibition.FirstMorphemeRA = firstMsa;
			prohibition.RestOfMorphsRS.Add(otherMsa);
			prohibition.Adjacency = 3;

			JObject json = Export();

			JObject jsonProhibition = (JObject)json["morphology"]["adhocProhibitions"][0];
			Assert.AreEqual("morpheme", (string)jsonProhibition["kind"]);
			Assert.AreEqual(firstMsa.Guid.ToString(), (string)jsonProhibition["primary"]);
			Assert.AreEqual(otherMsa.Guid.ToString(), (string)jsonProhibition["others"][0]);
			Assert.AreEqual("adjacentToLeft", (string)jsonProhibition["adjacency"]);
		}

		/// <summary>
		/// The export validates against the published contract (doc/lcm-grammar.schema.json) —
		/// both for an empty project and for a project exercising lexicon, phonology, MSAs,
		/// senses, entry refs, affix processes, ad hoc rules, and parser parameters.
		/// </summary>
		[Test]
		public void ExportGrammar_ValidatesAgainstPublishedSchema()
		{
			// Lexicon: stem entry with POS, MSA, sense (gloss + definition), citation form.
			IPartOfSpeech pos = MakePartOfSpeech("verb");
			ILexEntry entry = MakeStemEntry("kick", "kick");
			entry.SensesOS[0].Definition.SetAnalysisDefaultWritingSystem("to strike with the foot");
			IMoStemMsa msa = Cache.ServiceLocator.GetInstance<IMoStemMsaFactory>().Create();
			entry.MorphoSyntaxAnalysesOC.Add(msa);
			msa.PartOfSpeechRA = pos;
			entry.SensesOS[0].MorphoSyntaxAnalysisRA = msa;
			// Variant entry ref.
			ILexEntry variant = MakeStemEntry("kicked", "kicked");
			ILexEntryRef entryRef = Cache.ServiceLocator.GetInstance<ILexEntryRefFactory>().Create();
			variant.EntryRefsOS.Add(entryRef);
			entryRef.ComponentLexemesRS.Add(entry);
			// Affix process with a copy mapping.
			ILexEntry affixEntry = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
			IMoAffixProcess process = Cache.ServiceLocator.GetInstance<IMoAffixProcessFactory>().Create();
			affixEntry.LexemeFormOA = process;
			process.MorphTypeRA = Cache.ServiceLocator.GetInstance<IMoMorphTypeRepository>()
				.GetObject(MoMorphTypeTags.kguidMorphSuffix);
			IPhVariable variable = Cache.ServiceLocator.GetInstance<IPhVariableFactory>().Create();
			process.InputOS.Add(variable);
			IMoCopyFromInput copy = Cache.ServiceLocator.GetInstance<IMoCopyFromInputFactory>().Create();
			process.OutputOS.Add(copy);
			copy.ContentRA = variable;
			// Morpheme ad hoc rule.
			IMoMorphAdhocProhib prohibition = Cache.ServiceLocator.GetInstance<IMoMorphAdhocProhibFactory>().Create();
			Cache.LangProject.MorphologicalDataOA.AdhocCoProhibitionsOC.Add(prohibition);
			prohibition.FirstMorphemeRA = msa;
			IMoStemMsa otherMsa = Cache.ServiceLocator.GetInstance<IMoStemMsaFactory>().Create();
			variant.MorphoSyntaxAnalysesOC.Add(otherMsa);
			prohibition.RestOfMorphsRS.Add(otherMsa);
			prohibition.Adjacency = 2;
			// Phonology: phoneme, environment, natural class, rewrite rule.
			IPhPhonemeSet phonemeSet = EnsurePhonemeSet();
			IPhPhoneme phoneme = Cache.ServiceLocator.GetInstance<IPhPhonemeFactory>().Create();
			phonemeSet.PhonemesOC.Add(phoneme);
			IPhCode code = Cache.ServiceLocator.GetInstance<IPhCodeFactory>().Create();
			phoneme.CodesOS.Add(code);
			code.Representation.SetVernacularDefaultWritingSystem("a");
			IPhEnvironment environment = Cache.ServiceLocator.GetInstance<IPhEnvironmentFactory>().Create();
			Cache.LangProject.PhonologicalDataOA.EnvironmentsOS.Add(environment);
			environment.StringRepresentation = TsStringUtils.MakeString("/_[C]", Cache.DefaultVernWs);
			IPhNCSegments naturalClass = Cache.ServiceLocator.GetInstance<IPhNCSegmentsFactory>().Create();
			Cache.LangProject.PhonologicalDataOA.NaturalClassesOS.Add(naturalClass);
			naturalClass.Abbreviation.SetAnalysisDefaultWritingSystem("C");
			naturalClass.SegmentsRC.Add(phoneme);
			IPhRegularRule rewriteRule = Cache.ServiceLocator.GetInstance<IPhRegularRuleFactory>().Create();
			Cache.LangProject.PhonologicalDataOA.PhonRulesOS.Add(rewriteRule);
			IPhSimpleContextSeg ruleInput = Cache.ServiceLocator.GetInstance<IPhSimpleContextSegFactory>().Create();
			rewriteRule.StrucDescOS.Add(ruleInput);
			ruleInput.FeatureStructureRA = phoneme;
			IPhSegRuleRHS rhs = Cache.ServiceLocator.GetInstance<IPhSegRuleRHSFactory>().Create();
			rewriteRule.RightHandSidesOS.Add(rhs);
			IPhSimpleContextNC ruleChange = Cache.ServiceLocator.GetInstance<IPhSimpleContextNCFactory>().Create();
			rhs.StrucChangeOS.Add(ruleChange);
			ruleChange.FeatureStructureRA = naturalClass;
			// More rule-mapping kinds on the affix process.
			IMoInsertPhones insertPhones = Cache.ServiceLocator.GetInstance<IMoInsertPhonesFactory>().Create();
			process.OutputOS.Add(insertPhones);
			insertPhones.ContentRS.Add(phoneme);
			// Morphology: compound rule, affix slot + template, exception feature,
			// irregularly-inflected-form type, inflectional and derivational MSAs.
			IMoEndoCompound compoundRule = Cache.ServiceLocator.GetInstance<IMoEndoCompoundFactory>().Create();
			Cache.LangProject.MorphologicalDataOA.CompoundRulesOS.Add(compoundRule);
			compoundRule.HeadLast = true;
			if (compoundRule.LeftMsaOA == null)
				compoundRule.LeftMsaOA = Cache.ServiceLocator.GetInstance<IMoStemMsaFactory>().Create();
			compoundRule.LeftMsaOA.PartOfSpeechRA = pos;
			IMoInflAffixSlot slot = Cache.ServiceLocator.GetInstance<IMoInflAffixSlotFactory>().Create();
			pos.AffixSlotsOC.Add(slot);
			slot.Optional = true;
			IMoInflAffixTemplate template = Cache.ServiceLocator.GetInstance<IMoInflAffixTemplateFactory>().Create();
			pos.AffixTemplatesOS.Add(template);
			template.SuffixSlotsRS.Add(slot);
			if (Cache.LangProject.MorphologicalDataOA.ProdRestrictOA == null)
			{
				Cache.LangProject.MorphologicalDataOA.ProdRestrictOA =
					Cache.ServiceLocator.GetInstance<ICmPossibilityListFactory>().Create();
			}
			ICmPossibility restriction = Cache.ServiceLocator.GetInstance<ICmPossibilityFactory>().Create();
			Cache.LangProject.MorphologicalDataOA.ProdRestrictOA.PossibilitiesOS.Add(restriction);
			msa.ProdRestrictRC.Add(restriction);
			ILexDb lexDb = Cache.LangProject.LexDbOA;
			if (lexDb.VariantEntryTypesOA == null)
				lexDb.VariantEntryTypesOA = Cache.ServiceLocator.GetInstance<ICmPossibilityListFactory>().Create();
			ILexEntryInflType inflType = Cache.ServiceLocator.GetInstance<ILexEntryInflTypeFactory>().Create();
			lexDb.VariantEntryTypesOA.PossibilitiesOS.Add(inflType);
			inflType.SlotsRC.Add(slot);
			IMoInflAffMsa inflMsa = Cache.ServiceLocator.GetInstance<IMoInflAffMsaFactory>().Create();
			affixEntry.MorphoSyntaxAnalysesOC.Add(inflMsa);
			inflMsa.PartOfSpeechRA = pos;
			inflMsa.SlotsRC.Add(slot);
			IMoDerivAffMsa derivMsa = Cache.ServiceLocator.GetInstance<IMoDerivAffMsaFactory>().Create();
			affixEntry.MorphoSyntaxAnalysesOC.Add(derivMsa);
			derivMsa.FromPartOfSpeechRA = pos;
			derivMsa.ToPartOfSpeechRA = pos;
			// Complex-form entry ref.
			if (lexDb.ComplexEntryTypesOA == null)
				lexDb.ComplexEntryTypesOA = Cache.ServiceLocator.GetInstance<ICmPossibilityListFactory>().Create();
			ILexEntryType complexType = Cache.ServiceLocator.GetInstance<ILexEntryTypeFactory>().Create();
			lexDb.ComplexEntryTypesOA.PossibilitiesOS.Add(complexType);
			ILexEntry compoundEntry = MakeStemEntry("kickball", "kickball");
			ILexEntryRef complexRef = Cache.ServiceLocator.GetInstance<ILexEntryRefFactory>().Create();
			compoundEntry.EntryRefsOS.Add(complexRef);
			complexRef.ComponentLexemesRS.Add(entry);
			complexRef.ComplexEntryTypesRS.Add(complexType);
			// Parser parameters.
			Cache.LangProject.MorphologicalDataOA.ParserParameters =
				"<ParserParameters><HC><Strata>Morphology,Phonology</Strata></HC></ParserParameters>";

			AssertValidatesAgainstSchema(GrammarJsonServices.ExportGrammar(Cache), "populated project");
		}

		private static void AssertValidatesAgainstSchema(string json, string description)
		{
			string schemaPath = Path.Combine(TestContext.CurrentContext.TestDirectory,
				"doc", "lcm-grammar.schema.json");
			var schema = NJsonSchema.JsonSchema.FromJsonAsync(File.ReadAllText(schemaPath))
				.GetAwaiter().GetResult();
			var errors = schema.Validate(json);
			Assert.IsEmpty(errors, description + " export should validate against doc/lcm-grammar.schema.json: " +
				string.Join("; ", errors.Select(e => e.ToString())));
		}

		/// <summary>
		/// A sense whose MSA belongs to a different entry (stray but real-world data) is carried
		/// as-is with a warning — msa references resolve document-wide, not per-entry.
		/// </summary>
		[Test]
		public void ExportGrammar_WarnsOnCrossEntrySenseMsa()
		{
			ILexEntry owner = MakeStemEntry("kick", "kick");
			IMoStemMsa foreignMsa = Cache.ServiceLocator.GetInstance<IMoStemMsaFactory>().Create();
			owner.MorphoSyntaxAnalysesOC.Add(foreignMsa);
			ILexEntry stray = MakeStemEntry("sing", "sing");
			stray.SensesOS[0].MorphoSyntaxAnalysisRA = foreignMsa;

			var warnings = new List<string>();
			JObject json = JObject.Parse(GrammarJsonServices.ExportGrammar(Cache, warnings));

			JObject strayEntry = (JObject)json["lexicon"]["entries"]
				.Single(e => (string)e["guid"] == stray.Guid.ToString());
			Assert.AreEqual(foreignMsa.Guid.ToString(), (string)strayEntry["senses"][0]["msa"],
				"the cross-entry reference must be carried as-is");
			Assert.IsTrue(warnings.Any(w => w.Contains(stray.SensesOS[0].Guid.ToString())),
				"the cross-entry msa must be reported in warnings");
		}

		/// <summary>
		/// Exported text is NFC even though LCM holds strings NFD in memory, so an independent
		/// reader of the raw .fwdata XML (which is NFC at rest) reproduces the same bytes.
		/// </summary>
		[Test]
		public void ExportGrammar_NormalizesTextToNfc()
		{
			ILexEntry entry = MakeStemEntry("kick", "kick");
			entry.SensesOS[0].Gloss.set_String(Cache.DefaultAnalWs,
				TsStringUtils.MakeString("bambu\u0301", Cache.DefaultAnalWs));

			string json = GrammarJsonServices.ExportGrammar(Cache);

			// Ordinal comparisons: culture-sensitive search treats NFC and NFD as equal.
			Assert.IsTrue(json.IndexOf("bamb\u00FA", StringComparison.Ordinal) >= 0,
				"output must be NFC");
			Assert.IsTrue(json.IndexOf("bambu\u0301", StringComparison.Ordinal) < 0,
				"output must not carry NFD sequences");
		}

		/// <summary>
		/// When every MSA on an entry is an unsupported class, the msas property is omitted
		/// entirely (never written as an empty array, which the schema forbids).
		/// </summary>
		[Test]
		public void ExportGrammar_OmitsMsasWhenAllUnsupported()
		{
			ILexEntry entry = MakeStemEntry("kick", "kick");
			IMoDerivStepMsa step = Cache.ServiceLocator.GetInstance<IMoDerivStepMsaFactory>().Create();
			entry.MorphoSyntaxAnalysesOC.Add(step);

			var warnings = new List<string>();
			JObject json = JObject.Parse(GrammarJsonServices.ExportGrammar(Cache, warnings));

			JObject jsonEntry = (JObject)json["lexicon"]["entries"][0];
			Assert.IsNull(jsonEntry["msas"], "unsupported-only msas must be omitted, not []");
			Assert.IsTrue(warnings.Any(w => w.Contains(step.Guid.ToString())),
				"the skipped MSA must be reported in warnings");
			AssertValidatesAgainstSchema(json.ToString(), "entry with only unsupported MSAs");
		}

		/// <summary>
		/// A copy-from-input mapping that references something other than a top-level part of its
		/// own process's input would emit a silently wrong positional index, so the whole
		/// allomorph is skipped.
		/// </summary>
		[Test]
		public void ExportGrammar_SkipsAffixProcessWithForeignInputReference()
		{
			ILexEntry entryA = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
			IMoAffixProcess processA = Cache.ServiceLocator.GetInstance<IMoAffixProcessFactory>().Create();
			entryA.LexemeFormOA = processA;
			processA.MorphTypeRA = Cache.ServiceLocator.GetInstance<IMoMorphTypeRepository>()
				.GetObject(MoMorphTypeTags.kguidMorphSuffix);
			ILexEntry entryB = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
			IMoAffixProcess processB = Cache.ServiceLocator.GetInstance<IMoAffixProcessFactory>().Create();
			entryB.LexemeFormOA = processB;
			processB.MorphTypeRA = Cache.ServiceLocator.GetInstance<IMoMorphTypeRepository>()
				.GetObject(MoMorphTypeTags.kguidMorphSuffix);
			IPhVariable foreignPart = Cache.ServiceLocator.GetInstance<IPhVariableFactory>().Create();
			processB.InputOS.Add(foreignPart);
			IMoCopyFromInput copy = Cache.ServiceLocator.GetInstance<IMoCopyFromInputFactory>().Create();
			processA.OutputOS.Add(copy);
			copy.ContentRA = foreignPart;

			var warnings = new List<string>();
			JObject json = JObject.Parse(GrammarJsonServices.ExportGrammar(Cache, warnings));

			JObject jsonEntryA = (JObject)json["lexicon"]["entries"]
				.Single(e => (string)e["guid"] == entryA.Guid.ToString());
			Assert.AreEqual(0, ((JArray)jsonEntryA["allomorphs"]).Count,
				"the allomorph with the foreign input reference must be skipped whole");
			Assert.IsTrue(warnings.Any(w => w.Contains(processA.Guid.ToString())),
				"the skipped process must be reported in warnings");
		}

		/// <summary>Unrepresentable data is skipped with a warning, never silently.</summary>
		[Test]
		public void ExportGrammar_WarnsAndSkipsAllomorphWithoutMorphType()
		{
			ILexEntry entry = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();
			IMoStemAllomorph lexemeForm = Cache.ServiceLocator.GetInstance<IMoStemAllomorphFactory>().Create();
			entry.LexemeFormOA = lexemeForm;
			lexemeForm.Form.SetVernacularDefaultWritingSystem("mystery");

			var warnings = new List<string>();
			JObject json = JObject.Parse(GrammarJsonServices.ExportGrammar(Cache, warnings));

			JObject jsonEntry = (JObject)json["lexicon"]["entries"][0];
			Assert.AreEqual("stem", (string)jsonEntry["lexemeMorphType"], "defaults to stem");
			Assert.AreEqual(0, ((JArray)jsonEntry["allomorphs"]).Count, "allomorph skipped but array present");
			Assert.IsTrue(warnings.Any(w => w.Contains(lexemeForm.Guid.ToString())),
				"skipped allomorph should be reported in warnings");
		}
	}
}
