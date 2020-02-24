using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ.Client.src.client.impl
{
    internal static class WireConstants
    {
        public const byte Null = (byte)'V';
        public const byte String = (byte)'S';
        public const byte Array = String;
        public const byte Int = (byte)'I';
        public const byte Uint = (byte)'i';
        public const byte Decimal = (byte)'D';
        public const byte Timestamp = (byte)'T';
        public const byte Dictionary = (byte)'F';
        public const byte List = (byte)'A';
        public const byte Byte = (byte)'B';
        public const byte Sbyte = (byte)'b';
        public const byte Double = (byte)'d';
        public const byte Float = (byte)'f';
        public const byte Long = (byte)'l';
        public const byte Short = (byte)'s';
        public const byte Bool = (byte)'t';
        public const byte TableValue = (byte)'x';
    }
}
