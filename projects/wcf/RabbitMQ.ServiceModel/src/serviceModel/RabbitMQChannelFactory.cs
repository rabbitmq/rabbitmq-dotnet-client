// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2011 VMware, Inc.
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
//  The Initial Developer of the Original Code is VMware, Inc.
//  Copyright (c) 2007-2011 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------


namespace RabbitMQ.ServiceModel
{
    using System;
    using System.ServiceModel;
    using System.ServiceModel.Channels;

    using RabbitMQ.Client;

    internal sealed class RabbitMQChannelFactory<TChannel> : ChannelFactoryBase<IOutputChannel>
    {
        private BindingContext m_context;
        private CommunicationOperation m_openMethod;
        private RabbitMQTransportBindingElement m_bindingElement;
        private IModel m_model;

        public RabbitMQChannelFactory(BindingContext context)
        {
            m_context = context;
            m_openMethod = new CommunicationOperation(Open);
            m_bindingElement = context.Binding.Elements.Find<RabbitMQTransportBindingElement>();
            m_model = null;
        }

        protected override IOutputChannel OnCreateChannel(EndpointAddress address, Uri via)
        {
            return new RabbitMQOutputChannel(m_context, m_model, address);
        }
        
        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return m_openMethod.BeginInvoke(timeout, callback, state);
        }

        protected override void OnEndOpen(IAsyncResult result)
        {
            m_openMethod.EndInvoke(result);
        }

        protected override void OnOpen(TimeSpan timeout)
        {
#if VERBOSE
            DebugHelper.Start();
#endif
            m_model = m_bindingElement.Open(timeout);
#if VERBOSE
            DebugHelper.Stop(" ## Out.Open {{Time={0}ms}}.");
#endif
        }

        protected override void OnClose(TimeSpan timeout)
        {
#if VERBOSE
            DebugHelper.Start();
#endif
            
            if (m_model != null) {
                m_bindingElement.Close(m_model, timeout);
                m_model = null;
            }

#if VERBOSE
            DebugHelper.Stop(" ## Out.Close {{Time={0}ms}}.");
#endif
        }

        protected override void OnAbort()
        {
            base.OnAbort();
            OnClose(m_context.Binding.CloseTimeout);
        }
    }
}
