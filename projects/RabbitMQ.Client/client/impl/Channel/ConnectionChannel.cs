using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;
using RabbitMQ.Util;

namespace RabbitMQ.Client.client.impl.Channel
{
    #nullable enable
    internal sealed class ConnectionChannel : ChannelBase
    {
        public BlockingCell<ConnectionStartDetails>? ConnectionStartCell { get; set; }

        public ConnectionChannel(MainSession session)
            : base(session)
        {
        }

        internal ValueTask<string> ConnectionOpenAsync(string virtualHost, string capabilities, bool insist)
        {
            var k = new ModelBase.ConnectionOpenContinuation();
            lock (_rpcLock)
            {
                Enqueue(k);
                try
                {
                    ModelSend(new ConnectionOpen(virtualHost, capabilities, insist));
                }
                catch (AlreadyClosedException)
                {
                    // let continuation throw OperationInterruptedException,
                    // which is a much more suitable exception before connection
                    // negotiation finishes
                }
                k.GetReply(ContinuationTimeout);
            }

            return new ValueTask<string>(k.m_knownHosts);
        }

        private void HandleConnectionOpenOk(string knownHosts)
        {
            var k = (ModelBase.ConnectionOpenContinuation)GetRpcContinuation();
            k.m_redirect = false;
            k.m_host = null;
            k.m_knownHosts = knownHosts;
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        private void HandleConnectionStart(byte versionMajor, byte versionMinor, IDictionary<string, object> serverProperties, byte[] mechanisms, byte[] locales)
        {
            var cell = ConnectionStartCell;
            if (cell is null)
            {
                var reason = new ShutdownEventArgs(ShutdownInitiator.Library, Constants.CommandInvalid, "Unexpected Connection.Start");
                Session.Connection.Close(reason);
                return;
            }

            cell.ContinueWithValue(new ConnectionStartDetails
            {
                m_versionMajor = versionMajor,
                m_versionMinor = versionMinor,
                m_serverProperties = serverProperties,
                m_mechanisms = mechanisms,
                m_locales = locales
            });
            ConnectionStartCell = null;
        }

        private void HandleConnectionClose(ushort replyCode, string replyText, ushort classId, ushort methodId)
        {
            var reason = new ShutdownEventArgs(ShutdownInitiator.Peer, replyCode, replyText, classId, methodId);
            try
            {
                Session.Connection.InternalClose(reason);
                ModelSend(new ConnectionCloseOk());
                SetCloseReason(Session.Connection.CloseReason);
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

        internal ValueTask<ConnectionSecureOrTune> ConnectionSecureOkAsync(byte[] response)
        {
            var k = new ModelBase.ConnectionStartRpcContinuation();
            lock (_rpcLock)
            {
                Enqueue(k);
                try
                {
                    ModelSend(new ConnectionSecureOk(response));
                }
                catch (AlreadyClosedException)
                {
                    // let continuation throw OperationInterruptedException,
                    // which is a much more suitable exception before connection
                    // negotiation finishes
                }
                k.GetReply(ContinuationTimeout);
            }
            return new ValueTask<ConnectionSecureOrTune>(k.m_result);
        }

        private void HandleConnectionSecure(byte[] challenge)
        {
            var k = (ModelBase.ConnectionStartRpcContinuation)GetRpcContinuation();
            k.m_result = new ConnectionSecureOrTune
            {
                m_challenge = challenge
            };
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        internal ValueTask<ConnectionSecureOrTune> ConnectionStartOkAsync(IDictionary<string, object> clientProperties, string mechanism, byte[] response, string locale)
        {
            var k = new ModelBase.ConnectionStartRpcContinuation();
            lock (_rpcLock)
            {
                Enqueue(k);
                try
                {
                    ModelSend(new ConnectionStartOk(clientProperties, mechanism, response, locale));
                }
                catch (AlreadyClosedException)
                {
                    // let continuation throw OperationInterruptedException,
                    // which is a much more suitable exception before connection
                    // negotiation finishes
                }
                k.GetReply(ContinuationTimeout);
            }
            return new ValueTask<ConnectionSecureOrTune>(k.m_result);
        }

        internal ValueTask ConnectionTuneOkAsync(ushort channelMax, uint frameMax, ushort heartbeat)
        {
            ModelSend(new ConnectionTuneOk(channelMax, frameMax, heartbeat));
            return default;
        }

        private void HandleConnectionTune(ushort channelMax, uint frameMax, ushort heartbeatInSeconds)
        {
            var k = (ModelBase.ConnectionStartRpcContinuation)GetRpcContinuation();
            k.m_result = new ConnectionSecureOrTune
            {
                m_tuneDetails =
                {
                    m_channelMax = channelMax,
                    m_frameMax = frameMax,
                    m_heartbeatInSeconds = heartbeatInSeconds
                }
            };
            k.HandleCommand(IncomingCommand.Empty); // release the continuation.
        }

        internal ValueTask UpdateSecretAsync(string newSecret, string reason)
        {
            if (newSecret is null)
            {
                throw new ArgumentNullException(nameof(newSecret));
            }

            if (reason is null)
            {
                throw new ArgumentNullException(nameof(reason));
            }

            ModelRpc<ConnectionUpdateSecretOk>(new ConnectionUpdateSecret(Encoding.UTF8.GetBytes(newSecret), reason));
            return default;
        }

        private void HandleConnectionBlocked(string reason)
        {
            Session.Connection.HandleConnectionBlocked(reason);
        }

        private void HandleConnectionUnblocked()
        {
            Session.Connection.HandleConnectionUnblocked();
        }

        internal void FinishClose(ShutdownEventArgs shutdownEventArgs)
        {
            SetCloseReason(shutdownEventArgs);

            if (CloseReason != null)
            {
                Session.Close(CloseReason);
            }

            ConnectionStartCell?.ContinueWithValue(null!);
        }

        private protected override bool DispatchAsynchronous(in IncomingCommand cmd)
        {
            switch (cmd.Method.ProtocolCommandId)
            {
                case ProtocolCommandId.ConnectionStart:
                {
                    var __impl = (ConnectionStart)cmd.Method;
                    HandleConnectionStart(__impl._versionMajor, __impl._versionMinor, __impl._serverProperties, __impl._mechanisms, __impl._locales);
                    return true;
                }
                case ProtocolCommandId.ConnectionOpenOk:
                {
                    var __impl = (ConnectionOpenOk)cmd.Method;
                    HandleConnectionOpenOk(__impl._reserved1);
                    return true;
                }
                case ProtocolCommandId.ConnectionClose:
                {
                    var __impl = (ConnectionClose)cmd.Method;
                    HandleConnectionClose(__impl._replyCode, __impl._replyText, __impl._classId, __impl._methodId);
                    return true;
                }
                case ProtocolCommandId.ConnectionTune:
                {
                    var __impl = (ConnectionTune)cmd.Method;
                    HandleConnectionTune(__impl._channelMax, __impl._frameMax, __impl._heartbeat);
                    return true;
                }
                case ProtocolCommandId.ConnectionSecure:
                {
                    var __impl = (ConnectionSecure)cmd.Method;
                    HandleConnectionSecure(__impl._challenge);
                    return true;
                }
                case ProtocolCommandId.ConnectionBlocked:
                {
                    var __impl = (ConnectionBlocked)cmd.Method;
                    HandleConnectionBlocked(__impl._reason);
                    return true;
                }
                case ProtocolCommandId.ConnectionUnblocked:
                {
                    HandleConnectionUnblocked();
                    return true;
                }
                default: return false;
            }
        }
    }
}
