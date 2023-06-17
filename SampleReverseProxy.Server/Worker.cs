using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Net.WebSockets;
using System.Diagnostics;
using System.Globalization;

namespace SampleReverseProxy.Server
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ITunnel _tunnel;
        private readonly IListener _listener;

        private const string TargetHost = "localhost";
        private const int TargetPort = 8000;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8800/");
            listener.Start();
            Console.WriteLine("Listening...");

            while (true)
            {
                HttpListenerContext context = listener.GetContext();
                Task.Run(() => HandleRequest(context));
            }
        }

        static async void HandleRequest(HttpListenerContext context)
        {
            // Read the request data
            Stream inputStream = context.Request.InputStream;
            StreamReader reader = new StreamReader(inputStream);
            string requestData = reader.ReadToEnd();

            // Send the request data to the TCP server
            TcpClient client = new TcpClient("localhost", 8001);
            StreamWriter writer = new StreamWriter(client.GetStream());
            writer.Write(requestData);
            writer.Flush();

            // Close the client connection
            client.Close();

            // Wait for the response from the second server
            TcpListener responseListener = new TcpListener(IPAddress.Parse("127.0.0.1"), 8002);
            responseListener.Start();
            TcpClient responseClient = responseListener.AcceptTcpClient();

            // Read the response data from the TCP server
            StreamReader responseReader = new StreamReader(responseClient.GetStream());
            string responseData = await responseReader.ReadToEndAsync();

            // Send a response to the original client
            context.Response.StatusCode = 200;
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseData);
            context.Response.ContentLength64 = buffer.Length;
            Stream output = context.Response.OutputStream;
            await output.WriteAsync(buffer, 0, buffer.Length);
            output.Close();

            // Close the TCP client
            responseClient.Close();
            responseListener.Stop();
        }

        //public Worker(ILogger<Worker> logger, ITunnel tunnel, IListener listener)
        //{
        //    _logger = logger;
        //    _tunnel = tunnel;
        //    _listener = listener;
        //}

        ////////////////////////////////////////

        //protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        //{
        //    var listener = new HttpListener();
        //    listener.Prefixes.Add("https://localhost:8000/");

        //    // Load the SSL certificate from the file
        //    var certificate = new X509Certificate2("certificate.pfx", "Sample@ReverSePr0xy");

        //    // Get the thumbprint of the certificate
        //    string thumbprint = certificate.GetCertHashString();

        //    // Generate a new GUID for the appid
        //    Guid appId = Guid.NewGuid();

        //    // Register the certificate with HTTP.sys using netsh
        //    string arguments = $"http add sslcert ipport=0.0.0.0:8000 certhash={thumbprint} appid={{{appId}}}";

        //    using var process = new Process
        //    {
        //        StartInfo = new ProcessStartInfo
        //        {
        //            FileName = "netsh",
        //            Arguments = arguments,
        //            RedirectStandardOutput = true,
        //            RedirectStandardError = true,
        //            UseShellExecute = false,
        //            CreateNoWindow = true,
        //        }
        //    };

        //    process.Start();
        //    process.WaitForExit();

        //    // Configure HttpListener to use HTTPS and the certificate
        //    listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
        //    listener.Start();

        //    Console.WriteLine("Listening for requests on https://localhost:8000...");

        //    while (true)
        //    {
        //        var context = await listener.GetContextAsync();

        //        if (context.Request.IsWebSocketRequest)
        //        {
        //            await HandleWebSocket(context);
        //        }
        //        else
        //        {
        //            var response = await ForwardRequest(context.Request);
        //            await SendResponse(context.Response, response);
        //        }
        //    }
        //}

        //static async Task HandleWebSocket(HttpListenerContext context)
        //{
        //    var webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null);

        //    using (var webSocket = webSocketContext.WebSocket)
        //    {
        //        var client = new TcpClient();
        //        await client.ConnectAsync("localhost", 8001);

        //        using (var networkStream = client.GetStream())
        //        {
        //            var webSocketReceiveBuffer = new byte[1024];
        //            var networkStreamReceiveBuffer = new byte[1024];

        //            // Forward messages from WebSocket to the other service
        //            var webSocketReceiveTask = Task.Run(async () =>
        //            {
        //                try
        //                {
        //                    while (webSocket.State == WebSocketState.Open)
        //                    {
        //                        var receiveResult = await webSocket.ReceiveAsync(
        //                            new ArraySegment<byte>(webSocketReceiveBuffer),
        //                            CancellationToken.None
        //                        );

        //                        if (receiveResult.MessageType == WebSocketMessageType.Close)
        //                        {
        //                            await webSocket.CloseAsync(
        //                                WebSocketCloseStatus.NormalClosure,
        //                                string.Empty,
        //                                CancellationToken.None
        //                            );
        //                            break;
        //                        }

        //                        await networkStream.WriteAsync(
        //                            webSocketReceiveBuffer,
        //                            0,
        //                            receiveResult.Count
        //                        );
        //                    }
        //                }
        //                catch (WebSocketException)
        //                {
        //                    // WebSocket connection closed
        //                }
        //            });

        //            // Forward messages from the other service to WebSocket
        //            var networkStreamReceiveTask = Task.Run(async () =>
        //            {
        //                try
        //                {
        //                    while (client.Connected && webSocket.State == WebSocketState.Open)
        //                    {
        //                        var bytesRead = await networkStream.ReadAsync(
        //                            networkStreamReceiveBuffer,
        //                            0,
        //                            networkStreamReceiveBuffer.Length
        //                        );

        //                        if (bytesRead == 0)
        //                        {
        //                            break;
        //                        }

        //                        await webSocket.SendAsync(
        //                            new ArraySegment<byte>(networkStreamReceiveBuffer, 0, bytesRead),
        //                            WebSocketMessageType.Binary,
        //                            endOfMessage: true,
        //                            CancellationToken.None
        //                        );
        //                    }
        //                }
        //                catch (IOException)
        //                {
        //                    // Network stream closed
        //                }
        //            });

        //            await Task.WhenAny(webSocketReceiveTask, networkStreamReceiveTask);

        //            client.Close();
        //        }
        //    }
        //}

        //static async Task<HttpResponseMessage> ForwardRequest(HttpListenerRequest request)
        //{
        //    using (var httpClient = new HttpClient())
        //    {
        //        var targetUrl = $"http://localhost:8001{request.RawUrl}";

        //        // Create a new HttpRequestMessage with the same method and content as the original request
        //        var requestMessage = new HttpRequestMessage(new HttpMethod(request.HttpMethod), targetUrl)
        //        {
        //            Content = new StreamContent(request.InputStream)
        //        };

        //        // Copy the headers from the original request to the new request
        //        foreach (string headerName in request.Headers)
        //        {
        //            requestMessage.Headers.TryAddWithoutValidation(headerName, request.Headers[headerName]);
        //        }

        //        // Send the forwarded request and get the response
        //        var response = await httpClient.SendAsync(requestMessage);

        //        // Copy the response headers and status code
        //        var responseMessage = new HttpResponseMessage(response.StatusCode);
        //        foreach (var header in response.Headers)
        //        {
        //            responseMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
        //        }

        //        // Copy the response content
        //        responseMessage.Content = new StreamContent(await response.Content.ReadAsStreamAsync());

        //        return responseMessage;
        //    }
        //}

        //static async Task SendResponse(HttpListenerResponse response, HttpResponseMessage httpResponse)
        //{
        //    // Copy the response status code and headers
        //    response.StatusCode = (int)httpResponse.StatusCode;
        //    foreach (var header in httpResponse.Headers)
        //    {
        //        response.Headers.Add(header.Key, string.Join(",", header.Value));
        //    }

        //    // Copy the response content
        //    var contentStream = await httpResponse.Content.ReadAsStreamAsync();
        //    response.ContentLength64 = contentStream.Length;

        //    using (var responseStream = response.OutputStream)
        //    {
        //        await contentStream.CopyToAsync(responseStream);
        //    }

        //    response.OutputStream.Close();
        //}

        ////////////////////////////////

        //protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        //{
        //    var listener = new TcpListener(IPAddress.Loopback, 8000);
        //    listener.Start();

        //    Console.WriteLine("Listening for requests on port 8000...");

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

        //static async Task HandleHttpRequest(NetworkStream clientStream)
        //{
        //    var request = await ReadRequest(clientStream);

        //    // Modify the request headers or process as needed

        //    // Forward the modified request to the other service on port 8001
        //    using (var otherServiceClient = new TcpClient())
        //    {
        //        await otherServiceClient.ConnectAsync("localhost", 8001);

        //        using (var otherServiceStream = otherServiceClient.GetStream())
        //        {
        //            await otherServiceStream.WriteAsync(request, 0, request.Length);

        //            await Task.WhenAny(
        //                clientStream.CopyToAsync(otherServiceStream),
        //                otherServiceStream.CopyToAsync(clientStream)
        //            );
        //        }
        //    }
        //}

        //static async Task HandleWebSocket(NetworkStream clientStream)
        //{
        //    // Establish connection with the other service's WebSocket endpoint on port 8001
        //    using (var otherServiceClient = new TcpClient())
        //    {
        //        await otherServiceClient.ConnectAsync("localhost", 8001);

        //        using (var otherServiceStream = otherServiceClient.GetStream())
        //        {
        //            // Forward the WebSocket handshake request to the other service
        //            var request = await ReadRequest(clientStream);
        //            await otherServiceStream.WriteAsync(request, 0, request.Length);

        //            // Start bidirectional data transfer for WebSocket communication
        //            var transferTask = Task.WhenAny(
        //                clientStream.CopyToAsync(otherServiceStream),
        //                otherServiceStream.CopyToAsync(clientStream)
        //            );

        //            await transferTask;
        //        }
        //    }
        //}

        //static bool IsWebSocketRequest(NetworkStream clientStream)
        //{
        //    // Implement WebSocket handshake detection logic based on the HTTP headers
        //    // Return true if it's a WebSocket upgrade request, false otherwise
        //    // You can use your own logic to determine if the request is a WebSocket request
        //    // For example, check for the presence of "Upgrade: websocket" header

        //    // For simplicity, we'll assume it's a WebSocket request
        //    return true;
        //}

        //static async Task<byte[]> ReadRequest(NetworkStream stream)
        //{
        //    const int bufferSize = 4096;
        //    var buffer = new byte[bufferSize];
        //    var requestBuilder = new StringBuilder();
        //    var bytesRead = 0;

        //    // Read the request headers from the network stream
        //    do
        //    {
        //        bytesRead = await stream.ReadAsync(buffer, 0, bufferSize);
        //        requestBuilder.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

        //        // Continue reading until the end of headers marker is found
        //    } while (!requestBuilder.ToString().Contains("\r\n\r\n"));

        //    return Encoding.ASCII.GetBytes(requestBuilder.ToString());
        //}

        //////////////////////////////////

        //protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        //{
        //    do
        //    {
        //        var request = _listener.Read();
        //        _tunnel.Write(request);
        //        var forwardResponse = _tunnel.Read();
        //        _listener.Write(forwardResponse);
        //    } while (true);
        //}
    }
}
