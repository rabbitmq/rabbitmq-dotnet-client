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
        private Uri m_broker;
        private IProtocol m_brokerProtocol;
        private CompositeDuplexBindingElement m_compositeDuplex;
        private MessageEncodingBindingElement m_encoding;
        private bool m_isInitialized;
        private bool m_oneWayOnly;
        private ReliableSessionBindingElement m_session;
        private TransactionFlowBindingElement m_transactionFlow;
        private bool m_transactionsEnabled;
        private RabbitMQTransportBindingElement m_transport;

        /// <summary>
        /// Creates a new instance of the RabbitMQBinding class initialized 
        /// to use the Protocols.DefaultProtocol. The broker must be set 
        /// before use.
        /// </summary>
        public RabbitMQBinding()
            : this(Protocols.DefaultProtocol)
        { }

        /// <summary>
        /// Uses the default protocol and the broker specified by the given
        /// Uri.
        /// </summary>
        /// <param name="broker">The address of the broker to connect to</param>
        public RabbitMQBinding(Uri broker)
            : this(broker, Protocols.DefaultProtocol)
        { }

        /// <summary>
        /// Uses the broker and protocol specified
        /// </summary>
        /// <param name="broker">The address of the broker to connect to</param>
        /// <param name="protocol">The protocol version to use</param>
        public RabbitMQBinding(Uri broker, IProtocol protocol)
            : this(protocol)
        {
            this.Broker = broker;
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

        /// <summary>
        /// Uses the default protocol and the broker whose address is specified.
        /// </summary>
        /// <param name="brokerUri">The address of the broker to connect to</param>
        public RabbitMQBinding(string brokerUri)
            : this(new Uri(brokerUri))
        { }

        /// <summary>
        /// Uses the broker and protocol specified
        /// </summary>
        /// <param name="brokerUri">The address of the broker to connect to</param>
        /// <param name="protocol">The protocol version to use</param>
        public RabbitMQBinding(string brokerUri, IProtocol protocol)
            : this(new Uri(brokerUri), protocol)
        { }

        public override BindingElementCollection CreateBindingElements()
        {
            m_transport.Broker = this.Broker;
            m_transport.BrokerProtocol = this.BrokerProtocol;
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
        /// Specifies the broker that the binding should connect to.
        /// </summary>
        [ConfigurationProperty("broker")]
        public Uri Broker
        {
            get { return m_broker; }
            set { m_broker = value; }
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
