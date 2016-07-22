using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace RabbitMQ.Client
{
    static class IPAddressExt
    {
        // TODO: This method should already exist on IPAddress
        // but for some reason this does to compile against mono 4.4.1
        public static IPAddress MapToIPv6(this IPAddress addr)
        {
            var bytes = addr.GetAddressBytes();
            var bytes6 = new byte []
                { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xFF,
                  bytes [0], bytes [1], bytes [2], bytes [3] };

            return new IPAddress (bytes6);
        }
    }

    /// <summary>
    /// Simple wrapper around TcpClient.
    /// </summary>
    public class TcpClientAdapter : ITcpClient
    {
        protected TcpClient _tcpClient;


        public TcpClientAdapter(TcpClient tcpClient)
        {
            _tcpClient = tcpClient;
        }

        public virtual IAsyncResult BeginConnect(string host, int port, AsyncCallback requestCallback, object state)
        {
            assertTcpClient();
            var endpoints = Dns.GetHostAddresses(host);
            if(_tcpClient.Client.AddressFamily == AddressFamily.InterNetworkV6)
            {
                endpoints = endpoints.Select(a => a.MapToIPv6()).ToArray();
            }

            return _tcpClient.BeginConnect(endpoints, port, requestCallback, state);
        }

        private void assertTcpClient()
        {
            if (_tcpClient == null)
                throw new InvalidOperationException("Field tcpClient is null. Should have been passed to constructor.");
        }

        public virtual void EndConnect(IAsyncResult asyncResult)
        {
            assertTcpClient();

            _tcpClient.EndConnect(asyncResult);
        }

        public virtual void Close()
        {
            assertTcpClient();

            _tcpClient.Close();
        }

        public virtual NetworkStream GetStream()
        {
            assertTcpClient();

            return _tcpClient.GetStream();
        }

        public virtual Socket Client
        {
            get
            {
                assertTcpClient();

                return _tcpClient.Client;
            }
            set
            {
                _tcpClient.Client = value;
            }
        }

        public virtual bool Connected
        {
            get { return _tcpClient!=null && _tcpClient.Connected; }
        }

        public virtual int ReceiveTimeout
        {
            get
            {
                return _tcpClient.ReceiveTimeout;
            }
            set
            {
                _tcpClient.ReceiveTimeout = value;
            }
        }
    }
}
