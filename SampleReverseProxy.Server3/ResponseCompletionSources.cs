using System.Collections.Concurrent;

namespace SampleReverseProxy.Server3
{
    public class ResponseCompletionSources
    {
        private ConcurrentDictionary<string, TaskCompletionSource<byte[]>> _responseCompletionSources;

        public ConcurrentDictionary<string, TaskCompletionSource<byte[]>> Sources { get { return _responseCompletionSources; } private set { _responseCompletionSources = value; } }

        public ResponseCompletionSources ()
        {
            _responseCompletionSources = new ConcurrentDictionary<string, TaskCompletionSource<byte[]>>();
        }
    }
}
