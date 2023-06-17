using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SampleReverseProxy
{
    class Program
    {
        static async Task Main(string[] args)
        {
            int localPort = 8000;
            string targetHost = "localhost";
            int targetPort = 3000;

            Console.WriteLine("Reverse Proxy started...");
            Console.WriteLine($"Listening on port {localPort}, forwarding requests to {targetHost}:{targetPort}");

            TcpListener listener = new TcpListener(IPAddress.Loopback, localPort);
            listener.Start();

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                Task.Run(() => ProcessClientRequest(client, targetHost, targetPort));
            }
        }

        static async Task ProcessClientRequest(TcpClient client, string targetHost, int targetPort)
        {
            using (client)
            {
                using (NetworkStream clientStream = client.GetStream())
                {
                    byte[] requestBuffer = new byte[4096];
                    int bytesRead = await clientStream.ReadAsync(requestBuffer, 0, requestBuffer.Length);
                    string request = Encoding.UTF8.GetString(requestBuffer, 0, bytesRead);

                    string modifiedRequest = ModifyRequest(request, targetHost, targetPort);

                    using (TcpClient targetClient = new TcpClient(targetHost, targetPort))
                    using (NetworkStream targetStream = targetClient.GetStream())
                    {
                        byte[] requestBytes = Encoding.UTF8.GetBytes(modifiedRequest);
                        await targetStream.WriteAsync(requestBytes, 0, requestBytes.Length);

                        bool isWebSocketRequest = IsWebSocketRequest(request);
                        if (isWebSocketRequest)
                        {
                            await ForwardWebSocketRequest(clientStream, targetStream);
                        }
                        else
                        {
                            await ForwardHttpRequest(clientStream, targetStream);
                        }
                    }
                }
            }
        }

        static async Task ForwardWebSocketRequest(NetworkStream clientStream, NetworkStream targetStream)
        {
            await Task.WhenAny(
                clientStream.CopyToAsync(targetStream),
                targetStream.CopyToAsync(clientStream)
            );
        }

        static async Task ForwardHttpRequest(NetworkStream clientStream, NetworkStream targetStream)
        {
            byte[] responseBuffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = await targetStream.ReadAsync(responseBuffer, 0, responseBuffer.Length)) > 0)
            {
                await clientStream.WriteAsync(responseBuffer, 0, bytesRead);
            }
        }

        static string ModifyRequest(string request, string targetHost, int targetPort)
        {
            string[] lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);

            string hostLine = lines[1];
            string[] hostParts = hostLine.Split(' ');
            string[] hostAndPort = hostParts[1].Split(':');
            string host = hostAndPort[0];

            StringBuilder modifiedRequest = new StringBuilder();

            foreach (string line in lines)
            {
                if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                {
                    modifiedRequest.AppendLine($"Host: {targetHost}:{targetPort}");
                }
                else if (line.StartsWith("GET") || line.StartsWith("POST") || line.StartsWith("PUT") ||
                         line.StartsWith("DELETE") || line.StartsWith("OPTIONS") || line.StartsWith("HEAD"))
                {
                    string modifiedLine = line.Replace($"http://{host}:{targetPort}", $"http://{targetHost}:{targetPort}");
                    modifiedRequest.AppendLine(modifiedLine);
                }
                else
                {
                    modifiedRequest.AppendLine(line);
                }
            }

            return modifiedRequest.ToString();
        }

        static bool IsWebSocketRequest(string request)
        {
            // Check if the request contains the "Upgrade" header with the value "websocket"
            return request.Contains("Upgrade: websocket");
        }
    }
}