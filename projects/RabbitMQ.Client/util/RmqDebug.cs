using System;

namespace RabbitMQ.Client
{
    internal class RmqDebug
    {
        internal static bool s_isVerbose = false;

        internal static void Verbose(bool isVerbose)
        {
            s_isVerbose = isVerbose;
        }

        internal static void WriteLine(string value)
        {
            if (s_isVerbose)
            {
                Console.WriteLine(value);
            }
        }

        internal static void WriteLine(string format, object arg0)
        {
            if (s_isVerbose)
            {
                Console.WriteLine(format, arg0);
            }
        }
    }
}
