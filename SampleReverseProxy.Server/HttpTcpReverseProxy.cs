using System.Net.Sockets;
using System.Net;
using System.Text;

namespace SampleReverseProxy.Server
{
    public class HttpReverseProxy
    {
        private HttpListener _listener;
        private HttpClient _client;
        private string _targetUrl;

        public HttpReverseProxy(int listenPort, string targetUrl)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{listenPort}/");
            _client = new HttpClient();
            _targetUrl = targetUrl;
        }

        public async Task Start()
        {
            _listener.Start();
            while (true)
            {
                var context = await _listener.GetContextAsync();
                await ProcessRequest(context);
            }
        }

        private async Task ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            // Create a new request that will be sent to the target
            var targetRequest = new HttpRequestMessage();
            targetRequest.RequestUri = new Uri(_targetUrl + request.Url.AbsolutePath);
            targetRequest.Method = new HttpMethod(request.HttpMethod);
            foreach (var headerKey in request.Headers.AllKeys)
            {
                targetRequest.Headers.TryAddWithoutValidation(headerKey, request.Headers[headerKey]);
            }

            if (request.HasEntityBody)
            {
                using (var bodyStream = request.InputStream)
                {
                    var bodyReader = new StreamReader(bodyStream, request.ContentEncoding);
                    targetRequest.Content = new StringContent(await bodyReader.ReadToEndAsync(), request.ContentEncoding);
                }
            }

            // Send the request to the target and get the response
            var targetResponse = await _client.SendAsync(targetRequest);

            // Copy the target response to the original response
            response.StatusCode = (int)targetResponse.StatusCode;
            response.StatusDescription = targetResponse.ReasonPhrase;
            foreach (var header in targetResponse.Headers)
            {
                response.Headers[header.Key] = string.Join(", ", header.Value);
            }

            if (targetResponse.Content != null)
            {
                var responseBody = await targetResponse.Content.ReadAsByteArrayAsync();
                await response.OutputStream.WriteAsync(responseBody, 0, responseBody.Length);
            }

            response.Close();
        }

        public void Stop()
        {
            _listener.Stop();
        }
    }
}
