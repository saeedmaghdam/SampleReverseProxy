using System.Net.Sockets;
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
                    var client = listener.AcceptTcpClient();
                    var reader = new StreamReader(client.GetStream());
                    var writer = new StreamWriter(client.GetStream());

                    // Read the request from the TCP stream
                    var request = reader.ReadLine();
                    //var body = reader.ReadLine();

                    // Split the request into the path and query
                    var pathAndQuery = request.Split('?');
                    var path = pathAndQuery[0];
                    var query = pathAndQuery.Length > 1 ? pathAndQuery[1] : "";

                    // Create an HTTP client and send the request
                    var httpClient = new HttpClient();
                    //var httpContent = new StringContent(body);
                    //var httpResponse = await httpClient.GetAsync("http://localhost:3000" + path + "?" + query);
                    var httpResponse = await httpClient.GetAsync("https://www.google.com/" + path + "?" + query);

                    // Read the response and write it to the TCP stream
                    var response = await httpResponse.Content.ReadAsStringAsync();
                    writer.WriteLine(response);
                    writer.Flush();

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
