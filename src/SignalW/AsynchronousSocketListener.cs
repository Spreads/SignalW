using System;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace Spreads.SignalW
{
    // State object for reading client data asynchronously
    public class StateObject
    {
        // Client  socket.
        public Socket workSocket = null;

        // Size of receive buffer.
        public const int BufferSize = 1024;

        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];

        // Received data string.
        public StringBuilder sb = new StringBuilder();
    }

    public class AsynchronousSocketListener
    {
        public static bool started = false;
        public AsynchronousSocketListener()
        {
        }

        public static async void StartListening(Action<WebSocket> action)
        {
            if (started) return;
            started = true;

            // Data buffer for incoming data.
            byte[] bytes = new Byte[1024];

            // Establish the local endpoint for the socket.
            // The DNS name of the computer
            // running the listener is "host.contoso.com".
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = IPAddress.Loopback; //  new IPAddress(); ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 5003);

            // Create a TCP/IP socket.
            Socket listener = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                while (true)
                {

                    // Start an asynchronous socket to listen for connections.
                    Console.WriteLine("Waiting for a connection...");

                    var socket = await listener.AcceptAsync();
                    
                    var stream = new NetworkStream(socket);
#if NETCOREAPP2_1
                    var ws = WebSocket.CreateFromStream(stream, true, null, TimeSpan.FromMinutes(5));
                    action(ws);
#endif
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            Console.WriteLine("\nPress ENTER to continue...");
            Console.Read();
        }

        
    }
}
