using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var app = builder.Build();

// Configure the HTTP request pipeline.

app.Use(async (HttpContext context, Func<Task> next) =>
{
    //// Forward the request to the TCP server
    //var client = new TcpClient("localhost", 8001);
    //var writer = new StreamWriter(client.GetStream());
    //var reader = new StreamReader(client.GetStream());

    //// Write the request path and body to the TCP stream
    ////var body = new StreamReader(context.Request.Body).ReadToEnd();
    //writer.WriteLine(context.Request.Path);
    //writer.Flush();

    //// Read the response from the TCP server
    //var response = reader.ReadToEnd();

    //// Write the response to the HTTP response
    //context.Response.Clear();
    //context.Response.StatusCode = 200; // Or whatever status code you want.
    //context.Response.ContentType = "text/html"; // Or whatever content type you want.
    //await context.Response.WriteAsync(response);

    //// Close the TCP client
    //client.Close();



    // Forward the request to the TCP server
    using var client = new TcpClient("localhost", 8001);
    using var writer = new StreamWriter(client.GetStream());
    using var reader = new StreamReader(client.GetStream());

    // Write the request path and body to the TCP stream
    await writer.WriteLineAsync(context.Request.Path);
    await writer.FlushAsync();

    // Set the response status code and content type
    context.Response.StatusCode = 200; // Or whatever status code you want.
    context.Response.ContentType = "text/html"; // Or whatever content type you want.

    // Get the response stream from the HTTP response
    var responseStream = context.Response.Body;

    // Read the response from the TCP server and write it directly to the response stream
    var buffer = new char[4096];
    int bytesRead;
    while ((bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
    {
        var bytes = Encoding.UTF8.GetBytes(buffer, 0, bytesRead);
        await responseStream.WriteAsync(bytes, 0, bytes.Length);
        await responseStream.FlushAsync();
    }

    // Close the TCP client
    client.Close();
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