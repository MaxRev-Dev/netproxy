using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NetProxy
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                var deviceMappingsJson = System.IO.File.ReadAllText("mappings.json");
                var deviceMappings = JsonSerializer.Deserialize<RouteMapping[]>(deviceMappingsJson);

                await Task.WhenAll(
                    // start listeners on specified ports
                    // this will filter duplicates
                    deviceMappings
                    .Select(port =>
                          // create a proxy task with endpoint
                          new TcpProxy(deviceMappings)
                            .Start(new IPEndPoint(IPAddress.Any, 3000))));
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"It seems like JSON configuration is malformed: {ex.Message}");
            }
            catch (Exception ex)
            {
                // should never go here
                Console.WriteLine($"An error occured : {ex}");
            }
        }
    }

    internal class IncommingTcpClient : IDisposable
    {
        private TcpClient _remoteClient;
        private IPEndPoint _clientEndpoint;
        private IList<RouteMapping> _mappings;
        public IncommingTcpClient(IList<RouteMapping> mappings, TcpClient remoteClient)
        {
            _mappings = mappings;
            _remoteClient = remoteClient;
            _clientEndpoint = (IPEndPoint)_remoteClient.Client.RemoteEndPoint;
        }

        public void Dispose()
        {
            ((IDisposable)_remoteClient).Dispose();
        }

        public const int HeaderStart = 3;
        public const int DeviceIdSize = 4;
        public const int HeaderSize = HeaderStart + DeviceIdSize;

        public async Task HandleRequest(CancellationToken token = default)
        {
            try
            {
                var buffer = new byte[HeaderSize];
                uint deviceId;
                try
                {
                    _remoteClient.Client.Receive(buffer);
                    deviceId = BitConverter.ToUInt32(buffer, HeaderStart);
                }
                catch
                {
                    // we can't do nothing here. request is malformed
                    return;
                }

                var route = _mappings.FirstOrDefault(x => x.Contains(deviceId));
                if (route is null)
                {
                    // nothing was found in direct and range mappings
                    // select wildcard one if available
                    route = _mappings.FirstOrDefault(x => x.IsManyToOne);
                    if (route is null)
                    {
                        return;
                    }
                }

                using var proxyClient = new TcpClient
                {
                    NoDelay = true
                };
                await proxyClient.ConnectAsync(route.EndPoint.Address, route.EndPoint.Port, token);

                Console.WriteLine($"Established {_clientEndpoint} => {proxyClient.Client.RemoteEndPoint}");

                using var serverStream = proxyClient.GetStream();

                // send the buffer (header) that we read from client before
                await serverStream.WriteAsync(buffer, token);

                using var remoteStream = _remoteClient.GetStream();

                await Task.WhenAll(
                    remoteStream.CopyToAsync(serverStream, token), // copy remaining message body
                    serverStream.CopyToAsync(remoteStream, token));
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

    class TcpProxy
    {
        public TcpProxy(IList<RouteMapping> deviceMappings)
        {
            DeviceMappings = deviceMappings;
        }

        public IList<RouteMapping> DeviceMappings { get; }

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

            var router = DeviceMappings;

            while (true)
            {
                try
                {
                    var acceptedClient = await server.AcceptTcpClientAsync();
                    acceptedClient.NoDelay = true;

                    // run client thread detached
                    _ = Task.Run(async () =>
                    {
                        using var client = new IncommingTcpClient(router, acceptedClient);
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

    public class RouteMapping
    {
        private string _to;
        private string _from;
        private uint[] _allowed = Array.Empty<uint>();
        private RangeDefinition[] _ranges = Array.Empty<RangeDefinition>();

        public RouteMapping()
        {
            // this ctor mainly for configuration binder
        }

        public string From
        {
            get
            {
                return _from;
            }

            set
            {
                _from = value;
                if (!string.IsNullOrEmpty(value))
                {
                    FromRawInput(value);
                }
            }
        }
        public bool Contains(uint id)
        {
            if (_allowed.Contains(id))
            {
                return true;
            }

            return _ranges.Any(x => x.Contains(id));
        }

        public RouteMapping FromRawInput(string from)
        {
            from = from.Replace(" ", "");
            var parts = from.Split(',', ';');

            var splitGroup = parts.GroupBy(x => x.Contains('-'));

            // parse direct ids
            _allowed = splitGroup.Where(x => !x.Key)
               .SelectMany(x => x)
               .Select(uint.Parse).ToArray();
            // parse ranges
            _ranges = splitGroup.Where(x => x.Key)
                .SelectMany(x => x)
                .Select(x => new RangeDefinition(x)).ToArray();
            return this;
        }

        public IPEndPoint EndPoint { get; private set; }
        public bool IsManyToOne
        {
            get
            {
                return _from == "*";
            }
        }

        public string To
        {
            get => _to;
            set
            {
                if (value is null)
                {
                    return;
                }

                var source = value;
                EndPoint = IPEndPoint.Parse(source);
                _to = value;
            }
        }

        internal class RangeDefinition
        {
            public RangeDefinition(string raw)
            {
                var values = raw.Split('-').Select(x => x.Trim()).ToArray();
                From = uint.Parse(values[0]);
                To = uint.Parse(values[1]);
            }
            public uint From { get; }
            public uint To { get; }
            public bool Contains(uint id)
            {
                return id >= From && id <= To;
            }
        }
    }
}