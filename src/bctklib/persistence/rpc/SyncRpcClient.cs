using System;
using System.IO;
using System.Net.Http;
using System.Text;
using Neo;
using Neo.IO.Json;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;

namespace Neo.BlockchainToolkit.Persistence.RPC
{
    public class SyncRpcClient : IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly Uri baseAddress;

        public SyncRpcClient(Uri baseAddress) : this(new HttpClient(), baseAddress)
        {
        }

        public SyncRpcClient(HttpClient httpClient, Uri baseAddress)
        {
            this.httpClient = httpClient;
            this.baseAddress = baseAddress;
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }

        static RpcRequest AsRpcRequest(string method, params JObject[] paraArgs)
        {
            return new RpcRequest
            {
                Id = 1,
                JsonRpc = "2.0",
                Method = method,
                Params = paraArgs
            };
        }
        static RpcResponse AsRpcResponse(string content)
        {
            var response = RpcResponse.FromJson(JObject.Parse(content));
            response.RawResponse = content;

            if (response.Error != null)
            {
                throw new RpcException(response.Error.Code, response.Error.Message);
            }

            return response;
        }

        HttpRequestMessage AsHttpRequest(RpcRequest request)
        {
            var requestJson = request.ToJson().ToString();
            return new HttpRequestMessage(HttpMethod.Post, baseAddress)
            {
                Content = new StringContent(requestJson, Encoding.UTF8)
            };
        }

        public RpcResponse Send(RpcRequest request)
        {
            using var requestMsg = AsHttpRequest(request);
            using var responseMsg = httpClient.Send(requestMsg);
            using var contentStream = responseMsg.Content.ReadAsStream();
            using var contentReader = new StreamReader(contentStream);
            return AsRpcResponse(contentReader.ReadToEnd());
        }

        public virtual JObject RpcSend(string method, params JObject[] paraArgs)
        {
            var request = AsRpcRequest(method, paraArgs);
            var response = Send(request);
            return response.Result;
        }


    }
}
