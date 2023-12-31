using HeyRed.Mime;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SharpCompress.Archives;
using SharpCompress.Common;
using System.ComponentModel.Design;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SampleReverseProxy.Client3
{
    public class Worker : BackgroundService
    {
        private const string RequestQueueName = "requests";
        private const string ResponseQueueName = "responses";
        private static readonly Uri TargetApplicationUri = new Uri("https://aghdam.nl/"); // Specify the URL of the target application

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
                var responseBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));

                // Publish the response to RabbitMQ
                var responseProperties = _channel.CreateBasicProperties();
                responseProperties.MessageId = requestID;

                _channel.BasicPublish(exchange: "", routingKey: ResponseQueueName, basicProperties: responseProperties, body: responseBytes);

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

        private async Task<HttpResponseModel> ForwardRequestToTargetApplication(IDictionary<string, string> targetRequestDetails)
        {
            var path = targetRequestDetails["Path"];

            var handler = new HttpClientHandler();
            handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12; // Adjust the SSL/TLS protocol version as needed

            using var httpClient = new HttpClient(handler);

            var targetRequestMessage = CreateHttpRequestMessage(targetRequestDetails);

            HttpResponseMessage response = null;

            do
            {
                response = await httpClient.SendAsync(targetRequestMessage, HttpCompletionOption.ResponseContentRead);

                if (response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.MovedPermanently)
                {
                    var redirectUrl = response.Headers.Location;

                    targetRequestMessage = CreateHttpRequestMessage(targetRequestDetails);
                    targetRequestMessage.RequestUri = redirectUrl;
                    targetRequestMessage.Method = HttpMethod.Get; // Follow the redirect with a GET request
                }
            } while (response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.MovedPermanently);

            //int maxRedirects = 20; // Maximum number of allowed redirects
            //int redirectCount = 0; // Counter for tracking the number of redirects
            //do
            //{
            //    response = await httpClient.SendAsync(targetRequestMessage, HttpCompletionOption.ResponseContentRead);

            //    if (response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.MovedPermanently)
            //    {
            //        if (redirectCount >= maxRedirects)
            //        {
            //            // Reached the maximum number of allowed redirects
            //            throw new InvalidOperationException("Exceeded maximum number of redirects.");
            //        }

            //        var redirectUrl = response.Headers.Location;

            //        targetRequestMessage = CreateHttpRequestMessage(targetRequestDetails);
            //        targetRequestMessage.RequestUri = redirectUrl;
            //        targetRequestMessage.Method = HttpMethod.Get; // Follow the redirect with a GET request

            //        // Increment the redirect counter
            //        redirectCount++;
            //    }
            //} while (response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.MovedPermanently);

            var httpResponse = new HttpResponseModel();
            httpResponse.ContentType = response.Content.Headers.ContentType.MediaType;
            httpResponse.Headers = response.Headers.ToDictionary(h => h.Key, h => h.Value);

            if (!response.IsSuccessStatusCode)
            {
                httpResponse.HttpStatusCode = response.StatusCode;
                httpResponse.IsSuccessStatusCode = false;
                return httpResponse;
            }

            response.EnsureSuccessStatusCode();
            httpResponse.IsSuccessStatusCode = true;

            // Read the response content as bytes
            byte[] contentBytes = await response.Content.ReadAsByteArrayAsync();

            // Decompress the byte array
            byte[] decompressedBytes = await Decompress(response, contentBytes, httpResponse);

            if (path == "/")
            {
                var responseContent = Encoding.UTF8.GetString(decompressedBytes);
                responseContent = Regex.Replace(responseContent, TargetApplicationUri.Authority, "localhost:7200");
                httpResponse.Bytes = Encoding.UTF8.GetBytes(responseContent);
            }
            else
            {
                httpResponse.Bytes = decompressedBytes;
            }

            return httpResponse;
        }

        private HttpRequestMessage CreateHttpRequestMessage(IDictionary<string, string> targetRequestDetails)
        {
            var targetRequestDetailsCopy = new Dictionary<string, string>(targetRequestDetails);
            var targetRequestUri = new Uri(TargetApplicationUri, targetRequestDetailsCopy["Path"]);

            var targetRequestMessage = new HttpRequestMessage()
            {
                Method = new HttpMethod(targetRequestDetailsCopy["Method"]),
                RequestUri = targetRequestUri,
            };

            // Set headers from target request details
            targetRequestDetailsCopy.Remove("Method");
            targetRequestDetailsCopy.Remove("Path");

            foreach (var kvp in targetRequestDetailsCopy)
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

            return targetRequestMessage;
        }

        private async Task<byte[]> Decompress(HttpResponseMessage response, byte[] compressedBytes, HttpResponseModel httpResponse)
        {
            var contentEncoding = response.Content.Headers.ContentEncoding.FirstOrDefault();

            if (contentEncoding == null)
            {
                return compressedBytes;
            }
            else if (contentEncoding == "deflate")
            {
                using (Stream compressedStream = await response.Content.ReadAsStreamAsync())
                {
                    using (DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                    {
                        using (MemoryStream decompressedStream = new MemoryStream())
                        {
                            await deflateStream.CopyToAsync(decompressedStream);
                            return decompressedStream.ToArray();
                        }
                    }
                }
            }
            else if (contentEncoding == "br")
            {
                using (Stream compressedStream = await response.Content.ReadAsStreamAsync())
                {
                    using (BrotliStream brotliStream = new BrotliStream(compressedStream, CompressionMode.Decompress))
                    {
                        using (MemoryStream decompressedStream = new MemoryStream())
                        {
                            await brotliStream.CopyToAsync(decompressedStream);
                            return decompressedStream.ToArray();
                        }
                    }
                }
            }
            else
            {
                using (MemoryStream compressedStream = new MemoryStream(compressedBytes))
                {
                    IArchive archive = ArchiveFactory.Open(compressedStream);

                    foreach (IArchiveEntry entry in archive.Entries)
                    {
                        if (!entry.IsDirectory)
                        {
                            using (MemoryStream entryStream = new MemoryStream())
                            {
                                //entry.WriteTo(entryStream, new ExtractionOptions { ExtractFullPath = false, Overwrite = true });
                                entry.WriteTo(entryStream);

                                // Decompressed data as byte array
                                byte[] decompressedData = entryStream.ToArray();

                                return decompressedData;
                            }
                        }
                    }
                }
            }

            // No supported compression method found
            throw new InvalidOperationException("Unsupported compression method.");
        }
    }
}