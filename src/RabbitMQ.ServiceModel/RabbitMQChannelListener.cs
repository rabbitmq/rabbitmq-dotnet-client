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
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.Threading;

    using RabbitMQ.Client;
    using System.Diagnostics;

    internal sealed class RabbitMQChannelListener<TChannel> : RabbitMQChannelListenerBase<IInputChannel> where TChannel : class, IChannel
    {

        private IInputChannel m_channel;
        private IModel m_model;

        internal RabbitMQChannelListener(BindingContext context)
            : base(context)
        {
            m_channel = null;
            m_model = null;
        }

        protected override IInputChannel OnAcceptChannel(TimeSpan timeout)
        {
            // Since only one connection to a broker is required (even for communication
            // with multiple exchanges
            if (m_channel != null)
                return null;

            m_channel = new RabbitMQInputChannel(Context, m_model, new EndpointAddress(Uri.ToString()));
            m_channel.Closed += new EventHandler(ListenChannelClosed);
            return m_channel;
        }

        protected override bool OnWaitForChannel(TimeSpan timeout)
        {
            return false;
        }

        protected override void OnOpen(TimeSpan timeout)
        {

#if VERBOSE
            DebugHelper.Start();
#endif
            m_model = m_bindingElement.Open(timeout);
#if VERBOSE
            DebugHelper.Stop(" ## In.Open {{Time={0}ms}}.");
#endif
        }

        protected override void OnClose(TimeSpan timeout)
        {
#if VERBOSE
            DebugHelper.Start();
#endif
            if (m_channel != null)
            {
                m_channel.Close();
                m_channel = null;
            }

            if (m_model != null)
            {
                m_bindingElement.Close(m_model, timeout);
                m_model = null;
            }
#if VERBOSE
            DebugHelper.Stop(" ## In.Close {{Time={0}ms}}.");
#endif
        }

        private void ListenChannelClosed(object sender, EventArgs args)
        {
            Close();
        }
}
}
