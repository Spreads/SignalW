using Spreads.SignalW;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SiglanWServer
{
    public class MessageHub : Hub
    {
        public override Task OnConnectedAsync()
        {
            Context.Connection.Channel.TryComplete();
            return Task.FromResult(0);
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            return Task.FromResult(0);
        }

        public override async Task OnReceiveAsync(MemoryStream payload)
        {
            await Clients.All.InvokeAsync(payload);
        }
    }
}
