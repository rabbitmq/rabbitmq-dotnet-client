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
                this.HostName = rabbind.HostName;
                this.Port = rabbind.Port;
                this.MaxMessageSize = rabbind.MaxMessageSize;
                this.OneWayOnly = rabbind.OneWayOnly;
                this.TransactionFlowEnabled = rabbind.TransactionFlow;
                this.VirtualHost = rabbind.Transport.ConnectionFactory.VirtualHost;
                this.Username = rabbind.Transport.ConnectionFactory.UserName;
                this.Password = rabbind.Transport.ConnectionFactory.Password;
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

            rabbind.HostName = this.HostName;
            rabbind.Port = this.Port;
            rabbind.BrokerProtocol = this.Protocol;
            rabbind.OneWayOnly = this.OneWayOnly;
            rabbind.TransactionFlow = this.TransactionFlowEnabled;
            rabbind.Transport.Password = this.Password;
            rabbind.Transport.Username = this.Username;
            rabbind.Transport.VirtualHost = this.VirtualHost;
            rabbind.Transport.MaxReceivedMessageSize = this.MaxMessageSize;
        }

        /// <summary>
        /// Specifies the hostname of the broker that the binding should connect to.
        /// </summary>
        [ConfigurationProperty("hostname", IsRequired = true)]
        public String HostName
        {
            get { return ((String)base["hostname"]); }
            set { base["hostname"] = value; }
        }

        /// <summary>
        /// Specifies the port of the broker that the binding should connect to.
        /// </summary>
        [ConfigurationProperty("port", DefaultValue = AmqpTcpEndpoint.UseDefaultPort)]
        public int Port
        {
            get { return ((int)base["port"]); }
            set { base["port"] = value; }
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
        [ConfigurationProperty("password", DefaultValue = ConnectionFactory.DefaultPass)]
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
        [ConfigurationProperty("username", DefaultValue = ConnectionFactory.DefaultUser)]
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

        /// <summary>
        /// Specifies the maximum encoded message size
        /// </summary>
        [ConfigurationProperty("maxmessagesize", DefaultValue = 8192L)]
        public long MaxMessageSize
        {
            get { return (long)base["maxmessagesize"]; }
            set { base["maxmessagesize"] = value; }
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
        [ConfigurationProperty("virtualHost", DefaultValue = ConnectionFactory.DefaultVHost)]
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
