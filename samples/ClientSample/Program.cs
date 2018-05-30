using Spreads.SignalW.Client;
using System;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Runtime;
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
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            var client = new ClientWebSocket();
            await client.ConnectAsync(new Uri("ws://localhost:5002/api/signalw/echo?connectionId=test"), CancellationToken.None);

            //var client = new ClientWebSocket();
            //await client.ConnectAsync(new Uri("ws://127.0.0.1:5003"), CancellationToken.None);

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
                var t6 = channel.WriteAsync(stream);
                var t7 = channel.WriteAsync(stream);
                var t8 = channel.WriteAsync(stream);
                var t9 = channel.WriteAsync(stream);
                var t10 = channel.WriteAsync(stream);
                //var t11 = channel.WriteAsync(stream);
                //var t12 = channel.WriteAsync(stream);
                //var t13 = channel.WriteAsync(stream);
                //var t14 = channel.WriteAsync(stream);
                //var t15 = channel.WriteAsync(stream);
                await t1;
                await t2;
                await t3;
                await t4;
                await t5;
                await t6;
                await t7;
                await t8;
                await t9;
                await t10;
                //await t11;
                //await t12;
                //await t13;
                //await t14;
                //await t15;
                var result1 = channel.ReadAsync();
                var result2 = channel.ReadAsync();
                var result3 = channel.ReadAsync();
                var result4 = channel.ReadAsync();
                var result5 = channel.ReadAsync();
                var result6 = channel.ReadAsync();
                var result7 = channel.ReadAsync();
                var result8 = channel.ReadAsync();
                var result9 = channel.ReadAsync();
                var result10 = channel.ReadAsync();
                //var result11 = channel.ReadAsync();
                //var result12 = channel.ReadAsync();
                //var result13 = channel.ReadAsync();
                //var result14 = channel.ReadAsync();
                //var result15 = channel.ReadAsync();
                (await result1).Dispose();
                (await result2).Dispose();
                (await result3).Dispose();
                (await result4).Dispose();
                (await result5).Dispose();
                (await result6).Dispose();
                (await result7).Dispose();
                (await result8).Dispose();
                (await result9).Dispose();
                (await result10).Dispose();
                //(await result11).Dispose();
                //(await result12).Dispose();
                //(await result13).Dispose();
                //(await result14).Dispose();
                //(await result15).Dispose();
                count++;
                count++;
                count++;
                count++;
                count++;
                count++;
                count++;
                count++;
                count++;
                count++;
                //count++;
                //count++;
                //count++;
                //count++;
                //count++;
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
