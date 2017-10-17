SignalW
========

Even simpler and faster real-time web for ASP.NET Core.

`Install-Package Spreads.SignalW -Version 0.8.0-build1707052042` 
`Install-Package Spreads.SignalW.Client -Version 0.8.0-build1707052042` 

SignalW is a simplified version of [SignalR](https://github.com/aspnet/SignalR), with only WebSockets as a transport 
and `MemoryStream` as a message type.

* WebSockets work almost everywhere to bother about long polling/SSE/any other transport.
* Since messages could be framed, we cannot use a single buffer and need a stream to collect
all chunks. We use [`RecyclableMemoryStream`](https://github.com/Microsoft/Microsoft.IO.RecyclableMemoryStream)
that pools internal buffers.
* Serialization is out of scope. It is always a pain to abstract it for a general case, but in 
every concrete case it could be as simple as using JSON.NET (with extension methods for streams)
or as flexible as a custom binary encoding.
* Any generic WebSocket client should work. The SignalW.Client project has a WsChannel class that wraps
around the standard WebSocket class and gives methods to work with MemoryStreams instead of ArraySegments.

Instead of multiple methods inside Hubs that clients invoked by name, in SignalW we have a single method 
`async Task OnReceiveAsync(MemoryStream payload)`. If one uses Angular2 with `@ngrx/store` then
a deserialized-to-JSON message will always have a `type` field, and one could use a custom 
[`JsonCreationConverter<IMessage>`](https://github.com/Spreads/Spreads/blob/master/src/Spreads.Core/Serialization/IMessageJsonConverter.cs#L125)
to deserialize a message to its correct .NET type. Then one could write multiple methods with the same 
name that differ only by its parameter type and use `dynamic` keyword to dispatch a message to 
a correct handler.

```C#
public class DispatchHub : Hub {
    public override async Task OnReceiveAsync(MemoryStream payload) {
        // Extension method ReadJsonMessage returns IMessage object instance based on the `type` field in JSON
		object message = payload.ReadJsonMessage();
        // dispose as soon as it is no longer used becasue it uses pooled buffers inside
        payload.Dispose();

        // dynamic will dispatch to the correct method
        dynamic dynMessage = message;
        await OnReceiveAsync(dynMessage);
    }

    public async void OnReceiveAsync(MessageFoo message) {
		var stream = message.WriteJson();
        await Clients.Group("foo").InvokeAsync(stream);
    }

    public async void OnReceiveAsync(MessageBar message) {
		var stream = message.WriteJson();
        await Clients.Group("bar").InvokeAsync(stream);
    }
}
```

On the Angular side, one could simply use a `WebSocketSubject` and forward messages to `@ngrx/store` directly 
if they have a `type` field. No dependencies, no custom deserialization, no hassle!


Authentication with a bearer token
---------------------------------

**From a C# client**

```C#
var client = new ClientWebSocket();
var header = new AuthenticationHeaderValue("Bearer", _accessToken);
client.Options.RequestHeaders.Add("Authorization", header.ToString());
```

**From JavaScript:**

It is impossible to add headers to WebSocket constructor in JavaScript, but we could 
use protocol parameters for this. Here we are using RxJS WebSocketSubject:

```javascript
import { WebSocketSubjectConfig, WebSocketSubject } from 'rxjs/observable/dom/WebSocketSubject';
...
let wsConfig: WebSocketSubjectConfig = {
    url: 'wss://example.com/api/signalw/chat',
    protocol: [
    'access_token',
    token
    ]
};
let ws = new WebSocketSubject<any>(wsConfig);
```
Then in the very beginning of OWIN pipeline (before any identity middleware) 
use this trick to populate the correct header:
```C#
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

To use SignalW, create a custom Hub:
```C#
[Authorize]
public class Chat : Hub {
    public override Task OnConnectedAsync() {
        if (!Context.User.Identity.IsAuthenticated) {
            Context.Connection.Channel.TryComplete();
        }
        return Task.FromResult(0);
    }

    public override Task OnDisconnectedAsync(Exception exception) {
	
        return Task.FromResult(0);
    }

    public override async Task OnReceiveAsync(MemoryStream payload) {
        await Clients.All.InvokeAsync(payload);
    }
}
```

Then add SignalW to the OWIN pipeline and map hubs to a path. Here we use SignalR together 
with MVC on the "/api" path:
```C#
public void ConfigureServices(IServiceCollection services) {
	...
	services.AddSignalW();
	...
}

public void Configure(IApplicationBuilder app, ...){
	...
	app.Map("/api/signalw", signalw => {
		signalw.UseSignalW((config) => {
			config.MapHub<Chat>("chat", Format.Text);
		});
	});
	app.Map("/api", apiApp => {
		apiApp.UseMvc();
	});
	...
}
```

Open several pages of [https://www.websocket.org/echo.html](https://www.websocket.org/echo.html)
and connect to `https://[host]/api/signalw/chat?connectionId=[any value]`. Each page should broadcast
messages to every other page and this is a simple chat.

