
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;

namespace NetProxy.Stub
{
    class Program
    {
        private static volatile int acceptedClients, failedClients, successfullRequests;
        public static async Task Main(string[] args)
        {
            var port = args.Any() ? int.Parse(args[0]) : 9000;

            var serviceListener = new TcpListener(new IPEndPoint(IPAddress.Any, port));
            serviceListener.Start(int.MaxValue);
            Console.WriteLine($"Service stub started at {port}");
            var tab = new string(' ', 5);
            _ = Task.Run(() =>
            {
                Console.Clear();
                Console.CursorVisible = false;
                while (true)
                {
                    Thread.Sleep(100);
                    Console.SetCursorPosition(0, 0);
                    Console.WriteLine("Accepted clients: " + acceptedClients + tab);
                    Console.WriteLine("Failed connections: " + failedClients + tab);
                    Console.WriteLine("Successfull requests: " + successfullRequests + tab);
                    if (Console.KeyAvailable && Console.ReadKey().Key == ConsoleKey.R)
                    {
                        acceptedClients = failedClients = successfullRequests = 0;
                        Console.Clear();
                    }
                }
            });
            while (true)
            {
                // this won't block the loop
                if (!serviceListener.Pending())
                {
                    Thread.Sleep(10);
                    continue;
                }
                var clientSocket = await serviceListener.AcceptSocketAsync();
                clientSocket.NoDelay = true;
                Interlocked.Increment(ref acceptedClients);
                _ = Task.Run(() => RunClient(clientSocket));
            }

        }

        private static async Task RunClient(Socket clientSocket)
        {

            try
            {
                var cts = new CancellationTokenSource();
                cts.CancelAfter(2000); // cancell after 2 seconds if stuck
                var token = cts.Token;
                using (clientSocket)
                {
                    using var serverStream = new NetworkStream(clientSocket, false);

                    var buffer = new byte[200];
                    var received = await serverStream.ReadAsync(buffer, token);
                    // write server response
                    await serverStream.WriteAsync(Encoding.UTF8.GetBytes("Echo OK"), token);
                    await serverStream.FlushAsync(token);
                    Interlocked.Increment(ref successfullRequests);
                }
            }
            catch (OperationCanceledException)
            {
                Interlocked.Increment(ref failedClients);
                Console.WriteLine("Client cancelled!");
            }
        }
    }
}