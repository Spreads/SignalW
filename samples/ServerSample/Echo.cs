using Microsoft.AspNetCore.Authorization;
using Spreads.SignalW;
using System;
using System.IO;
using System.Threading.Tasks;
using SR = Microsoft.AspNetCore.SignalR;


public class Echo : Hub
{
    public override ValueTask OnReceiveAsync(MemoryStream payload)
    {
        Clients.Client(Context.ConnectionId).SendAsync(payload);
        return new ValueTask();
    }
}