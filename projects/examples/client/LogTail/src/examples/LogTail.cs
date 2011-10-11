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

using RabbitMQ.Client;
using RabbitMQ.Client.Content;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.MessagePatterns;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Examples {
    public class LogTail {
        public static int Main(string[] args) {
            if (args.Length < 4) {
                Console.Error.WriteLine("Usage: LogTail <uri> <exchange> <exchangetype> <routingkey>");
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
                    using (IModel ch = conn.CreateModel()) {
                        ch.QueueDeclare(routingKey, false, true, true, null);
                        Subscription sub = new Subscription(ch, routingKey);
                        if (exchange != "") {
                            ch.ExchangeDeclare(exchange, exchangeType);
                            ch.QueueBind(routingKey, exchange, routingKey, null);
                        }

                        Console.WriteLine("Consumer tag: " + sub.ConsumerTag);
                        foreach (BasicDeliverEventArgs e in sub) {
                            sub.Ack(e);
                            ProcessSingleDelivery(e);
                            if (Encoding.UTF8.GetString(e.Body) == "quit") {
                                Console.WriteLine("Quitting!");
                                break;
                            }
                        }

                        return 0;
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
