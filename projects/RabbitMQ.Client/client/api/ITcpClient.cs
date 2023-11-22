using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Wrapper interface for <see cref="Socket"/>.
    /// Provides the socket for socket frame handler class.
    /// </summary>
    /// <remarks>Contains all methods that are currently in use in rabbitmq client.</remarks>
    public interface ITcpClient : IDisposable
    {
        bool Connected { get; }

        TimeSpan ReceiveTimeout { get; set; }

        Socket Client { get; }

        // TODO CancellationToken
        Task ConnectAsync(string host, int port);
        // TODO CancellationToken
        Task ConnectAsync(IPAddress host, int port);

        NetworkStream GetStream();

        void Close();
    }
}
