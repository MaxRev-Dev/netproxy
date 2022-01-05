using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using NetProxy.Configuration.Routes;
using System.Runtime.InteropServices;
using NetProxy.Parser;
using System.IO;

namespace NetProxy
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var configJson = System.IO.File.ReadAllText("config.json");

                var configs = JsonSerializer.Deserialize<Dictionary<string, ProxyConfig>>(configJson);

                var deviceMappingsJson = System.IO.File.ReadAllText("mappings.json");
                var deviceMappings = JsonSerializer.Deserialize<RoutesRepository>(deviceMappingsJson);

                Task.WhenAll(configs.Select(c =>
                {
                    if (c.Value.protocol == "tcp")
                    {
                        try
                        {
                            var proxy = new TcpProxy(deviceMappings);
                            return proxy.Start(c.Value.forwardIp, c.Value.forwardPort, c.Value.localPort, c.Value.localIp);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to start {c.Key} : {ex.Message}");
                            throw;
                        }
                    }
                    else
                    {
                        return Task.FromException(new InvalidOperationException($"procotol not supported {c.Value.protocol}"));
                    }
                })).Wait();  
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occured : {ex}");
            }
        }
    }

    public class ProxyConfig
    {
        public string protocol { get; set; }
        public ushort localPort { get; set; }
        public string localIp { get; set; }
        public string forwardIp { get; set; }
        public ushort forwardPort { get; set; }
    }
    class TcpProxy
    {
        public TcpProxy(RoutesRepository deviceMappings)
        {
            DeviceMappings = deviceMappings;
        }

        public RoutesRepository DeviceMappings { get; }

        public async Task Start(string remoteServerIp, ushort remoteServerPort, ushort localPort, string localIp)
        {
            IPAddress localIpAddress = string.IsNullOrEmpty(localIp) ? IPAddress.IPv6Any : IPAddress.Parse(localIp);
            var server = new TcpListener(new IPEndPoint(localIpAddress, localPort));
            server.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            server.Start();

            Console.WriteLine($"TCP proxy started {localPort} -> {remoteServerIp}|{remoteServerPort}");

            var router = new RequestRouter(DeviceMappings);

            while (true)
            {
                try
                {
                    var remoteClient = await server.AcceptTcpClientAsync();
                    remoteClient.NoDelay = true;
                    var ips = await Dns.GetHostAddressesAsync(remoteServerIp);

                    new TcpClient(router, remoteClient, new IPEndPoint(ips.First(), remoteServerPort));
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(ex);
                    Console.ResetColor();
                }

            }
        }
    }
    internal class RequestRouter  
    {
        private readonly RoutesRepository _routes;

        public RequestRouter(RoutesRepository routes)
        {
            _routes = routes;
        }

        public RouteMapping Route(DeviceIdRequestPartial requestPartial)
        {
            if (!requestPartial.IsValid())
            {
                throw new InvalidDataException("Request is not valid");
            }

            return _routes.FirstOrDefault(requestPartial.DeviceId!.Value);
        }
    }
    class TcpClient
    {
        private readonly RequestRouter _router;
        private System.Net.Sockets.TcpClient _remoteClient;
        private IPEndPoint _clientEndpoint;
        private IPEndPoint _remoteServer;
        public System.Net.Sockets.TcpClient _client = new System.Net.Sockets.TcpClient();

        public TcpClient(RequestRouter router, System.Net.Sockets.TcpClient remoteClient, IPEndPoint remoteServer)
        {
            _router = router;
            _remoteClient = remoteClient;
            _remoteServer = remoteServer;
            _client.NoDelay = true;
            _clientEndpoint = (IPEndPoint)_remoteClient.Client.RemoteEndPoint;
            Console.WriteLine($"Established {_clientEndpoint} => {remoteServer}");
            Run();
        }


        private void Run()
        {

            Task.Run(async () =>
            {
                try
                {
                    using (_remoteClient)
                    using (_client)
                    {
                        await _client.ConnectAsync(_remoteServer.Address, _remoteServer.Port);

                        if (!DeviceIdParser.RetrivePacket(_client.Client, out DeviceIdRequestPartial result))
                        {
                            return;
                        }

                        var route = _router.Route(result);
                        if (route is null)
                        {
                            return;
                        }

                        using var serverStream = _client.GetStream();

                        // send the buffer (header) that we read from client before
                        serverStream.Write(result.Buffer!);

                        using var remoteStream = _remoteClient.GetStream();

                        await Task.WhenAny(
                            remoteStream.CopyToAsync(serverStream), // copy remaining message body
                            serverStream.CopyToAsync(remoteStream));
                    }
                }
                catch (Exception) { }
                finally
                {
                    Console.WriteLine($"Closed {_clientEndpoint} => {_remoteServer}");
                    _remoteClient.Close();
                    _remoteClient = null;
                }
            });
        }
    }
}