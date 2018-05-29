using Spreads.SignalW.Client;
using System;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace ClientSample
{
    class Program
    {
        public static void Main()
        {
            Task.Run(Run).Wait();
        }
        public static async Task Run()
        {
            var client = new ClientWebSocket();
            await client.ConnectAsync(new Uri("ws://localhost:5002/api/signalw/echo?connectionId=test"), CancellationToken.None);
            //var header = new AuthenticationHeaderValue("Bearer", _accessToken);
            //client.Options.SetRequestHeader("Authorization", header.ToString());

            var message = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            var arraySegment = new ArraySegment<byte>(message);
            var channel = new WsChannel(client, Format.Binary);
            var stream = Spreads.Buffers.RecyclableMemoryStreamManager.Default.GetStream(null, 8);
            stream.Write(message, 0, message.Length);
            var count = 0;
            var sw = new Stopwatch();
            sw.Start();
            var previous = 0L;
            while (true)
            {
                var t1 = channel.WriteAsync(stream);
                var t2 = channel.WriteAsync(stream);
                var t3 = channel.WriteAsync(stream);
                var t4 = channel.WriteAsync(stream);
                var t5 = channel.WriteAsync(stream);
                await t1;
                await t2;
                await t3;
                await t4;
                await t5;
                var result1 = channel.ReadAsync();
                var result2 = channel.ReadAsync();
                var result3 = channel.ReadAsync();
                var result4 = channel.ReadAsync();
                var result5 = channel.ReadAsync();
                (await result1).Dispose();
                (await result2).Dispose();
                (await result3).Dispose();
                (await result4).Dispose();
                (await result5).Dispose();
                count++;
                count++;
                count++;
                count++;
                count++;
                if (count % 10000 == 0)
                {
                    var elapsed = sw.ElapsedMilliseconds;
                    var delta = elapsed - previous;
                    Console.WriteLine($"{count} - {delta}");
                    previous = elapsed;
                }

            }
            Console.WriteLine("Finished");
            Console.ReadLine();
        }
    }
}
