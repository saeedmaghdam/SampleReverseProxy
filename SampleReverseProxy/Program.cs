using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SampleReverseProxy
{
    class Program
    {
        static void Main(string[] args)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 8000);
            listener.Start();

            Console.WriteLine("Server is running...");
            Console.WriteLine("Listening on port 8000...");

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                SslStream ssl = new SslStream(client.GetStream(), false);
                try
                {
                    var serverCertificate = new X509Certificate2("certificate.pfx", "Sample@ReverSePr0xy");
                    ssl.AuthenticateAsServer(serverCertificate, clientCertificateRequired: false,
                                             SslProtocols.Tls12, checkCertificateRevocation: false);
                    Console.WriteLine("Client connected.");

                    // Send a message to the client.
                    byte[] message = Encoding.UTF8.GetBytes("Hello, client!");
                    ssl.Write(message);

                    // Read a message from the client.
                    byte[] buffer = new byte[4096];
                    int bytes = ssl.Read(buffer, 0, buffer.Length);
                    Console.WriteLine("Client says: " + Encoding.UTF8.GetString(buffer, 0, bytes));
                }
                catch (AuthenticationException e)
                {
                    Console.WriteLine("Exception: {0}", e.Message);
                    if (e.InnerException != null)
                    {
                        Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                    }
                    ssl.Close();
                    client.Close();
                    return;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }
    }
}