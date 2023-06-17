using System.Net.Sockets;
using System.Net;
using System.Text;

namespace SampleReverseProxy.Client
{
    public class TcpHttpReverseProxy
    {
        private TcpListener _listener;
        private HttpClient _httpClient;
        private const string TargetHost = "http://localhost:3000/";

        public TcpHttpReverseProxy()
        {
            _listener = new TcpListener(IPAddress.Any, 8001);
            _httpClient = new HttpClient();
        }

        public async Task Start()
        {
            _listener.Start();
            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync();
                ProcessRequest(client);
            }
        }

        private async void ProcessRequest(TcpClient client)
        {
            using (var networkStream = client.GetStream())
            using (var reader = new StreamReader(networkStream, Encoding.UTF8, true, 1024, true))
            using (var writer = new StreamWriter(networkStream, Encoding.UTF8, 1024, true))
            {
                // Read the request from the TCP stream
                var requestLine = await reader.ReadLineAsync();
                var parts = requestLine.Split(new[] { ' ' }, 3);
                var method = parts[0];
                var uri = new Uri(TargetHost + parts[1]);
                var version = parts[2].Substring(5);

                // Send the request to the React app
                var request = new HttpRequestMessage(new HttpMethod(method), uri);
                while (!string.IsNullOrEmpty(requestLine = await reader.ReadLineAsync()))
                {
                    parts = requestLine.Split(new[] { ':' }, 2);
                    request.Headers.TryAddWithoutValidation(parts[0], parts[1].Trim());
                }
                var response = await _httpClient.SendAsync(request);

                // Send the response back over the TCP stream
                await writer.WriteLineAsync($"HTTP/{version} {(int)response.StatusCode} {response.ReasonPhrase}");
                foreach (var header in response.Headers)
                {
                    await writer.WriteLineAsync($"{header.Key}: {string.Join(", ", header.Value)}");
                }

                await writer.WriteLineAsync();
                await writer.FlushAsync();

                if (response.Content != null)
                {
                    await response.Content.CopyToAsync(writer.BaseStream);
                    await writer.BaseStream.FlushAsync();
                }
            }
        }

        public void Stop()
        {
            _listener.Stop();
        }
    }
}
