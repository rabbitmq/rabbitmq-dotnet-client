// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2014 GoPivotal, Inc.
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

using System;
using System.Text;
using RabbitMQ.Client.Events;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Examples
{
    public class ExceptionTest
    {
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length < 1)
                {
                    Console.Error.WriteLine("Usage: ExceptionTest <uri>");
                    Console.Error.WriteLine("RabbitMQ .NET client version " + typeof (IModel).Assembly.GetName().Version);
                    Console.Error.WriteLine("Parameters:");
                    Console.Error.WriteLine("  <uri> = \"amqp://user:pass@host:port/vhost\"");
                    return 2;
                }

                string serverAddress = args[0];
                var connectionFactory = new ConnectionFactory {Uri = serverAddress};

                using (IConnection connection = connectionFactory.CreateConnection())
                {
                    connection.ConnectionShutdown += OnConnectionShutdownFirst;
                    connection.ConnectionShutdown += OnConnectionShutdown;
                    connection.ConnectionShutdown += OnConnectionShutdownSecond;
                    connection.CallbackException += OnCallbackException;

                    using (IModel model = connection.CreateModel())
                    {
                        model.ModelShutdown += OnModelShutdownFirst;
                        model.ModelShutdown += OnModelShutdown;
                        model.ModelShutdown += OnModelShutdownSecond;
                        model.CallbackException += OnCallbackException;

                        string queueName = model.QueueDeclare();

                        var consumer = new ThrowingConsumer(model);
                        string consumerTag = model.BasicConsume(queueName, false, consumer);
                        model.BasicPublish("", queueName, null, Encoding.UTF8.GetBytes("test"));
                        model.BasicCancel(consumerTag);
                        return 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("-=-=-=-=-= MAIN EXCEPTION CATCHER");
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        public static void OnConnectionShutdownFirst(object sender, ShutdownEventArgs args)
        {
            Console.WriteLine("********** First (IConnection)");
        }

        public static void OnConnectionShutdownSecond(object sender, ShutdownEventArgs args)
        {
            Console.WriteLine("********** Second (IConnection)");
        }

        public static void OnModelShutdownFirst(object sender, ShutdownEventArgs args)
        {
            Console.WriteLine("********** First (IModel)");
        }

        public static void OnModelShutdownSecond(object sender, ShutdownEventArgs args)
        {
            Console.WriteLine("********** Second (IModel)");
        }

        public static void OnConnectionShutdown(object sender, ShutdownEventArgs reason)
        {
            throw new Exception("OnConnectionShutdown");
        }

        public static void OnModelShutdown(object conn, ShutdownEventArgs reason)
        {
            throw new Exception("OnModelShutdown");
        }

        public static void OnCallbackException(object sender, CallbackExceptionEventArgs args)
        {
            Console.WriteLine("OnCallbackException ==============================");
            Console.WriteLine("Sender: " + sender);
            Console.WriteLine("Message: " + args.Exception.Message);
            Console.WriteLine("Detail:");
            DebugUtil.DumpProperties(args.Detail, Console.Out, 2);
            Console.WriteLine("----------------------------------------");
        }
    }

    public class ThrowingConsumer : DefaultBasicConsumer
    {
        public ThrowingConsumer(IModel ch)
            : base(ch)
        {
        }

        public override void HandleBasicConsumeOk(string consumerTag)
        {
            throw new Exception("HandleBasicConsumeOk " + consumerTag);
        }

        public override void HandleBasicCancelOk(string consumerTag)
        {
            throw new Exception("HandleBasicCancelOk " + consumerTag);
        }

        public override void HandleModelShutdown(object model, ShutdownEventArgs args)
        {
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