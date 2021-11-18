# Neo Blockchain Toolkit Change Log

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This project uses [NerdBank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning)
to manage version numbers. This tool automatically sets the Semantic Versioning Patch
value based on the [Git height](https://github.com/dotnet/Nerdbank.GitVersioning#what-is-git-height)
of the commit that generated the build. As such, released versions of this package
will not have contiguous patch numbers. Initial major and minor releases will be documented
in this file without a patch number. Patch version will be included for bug fixes, but
may not exactly match a publicly released version.

## [Unreleased]

### Added
* Instruction and branch execution tracking in `TestApplicationEngine` (#50)
* `ICheckpointStore`, `CheckpointStore` and `NullCheckpointStore`
* `RocksDbUtility.GetTempPath`
* `IStore.EnsureLedgerInitialized` extension method
* `RocksDbStorageProvider.OpenForDiscard` static method

### Removed
* `IDisposableStorageProvider` interface
* Obsolete `RocksDbStorageProvider.RestoreCheckpoint` methods
* public `RocksDbStore` constructors

### Changes

* Simplified service override code in `TestApplicationEngine` (#50)
* Made `NullStore.Instance` readonly (#50)
* `MemoryTrackingStore`disposes underlying `IReadOnlyStore`, if underlying store is `IDisposable`
* Replaced `checkpointCleanup` disposable with checkpoint folder cleanup in `CheckpointStorageProvider`
* Replaced `CheckpointStorageProvider` public constructor with static `Open` method
* Made metadata parameters to `RocksDbUtility.RestoreCheckpoint` optional

## [3.0.12] - 2021-10-12

Thanks to @merl111 for his contribution (#41) in this release

### Added

* static function to create a dummy block for testing (#41) 
* StateServiceStore (#45, #46, #48)

### Changes

* Persistence Refactoring (#44)
* Update dependencies (#47)
  * MessagePack to 2.3.85
  * Neo to 3.0.3
  * OneOf to 3.0.201
  * rocksdb to 6.22.1.20635
  * System.IO.Abstractions to 13.2.47

## [3.0.3] - 2021-08-06

### Changed

* Updated Neo dependency to 3.0.2

## [3.0] - 2021-08-02

### Changed

* Neo N3 release support
* Bumped major version to 3 for consistency with Neo N3 release
* Changed package name from Neo.BlockchainToolkit3 to Neo.BlockchainToolkit.Library

## [1.0.56-preview] - 2021-07-21

### Changed

* Neo N3 RC4 support
* Update trace models (#34)

### Fixed

* correctly update immutable tracking map (#37)
* move read array/map header out of for loop in StackItemFormatter.Deserialize (#38)
* support empty string param in TrimStartDirectorySeparators (f271ac0e745cc2da00f59452364ce90aaa04d260)
* Ensure operation field is not null (#39)


## [1.0.51-preview] - 2021-06-15

### Changed

* Update GitHub + Azure Pipeline Build files

### Fixed

* Create test transactions for TestApplicationEngine (#33)

## [1.0.47-preview] - 2021-06-06

### Fixed

* Cross Platform Path Handling (#31)

## [1.0.46-preview] - 2021-06-04

### Changed

* Neo N3 RC3 support

### Fixed

* Handle sequence points with negative document index values (#27)
* Resolve Slot Indexes when loading debug info (#28)
* Work around https://github.com/neo-project/neo-devpack-dotnet/issues/637 (#29)

## [1.0.42-preview] - 2021-05-17

### Added

* file:// URI support (#17)
* Debug Info Static Variables (#21)
* added ProtocolSettingsRecord (#22)
* support optional explicit slot indexes (#25)

### Fixed

* ensure length before accessing string char by index + test (#16) fixes https://github.com/neo-project/neo-express/issues/136

## [1.0.34-preview] - 2021-05-04

### Changed

* Neo N3 RC2 support

### Added

* Debug Info Parsing
* Disassembly Helper Methods (`EnumerateInstructions`, `GetOperandString` and `GetComment`)

## [1.0.28-preview] - 2021-03-18

### Changed

* Neo N3 RC1 support

## [1.0.9-preview] - 2021-02-08

### Changed

* Neo 3 Preview 5 support

## [0.4.67-preview] - 2020-12-28

Initial Release