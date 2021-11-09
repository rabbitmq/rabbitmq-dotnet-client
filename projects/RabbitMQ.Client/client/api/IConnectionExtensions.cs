using System;
using System.IO;
using System.Threading;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client
{
    public static class IConnectionExtensions
    {
        /// <summary>
        /// Close this connection and all its channels.
        /// </summary>
        /// <remarks>
        /// Note that all active channels, sessions, and models will be
        /// closed if this method is called. It will wait for the in-progress
        /// close operation to complete. This method will not return to the caller
        /// until the shutdown is complete. If the connection is already closed
        /// (or closing), then this method will do nothing.
        /// It can also throw <see cref="IOException"/> when socket was closed unexpectedly.
        /// </remarks>
        public static void Close(this IConnection connection)
        {
            connection.Close(Constants.ReplySuccess, "Goodbye", Timeout.InfiniteTimeSpan, false);
        }

        /// <summary>
        /// Close this connection and all its channels.
        /// </summary>
        /// <remarks>
        /// The method behaves in the same way as <see cref="Close(IConnection)"/>, with the only
        /// difference that the connection is closed with the given connection close code and message.
        /// <para>
        /// The close code (See under "Reply Codes" in the AMQP specification).
        /// </para>
        /// <para>
        /// A message indicating the reason for closing the connection.
        /// </para>
        /// </remarks>
        public static void Close(this IConnection connection, ushort reasonCode, string reasonText)
        {
            connection.Close(reasonCode, reasonText, Timeout.InfiniteTimeSpan, false);
        }

        /// <summary>
        /// Close this connection and all its channels
        /// and wait with a timeout for all the in-progress close operations to complete.
        /// </summary>
        /// <remarks>
        /// Note that all active channels, sessions, and models will be
        /// closed if this method is called. It will wait for the in-progress
        /// close operation to complete with a timeout. If the connection is
        /// already closed (or closing), then this method will do nothing.
        /// It can also throw <see cref="IOException"/> when socket was closed unexpectedly.
        /// If timeout is reached and the close operations haven't finished, then socket is forced to close.
        /// <para>
        /// To wait infinitely for the close operations to complete use <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>.
        /// </para>
        /// </remarks>
        public static void Close(this IConnection connection, TimeSpan timeout)
        {
            connection.Close(Constants.ReplySuccess, "Goodbye", timeout, false);
        }

        /// <summary>
        /// Close this connection and all its channels
        /// and wait with a timeout for all the in-progress close operations to complete.
        /// </summary>
        /// <remarks>
        /// The method behaves in the same way as <see cref="Close(IConnection,TimeSpan)"/>, with the only
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
        public static void Close(this IConnection connection, ushort reasonCode, string reasonText, TimeSpan timeout)
        {
            connection.Close(reasonCode, reasonText, timeout, false);
        }

        /// <summary>
        /// Close this connection and all its channels
        /// and wait with a timeout for all the in-progress close operations to complete.
        /// </summary>
        /// <remarks>
        /// The method behaves in the same way as <see cref="Close(IConnection,TimeSpan)"/>, with the only
        /// differences that the connection is closed with the given connection close code and message without stopping the recovery loop.
        /// <para>
        /// The close code (See under "Reply Codes" in the AMQP 0-9-1 specification).
        /// </para>
        /// <para>
        /// A message indicating the reason for closing the connection.
        /// </para>
        /// <para>
        /// Recovery loop should continue.
        /// </para>
        /// </remarks>
        internal static void Close(this AutorecoveringConnection connection, ushort reasonCode, string reasonText, bool recoveryFailed)
        {
            connection.Close(reasonCode, reasonText, Timeout.InfiniteTimeSpan, false, recoveryFailed);
        }

        /// <summary>
        /// Abort this connection and all its channels.
        /// </summary>
        /// <remarks>
        /// Note that all active channels, sessions, and models will be closed if this method is called.
        /// In comparison to normal <see cref="Close(IConnection)"/> method, <see cref="Abort(IConnection)"/> will not throw
        /// <see cref="IOException"/> during closing connection.
        ///This method waits infinitely for the in-progress close operation to complete.
        /// </remarks>
        public static void Abort(this IConnection connection)
        {
            connection.Close(Constants.ReplySuccess, "Connection close forced", Timeout.InfiniteTimeSpan, true);
        }

        /// <summary>
        /// Abort this connection and all its channels.
        /// </summary>
        /// <remarks>
        /// The method behaves in the same way as <see cref="Abort(IConnection)"/>, with the only
        /// difference that the connection is closed with the given connection close code and message.
        /// <para>
        /// The close code (See under "Reply Codes" in the AMQP 0-9-1 specification)
        /// </para>
        /// <para>
        /// A message indicating the reason for closing the connection
        /// </para>
        /// </remarks>
        public static void Abort(this IConnection connection, ushort reasonCode, string reasonText)
        {
            connection.Close(reasonCode, reasonText, Timeout.InfiniteTimeSpan, true);
        }

        /// <summary>
        /// Abort this connection and all its channels and wait with a
        /// timeout for all the in-progress close operations to complete.
        /// </summary>
        /// <remarks>
        /// This method, behaves in a similar way as method <see cref="Abort(IConnection)"/> with the
        /// only difference that it explicitly specifies a timeout given
        /// for all the in-progress close operations to complete.
        /// If timeout is reached and the close operations haven't finished, then socket is forced to close.
        /// <para>
        /// To wait infinitely for the close operations to complete use <see cref="Timeout.Infinite"/>.
        /// </para>
        /// </remarks>
        public static void Abort(this IConnection connection, TimeSpan timeout)
        {
            connection.Close(Constants.ReplySuccess, "Connection close forced", timeout, true);
        }

        /// <summary>
        /// Abort this connection and all its channels and wait with a
        /// timeout for all the in-progress close operations to complete.
        /// </summary>
        /// <remarks>
        /// The method behaves in the same way as <see cref="Abort(IConnection,TimeSpan)"/>, with the only
        /// difference that the connection is closed with the given connection close code and message.
        /// <para>
        /// The close code (See under "Reply Codes" in the AMQP 0-9-1 specification).
        /// </para>
        /// <para>
        /// A message indicating the reason for closing the connection.
        /// </para>
        /// </remarks>
        public static void Abort(this IConnection connection, ushort reasonCode, string reasonText, TimeSpan timeout)
        {
            connection.Close(reasonCode, reasonText, timeout, true);
        }
    }
}
