using NetProxy.Configuration.Routes;
using System;
using System.Linq;
using System.Net;
using System.Text.Json;
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
                var deviceMappings = JsonSerializer.Deserialize<RoutesRepository>(deviceMappingsJson);

                await Task.WhenAll(
                    // start listeners on specified ports
                    // this will filter duplicates
                    deviceMappings.Mappings
                    .Select(x => x.Port).Distinct()
                    .Select(port =>
                          // create a proxy task with endpoint
                          new TcpProxy(deviceMappings)
                            .Start(new IPEndPoint(IPAddress.Any, (int)port))));
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
}