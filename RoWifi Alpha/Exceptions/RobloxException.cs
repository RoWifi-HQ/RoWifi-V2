using System;
using System.Collections.Generic;
using System.Text;

namespace RoWifi_Alpha.Exceptions
{
    public class RobloxException : Exception
    {
        public RobloxException() { }
        public RobloxException(string message) : base(message) { }
        public RobloxException(string message, Exception inner) : base(message, inner) { }
    }
}
