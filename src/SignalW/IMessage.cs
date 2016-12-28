using System;

namespace DataSpreads.SignalW {

    public interface IMessage {
        string Type { get; }
        string Id { get; }
    }

    public class MessageTypeAttribute : Attribute {

        public MessageTypeAttribute(string type) {
            Type = type;
        }

        public string Type { get; }
    }

    [MessageType("ping")]
    public class PingMessage : IMessage {
        public string Type => "ping";
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime DateTimeUtc { get; set; } = DateTime.UtcNow;
    }

    [MessageType("pong")]
    public class PongMessage : IMessage {
        public string Type => "pong";
        public string Id { get; set; }
        public DateTime DateTimeUtc { get; set; } = DateTime.UtcNow;
    }

}
