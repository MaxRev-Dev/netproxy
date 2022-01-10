using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace NetProxy.Tests
{
    public static class TestHelpers
    {
        public static void CopyIdToBuffer(uint deviceId, byte[] buffer)
        {
            var sliceOfId = buffer.AsSpan()
                .Slice(IncommingTcpClient.DeviceIdStart, IncommingTcpClient.DeviceIdSize);
            MemoryMarshal.Write(sliceOfId, ref deviceId);
        }

        public static Task StartProxy(string targetServer, uint deviceId, IPEndPoint proxyEndpoint)
        {
            var proxy = new TcpProxy(
                new[] {
                    new RouteMapping
                    {
                        From = deviceId.ToString(),
                        To = targetServer
                    }
                });
            return proxy.Start(proxyEndpoint);
        }
    }
}