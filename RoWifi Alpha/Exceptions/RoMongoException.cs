using System;
using System.Collections.Generic;
using System.Text;

namespace RoWifi_Alpha.Exceptions
{
    public class RoMongoException : Exception
    {
        public RoMongoException() { }
        public RoMongoException(string message) : base(message) { }
        public RoMongoException(string message, Exception inner): base(message, inner) { }
    }
}
