using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Net;

namespace SampleReverseProxy.Client
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            TcpListener server = new TcpListener(IPAddress.Parse("127.0.0.1"), 8001);
            server.Start();
            Console.WriteLine("Listening...");

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                Task.Run(() => HandleRequest(client));
            }
        }

        static async void HandleRequest(TcpClient client)
        {
            // Read the request data
            StreamReader reader = new StreamReader(client.GetStream());
            string requestData = reader.ReadToEnd();

            // Send the request data to the HTTP server
            HttpClient httpClient = new HttpClient();
            HttpContent content = new StringContent(requestData);
            HttpResponseMessage response = await httpClient.GetAsync("http://localhost:3000");

            // Read the response data from the HTTP server
            string responseData = await response.Content.ReadAsStringAsync();

            // Close the TCP client
            client.Close();

            // Send the response data to the first server
            TcpClient responseClient = new TcpClient("localhost", 8002);
            StreamWriter writer = new StreamWriter(responseClient.GetStream());
            writer.Write(responseData);
            writer.Flush();

            // Close the response client
            responseClient.Close();
        }

        //protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        //{
        //    var listener = new TcpListener(IPAddress.Loopback, 8001);
        //    listener.Start();

        //    Console.WriteLine("Listening for requests on port 8001...");

        //    while (true)
        //    {
        //        var client = await listener.AcceptTcpClientAsync();

        //        // Handle each client connection in a separate task
        //        _ = Task.Run(async () =>
        //        {
        //            using (var clientStream = client.GetStream())
        //            {
        //                try
        //                {
        //                    if (IsWebSocketRequest(clientStream))
        //                    {
        //                        await HandleWebSocket(clientStream);
        //                    }
        //                    else
        //                    {
        //                        await HandleHttpRequest(clientStream);
        //                    }
        //                }
        //                catch (Exception ex)
        //                {
        //                    Console.WriteLine($"Error: {ex.Message}");
        //                }
        //            }

        //            client.Close();
        //        });
        //    }
        //}

        static async Task HandleHttpRequest(NetworkStream clientStream)
        {
            var request = await ReadRequest(clientStream);

            // Modify the request headers or process as needed

            // Forward the modified request to the React app
            using (var reactClient = new TcpClient())
            {
                await reactClient.ConnectAsync("localhost", 3000);

                using (var reactStream = reactClient.GetStream())
                {
                    await reactStream.WriteAsync(request, 0, request.Length);

                    await Task.WhenAny(
                        clientStream.CopyToAsync(reactStream),
                        reactStream.CopyToAsync(clientStream)
                    );
                }
            }
        }

        static async Task HandleWebSocket(NetworkStream clientStream)
        {
            // Establish connection with the React app's WebSocket endpoint
            using (var reactClient = new TcpClient())
            {
                await reactClient.ConnectAsync("localhost", 3000);

                using (var reactStream = reactClient.GetStream())
                {
                    // Forward the WebSocket handshake request to the React app
                    var request = await ReadRequest(clientStream);
                    await reactStream.WriteAsync(request, 0, request.Length);

                    // Start bidirectional data transfer for WebSocket communication
                    var cts = new CancellationTokenSource();
                    var transferTask = Task.WhenAny(
                        clientStream.CopyToAsync(reactStream),
                        reactStream.CopyToAsync(clientStream)
                    );

                    // Continuously monitor the WebSocket connection
                    while (!transferTask.IsCompleted)
                    {
                        var buffer = new byte[1024];
                        var bytesRead = await clientStream.ReadAsync(buffer, 0, buffer.Length);
                        await reactStream.WriteAsync(buffer, 0, bytesRead);
                    }

                    // Cancel the transfer task when WebSocket connection is closed
                    cts.Cancel();
                }
            }
        }

        static bool IsWebSocketRequest(NetworkStream clientStream)
        {
            // Implement WebSocket handshake detection logic based on the HTTP headers
            // Return true if it's a WebSocket upgrade request, false otherwise
            // You can use your own logic to determine if the request is a WebSocket request
            // For example, check for the presence of "Upgrade: websocket" header

            // For simplicity, we'll assume it's a WebSocket request
            return true;
        }

        static async Task<byte[]> ReadRequest(NetworkStream stream)
        {
            const int bufferSize = 4096;
            var buffer = new byte[bufferSize];
            var requestBuilder = new StringBuilder();
            var bytesRead = 0;

            // Read the request headers from the network stream
            do
            {
                bytesRead = await stream.ReadAsync(buffer, 0, bufferSize);
                requestBuilder.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

                // Continue reading until the end of headers marker is found
            } while (!requestBuilder.ToString().Contains("\r\n\r\n"));

            return Encoding.ASCII.GetBytes(requestBuilder.ToString());
        }

        ////////////////////////////////////////////

        //protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        //{
        //    TcpClient client = new TcpClient("localhost", 8080);
        //    SslStream ssl = new SslStream(client.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);

        //    string targetHost = "localhost";
        //    int targetPort = 3000;

        //    try
        //    {
        //        var clientCertificate = new X509Certificate2("certificate.pfx", "Sample@ReverSePr0xy");
        //        ssl.AuthenticateAsClient("localhost", new X509CertificateCollection { clientCertificate },
        //                                 SslProtocols.Tls12, checkCertificateRevocation: false);

        //        while (true)
        //        {
        //            // Read a message from the server.
        //            byte[] buffer = new byte[4096];
        //            int bytes = ssl.Read(buffer, 0, buffer.Length);
        //            var request = Encoding.UTF8.GetString(buffer, 0, bytes);
        //            _logger.LogInformation($"Server says: {request}");

        //            await ProcessClientRequest(request, ssl, targetHost, targetPort);

        //            // Send a message to the server.
        //            //byte[] message = Encoding.UTF8.GetBytes("Hello");
        //            //ssl.Write(message);
        //        }
        //    }
        //    catch (AuthenticationException e)
        //    {
        //        _logger.LogError("Exception: {0}", e.Message);
        //        if (e.InnerException != null)
        //        {
        //            _logger.LogError("Inner exception: {0}", e.InnerException.Message);
        //        }
        //    }
        //    finally
        //    {
        //        ssl.Close();
        //        client.Close();
        //    }
        //}

        //// The following method is invoked by the RemoteCertificateValidationDelegate.
        //public bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        //{
        //    if (sslPolicyErrors == SslPolicyErrors.None)
        //        return true;

        //    _logger.LogError("Certificate error: {0}", sslPolicyErrors);

        //    // Allow this client to communicate with servers that present self        signed certificates.
        //    if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors)
        //    {
        //        // If all errors are related to the root being unknown,
        //        // consider the certificate valid.
        //        foreach (X509ChainStatus chainStatus in chain.ChainStatus)
        //        {
        //            if (chainStatus.Status != X509ChainStatusFlags.UntrustedRoot)
        //            {
        //                return false;
        //            }
        //        }
        //        return true;
        //    }

        //    // In all other cases, do not trust the server's certificate.
        //    return false;
        //}

        //static async Task ProcessClientRequest(TcpClient client, string targetHost, int targetPort)
        //{
        //    using (client)
        //    {
        //        using (NetworkStream clientStream = client.GetStream())
        //        {
        //            byte[] requestBuffer = new byte[4096];
        //            int bytesRead = await clientStream.ReadAsync(requestBuffer, 0, requestBuffer.Length);
        //            string request = Encoding.UTF8.GetString(requestBuffer, 0, bytesRead);

        //            string modifiedRequest = ModifyRequest(request, targetHost, targetPort);

        //            using (TcpClient targetClient = new TcpClient(targetHost, targetPort))
        //            using (NetworkStream targetStream = targetClient.GetStream())
        //            {
        //                byte[] requestBytes = Encoding.UTF8.GetBytes(modifiedRequest);
        //                await targetStream.WriteAsync(requestBytes, 0, requestBytes.Length);

        //                bool isWebSocketRequest = IsWebSocketRequest(request);
        //                if (isWebSocketRequest)
        //                {
        //                    await ForwardWebSocketRequest(clientStream, targetStream);
        //                }
        //                else
        //                {
        //                    await ForwardHttpRequest(clientStream, targetStream);
        //                }
        //            }
        //        }
        //    }
        //}

        //static async Task ProcessClientRequest(string request, SslStream ssl, string targetHost, int targetPort)
        //{
        //    try
        //    {
        //        string modifiedRequest = ModifyRequest(request, targetHost, targetPort);

        //        using (TcpClient targetClient = new TcpClient(targetHost, targetPort))
        //        using (NetworkStream targetStream = targetClient.GetStream())
        //        {
        //            byte[] requestBytes = Encoding.UTF8.GetBytes(modifiedRequest);
        //            await targetStream.WriteAsync(requestBytes, 0, requestBytes.Length);

        //            bool isWebSocketRequest = IsWebSocketRequest(request);
        //            if (isWebSocketRequest)
        //            {
        //                await ForwardWebSocketRequest(ssl, targetStream);
        //            }
        //            else
        //            {
        //                await ForwardHttpRequest(ssl, targetStream);
        //            }
        //        }
        //    }
        //    catch (AuthenticationException ex)
        //    {
        //        Console.WriteLine($"SSL authentication error: {ex.Message}");
        //    }
        //}

        //static async Task ForwardWebSocketRequest(SslStream clientStream, SslStream targetStream)
        //{
        //    await Task.WhenAny(
        //        clientStream.CopyToAsync(targetStream),
        //        targetStream.CopyToAsync(clientStream)
        //    );
        //}

        //static async Task ForwardHttpRequest(SslStream clientStream, SslStream targetStream)
        //{
        //    byte[] responseBuffer = new byte[4096];
        //    int bytesRead;
        //    while ((bytesRead = await targetStream.ReadAsync(responseBuffer, 0, responseBuffer.Length)) > 0)
        //    {
        //        await clientStream.WriteAsync(responseBuffer, 0, bytesRead);
        //    }
        //}

        //static async Task ForwardWebSocketRequest(SslStream ssl, NetworkStream targetStream)
        //{
        //    await Task.WhenAny(
        //        ssl.CopyToAsync(targetStream),
        //        targetStream.CopyToAsync(ssl)
        //    );
        //}

        //static async Task ForwardHttpRequest(SslStream ssl, NetworkStream targetStream)
        //{
        //    byte[] responseBuffer = new byte[4096];
        //    int bytesRead;
        //    while ((bytesRead = await targetStream.ReadAsync(responseBuffer, 0, responseBuffer.Length)) > 0)
        //    {
        //        await ssl.WriteAsync(responseBuffer, 0, bytesRead);
        //    }
        //}

        //static async Task ForwardWebSocketRequest(NetworkStream clientStream, NetworkStream targetStream)
        //{
        //    await Task.WhenAny(
        //        clientStream.CopyToAsync(targetStream),
        //        targetStream.CopyToAsync(clientStream)
        //    );
        //}

        //static async Task ForwardHttpRequest(NetworkStream clientStream, NetworkStream targetStream)
        //{
        //    byte[] responseBuffer = new byte[4096];
        //    int bytesRead;
        //    while ((bytesRead = await targetStream.ReadAsync(responseBuffer, 0, responseBuffer.Length)) > 0)
        //    {
        //        await clientStream.WriteAsync(responseBuffer, 0, bytesRead);
        //    }
        //}

        //static string ModifyRequest(string request, string targetHost, int targetPort)
        //{
        //    string[] lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);

        //    string hostLine = lines[1];
        //    string[] hostParts = hostLine.Split(' ');
        //    string[] hostAndPort = hostParts[1].Split(':');
        //    string host = hostAndPort[0];

        //    StringBuilder modifiedRequest = new StringBuilder();

        //    foreach (string line in lines)
        //    {
        //        if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
        //        {
        //            modifiedRequest.AppendLine($"Host: {targetHost}:{targetPort}");
        //        }
        //        else if (line.StartsWith("GET") || line.StartsWith("POST") || line.StartsWith("PUT") ||
        //                 line.StartsWith("DELETE") || line.StartsWith("OPTIONS") || line.StartsWith("HEAD"))
        //        {
        //            string modifiedLine = line.Replace($"http://{host}:{targetPort}", $"http://{targetHost}:{targetPort}");
        //            modifiedRequest.AppendLine(modifiedLine);
        //        }
        //        else
        //        {
        //            modifiedRequest.AppendLine(line);
        //        }
        //    }

        //    return modifiedRequest.ToString();
        //}

        //static bool IsWebSocketRequest(string request)
        //{
        //    // Check if the request contains the "Upgrade" header with the value "websocket"
        //    return request.Contains("Upgrade: websocket");
        //}
    }
}