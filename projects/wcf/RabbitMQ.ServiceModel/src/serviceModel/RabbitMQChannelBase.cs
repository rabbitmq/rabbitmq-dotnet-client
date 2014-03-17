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
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------


namespace RabbitMQ.ServiceModel
{
    using System;
    using System.ServiceModel;
    using System.ServiceModel.Channels;

    internal abstract class RabbitMQChannelBase : IChannel
    {
        private CommunicationOperation m_closeMethod;
        private BindingContext m_context;
        private CommunicationOperation m_openMethod;
        private CommunicationState m_state;

        private RabbitMQChannelBase()
        {
            m_state = CommunicationState.Created;
            m_closeMethod = new CommunicationOperation(Close);
            m_openMethod = new CommunicationOperation(Open);
        }

        protected RabbitMQChannelBase(BindingContext context)
            : this()
        {
            m_context = context;
        }

        public abstract void Close(TimeSpan timeout);

        public abstract void Open(TimeSpan timeout);

        public virtual void Abort()
        {
            Close();
        }

        public virtual void Close()
        {
            Close(m_context.Binding.CloseTimeout);
        }

        public virtual T GetProperty<T>() where T : class
        {
            return default(T);
        }

        public virtual void Open()
        {
            Open(m_context.Binding.OpenTimeout);
        }

        #region Async Methods

        public virtual IAsyncResult BeginClose(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return m_closeMethod.BeginInvoke(timeout, callback, state);
        }

        public virtual IAsyncResult BeginClose(AsyncCallback callback, object state)
        {
            return m_closeMethod.BeginInvoke(m_context.Binding.CloseTimeout, callback, state);
        }

        public virtual IAsyncResult BeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return m_openMethod.BeginInvoke(timeout, callback, state);
        }

        public virtual IAsyncResult BeginOpen(AsyncCallback callback, object state)
        {
            return m_openMethod.BeginInvoke(m_context.Binding.OpenTimeout, callback, state);
        }

        public virtual void EndClose(IAsyncResult result)
        {
            m_closeMethod.EndInvoke(result);
        }

        public virtual void EndOpen(IAsyncResult result)
        {
            m_openMethod.EndInvoke(result);
        }

        #endregion

        #region Event Raising Methods

        protected void OnOpening()
        {
            m_state = CommunicationState.Opening;
            if (Opening != null)
                Opening(this, null);
        }

        protected void OnOpened()
        {
            m_state = CommunicationState.Opened;
            if (Opened != null)
                Opened(this, null);
        }

        protected void OnClosing()
        {
            m_state = CommunicationState.Closing;
            if (Closing != null)
                Closing(this, null);
        }

        protected void OnClosed()
        {
            m_state = CommunicationState.Closed;
            if (Closed != null)
                Closed(this, null);
        }

        protected void OnFaulted()
        {
            m_state = CommunicationState.Faulted;
            if (Faulted != null)
                Faulted(this, null);
        }

        #endregion


        public CommunicationState State
        {
            get { return m_state; }
        }

        protected BindingContext Context
        {
            get { return m_context; }
        }

        protected String Exchange
        {
            get { return "amq.direct"; }
        }

        public event EventHandler Closed;

        public event EventHandler Closing;

        public event EventHandler Faulted;

        public event EventHandler Opened;

        public event EventHandler Opening;
    }
}
