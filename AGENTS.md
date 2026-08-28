# AGENTS: liblcm (LCM)

## Summary

liblcm is the core FieldWorks Language and Culture Model library. It provides the
object-oriented data model, serialization, persistence, and domain services for linguistic,
anthropological, and text corpus data used by
[FieldWorks](https://github.com/sillsdev/FieldWorks).

The codebase is heavily code-generated from `MasterLCModel.xml`. Understand the generation
pipeline and the rules below before changing anything.

## Critical rules

Rules 2, 3 and 5 corrupt data when broken. Rule 4 breaks the build. Rule 1 does neither,
which is what makes it the most dangerous of the five.

1. **Never edit `Generated*.cs`.** Nine files are produced from `MasterLCModel.xml` via the
   NVelocity templates in `LcmGenerate/*.vm.cs`:

   - `GeneratedConstants.cs`, `GeneratedInterfaces.cs`, `GeneratedFactoryInterfaces.cs`,
     `GeneratedRepositoryInterfaces.cs`
   - `DomainImpl/GeneratedClasses.cs`, `DomainImpl/GeneratedFactoryImplementations.cs`
   - `Infrastructure/Impl/GeneratedRepositoryImplementations.cs`,
     `Infrastructure/Impl/GeneratedBackendProvider.cs`
   - `IOC/GeneratedServiceLocatorBootstrapper.cs`

   An edit there does not fail loudly. Regeneration is skipped while the file is newer than
   `MasterLCModel.xml`, so the edit compiles into your local build and your tests pass,
   while CI generates from the XML into a fresh tree and builds different code. The files
   are gitignored, so the edit can never be committed, and any `MasterLCModel.xml` change
   or `dotnet clean` destroys it. Edit the XML or the templates instead.

   A fresh clone or worktree has none of these files until the first build. After switching
   branches, delete them: a generated file left over from another branch is newer than the
   XML, so it will be compiled as it stands.

2. **Model changes require a version bump and migration.** Almost every change to
   `MasterLCModel.xml` requires incrementing the `version` attribute and writing a data
   migration class. The only exceptions are editing `<comment>` or `<notes>` elements,
   editing XML comments, and adding attributes that only affect the code generator. Read the
   `WARNING` block at the top of `MasterLCModel.xml`; it is authoritative, and it also
   requires a matching update to the FLEx Bridge metadata cache.

3. **All data changes must occur within a UnitOfWork.** Use `UndoableUnitOfWorkHelper` for
   user actions or `NonUndoableUnitOfWorkHelper` for system operations. Changes outside a
   UOW throw or silently fail.

4. **No references to `System.Windows.Forms`.** Enforced by the `CheckWinForms` target in
   `SIL.LCModel`, `SIL.LCModel.Core` and `SIL.LCModel.Utils`.

5. **Model version bumps require a matching migration registration.** New migrations must be
   registered in the `LcmDataMigrationManager` constructor dictionary. Even a no-op version
   bump needs an entry, for which `m_bumpNumberOnlyMigration` exists.

## Proving a change

Use `dotnet build -m:1` for a cold-start build (a tree with no generated sources yet).
Parallel builds race on the generated sources and fail with `LcmGenerate` or `IdlImp`
errors. Plain `dotnet build` is fine once those sources exist.

```
dotnet build -m:1 --configuration Release
dotnet test tests/<Project>/<Project>.csproj --configuration Release --no-restore --no-build -p:ParallelizeAssembly=false
```

`--no-restore --no-build` require a completed build in the same configuration. Scope tests to
one project while iterating. `-p:ParallelizeAssembly=false` is not optional: ICU and the
writing system subsystems hold shared state.

Windows builds need the Visual Studio C++ tools whether or not you use the IDE, because code
generation preprocesses the IDL with `cl.exe`, located via `vswhere`.

ICU needs no manual environment setup. Test assemblies declare
`[assembly: InitializeIcu(IcuDataPath = "IcuData")]`, which resolves against the build output.

A root build compiles every project, and any stray `.cs` file inside a project directory
joins that compilation. Run `git status --porcelain` first: files left over from another
branch produce compile errors that look like your change broke something.

The SDK floor, the target frameworks and the exact CI sequence are defined in `global.json`,
the `.csproj` files and `.github/workflows/ci-cd.yml`. Read those rather than a transcription
here.

### Worktrees

```
git worktree add -b <branch> .claude/worktrees/<name> origin/master
```

Always a named branch. GitVersion cannot version a detached HEAD, and the build then fails
with `MSB3073` errors whose real cause appears only in a `WARN` line above them.

To remove one, leave the directory first, then force it, since build output is untracked:

```
dotnet build-server shutdown
git worktree remove --force .claude/worktrees/<name>
```

## Architecture

### Code generation

`MasterLCModel.xml` is the single source of truth. The `GenerateModel` target runs
`LcmGenerate` from `SIL.LCModel.Build.Tasks`, which parses the XML and uses the NVelocity
templates in `LcmGenerate/*.vm.cs` to produce the nine generated C# files.

`SIL.LCModel.Core` has a second generator: `GenerateKernelCs` runs the `IdlImp` task from the
same build-tasks assembly over `KernelInterfaces/*.idh` to produce `Kernel.cs`.

### MasterLCModel.xml schema

The model is organized into `CellarModule` elements containing `class` elements. Read the
module ids and numbers off the file rather than memorizing them. Each class carries:

- `id`: class name, e.g. `LexEntry`
- `num`: class number within its module
- `base`: parent class; all classes descend from `CmObject`
- `abstract`, `depth`, `abbr`
- `owner`: `required` (default), `optional`, or `none`
- `singleton`: whether only one instance exists, e.g. `LangProject`

Properties come in three kinds:

- `<basic>`: value types. `sig` is `Integer`, `Boolean`, `String`, `Unicode`, `MultiString`,
  `MultiUnicode`, `Time`, `GenDate`, `Binary`, `Guid` or `TextPropBinary`
- `<owning>`: ownership. `card` is `atomic`, `seq` or `col`; `sig` is the target class
- `<rel>`: non-owning references, same attributes as `<owning>`

Field ids (flids) are the module number, then the class number to three digits, then the
field number to three digits. `LexSenseTags.kflidDefinition` is `5016005`: Ling module 5,
`LexSense` class 16, `Definition` field 5.

`Unicode` and `MultiUnicode` are plain character sequences with no formatting. `String` and
`MultiString` carry embedded runs with writing systems, styles and other properties.

### Partial class pattern

Generated classes are `partial`. Hand-written code extends them in `DomainImpl/Overrides*.cs`,
split by domain: `OverridesLing_Lex.cs`, `OverridesCellar.cs`, `OverridesLing_Wfi.cs`,
`OverridesLing_MoClasses.cs`, `OverridesLangProj.cs`, `OverridesLing_Disc.cs`,
`OverridesNotebk.cs`.

These add virtual properties (`[VirtualProperty]`), convenience methods, business logic and
side-effect handlers. Virtual properties are discovered by reflection, so they need no XML and
no registration. Partial interface extensions live in `InterfaceAdditions.cs`.

### Persistence and infrastructure

**LcmCache** (`LcmCache.cs`) is the entry point for all data access. Despite the name it is a
service locator facade, not a cache. Key accessors: `ServiceLocator`, `LanguageProject`,
`DomainDataByFlid`, `ActionHandlerAccessor`.

**Backend providers**, all in `Infrastructure/Impl/`:

- `XMLBackendProvider` -- file-based XML storage, the `.fwdata` format
- `MemoryOnlyBackendProvider` -- in-memory, used by tests
- `SharedXMLBackendProvider` -- multi-process shared access via memory-mapped files

**Surrogate and IdentityMap.** Objects load lazily. The backend reads XML into
`CmObjectSurrogate` placeholders; on first access to `.Object` the surrogate parses the XML and
creates the real `CmObject`. `IdentityMap` guarantees one instance per Guid and Hvo. Bulk
loading by domain is available through `BackendProvider.LoadDomain()`.

**Dependency injection.** `LcmServiceLocatorFactory` builds a
`Microsoft.Extensions.DependencyInjection` container and wraps it in `MicrosoftServiceLocator`,
which derives from `ServiceLocatorImplBase` so `GetInstance<T>()` keeps working. Each type is registered as a singleton by its concrete
type, with the interface registered as an alias resolving to the same instance. Generated code
supplies the factory and repository registrations in `GeneratedServiceLocatorBootstrapper.cs`.

### Data migration

Migrations live in `DomainServices/DataMigration/` and are registered in
`LcmDataMigrationManager`. They operate on raw XML through `DomainObjectDTO`; no live
`ICmObject` is available. See the `writing-a-data-migration` skill for structure and repository
behaviour.

### Key domain classes

Simplified ownership hierarchy:

```
LangProject (singleton, owner=none)
  +-- LexDb (atomic)
  |     +-- [Entries reached through a virtual property; LexEntry has owner=none]
  |           +-- LexSense (seq)
  |           |     +-- LexExampleSentence (seq)
  |           +-- MoForm / MoStemAllomorph / MoAffixAllomorph
  |           +-- MoMorphSynAnalysis (col: MorphoSyntaxAnalyses)
  +-- PartsOfSpeech (CmPossibilityList, atomic)
  +-- SemanticDomainList (CmPossibilityList, atomic)
  +-- ResearchNotebook (RnResearchNbk, atomic)
  +-- TranslatedScripture (Scripture, atomic)
  +-- Styles (StStyle, col)
```

`CmPossibility` and `CmPossibilityList` are the list and list-item pattern used throughout for
categories, types, domains and other enumerated values.

Projects have vernacular writing systems (the language being studied) and analysis writing
systems (languages used for descriptions). `MultiUnicode` and `MultiString` properties store
alternatives keyed by writing system.

## Project layout

```
src/
  SIL.LCModel/                  Main library
    MasterLCModel.xml            Model source of truth
    MasterLCModel.xsd            Schema for the model
    LcmGenerate/                 NVelocity templates + HandGenerated.xml
    DomainImpl/                  Generated and hand-written class implementations
    DomainServices/              Business logic and domain services
      DataMigration/             Migration classes and the migration manager
    Infrastructure/Impl/         Backend providers, UnitOfWork, IdentityMap
    IOC/                         Dependency injection setup
  SIL.LCModel.Core/             Cellar types, ICU, writing systems, Kernel interfaces
  SIL.LCModel.Utils/            Shared utilities
  SIL.LCModel.Build.Tasks/      MSBuild tasks: LcmGenerate and IdlImp
  SIL.LCModel.FixData/          Data-fix utilities
  CSTools/                       Auxiliary tools (pg/lg)
tests/
  SIL.LCModel.Tests/            Main library tests
  SIL.LCModel.Core.Tests/       Core tests
  SIL.LCModel.Utils.Tests/      Utility tests
  SIL.LCModel.FixData.Tests/    FixData tests
  TestHelper/                    Test support project
```

## Common tasks

Step-by-step guides live in `.claude/skills/`. Read the SKILL.md directly if your agent does
not load them automatically.

- Adding a property to an existing class -- `.claude/skills/adding-a-property/SKILL.md`
- Adding a new class to the model -- `.claude/skills/adding-a-new-class/SKILL.md`
- Adding a virtual property, computed and not persisted --
  `.claude/skills/adding-a-virtual-property/SKILL.md`
- Writing a data migration -- `.claude/skills/writing-a-data-migration/SKILL.md`
- Writing tests -- `.claude/skills/writing-tests/SKILL.md`

## Trust these instructions

Follow this file first. Only search the repo if these instructions are incomplete or prove
incorrect for your task. If these instructions fail notify the author of the task that they
should verify and update the instructions if necessary.
