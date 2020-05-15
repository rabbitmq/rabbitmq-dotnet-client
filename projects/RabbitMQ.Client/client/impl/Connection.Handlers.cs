using System.IO;
using System.Threading.Tasks;

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing.Impl
{
    internal sealed partial class Connection
    {
        public async ValueTask HandleConnectionClose(ushort replyCode, string replyText, ushort classId, ushort methodId)
        {
            var reason = new ShutdownEventArgs(ShutdownInitiator.Peer, replyCode, replyText, classId, methodId);
            try
            {
                _rpcContinuationQueue.HandleModelShutdown(reason);
                await InternalClose(reason).ConfigureAwait(false);
                await Transmit(new Command(new ConnectionCloseOk()), 0).ConfigureAwait(false);
                SetCloseReason(CloseReason);
            }
            catch (IOException)
            {
                // Ignored. We're only trying to be polite by sending
                // the close-ok, after all.
            }
            catch (AlreadyClosedException)
            {
                // Ignored. We're only trying to be polite by sending
                // the close-ok, after all.
            }
        }

        public ValueTask HandleCommand(Command cmd)
        {
            switch (cmd.Method)
            {
                case ConnectionOpenOk _:
                case ConnectionSecure _:
                case ConnectionStart _:
                case ConnectionTune _:
                case ConnectionUpdateSecretOk _:
                    // Command will be handled asynchronously, we can't dispose it yet.
                    _rpcContinuationQueue.HandleCommand(cmd);
                    break;
                case ConnectionBlocked connectionBlocked:
                    HandleConnectionBlocked(connectionBlocked._reason);
                    break;
                case ConnectionUnblocked _:
                    HandleConnectionUnblocked();
                    break;
                case ConnectionClose connectionClose:
                    // Returning so we'll wrap this in a using statement.
                    return HandleConnectionClose(connectionClose._replyCode, connectionClose._replyText, connectionClose._classId, connectionClose._methodId);
            }

            return default;
        }

        public void HandleConnectionUnblocked()
        {
            OnConnectionUnblocked();
        }

        public void HandleConnectionBlocked(string reason)
        {
            var args = new ConnectionBlockedEventArgs(reason);
            OnConnectionBlocked(args);
        }
    }
}
