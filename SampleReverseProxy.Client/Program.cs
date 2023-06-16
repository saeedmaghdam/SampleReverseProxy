using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SampleReverseProxy.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            TcpClient client = new TcpClient("localhost", 8000);
            SslStream ssl = new SslStream(client.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);

            try
            {
                var clientCertificate = new X509Certificate2("certificate.pfx", "Sample@ReverSePr0xy");
                ssl.AuthenticateAsClient("localhost", new X509CertificateCollection { clientCertificate },
                                         SslProtocols.Tls12, checkCertificateRevocation: false);

                // Read a message from the server.
                byte[] buffer = new byte[4096];
                int bytes = ssl.Read(buffer, 0, buffer.Length);
                Console.WriteLine("Server says: " + Encoding.UTF8.GetString(buffer, 0, bytes));

                // Send a message to the server.
                byte[] message = Encoding.UTF8.GetBytes("Hello, server!");
                ssl.Write(message);
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                }
            }
            finally
            {
                ssl.Close();
                client.Close();
            }
        }

        // The following method is invoked by the RemoteCertificateValidationDelegate.
        public static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            Console.WriteLine("Certificate error: {0}", sslPolicyErrors);

            // Allow this client to communicate with servers that present self        signed certificates.
            if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors)
            {
                // If all errors are related to the root being unknown,
                // consider the certificate valid.
                foreach (X509ChainStatus chainStatus in chain.ChainStatus)
                {
                    if (chainStatus.Status != X509ChainStatusFlags.UntrustedRoot)
                    {
                        return false;
                    }
                }
                return true;
            }

            // In all other cases, do not trust the server's certificate.
            return false;
        }
    }
}