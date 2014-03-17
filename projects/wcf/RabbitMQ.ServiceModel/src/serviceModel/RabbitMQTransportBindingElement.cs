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
        private IProtocol m_protocol;
        private String m_host;
        private int m_port;
        private long m_maxReceivedMessageSize;
        private String m_username;
        private String m_password;
        private String m_vhost;

        /// <summary>
        /// Creates a new instance of the RabbitMQTransportBindingElement Class using the default protocol.
        /// </summary>
        public RabbitMQTransportBindingElement()
        {
            MaxReceivedMessageSize = RabbitMQBinding.DefaultMaxMessageSize;
        }

        private RabbitMQTransportBindingElement(RabbitMQTransportBindingElement other)
        {
            HostName = other.HostName;
            Port = other.Port;
            BrokerProtocol = other.BrokerProtocol;
            Username = other.Username;
            Password = other.Password;
            VirtualHost = other.VirtualHost;
            MaxReceivedMessageSize = other.MaxReceivedMessageSize;
        }


        public override IChannelFactory<TChannel> BuildChannelFactory<TChannel>(BindingContext context)
        {
            if (HostName == null)
                throw new InvalidOperationException("No broker was specified.");
            return (IChannelFactory<TChannel>)(object)new RabbitMQChannelFactory<TChannel>(context);
        }

        public override IChannelListener<TChannel> BuildChannelListener<TChannel>(BindingContext context)
        {
            if (HostName == null)
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
                m_connection = ConnectionFactory.CreateConnection();
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
        /// Specifies the hostname of the RabbitMQ Server
        /// </summary>
        [ConfigurationProperty("hostname")]
        public String HostName
        {
            get{ return m_host; }
            set
            {
                m_host = value;
                m_connectionFactory = null;
            }
        }

        /// <summary>
        /// Specifies the RabbitMQ Server port
        /// </summary>
        [ConfigurationProperty("port")]
        public int Port
        {
            get { return m_port; }
            set
            {
                m_port = value;
                m_connectionFactory = null;
            }
        }

        /// <summary>
        /// The largest receivable encoded message
        /// </summary>
        public override long MaxReceivedMessageSize
        {
            get { return m_maxReceivedMessageSize; }
            set { m_maxReceivedMessageSize = value; }
        }

        /// <summary>
        /// The username  to use when authenticating with the broker
        /// </summary>
        internal String Username
        {
            get { return m_username; }
            set
            {
                m_username = value;
                m_connectionFactory = null;
            }
        }

        /// <summary>
        /// Password to use when authenticating with the broker
        /// </summary>
        internal String Password
        {
            get { return m_password; }
            set
            {
                m_password = value;
                m_connectionFactory = null;
            }
        }

        /// <summary>
        /// Specifies the broker virtual host
        /// </summary>
        internal String VirtualHost
        {
            get { return m_vhost; }
            set
            {
                m_vhost = value;
                m_connectionFactory = null;
            }
        }

        /// <summary>
        /// Specifies the version of the AMQP protocol that should be used to
        /// communicate with the broker
        /// </summary>
        public IProtocol BrokerProtocol
        {
            get { return m_protocol; }
            set
            {
                m_protocol = value;
                m_connectionFactory = null;
            }
        }

        internal ConnectionFactory ConnectionFactory
        {
            get
            {
                if (m_connectionFactory != null)
                {
                    return m_connectionFactory;
                }
                ConnectionFactory connFactory = new ConnectionFactory();
                if (HostName != null)
                {
                    connFactory.HostName = HostName;
                }
                if (Port != AmqpTcpEndpoint.UseDefaultPort)
                {
                    connFactory.Port = Port;
                }
                if (BrokerProtocol != null)
                {
                    connFactory.Protocol = BrokerProtocol;
                }
                if (Username != null)
                {
                    connFactory.UserName = Username;
                }
                if (Password != null)
                {
                    connFactory.Password = Password;
                }
                if (VirtualHost != null)
                {
                    connFactory.VirtualHost = VirtualHost;
                }
                m_connectionFactory = connFactory;
                return connFactory;
            }
        }
    }
}
