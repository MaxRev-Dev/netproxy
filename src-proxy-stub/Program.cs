
using System.Net;
using System.Threading.Tasks;

namespace NetProxy.Stub
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            var proxy = new TcpProxy(
                new[] {
                    new RouteMapping
                    {
                        From = "1000-1000000",
                        To = "127.0.0.1:9000"
                    }
                });
            await proxy.Start(new IPEndPoint(IPAddress.Any, 15000));
        }
    }
}