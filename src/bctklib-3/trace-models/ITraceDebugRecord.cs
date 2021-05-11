using MessagePack;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [Union(TraceRecord.RecordKey, typeof(TraceRecord))]
    [Union(NotifyRecord.RecordKey, typeof(NotifyRecord))]
    [Union(LogRecord.RecordKey, typeof(LogRecord))]
    [Union(ResultsRecord.RecordKey, typeof(ResultsRecord))]
    [Union(FaultRecord.RecordKey, typeof(FaultRecord))]
    [Union(ScriptRecord.RecordKey, typeof(ScriptRecord))]
    [Union(StorageRecord.RecordKey, typeof(StorageRecord))]
    [Union(ProtocolSettingsRecord.RecordKey, typeof(ProtocolSettingsRecord))]
    public interface ITraceDebugRecord
    {
    }
}
