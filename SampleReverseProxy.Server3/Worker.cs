using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace SampleReverseProxy.Server3
{
    public class Worker : IHostedService
    {
        private const string ResponseQueueName = "responses";

        private readonly ResponseCompletionSources _responseCompletionSources;

        public Worker(ResponseCompletionSources responseCompletionSources)
        {
            _responseCompletionSources = responseCompletionSources;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Set up RabbitMQ connection and channels
            var factory = new ConnectionFactory { HostName = "localhost", Port = 5672, UserName = "guest", Password = "guest" };
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();

            // Start consuming responses
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var responseID = ea.BasicProperties.MessageId;
                //var response = Encoding.UTF8.GetString(ea.Body.ToArray());

                // Retrieve the response completion source and complete it
                if (RetrieveResponseCompletionSource(responseID, out var responseCompletionSource))
                {
                    var httpResponse = JsonSerializer.Deserialize<HttpResponseModel>(Encoding.UTF8.GetString(ea.Body.ToArray()));
                    responseCompletionSource.SetResult(httpResponse);
                }
                channel.BasicAck(ea.DeliveryTag, false);
            };

            channel.BasicConsume(queue: ResponseQueueName, autoAck: false, consumer: consumer);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private bool RetrieveResponseCompletionSource(string requestID, out TaskCompletionSource<HttpResponseModel> responseCompletionSource)
        {
            // Retrieve the response completion source from the dictionary or cache based on requestID
            // Return true if the response completion source is found, false otherwise
            // You need to implement the appropriate logic for retrieving and removing the completion source
            // This is just a placeholder method to illustrate the concept

            // Example implementation using a dictionary
            return _responseCompletionSources.Sources.TryRemove(requestID, out responseCompletionSource);
        }
    }
}
