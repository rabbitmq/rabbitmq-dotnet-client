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
        private Uri broker;
        private IProtocol brokerProtocol;
        private ConnectionFactory connectionFactory;
        private IConnection connection;

        /// <summary>
        /// Creates a new instance of the RabbitMQTransportBindingElement Class using the default protocol.
        /// </summary>
        public RabbitMQTransportBindingElement()
        {
            brokerProtocol = Protocols.DefaultProtocol;
            connectionFactory = new ConnectionFactory();
            connection = null;
        }

        private RabbitMQTransportBindingElement(RabbitMQTransportBindingElement other)
            : this()
        {
            brokerProtocol = other.brokerProtocol;
            broker = other.Broker;
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
            if (connection == null) {
                connection = connectionFactory.CreateConnection(BrokerProtocol, Broker.Host, Broker.Port);
                //TODO: configure connection parameters
            }
        }

        internal IModel InternalOpen() {
            EnsureConnectionAvailable();
            IModel result = connection.CreateModel();
            connection.AutoClose = true;
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
            connection = null;
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
            get { return this.broker; }
            set { this.broker = value; }
        }

        /// <summary>
        /// Specifies the version of the AMQP protocol that should be used to 
        /// communicate with the broker
        /// </summary>
        public IProtocol BrokerProtocol
        {
            get { return brokerProtocol; }
            set { brokerProtocol = value; }
        }

        public ConnectionParameters ConnectionParameters
        {
            get { return connectionFactory.Parameters; }
        }

        //internal ConnectionFactory ConnectionFactory
        //{
        //    get { return this.connectionFactory; }
        //}
    }
}
