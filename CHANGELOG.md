# Change Log

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/)
and this project adheres to [Semantic Versioning](http://semver.org/).

<!-- Available types of changes:
### Added
### Changed
### Fixed
### Deprecated
### Removed
### Security
-->

## [Unreleased]

### Added

- [SIL.LCModel] Add new virtual property LicenseTSS in CmPicture, to access info about the picture's copyright and license.
- [SIL.LCModel] Add new virtual property CreatorTSS in CmPicture, to access info about the picture's creator.
- [SIL.LCModel] Add SpecificItemAndFieldName() to ISenseOrEntry
- [SIL.LCModel] Added a parameter to GetBestGuess() and TryGetBestGuess() to do lowercase matching regardless of the occurrence index
- [SIL.LCModel] Add GetCaptionOrHeadword() to CmPicture
- [SIL.LCModel] `LCModelStrings.NotSure` to allow clients to know if a grammatical category is the placeholder
- [SIL.LCModel.Utils] `DateTime` extension method `ToLCMTimeFormatWithMillisString()` (replaces `ReadWriteServices.FormatDateTime`)

### Fixed

- [SIL.LCModel] Data migration now serializes dates using the culture-neutral `ToLCMTimeFormatWithMillisString` (LT-20698)
- [SIL.LCModel] `ReadWriteServices.LoadDateTime` now parses milliseconds correctly (LT-18205)
- [SIL.LCModel.Core] Copy `SIL.LCModel.Core.dll.config` to output directory
- [SIL.LCModel] Use `CaseFunctions` (to use the `WritingSystemDefinition.CaseAlias`, if any)
- [SIL.LCModel.Core] Use the `WritingSystemDefinition.CaseAlias`, if any, in `CaseFunctions`

### Changed

- [SIL.LCModel] `FileUtils.IsFileUriOrPath` checks for the presence of "file:" rather than the absence of known non-file URI schemes
- Changed to target .Net Framework 4.6.2 instead of 4.6.1
- Update libPalaso dependency from version 14.2.0-* to 17.0.0-*
- Update Newtonsoft.Json dependency from version 13.0.2 to 13.0.3
- Update icu.net dependency from version 3.0.0-* to 3.0.1

### Deprecated

- [SIL.LCModel] `SyncMsg` seems to be a relic of TE, which was discontinued years ago
- [SIL.LCModel.Core] `new CaseFunctions(string)` in favor of the new `new CaseFunctions(CoreWritingSystemDefinition)`

### Removed

- Support for .Net Framework 4.6.1

## [10.1.0] - 2021-10-01

### Changed

- Create nuget packages
- [SIL.LCModel.Build.Tasks] `IdlImp` task now reports errors and warnings through msbuild logger
  instead of console
- [SIL.LCModel.Build.Tasks] `LcmGenerate` task now allows to specify location that contains
  `HandGenerated.xml` and `IntPropTypeOverrides.xml` files (`HandGeneratedDir` property)

### Deprecated

- [SIL.LCModel.Utils] `MiscUtils.RunProcess` is deprecated in favor of
  `ProcessExtensions.RunProcess`
- [SIL.LCModel.Utils] `MiscUtils.IsWindows`, `MiscUtils.IsUnix`, `MiscUtils.IsMac`,
  `MiscUtils.IsMono`, and `MiscUtils.IsDotNet` are deprecated in favor of the corresponding
  `Process.Is*` properties

[Unreleased]: https://github.com/sillsdev/liblcm/compare/v10.1.0...develop
[10.1.0]: https://github.com/sillsdev/liblcm/compare/v9.0.0...v10.1.0