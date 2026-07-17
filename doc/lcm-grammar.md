# LCM Grammar JSON (format `lcm-grammar`, version 1)

A deterministic, GUID-keyed JSON projection of the **parser-relevant subset** of a FieldWorks/LCM
project: phonology, morphology, and lexicon — everything a morphological parser needs, and (aside
from glosses, definitions, and names) nothing else. Produced by
`SIL.LCModel.DomainServices.GrammarJsonServices.ExportGrammar`; the machine-checkable structure is
[`lcm-grammar.schema.json`](lcm-grammar.schema.json) (validated against the exporter by
`GrammarJsonServicesTests`).

**What it is for:** grammar verification tooling ("does this change parse better?"), conformance
fixtures for morphological-parser test suites, and field deployment (a small artifact that
web/wasm parsers can load without LibLCM — as one measured data point, a real ~56 MB `.fwdata`
project exported to roughly 2.4 MB pretty-printed, ~250 KB gzipped).

**What it is not:** an editing format, a synchronization/merge format, or a replacement for
`.fwdata`. It is a read-only projection; LCM remains the authority. Consuming an LCM Grammar JSON
document never requires LibLCM or FieldWorks — that independence is the point.

## 1. Conventions

- **Envelope.** Every document is `{ "format": "lcm-grammar", "version": 1, ... }`. Consumers
  should reject any other format tag or major version.
- **Naming.** camelCase field names owned by this spec — not a mirror of LCM class/property names.
  The exporter source documents which LCM property each field originates from.
