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


//TODO: Rename to RabbitMQBinding
namespace RabbitMQ.ServiceModel
{
    using System;
    using System.Configuration;
    using System.ServiceModel;
    using System.ServiceModel.Channels;

    using RabbitMQ.Client;

    /// <summary>
    /// A windows communication foundation binding over AMQP
    /// </summary>
    public sealed class RabbitMQBinding : Binding
    {
        private String m_host;
        private int m_port;
        private long m_maxMessageSize;
        private IProtocol m_brokerProtocol;
        private CompositeDuplexBindingElement m_compositeDuplex;
        private TextMessageEncodingBindingElement m_encoding;
        private bool m_isInitialized;
        private bool m_oneWayOnly;
        private ReliableSessionBindingElement m_session;
        private TransactionFlowBindingElement m_transactionFlow;
        private bool m_transactionsEnabled;
        private RabbitMQTransportBindingElement m_transport;

        public static readonly long DefaultMaxMessageSize = 8192L;

        /// <summary>
        /// Creates a new instance of the RabbitMQBinding class initialized
        /// to use the Protocols.DefaultProtocol. The broker must be set
        /// before use.
        /// </summary>
        public RabbitMQBinding()
            : this(Protocols.DefaultProtocol)
        { }

        /// <summary>
        /// Uses the broker specified by the given hostname and port with
        /// Protocols.DefaultProtocol.
        /// </summary>
        /// <param name="hostname">The hostname of the broker to connect to</param>
        /// <param name="port">The port of the broker to connect to</param>
        public RabbitMQBinding(String hostname, int port)
            : this(hostname, port, Protocols.DefaultProtocol)
        { }

        /// <summary>
        /// Uses the broker and protocol specified
        /// </summary>
        /// <param name="hostname">The hostname of the broker to connect to</param>
        /// <param name="port">The port of the broker to connect to</param>
        /// <param name="protocol">The protocol version to use</param>
        public RabbitMQBinding(String hostname, int port, IProtocol protocol)
            : this(protocol)
        {
            this.HostName = hostname;
            this.Port = port;
        }

        /// <summary>
        /// Uses the broker, login and protocol specified
        /// </summary>
        /// <param name="hostname">The hostname of the broker to connect to</param>
        /// <param name="port">The port of the broker to connect to</param>
        /// <param name="username">The broker username to connect with</param>
        /// <param name="password">The broker password to connect with</param>
        /// <param name="virtualhost">The broker virtual host</param>
        /// <param name="maxMessageSize">The largest allowable encoded message size</param>
        /// <param name="protocol">The protocol version to use</param>
        public RabbitMQBinding(String hostname, int port,
                               String username, String password,  String virtualhost,
                               long maxMessageSize, IProtocol protocol)
            : this(protocol)
        {
            this.HostName = hostname;
            this.Port = port;
            this.Transport.Username = username;
            this.Transport.Password = password;
            this.Transport.VirtualHost = virtualhost;
            this.MaxMessageSize = maxMessageSize;

        }

        /// <summary>
        /// Uses the specified protocol. The broker must be set before use.
        /// </summary>
        /// <param name="protocol">The protocol version to use</param>
        public RabbitMQBinding(IProtocol protocol)
        {
            m_brokerProtocol = protocol;
            base.Name = "RabbitMQBinding";
            base.Namespace = "http://schemas.rabbitmq.com/2007/RabbitMQ/";

            Initialize();

            this.TransactionFlow = true;
        }

        public override BindingElementCollection CreateBindingElements()
        {
            m_transport.HostName = this.HostName;
            m_transport.Port = this.Port;
            m_transport.BrokerProtocol = this.BrokerProtocol;
            if (MaxMessageSize != DefaultMaxMessageSize)
            {
                m_transport.MaxReceivedMessageSize = MaxMessageSize;
            }
            BindingElementCollection elements = new BindingElementCollection();

            if (m_transactionsEnabled)
            {
                elements.Add(m_transactionFlow);
            }
            if (!OneWayOnly)
            {
                elements.Add(m_session);
                elements.Add(m_compositeDuplex);
            }
            elements.Add(m_encoding);
            elements.Add(m_transport);

            return elements;
        }

        private void Initialize()
        {
            lock (this)
            {
                if (!m_isInitialized)
                {
                    m_transport = new RabbitMQTransportBindingElement();
                    m_encoding = new TextMessageEncodingBindingElement(); // new TextMessageEncodingBindingElement();
                    m_session = new ReliableSessionBindingElement();
                    m_compositeDuplex = new CompositeDuplexBindingElement();
                    m_transactionFlow = new TransactionFlowBindingElement();
                    m_maxMessageSize = DefaultMaxMessageSize;
                    m_isInitialized = true;
                }
            }
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
            get { return m_host; }
            set { m_host = value; }
        }

        /// <summary>
        /// Specifies the RabbitMQ Server port
        /// </summary>
        [ConfigurationProperty("port")]
        public int Port
        {
            get { return m_port; }
            set { m_port = value; }
        }

        /// <summary>
        /// Specifies the maximum encoded message size
        /// </summary>
        [ConfigurationProperty("maxmessagesize")]
        public long MaxMessageSize
        {
            get { return m_maxMessageSize; }
            set { m_maxMessageSize = value; }
        }

        /// <summary>
        /// Specifies the version of the AMQP protocol that should be used to communicate with the broker
        /// </summary>
        public IProtocol BrokerProtocol
        {
            get { return m_brokerProtocol; }
            set { m_brokerProtocol = value; }
        }

        /// <summary>
        /// Gets the AMQP transport binding element
        /// </summary>
        public RabbitMQTransportBindingElement Transport
        {
            get { return m_transport; }
        }

        /// <summary>
        /// Gets the reliable session parameters for this binding instance
        /// </summary>
        public ReliableSession ReliableSession
        {
            get { return new ReliableSession(m_session); }
        }

        /// <summary>
        /// Determines whether or not the TransactionFlowBindingElement will
        /// be added to the channel stack
        /// </summary>
        public bool TransactionFlow
        {
            get { return m_transactionsEnabled; }
            set { m_transactionsEnabled = value; }
        }

        /// <summary>
        /// Specifies whether or not the CompositeDuplex and ReliableSession
        /// binding elements are added to the channel stack.
        /// </summary>
        public bool OneWayOnly
        {
            get { return m_oneWayOnly; }
            set { m_oneWayOnly = value; }
        }
    }
}
