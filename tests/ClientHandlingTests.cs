using NetProxy.Configuration.Routes;
using NetProxy.Parser;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NetProxy.Tests
{
    public partial class ClientHandlingTests
    {
        [Fact]
        public async void Client_connection_flow_works()
        {
            // in case of a read-blocking operations
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            var token = cts.Token;

            const string requestMessage = "Request";
            const string responseMessage = "Some awesome test";

            var targetServer = "localhost:9001";
            var resolvedIP = Utils.ResloveDns(targetServer);

            // run a simple stub for this request
            var serviceStub = GetServiceStub(resolvedIP, requestMessage, responseMessage, token);

            uint? deviceId = 5248274;

            // start proxy listener
            var proxyEndpoint = new IPEndPoint(IPAddress.Loopback, 34000);
            _ = StartProxy(targetServer, deviceId.Value, proxyEndpoint);

            var remoteClient = new TcpClient();
            remoteClient.Connect(proxyEndpoint);

            var remoteClientStream = remoteClient.GetStream();

            var buffer = new byte[DeviceIdParser.HeaderSize];
            TestHelpers.CopyIdToBuffer(deviceId!.Value, buffer);

            await remoteClientStream.WriteAsync(buffer, token);
            await remoteClientStream.WriteAsync(Encoding.UTF8.GetBytes(requestMessage), token);

            // read server response 
            buffer = new byte[100];
            int messageSize = await remoteClientStream.ReadAsync(buffer, token);
            Assert.True(messageSize > 0);

            var result = Encoding.UTF8.GetString(buffer.AsSpan(0, messageSize));
            Assert.Equal(responseMessage, result);

            await serviceStub;
        }

        private static Task StartProxy(string targetServer, uint deviceId, IPEndPoint proxyEndpoint)
        {
            var proxy = new TcpProxy(
                new RoutesRepository(new[]
                {
                    new RouteMapping
                    {
                        From = deviceId.ToString(),
                        To = targetServer
                    }
                }));
            return proxy.Start(proxyEndpoint);
        }

        private Task GetServiceStub(System.Net.IPEndPoint resolvedIP, string requestMessage, string responseMessage, CancellationToken token)
        {
            return Task.Run(async () =>
            {
                var serviceListener = new TcpListener(resolvedIP);
                serviceListener.Start();

                while (!serviceListener.Pending())
                    Thread.Sleep(10);

                // read message
                var clientSocket = serviceListener.AcceptSocket();
                var serverStream = new NetworkStream(clientSocket, false);

                var buffer = new byte[DeviceIdParser.HeaderSize];
                var received = await serverStream.ReadAsync(buffer, token);
                Assert.Equal(received, DeviceIdParser.HeaderSize);
                buffer = new byte[100];
                received = await serverStream.ReadAsync(buffer, token);
                Assert.True(received > 0);
                Assert.Equal(requestMessage, Encoding.UTF8.GetString(buffer.AsSpan(0, received)));
                // write server response
                await serverStream.WriteAsync(Encoding.UTF8.GetBytes(responseMessage), token);
                await serverStream.FlushAsync(token);
            }, token);
        }
    }
}