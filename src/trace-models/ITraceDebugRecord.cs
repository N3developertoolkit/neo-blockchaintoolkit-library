using MessagePack;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [Union(TraceRecord.RecordKey, typeof(TraceRecord))]
    [Union(NotifyRecord.RecordKey, typeof(NotifyRecord))]
    [Union(LogRecord.RecordKey, typeof(LogRecord))]
    [Union(ResultsRecord.RecordKey, typeof(ResultsRecord))]
    [Union(FaultRecord.RecordKey, typeof(FaultRecord))]
    public interface ITraceDebugRecord
    {
    }
}
