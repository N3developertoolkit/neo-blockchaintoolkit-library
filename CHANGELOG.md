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

* `TestApplicationEngine` emits raw code coverage data to folder specified in `NEO_TEST_APP_ENGINE_COVERAGE_PATH`
  environment variable when set. 

## [3.5.30] - 2023-01-03

### Added

* `ToolkitWallet` a shared implementation of Neo Wallet for developer scenarios
* `BranchInfo` model type
* Diagnostic record model types
* `EnableAnsiEscapeSequences` utility method
* `ResolveFileName` extension method
* `JsonWriterExtensions` extension methods
* WorkNet extension constants

### Changed

* Updated `DebugInfo` model types to use C# `record` / `record struct`
* Marked `DebugInfo.LoadAsync` obsolete
* PersistentTrackingStore class uses column family named `PersistentTrackingStore` by default
* Reworked `StateServiceStore` to load as much info asyncronously ahead of time (using `BranchInfo` record),
  handle remote logic directly and redefined `ICacheClient` to only handle caching responsibilities

### Removed

* RocksDB extension methods that have recently been encorporated into [rocksdb-sharp library](https://github.com/curiosity-ai/rocksdb-sharp)

### Engineering Systems

* Reworked GitHub Action workflows into reusable pieces (`test` & `package`) and action specific workflows (`pr`, `push` & `release`).

## [3.4.13] - 2202-08-17

### Changed

* Updated to Neo v3.4 ([#70](https://github.com/ngdenterprise/neo-blockchaintoolkit-library/pull/70))

## [3.3.21] - 2022-06-27

### Added

* CreateGenesisContract extension method (#60)
* Utility.VerifyProof method (1d4b6ab53868b0b0fda961d135e6d86f9c65f6ce)
* PersistentTrackingStore (#67)

### Removed 

* Removed storage providers (#69)
  * Storage Providers are specific to Neo Express so they were [moved into that repo](https://github.com/neo-project/neo-express/pull/235)

### Changed

* Updated to Neo 3.3.1 (#69)
* Verify state service proofs in StateServiceStore methods (#64)
* MemoryTrackingStore throws NullReferenceException when `Put` a null value to match RocksDB behavior
* Moved `RocksDbStorageProvider.CreateCheckpoint` overloads to be extension methods
* Support additional instructions in GetComment (4cafe6643f0516ba8ba3536bea4e725da9bce12e)

### Engineering

* Changed to embedded debug symbols
* Replaced all Azure DevOps Pipelines with GitHub Actions 
* Updated build definition to run tests on MacOS as well as Windows and Ubuntu

## [3.1.35] - 2022-02-25

* ContractParameterParser.ConvertObject and ContractParameterParser.ConvertStackItem static methods (#56)
* Added method token support for CALLT opCode in Extensions.GetComment (#57)

## [3.1.27] - 2021-12-14

### Changed
* Update to Neo 3.1.0, target framework net6.0 and C# language version 10 (#55)
* Update KNOWN_NETWORK_NUMBERS (#55)
* Moved general Neo MessagePack formatters to MessagePack.Formatters.Neo.BlockchainToolkit namespace (#55)
* Simplified service override code in `TestApplicationEngine` (#50)
* Made `NullStore.Instance` readonly (#50)
* `MemoryTrackingStore`disposes underlying `IReadOnlyStore`, if underlying store is `IDisposable` (#51)
* Replaced `checkpointCleanup` disposable with checkpoint folder cleanup in `CheckpointStorageProvider` (#51)
* Replaced `CheckpointStorageProvider` public constructor with static `Open` method (#51)
* Made metadata parameters to `RocksDbUtility.RestoreCheckpoint` optional (#51)

### Added
* Instruction and branch execution tracking in `TestApplicationEngine` (#50)
* `ICheckpointStore`, `CheckpointStore` and `NullCheckpointStore` (#51)
* `RocksDbUtility.GetTempPath` (#51)
* `IStore.EnsureLedgerInitialized` extension method (#51)
* `RocksDbStorageProvider.OpenForDiscard` static method (#51)
* Added Base64 encoding support for ContractParameterParser.ParseObjectParameter (#55)
* Add UInt256 MessagePack Formatter (#55)

### Removed
* `IDisposableStorageProvider` interface (#51)
* Obsolete `RocksDbStorageProvider.RestoreCheckpoint` methods (#51)
* Public `RocksDbStore` constructors (#51)


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
