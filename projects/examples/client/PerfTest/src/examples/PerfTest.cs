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
using System.Diagnostics;
using System.Text;

using RabbitMQ.Client;
using RabbitMQ.Client.MessagePatterns;

namespace RabbitMQ.Client.Examples {
    public class PerfTest {
        private static byte[] Message;

        public static int Main(string[] args) {
            if (args.Length < 3) {
                Console.Error.WriteLine("Usage: PerfTest <uri> <number of messages> <size of messages>");
                Console.Error.WriteLine("RabbitMQ .NET client version "+typeof(IModel).Assembly.GetName().Version.ToString());
                Console.Error.WriteLine("Parameters:");
                Console.Error.WriteLine("  <uri> = \"amqp://user:pass@host:port/vhost\"");
                return 2;
            }

            string serverAddress = args[0];
            int messageCount = int.Parse(args[1]);
            int messageSize = int.Parse(args[2]);
            Message = new byte[messageSize];

            ConnectionFactory cf = new ConnectionFactory();
            cf.Uri = serverAddress;
            using (IConnection conn = cf.CreateConnection())
            {
                Stopwatch sendTimer = new Stopwatch();
                Stopwatch receiveTimer = new Stopwatch();

                using (IModel ch = conn.CreateModel()) {
                    sendTimer.Start();

                    for (int i = 0; i < messageCount; ++i) {
                        ch.BasicPublish("", "", null, Message);
                    }
                }
                sendTimer.Stop();

                using (IModel ch = conn.CreateModel()) {
                    string q = ch.QueueDeclare();

                    for (int i = 0; i < messageCount + 1; ++i) {
                        ch.BasicPublish("", q, null, Message);
                    }
                    //This ensures that all messages have been enqueued
                    ch.BasicGet(q, true);

                    QueueingBasicConsumer consumer =
                        new QueueingBasicConsumer(ch);
                    receiveTimer.Start();
                    ch.BasicConsume(q, true, consumer);

                    for (int i = 0; i < messageCount; ++i) {
                        consumer.Queue.Dequeue();
                    }
                    receiveTimer.Stop();
                }

                Console.WriteLine("Performance Test Completed");
                Console.WriteLine("Send:    {0}Hz", ToHertz(sendTimer.ElapsedMilliseconds, messageCount));
                Console.WriteLine("Receive: {0}Hz", ToHertz(receiveTimer.ElapsedMilliseconds, messageCount));

                return 0;
            }
        }

        private static double ToHertz(long milliseconds, int messageCount) {
            return ((long)messageCount)*1000L/milliseconds;
        }
    }
}
