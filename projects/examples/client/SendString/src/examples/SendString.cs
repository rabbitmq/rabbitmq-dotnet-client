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
using System.Text;

using RabbitMQ.Client;

namespace RabbitMQ.Client.Examples {
    public class SendString {
        public static int Main(string[] args) {
            if (args.Length < 5) {
                Console.Error.WriteLine("Usage: SendString <uri> <exchange> <exchangetype> <routingkey> <message>");
                Console.Error.WriteLine("RabbitMQ .NET client version "+typeof(IModel).Assembly.GetName().Version.ToString());
                Console.Error.WriteLine("Parameters:");
                Console.Error.WriteLine("  <uri> = \"amqp://user:pass@host:port/vhost\"");
                return 2;
            }

            string serverAddress = args[0];
            string exchange = args[1];
            string exchangeType = args[2];
            string routingKey = args[3];
            string message = args[4];

            ConnectionFactory cf = new ConnectionFactory();
            cf.Uri = serverAddress;

            using (IConnection conn = cf.CreateConnection())
                {
                    using (IModel ch = conn.CreateModel()) {

                        if (exchange != "") {
                            ch.ExchangeDeclare(exchange, exchangeType);
                        }
                        ch.BasicPublish(exchange,
                                        routingKey,
                                        null,
                                        Encoding.UTF8.GetBytes(message));
                        return 0;
                    }
                }
        }
    }
}
