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

using System;
using System.IO;
using System.Text;
using System.Threading;

using RabbitMQ.Client;
using RabbitMQ.Client.Content;
using RabbitMQ.Client.Events;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Examples {
    public class LowlevelLogTail {
        public static int Main(string[] args) {
            if (args.Length < 4) {
                Console.Error.WriteLine("Usage: LowlevelLogTail <uri> <exchange> <exchangetype> <routingkey>");
                Console.Error.WriteLine("RabbitMQ .NET client version "+typeof(IModel).Assembly.GetName().Version.ToString());
                Console.Error.WriteLine("If the exchange name is the empty string, will instead declare a queue named");
                Console.Error.WriteLine("by the routingkey, and consume from that queue.");
                return 2;
            }

            string serverAddress = args[0];
            string exchange = args[1];
            string exchangeType = args[2];
            string routingKey = args[3];

            ConnectionFactory cf = new ConnectionFactory();
            cf.Uri = serverAddress;
            using (IConnection conn = cf.CreateConnection())
                {
                    using (IModel ch = conn.CreateModel())
                        {
                            string queueName;
                            if (exchange == "") {
                                ch.QueueDeclare(routingKey, false, true, true, null);
                                queueName = routingKey;
                            } else {
                                ch.ExchangeDeclare(exchange, exchangeType);
                                queueName = ch.QueueDeclare();
                                ch.QueueBind(queueName, exchange, routingKey, null);
                            }

                            MyConsumer consumer = new MyConsumer(ch);
                            ch.BasicConsume(queueName, false, consumer);

                            Console.WriteLine("Consumer tag: " + consumer.ConsumerTag);

                            while (consumer.IsRunning) {
                                // Dummy main thread. Often, this will be
                                // a GUI thread or similar.
                                Thread.Sleep(500);
                            }

                            return 0;
                        }
                }
        }

        ///<summary>Subclass of the very low-level DefaultBasicConsumer</summary>
        ///<remarks>
        /// Be warned: the threading issues involved in using
        /// DefaultBasicConsumer can be subtle! Usually,
        /// QueueingBasicConsumer is what you want. Please see the
        /// comments attached to DefaultBasicConsumer, as well as the
        /// section on DefaultBasicConsumer and threading in the user
        /// manual.
        ///</remarks>
        public class MyConsumer : DefaultBasicConsumer
        {
            public MyConsumer(IModel ch) : base(ch) {}

            public override void HandleBasicDeliver(string consumerTag,
                                                    ulong deliveryTag,
                                                    bool redelivered,
                                                    string exchange,
                                                    string routingKey,
                                                    IBasicProperties properties,
                                                    byte[] body)
            {
                this.Model.BasicAck(deliveryTag, false);

                // We only use BasicDeliverEventArgs here for
                // convenience. Often we wouldn't bother packaging up
                // all the arguments we received: we'd simply use
                // those we needed directly.
                BasicDeliverEventArgs e = new BasicDeliverEventArgs();
                e.ConsumerTag = consumerTag;
                e.DeliveryTag = deliveryTag;
                e.Redelivered = redelivered;
                e.Exchange = exchange;
                e.RoutingKey = routingKey;
                e.BasicProperties = properties;
                e.Body = body;
                ProcessSingleDelivery(e);

                if (Encoding.UTF8.GetString(body) == "quit") {
                    Console.WriteLine("Quitting!");
                    this.OnCancel();
                }
            }
        }

        public static void ProcessSingleDelivery(BasicDeliverEventArgs e) {
            Console.WriteLine("Delivery =========================================");
            DebugUtil.DumpProperties(e, Console.Out, 0);
            Console.WriteLine("----------------------------------------");

            if (e.BasicProperties.ContentType == MapMessageReader.MimeType) {
                IMapMessageReader r = new MapMessageReader(e.BasicProperties, e.Body);
                DebugUtil.DumpProperties(r.Body, Console.Out, 0);
            } else if (e.BasicProperties.ContentType == StreamMessageReader.MimeType) {
                IStreamMessageReader r = new StreamMessageReader(e.BasicProperties, e.Body);
                while (true) {
                    try {
                        object v = r.ReadObject();
                        Console.WriteLine("("+v.GetType()+") "+v);
                    } catch (EndOfStreamException) {
                        break;
                    }
                }
            } else {
                // No special content-type. Already covered by the DumpProperties above.
            }

            Console.WriteLine("==================================================");
        }
    }
}
