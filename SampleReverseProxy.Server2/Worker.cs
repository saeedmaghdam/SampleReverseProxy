﻿using System.Net.Sockets;
using System.Net;

namespace SampleReverseProxy.Server2
{
    public class Worker : IHostedService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Create a TCP listener
            var listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 8001);
            listener.Start();

            while (true)
            {
                try
                {
                    // Accept a TCP client
                    var client = await listener.AcceptTcpClientAsync();
                    var reader = new StreamReader(client.GetStream());
                    var writer = new StreamWriter(client.GetStream());

                    // Read the request from the TCP stream
                    var request = await reader.ReadLineAsync();
                    //var body = await reader.ReadLineAsync();

                    // Split the request into the path and query
                    var pathAndQuery = request.Split('?');
                    var path = pathAndQuery[0];
                    var query = pathAndQuery.Length > 1 ? pathAndQuery[1] : "";

                    // Create an HTTP client and send the request
                    using (var httpClient = new HttpClient())
                    {
                        var httpResponse = await httpClient.GetAsync("https://google.com" + path + "?" + query);

                        // Stream the response from the HTTP client to the TCP client
                        var responseStream = await httpResponse.Content.ReadAsStreamAsync();
                        await responseStream.CopyToAsync(client.GetStream());
                    }

                    // Close the TCP client
                    client.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message, ex);
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
