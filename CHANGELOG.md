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

### Changed

- Create nuget packages
- [SIL.LCModel.Build.Tasks] `IdlImp` task now reports errors and warnings through msbuild logger
  instead of console
- [SIL.LCModel.Build.Tasks] `LcmGenerate` task now allows to specify location that contains
  `HandGenerated.xml` and `IntPropTypeOverrides.xml` files (`HandGeneratedDir` attribute)
