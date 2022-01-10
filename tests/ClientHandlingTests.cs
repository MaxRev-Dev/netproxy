using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NetProxy.Tests
{
    public partial class ClientHandlingTests
    {
        private ITestOutputHelper _output;
        public ClientHandlingTests(ITestOutputHelper output)
        {
            _output = output;
        }
        [Fact]
        public async Task Client_connection_flow_works()
        {
            // in case of a read-blocking operations
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            var token = cts.Token;

            const string requestMessage = "Request";
            const string responseMessage = "Some awesome test";

            var targetServer = "127.0.0.1:9000";
            var resolvedIP = IPEndPoint.Parse(targetServer);

            // run a simple stub for this request
            var serviceStub = GetServiceStub(resolvedIP, requestMessage, responseMessage, token);

            uint? deviceId = 5248274;

            // start proxy listener
            var proxyEndpoint = new IPEndPoint(IPAddress.Loopback, 34000);
            _ = TestHelpers.StartProxy(targetServer, deviceId.Value, proxyEndpoint);

            var remoteClient = new TcpClient();
            remoteClient.Connect(proxyEndpoint);

            var remoteClientStream = remoteClient.GetStream();

            var buffer = new byte[IncommingTcpClient.DefaultPeekBufferSize];
            TestHelpers.CopyIdToBuffer(deviceId!.Value, buffer);

            await remoteClientStream.WriteAsync(buffer, token);
            await remoteClientStream.WriteAsync(Encoding.UTF8.GetBytes(requestMessage), token);
            await remoteClientStream.FlushAsync(token);

            // read server response 
            buffer = new byte[100];
            int messageSize = await remoteClientStream.ReadAsync(buffer, token);
            Assert.True(messageSize > 0);

            var result = Encoding.UTF8.GetString(buffer.AsSpan(0, messageSize));
            Assert.Equal(responseMessage, result);

            cts.Cancel();
            await serviceStub;
        }

        private Task GetServiceStub(IPEndPoint resolvedIP, string requestMessage, string responseMessage, CancellationToken token)
        {
            return Task.Run(() =>
            {
                var serviceListener = new TcpListener(resolvedIP);
                serviceListener.Start();

                while (!token.IsCancellationRequested)
                {
                    // this won't block the loop
                    if (!serviceListener.Pending())
                    {
                        Thread.Sleep(10);
                        continue;
                    }
                    var clientSocket = serviceListener.AcceptSocket();
                    _ = Task.Run(async () =>
                    {
                        var serverStream = new NetworkStream(clientSocket, false);

                        var buffer = new byte[IncommingTcpClient.DefaultPeekBufferSize];
                        var received = await serverStream.ReadAsync(buffer, token);
                        Assert.Equal(received, IncommingTcpClient.DefaultPeekBufferSize);
                        buffer = new byte[100];
                        received = await serverStream.ReadAsync(buffer, token);
                        Assert.True(received > 0);
                        Assert.Equal(requestMessage, Encoding.UTF8.GetString(buffer.AsSpan(0, received)));
                        // write server response
                        await serverStream.WriteAsync(Encoding.UTF8.GetBytes(responseMessage), token);
                        await serverStream.FlushAsync(token);
                    });
                }
            }, token);
        }
    }
}