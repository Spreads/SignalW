using System;
using System.IO; 
using Spreads.SignalW.Client;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using Spreads.Buffers;
using System.Text;

namespace SignalWClient
{
    class WsClient
    {
        static WsChannel _WsChannel = null; 
       


        public static void Main(string[] args)
        {
            Console.WriteLine("Websocket Client");

            Console.WriteLine("Enter URI of websocket server to connect to");

            while (_WsChannel == null)
            {
                var input = Console.ReadLine();
                bool validURI = Uri.TryCreate(input, UriKind.Absolute, out Uri uri);
                if (validURI == false)
                {
                    Console.WriteLine("Uri you entered isn't valid");
                    continue; 
                }

                var task = Task.Run(async () => await TryConnect(2000, uri));
                task.Wait(); 
                KeyValuePair<bool, WebSocket> connected = task.Result;

                if (connected.Key == false)
                {
                    Console.WriteLine("Connection failed");
                    continue;
                }

                Console.WriteLine("Connected");
                _WsChannel = new WsChannel(connected.Value, Format.Text); 
            }

            Task.Run(async () =>
            {
                while (true)
                {
                    var stream = await _WsChannel.ReadAsync();
                    RecyclableMemoryStream rs = stream as RecyclableMemoryStream;
                    if (stream == null)
                    {
                        break;
                    }
                    byte[] buffer = new byte[rs.Length];
                    rs.Position = 0; 
                    rs.Read(buffer, 0, (int)rs.Length); 
                    var message = Encoding.UTF8.GetString(buffer);
                    Console.WriteLine($"Arrived Message: {message}");
                }
            });

            Console.WriteLine("Enter Messages to send to server. Enter q to Quit");
            while (true)
            {
                string input = Console.ReadLine();
                if (input == "q")
                    break;

                var data = Encoding.UTF8.GetBytes(input);
                MemoryStream stream = RecyclableMemoryStreamManager.Default.GetStream("userInput", data.Length, true);
                stream.Write(data, 0, data.Length);
                var writeTask = Task.Run(async () => await _WsChannel.WriteAsync(stream));
                writeTask.Wait();
                stream.Dispose();
            }

        }

        async static Task<KeyValuePair<bool, WebSocket>> TryConnect(int millisecondTimeout, Uri uri)
        {
            ClientWebSocket webSocket = new ClientWebSocket();
            var cts = new System.Threading.CancellationTokenSource(millisecondTimeout).Token;
            try
            {
                await webSocket.ConnectAsync(uri, cts);
            }
            catch(Exception exception)
            {
                webSocket.Dispose(); 
                return new KeyValuePair<bool, WebSocket>(false, null); 
            }
            return new KeyValuePair<bool, WebSocket>(true, webSocket);
        }
    }
}
