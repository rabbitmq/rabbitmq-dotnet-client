using System;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Wrapper interface for standard TCP-client. Provides socket for socket frame handler class.
    /// </summary>
    /// <remarks>Contains all methods that are currenty in use in rabbitmq client.</remarks>
    public interface ITcpClient : IDisposable
    {
        bool Connected { get; }

        TimeSpan ReceiveTimeout { get; set; }

        Socket Client { get; }

        Task ConnectAsync(string host, int port);

        NetworkStream GetStream();

        void Close();
    }
}
