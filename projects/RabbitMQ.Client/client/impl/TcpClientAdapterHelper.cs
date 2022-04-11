using System.Net;
using System.Net.Sockets;

namespace RabbitMQ.Client.Impl
{
    internal static class TcpClientAdapterHelper
    {
        public static IPAddress GetMatchingHost(IPAddress[] addresses, AddressFamily addressFamily)
        {
            if (addresses != null && addresses.Length == 1 && addressFamily == AddressFamily.Unspecified)
            {
                return addresses[0];
            }

            for (int i = 0; i < addresses.Length; i++)
            {
                IPAddress address = addresses[i];
                if (address.AddressFamily == addressFamily)
                {
                    return address;
                }
            }

            return null;
        }
    }
}
