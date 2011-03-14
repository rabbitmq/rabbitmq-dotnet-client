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
    using System.Configuration;
    using System.ServiceModel;
    using System.ServiceModel.Channels;

    using RabbitMQ.Client;
    using RabbitMQ.Client.Exceptions;
    using System.ServiceModel.Description;

    /// <summary>
    /// Represents the binding element used to specify AMQP transport for transmitting messages.
    /// </summary>
    public sealed class RabbitMQTransportBindingElement : TransportBindingElement
    {
        private ConnectionFactory m_connectionFactory;
        private IConnection m_connection;
        private bool m_hasBroker = false;

        /// <summary>
        /// Creates a new instance of the RabbitMQTransportBindingElement Class using the default protocol.
        /// </summary>
        public RabbitMQTransportBindingElement()
        {
            m_connectionFactory = new ConnectionFactory();
        }

        private RabbitMQTransportBindingElement(RabbitMQTransportBindingElement other)
            : this()
        {
            Broker = other.Broker;
            BrokerProtocol = other.BrokerProtocol;
        }

        
        public override IChannelFactory<TChannel> BuildChannelFactory<TChannel>(BindingContext context)
        {
            if (Broker == null)
                throw new InvalidOperationException("No broker was specified.");
            return (IChannelFactory<TChannel>)(object)new RabbitMQChannelFactory<TChannel>(context);
        }

        public override IChannelListener<TChannel> BuildChannelListener<TChannel>(BindingContext context)
        {
            if (Broker == null)
                throw new InvalidOperationException("No broker was specified.");

            return (IChannelListener<TChannel>)((object)new RabbitMQChannelListener<TChannel>(context));
        }

        public override bool CanBuildChannelFactory<TChannel>(BindingContext context)
        {
            return typeof(TChannel) == typeof(IOutputChannel);
        }

        public override bool CanBuildChannelListener<TChannel>(BindingContext context)
        {
            return typeof(TChannel) == typeof(IInputChannel);
        }

        public override BindingElement Clone()
        {
            return new RabbitMQTransportBindingElement(this);
        }

        internal void EnsureConnectionAvailable()
        {
            if (m_connection == null) {
                m_connection = m_connectionFactory.CreateConnection();
            }
        }

        internal IModel InternalOpen() {
            EnsureConnectionAvailable();
            IModel result = m_connection.CreateModel();
            m_connection.AutoClose = true;
            return result;
        }

        internal IModel Open(TimeSpan timeout)
        {
            // TODO: Honour timeout.
            try {
                return InternalOpen();
            } catch (AlreadyClosedException) {
                // fall through
            } catch (ChannelAllocationException) {
                // fall through
            }
            m_connection = null;
            return InternalOpen();
        }

        internal void Close(IModel model, TimeSpan timeout)
        {
            // TODO: Honour timeout.
            if (model != null) {
                model.Close(CurrentVersion.StatusCodes.Ok, "Goodbye");
            }
        }

        public override T GetProperty<T>(BindingContext context)
        {
            return context.GetInnerProperty<T>();
        }

        /// <summary>
        /// Gets the scheme used by the binding, soap.amqp
        /// </summary>
        public override string Scheme
        {
            get { return CurrentVersion.Scheme; }
        }


        /// <summary>
        /// Specifies the address of the RabbitMQ Server
        /// </summary>
        [ConfigurationProperty("broker")]
        public Uri Broker
        {
            get 
            { 
                if(!m_hasBroker) return null;                
                UriBuilder build = new UriBuilder();
                build.Host = m_connectionFactory.HostName;
                build.Port = m_connectionFactory.Port;
                return build.Uri; 
            }
            set 
            { 
                if(value == null) m_hasBroker = false;
                else 
                {
                    m_hasBroker = true;	
                    m_connectionFactory.HostName = value.Host;
                    m_connectionFactory.Port = value.Port;  
                }
            }
        }

        /// <summary>
        /// Specifies the version of the AMQP protocol that should be used to 
        /// communicate with the broker
        /// </summary>
        public IProtocol BrokerProtocol
        {
            get { return m_connectionFactory.Protocol; }
            set { m_connectionFactory.Protocol = value; }
        }

        public ConnectionFactory ConnectionFactory
        {
            get { return m_connectionFactory; }
        }

        //internal ConnectionFactory ConnectionFactory
        //{
        //    get { return m_connectionFactory; }
        //}
    }
}
