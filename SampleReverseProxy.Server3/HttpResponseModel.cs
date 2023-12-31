﻿using System.Net;

namespace SampleReverseProxy.Server3
{
    public class HttpResponseModel
    {
        public string ContentType { get; set; }
        public IDictionary<string, IEnumerable<string>> Headers { get; set; }
        public byte[] Bytes { get; set; }
        public HttpStatusCode HttpStatusCode { get; set; }
        public bool IsSuccessStatusCode { get; set; }
    }
}
