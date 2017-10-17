using System.IO;
using System.Threading.Tasks;
using Spreads.Serialization;
using System;

namespace Spreads.SignalW
{
    public class MessageHub : Hub
    {
        //public static Gr

        public override Task OnConnectedAsync()
        {
            Groups.AddAsync("Users");
            
            //Context.Connection.Channel.TryComplete(); -- Used to close connection
            return Task.FromResult(0);
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            return Task.FromResult(0);
        }

        public override async Task OnReceiveAsync(MemoryStream payload)
        {
            await Clients.Group("Users").InvokeAsync(payload);
        }
    }
}