using System;
using System.Linq;
using System.Net;

namespace NetProxy.Configuration.Routes
{
    public class Utils
    {
        /// <summary>
        /// Resolves IP endpoint from domin name servers
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        /// <exception cref="FormatException"></exception>
        public static IPEndPoint ResloveDns(string source)
        {
            if (!source.Contains(':'))
                source = source + ":80";

            var partials = source.Split(':');
            if (partials.Length != 2)
                throw new FormatException("Invalid endpoint format");
            if (!int.TryParse(partials[1], out var port))
                throw new FormatException("Invalid port");
            var address = Dns.GetHostAddresses(partials[0]).FirstOrDefault();
            if (address is null)
                throw new FormatException("Dns record of address not found");
            return new IPEndPoint(address, port);
        }
    }
}