- **Cross-references.** Every reference to another object in the document is a FieldWorks GUID,
  rendered lowercase-hyphenated (`Guid.ToString()`'s default format) — never an `Hvo`
  (FieldWorks' in-session integer id, which is not durable).
- **Determinism.** Two exports of the same data are byte-identical, and an independent
  implementation reading the same project must be able to reproduce the same bytes:
  - Output is pretty-printed with two-space indentation. Keys within each object appear in a
    fixed, normative order: the order in which the schema's `properties` lists them.
  - LCM owning/reference *sequences* (`OS`/`RS` properties) keep model order — that order is
    semantically meaningful (rule order, slot order, allomorph disjunctive order, ...).
  - Unordered LCM *collections* (`OC`/`RC` properties, repository enumerations — notably lexical
    entries) are sorted by the ordinal comparison of their lowercase GUID string.
  - Multistring values are arrays of `{"ws": tag, "form": text}` sorted ordinally by
    writing-system tag; empty alternatives are skipped. (Phoneme/boundary-marker
    `representations` concatenate codes in model order, sorting per-code.) A `ws` tag **may
    repeat** within one array (a phoneme with two codes in the same writing system, for
    example) — model these fields as ordered lists of pairs, never as a map keyed by tag.
- **Optional vs. absent.** Absent optional values and empty arrays are omitted entirely, never
  written as `null` or `[]` — with one exception: a lexical entry's `allomorphs` array is always
  present, even when empty. An omitted field means "no data"; consumers must not distinguish
  omission from emptiness.
- **Tagged unions.** Polymorphic objects (natural classes, phonological rules, pattern contexts,
  compound rules, ad hoc rules, rule mappings, MSAs, entry refs) carry a `kind` discriminator.
- **Never-silent skips.** Data the format cannot represent (an unknown morph type, a dangling
  reference, malformed parser-parameter XML) is skipped by the exporter with a message in its
  optional warnings collection — the document itself contains only well-formed data.
- **Unicode normalization.** All exported text is NFC. (LCM holds strings NFD in memory; the raw
  `.fwdata` XML at rest is NFC — exporting NFC lets an independent reader of the raw XML
  reproduce the same bytes.)
- **Name fields.** Name-like multistrings export the best analysis alternative, falling back to
  the best vernacular alternative (phoneme names, for example, are usually authored only in a
  vernacular writing system). A field with no usable value is exported as `""` (FieldWorks'
  internal `"***"` missing-value marker is normalized to `""`).

## 2. Document structure

Top level: `format`, `version`, `project`, `featureSystems`, `phonology`, `morphology`,
`lexicon` — all always present. See the JSON Schema for every field's exact shape; the highlights
per section:

### `project`
`name`; `vernacularWritingSystems` / `analysisWritingSystems` (ICU tags, default writing system
first, then the remaining current writing systems in project order).

### `featureSystems`
`phonological` and `morphosyntactic` — FieldWorks' two independent feature systems, each with
`closedFeatures` (values enumerated as guid/name/abbreviation symbols) and `complexFeatures`.
Feature *structures* appear throughout the document as
`{"values":[{"feature": guid, "value": {"kind": "closed"|"complex", "value": ...}}]}`, recursive
through complex values. A structure's features resolve against whichever feature system its host
belongs to; the two are never mixed in one structure. A complex feature's `featureType` is an
**intentionally opaque** reference to FieldWorks' feature-structure-type object, which this
format does not project — it never resolves in-document; treat it as an opaque grouping key or
ignore it.

### `phonology`
`phonemes` (per-writing-system `representations` with FieldWorks' dotted-circle placeholder
U+25CC stripped; optional `features`, `basicIpaSymbol`), `boundaryMarkers` (excluding the built-in
word-boundary marker — see appendix), `naturalClasses` (`kind:"segments"` extensional /
`kind:"features"` intensional; `name` is the FieldWorks *abbreviation*, which is what environment
strings reference), `environments` (the raw FieldWorks environment string,
untokenized — these follow FieldWorks' phonological-environment syntax: `/` introduces the
context, `_` is the target slot, `#` a word boundary, `[...]` a natural-class *abbreviation*,
`(...)` optional material; e.g. `/[V+mid] ([preNas]) _ #`; this format carries the string
verbatim and consumers that evaluate environments own its tokenization), `rules` (rewrite and
metathesis, in `OrderNumber` order, disabled rules excluded), and `featureConstraints`
(alpha-variable slots).

Pattern positions use the recursive `PhonContext` union: `sequence`, `iteration` (`min` is
always ≥ 0; `max` = -1 means unbounded), `segment`, `naturalClass` (with `plusVariables`/`minusVariables` alpha-variable
agreement), `boundary`, `wordBoundary`, `variable`. Rewrite rules carry `direction`
(`leftToRight` | `rightToLeft` | `simultaneous`), a structural description, and one or more
right-hand sides (structural change, left/right context, required parts of speech,
required/excluded rule features). A rewrite rule's `featureConstraintVariables` lists its
alpha-variable feature constraints **in assignment order** — consumers assign variable names
(α, β, γ, ...) in exactly this order, so the order is semantic, not cosmetic. Metathesis rules carry 0-based `leftSwitchIndex` /
`rightSwitchIndex` into their structural description.

### `morphology`
`partsOfSpeech` (the full possibility tree, with per-POS inflection classes (recursive),
`defaultInflectionClass`, `inflectableFeatures`, stem names (feature-structure `regions`), affix
slots, and affix templates (`prefixSlots` innermost-to-outermost, `suffixSlots` in order,
`isFinal`, disabled templates included with their flag)); `compoundRules` (`endocentric` /
`exocentric`, disabled included); `adhocProhibitions` (allomorph- and morpheme-level, with
`adjacency`: `anywhere` | `somewhereToLeft` | `somewhereToRight` | `adjacentToLeft` |
`adjacentToRight`); `exceptionFeatures` (the merged registry of productivity restrictions and
possibility-typed phonological rule features); `lexEntryInflTypes` (irregularly-inflected-form
variant types, with `glossPrepend`/`glossAppend` and template `slots`); and `parserParameters`
(parsed from FieldWorks' stored XML block: `notOnClitics` **defaults to true when absent**,
`acceptUnspecifiedGraphemes` and `noDefaultCompounding` default to false, optional raw `strata`
string, optional per-compound-rule `compoundRuleMaxApplications`).

### `lexicon`
`entries`, GUID-sorted. Each entry: `citationForm` (optional — when absent, the conventional
headword is the lexeme form's `forms`, i.e. the **last** allomorph's), `lexemeMorphType` (the
lexeme form's morph type — see appendix for the closed enum), `allomorphs` (**alternate forms
first, lexeme form last** — this order carries allomorph-selection semantics), `msas`, `senses`,
`entryRefs`. An allomorph's `isAbstract` marks an underlying/abstract form rather than a surface
form; parsers conventionally exclude abstract allomorphs from surface matching.

- **Allomorphs** cover stem allomorphs (`environments`, `stemName`), affix allomorphs
  (`environments`, `positions`, `inflectionClasses`, `msEnvFeatures`, `msEnvPartOfSpeech`), and
  affix processes (`process` with `input` pattern parts and `output` mappings:
  `insertNaturalClass`, `copyFromInput`, `insertSegments`, `modifyFromInput`). `copyFromInput` /
  `modifyFromInput` reference input parts **positionally** (1-based index into `input`); an affix
  process with an unrepresentable input part is therefore skipped whole rather than emitted with
  misaligned indices. Forms may contain FieldWorks' lexical-pattern bracket notation (e.g.
  `[C][V]d`) verbatim; tokenizing it is the consumer's concern.
- **MSAs** are a tagged union: `stem`, `inflectional` (empty `slots` means the affix applies
  outside any template), `derivational` (from/to pairs), `unclassified`.
- **Senses** are the entry's sense tree flattened pre-order (parent before its subsenses), each
  with `gloss`, `definition`, and an `msa` reference. Resolve `msa` against the **document-wide
  union** of every entry's `msas`, not just the owning entry's: real projects contain stray
  senses whose MSA belongs to a different entry (the exporter emits a warning when it sees one,
  but carries the reference as-is).
- **Entry refs** are `variant` or `complexForm`: a ref is a `complexForm` only when it has
  complex-form types and no variant types; anything else (variant types only, both kinds, or
  neither) is a `variant`. `componentLexemes` may reference entries **or** senses.

## 3. Referential integrity — what the schema does not check

The JSON Schema validates structure and GUID *shape* only, never reference *existence*. Every
consumer must run its own resolution pass. GUIDs are globally unique across the whole document
(entries, senses, MSAs, allomorphs, phonemes, ... never collide), so build **one document-wide
guid → object index**; per-category indexes are insufficient for the starred rows below. Scope
of each reference field:

| Reference | Resolves against |
|---|---|
| `sense.msa` * | document-wide union of every entry's `msas` (usually, but not always, the owning entry's) |
| `entryRef.componentLexemes` * | union of all entry guids **and** all sense guids |
| `entryRef.variantEntryTypes` | `morphology.lexEntryInflTypes`, **or** a plain variant-type possibility this format does not enumerate — unresolvable guids here are normal |
| `entryRef.complexEntryTypes` | complex-form-type possibilities this format does not enumerate — opaque |
| `complexFeature.featureType` | **never resolvable in-document** (see §2) |
| feature-structure `feature` / closed `value` | the host's feature system's features / that feature's `values` |
| `naturalClass.phonemes`, `PhonContext.segment.phoneme` | `phonology.phonemes` |
| `PhonContext.boundary.marker` | `phonology.boundaryMarkers` |
| `PhonContext.naturalClass`, rule-mapping `naturalClass` | `phonology.naturalClasses` |
| `plusVariables`/`minusVariables`, `featureConstraintVariables` | `phonology.featureConstraints` |
| allomorph `environments`/`positions` | `phonology.environments` |
| allomorph/MSA `stemName`, `inflectionClasses`, `partOfSpeech`, `slots` | the `morphology.partsOfSpeech` tree's stem names / inflection classes / own guids / affix slots |
| MSA/compound/rewrite-RHS `exceptionFeatures`/rule features | `morphology.exceptionFeatures` **or** the inflection-class hierarchy (both are valid targets) |
| ad hoc `primary`/`others` | all allomorph guids (allomorph kind) or all MSA guids (morpheme kind) |
| `parserParameters.compoundRuleMaxApplications[].compoundRule` | `morphology.compoundRules` |
| `copyFromInput`/`modifyFromInput` `part` | 1-based position in the **same process's** `input` array (positional, not a guid; always in range in exporter output) |

