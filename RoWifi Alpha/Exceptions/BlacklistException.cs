using System;

namespace RoWifi_Alpha.Exceptions
{
    public class BlacklistException : Exception
    {
        public BlacklistException() { }
        public BlacklistException(string message) : base(message) { }
        public BlacklistException(string message, Exception inner) : base(message, inner) { }
    }
}
