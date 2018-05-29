using Microsoft.AspNetCore.Authorization;
using Spreads.SignalW;
using System;
using System.IO;
using System.Threading.Tasks;


public class Echo : Hub
{
    public override async Task OnReceiveAsync(MemoryStream payload)
    {
        await Clients.Client(Context.ConnectionId).InvokeAsync(payload);
    }
}