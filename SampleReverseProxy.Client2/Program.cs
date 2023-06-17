using System.Net.Sockets;
using System.Net.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var app = builder.Build();

// Configure the HTTP request pipeline.

app.Use(async (HttpContext context, Func<Task> next) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var clientWebSocket = new ClientWebSocket();
        var targetUri = new Uri("ws://localhost:3000" + context.Request.Path);

        await clientWebSocket.ConnectAsync(targetUri, context.RequestAborted);
        using var serverWebSocket = await context.WebSockets.AcceptWebSocketAsync();

        var buffer = new byte[1024 * 4];
        var serverTask = Redirect(serverWebSocket, clientWebSocket, buffer, context.RequestAborted);
        var clientTask = Redirect(clientWebSocket, serverWebSocket, buffer, context.RequestAborted);

        await Task.WhenAll(serverTask, clientTask);
    }
    else
    {
        // Forward the request to the TCP server
        var client = new TcpClient("localhost", 8001);
        var writer = new StreamWriter(client.GetStream());
        var reader = new StreamReader(client.GetStream());

        // Write the request path and body to the TCP stream
        //var body = new StreamReader(context.Request.Body).ReadToEnd();
        writer.WriteLine(context.Request.Path);
        writer.Flush();

        // Read the response from the TCP server
        var response = reader.ReadToEnd();

        // Write the response to the HTTP response
        context.Response.Clear();
        context.Response.StatusCode = 200; // Or whatever status code you want.
        context.Response.ContentType = "text/html"; // Or whatever content type you want.
        await context.Response.WriteAsync(response);

        // Close the TCP client
        client.Close();
    }
});

app.Run();

async Task Redirect(WebSocket source, WebSocket target, byte[] buffer, CancellationToken cancellationToken)
{
    while (true)
    {
        var result = await source.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            await target.CloseAsync(source.CloseStatus.Value, source.CloseStatusDescription, cancellationToken);
            break;
        }

        await target.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, cancellationToken);
    }
}