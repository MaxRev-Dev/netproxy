using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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

                // create a proxy task with endpoint
                await new TcpProxy(deviceMappings)
                              .Start(new IPEndPoint(IPAddress.Any, 3000));
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

        public const int DeviceIdStart = 5;
        public const int DeviceIdSize = sizeof(uint);
        public const int DefaultPeekBufferSize = DeviceIdStart + DeviceIdSize;

        public void HandleRequest(CancellationToken token = default)
        {
            // peek a small amount of payload to see if we can forward it
            var buffer = new byte[DefaultPeekBufferSize];
            uint deviceId;
            try
            {
                _remoteClient.Client.Receive(buffer, SocketFlags.Peek);
                deviceId = MemoryMarshal.Read<uint>(buffer.AsSpan().Slice(DeviceIdStart, DeviceIdSize));
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

            proxyClient.Connect(route.EndPoint.Address, route.EndPoint.Port);

            //Console.WriteLine($"Established {_clientEndpoint} => {proxyClient.Client.RemoteEndPoint}");

            using var serverStream = proxyClient.GetStream();

            using var remoteStream = _remoteClient.GetStream();
            Task.WaitAll(new[]{
                remoteStream.CopyToAsync(serverStream, token),
                serverStream.CopyToAsync(remoteStream, token) }, token);
        }
        private static void CopyData(NetworkStream nsSource, NetworkStream nsTarget, int sendBufferSize = 256)
        {
            Span<byte> sendBuffer = sendBufferSize < 1024 ? stackalloc byte[sendBufferSize] : new byte[sendBufferSize];
            int read;
            while (nsSource.DataAvailable &&
                (read = nsSource.Read(sendBuffer)) > 0)
            {
                nsTarget.Write(sendBuffer.Slice(0, read));
            }
        }
    }

    public class TcpProxy
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
                server.Start(int.MaxValue);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                Console.WriteLine($"Port is already in use {localEndPoint.Port}");
                return;
            }

            Console.WriteLine($"TCP proxy started {localEndPoint.Port}");

            while (true)
            {
                while (!server.Pending())
                {
                    Thread.Sleep(10);
                }
                try
                {
                    var acceptedClient = await server.AcceptTcpClientAsync();
                    acceptedClient.NoDelay = true;

                    // run client thread detached
                    _ = Task.Factory.StartNew(() =>
                    {
                        using var client = new IncommingTcpClient(DeviceMappings, acceptedClient);
                        try
                        {
                            client.HandleRequest();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Failed to handle client request: " + ex.ToString());
                        }
                    }, TaskCreationOptions.PreferFairness);
                }
                catch (SocketException)
                {
                    Console.WriteLine("Failed to accept connection");
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
                if (_from == "*")
                    return;
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