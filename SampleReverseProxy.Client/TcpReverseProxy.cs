using System.Net.Sockets;
using System.Net;
using System.Text;

namespace SampleReverseProxy.Client
{
    public class TcpReverseProxy
    {
        private TcpListener _listener;
        private const string TargetHost = "localhost";
        private const int TargetPort = 3000;

        public TcpReverseProxy()
        {
            _listener = new TcpListener(IPAddress.Any, 8001);
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

        //private async void ProcessRequest(TcpClient client)
        //{
        //    using (var networkStream = client.GetStream())
        //    using (var reader = new StreamReader(networkStream, Encoding.UTF8, true, 1024, true))
        //    using (var writer = new StreamWriter(networkStream, Encoding.UTF8, 1024, true))
        //    using (var targetClient = new TcpClient(TargetHost, TargetPort))
        //    using (var targetStream = targetClient.GetStream())
        //    {
        //        // Copy client request to target
        //        await reader.BaseStream.CopyToAsync(targetStream);
        //        await targetStream.FlushAsync();

        //        // Copy target response to client
        //        await targetStream.CopyToAsync(writer.BaseStream);
        //        await writer.FlushAsync();
        //    }
        //}

        private async void ProcessRequest(TcpClient client)
        {
            using (var networkStream = client.GetStream())
            using (var targetClient = new TcpClient(TargetHost, TargetPort))
            using (var targetStream = targetClient.GetStream())
            {
                byte[] buffer = new byte[8192];
                var timeout = TimeSpan.FromSeconds(10);  // Adjust timeout as needed

                // Copy client request to target
                var bytesReadTask = networkStream.ReadAsync(buffer, 0, buffer.Length);
                if (await Task.WhenAny(bytesReadTask, Task.Delay(timeout)) == bytesReadTask)
                {
                    // bytesReadTask completed within timeout
                    int bytesRead = await bytesReadTask;
                    await targetStream.WriteAsync(buffer, 0, bytesRead);
                    await targetStream.FlushAsync();
                }
                else
                {
                    // bytesReadTask did not complete within timeout
                    throw new TimeoutException("The operation has timed out.");
                }

                // Copy target response to client
                bytesReadTask = targetStream.ReadAsync(buffer, 0, buffer.Length);
                if (await Task.WhenAny(bytesReadTask, Task.Delay(timeout)) == bytesReadTask)
                {
                    // bytesReadTask completed within timeout
                    int bytesRead = await bytesReadTask;
                    await networkStream.WriteAsync(buffer, 0, bytesRead);
                    await networkStream.FlushAsync();
                }
                else
                {
                    // bytesReadTask did not complete within timeout
                    throw new TimeoutException("The operation has timed out.");
                }
            }
        }

        public void Stop()
        {
            _listener.Stop();
        }
    }
}
