using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace TcpSocketLib
{
    public sealed class Record
    {
        public EndPoint EndPoint { get; set; }
        public string Message { get; set; }
    }
}
