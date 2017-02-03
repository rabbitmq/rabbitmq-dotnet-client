using System;
using System.Collections.Generic;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Framing.Impl
{
    internal class AsyncConnectionDecorator : IAsyncConnection
    {
        readonly IConnection connection;

        public AsyncConnectionDecorator(IConnection connection)
        {
            this.connection = connection;

            AsyncConsumerWorkService = new AsyncConsumerWorkService();
        }

        public int LocalPort => connection.LocalPort;

        public int RemotePort => connection.RemotePort;

        public void Dispose()
        {
            connection.Dispose();
        }

        public bool AutoClose { get; set; }
        public ushort ChannelMax => connection.ChannelMax;
        public IDictionary<string, object> ClientProperties => connection.ClientProperties;
        public ShutdownEventArgs CloseReason => connection.CloseReason;
        public AmqpTcpEndpoint Endpoint => connection.Endpoint;
        public uint FrameMax => connection.FrameMax;
        public ushort Heartbeat => connection.Heartbeat;
        public bool IsOpen => connection.IsOpen;
        public AmqpTcpEndpoint[] KnownHosts => connection.KnownHosts;
        public IProtocol Protocol => connection.Protocol;
        public IDictionary<string, object> ServerProperties => connection.ServerProperties;
        public IList<ShutdownReportEntry> ShutdownReport => connection.ShutdownReport;
        public string ClientProvidedName => connection.ClientProvidedName;

        public event EventHandler<CallbackExceptionEventArgs> CallbackException
        {
            add { connection.CallbackException += value; }
            remove { connection.CallbackException -= value; }
        }

        public event EventHandler<EventArgs> RecoverySucceeded
        {
            add { connection.RecoverySucceeded += value; }
            remove { connection.RecoverySucceeded -= value; }
        }

        public event EventHandler<ConnectionRecoveryErrorEventArgs> ConnectionRecoveryError
        {
            add { connection.ConnectionRecoveryError += value; }
            remove { connection.ConnectionRecoveryError -= value; }
        }
        public event EventHandler<ConnectionBlockedEventArgs> ConnectionBlocked
        {
            add { connection.ConnectionBlocked += value; }
            remove { connection.ConnectionBlocked -= value; }
        }
        public event EventHandler<ShutdownEventArgs> ConnectionShutdown
        {
            add { connection.ConnectionShutdown += value; }
            remove { connection.ConnectionShutdown -= value; }
        }

        public event EventHandler<EventArgs> ConnectionUnblocked
        {
            add { connection.ConnectionUnblocked += value; }
            remove { connection.ConnectionUnblocked -= value; }
        }

        public void Abort() => connection.Abort();

        public void Abort(ushort reasonCode, string reasonText) => connection.Abort(reasonCode, reasonText);

        public void Abort(int timeout) => connection.Abort(timeout);

        public void Abort(ushort reasonCode, string reasonText, int timeout) => connection.Abort(reasonCode, reasonText, timeout);

        public void Close() => connection.Close();

        public void Close(ushort reasonCode, string reasonText) => connection.Close(reasonCode, reasonText);

        public void Close(int timeout) => connection.Close(timeout);

        public void Close(ushort reasonCode, string reasonText, int timeout) => connection.Close(reasonCode, reasonText, timeout);

        public IModel CreateModel() => connection.CreateModel();

        public void HandleConnectionBlocked(string reason) => connection.HandleConnectionBlocked(reason);

        public void HandleConnectionUnblocked() => connection.HandleConnectionUnblocked();

        public ConsumerWorkService ConsumerWorkService => connection.ConsumerWorkService;

        public AsyncConsumerWorkService AsyncConsumerWorkService { get; }
    }
}