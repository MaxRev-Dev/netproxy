using NetProxy.Configuration.Routes;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetProxy
{
    class TcpProxy
    {
        public TcpProxy(RoutesRepository deviceMappings)
        {
            DeviceMappings = deviceMappings;
        }

        public RoutesRepository DeviceMappings { get; }

        public async Task Start(IPEndPoint localEndPoint)
        {
            var server = new TcpListener(localEndPoint);
            server.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            try
            {
                server.Start();
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                Console.WriteLine($"Port is already in use {localEndPoint.Port}");
                return;
            }

            Console.WriteLine($"TCP proxy started {localEndPoint.Port}");

            var router = new RequestRouter(DeviceMappings);

            while (true)
            {
                try
                {
                    var acceptedSocket = await server.AcceptTcpClientAsync();
                    acceptedSocket.NoDelay = true;

                    // run client thread detached
                    _ = Task.Run(async () =>
                    {
                        using var client = new TcpClient(router, acceptedSocket);
                        await client.HandleRequest();
                    });
                }
                catch (SocketException ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                catch (ObjectDisposedException ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(ex);
                    Console.ResetColor();
                }
            }
        }
    }
}