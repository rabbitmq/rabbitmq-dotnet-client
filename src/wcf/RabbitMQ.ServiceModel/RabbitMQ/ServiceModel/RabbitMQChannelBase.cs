// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2009 LShift Ltd., Cohesive Financial
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
//   The Initial Developers of the Original Code are LShift Ltd,
//   Cohesive Financial Technologies LLC, and Rabbit Technologies Ltd.
//
//   Portions created before 22-Nov-2008 00:00:00 GMT by LShift Ltd,
//   Cohesive Financial Technologies LLC, or Rabbit Technologies Ltd
//   are Copyright (C) 2007-2008 LShift Ltd, Cohesive Financial
//   Technologies LLC, and Rabbit Technologies Ltd.
//
//   Portions created by LShift Ltd are Copyright (C) 2007-2009 LShift
//   Ltd. Portions created by Cohesive Financial Technologies LLC are
//   Copyright (C) 2007-2009 Cohesive Financial Technologies
//   LLC. Portions created by Rabbit Technologies Ltd are Copyright
//   (C) 2007-2009 Rabbit Technologies Ltd.
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

    internal abstract class RabbitMQChannelBase : IChannel
    {   
        private CommunicationOperation closeMethod;
        private BindingContext context;
        private CommunicationOperation openMethod;
        private CommunicationState state;

        private RabbitMQChannelBase()
        {
            this.state = CommunicationState.Created;
            this.closeMethod = new CommunicationOperation(Close);
            this.openMethod = new CommunicationOperation(Open);
        }

        protected RabbitMQChannelBase(BindingContext context)
            : this()
        {
            this.context = context;
        }

        public abstract void Close(TimeSpan timeout);

        public abstract void Open(TimeSpan timeout);

        public virtual void Abort()
        {
            Close();
        }

        public virtual void Close()
        {
            Close(context.Binding.CloseTimeout);
        }

        public virtual T GetProperty<T>() where T : class
        {
            return default(T);
        }

        public virtual void Open()
        {
            Open(context.Binding.OpenTimeout);
        }

        #region Async Methods
        
        public virtual IAsyncResult BeginClose(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return closeMethod.BeginInvoke(timeout, callback, state);
        }

        public virtual IAsyncResult BeginClose(AsyncCallback callback, object state)
        {
            return closeMethod.BeginInvoke(context.Binding.CloseTimeout, callback, state);
        }

        public virtual IAsyncResult BeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return openMethod.BeginInvoke(timeout, callback, state);
        }

        public virtual IAsyncResult BeginOpen(AsyncCallback callback, object state)
        {
            return openMethod.BeginInvoke(context.Binding.OpenTimeout, callback, state);
        }
        
        public virtual void EndClose(IAsyncResult result)
        {
            closeMethod.EndInvoke(result);
        }

        public virtual void EndOpen(IAsyncResult result)
        {
            openMethod.EndInvoke(result);
        }
        
        #endregion

        #region Event Raising Methods
        
        protected void OnOpening()
        {
            state = CommunicationState.Opening;
            if (Opening != null)
                Opening(this, null);
        }

        protected void OnOpened()
        {
            state = CommunicationState.Opened;
            if (Opened != null)
                Opened(this, null);
        }

        protected void OnClosing()
        {
            state = CommunicationState.Closing;
            if (Closing != null)
                Closing(this, null);
        }

        protected void OnClosed()
        {
            state = CommunicationState.Closed;
            if (Closed != null)
                Closed(this, null);
        }

        protected void OnFaulted()
        {
            state = CommunicationState.Faulted;
            if (Faulted != null)
                Faulted(this, null);
        }

        #endregion


        public CommunicationState State
        {
            get { return state; }
        }

        protected BindingContext Context
        {
            get { return context; }
        }


        public event EventHandler Closed;

        public event EventHandler Closing;

        public event EventHandler Faulted;

        public event EventHandler Opened;

        public event EventHandler Opening;
    }
}
