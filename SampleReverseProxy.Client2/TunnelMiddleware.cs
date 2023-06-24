namespace SampleReverseProxy.Client2
{
    public class TunnelMiddleware
    {
        private readonly RequestDelegate _next;

        public TunnelMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, ITunnel tunnel)
        {
            await tunnel.ProcessRequest(context);
            await _next(context);
        }
    }
}
