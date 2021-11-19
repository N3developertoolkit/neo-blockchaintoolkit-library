using System;
using System.Collections.Generic;
using System.Linq;
using Neo.IO.Json;

namespace test.bctklib
{
    class TestableRpcClient : Neo.Network.RPC.RpcClient
    {
        Stack<Func<JObject>> funcStack = new();

        public TestableRpcClient(params Func<JObject>[] functions) : base(null)
        {
            foreach (var func in functions.Reverse())
            {
                funcStack.Push(func);
            }

        }
        public override JObject RpcSend(string method, params JObject[] paraArgs)
        {
            return funcStack.Pop()();
        }
    }
}
