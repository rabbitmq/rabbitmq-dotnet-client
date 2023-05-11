using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace MassPublish
{
    internal class TcpClient : ITcpClient
    {
        private Socket _sock;

        public TcpClient(Socket socket)
        {
            _sock = socket ?? throw new InvalidOperationException("socket must not be null");
        }

        public virtual async Task ConnectAsync(string host, int port)
        {
            AssertSocket();
            await _sock.ConnectAsync(host, port).ConfigureAwait(false);
        }

        public virtual Task ConnectAsync(IPAddress ep, int port)
        {
            AssertSocket();
            return _sock.ConnectAsync(ep, port);
        }

        public virtual void Close()
        {
            _sock?.Dispose();
            _sock = null;
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
    }
}
