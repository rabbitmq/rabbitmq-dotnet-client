// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2013 GoPivotal, Inc.
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

using RabbitMQ.Client;
using RabbitMQ.Client.Content;
using RabbitMQ.Client.Events;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Examples {
    public class DeclareQueue {
        public static int Main(string[] args) {
            int optionIndex = 0;
            bool durable = false;
            bool delete = false;
            IDictionary<string, object> arguments = null;
            while (optionIndex < args.Length) {
                if (args[optionIndex] == "/durable") { durable = true; }
                else if (args[optionIndex] == "/delete") { delete = true; }
                else if (args[optionIndex].StartsWith("/arg:")) {
                    if (arguments == null) { arguments = new Dictionary<string, object>(); }
                    string[] pieces = args[optionIndex].Split(new Char[] { ':' });
                    if (pieces.Length >= 3) {
                        arguments[pieces[1]] = pieces[2];
                    }
                }
                else { break; }
                optionIndex++;
            }

            if (((args.Length - optionIndex) < 2) ||
                (((args.Length - optionIndex) % 2) != 0))
                {
                    Console.Error.WriteLine("Usage: DeclareQueue [<option> ...] <uri> <queue> [<exchange> <routingkey>] ...");
                    Console.Error.WriteLine("RabbitMQ .NET client version "+typeof(IModel).Assembly.GetName().Version.ToString());
                    Console.Error.WriteLine("Parameters:");
                    Console.Error.WriteLine("  <uri> = \"amqp://user:pass@host:port/vhost\"");
                    Console.Error.WriteLine("Available options:");
                    Console.Error.WriteLine("  /durable      declare a durable queue");
                    Console.Error.WriteLine("  /delete       delete after declaring");
                    Console.Error.WriteLine("  /arg:KEY:VAL  add longstr entry to arguments table");
                    return 2;
                }

            string serverAddress = args[optionIndex++];
            string inputQueueName = args[optionIndex++];
            ConnectionFactory cf = new ConnectionFactory();
            cf.Uri = serverAddress;

            using (IConnection conn = cf.CreateConnection())
                {
                    using (IModel ch = conn.CreateModel()) {

                        string finalName = ch.QueueDeclare(inputQueueName, durable,
                                                           false, false, arguments);
                        Console.WriteLine("{0}\t{1}", finalName, durable);

                        while ((optionIndex + 1) < args.Length) {
                            string exchange = args[optionIndex++];
                            string routingKey = args[optionIndex++];
                            ch.QueueBind(finalName, exchange, routingKey, null);
                            Console.WriteLine("{0}\t{1}\t{2}", finalName, exchange, routingKey);
                        }

                        if (delete) {
                            ch.QueueDelete(finalName);
                        }

                        return 0;
                    }
                }
        }
    }
}
