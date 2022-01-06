using System;
using System.Threading.Tasks;
using System.Net;
using NetProxy.Parser;
using NetProxy.Configuration.Routes;
using System.Threading;

namespace NetProxy.Core
{
    internal class IncommingTcpClient : IDisposable
    {
        private readonly RequestRouter _router;
        private System.Net.Sockets.TcpClient _remoteClient;
        private IPEndPoint _clientEndpoint;

        public IncommingTcpClient(RequestRouter router, System.Net.Sockets.TcpClient remoteClient)
        {
            _router = router;
            _remoteClient = remoteClient;
            _clientEndpoint = (IPEndPoint)_remoteClient.Client.RemoteEndPoint;
        }

        public void Dispose()
        {
            ((IDisposable)_remoteClient).Dispose();
        }

        public async Task HandleRequest(CancellationToken token = default)
        {
            try
            {
                if (!DeviceIdParser.RetrivePacket(_remoteClient.Client, out DeviceIdRequestPartial result))
                {
                    return;
                }

                var route = _router.Route(result);
                if (route is null)
                {
                    return;
                }

                using var proxyClient = new System.Net.Sockets.TcpClient
                {
                    NoDelay = true
                };
                await HandleData(route, proxyClient, result, token);
            }
            catch (ObjectDisposedException)
            {
                // connection aborted
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during client request handling: " + ex.Message);
            }
        }

        internal async Task HandleData(RouteMapping route, System.Net.Sockets.TcpClient proxyClient, DeviceIdRequestPartial result, CancellationToken token = default)
        {
            await proxyClient.ConnectAsync(route.EndPoint.Address, route.EndPoint.Port, token);

            Console.WriteLine($"Established {_clientEndpoint} => {proxyClient.Client.RemoteEndPoint}");

            using var serverStream = proxyClient.GetStream();

            // send the buffer (header) that we read from client before
            await serverStream.WriteAsync(result.Buffer!, token);

            using var remoteStream = _remoteClient.GetStream();

            await Task.WhenAll(
                remoteStream.CopyToAsync(serverStream, token), // copy remaining message body
                serverStream.CopyToAsync(remoteStream, token));
        }
    }
}