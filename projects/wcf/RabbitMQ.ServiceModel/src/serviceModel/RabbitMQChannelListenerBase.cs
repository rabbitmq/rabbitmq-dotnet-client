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
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.ServiceModel.Description;

    internal abstract class RabbitMQChannelListenerBase<TChannel> : ChannelListenerBase<TChannel> where TChannel: class, IChannel
    {
        private Uri m_listenUri;
        private BindingContext m_context;
        protected RabbitMQTransportBindingElement m_bindingElement;
        private CommunicationOperation m_closeMethod;
        private CommunicationOperation m_openMethod;
        private CommunicationOperation<TChannel> m_acceptChannelMethod;
        private CommunicationOperation<bool> m_waitForChannelMethod;

        protected RabbitMQChannelListenerBase(BindingContext context)
        {
            m_context = context;
            m_bindingElement = context.Binding.Elements.Find<RabbitMQTransportBindingElement>();
            m_closeMethod = new CommunicationOperation(OnClose);
            m_openMethod = new CommunicationOperation(OnOpen);
            m_waitForChannelMethod = new CommunicationOperation<bool>(OnWaitForChannel);
            m_acceptChannelMethod = new CommunicationOperation<TChannel>(OnAcceptChannel);
            
            if (context.ListenUriMode == ListenUriMode.Explicit && context.ListenUriBaseAddress != null)
            {
                m_listenUri = new Uri(context.ListenUriBaseAddress, context.ListenUriRelativeAddress);
            }
            else
            {
                m_listenUri = new Uri(new Uri("soap.amqp:///"), Guid.NewGuid().ToString());
            }

        }

        protected override void OnAbort()
        {
            OnClose(m_context.Binding.CloseTimeout);
        }

        protected override IAsyncResult OnBeginAcceptChannel(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return m_acceptChannelMethod.BeginInvoke(timeout, callback, state);
        }

        protected override TChannel OnEndAcceptChannel(IAsyncResult result)
        {
            return m_acceptChannelMethod.EndInvoke(result);
        }

        protected override IAsyncResult OnBeginWaitForChannel(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return m_waitForChannelMethod.BeginInvoke(timeout, callback, state);
        }

        protected override bool OnEndWaitForChannel(IAsyncResult result)
        {
            return m_waitForChannelMethod.EndInvoke(result);
        }
        
        protected override IAsyncResult OnBeginClose(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return m_closeMethod.BeginInvoke(timeout, callback, state);
        }

        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return m_openMethod.BeginInvoke(timeout, callback, state);
        }

        protected override void OnEndClose(IAsyncResult result)
        {
            m_closeMethod.EndInvoke(result);
        }

        protected override void OnEndOpen(IAsyncResult result)
        {
            m_openMethod.EndInvoke(result);
        }
            
        
        public override Uri Uri
        {
            get { return m_listenUri; }
        }

        protected BindingContext Context
        {
            get { return m_context; }
        }
    }
}
