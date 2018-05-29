using Spreads.SignalW;
using System;
using System.IO;
using System.Threading.Tasks;


public class Echo : Hub
{
    public override ValueTask OnReceiveAsync(MemoryStream payload)
    {
        Clients.Client(Context.ConnectionId).InvokeAsync(payload);
        return new ValueTask();
    }
}