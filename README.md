SignalW
========

Even simpler real-time web for ASP.NET Core.

SignalW is a simplified version of [SignalR](https://github.com/aspnet/SignalR), with only WebSockets as a transport 
and `MemoryStream` as a message type.

* WebSockets work almost everywhere to bother about long polling/SSE/any other transport.
* Since messages could be framed, we cannot use a single buffer and need a stream to collect
all chunks. We use [`RecyclableMemoryStream`](https://github.com/Microsoft/Microsoft.IO.RecyclableMemoryStream)
that pools internals buffers.
* Serialization is out of scope. It's always a pain to abstract it for a general case, but in 
every concrete case it could be as simple as using JSON.NET (with extension methods for streams)
or as flexible as a custom binary encoding.
* Any generic WebSocket client should work.


For authentication with a bearer token:

**from a C# client:**

```
WebSocketClient client = new WebSocketClient();
client.ConfigureRequest = (req) =>
{
    req.Headers.Add("Bearer ", _tokenValue);
};
```

**from RxJS WebSocketSubject:**
```
let wsConfig: WebSocketSubjectConfig = {
    url: 'wss://localhost.dataspreads.com:5001/websockets/',
    protocol: [
    'access_token',
    token
    ]
};
let ws = new WebSocketSubject<any>(wsConfig);
```
then in OWIN pipeline use this trick to populate the correct header
```
app.Use((context, next) => {
    if (!context.Request.Headers.ContainsKey("Authorization")
        && context.Request.Headers.ContainsKey("Upgrade")) {
        if (context.WebSockets.WebSocketRequestedProtocols.Count >= 2) {
            var first = context.WebSockets.WebSocketRequestedProtocols[0];
            var second = context.WebSockets.WebSocketRequestedProtocols[1];
            if (first == "access_token") {
                context.Request.Headers.Add("Authorization", "Bearer " + second);
                context.Response.Headers.Add("Sec-WebSocket-Protocol", "access_token");
            }
        }
    }
    return next();
});
```