using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace TcpSocketLib
{
    [Serializable]
    public sealed class Record<T>
    {
        public EndPoint EndPoint { get; set; }    
        public T Message { get; set; }
        public string Error { get; set; }
    }
}
