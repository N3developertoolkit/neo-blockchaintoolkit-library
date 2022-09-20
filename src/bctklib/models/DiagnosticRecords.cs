using System;

namespace Neo.BlockchainToolkit.Models
{
    public record GetStorageStart(
        UInt160 ContractHash,
        string ContractName,
        ReadOnlyMemory<byte> Key);

    public record GetStorageStop(TimeSpan Elapsed);

    public record DownloadStatesStart(
        UInt160 ContractHash,
        string ContractName,
        byte? Prefix);

    public record DownloadStatesFound(
        int Total,
        int Count);

    public record DownloadStatesStop(
        int Count,
        TimeSpan Elapsed);
}