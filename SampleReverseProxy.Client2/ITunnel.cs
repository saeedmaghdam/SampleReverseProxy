using System.Net.Sockets;

namespace SampleReverseProxy.Client2
{
    public interface ITunnel
    {
        Task ProcessRequest(HttpContext context);
    }
}
