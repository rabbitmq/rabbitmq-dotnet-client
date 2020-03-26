using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace RabbitMQ.Client.Impl
{
    static class TcpClientAdapterHelper
    {
        public static IPAddress GetMatchingHost(IReadOnlyCollection<IPAddress> addresses, AddressFamily addressFamily)
        {
            IPAddress ep = addresses.FirstOrDefault(a => a.AddressFamily == addressFamily);
            if (ep == null && addresses.Count == 1 && addressFamily == AddressFamily.Unspecified)
            {
                return addresses.Single();
            }
            return ep;
        }
    }
}
