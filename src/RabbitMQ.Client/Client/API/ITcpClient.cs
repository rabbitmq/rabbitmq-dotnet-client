using System;

using System.Net.Sockets;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Wrapper interface for standard TCP-client. Provides socket for socket frame handler class. 
    /// </summary>
    /// <remarks>Contains all methods that are currenty in use in rabbitmq client.</remarks>
    public interface ITcpClient
    {
        bool Connected { get; }

        int ReceiveTimeout { get; set; }

        Socket Client { get; set; }

        IAsyncResult BeginConnect(string host, int port, AsyncCallback requestCallback, object state);

        void EndConnect(IAsyncResult asyncResult);

        NetworkStream GetStream();

        void Close();
    }
}
