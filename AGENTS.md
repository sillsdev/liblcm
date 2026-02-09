# AGENTS: liblcm (LCM)

## Summary

liblcm is the core FieldWorks Language & Culture Model library. It provides the object-oriented data model, serialization, persistence, and domain services for linguistic, anthropological, and text corpus data used by [FieldWorks](https://github.com/sillsdev/FieldWorks).

The codebase is heavily code-generated from `MasterLCModel.xml`. Understanding the generation pipeline and the rules below is essential before making changes.

## Critical Rules

**Violating any of these will break the build or corrupt data.**

1. **NEVER edit `Generated*.cs` files.** These 9 files are produced by the code generation pipeline from `MasterLCModel.xml` via NVelocity templates. Edit the XML model or `.vm.cs` templates instead. The generated files are:
   - `GeneratedConstants.cs`, `GeneratedInterfaces.cs`, `GeneratedFactoryInterfaces.cs`, `GeneratedRepositoryInterfaces.cs`
   - `DomainImpl/GeneratedClasses.cs`, `DomainImpl/GeneratedFactoryImplementations.cs`
   - `Infrastructure/Impl/GeneratedRepositoryImplementations.cs`, `Infrastructure/Impl/GeneratedBackendProvider.cs`
   - `IOC/GeneratedServiceLocatorBootstrapper.cs`

2. **Model changes require a version bump and migration.** Almost every change to `MasterLCModel.xml` requires incrementing the `version` attribute and writing a data migration class. The ONLY exceptions are: editing `<comment>`/`<notes>` elements, editing XML comments, or adding attributes that only affect the code generator. Read the warnings at the top of `MasterLCModel.xml` carefully.

3. **All data changes must occur within a UnitOfWork.** Use `UndoableUnitOfWorkHelper` for user actions or `NonUndoableUnitOfWorkHelper` for system operations. Changes outside a UOW will throw or silently fail.

4. **No references to `System.Windows.Forms`.** The build enforces this via the `CheckWinForms` target.

5. **Model version bumps require a matching migration registration.** New migrations must be registered in `LcmDataMigrationManager`'s constructor dictionary. Even no-op version bumps need a `DoNothingDataMigration` entry.

## Build and Validation

### Prerequisites
- .NET SDK 8.x
- Windows: .NET Framework 4.6.1 targeting pack
- Linux: mono-devel, icu-fw packages
- Full git history (GitVersion.MsBuild requires `git fetch --unshallow` or `fetch-depth: 0`)

### CI Commands (GitHub Actions, `.github/workflows/ci-cd.yml`)
```
dotnet build --configuration Release
dotnet test --no-restore --no-build -p:ParallelizeAssembly=false --configuration Release
dotnet pack --include-symbols --no-restore --no-build -p:SymbolPackageFormat=snupkg --configuration Release
```
On Linux, prefix test/build with `. environ &&`.

### Local Build
- Windows: `build.cmd [Debug|Release]` (from repo root)
- Linux: `build.sh [Debug|Release]` (from repo root)

### Known Issues
- GitVersion.MsBuild requires full git metadata. Shallow clones will fail.
- `NU1701` warnings are expected; treat as warnings unless the build breaks.

## Architecture Overview

### Code Generation Pipeline

`MasterLCModel.xml` is the single source of truth for the data model. The `GenerateModel` MSBuild target (in `SIL.LCModel.csproj`) invokes `LcmGenerate` from `SIL.LCModel.Build.Tasks`, which:

1. Parses `MasterLCModel.xml` into a `Model` object hierarchy (`CellarModule` > `Class` > `Property`)
2. Loads `LcmGenerate/HandGenerated.xml` (properties to skip generation for) and `LcmGenerate/IntPropTypeOverrides.xml`
3. Processes NVelocity templates (`LcmGenerate/*.vm.cs`) to produce the 9 generated C# files

The generated code provides: class ID/field ID constants, interfaces, concrete implementations, factory interfaces and implementations, repository interfaces and implementations, the backend provider's `ModelVersion` constant, and StructureMap DI bootstrapping.

### MasterLCModel.xml Schema

The model is organized into `CellarModule` elements containing `class` elements. Each class has:
- `id`: Class name (e.g., `LexEntry`)
- `num`: Class number within its module (combined with module number to form the class ID)
- `base`: Parent class (inheritance). All classes inherit from `CmObject`
- `abstract`: Whether the class can be instantiated
- `owner`: `required` (default), `optional`, or `none`
- `singleton`: Whether only one instance exists (e.g., `LangProject`)

Properties come in three types:
- `<basic>`: Value types. `sig` is the type: `Integer`, `Boolean`, `String`, `Unicode`, `MultiString`, `MultiUnicode`, `Time`, `GenDate`, `Binary`, `Guid`, `TextPropBinary`
- `<owning>`: Ownership references. `card` is `atomic`, `seq`, or `col`. `sig` is the target class
- `<rel>`: Non-owning references. Same attributes as `<owning>`

Field IDs (flids) are formed as: module-number + class-number + field-number (e.g., `5016005` = Ling module `5` + LexSense class `016` + Definition field `005`).

Key string type distinction: `Unicode`/`MultiUnicode` are plain character sequences with no formatting. `String`/`MultiString` support embedded runs with writing systems, styles, and other attributes.

### Partial Class Pattern

Generated classes are `partial`. Hand-written code extends them in `DomainImpl/Overrides*.cs` files:
- `OverridesLing_Lex.cs` -- Lexical domain (LexDb, LexEntry, LexSense, etc.)
- `OverridesCellar.cs` -- Core classes (CmObject, CmPossibility, StText, etc.)
- `OverridesLing_Wfi.cs` -- Wordform analysis
- `OverridesLing_MoClasses.cs` -- Morphological classes
- `OverridesLangProj.cs` -- Language project
- `OverridesLing_Disc.cs` -- Discourse charting
- `OverridesNotebk.cs` -- Notebook

These files add virtual properties (`[VirtualProperty]` attribute), convenience methods, business logic, and side-effect handlers. Virtual properties are discovered automatically via reflection -- no XML or registration needed.

### Persistence and Infrastructure

**LcmCache** (`LcmCache.cs`) is the entry point for all data access. Despite its name, it is a service locator facade, not a cache. Key accessors: `ServiceLocator`, `LanguageProject`, `DomainDataByFlid`, `ActionHandlerAccessor`.

**Backend Providers** (all in `Infrastructure/Impl/`):
- `XMLBackendProvider` -- File-based XML storage (the `.fwdata` format)
- `MemoryOnlyBackendProvider` -- In-memory only, used in tests
- `SharedXMLBackendProvider` -- Multi-process shared access via memory-mapped files

**Surrogate/IdentityMap Pattern**: Objects are loaded lazily. The backend reads XML into `CmObjectSurrogate` placeholders. On first access to `.Object`, the surrogate parses XML and creates the real `CmObject`. The `IdentityMap` ensures one instance per Guid/Hvo. Bulk loading by domain (Lexicon, Scripture, Text, WFI) is available via `BackendProvider.LoadDomain()`.

**IOC**: StructureMap via `LcmServiceLocatorFactory`. Factories, repositories, and infrastructure services are registered as singletons. Generated code handles factory/repository registration in `GeneratedServiceLocatorBootstrapper.cs`.

### Data Migration System

Migrations live in `DomainServices/DataMigration/`. Each migration:
- Implements `IDataMigration` with a single `PerformMigration(IDomainObjectDTORepository)` method
- Operates on `DomainObjectDTO` objects (raw XML, no live CmObjects)
- Uses `XElement.Parse()` for XML manipulation
- Checks starting version, performs changes, increments version
- Is registered in `LcmDataMigrationManager`'s constructor

Current model version: **7000072**. Next migration would be `DataMigration7000073.cs`.

The `DomainObjectDTORepository` tracks changes in three sets: **newbies** (created), **dirtballs** (modified), **goners** (deleted).

### Key Domain Classes

Ownership hierarchy (simplified):
```
LangProject (singleton, owner=none)
  +-- LexDb (atomic)
  |     +-- [Entries accessed via virtual property, LexEntry has owner=none]
  |           +-- LexSense (seq)
  |           |     +-- LexExampleSentence (seq)
  |           +-- MoForm / MoStemAllomorph / MoAffixAllomorph (atomic: LexemeForm, seq: AlternateForms)
  |           +-- MoMorphSynAnalysis (col: MorphoSyntaxAnalyses)
  +-- PartsOfSpeech (CmPossibilityList, atomic)
  +-- SemanticDomainList (CmPossibilityList, atomic)
  +-- ResearchNotebook (RnResearchNbk, atomic)
  +-- TranslatedScripture (Scripture, atomic)
  +-- Styles (StStyle, col)
```

`CmPossibility` / `CmPossibilityList` are the list/list-item pattern used extensively for categories, types, domains, and other enumerated values.

Writing systems: Projects have vernacular (the language being studied) and analysis (languages used for descriptions, typically English/French/Spanish) writing systems. `MultiUnicode` and `MultiString` properties store alternatives keyed by writing system.

## Project Layout

```
src/
  SIL.LCModel/                  Main library (net462; netstandard2.0)
    MasterLCModel.xml            Model source of truth
    MasterLCModel.xsd            XML schema for the model
    LcmGenerate/                 NVelocity templates + HandGenerated.xml
    DomainImpl/                  Generated + hand-written class implementations
    DomainServices/              Business logic and domain services
      DataMigration/             Migration classes (DataMigration7000001..7000072)
    Infrastructure/Impl/         Backend providers, UnitOfWork, IdentityMap
    IOC/                         StructureMap DI setup
  SIL.LCModel.Core/             Core utilities, Cellar types, ICU, writing systems (netstandard2.0; net462; net8.0)
  SIL.LCModel.Utils/            Shared utilities (net462; netstandard2.0)
  SIL.LCModel.Build.Tasks/      MSBuild tasks for code generation
  SIL.LCModel.FixData/          Data-fix utilities
  CSTools/                       Auxiliary tools (pg/lg)
tests/
  SIL.LCModel.Tests/            Main library tests
  SIL.LCModel.Core.Tests/       Core tests
  SIL.LCModel.Utils.Tests/      Utility tests
  SIL.LCModel.FixData.Tests/    FixData tests
  TestHelper/                    Test support project
```

## Common Tasks

When performing any of the following tasks, read the linked guide first:

- **Adding a property to an existing class**: [docs/agents/adding-a-property.md](docs/agents/adding-a-property.md)
- **Writing a data migration**: [docs/agents/writing-a-data-migration.md](docs/agents/writing-a-data-migration.md)
- **Adding a new class to the model**: [docs/agents/adding-a-new-class.md](docs/agents/adding-a-new-class.md)
- **Adding a virtual property** (computed, no model change): [docs/agents/adding-a-virtual-property.md](docs/agents/adding-a-virtual-property.md)
- **Writing tests**: [docs/agents/writing-tests.md](docs/agents/writing-tests.md)

## Trust These Instructions

Follow this file first. Only search the repo if these instructions are incomplete or prove incorrect for your task.
