using HeyRed.Mime;
using RabbitMQ.Client;
using System.Net.Mime;
using System.Text;

namespace SampleReverseProxy.Server3
{
    public class ProxyMiddleware
    {
        private const string RequestQueueName = "requests";
        private const string ResponseQueueName = "responses";

        private readonly ResponseCompletionSources _responseCompletionSources;

        private readonly RequestDelegate _next;

        public ProxyMiddleware(RequestDelegate next, ResponseCompletionSources responseCompletionSources)
        {
            _next = next;
            _responseCompletionSources = responseCompletionSources;
        }

        public async Task Invoke(HttpContext context)
        {
            var requestID = Guid.NewGuid().ToString();

            // Create a TaskCompletionSource to await the response
            var responseCompletionSource = new TaskCompletionSource<HttpResponseModel>();

            // Store the response completion source in a dictionary or cache
            StoreResponseCompletionSource(requestID, responseCompletionSource);

            // Extract request details
            var requestData = await GetRequestDataAsync(context.Request);

            PublishRequestToRabbitMQ(requestID, requestData);

            // Wait for the response or timeout
            var responseTask = responseCompletionSource.Task;
            if (await Task.WhenAny(responseTask, Task.Delay(TimeSpan.FromSeconds(10))) == responseTask)
            {
                var response = await responseTask;

                var contentType = "image/jpeg";

                if (context.Request.Path != "/")
                {
                    if (response.Bytes != null && response.Bytes.Length > 0)
                    {
                        // Create a memory stream from the byte array
                        var memoryStream = new MemoryStream(response.Bytes);

                        // Set the response headers
                        context.Response.ContentType = response.ContentType;
                        context.Response.ContentLength = memoryStream.Length;

                        // Write the image content to the response stream
                        await memoryStream.CopyToAsync(context.Response.Body);

                        // Close the memory stream
                        memoryStream.Close();
                    }
                }
                else
                {
                    context.Response.ContentType = response.ContentType;
                    await context.Response.WriteAsync(Encoding.UTF8.GetString(response.Bytes));
                }
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            }
        }

        private void PublishRequestToRabbitMQ(string requestID, string requestData)
        {
            var factory = new ConnectionFactory { HostName = "localhost", Port = 5672, UserName = "guest", Password = "guest" };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            var properties = channel.CreateBasicProperties();
            properties.MessageId = requestID;
            properties.ReplyTo = ResponseQueueName;

            channel.BasicPublish(exchange: "", routingKey: RequestQueueName, basicProperties: properties, body: Encoding.UTF8.GetBytes(requestData));
        }

        private void StoreResponseCompletionSource(string requestID, TaskCompletionSource<HttpResponseModel> responseCompletionSource)
        {
            // Store the response completion source in a dictionary or cache based on requestID
            _responseCompletionSources.Sources.TryAdd(requestID, responseCompletionSource);
        }

        private async Task<string> GetRequestDataAsync(HttpRequest request)
        {
            using (var reader = new StreamReader(request.Body, Encoding.UTF8, true, 1024, true))
            {
                var requestBody = await reader.ReadToEndAsync();

                // Reconstruct the full request including headers, query parameters, and path
                var requestBuilder = new StringBuilder();
                requestBuilder.AppendLine($"{request.Method} {request.Path}{request.QueryString} {request.Protocol}");

                foreach (var header in request.Headers)
                {
                    requestBuilder.AppendLine($"{header.Key}: {header.Value}");
                }

                requestBuilder.AppendLine();
                requestBuilder.AppendLine(requestBody);

                return requestBuilder.ToString();
            }
        }

        private bool IsFileType(string contentType)
        {
            string fileExtension = MimeTypesMap.GetExtension(contentType);
            return !string.IsNullOrEmpty(fileExtension);
        }
    }
}