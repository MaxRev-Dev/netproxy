using System;
using System.Linq;
using System.Net;

#nullable enable

namespace NetProxy.Configuration.Routes
{
    public class RouteMapping
    {
        private string? _from;
        private string? _to;

        public RouteMapping()
        {
            // this ctor mainly for configuration binder
        }

        public RouteMapping(uint port, string from, string to)
        {
            Port = port;
            From = from;
            To = to;
        }


        public bool Matches(uint otherId) => otherId.Equals(FromValue);

        public uint? FromValue { get; private set; }

        public uint? Port { get; set; } = 3000; // default port

        public string? From
        {
            get => _from;
            set
            {
                if (value != null && value != "*")
                    FromValue = uint.Parse(value!);
                _from = value;
            }
        }
        public string? To
        {
            get => _to;
            set
            {
                if (value is null)
                    return;
                var source = value;
                EndPoint = Utils.ResolveIpFromDns(source);
                _to = value;
            }
        }
        public bool IsManyToOne => From == "*";

        public IPEndPoint? EndPoint { get; private set; }
    }
}