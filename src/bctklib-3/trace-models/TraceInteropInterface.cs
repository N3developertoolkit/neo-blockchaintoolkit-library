using Neo.VM.Types;

namespace Neo.BlockchainToolkit.TraceDebug
{
    public class TraceInteropInterface : StackItem
    {
        public TraceInteropInterface(string typeName)
        {
            TypeName = typeName;
        }

        public override StackItemType Type => StackItemType.InteropInterface;

        public string TypeName { get; }

        public override bool GetBoolean() => true;
    }
}
