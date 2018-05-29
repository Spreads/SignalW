using System.IO;
using System.Threading.Tasks;
using Spreads.Serialization;

namespace Spreads.SignalW
{
    public class MessageHub : Hub
    {
        public override async ValueTask OnReceiveAsync(MemoryStream payload)
        {
            var message = BinarySerializer.Json.Deserialize<IMessage>(payload);
            // dispose as soon as it is no longer used becasue it uses pooled buffers inside
            payload.Dispose();

            // dynamic will dispatch to the correct method
            //dynamic dynMessage = message;
            await OnReceiveAsync(message);
        }

        public virtual async Task OnReceiveAsync(IMessage message)
        {
            return;
        }
    }
}