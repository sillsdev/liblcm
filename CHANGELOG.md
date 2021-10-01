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