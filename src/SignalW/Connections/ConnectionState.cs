using System;

namespace Spreads.SignalW.Connections
{
    public class ConnectionState {
        public Connection Connection { get; set; }

        // These are used for long polling mostly
        public Action Close { get; set; }
        public DateTimeOffset LastSeen { get; set; }
        public bool Active { get; set; } = true;
    }
}
