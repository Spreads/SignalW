using Microsoft.AspNetCore.Authorization;
using Spreads.SignalW;
using System;
using System.IO;
using System.Threading.Tasks;

// [Authorize]
public class Chat : Hub
{
    //public override Task OnConnectedAsync()
    //{
    //    if (!this.Context.User.Identity.IsAuthenticated)
    //    {
    //        this.Context.Connection.Channel.TryComplete();
    //    }
    //    return Task.FromResult(0);
    //}

    public override Task OnDisconnectedAsync(Exception exception)
    {
        return Task.FromResult(0);
    }

    public override async Task OnReceiveAsync(MemoryStream payload)
    {
        await Clients.All.InvokeAsync(payload);
    }
}