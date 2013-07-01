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

    internal abstract class RabbitMQInputChannelBase : RabbitMQChannelBase, IInputChannel
    {
        private EndpointAddress m_localAddress;
        private CommunicationOperation<Message> m_receiveMethod;
        private CommunicationOperation<bool, Message> m_tryReceiveMethod;
        private CommunicationOperation<bool> m_waitForMessage;


        protected RabbitMQInputChannelBase(BindingContext context, EndpointAddress localAddress)
        :base(context)
        {
            m_localAddress = localAddress;
            m_receiveMethod = new CommunicationOperation<Message>(Receive);
            m_tryReceiveMethod = new CommunicationOperation<bool, Message>(TryReceive);
            m_waitForMessage = new CommunicationOperation<bool>(WaitForMessage);
        }


        #region Async Methods
        public virtual IAsyncResult BeginReceive(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return m_receiveMethod.BeginInvoke(timeout, callback, state);
        }

        public virtual IAsyncResult BeginReceive(AsyncCallback callback, object state)
        {
            return m_receiveMethod.BeginInvoke(Context.Binding.ReceiveTimeout, callback, state);
        }

        public virtual IAsyncResult BeginTryReceive(TimeSpan timeout, AsyncCallback callback, object state)
        {
            Message message = null;
            return m_tryReceiveMethod.BeginInvoke(timeout, out message, callback, state);
        }

        public virtual IAsyncResult BeginWaitForMessage(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return m_waitForMessage.BeginInvoke(timeout, callback, state);
        }

        public virtual Message EndReceive(IAsyncResult result)
        {
            return m_receiveMethod.EndInvoke(result);
        }

        public virtual bool EndTryReceive(IAsyncResult result, out Message message)
        {
            return m_tryReceiveMethod.EndInvoke(out message, result);
        }

        public virtual bool EndWaitForMessage(IAsyncResult result)
        {
            return m_waitForMessage.EndInvoke(result);
        }
        #endregion

        public abstract Message Receive(TimeSpan timeout);

        public abstract bool TryReceive(TimeSpan timeout, out Message message);

        public abstract bool WaitForMessage(TimeSpan timeout);

        public virtual Message Receive()
        {
            return Receive(base.Context.Binding.ReceiveTimeout);
        }

        
        public EndpointAddress LocalAddress
        {
            get { return m_localAddress; }
        }
    }
}
