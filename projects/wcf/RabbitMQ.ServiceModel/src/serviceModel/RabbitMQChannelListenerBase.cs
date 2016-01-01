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
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------


using System;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

namespace RabbitMQ.ServiceModel
{
    internal abstract class RabbitMQChannelListenerBase<TChannel> : ChannelListenerBase<TChannel>
        where TChannel : class, IChannel
    {
        private readonly Func<TimeSpan, TChannel> _acceptChannelMethod;
        private readonly Action<TimeSpan> _closeMethod;
        private readonly BindingContext _context;
        private readonly Uri _listenUri;
        private readonly Action<TimeSpan> _openMethod;
        private readonly Func<TimeSpan, bool> _waitForChannelMethod;
        protected RabbitMQTransportBindingElement m_bindingElement;

        protected RabbitMQChannelListenerBase(BindingContext context)
        {
            _context = context;
            m_bindingElement = context.Binding.Elements.Find<RabbitMQTransportBindingElement>();
            _closeMethod = OnClose;
            _openMethod = OnOpen;
            _waitForChannelMethod = OnWaitForChannel;
            _acceptChannelMethod = OnAcceptChannel;

            if (context.ListenUriMode == ListenUriMode.Explicit && context.ListenUriBaseAddress != null)
            {
                _listenUri = new Uri(context.ListenUriBaseAddress, context.ListenUriRelativeAddress);
            }
            else
            {
                _listenUri = new Uri(new Uri("soap.amqp:///"), Guid.NewGuid().ToString());
            }
        }

        public override Uri Uri
        {
            get { return _listenUri; }
        }

        protected BindingContext Context
        {
            get { return _context; }
        }

        protected override void OnAbort()
        {
            OnClose(_context.Binding.CloseTimeout);
        }

        protected override IAsyncResult OnBeginAcceptChannel(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return _acceptChannelMethod.BeginInvoke(timeout, callback, state);
        }

        protected override TChannel OnEndAcceptChannel(IAsyncResult result)
        {
            return _acceptChannelMethod.EndInvoke(result);
        }

        protected override IAsyncResult OnBeginWaitForChannel(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return _waitForChannelMethod.BeginInvoke(timeout, callback, state);
        }

        protected override bool OnEndWaitForChannel(IAsyncResult result)
        {
            return _waitForChannelMethod.EndInvoke(result);
        }

        protected override IAsyncResult OnBeginClose(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return _closeMethod.BeginInvoke(timeout, callback, state);
        }

        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return _openMethod.BeginInvoke(timeout, callback, state);
        }

        protected override void OnEndClose(IAsyncResult result)
        {
            _closeMethod.EndInvoke(result);
        }

        protected override void OnEndOpen(IAsyncResult result)
        {
            _openMethod.EndInvoke(result);
        }
    }
}
