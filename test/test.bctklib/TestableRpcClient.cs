using System;
using System.Collections.Generic;
using System.Linq;
using Neo.IO.Json;

namespace test.bctklib
{
    class TestableRpcClient : Neo.Network.RPC.RpcClient
    {
        Queue<Func<JObject>> responseQueue = new();

        public TestableRpcClient(params Func<JObject>[] functions) : base(null)
        {
            foreach (var func in functions.Reverse())
            {
                responseQueue.Enqueue(func);
            }
        }


        public void QueueResource(string resourceName)
        {
            responseQueue.Enqueue(() => JObject.Parse(Utility.GetResource(resourceName)));
        }

        public override JObject RpcSend(string method, params JObject[] paraArgs)
        {
            return responseQueue.Dequeue()();
        }
    }
}
