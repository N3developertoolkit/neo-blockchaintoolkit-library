# Neo Blockchain Toolkit Persistence Library

[![Build Status](https://dev.azure.com/ngdenterprise/Build/_apis/build/status/ngdenterprise.neo-blockchaintoolkit-library?branchName=master)](https://dev.azure.com/ngdenterprise/Build/_build/latest?definitionId=6&branchName=master)

This repo projects for code shared between managed projects in the Neo Blockchain Toolkit.
In particular, these libraries are used in [Neo-Express](https://github.com/neo-project/neo-express)
and the [Neo Smart Contract Debugger for VS Code](https://github.com/neo-project/neo-debugger).

Continutious Integration build packages are available via
[Azure Artifacts](https://dev.azure.com/ngdenterprise/Build/_packaging?_a=feed&feed=public).

## Contract Parameter Parsing

This library contains code to parse Neo Express contract invoke files as specified
in [NDX-DN12](https://github.com/ngdseattle/design-notes/blob/master/NDX-DN12%20-%20Neo%20Express%20Invoke%20Files.md).
This includes custom handling of JSON native types (boolean, integer, null, array)
as well as custom handling of JSON strings for encoding addresses with `@` prefix,
hashes with `#` prefix and hex strings with `0x` prefix.

## Persistence

This library contains two `Neo.Persistence.IStore` implementations:

* **RocksDbStore**: This implementation stores blockchain information in a
  [RocksDb](https://rocksdb.org/). It is similar to the RocksDbStore implementation in
  [neo-modules](https://github.com/neo-project/neo-modules), but is optimized for
  fast startup and includes live checkpoint support.

* **CheckpointStore**: This implementation sits on top of any `Neo.Persistence.IReadOnlyStore`
  implementation and stores all changes in memory. This enables test/debug runs to
  use live data without persisting further changes.

* **StateServiceStore**: This implementation sits on top of a [StateService node](https://github.com/neo-project/neo-modules/tree/master/src/StateService)
  running with `FullState: true`. This enables code to use live data from a public Neo
  blockchain network such as MainNet or TestNet.

## Trace Models

This library contains the model classes that read/write Time Travel Debugging (TTD) traces.
TTD traces are encoded using [MessagePack](https://msgpack.org/). These model classes
use the [MessagePack](https://github.com/neuecc/MessagePack-CSharp) managed library.
In addition to the trace model classes, this library includes message pack formatters for
Neo types that are serialized in TTD traces as well as a MessagePack resolver.

# Application Engines

This library contains two `Neo.SmartContract.ApplicationEngine` subclasses:

* **TestApplicationEngine**: This implementation is used across test scenarios. It supports
  overriding the CheckWitness service and collecting code coverage information
* **TraceApplicationEngine**: This implementation writes trace information to a provided
  ITraceDebugSink. The Trace Model classes (described above) include an implementation of
  ITraceDebugSink that writes trace messages to a file in MessagePack format. 
