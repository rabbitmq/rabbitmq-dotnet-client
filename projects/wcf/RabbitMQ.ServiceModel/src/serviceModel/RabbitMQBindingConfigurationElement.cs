// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2010 LShift Ltd., Cohesive Financial
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
//   Portions created by LShift Ltd are Copyright (C) 2007-2010 LShift
//   Ltd. Portions created by Cohesive Financial Technologies LLC are
//   Copyright (C) 2007-2010 Cohesive Financial Technologies
//   LLC. Portions created by Rabbit Technologies Ltd are Copyright
//   (C) 2007-2010 Rabbit Technologies Ltd.
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
    using System.ServiceModel.Channels;
    using System.ServiceModel.Configuration;
    using RabbitMQ.Client;
    using System.Reflection;

    /// <summary>
    /// Represents the configuration for a RabbitMQBinding.
    /// </summary>
    /// <remarks>
    /// This configuration element should be imported into the client
    /// and server configuration files to provide declarative configuration 
    /// of a AMQP bound service.
    /// </remarks>
    public sealed class RabbitMQBindingConfigurationElement : StandardBindingElement
    {
        /// <summary>
        /// Creates a new instance of the RabbitMQBindingConfigurationElement
        /// Class initialized with values from the specified configuration.
        /// </summary>
        /// <param name="configurationName"></param>
        public RabbitMQBindingConfigurationElement(string configurationName)
            : base(configurationName) {
        }
     
        /// <summary>
        /// Creates a new instance of the RabbitMQBindingConfigurationElement Class.
        /// </summary>
        public RabbitMQBindingConfigurationElement()
            : this(null) {
        }


        protected override void InitializeFrom(Binding binding)
        {
            base.InitializeFrom(binding);
            RabbitMQBinding rabbind = binding as RabbitMQBinding;
            if (rabbind != null)
            {
                this.Broker = rabbind.Broker;
                this.OneWayOnly = rabbind.OneWayOnly;
                this.TransactionFlowEnabled = rabbind.TransactionFlow;
                this.VirtualHost = rabbind.Transport.ConnectionParameters.VirtualHost;
                this.Username = rabbind.Transport.ConnectionParameters.UserName;
                this.Password = rabbind.Transport.ConnectionParameters.Password;
            }
        }

        protected override void OnApplyConfiguration(Binding binding)
        {
            if (binding == null)
                throw new ArgumentNullException("binding");

            RabbitMQBinding rabbind = binding as RabbitMQBinding;
            if (rabbind == null)
            {
                throw new ArgumentException(
                    string.Format("Invalid type for binding. Expected {0}, Passed: {1}", 
                        typeof(RabbitMQBinding).AssemblyQualifiedName, 
                        binding.GetType().AssemblyQualifiedName));
            }

            rabbind.Broker = this.Broker;
            rabbind.BrokerProtocol = this.Protocol;
            rabbind.OneWayOnly = this.OneWayOnly;
            rabbind.TransactionFlow = this.TransactionFlowEnabled;
            rabbind.Transport.ConnectionParameters.Password = this.Password;
            rabbind.Transport.ConnectionParameters.UserName = this.Username;
            rabbind.Transport.ConnectionParameters.VirtualHost = this.VirtualHost;
        }

        /// <summary>
        /// Specifies the broker that the binding should connect to.
        /// </summary>
        [ConfigurationProperty("broker", IsRequired=true)]
        public Uri Broker
        {
            get { return ((Uri)base["broker"]); }
            set { base["broker"] = value; }
        }
        
        /// <summary>
        /// Specifies whether or not the CompositeDuplex and ReliableSession
        /// binding elements are added to the channel stack.
        /// </summary>
        [ConfigurationProperty("oneWay", DefaultValue = false)]
        public bool OneWayOnly
        {
            get { return ((bool)base["oneWay"]); }
            set { base["oneWay"] = value; }
        }

        /// <summary>
        /// Password to use when authenticating with the broker
        /// </summary>
        [ConfigurationProperty("password", DefaultValue = ConnectionParameters.DefaultPass)]
        public string Password
        {
            get { return ((string)base["password"]); }
            set { base["password"] = value; }
        }

        /// <summary>
        /// Specifies whether or not WS-AtomicTransactions are supported by the binding
        /// </summary>
        [ConfigurationProperty("transactionFlow", DefaultValue = false)]
        public bool TransactionFlowEnabled
        {
            get { return ((bool)base["transactionFlow"]); }
            set { base["transactionFlow"] = value; }
        }

        /// <summary>
        /// The username  to use when authenticating with the broker
        /// </summary>
        [ConfigurationProperty("username", DefaultValue = ConnectionParameters.DefaultUser)]
        public string Username
        {
            get { return ((string)base["username"]); }
            set { base["username"] = value; }
        }

        /// <summary>
        /// Specifies the protocol version to use when communicating with the broker
        /// </summary>
        [ConfigurationProperty("protocolversion", DefaultValue = "DefaultProtocol")]
        public string ProtocolVersion
        {
            get {
                return ((string)base["protocolversion"]);
            }
            set {
                base["protocolversion"] = value;
                GetProtocol();
            }
        }

        private IProtocol GetProtocol() {
            IProtocol result = Protocols.Lookup(this.ProtocolVersion);
            if (result == null) {
                throw new ConfigurationErrorsException(string.Format("'{0}' is not a valid AMQP protocol name",
                                                                     this.ProtocolVersion));
            }
            return result;
        }

        /// <summary>
        /// Gets the protocol version specified by the current configuration
        /// </summary>
        public IProtocol Protocol
        {
            get {
                return GetProtocol();
            }
        }

        /// <summary>
        /// The virtual host to access.
        /// </summary>
        [ConfigurationProperty("virtualHost", DefaultValue = ConnectionParameters.DefaultVHost)]
        public string VirtualHost
        {
            get { return ((string)base["virtualHost"]); }
            set { base["virtualHost"] = value; }
        }

        protected override System.Type BindingElementType
        {
            get { return typeof(RabbitMQBinding); }
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get
            {
                ConfigurationPropertyCollection configProperties = base.Properties;
                foreach (PropertyInfo prop in this.GetType().GetProperties(BindingFlags.DeclaredOnly
                                                                           | BindingFlags.Public
                                                                           | BindingFlags.Instance))
                {
                    foreach (ConfigurationPropertyAttribute attr in prop.GetCustomAttributes(typeof(ConfigurationPropertyAttribute), false))
                    {
                        configProperties.Add(
                            new ConfigurationProperty(attr.Name, prop.PropertyType, attr.DefaultValue));
                    }
                }

                return configProperties;
            }
        }
    }
}
