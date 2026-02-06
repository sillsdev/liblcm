# AGENTS: liblcm (LCM)

## Summary
liblcm (LCM) is the core FieldWorks Language & Culture Model library for linguistic analyses. It provides the data model, serialization, utilities, and tooling for linguistic, anthropological, and text corpus data. It is a multi-project .NET solution with code generation steps and multi-targeting for legacy .NET Framework and modern .NET.

## High-level repo facts
- Type: .NET solution (multi-project class libraries + build tasks + tools + tests).
- Languages: C# (.cs), MSBuild (.proj/.csproj/.props/.targets), XML, shell/batch scripts.
- Target frameworks: net462, netstandard2.0, net8.0 (see .csproj files in src/ and tests/).
- Build tools: MSBuild, dotnet SDK, GitVersion.MsBuild, NUnit.
- Output: artifacts/ (NuGet packages and binaries by configuration/TFM).

## Build and validation (validated commands and observations)

### What CI runs (GitHub Actions)
CI runs on Windows and Ubuntu. See .github/workflows/ci-cd.yml:
1) Install .NET SDK 8.x.
2) Ubuntu: install mono-devel and icu-fw packages.
3) Windows: remove c:\tools\php\icuuc*.dll; install .NET Framework 4.6.1 targeting pack.
4) Build: dotnet build --configuration Release
5) Test:
   - Linux: . environ && dotnet test --no-restore --no-build -p:ParallelizeAssembly=false --configuration Release
   - Windows: dotnet test --no-restore --no-build -p:ParallelizeAssembly=false --configuration Release
6) Pack: dotnet pack --include-symbols --no-restore --no-build -p:SymbolPackageFormat=snupkg --configuration Release

Always mirror this sequence when validating a change locally.

### Local build scripts (not validated here)
- Windows: build.cmd [Debug|Release] [Target] (uses MSBuild on LCM.sln).
- Linux: build.sh [Debug|Release] [Target] (sources environ, uses msbuild on LCM.sln).
These scripts call build/LCM.proj targets (Build/Test/Pack). If you use them, always run from repo root.

### Tests per README (not validated here)
- Windows, ReSharper: open LCM.sln and “Run Unit Tests”.
- Windows, no ReSharper: use MSBuild, then run nunit3-console.exe from artifacts/Debug/net462.
- Linux terminal: source environ, then run mono with nunit3-console.exe on *Tests.dll in artifacts/Debug/net462.

### Commands actually run during onboarding
- dotnet test .\LCM.sln → FAILED
- dotnet build --configuration Release → FAILED
Failure signature (both commands): GitVersion.MsBuild (netcoreapp3.1 gitversion.dll) exited with code 1. This blocks build/test in this environment. CI uses fetch-depth 0, so ensure a full git history is available. If GitVersion still fails, check GitVersion prerequisites and local .NET runtime compatibility.

No command timeouts were observed.

### Known prerequisites and gotchas
- GitVersion.MsBuild is used across projects; it requires git metadata. CI checks out with fetch-depth 0.
- net462 builds on Windows require the .NET Framework 4.6.1 targeting pack (CI installs it).
- ICU data generation requires ICU binaries (CI installs icu-fw on Ubuntu).
- Some projects warn on NU1701; treat as warnings unless build breaks.
- The build prohibits references to System.Windows.Forms (CheckWinForms target).

## Project layout and architecture

### Key solution and build files
- LCM.sln: solution entry point.
- build.cmd / build.sh: wrapper scripts for MSBuild.
- build/LCM.proj: orchestrated build/test/pack, uses NUnit console on output/ for legacy builds.
- Directory.Build.props / Directory.Build.targets: repo-wide build settings and packaging.
- Directory.Solution.props / Directory.Solution.targets: solution-level defaults.
- GitVersion.yml: GitVersion configuration.
- global.json: SDK roll-forward config.
- .editorconfig: formatting rules.

### Major source projects (src/)
- src/SIL.LCModel: main LCM library (net462; netstandard2.0).
- src/SIL.LCModel.Core: core utilities and ICU data generation (netstandard2.0; net462; net8.0).
- src/SIL.LCModel.Utils: shared utilities (net462; netstandard2.0).
- src/SIL.LCModel.Build.Tasks: MSBuild tasks used for code generation.
- src/SIL.LCModel.FixData: data-fix utilities.
- src/CSTools: auxiliary tools (pg/lg/Tools).

Code generation targets to know about:
- SIL.LCModel: GenerateModel (MasterLCModel.xml → Generated*.cs).
- SIL.LCModel.Core: GenerateKernelCs, GenerateIcuData.

### Tests (tests/)
- SIL.LCModel.Tests
- SIL.LCModel.Core.Tests
- SIL.LCModel.Utils.Tests
- SIL.LCModel.FixData.Tests
- TestHelper (support project)

### CI/validation checks
- GitHub Actions: .github/workflows/ci-cd.yml (build, test, pack, publish).
- Tests run with dotnet test and ParallelizeAssembly=false.
- Packaging uses dotnet pack with symbol packages.

### Dependencies not obvious from layout
- ICU data and binaries (icu-fw) for Core ICU generation.
- Mono on Linux for some runtime/test workflows.
- GitVersion.MsBuild for versioning (requires git metadata).

## Root files list
- .editorconfig
- .gitattributes
- .gitignore
- build.cmd
- build.sh
- CHANGELOG.md
- Directory.Build.props
- Directory.Build.targets
- Directory.Solution.props
- Directory.Solution.targets
- environ
- GitVersion.yml
- global.json
- LCM.sln
- LCM.sln.DotSettings
- LICENSE
- README.md

## Repo top-level directories
- .github/ (GitHub Actions workflow)
- .vscode/ (VS settings)
- artifacts/ (build outputs)
- build/ (LCM.proj)
- src/ (production code)
- tests/ (unit tests)

## README highlights (summary)
- Describes liblcm as FieldWorks model library for linguistic analyses.
- Build: use build.cmd (Windows) or build.sh (Linux). Default Debug, optional Release.
- Debugging: use LOCAL_NUGET_REPO to publish local packages; see NuGet local feeds.
- Tests: Windows via ReSharper or NUnit console; Linux via mono + NUnit console (requires environ).

## Trust these instructions
Follow this file first. Only search the repo if these instructions are incomplete or prove incorrect for your task.
