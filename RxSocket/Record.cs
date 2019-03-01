using System;
using System.Net;

namespace RxSocket
{
    [Serializable]
    public sealed class Record<T>
    {
        public EndPoint EndPoint { get; set; }
        public T Message { get; set; }
        public string Error { get; set; }
    }
}
