using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
namespace RabbitMQ.Client
{


    /// <summary>
    /// Simple wrapper around TcpClient. 
    /// </summary>
    public class TcpClientAdapter : ITcpClient
    {
        protected Socket sock;

        public TcpClientAdapter(Socket socket)
        {
            if (socket == null)
                throw new InvalidOperationException("socket must not be null");
        
            this.sock = socket;
        }

        public virtual void Connect(string host, int port)
        {
            var _host = IPAddress.Parse("127.0.0.1");
            var ep =new IPEndPoint(_host, port);
    
            sock.Connect(ep);
        }

        public virtual void Close()
        {
            sock.Dispose();
        }

        public virtual NetworkStream GetStream()
        {
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
            get { return sock.Connected; }
        }

        public virtual int ReceiveTimeout
        {
            get
            {
                return sock.ReceiveTimeout;
            }
            set
            {
                sock.ReceiveTimeout = value;
            }
        }
    }
}
