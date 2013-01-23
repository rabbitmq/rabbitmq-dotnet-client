// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2013 VMware, Inc.
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
//  Copyright (c) 2007-2013 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.IO;
using System.Text;

using RabbitMQ.Client;
using RabbitMQ.Client.Content;
using RabbitMQ.Client.Events;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Examples {
    public class ExceptionTest {
        public static int Main(string[] args) {
            try {
                if (args.Length < 1) {
                    Console.Error.WriteLine("Usage: ExceptionTest <uri>");
                    Console.Error.WriteLine("RabbitMQ .NET client version "+typeof(IModel).Assembly.GetName().Version.ToString());
                    Console.Error.WriteLine("Parameters:");
                    Console.Error.WriteLine("  <uri> = \"amqp://user:pass@host:port/vhost\"");
                    return 2;
                }

                string serverAddress = args[0];
                ConnectionFactory cf = new ConnectionFactory();
                cf.Uri = serverAddress;

                using (IConnection conn = cf.CreateConnection())
                {
                    conn.ConnectionShutdown += new ConnectionShutdownEventHandler(First);
                    conn.ConnectionShutdown += new ConnectionShutdownEventHandler(OnConnectionShutdown);
                    conn.ConnectionShutdown += new ConnectionShutdownEventHandler(Second);
                    conn.CallbackException += new CallbackExceptionEventHandler(OnCallbackException);

                    using (IModel ch = conn.CreateModel()) {
                        ch.ModelShutdown += new ModelShutdownEventHandler(First);
                        ch.ModelShutdown += new ModelShutdownEventHandler(OnModelShutdown);
                        ch.ModelShutdown += new ModelShutdownEventHandler(Second);
                        ch.CallbackException += new CallbackExceptionEventHandler(OnCallbackException);

                        string queueName = ch.QueueDeclare();

                        ThrowingConsumer consumer = new ThrowingConsumer(ch);
                        string consumerTag = ch.BasicConsume(queueName, false, consumer);
                        ch.BasicPublish("", queueName, null, Encoding.UTF8.GetBytes("test"));
                        ch.BasicCancel(consumerTag);
                        return 0;
                    }
                }
            } catch (Exception e) {
                Console.Error.WriteLine("-=-=-=-=-= MAIN EXCEPTION CATCHER");
                Console.Error.WriteLine(e);
                return 1;
            }
        }

        public static void First(IConnection sender, ShutdownEventArgs args) {
            Console.WriteLine("********** First (IConnection)");
        }

        public static void Second(IConnection sender, ShutdownEventArgs args) {
            Console.WriteLine("********** Second (IConnection)");
        }

        public static void First(IModel sender, ShutdownEventArgs args) {
            Console.WriteLine("********** First (IModel)");
        }

        public static void Second(IModel sender, ShutdownEventArgs args) {
            Console.WriteLine("********** Second (IModel)");
        }

        public static void OnConnectionShutdown(IConnection conn, ShutdownEventArgs reason) {
            throw new Exception("OnConnectionShutdown");
        }

        public static void OnModelShutdown(IModel conn, ShutdownEventArgs reason) {
            throw new Exception("OnModelShutdown");
        }

        public static void OnCallbackException(object sender, CallbackExceptionEventArgs args) {
            Console.WriteLine("OnCallbackException ==============================");
            Console.WriteLine("Sender: " + sender);
            Console.WriteLine("Message: " + args.Exception.Message);
            Console.WriteLine("Detail:");
            DebugUtil.DumpProperties(args.Detail, Console.Out, 2);
	    Console.WriteLine("----------------------------------------");
        }
    }

    public class ThrowingConsumer: DefaultBasicConsumer {
        public ThrowingConsumer(IModel ch)
            : base(ch)
        {}

        public override void HandleBasicConsumeOk(string consumerTag) {
            throw new Exception("HandleBasicConsumeOk " + consumerTag);
        }

        public override void HandleBasicCancelOk(string consumerTag) {
            throw new Exception("HandleBasicCancelOk " + consumerTag);
        }

        public override void HandleModelShutdown(IModel model, ShutdownEventArgs args) {
            throw new Exception("HandleModelShutdown " + args);
        }

        public override void HandleBasicDeliver(string consumerTag,
                                                ulong deliveryTag,
                                                bool redelivered,
                                                string exchange,
                                                string routingKey,
                                                IBasicProperties properties,
                                                byte[] body)
        {
            throw new Exception("HandleBasicDeliver " + consumerTag);
        }
    }
}
