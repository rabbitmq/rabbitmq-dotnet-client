using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace RabbitMQ.Client
{


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
            
            return _tcpClient.BeginConnect(host, port, requestCallback, state);
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
