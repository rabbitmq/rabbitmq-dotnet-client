using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Simple wrapper around TcpClient.
    /// </summary>
    public class TcpClientAdapter : ITcpClient
    {
        private Socket sock;

        public TcpClientAdapter(Socket socket)
        {
            if (socket == null)
                throw new InvalidOperationException("socket must not be null");

            this.sock = socket;
        }

        public virtual async Task ConnectAsync(string host, int port)
        {
            AssertSocket();
            var adds = await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
            var ep = TcpClientAdapterHelper.GetMatchingHost(adds, sock.AddressFamily);
            if (ep == default(IPAddress))
            {
                throw new ArgumentException("No ip address could be resolved for " + host);
            }

            await sock.ConnectAsync(ep, port).ConfigureAwait(false);
        }

        public virtual void Close()
        {
            if (sock != null)
            {
                sock.Dispose();
            }
            sock = null;
        }

        [Obsolete("Override Dispose(bool) instead.")]
        public virtual void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // dispose managed resources
                Close();
            }

            // dispose unmanaged resources
        }

        public virtual Stream GetStream()
        {
            AssertSocket();
            return new NetworkStream(sock);
        }

        public virtual Socket Client
        {
            get
            {
                return sock;
            }
        }

        public virtual bool Connected
        {
            get
            {
                if (sock == null) return false;
                return sock.Connected;
            }
        }

        public virtual int ReceiveTimeout
        {
            get
            {
                AssertSocket();
                return sock.ReceiveTimeout;
            }
            set
            {
                AssertSocket();
                sock.ReceiveTimeout = value;
            }
        }

        private void AssertSocket()
        {
            if (sock == null)
            {
                throw new InvalidOperationException("Cannot perform operation as socket is null");
            }
        }
    }
}
