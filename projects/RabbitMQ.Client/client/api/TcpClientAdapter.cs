using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Simple wrapper around <see cref="Socket"/>.
    /// </summary>
    public class TcpClientAdapter : ITcpClient
    {
        private Socket _sock;

        public TcpClientAdapter(Socket socket)
        {
            _sock = socket ?? throw new InvalidOperationException("socket must not be null");
        }

#if NET6_0_OR_GREATER
        public virtual Task ConnectAsync(IPAddress ep, int port, CancellationToken cancellationToken = default)
        {
            AssertSocket();
            return _sock.ConnectAsync(ep, port, cancellationToken).AsTask();
        }
#else
        public virtual Task ConnectAsync(IPAddress ep, int port, CancellationToken cancellationToken = default)
        {
            return _sock.ConnectAsync(ep, port).WaitAsync(cancellationToken);
        }
#endif

        public virtual void Close()
        {
            _sock.Dispose();
            _sock = null;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Close();
            }
        }

        public virtual NetworkStream GetStream()
        {
            AssertSocket();
            return new NetworkStream(_sock);
        }

        public virtual Socket Client
        {
            get
            {
                return _sock;
            }
        }

        public virtual bool Connected
        {
            get
            {
                if (_sock is null)
                {
                    return false;
                }
                return _sock.Connected;
            }
        }

        public virtual TimeSpan ReceiveTimeout
        {
            get
            {
                AssertSocket();
                return TimeSpan.FromMilliseconds(_sock.ReceiveTimeout);
            }
            set
            {
                AssertSocket();
                _sock.ReceiveTimeout = (int)value.TotalMilliseconds;
            }
        }

        private void AssertSocket()
        {
            if (_sock is null)
            {
                throw new InvalidOperationException("Cannot perform operation as socket is null");
            }
        }

        public static IPAddress GetMatchingHost(IReadOnlyCollection<IPAddress> addresses, AddressFamily addressFamily)
        {
            IPAddress ep = addresses.FirstOrDefault(a => a.AddressFamily == addressFamily);
            if (ep is null && addresses.Count == 1 && addressFamily == AddressFamily.Unspecified)
            {
                return addresses.Single();
            }
            return ep;
        }
    }
}