Dangling references beyond those noted as expected indicate source-data problems; the exporter
emits a warning for every reference it knows to be stray but still carries representable data.

## 4. Versioning and evolution

- The schema in this directory validates exactly what the current exporter emits; exporter and
  schema change together, in the same commit.
- Within major version 1, changes are **additive only**: new optional fields may appear; existing
  fields never change meaning, type, or optionality. Consumers should ignore fields they do not
  recognize.
- Anything that would break a faithful consumer requires a major-version bump of the `version`
  field.

Planned additive extensions (not yet present): writing-system definitions (so a document can be
imported with no source project behind it) and optional export filters with an `omits` envelope
marker. A cross-implementation byte-equality gate against an independent `.fwdata` reader is
planned to keep this spec honest.

## 5. Appendix: built-in FieldWorks GUIDs

These well-known objects are referenced by meaning rather than enumerated in the document. They
are constant across all FieldWorks projects.

**Word boundary** (`PhBdryMarker`, excluded from `boundaryMarkers`; pattern contexts referencing
it are exported as `kind:"wordBoundary"`): `7db635e0-9ef3-4167-a594-12551ed89aaa`.

**Morph types** (`MoMorphType` possibilities → the `morphType`/`lexemeMorphType` enum):

| Enum value | GUID |
|---|---|
| `stem` | `d7f713e8-e8cf-11d3-9764-00c04f186933` |
| `boundStem` | `d7f713e7-e8cf-11d3-9764-00c04f186933` |
| `root` | `d7f713e5-e8cf-11d3-9764-00c04f186933` |
| `boundRoot` | `d7f713e4-e8cf-11d3-9764-00c04f186933` |
| `prefix` | `d7f713db-e8cf-11d3-9764-00c04f186933` |
| `suffix` | `d7f713dd-e8cf-11d3-9764-00c04f186933` |
| `infix` | `d7f713da-e8cf-11d3-9764-00c04f186933` |
| `circumfix` | `d7f713df-e8cf-11d3-9764-00c04f186933` |
| `proclitic` | `d7f713e2-e8cf-11d3-9764-00c04f186933` |
| `enclitic` | `d7f713e1-e8cf-11d3-9764-00c04f186933` |
| `clitic` | `c2d140e5-7ca9-41f4-a69a-22fc7049dd2c` |
| `particle` | `56db04bf-3d58-44cc-b292-4c8aa68538f4` |
| `phrase` | `a23b6faa-1052-4f4d-984b-4b338bdaf95f` |
| `discontigPhrase` | `0cc8c35a-cee9-434d-be58-5d29130fba5b` |
| `prefixingInterfix` | `af6537b0-7175-4387-ba6a-36547d37fb13` |
| `infixingInterfix` | `18d9b1c3-b5b6-4c07-b92c-2fe1d2281bd4` |
| `suffixingInterfix` | `3433683d-08a9-4bae-ae53-2a7798f64068` |

FieldWorks' *simulfix* and *suprafix* morph types, `MoDerivStepMsa` analyses, and coordinate
compound rules (`MoCoordinateCompound`) have no representation in this format; each is skipped
with a warning.
