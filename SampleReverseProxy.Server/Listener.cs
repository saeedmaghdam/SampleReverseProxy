using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SampleReverseProxy.Server
{
    public class Listener : IListener
    {
        private static TcpClient _client;

        public Listener()
        {
            if (_client == null || !_client.Connected)
            {
                TcpListener listener = new TcpListener(IPAddress.Any, 8000);
                listener.Start();

                Console.WriteLine("Server is running...");
                Console.WriteLine("Listening on port 8000...");

                _client = listener.AcceptTcpClient();
                
                Console.WriteLine("Client connected.");
            }
        }

        public void Write(string message)
        {
            try
            {
                NetworkStream stream = _client.GetStream();
                StreamWriter writer = new StreamWriter(stream);

                writer.WriteLine(message);
                writer.Flush();
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                }
            }
        }
        public string Read()
        {
            try
            {
                NetworkStream stream = _client.GetStream();
                StreamReader reader = new StreamReader(stream);

                //string message = reader.ReadToEnd();
                StringBuilder responseBuilder = new StringBuilder();
                char[] buffer = new char[4096]; // Adjust the buffer size as needed

                int bytesRead;
                do
                {
                    bytesRead = reader.Read(buffer, 0, buffer.Length);
                    responseBuilder.Append(buffer, 0, bytesRead);
                } while (bytesRead == buffer.Length);

                string message = responseBuilder.ToString();

                return message;
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                }

                return string.Empty;
            }
        }
    }
}
