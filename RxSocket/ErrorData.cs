using System;
using System.Collections.Generic;
using System.Text;

namespace RxSocket
{
    public class ErrorData
    {
        public string Method { get; }
        public Exception Exception { get; }

        public ErrorData(string method, Exception exp)
        {
            Method = method;
            Exception = exp;
        }
    }
}
