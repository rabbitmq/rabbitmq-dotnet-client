// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007, 2008 LShift Ltd., Cohesive Financial
//   Technologies LLC., and Rabbit Technologies Ltd.
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
//   The contents of this file are subject to the Mozilla Public License
//   Version 1.1 (the "License"); you may not use this file except in
//   compliance with the License. You may obtain a copy of the License at
//   http://www.rabbitmq.com/mpl.html
//
//   Software distributed under the License is distributed on an "AS IS"
//   basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
//   License for the specific language governing rights and limitations
//   under the License.
//
//   The Original Code is The RabbitMQ .NET Client.
//
//   The Initial Developers of the Original Code are LShift Ltd.,
//   Cohesive Financial Technologies LLC., and Rabbit Technologies Ltd.
//
//   Portions created by LShift Ltd., Cohesive Financial Technologies
//   LLC., and Rabbit Technologies Ltd. are Copyright (C) 2007, 2008
//   LShift Ltd., Cohesive Financial Technologies LLC., and Rabbit
//   Technologies Ltd.;
//
//   All Rights Reserved.
//
//   Contributor(s): ______________________________________.
//
//---------------------------------------------------------------------------
//------------------------------------------------------
// Copyright (c) LShift Ltd. All Rights Reserved
//------------------------------------------------------

namespace RabbitMQ.ServiceModel
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.ServiceModel;
    using System.ServiceModel.Channels;

    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;

    internal sealed class RabbitMQInputChannel : RabbitMQInputChannelBase
    {
        private RabbitMQTransportBindingElement bindingElement;
        private MessageEncoder encoder;
        private IModel model;
        private ushort ticket;
        private QueueingBasicConsumer messageQueue;

        public RabbitMQInputChannel(BindingContext context, IModel model, ushort ticket, EndpointAddress address)
            : base(context, address)
        {
            this.bindingElement = context.Binding.Elements.Find<RabbitMQTransportBindingElement>();
            MessageEncodingBindingElement encoderElem = context.BindingParameters.Find<MessageEncodingBindingElement>();
            if (encoderElem != null) {
                this.encoder = encoderElem.CreateMessageEncoderFactory().Encoder;
            }
            this.model = model;
            this.ticket = ticket;
            this.messageQueue = null;
        }


        public override Message Receive(TimeSpan timeout)
        {
            try
            {
                BasicDeliverEventArgs msg = messageQueue.Queue.Dequeue() as BasicDeliverEventArgs;
#if VERBOSE
                DebugHelper.Start();
#endif
                Message result = encoder.ReadMessage(new MemoryStream(msg.Body), (int)bindingElement.MaxReceivedMessageSize);
                result.Headers.To = base.LocalAddress.Uri;
                messageQueue.Model.BasicAck(msg.DeliveryTag, false);
#if VERBOSE
                DebugHelper.Stop(" #### Message.Receive {{\n\tAction={2}, \n\tBytes={1}, \n\tTime={0}ms}}.",
                        msg.Body.Length,
                        result.Headers.Action.Remove(0, result.Headers.Action.LastIndexOf('/')));
#endif
                return result;
            }
            catch (EndOfStreamException)
            {
                if (messageQueue== null || messageQueue.ShutdownReason != null && messageQueue.ShutdownReason.ReplyCode != 200)
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
            if (messageQueue != null) {
                model.BasicCancel(messageQueue.ConsumerTag);
                messageQueue = null;
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
            string queue = model.QueueDeclare(ticket);
            model.QueueBind(ticket, queue, base.LocalAddress.Uri.Host, base.LocalAddress.Uri.PathAndQuery, false, null);

            //Listen to the queue
            messageQueue = new QueueingBasicConsumer(model);
            model.BasicConsume(ticket, queue, null, messageQueue);

#if VERBOSE
            DebugHelper.Stop(" ## In.Channel.Open {{\n\tAddress={1}, \n\tTime={0}ms}}.", LocalAddress.Uri.PathAndQuery);
#endif
            OnOpened();
        }
    }
}
