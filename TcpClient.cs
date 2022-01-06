using System;
using System.Threading.Tasks;
using System.Net;
using NetProxy.Parser;

namespace NetProxy
{
    class TcpClient : IDisposable
    {
        private readonly RequestRouter _router;
        private System.Net.Sockets.TcpClient _remoteClient;
        private IPEndPoint _clientEndpoint;

        public TcpClient(RequestRouter router, System.Net.Sockets.TcpClient remoteClient)
        {
            _router = router;
            _remoteClient = remoteClient;
            _clientEndpoint = (IPEndPoint)_remoteClient.Client.RemoteEndPoint;
        }

        public void Dispose()
        {
            ((IDisposable)_remoteClient).Dispose();
        }

        public async Task HandleRequest()
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

                var proxyClient = new System.Net.Sockets.TcpClient
                {
                    NoDelay = true
                };
                using (proxyClient)
                {
                    await proxyClient.ConnectAsync(route.EndPoint.Address, route.EndPoint.Port);

                    Console.WriteLine($"Established {_clientEndpoint} => {proxyClient.Client.RemoteEndPoint}");

                    using var serverStream = proxyClient.GetStream();

                    // send the buffer (header) that we read from client before
                    serverStream.Write(result.Buffer!);

                    using var remoteStream = _remoteClient.GetStream();

                    await Task.WhenAny(
                        remoteStream.CopyToAsync(serverStream), // copy remaining message body
                        serverStream.CopyToAsync(remoteStream));
                }

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
    }
}