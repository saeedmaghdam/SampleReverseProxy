using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SampleReverseProxy.Client2
{
    public class Tunnel : ITunnel
    {
        public Tunnel()
        {
            ServicePointManager.UseNagleAlgorithm = false;
            ServicePointManager.Expect100Continue = false;
        }

        public async Task ProcessRequest(HttpContext context)
        {
            using (var client = new TcpClient("localhost", 8001))
            {

                // Forward the request to the TCP server
                using var writer = new StreamWriter(client.GetStream());
                using var reader = new StreamReader(client.GetStream());

                // Write the request path and body to the TCP stream
                writer.WriteLineAsync(context.Request.Path).Wait();
                writer.FlushAsync().Wait();

                // Set the response status code and content type
                context.Response.StatusCode = 200; // Or whatever status code you want.
                context.Response.ContentType = "text/html"; // Or whatever content type you want.

                // Get the response stream from the HTTP response
                var responseStream = context.Response.Body;

                // Read the response from the TCP server and write it directly to the response stream
                var buffer = new char[4096];
                int bytesRead;
                while ((bytesRead = reader.ReadAsync(buffer, 0, buffer.Length).Result) > 0)
                {
                    var bytes = Encoding.UTF8.GetBytes(buffer, 0, bytesRead);
                    responseStream.WriteAsync(bytes, 0, bytes.Length).Wait();
                    responseStream.FlushAsync().Wait();
                }
            }

            //if (await EnsureConnectedAsync())
            //{
            //    // Forward the request to the TCP server
            //    using var writer = new StreamWriter(_tcpClient.GetStream());
            //    using var reader = new StreamReader(_tcpClient.GetStream());

            //    // Write the request path and body to the TCP stream
            //    await writer.WriteLineAsync(context.Request.Path);
            //    await writer.FlushAsync();

            //    // Set the response status code and content type
            //    context.Response.StatusCode = 200; // Or whatever status code you want.
            //    context.Response.ContentType = "text/html"; // Or whatever content type you want.

            //    // Get the response stream from the HTTP response
            //    var responseStream = context.Response.Body;

            //    // Read the response from the TCP server and write it directly to the response stream
            //    var buffer = new char[4096];
            //    int bytesRead;
            //    while ((bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
            //    {
            //        var bytes = Encoding.UTF8.GetBytes(buffer, 0, bytesRead);
            //        await responseStream.WriteAsync(bytes, 0, bytes.Length);
            //        await responseStream.FlushAsync();
            //    }
            //}
            //else
            //{
            //    // Handle the case where the connection could not be established
            //    // You can return an error response or take appropriate action
            //}
        }
    }
}
