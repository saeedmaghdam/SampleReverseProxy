using HeyRed.Mime;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SampleReverseProxy.Client3
{
    public class Worker : BackgroundService
    {
        private const string RequestQueueName = "requests";
        private const string ResponseQueueName = "responses";
        private static readonly Uri TargetApplicationUri = new Uri("https://openai.com/"); // Specify the URL of the target application

        private IModel _channel;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Set up RabbitMQ connection and channels
            var factory = new ConnectionFactory { HostName = "localhost", Port = 5672, UserName = "guest", Password = "guest" };
            var connection = factory.CreateConnection();
            _channel = connection.CreateModel();

            // Start consuming requests
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var requestID = ea.BasicProperties.MessageId;
                var requestData = Encoding.UTF8.GetString(ea.Body.ToArray());

                // Extract the target request details from the request data
                var targetRequestDetails = GetTargetRequestDetails(requestData);

                // Forward the request to the target application
                var response = await ForwardRequestToTargetApplication(targetRequestDetails);

                // Publish the response to RabbitMQ
                var responseProperties = _channel.CreateBasicProperties();
                responseProperties.MessageId = requestID;

                _channel.BasicPublish(exchange: "", routingKey: ResponseQueueName, basicProperties: responseProperties, body: response);

                _channel.BasicAck(ea.DeliveryTag, false);
            };

            _channel.BasicConsume(queue: RequestQueueName, autoAck: false, consumer: consumer);

            // Wait for the cancellation token to be triggered
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private IDictionary<string, string> GetTargetRequestDetails(string requestData)
        {
            var requestLines = requestData.Split("\n");
            if (requestLines.Length > 0)
            {
                var targetRequestDetails = new Dictionary<string, string>();

                // Parse the request data and extract method, headers, and path
                var firstLine = requestLines[0].Trim();
                var parts = firstLine.Split(" ");
                if (parts.Length >= 3)
                {
                    targetRequestDetails["Method"] = parts[0].Trim();
                    targetRequestDetails["Path"] = parts[1].Trim();
                    targetRequestDetails["HttpVersion"] = parts[2].Trim();
                }

                for (int i = 1; i < requestLines.Length; i++)
                {
                    var line = requestLines[i].Trim();
                    if (line.StartsWith(":"))
                        continue;
                    if (string.IsNullOrEmpty(line))
                    {
                        break;
                    }

                    var headerParts = line.Split(":");
                    if (headerParts.Length >= 2)
                    {
                        var headerKey = headerParts[0].Trim();
                        var headerValue = string.Join(":", headerParts.Skip(1)).Trim();
                        targetRequestDetails[headerKey] = headerValue;
                    }
                }

                return targetRequestDetails;
            }

            throw new ArgumentException("Invalid request data.");
        }

        private async Task<byte[]> ForwardRequestToTargetApplication(IDictionary<string, string> targetRequestDetails)
        {
            var targetRequestUri = new Uri(TargetApplicationUri, targetRequestDetails["Path"]);

            var handler = new HttpClientHandler();
            handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12; // Adjust the SSL/TLS protocol version as needed

            using var httpClient = new HttpClient(handler);

            var targetRequestMessage = new HttpRequestMessage()
            {
                Method = new HttpMethod(targetRequestDetails["Method"]),
                RequestUri = targetRequestUri,
            };

            // Set headers from target request details
            targetRequestDetails.Remove("Method");
            targetRequestDetails.Remove("Path");

            foreach (var kvp in targetRequestDetails)
            {
                if (kvp.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    targetRequestMessage.Headers.Host = TargetApplicationUri.Authority;
                }
                else
                {
                    targetRequestMessage.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                }
            }

            HttpResponseMessage response = null;

            do
            {
                response = await httpClient.SendAsync(targetRequestMessage);

                if (response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.MovedPermanently)
                {
                    var redirectUrl = response.Headers.Location;
                    targetRequestMessage.RequestUri = redirectUrl;
                    targetRequestMessage.Method = HttpMethod.Get; // Follow the redirect with a GET request
                }
            } while (response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.MovedPermanently);

            // Read the response content as bytes
            byte[] contentBytes = await response.Content.ReadAsByteArrayAsync();
            var responseContent = default(string);

            string contentType = response.Content.Headers.ContentType.MediaType;

            if (contentType != "text/html" && IsFileType(contentType))
                return contentBytes;
            
            if (response.Content.Headers.ContentEncoding.Contains("gzip"))
            {
                using (var stream = new System.IO.Compression.GZipStream(new MemoryStream(contentBytes), CompressionMode.Decompress))
                {
                    // Read the decompressed content
                    byte[] decompressedBytes = new byte[contentBytes.Length * 2]; // Adjust the buffer size if needed
                    int bytesRead = await stream.ReadAsync(decompressedBytes, 0, decompressedBytes.Length);

                    // Decode the bytes using the appropriate encoding
                    responseContent = Encoding.UTF8.GetString(decompressedBytes, 0, bytesRead); // Adjust the encoding if needed
                }
            }
            else if (response.Content.Headers.ContentEncoding.Contains("br"))
            {
                using (MemoryStream stream = new MemoryStream(contentBytes))
                {
                    // Decompress the content using Brotli.NET library
                    BrotliStream brotliStream = new BrotliStream(stream, CompressionMode.Decompress);
                    byte[] decompressedBytes = new byte[contentBytes.Length * 10]; // Adjust the buffer size if needed
                    int bytesRead = brotliStream.Read(decompressedBytes, 0, decompressedBytes.Length);

                    // Decode the bytes using the appropriate encoding
                    responseContent = Encoding.UTF8.GetString(decompressedBytes, 0, bytesRead); // Adjust the encoding if needed
                }
            }
            else
            {
                // If the response is not compressed, decode it directly
                responseContent = Encoding.UTF8.GetString(contentBytes); // Adjust the encoding if needed
            }

            responseContent = Regex.Replace(responseContent, TargetApplicationUri.Authority, "localhost:7200");

            return Encoding.UTF8.GetBytes(responseContent);
            //return await response.Content.ReadAsStringAsync();
        }

        private string GetFileExtension(string contentType)
        {
            string fileExtension = MimeTypesMap.GetExtension(contentType);

            if (!string.IsNullOrEmpty(fileExtension))
            {
                return fileExtension.StartsWith(".") ? fileExtension : "." + fileExtension;
            }

            return ".dat"; // Default file extension if content type is unknown
        }

        private bool IsFileType(string contentType)
        {
            string fileExtension = MimeTypesMap.GetExtension(contentType);
            return !string.IsNullOrEmpty(fileExtension);
        }
    }
}