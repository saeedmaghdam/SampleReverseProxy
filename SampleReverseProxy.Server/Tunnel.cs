using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SampleReverseProxy.Server
{
    public class Tunnel : ITunnel
    {
        private static TcpClient _client;
        private static SslStream _ssl;

        public Tunnel()
        {
            if (_client == null || !_client.Connected)
            {
                TcpListener listener = new TcpListener(IPAddress.Any, 8080);
                listener.Start();

                Console.WriteLine("Server is running...");
                Console.WriteLine("Listening on port 8080...");

                _client = listener.AcceptTcpClient();
                _ssl = new SslStream(_client.GetStream(), false);

                var serverCertificate = new X509Certificate2("certificate.pfx", "Sample@ReverSePr0xy");
                _ssl.AuthenticateAsServer(serverCertificate, clientCertificateRequired: false,
                                         SslProtocols.Tls12, checkCertificateRevocation: false);
                Console.WriteLine("Client connected.");
            }
        }

        public void Write(string message)
        {
            try
            {
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                _ssl.Write(messageBytes);
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
                byte[] buffer = new byte[4096];
                int bytes = _ssl.Read(buffer, 0, buffer.Length);
                var message = Encoding.UTF8.GetString(buffer, 0, bytes);

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
