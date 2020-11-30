# Neo Blockchain Toolkit Persistence Library

[![.NET Core](https://github.com/ngdseattle/neo-blockchaintoolkit-library/workflows/.NET%20Core/badge.svg?branch=master&event=push)](https://github.com/ngdseattle/neo-blockchaintoolkit-library/actions)
[![](https://img.shields.io/endpoint?logo=nuget&url=https%3A%2F%2Fneobctk.blob.core.windows.net%2Fpackages%2Fbadges%2Fvpre%2Fneo.blockchaintoolkit3.json)](https://neobctk.blob.core.windows.net/packages/index.json)

This repo projects for code shared between managed projects in the Neo Blockchain Toolkit.
In particular, these libraries are used in [Neo-Express](https://github.com/neo-project/neo-express)
and the [Neo Smart Contract Debugger for VS Code](https://github.com/neo-project/neo-debugger).

CI packages are available via [GitHub Packages](https://github.com/ngdseattle/neo-blockchaintoolkit-library/packages/)
and a publicly hosted [NuGet feed](https://neobctk.blob.core.windows.net/packages/index.json)
for anonymous usage.

> Note, downstream Neo Blockchain Toolkit projects use the hosted NuGet feed as GitHub Packages
  [requires authentication for installation](https://docs.github.com/en/free-pro-team@latest/packages/publishing-and-managing-packages/installing-a-package#installing-a-package).

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

## Trace Models

This library contains the model classes that read/write Time Travel Debugging (TTD) traces.
TTD traces are encoded using [MessagePack](https://msgpack.org/). These model classes
use the [MessagePack](https://github.com/neuecc/MessagePack-CSharp) managed library.
In addition to the trace model classes, this library includes message pack formatters for
Neo types that are serialized in TTD traces as well as a MessagePack resolver.
