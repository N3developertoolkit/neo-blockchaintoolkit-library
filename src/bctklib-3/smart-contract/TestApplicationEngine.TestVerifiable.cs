using System;
using System.IO;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;

namespace Neo.BlockchainToolkit.SmartContract
{
    public partial class TestApplicationEngine
    {
        class TestVerifiable : IVerifiable
        {
            readonly UInt160[] signers;

            public TestVerifiable(params UInt160[] signers)
            {
                this.signers = signers;
            }

            public UInt160[] GetScriptHashesForVerifying(DataCache snapshot) => signers;

            public Witness[] Witnesses
            {
                get => throw new NotImplementedException();
                set => throw new NotImplementedException();
            }

            public int Size => throw new NotImplementedException();
            public void Deserialize(BinaryReader reader) => throw new NotImplementedException();
            public void DeserializeUnsigned(BinaryReader reader) => throw new NotImplementedException();
            public void Serialize(BinaryWriter writer) => throw new NotImplementedException();
            public void SerializeUnsigned(BinaryWriter writer) => throw new NotImplementedException();
        }
    }
}
