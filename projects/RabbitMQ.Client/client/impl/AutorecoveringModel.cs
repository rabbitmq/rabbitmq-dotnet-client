// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2020 VMware, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       https://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v2.0:
//
//---------------------------------------------------------------------------
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    internal sealed class AutorecoveringModel : IFullModel, IRecoverable
    {
        private bool _disposed = false;
        private readonly object _eventLock = new object();
        private AutorecoveringConnection _connection;
        private RecoveryAwareModel _delegate;
        private List<string> _recordedConsumerTags = new List<string>();

        private EventHandler<BasicAckEventArgs> _recordedBasicAckEventHandlers;
        private EventHandler<BasicNackEventArgs> _recordedBasicNackEventHandlers;
        private EventHandler<BasicReturnEventArgs> _recordedBasicReturnEventHandlers;
        private EventHandler<CallbackExceptionEventArgs> _recordedCallbackExceptionEventHandlers;
        private EventHandler<ShutdownEventArgs> _recordedShutdownEventHandlers;

        private ushort _prefetchCountConsumer = 0;
        private ushort _prefetchCountGlobal = 0;
        private bool _usesPublisherConfirms = false;
        private bool _usesTransactions = false;

        public IConsumerDispatcher ConsumerDispatcher
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return _delegate.ConsumerDispatcher;
            }
        }

        public TimeSpan ContinuationTimeout
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return _delegate.ContinuationTimeout;
            }

            set
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                _delegate.ContinuationTimeout = value;
            }
        }

        public IEnumerable<string> ConsumerTags => _recordedConsumerTags;

        public AutorecoveringModel(AutorecoveringConnection conn, RecoveryAwareModel _delegate)
        {
            _connection = conn;
            this._delegate = _delegate;
        }

        public event EventHandler<BasicAckEventArgs> BasicAcks
        {
            add
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                lock (_eventLock)
                {
                    _recordedBasicAckEventHandlers += value;
                    _delegate.BasicAcks += value;
                }
            }
            remove
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                lock (_eventLock)
                {
                    _recordedBasicAckEventHandlers -= value;
                    _delegate.BasicAcks -= value;
                }
            }
        }

        public event EventHandler<BasicNackEventArgs> BasicNacks
        {
            add
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                lock (_eventLock)
                {
                    _recordedBasicNackEventHandlers += value;
                    _delegate.BasicNacks += value;
                }
            }
            remove
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                lock (_eventLock)
                {
                    _recordedBasicNackEventHandlers -= value;
                    _delegate.BasicNacks -= value;
                }
            }
        }

        public event EventHandler<EventArgs> BasicRecoverOk
        {
            add
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                // TODO: record and re-add handlers
                _delegate.BasicRecoverOk += value;
            }
            remove
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                _delegate.BasicRecoverOk -= value;
            }
        }

        public event EventHandler<BasicReturnEventArgs> BasicReturn
        {
            add
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                lock (_eventLock)
                {
                    _recordedBasicReturnEventHandlers += value;
                    _delegate.BasicReturn += value;
                }
            }
            remove
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                lock (_eventLock)
                {
                    _recordedBasicReturnEventHandlers -= value;
                    _delegate.BasicReturn -= value;
                }
            }
        }

        public event EventHandler<CallbackExceptionEventArgs> CallbackException
        {
            add
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                lock (_eventLock)
                {
                    _recordedCallbackExceptionEventHandlers += value;
                    _delegate.CallbackException += value;
                }
            }
            remove
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                lock (_eventLock)
                {
                    _recordedCallbackExceptionEventHandlers -= value;
                    _delegate.CallbackException -= value;
                }
            }
        }

        public event EventHandler<FlowControlEventArgs> FlowControl
        {
            add
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                // TODO: record and re-add handlers
                _delegate.FlowControl += value;
            }
            remove
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                _delegate.FlowControl -= value;
            }
        }

        public event EventHandler<ShutdownEventArgs> ModelShutdown
        {
            add
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                lock (_eventLock)
                {
                    _recordedShutdownEventHandlers += value;
                    _delegate.ModelShutdown += value;
                }
            }
            remove
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                lock (_eventLock)
                {
                    _recordedShutdownEventHandlers -= value;
                    _delegate.ModelShutdown -= value;
                }
            }
        }

        public event EventHandler<EventArgs> Recovery;

        public int ChannelNumber
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return _delegate.ChannelNumber;
            }
        }

        public ShutdownEventArgs CloseReason
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return _delegate.CloseReason;
            }
        }

        public IBasicConsumer DefaultConsumer
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return _delegate.DefaultConsumer;
            }
            set
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                _delegate.DefaultConsumer = value;
            }
        }

        public IModel Delegate
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return _delegate;
            }
        }

        public bool IsClosed
        {
            get
            {
                return !IsOpen;
            }
        }

        public bool IsOpen
        {
            get
            {
                if (_delegate == null)
                {
                    return false;
                }
                else
                {
                    return _delegate.IsOpen;
                }
            }
        }

        public ulong NextPublishSeqNo
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return _delegate.NextPublishSeqNo;
            }
        }

        public void AutomaticallyRecover(AutorecoveringConnection conn, bool recoverConsumers)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _connection = conn;
            RecoveryAwareModel defunctModel = _delegate;

            var newModel = conn.CreateNonRecoveringModel();
            newModel.InheritOffsetFrom(defunctModel);

            lock (_eventLock)
            {
                newModel.ModelShutdown += _recordedShutdownEventHandlers;
                newModel.BasicReturn += _recordedBasicReturnEventHandlers;
                newModel.BasicAcks += _recordedBasicAckEventHandlers;
                newModel.BasicNacks += _recordedBasicNackEventHandlers;
                newModel.CallbackException += _recordedCallbackExceptionEventHandlers;
            }

            if (_prefetchCountConsumer != 0)
            {
                newModel.BasicQos(0, _prefetchCountConsumer, false);
            }

            if (_prefetchCountGlobal != 0)
            {
                newModel.BasicQos(0, _prefetchCountGlobal, true);
            }

            if (_usesPublisherConfirms)
            {
                newModel.ConfirmSelect();
            }

            if (_usesTransactions)
            {
                newModel.TxSelect();
            }

            /*
             * https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1140
             * If this assignment is not done before recovering consumers, there is a good
             * chance that an invalid Model will be used to handle a basic.deliver frame,
             * with the resulting basic.ack never getting sent out.
             */
            _delegate = newModel;

            if (recoverConsumers)
            {
                _connection.RecoverConsumers(this, newModel);
            }

            RunRecoveryEventHandlers();
        }

        public void Close(ushort replyCode, string replyText, bool abort)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            try
            {
                _delegate.Close(replyCode, replyText, abort);
            }
            finally
            {
                _connection.UnregisterModel(this);
            }
        }

        public void Close(ShutdownEventArgs reason, bool abort)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            try
            {
                _delegate.Close(reason, abort).GetAwaiter().GetResult();
            }
            finally
            {
                _connection.UnregisterModel(this);
            }
        }

        public override string ToString()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return _delegate.ToString();
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                Abort();

                _recordedConsumerTags.Clear();
                _recordedConsumerTags = null;
                _connection = null;
                _delegate = null;
                _recordedBasicAckEventHandlers = null;
                _recordedBasicNackEventHandlers = null;
                _recordedBasicReturnEventHandlers = null;
                _recordedCallbackExceptionEventHandlers = null;
                _recordedShutdownEventHandlers = null;

                _disposed = true;
            }
        }

        public void ConnectionTuneOk(ushort channelMax,
            uint frameMax,
            ushort heartbeat)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.ConnectionTuneOk(channelMax, frameMax, heartbeat);
        }

        public void HandleBasicAck(ulong deliveryTag, bool multiple)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleBasicAck(deliveryTag, multiple);
        }

        public void HandleBasicCancel(string consumerTag, bool nowait)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleBasicCancel(consumerTag, nowait);
        }

        public void HandleBasicCancelOk(string consumerTag)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleBasicCancelOk(consumerTag);
        }

        public void HandleBasicConsumeOk(string consumerTag)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleBasicConsumeOk(consumerTag);
        }

        public void HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange,
                routingKey, basicProperties, body);
        }

        public void HandleBasicGetEmpty()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleBasicGetEmpty();
        }

        public void HandleBasicGetOk(ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            uint messageCount,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleBasicGetOk(deliveryTag, redelivered, exchange, routingKey,
                messageCount, basicProperties, body);
        }

        public void HandleBasicNack(ulong deliveryTag,
            bool multiple,
            bool requeue)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleBasicNack(deliveryTag, multiple, requeue);
        }

        public void HandleBasicRecoverOk()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleBasicRecoverOk();
        }

        public void HandleBasicReturn(ushort replyCode,
            string replyText,
            string exchange,
            string routingKey,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleBasicReturn(replyCode, replyText, exchange,
                routingKey, basicProperties, body);
        }

        public void HandleChannelClose(ushort replyCode,
            string replyText,
            ushort classId,
            ushort methodId)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleChannelClose(replyCode, replyText, classId, methodId);
        }

        public void HandleChannelCloseOk()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleChannelCloseOk();
        }

        public void HandleChannelFlow(bool active)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleChannelFlow(active);
        }

        public void HandleConnectionBlocked(string reason)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleConnectionBlocked(reason);
        }

        public void HandleConnectionClose(ushort replyCode,
            string replyText,
            ushort classId,
            ushort methodId)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleConnectionClose(replyCode, replyText, classId, methodId);
        }

        public void HandleConnectionOpenOk(string knownHosts)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleConnectionOpenOk(knownHosts);
        }

        public void HandleConnectionSecure(byte[] challenge)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleConnectionSecure(challenge);
        }

        public void HandleConnectionStart(byte versionMajor,
            byte versionMinor,
            IDictionary<string, object> serverProperties,
            byte[] mechanisms,
            byte[] locales)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleConnectionStart(versionMajor, versionMinor, serverProperties,
                mechanisms, locales);
        }

        public void HandleConnectionTune(ushort channelMax,
            uint frameMax,
            ushort heartbeat)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleConnectionTune(channelMax, frameMax, heartbeat);
        }

        public void HandleConnectionUnblocked()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleConnectionUnblocked();
        }

        public void HandleQueueDeclareOk(string queue,
            uint messageCount,
            uint consumerCount)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleQueueDeclareOk(queue, messageCount, consumerCount);
        }

        public void _Private_BasicCancel(string consumerTag,
            bool nowait)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_BasicCancel(consumerTag, nowait);
        }

        public void _Private_BasicConsume(string queue,
            string consumerTag,
            bool noLocal,
            bool autoAck,
            bool exclusive,
            bool nowait,
            IDictionary<string, object> arguments)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_BasicConsume(queue,
                consumerTag,
                noLocal,
                autoAck,
                exclusive,
                nowait,
                arguments);
        }

        public void _Private_BasicGet(string queue, bool autoAck)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_BasicGet(queue, autoAck);
        }

        public void _Private_BasicPublish(string exchange,
            string routingKey,
            bool mandatory,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body)
        {
            if (routingKey == null)
            {
                throw new ArgumentNullException(nameof(routingKey));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_BasicPublish(exchange, routingKey, mandatory,
                basicProperties, body);
        }

        public void _Private_BasicRecover(bool requeue)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_BasicRecover(requeue);
        }

        public void _Private_ChannelClose(ushort replyCode,
            string replyText,
            ushort classId,
            ushort methodId)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_ChannelClose(replyCode, replyText,
                classId, methodId);
        }

        public void _Private_ChannelCloseOk()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_ChannelCloseOk();
        }

        public void _Private_ChannelFlowOk(bool active)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_ChannelFlowOk(active);
        }

        public void _Private_ChannelOpen(string outOfBand)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_ChannelOpen(outOfBand);
        }

        public void _Private_ConfirmSelect(bool nowait)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_ConfirmSelect(nowait);
        }

        public void _Private_ConnectionClose(ushort replyCode,
            string replyText,
            ushort classId,
            ushort methodId)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_ConnectionClose(replyCode, replyText,
                classId, methodId);
        }

        public void _Private_ConnectionCloseOk()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_ConnectionCloseOk();
        }

        public void _Private_ConnectionOpen(string virtualHost,
            string capabilities,
            bool insist)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_ConnectionOpen(virtualHost, capabilities, insist);
        }

        public void _Private_ConnectionSecureOk(byte[] response)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_ConnectionSecureOk(response);
        }

        public void _Private_ConnectionStartOk(IDictionary<string, object> clientProperties,
            string mechanism, byte[] response, string locale)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_ConnectionStartOk(clientProperties, mechanism,
                response, locale);
        }

        public void _Private_UpdateSecret(byte[] newSecret, string reason)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_UpdateSecret(newSecret, reason);
        }

        public void _Private_ExchangeBind(string destination,
            string source,
            string routingKey,
            bool nowait,
            IDictionary<string, object> arguments)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_ExchangeBind(destination, source, routingKey,
                nowait, arguments);
        }

        public void _Private_ExchangeDeclare(string exchange,
            string type,
            bool passive,
            bool durable,
            bool autoDelete,
            bool @internal,
            bool nowait,
            IDictionary<string, object> arguments)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_ExchangeDeclare(exchange, type, passive,
                durable, autoDelete, @internal,
                nowait, arguments);
        }

        public void _Private_ExchangeDelete(string exchange,
            bool ifUnused,
            bool nowait)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_ExchangeDelete(exchange, ifUnused, nowait);
        }

        public void _Private_ExchangeUnbind(string destination,
            string source,
            string routingKey,
            bool nowait,
            IDictionary<string, object> arguments)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_ExchangeUnbind(destination, source, routingKey,
                nowait, arguments);
        }

        public void _Private_QueueBind(string queue,
            string exchange,
            string routingKey,
            bool nowait,
            IDictionary<string, object> arguments)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_QueueBind(queue, exchange, routingKey,
                nowait, arguments);
        }

        public void _Private_QueueDeclare(string queue,
            bool passive,
            bool durable,
            bool exclusive,
            bool autoDelete,
            bool nowait,
            IDictionary<string, object> arguments)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate._Private_QueueDeclare(queue, passive,
                durable, exclusive, autoDelete,
                nowait, arguments);
        }

        public uint _Private_QueueDelete(string queue,
            bool ifUnused,
            bool ifEmpty,
            bool nowait)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return _delegate._Private_QueueDelete(queue, ifUnused,
                ifEmpty, nowait);
        }

        public uint _Private_QueuePurge(string queue,
            bool nowait)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return _delegate._Private_QueuePurge(queue, nowait);
        }

        public void Abort()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            try
            {
                _delegate.Abort();
            }
            finally
            {
                _connection.UnregisterModel(this);
            }
        }

        public void Abort(ushort replyCode, string replyText)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            try
            {
                _delegate.Abort(replyCode, replyText);
            }
            finally
            {
                _connection.UnregisterModel(this);
            }
        }

        public void BasicAck(ulong deliveryTag,
            bool multiple)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.BasicAck(deliveryTag, multiple);
        }

        public void BasicCancel(string consumerTag)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            RecordedConsumer cons = _connection.DeleteRecordedConsumer(consumerTag);
            if (cons != null)
            {
                _connection.MaybeDeleteRecordedAutoDeleteQueue(cons.Queue);
            }
            _delegate.BasicCancel(consumerTag);
        }

        public void BasicCancelNoWait(string consumerTag)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            RecordedConsumer cons = _connection.DeleteRecordedConsumer(consumerTag);
            if (cons != null)
            {
                _connection.MaybeDeleteRecordedAutoDeleteQueue(cons.Queue);
            }
            _delegate.BasicCancelNoWait(consumerTag);
        }

        public string BasicConsume(
            string queue,
            bool autoAck,
            string consumerTag,
            bool noLocal,
            bool exclusive,
            IDictionary<string, object> arguments,
            IBasicConsumer consumer)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            string resultConsumerTag = _delegate.BasicConsume(queue, autoAck, consumerTag, noLocal,
                exclusive, arguments, consumer);
            RecordedConsumer rc = new RecordedConsumer(this, queue, resultConsumerTag).
                WithConsumer(consumer).
                WithExclusive(exclusive).
                WithAutoAck(autoAck).
                WithArguments(arguments);
            _connection.RecordConsumer(resultConsumerTag, rc);
            _recordedConsumerTags.Add(resultConsumerTag);
            return resultConsumerTag;
        }

        public BasicGetResult BasicGet(string queue,
            bool autoAck)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return _delegate.BasicGet(queue, autoAck);
        }

        public void BasicNack(ulong deliveryTag,
            bool multiple,
            bool requeue)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.BasicNack(deliveryTag, multiple, requeue);
        }

        public void BasicPublish(string exchange,
            string routingKey,
            bool mandatory,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body)
        {
            if (routingKey == null)
            {
                throw new ArgumentNullException(nameof(routingKey));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.BasicPublish(exchange,
                routingKey,
                mandatory,
                basicProperties,
                body);
        }

        public void BasicQos(uint prefetchSize,
            ushort prefetchCount,
            bool global)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            if (global)
            {
                _prefetchCountGlobal = prefetchCount;
            }
            else
            {
                _prefetchCountConsumer = prefetchCount;
            }
            _delegate.BasicQos(prefetchSize, prefetchCount, global);
        }

        public void BasicRecover(bool requeue)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.BasicRecover(requeue);
        }

        public void BasicRecoverAsync(bool requeue)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.BasicRecoverAsync(requeue);
        }

        public void BasicReject(ulong deliveryTag,
            bool requeue)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.BasicReject(deliveryTag, requeue);
        }

        public void Close()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            try
            {
                _delegate.Close();
            }
            finally
            {
                _connection.UnregisterModel(this);
            }
        }

        public void Close(ushort replyCode, string replyText)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            try
            {
                _delegate.Close(replyCode, replyText);
            }
            finally
            {
                _connection.UnregisterModel(this);
            }
        }

        public void ConfirmSelect()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _usesPublisherConfirms = true;
            _delegate.ConfirmSelect();
        }

        public IBasicProperties CreateBasicProperties()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return _delegate.CreateBasicProperties();
        }

        public void ExchangeBind(string destination,
            string source,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            RecordedBinding eb = new RecordedExchangeBinding().
                WithSource(source).
                WithDestination(destination).
                WithRoutingKey(routingKey).
                WithArguments(arguments);
            _connection.RecordBinding(eb);
            _delegate.ExchangeBind(destination, source, routingKey, arguments);
        }

        public void ExchangeBindNoWait(string destination,
            string source,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.ExchangeBindNoWait(destination, source, routingKey, arguments);
        }

        public void ExchangeDeclare(string exchange, string type, bool durable,
            bool autoDelete, IDictionary<string, object> arguments)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            RecordedExchange rx = new RecordedExchange(exchange).
                WithType(type).
                WithDurable(durable).
                WithAutoDelete(autoDelete).
                WithArguments(arguments);
            _delegate.ExchangeDeclare(exchange, type, durable,
                autoDelete, arguments);
            _connection.RecordExchange(exchange, rx);
        }

        public void ExchangeDeclareNoWait(string exchange,
            string type,
            bool durable,
            bool autoDelete,
            IDictionary<string, object> arguments)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            RecordedExchange rx = new RecordedExchange(exchange).
                WithType(type).
                WithDurable(durable).
                WithAutoDelete(autoDelete).
                WithArguments(arguments);
            _delegate.ExchangeDeclareNoWait(exchange, type, durable,
                autoDelete, arguments);
            _connection.RecordExchange(exchange, rx);
        }

        public void ExchangeDeclarePassive(string exchange)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.ExchangeDeclarePassive(exchange);
        }

        public void ExchangeDelete(string exchange,
            bool ifUnused)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.ExchangeDelete(exchange, ifUnused);
            _connection.DeleteRecordedExchange(exchange);
        }

        public void ExchangeDeleteNoWait(string exchange,
            bool ifUnused)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.ExchangeDeleteNoWait(exchange, ifUnused);
            _connection.DeleteRecordedExchange(exchange);
        }

        public void ExchangeUnbind(string destination,
            string source,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            RecordedBinding eb = new RecordedExchangeBinding().
                WithSource(source).
                WithDestination(destination).
                WithRoutingKey(routingKey).
                WithArguments(arguments);
            _connection.DeleteRecordedBinding(eb);
            _delegate.ExchangeUnbind(destination, source, routingKey, arguments);
            _connection.MaybeDeleteRecordedAutoDeleteExchange(source);
        }

        public void ExchangeUnbindNoWait(string destination,
            string source,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.ExchangeUnbind(destination, source, routingKey, arguments);
        }

        public void QueueBind(string queue,
            string exchange,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            RecordedBinding qb = new RecordedQueueBinding().
                WithSource(exchange).
                WithDestination(queue).
                WithRoutingKey(routingKey).
                WithArguments(arguments);
            _connection.RecordBinding(qb);
            _delegate.QueueBind(queue, exchange, routingKey, arguments);
        }

        public void QueueBindNoWait(string queue,
            string exchange,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.QueueBind(queue, exchange, routingKey, arguments);
        }

        public QueueDeclareOk QueueDeclare(string queue, bool durable,
                                           bool exclusive, bool autoDelete,
                                           IDictionary<string, object> arguments)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            QueueDeclareOk result = _delegate.QueueDeclare(queue, durable, exclusive,
                autoDelete, arguments);
            RecordedQueue rq = new RecordedQueue(result.QueueName).
                WithDurable(durable).
                WithExclusive(exclusive).
                WithAutoDelete(autoDelete).
                WithArguments(arguments).
                WithServerNamed(string.Empty.Equals(queue));
            _connection.RecordQueue(result.QueueName, rq);
            return result;
        }

        public void QueueDeclareNoWait(string queue, bool durable,
                                       bool exclusive, bool autoDelete,
                                       IDictionary<string, object> arguments)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.QueueDeclareNoWait(queue, durable, exclusive,
                autoDelete, arguments);
            RecordedQueue rq = new RecordedQueue(queue).
                WithDurable(durable).
                WithExclusive(exclusive).
                WithAutoDelete(autoDelete).
                WithArguments(arguments).
                WithServerNamed(string.Empty.Equals(queue));
            _connection.RecordQueue(queue, rq);
        }

        public QueueDeclareOk QueueDeclarePassive(string queue)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return _delegate.QueueDeclarePassive(queue);
        }

        public uint MessageCount(string queue)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return _delegate.MessageCount(queue);
        }

        public uint ConsumerCount(string queue)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return _delegate.ConsumerCount(queue);
        }

        public uint QueueDelete(string queue,
            bool ifUnused,
            bool ifEmpty)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            uint result = _delegate.QueueDelete(queue, ifUnused, ifEmpty);
            _connection.DeleteRecordedQueue(queue);
            return result;
        }

        public void QueueDeleteNoWait(string queue,
            bool ifUnused,
            bool ifEmpty)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.QueueDeleteNoWait(queue, ifUnused, ifEmpty);
            _connection.DeleteRecordedQueue(queue);
        }

        public uint QueuePurge(string queue)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return _delegate.QueuePurge(queue);
        }

        public void QueueUnbind(string queue,
            string exchange,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            RecordedBinding qb = new RecordedQueueBinding().
                WithSource(exchange).
                WithDestination(queue).
                WithRoutingKey(routingKey).
                WithArguments(arguments);
            _connection.DeleteRecordedBinding(qb);
            _delegate.QueueUnbind(queue, exchange, routingKey, arguments);
            _connection.MaybeDeleteRecordedAutoDeleteExchange(exchange);
        }

        public void TxCommit()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.TxCommit();
        }

        public void TxRollback()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.TxRollback();
        }

        public void TxSelect()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _usesTransactions = true;
            _delegate.TxSelect();
        }

        public bool WaitForConfirms(TimeSpan timeout, out bool timedOut)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return _delegate.WaitForConfirms(timeout, out timedOut);
        }

        public bool WaitForConfirms(TimeSpan timeout)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return _delegate.WaitForConfirms(timeout);
        }

        public bool WaitForConfirms()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return _delegate.WaitForConfirms();
        }

        public void WaitForConfirmsOrDie()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.WaitForConfirmsOrDie();
        }

        public void WaitForConfirmsOrDie(TimeSpan timeout)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.WaitForConfirmsOrDie(timeout);
        }

        private void RecoverBasicAckHandlers()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            lock (_eventLock)
            {
                _delegate.BasicAcks += _recordedBasicAckEventHandlers;
            }
        }

        private void RecoverBasicNackHandlers()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            lock (_eventLock)
            {
                _delegate.BasicNacks += _recordedBasicNackEventHandlers;
            }
        }

        private void RecoverBasicReturnHandlers()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            lock (_eventLock)
            {
                _delegate.BasicReturn += _recordedBasicReturnEventHandlers;
            }
        }

        private void RecoverCallbackExceptionHandlers()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            lock (_eventLock)
            {
                _delegate.CallbackException += _recordedCallbackExceptionEventHandlers;
            }
        }

        private void RunRecoveryEventHandlers()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            foreach (EventHandler<EventArgs> reh in Recovery?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try
                {
                    reh(this, EventArgs.Empty);
                }
                catch (Exception e)
                {
                    var args = new CallbackExceptionEventArgs(e);
                    args.Detail["context"] = "OnModelRecovery";
                    _delegate.OnCallbackException(args);
                }
            }
        }

        public IBasicPublishBatch CreateBasicPublishBatch()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return ((IFullModel)_delegate).CreateBasicPublishBatch();
        }
    }
}
