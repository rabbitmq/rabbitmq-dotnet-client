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
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.ServiceModel.Description;

    internal abstract class RabbitMQChannelListenerBase<TChannel> : ChannelListenerBase<TChannel> where TChannel: class, IChannel
    {
        private Uri listenUri;
        private BindingContext context;
        protected RabbitMQTransportBindingElement bindingElement;
        private CommunicationOperation closeMethod;
        private CommunicationOperation openMethod;
        private CommunicationOperation<TChannel> acceptChannelMethod;
        private CommunicationOperation<bool> waitForChannelMethod;

        protected RabbitMQChannelListenerBase(BindingContext context)
        {
            this.context = context;
            this.bindingElement = context.Binding.Elements.Find<RabbitMQTransportBindingElement>();
            this.closeMethod = new CommunicationOperation(OnClose);
            this.openMethod = new CommunicationOperation(OnOpen);
            this.waitForChannelMethod = new CommunicationOperation<bool>(OnWaitForChannel);
            this.acceptChannelMethod = new CommunicationOperation<TChannel>(OnAcceptChannel);
            
            if (context.ListenUriMode == ListenUriMode.Explicit && context.ListenUriBaseAddress != null)
            {
                this.listenUri = new Uri(context.ListenUriBaseAddress, context.ListenUriRelativeAddress);
            }
            else
            {
                this.listenUri = new Uri(new Uri("soap.amqp:///"), Guid.NewGuid().ToString());
            }

        }

        protected override void OnAbort()
        {
            OnClose(context.Binding.CloseTimeout);
        }

        protected override IAsyncResult OnBeginAcceptChannel(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return acceptChannelMethod.BeginInvoke(timeout, callback, state);
        }

        protected override TChannel OnEndAcceptChannel(IAsyncResult result)
        {
            return acceptChannelMethod.EndInvoke(result);
        }

        protected override IAsyncResult OnBeginWaitForChannel(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return waitForChannelMethod.BeginInvoke(timeout, callback, state);
        }

        protected override bool OnEndWaitForChannel(IAsyncResult result)
        {
            return waitForChannelMethod.EndInvoke(result);
        }
        
        protected override IAsyncResult OnBeginClose(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return closeMethod.BeginInvoke(timeout, callback, state);
        }

        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return openMethod.BeginInvoke(timeout, callback, state);
        }

        protected override void OnEndClose(IAsyncResult result)
        {
            closeMethod.EndInvoke(result);
        }

        protected override void OnEndOpen(IAsyncResult result)
        {
            openMethod.EndInvoke(result);
        }
            
        
        public override Uri Uri
        {
            get { return listenUri; }
        }

        protected BindingContext Context
        {
            get { return context; }
        }
    }
}
