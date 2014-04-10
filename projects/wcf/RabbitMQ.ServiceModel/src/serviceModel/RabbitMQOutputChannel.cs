// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2014 GoPivotal, Inc.
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
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------


namespace RabbitMQ.ServiceModel
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.ServiceModel;
    using System.ServiceModel.Channels;

    using RabbitMQ.Client;

    internal sealed class RabbitMQOutputChannel : RabbitMQOutputChannelBase
    {
        private RabbitMQTransportBindingElement m_bindingElement;
        private MessageEncoder m_encoder;
        private IModel m_model;

        public RabbitMQOutputChannel(BindingContext context, IModel model, EndpointAddress address)
            : base(context, address)
        {
            m_bindingElement = context.Binding.Elements.Find<RabbitMQTransportBindingElement>();
            MessageEncodingBindingElement encoderElement = context.Binding.Elements.Find<MessageEncodingBindingElement>();
            if (encoderElement != null) {
                m_encoder = encoderElement.CreateMessageEncoderFactory().Encoder;
            }
            m_model = model;
        }

        public override void Send(Message message, TimeSpan timeout)
        {
            if (message.State != MessageState.Closed)
            {
                byte[] body = null;
#if VERBOSE
                DebugHelper.Start();
#endif
                using (MemoryStream str = new MemoryStream())
                {
                    m_encoder.WriteMessage(message, str);
                    body = str.ToArray();
                }
#if VERBOSE
                DebugHelper.Stop(" #### Message.Send {{\n\tAction={2}, \n\tBytes={1}, \n\tTime={0}ms}}.",
                    body.Length,
                    message.Headers.Action.Remove(0, message.Headers.Action.LastIndexOf('/')));
#endif
                m_model.BasicPublish(Exchange,
                                   base.RemoteAddress.Uri.PathAndQuery,
                                   null,
                                   body);
            }
        }

        public override void Close(TimeSpan timeout)
        {
            if (base.State == CommunicationState.Closed || base.State == CommunicationState.Closing)
                return; // Ignore the call, we're already closing.

            OnClosing();
            OnClosed();
        }

        public override void Open(TimeSpan timeout)
        {
            if (base.State != CommunicationState.Created && base.State != CommunicationState.Closed)
                throw new InvalidOperationException(string.Format("Cannot open the channel from the {0} state.", base.State));

            OnOpening();
            OnOpened();
        }
    }
}
