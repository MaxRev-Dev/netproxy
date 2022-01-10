using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NetProxy.Tests
{
    [Collection("Sequential")]
    public partial class StressTests
    {
        public static IEnumerable<object[]> ThreadsToExecute =>
            new[]{
                1,50,500,1000,2000,5000,10000
            }.Select(x => new object[] { x }).ToArray();

        private ITestOutputHelper _output;
        private uint deviceId;
        private volatile float clientTimeAverageMs = 0;
        const string requestMessage = "Request";

        const string targetServer = "127.0.0.1:9000";
        private readonly IPEndPoint proxyEndpoint;

        public StressTests(ITestOutputHelper output)
        {
            _output = output; 
            deviceId = 50000;
            var proxyPort = 15000;
            proxyEndpoint = new IPEndPoint(IPAddress.Loopback, proxyPort);
        }

        [MemberData(nameof(ThreadsToExecute))]
        [Theory]
        public async Task RunStressTest(int requestsCount)
        {
            var resolvedIP = IPEndPoint.Parse(targetServer);
            var token = CancellationToken.None;
            await Task.WhenAll(Enumerable.Range(0, requestsCount)
                .Select(x => new TcpClient())
                .Select(remoteClient =>
            {
                return Task.Run(async () =>
                {
                    var clientStopwatch = new Stopwatch();
                    using (remoteClient)
                        try
                        { 
                            remoteClient.NoDelay = true;
                            remoteClient.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
                            await remoteClient.ConnectAsync(proxyEndpoint.Address, proxyEndpoint.Port);
                            clientStopwatch.Start();
                            var remoteClientStream = remoteClient.GetStream();

                            var buffer = new byte[IncommingTcpClient.DefaultPeekBufferSize];
                            TestHelpers.CopyIdToBuffer(deviceId, buffer);

                            await remoteClientStream.WriteAsync(buffer, token);
                            await remoteClientStream.WriteAsync(Encoding.UTF8.GetBytes(requestMessage), token);
                            await remoteClientStream.FlushAsync();
                            // read server response 
                            buffer = new byte[100];
                            var read = await remoteClientStream.ReadAsync(buffer, token);
                            Assert.True(read > 0);
                            clientStopwatch.Stop();
                            Interlocked.Exchange(ref clientTimeAverageMs, clientTimeAverageMs + clientStopwatch.ElapsedMilliseconds / requestsCount);
                        }
                        catch (Exception ex)
                        {
                            _output.WriteLine(ex.Message); 
                        }
                }, token);
            }));
            _output.WriteLine($"Client time average: {clientTimeAverageMs} ms"); 
        }
    }
}