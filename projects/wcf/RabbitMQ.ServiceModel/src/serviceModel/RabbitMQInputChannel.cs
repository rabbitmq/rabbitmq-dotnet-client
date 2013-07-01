// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2013 GoPivotal, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
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
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2013 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------


namespace RabbitMQ.ServiceModel
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.ServiceModel;
    using System.ServiceModel.Channels;

    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;

    // We use spec version 0-9 for common constants such as frame types,
    // error codes, and the frame end byte, since they don't vary *within
    // the versions we support*. Obviously we may need to revisit this if
    // that ever changes.
    using CommonFraming = RabbitMQ.Client.Framing.v0_9;

    internal sealed class RabbitMQInputChannel : RabbitMQInputChannelBase
    {
        private RabbitMQTransportBindingElement m_bindingElement;
        private MessageEncoder m_encoder;
        private IModel m_model;
        private QueueingBasicConsumer m_messageQueue;

        public RabbitMQInputChannel(BindingContext context, IModel model, EndpointAddress address)
            : base(context, address)
        {
            m_bindingElement = context.Binding.Elements.Find<RabbitMQTransportBindingElement>();
            TextMessageEncodingBindingElement encoderElem = context.BindingParameters.Find<TextMessageEncodingBindingElement>();
            encoderElem.ReaderQuotas.MaxStringContentLength = (int)m_bindingElement.MaxReceivedMessageSize;
            if (encoderElem != null) {
                m_encoder = encoderElem.CreateMessageEncoderFactory().Encoder;
            }
            m_model = model;
            m_messageQueue = null;
        }


        public override Message Receive(TimeSpan timeout)
        {
            try
            {
                BasicDeliverEventArgs msg = m_messageQueue.Queue.Dequeue() as BasicDeliverEventArgs;
#if VERBOSE
                DebugHelper.Start();
#endif
                Message result = m_encoder.ReadMessage(new MemoryStream(msg.Body), (int)m_bindingElement.MaxReceivedMessageSize);
                result.Headers.To = base.LocalAddress.Uri;
                m_messageQueue.Model.BasicAck(msg.DeliveryTag, false);
#if VERBOSE
                DebugHelper.Stop(" #### Message.Receive {{\n\tAction={2}, \n\tBytes={1}, \n\tTime={0}ms}}.",
                        msg.Body.Length,
                        result.Headers.Action.Remove(0, result.Headers.Action.LastIndexOf('/')));
#endif
                return result;
            }
            catch (EndOfStreamException)
            {
                if (m_messageQueue== null || m_messageQueue.ShutdownReason != null && m_messageQueue.ShutdownReason.ReplyCode != CommonFraming.Constants.ReplySuccess)
                {
                    OnFaulted();
                }
                Close();
                return null;
            }
        }

        public override bool TryReceive(TimeSpan timeout, out Message message)
        {
            message = Receive(timeout);
            return true;
        }

        public override bool WaitForMessage(TimeSpan timeout)
        {
            throw new NotImplementedException();
        }

        public override void Close(TimeSpan timeout)
        {

            if (base.State == CommunicationState.Closed
                || base.State == CommunicationState.Closing)
            {
                return; // Ignore the call, we're already closing.
            }

            OnClosing();
#if VERBOSE
            DebugHelper.Start();
#endif
            if (m_messageQueue != null) {
                m_model.BasicCancel(m_messageQueue.ConsumerTag);
                m_messageQueue = null;
            }
#if VERBOSE
            DebugHelper.Stop(" ## In.Channel.Close {{\n\tAddress={1}, \n\tTime={0}ms}}.", LocalAddress.Uri.PathAndQuery);
#endif
            OnClosed();
        }

        public override void Open(TimeSpan timeout)
        {
            if (State != CommunicationState.Created && State != CommunicationState.Closed)
                throw new InvalidOperationException(string.Format("Cannot open the channel from the {0} state.", base.State));

            OnOpening();
#if VERBOSE
            DebugHelper.Start();
#endif
            //Create a queue for messages destined to this service, bind it to the service URI routing key
            string queue = m_model.QueueDeclare();
            m_model.QueueBind(queue, Exchange, base.LocalAddress.Uri.PathAndQuery, null);

            //Listen to the queue
            m_messageQueue = new QueueingBasicConsumer(m_model);
            m_model.BasicConsume(queue, false, m_messageQueue);

#if VERBOSE
            DebugHelper.Stop(" ## In.Channel.Open {{\n\tAddress={1}, \n\tTime={0}ms}}.", LocalAddress.Uri.PathAndQuery);
#endif
            OnOpened();
        }
    }
}
