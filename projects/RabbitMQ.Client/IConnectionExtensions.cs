using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client
{
    public static class IConnectionExtensions
    {
        /// <summary>
        /// Asynchronously close this connection and all its channels.
        /// </summary>
        /// <remarks>
        /// Note that all active channels and sessions will be closed if this method is called.
        /// It waits 30 seconds for the in-progress close operation to complete and throws if that
        /// elapses: an <see cref="OperationCanceledException"/> (a
        /// <see cref="System.Threading.Tasks.TaskCanceledException"/> on .NET) rather than
        /// <see cref="IOException"/>, which signals a socket closed unexpectedly. Note that a
        /// connection returned by <see cref="ConnectionFactory"/> with automatic recovery enabled,
        /// the default, first stops its recovery loop on a separate budget of
        /// <see cref="ConnectionFactory.RequestedConnectionTimeout"/>, so the total time can
        /// exceed 30 seconds. On a connection that is already closed this does nothing when
        /// automatic recovery is enabled, and throws <see cref="Exceptions.AlreadyClosedException"/>
        /// when it is not.
        /// </remarks>
        public static Task CloseAsync(this IConnection connection, CancellationToken cancellationToken = default)
        {
            return connection.CloseAsync(Constants.ReplySuccess, "Goodbye", InternalConstants.DefaultConnectionCloseTimeout, false,
                cancellationToken);
        }

        /// <summary>
        /// Asynchronously close this connection and all its channels.
        /// </summary>
        /// <remarks>
        /// The method behaves in the same way as <see cref="CloseAsync(IConnection, CancellationToken)"/>, with the only
        /// difference that the connection is closed with the given connection close code and message.
        /// <para>
        /// The close code (See under "Reply Codes" in the AMQP specification).
        /// </para>
        /// <para>
        /// A message indicating the reason for closing the connection.
        /// </para>
        /// </remarks>
        public static Task CloseAsync(this IConnection connection, ushort reasonCode, string reasonText,
            CancellationToken cancellationToken = default)
        {
            return connection.CloseAsync(reasonCode, reasonText, InternalConstants.DefaultConnectionCloseTimeout, false,
                cancellationToken);
        }

        /// <summary>
        /// Asynchronously close this connection and all its channels
        /// and wait with a timeout for all the in-progress close operations to complete.
        /// </summary>
        /// <remarks>
        /// Note that all active channels and sessions will be
        /// closed if this method is called. It will wait for the in-progress
        /// close operation to complete with a timeout. If the connection is
        /// already closed (or closing), then this method will do nothing.
        /// It can also throw <see cref="IOException"/> when socket was closed unexpectedly.
        /// If timeout is reached and the close operations haven't finished, then socket is forced to close.
        /// <para>
        /// To wait infinitely for the close operations to complete use <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>.
        /// </para>
        /// <para>
        /// A finite timeout shorter than 30 seconds is raised to 30 seconds, because the
        /// timeout also bounds the close handshake itself and cutting that short leaves the
        /// connection only partly shut down. Use <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>
        /// to wait without a bound.
        /// </para>
        /// <para>
        /// A value too large for the timer, including <see cref="TimeSpan.MaxValue"/>, is clamped to
        /// the largest supported bound rather than throwing. That limit depends on which build of
        /// this library your application resolves, not on the runtime it executes on: roughly 24.86
        /// days for the netstandard2.0 build, which is what .NET Framework and .NET versions before
        /// 8 load, and roughly 49.7 days for the net8.0 build.
        /// </para>
        /// <para>
        /// <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> waits without any bound, and
        /// nothing else can end that wait: no timer is armed, and a
        /// <see cref="CancellationToken"/> passed to the underlying
        /// <see cref="IConnection.CloseAsync(ushort, string, TimeSpan, bool, CancellationToken)"/>
        /// is deliberately ignored while the connection is open, so that a close already under way
        /// is not truncated. Use it only when waiting indefinitely for the peer's reply is what you
        /// want.
        /// </para>
        /// </remarks>
        public static Task CloseAsync(this IConnection connection, TimeSpan timeout)
        {
            return connection.CloseAsync(Constants.ReplySuccess, "Goodbye", timeout, false,
                CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously close this connection and all its channels
        /// and wait with a timeout for all the in-progress close operations to complete.
        /// </summary>
        /// <remarks>
        /// The method behaves in the same way as <see cref="CloseAsync(IConnection,TimeSpan)"/>, with the only
        /// difference that the connection is closed with the given connection close code and message.
        /// <para>
        /// The close code (See under "Reply Codes" in the AMQP 0-9-1 specification).
        /// </para>
        /// <para>
        /// A message indicating the reason for closing the connection.
        /// </para>
        /// <para>
        /// Operation timeout.
        /// </para>
        /// </remarks>
        public static Task CloseAsync(this IConnection connection, ushort reasonCode, string reasonText, TimeSpan timeout)
        {
            return connection.CloseAsync(reasonCode, reasonText, timeout, false,
                CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously abort this connection and all its channels.
        /// </summary>
        /// <remarks>
        /// Note that all active channels and sessions will be closed if this method is called.
        /// In comparison to normal <see cref="CloseAsync(IConnection, CancellationToken)"/> method, <see cref="AbortAsync(IConnection, CancellationToken)"/> will not throw
        /// <see cref="IOException"/> during closing connection.
        /// This method waits 5 seconds for the in-progress close operation to complete and then
        /// attempts to close the socket, and unlike a graceful close it does not rethrow when that
        /// wait elapses. Note that a connection returned by <see cref="ConnectionFactory"/> with
        /// automatic recovery enabled, the default, first stops its recovery loop on a separate
        /// budget of <see cref="ConnectionFactory.RequestedConnectionTimeout"/>, so the total time
        /// can exceed 5 seconds.
        /// </remarks>
        public static Task AbortAsync(this IConnection connection)
        {
            return connection.CloseAsync(Constants.ReplySuccess, "Connection close forced",
                InternalConstants.DefaultConnectionAbortTimeout, true, default);
        }

        /// <summary>
        /// Asynchronously abort this connection and all its channels.
        /// </summary>
        /// <remarks>
        /// Note that all active channels and sessions will be closed if this method is called.
        /// In comparison to normal <see cref="CloseAsync(IConnection, CancellationToken)"/> method, <see cref="AbortAsync(IConnection, CancellationToken)"/> will not throw
        /// <see cref="IOException"/> during closing connection.
        /// This method waits 5 seconds for the in-progress close operation to complete and then
        /// attempts to close the socket, and unlike a graceful close it does not rethrow when that
        /// wait elapses. Note that a connection returned by <see cref="ConnectionFactory"/> with
        /// automatic recovery enabled, the default, first stops its recovery loop on a separate
        /// budget of <see cref="ConnectionFactory.RequestedConnectionTimeout"/>, so the total time
        /// can exceed 5 seconds.
        /// </remarks>
        public static Task AbortAsync(this IConnection connection, CancellationToken cancellationToken = default)
        {
            return connection.CloseAsync(Constants.ReplySuccess, "Connection close forced",
                InternalConstants.DefaultConnectionAbortTimeout, true, cancellationToken);
        }

        /// <summary>
        /// Asynchronously abort this connection and all its channels.
        /// </summary>
        /// <remarks>
        /// The method behaves in the same way as <see cref="AbortAsync(IConnection, CancellationToken)"/>, with the only
        /// difference that the connection is closed with the given connection close code and message.
        /// <para>
        /// The close code (See under "Reply Codes" in the AMQP 0-9-1 specification)
        /// </para>
        /// <para>
        /// A message indicating the reason for closing the connection
        /// </para>
        /// </remarks>
        public static Task AbortAsync(this IConnection connection, ushort reasonCode, string reasonText)
        {
            return connection.CloseAsync(reasonCode, reasonText,
                InternalConstants.DefaultConnectionAbortTimeout, true, default);
        }

        /// <summary>
        /// Asynchronously abort this connection and all its channels.
        /// </summary>
        /// <remarks>
        /// The method behaves in the same way as <see cref="AbortAsync(IConnection, CancellationToken)"/>, with the only
        /// difference that the connection is closed with the given connection close code and message.
        /// <para>
        /// The close code (See under "Reply Codes" in the AMQP 0-9-1 specification)
        /// </para>
        /// <para>
        /// A message indicating the reason for closing the connection
        /// </para>
        /// </remarks>
        public static Task AbortAsync(this IConnection connection, ushort reasonCode, string reasonText, CancellationToken cancellationToken = default)
        {
            return connection.CloseAsync(reasonCode, reasonText,
                InternalConstants.DefaultConnectionAbortTimeout, true, cancellationToken);
        }

        /// <summary>
        /// Asynchronously abort this connection and all its channels and wait with a
        /// timeout for all the in-progress close operations to complete.
        /// </summary>
        /// <remarks>
        /// This method, behaves in a similar way as method <see cref="AbortAsync(IConnection, CancellationToken)"/> with the
        /// only difference that it explicitly specifies a timeout given
        /// for all the in-progress close operations to complete.
        /// If timeout is reached and the close operations haven't finished, then socket is forced to close.
        /// <para>
        /// An abort is always bounded, so unlike <see cref="CloseAsync(IConnection,TimeSpan)"/>
        /// it does not honour <see cref="Timeout.InfiniteTimeSpan"/>: an unbounded abort
        /// would make the forced socket close above unreachable, defeating the best-effort
        /// guarantee that abort exists to provide. A timeout shorter than 5 seconds, or an
        /// unbounded one, is resolved to 5 seconds, because the timeout also bounds the close
        /// handshake itself and cutting that short leaves the connection only partly shut down. A
        /// finite value above 5 seconds is honoured as given, however large, after being clamped to
        /// the largest bound the timer supports.
        /// </para>
        /// </remarks>
        public static Task AbortAsync(this IConnection connection, TimeSpan timeout)
        {
            return connection.CloseAsync(Constants.ReplySuccess, "Connection close forced", timeout, true,
                CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously abort this connection and all its channels and wait with a
        /// timeout for all the in-progress close operations to complete.
        /// </summary>
        /// <remarks>
        /// The method behaves in the same way as <see cref="AbortAsync(IConnection,TimeSpan)"/>, with the only
        /// difference that the connection is closed with the given connection close code and message.
        /// <para>
        /// The close code (See under "Reply Codes" in the AMQP 0-9-1 specification).
        /// </para>
        /// <para>
        /// A message indicating the reason for closing the connection.
        /// </para>
        /// </remarks>
        public static Task AbortAsync(this IConnection connection, ushort reasonCode, string reasonText, TimeSpan timeout)
        {
            return connection.CloseAsync(reasonCode, reasonText, timeout, true,
                CancellationToken.None);
        }
    }
}
