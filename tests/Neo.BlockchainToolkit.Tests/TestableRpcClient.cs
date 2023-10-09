// Copyright (C) 2023 neo-project
//
// neo-blockchaintoolkit-library is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.BlockchainToolkit.Tests
{
    class TestableRpcClient : Neo.Network.RPC.RpcClient
    {
        Queue<Func<JToken>> responseQueue = new();

        public TestableRpcClient(params Func<JToken>[] functions) : base(null)
        {
            foreach (var func in functions.Reverse())
            {
                responseQueue.Enqueue(func);
            }
        }

        public void QueueResource(string resourceName)
        {
            responseQueue.Enqueue(() => JToken.Parse(Utility.GetResource(resourceName)) ?? throw new NullReferenceException());
        }

        public override JToken RpcSend(string method, params JToken[] paraArgs)
        {
            return responseQueue.Dequeue()();
        }
    }
}
