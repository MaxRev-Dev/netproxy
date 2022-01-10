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
        const string requestMessage = "Request";

        const string targetServer = "127.0.0.1:9000";
        private readonly IPEndPoint proxyEndpoint;

        public StressTests(ITestOutputHelper output)
        {
            _output = output;
            var random = new Random();
            deviceId = 50000;
            var proxyPort = 15000;
            proxyEndpoint = new IPEndPoint(IPAddress.Loopback, proxyPort);
        }

        [MemberData(nameof(ThreadsToExecute))]
        [Theory]
        public async Task RunStressTest(int requestsCount)
        {
            var resolvedIP = IPEndPoint.Parse(targetServer);

            var cts = new CancellationTokenSource();
            //cts.CancelAfter(30000);
            var token = cts.Token;

            // start proxy listener
            int threadsActive = requestsCount;
            _output.WriteLine($"Requests count {requestsCount}");
            var stopwatch = new Stopwatch();
            var barrier = new Barrier(requestsCount, (b) =>
            {
                if (b.CurrentPhaseNumber == 1)
                {
                    stopwatch.Start();
                }
            });

            await Task.WhenAll(Enumerable.Range(0, requestsCount)
                .Select(x => new TcpClient())
                .Select(remoteClient =>
            {
                return Task.Run(async () =>
                {
                    using (remoteClient)
                        try
                        {
                            remoteClient.NoDelay = true;
                            remoteClient.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
                            await remoteClient.ConnectAsync(proxyEndpoint.Address, proxyEndpoint.Port);

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
                        }
                        catch (Exception ex)
                        {
                            _output.WriteLine(ex.Message);

                        }
                }, token);
            }));
            stopwatch.Stop();
            _output.WriteLine($"Elapsed: {stopwatch.ElapsedMilliseconds} ms");
        }
    }
}