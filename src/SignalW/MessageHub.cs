using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DataSpreads.SignalW
{
    public class MessageHub : Hub
    {
        public override async Task OnReceiveAsync(MemoryStream payload) {
            object message = payload.ReadJsonMessage();
            // dispose as soon as it is no longer used becasue it uses pooled buffers inside
            payload.Dispose();

            // dynamic will dispatch to the correct method
            dynamic dynMessage = message;
            await OnReceiveAsync(dynMessage);
        }

        public virtual async Task OnReceiveAsync(IMessage message) {
            return;
        }
    }
}
