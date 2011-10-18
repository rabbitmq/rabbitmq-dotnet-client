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
using System.Text;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.MessagePatterns;

namespace RabbitMQ.Client.Examples {
    public class Subscriber {
        public static int Main(string[] args) {
            if (args.Length < 1) {
                Console.Error.WriteLine("Usage: Subscriber <uri> [<message count>]");
                Console.Error.WriteLine("RabbitMQ .NET client version "+typeof(IModel).Assembly.GetName().Version.ToString());
                Console.Error.WriteLine("Parameters:");
                Console.Error.WriteLine("  <uri> = \"amqp://user:pass@host:port/vhost\"");
                return 2;
            }

            string serverAddress = args[0];
            long msgCount = (args.Length > 1) ? int.Parse(args[1]) : 10;
            ConnectionFactory cf = new ConnectionFactory();
            cf.Uri = serverAddress;
            using (IConnection conn = cf.CreateConnection()) {
                using (IModel ch = conn.CreateModel()) {
                    string queueName = ensureQueue(ch);

                    /* We'll consume msgCount message twice: once
                       using Subscription.Next() and once using the
                       IEnumerator interface.  So, we'll send out
                       2*msgCount messages. */
                    sendMessages(ch, queueName, 2*msgCount);
                    using (Subscription sub = new Subscription(ch, queueName)) {
                        blockingReceiveMessages(sub, msgCount);
                        enumeratingReceiveMessages(sub, msgCount);
                    }
                }
            }

            return 0;
        }

        private static void sendMessages(IModel ch, string queueName, long msgCount) {
            Console.WriteLine("Sending {0} messages to queue {1} via the amq.direct exchange.", msgCount, queueName);

            while (msgCount --> 0) {
                ch.BasicPublish("amq.direct", queueName, null, Encoding.UTF8.GetBytes("Welcome to Caerbannog!"));
            }

            Console.WriteLine("Done.\n");
        }

        private static void blockingReceiveMessages(Subscription sub, long msgCount) {
            Console.WriteLine("Receiving {0} messages (using a Subscriber)", msgCount);

            for (int i = 0; i < msgCount; ++i) {
                Console.WriteLine("Message {0}: {1} (via Subscription.Next())",
                                  i, messageText(sub.Next()));
                Console.WriteLine("Message {0} again: {1} (via Subscription.LatestEvent)",
                                  i, messageText(sub.LatestEvent));
                sub.Ack();
            }

            Console.WriteLine("Done.\n");
        }

        private static void enumeratingReceiveMessages(Subscription sub, long msgCount) {
            Console.WriteLine("Receiving {0} messages (using Subscriber's IEnumerator)", msgCount);

            int i = 0;
            foreach (BasicDeliverEventArgs ev in sub) {
                Console.WriteLine("Message {0}: {1}",
                                  i, messageText(ev));
                if (++i == msgCount)
                    break;
                sub.Ack();
            }

            Console.WriteLine("Done.\n");
        }

        private static string messageText(BasicDeliverEventArgs ev) {
            return Encoding.UTF8.GetString(ev.Body);
        }

        private static string ensureQueue(IModel ch) {
            Console.WriteLine("Creating a queue and binding it to amq.direct");
            string queueName = ch.QueueDeclare();
            ch.QueueBind(queueName, "amq.direct", queueName, null);
            Console.WriteLine("Done.  Created queue {0} and bound it to amq.direct.\n", queueName);
            return queueName;
        }
    }
}
