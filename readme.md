# Neo Blockchain Toolkit Persistence Library

[![.NET Core](https://github.com/ngdseattle/neo-blockchaintoolkit-library/workflows/.NET%20Core/badge.svg?branch=master&event=push)](https://github.com/ngdseattle/neo-blockchaintoolkit-library/actions)
![](https://neobctk.blob.core.windows.net/packages/badges/vpre/Neo.BlockchainToolkit3.svg)

https://neobctk.blob.core.windows.net/packages/index.json
This repo projects for code shared between managed projects in the Neo Blockchain Toolkit.
In particular, these libraries are used in [Neo-Express](https://github.com/neo-project/neo-express)
and the [Neo Smart Contract Debugger for VS Code](https://github.com/neo-project/neo-debugger).

## Persistence

This library contains two `Neo.Persistence.IStore` implementations:

* **RocksDbStore**: This implementation stores blockchain information in a
  [RocksDb](https://rocksdb.org/). It is similar to the RocksDbStore implementation in
  [neo-modules](https://github.com/neo-project/neo-modules), but is optimized for
  fast startup and includes live checkpoint support.

* **CheckpointStore**: This implementation sits on top of any `Neo.Persistence.IReadOnlyStore`
  implementation and stores all changes in memory. This enables test/debug runs to
  use live data without persisting further changes.

## Trace Models

This library contains the model classes that read/write Time Travel Debugging (TTD) traces.
TTD traces are encoded using [MessagePack](https://msgpack.org/). These model classes
use the [MessagePack](https://github.com/neuecc/MessagePack-CSharp) managed library.
In addition to the trace model classes, this library includes message pack formatters for
Neo types that are serialized in TTD traces as well as a MessagePack resolver.
