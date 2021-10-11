using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Neo.BlockchainToolkit.Persistence;
using Neo.Network.RPC;
using Neo.SmartContract.Native;

namespace state_store
{
    class Program
    {
        const string MAINNET_URL = "http://127.0.0.1:10332";
        const string TESTNET_URL = "http://127.0.0.1:20332";

        static async Task Main(string[] args)
        {
            await NewMethodAsync(TESTNET_URL).ConfigureAwait(false);
            await NewMethodAsync(MAINNET_URL).ConfigureAwait(false);
        }

        private static async Task NewMethodAsync(string url)
        {
            var uri = new Uri(url);
            uint count;
            using (var rpc = new RpcClient(uri))
            {
                count = await rpc.GetBlockCountAsync().ConfigureAwait(false);
            }

            var cachePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Console.WriteLine($"{cachePath} - {url}");

            try
            {
                Console.Write($"StateServiceStore ctor ");
                var sw = Stopwatch.StartNew();
                using (var store = new StateServiceStore(uri, count - 1))
                {
                    var t1 = sw.Elapsed;
                    Console.WriteLine(t1);

                    var key = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 };
                    foreach (var contract in NativeContract.Contracts)
                    {
                        if (contract.Id == NativeContract.Ledger.Id) continue;

                        Console.WriteLine($"{contract.Name} {contract.Id}");
                        // BinaryPrimitives.WriteInt32LittleEndian(key.AsSpan(0, 4), contract.Id);

                        // sw.Restart();
                        // store.TryGet(key);
                        // var t = sw.Elapsed;
                        // Console.WriteLine(t);
                    }

                    foreach (var kvp in store.ContractMap.OrderBy(p => p.Key))
                    {
                        Console.WriteLine($"{kvp.Key} {kvp.Value.manifest.Name}");
                    }

                }


                // var di = new DirectoryInfo(cachePath);
                // var size = di.EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length);
                // Console.WriteLine(size);
            }
            finally
            {
                // Directory.Delete(cachePath, true);
            }
        }
    }
}
