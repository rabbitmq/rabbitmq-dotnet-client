// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2016 Pivotal Software, Inc.
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
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at http://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;

using System.Threading;
using System.Threading.Tasks;

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace RabbitMQ.Client.MessagePatterns
{
    ///<summary>Manages a subscription to a queue.</summary>
    ///<remarks>
    ///<para>
    /// This convenience class abstracts away from much of the detail
    /// involved in receiving messages from a queue.
    ///</para>
    ///<para>
    /// Once created, the Subscription consumes from a queue (using a
    /// EventingBasicConsumer). Received deliveries can be retrieved
    /// by calling Next(), or by using the Subscription as an
    /// IEnumerator in, for example, a foreach loop.
    ///</para>
    ///<para>
    /// Note that if the "autoAck" option is enabled (which it is by
    /// default), then received deliveries are automatically acked
    /// within the server before they are even transmitted across the
    /// network to us. Calling Ack() on received events will always do
    /// the right thing: if "autoAck" is enabled, nothing is done on an
    /// Ack() call, and if "autoAck" is disabled, IModel.BasicAck() is
    /// called with the correct parameters.
    ///</para>
    ///</remarks>
    public class Subscription : ISubscription
    {
        protected readonly object m_eventLock = new object();
        protected volatile EventingBasicConsumer m_consumer;
        private BlockingCollection<BasicDeliverEventArgs> m_queue = 
            new BlockingCollection<BasicDeliverEventArgs>(new ConcurrentQueue<BasicDeliverEventArgs>());

        private CancellationTokenSource m_queueCts = new CancellationTokenSource();

#if NETFX_CORE || NET4
        private ConcurrentQueue<TaskCompletionSource<BasicDeliverEventArgs>> m_waiting = 
            new ConcurrentQueue<TaskCompletionSource<BasicDeliverEventArgs>>();
#endif
        ///<summary>Creates a new Subscription in "autoAck" mode,
        ///consuming from a named queue.</summary>
        public Subscription(IModel model, string queueName)
            : this(model, queueName, true)
        {
        }

        ///<summary>Creates a new Subscription, with full control over
        ///both "autoAck" mode and the name of the queue.</summary>
        public Subscription(IModel model, string queueName, bool autoAck)
        {
            Model = model;
            QueueName = queueName;
            AutoAck = autoAck;
            m_consumer = new EventingBasicConsumer(Model);
#if NETFX_CORE || NET4
            m_consumer.Received += (sender, args) => QueueAdd(args); 
#else
            m_consumer.Received += (sender, args) => m_queue.Add(args); 
#endif
            ConsumerTag = Model.BasicConsume(QueueName, AutoAck, m_consumer);
            m_consumer.ConsumerCancelled += HandleConsumerCancelled;
            LatestEvent = null;
        }

        ///<summary>Creates a new Subscription, with full control over
        ///both "autoAck" mode, the name of the queue, and the consumer tag.</summary>
        public Subscription(IModel model, string queueName, bool autoAck, string consumerTag)
        {
            Model = model;
            QueueName = queueName;
            AutoAck = autoAck;
            m_consumer = new EventingBasicConsumer(Model);
            m_consumer.ConsumerCancelled += HandleConsumerCancelled;
            m_consumer.Received += (sender, args) => m_queue.Add(args);
            ConsumerTag = Model.BasicConsume(QueueName, AutoAck, consumerTag, m_consumer);
            LatestEvent = null;
        }

        ///<summary>Retrieve the IBasicConsumer that is receiving the
        ///messages from the server for us. Normally, you will not
        ///need to access this property - use Next() and friends
        ///instead.</summary>
        public IBasicConsumer Consumer
        {
            get { return m_consumer; }
        }

        ///<summary>Retrieve the consumer-tag that this subscription
        ///is using. Will usually be a server-generated
        ///name.</summary>
        public string ConsumerTag { get; protected set; }

        ///<summary>Returns the most recent value returned by Next(),
        ///or null when either no values have been retrieved yet, the
        ///end of the subscription has been reached, or the most
        ///recent value has already been Ack()ed. See also the
        ///documentation for Ack().</summary>
        public BasicDeliverEventArgs LatestEvent { get; protected set; }

        ///<summary>Retrieve the IModel our subscription is carried by.</summary>
        public IModel Model { get; protected set; }

        ///<summary>Returns true if we are in "autoAck" mode, where
        ///calls to Ack() will be no-ops, and where the server acks
        ///messages before they are delivered to us. Returns false if
        ///we are in a mode where calls to Ack() are required, and
        ///where such calls will actually send an acknowledgement
        ///message across the network to the server.</summary>
        public bool AutoAck { get; protected set; }

        ///<summary>Retrieve the queue name we have subscribed to.</summary>
        public string QueueName { get; protected set; }

        ///<summary>Implementation of the IEnumerator interface, for
        ///permitting Subscription to be used in foreach
        ///loops.</summary>
        ///<remarks>
        ///<para>
        /// As per the IEnumerator interface definition, throws
        /// InvalidOperationException if LatestEvent is null.
        ///</para>
        ///<para>
        /// Does not acknowledge any deliveries at all. Ack() must be
        /// called explicitly on received deliveries.
        ///</para>
        ///</remarks>
        object IEnumerator.Current
        {
            get
            {
                if (LatestEvent == null)
                {
                    throw new InvalidOperationException();
                }
                return LatestEvent;
            }
        }

        ///<summary>If LatestEvent is non-null, passes it to
        ///Ack(BasicDeliverEventArgs). Causes LatestEvent to become
        ///null.</summary>
        public void Ack()
        {
            Ack(LatestEvent);
        }

        ///<summary>If we are not in "autoAck" mode, calls
        ///IModel.BasicAck with the delivery-tag from <paramref name="evt"/>;
        ///otherwise, sends nothing to the server. if <paramref name="evt"/> is the same as LatestEvent
        ///by pointer comparison, sets LatestEvent to null.
        ///</summary>
        ///<remarks>
        ///Passing an event that did not originate with this Subscription's
        /// channel, will lead to unpredictable behaviour
        ///</remarks>
        public void Ack(BasicDeliverEventArgs evt)
        {
            if (evt == null)
            {
                return;
            }

            if (!AutoAck && Model.IsOpen)
            {
                Model.BasicAck(evt.DeliveryTag, false);
            }

            if (evt == LatestEvent)
            {
                MutateLatestEvent(null);
            }
        }

        ///<summary>Closes this Subscription, cancelling the consumer
        ///record in the server.</summary>
        public void Close()
        {
            try
            {
                bool shouldCancelConsumer = false;
                if (m_consumer != null)
                {
                    shouldCancelConsumer = m_consumer.IsRunning;
                    m_consumer = null;
                }

                if (shouldCancelConsumer)
                {
                    if (Model.IsOpen)
                    {
                        Model.BasicCancel(ConsumerTag);
                    }

                    ConsumerTag = null;
                }

                m_queueCts.Cancel(true);
                if(m_queue != null)
                {
                    m_queue.Dispose();
                    m_queue = null;
                }
#if NETFX_CORE || NET4
                var exn = new EndOfStreamException("Subscription closed");
                foreach (var tsc in m_waiting)
                {
                    tsc.TrySetException(exn);
                }
#endif
            }
            catch (OperationInterruptedException)
            {
                // We don't mind, here.
            }
        }

        ///<summary>If LatestEvent is non-null, passes it to
        ///Nack(BasicDeliverEventArgs, false, requeue). Causes LatestEvent to become
        ///null.</summary>
        public void Nack(bool requeue)
        {
            Nack(LatestEvent, false, requeue);
        }

        ///<summary>If LatestEvent is non-null, passes it to
        ///Nack(BasicDeliverEventArgs, multiple, requeue). Causes LatestEvent to become
        ///null.</summary>
        public void Nack(bool multiple, bool requeue)
        {
            Nack(LatestEvent, multiple, requeue);
        }

        ///<summary>If we are not in "autoAck" mode, calls
        ///IModel.BasicNack with the delivery-tag from <paramref name="evt"/>;
        ///otherwise, sends nothing to the server. if <paramref name="evt"/> is the same as LatestEvent
        ///by pointer comparison, sets LatestEvent to null.
        ///</summary>
        ///<remarks>
        ///Passing an event that did not originate with this Subscription's
        /// channel, will lead to unpredictable behaviour
        ///</remarks>
        public void Nack(BasicDeliverEventArgs evt, bool multiple, bool requeue)
        {
            if (evt == null)
            {
                return;
            }

            if (!AutoAck && Model.IsOpen)
            {
                Model.BasicNack(evt.DeliveryTag, multiple, requeue);
            }

            if (evt == LatestEvent)
            {
                MutateLatestEvent(null);
            }
        }

        ///<summary>Retrieves the next incoming delivery in our
        ///subscription queue.</summary>
        ///<remarks>
        ///<para>
        /// Returns null when the end of the stream is reached and on
        /// every subsequent call. End-of-stream can arise through the
        /// action of the Subscription.Close() method, or through the
        /// closure of the IModel or its underlying IConnection.
        ///</para>
        ///<para>
        /// Updates LatestEvent to the value returned.
        ///</para>
        ///<para>
        /// Does not acknowledge any deliveries at all (but in "autoAck"
        /// mode, the server will have auto-acknowledged each event
        /// before it is even sent across the wire to us).
        ///</para>
        ///</remarks>
        public BasicDeliverEventArgs Next()
        {
            // Alias the pointer as otherwise it may change out
            // from under us by the operation of Close() from
            // another thread.
            EventingBasicConsumer consumer = m_consumer;
            try
            {
                if (consumer == null || Model.IsClosed)
                {
                    MutateLatestEvent(null);
                }
                else
                {
                    BasicDeliverEventArgs bdea = m_queue.Take(m_queueCts.Token);
                    MutateLatestEvent(bdea);
                }
            }
            catch (EndOfStreamException)
            {
                MutateLatestEvent(null);
            }
            return LatestEvent;
        }

#if NETFX_CORE || NET4
        public Task<BasicDeliverEventArgs> NextAsync() 
        {
            try 
            {
                // Alias the pointer as otherwise it may change out
                // from under us by the operation of Close() from
                // another thread.
                var queue = m_queue;
                if (queue == null || Model.IsClosed) 
                {
                    // Closed!
                    MutateLatestEvent(null);
                }
                else 
                {
                    BasicDeliverEventArgs evt = null;
                    if(queue.TryTake(out evt))
                    {
                        MutateLatestEvent(evt);
                    }
                    else
                    {
                        var tcs = new TaskCompletionSource<BasicDeliverEventArgs>();
                        m_waiting.Enqueue(tcs);
                        return tcs.Task;
                    }
                }
            }
            catch (AggregateException ex) 
            {
                // since tasks wrap exceptions as AggregateException, 
                // reach in and check if the EndOfStream exception is what happened
                if (ex.InnerException is EndOfStreamException) 
                {
                    MutateLatestEvent(null);
                }
            }
            catch (EndOfStreamException) 
            {
                MutateLatestEvent(null);
            }

            return Task.FromResult(LatestEvent);
        }
#endif

        ///<summary>Retrieves the next incoming delivery in our
        ///subscription queue, or times out after a specified number
        ///of milliseconds.</summary>
        ///<remarks>
        ///<para>
        /// Returns false only if the timeout expires before either a
        /// delivery appears or the end-of-stream is reached. If false
        /// is returned, the out parameter "result" is set to null,
        /// but LatestEvent is not updated.
        ///</para>
        ///<para>
        /// Returns true to indicate a delivery or the end-of-stream.
        ///</para>
        ///<para>
        /// If a delivery is already waiting in the queue, or one
        /// arrives before the timeout expires, it is removed from the
        /// queue and placed in the "result" out parameter. If the
        /// end-of-stream is detected before the timeout expires,
        /// "result" is set to null.
        ///</para>
        ///<para>
        /// Whenever this method returns true, it updates LatestEvent
        /// to the value placed in "result" before returning.
        ///</para>
        ///<para>
        /// End-of-stream can arise through the action of the
        /// Subscription.Close() method, or through the closure of the
        /// IModel or its underlying IConnection.
        ///</para>
        ///<para>
        /// This method does not acknowledge any deliveries at all
        /// (but in "autoAck" mode, the server will have
        /// auto-acknowledged each event before it is even sent across
        /// the wire to us).
        ///</para>
        ///<para>
        /// A timeout of -1 (i.e. System.Threading.Timeout.Infinite)
        /// will be interpreted as a command to wait for an
        /// indefinitely long period of time for an item or the end of
        /// the stream to become available. Usage of such a timeout is
        /// equivalent to calling Next() with no arguments (modulo
        /// predictable method signature differences).
        ///</para>
        ///</remarks>
        public bool Next(int millisecondsTimeout, out BasicDeliverEventArgs result)
        {
            try
            {
                // Alias the pointer as otherwise it may change out
                // from under us by the operation of Close() from
                // another thread.
                var consumer = m_consumer;
                if (consumer == null || Model.IsClosed)
                {
                    MutateLatestEvent(null);
                    result = null;
                    return false;
                }
                else
                {
                    BasicDeliverEventArgs qValue;
                    if (!m_queue.TryTake(out qValue, millisecondsTimeout))
                    {
                        result = null;
                        return false;
                    }
                    MutateLatestEvent(qValue);
                }
            }
            catch (EndOfStreamException)
            {
                MutateLatestEvent(null);
            }
            result = LatestEvent;
            return true;
        }

        ///<summary>Implementation of the IDisposable interface,
        ///permitting Subscription to be used in using
        ///statements. Simply calls Close().</summary>
        void IDisposable.Dispose()
        {
            Close();
        }

        ///<summary>Implementation of the IEnumerable interface, for
        ///permitting Subscription to be used in foreach
        ///loops.</summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this;
        }

        ///<summary>Implementation of the IEnumerator interface, for
        ///permitting Subscription to be used in foreach
        ///loops.</summary>
        ///<remarks>
        ///<para>
        /// Does not acknowledge any deliveries at all. Ack() must be
        /// called explicitly on received deliveries.
        ///</para>
        ///</remarks>
        bool IEnumerator.MoveNext()
        {
            return Next() != null;
        }

        ///<summary>Dummy implementation of the IEnumerator interface,
        ///for permitting Subscription to be used in foreach loops;
        ///Reset()ting a Subscription doesn't make sense, so this
        ///method always throws InvalidOperationException.</summary>
        void IEnumerator.Reset()
        {
            // It really doesn't make sense to try to reset a subscription.
            throw new InvalidOperationException("Subscription.Reset() does not make sense");
        }

        protected void MutateLatestEvent(BasicDeliverEventArgs value)
        {
            lock (m_eventLock)
            {
                LatestEvent = value;
            }
        }

        private void HandleConsumerCancelled(object sender, ConsumerEventArgs e)
        {
            lock (m_eventLock)
            {
                m_consumer = null;
                MutateLatestEvent(null);
            }
        }

#if NETFX_CORE || NET4
        private void QueueAdd(BasicDeliverEventArgs args)
        {
            //NB: as long as there are async awaiters sync callers will never be served
            //this is not ideal but consistent with how SharedQueue behaves
            TaskCompletionSource<BasicDeliverEventArgs> tsc;
            if(m_waiting.TryDequeue(out tsc) && tsc.TrySetResult(args))
            {
                return;
            }
            else
            {
                m_queue.Add(args);
            }
        }
#endif
    }
}
