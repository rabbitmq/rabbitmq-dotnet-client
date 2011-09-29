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
using RabbitMQ.Client.Events;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Examples {
    public class SingleGet {
        public static int Main(string[] args) {
            if (args.Length < 2) {
                Console.Error.WriteLine("Usage: SingleGet <hostname>[:<portnumber>] <queuename>");
                Console.Error.WriteLine("RabbitMQ .NET client version "+typeof(IModel).Assembly.GetName().Version.ToString());
                return 2;
            }

            string serverAddress = args[0];
            string queueName = args[1];
            ConnectionFactory cf = new ConnectionFactory();
            cf.Uri = serverAddress;
            IConnection conn = cf.CreateConnection();
            conn.ConnectionShutdown += new ConnectionShutdownEventHandler(LogConnClose);

            using (IModel ch = conn.CreateModel()) {
                conn.AutoClose = true;

                ch.QueueDeclare(queueName, false, false, false, null);
                BasicGetResult result = ch.BasicGet(queueName, false);
                if (result == null) {
                    Console.WriteLine("No message available.");
                } else {
                    ch.BasicAck(result.DeliveryTag, false);
                    Console.WriteLine("Message:");
                    DebugUtil.DumpProperties(result, Console.Out, 0);
                }

                return 0;
            }

            // conn will have been closed here by AutoClose above.
        }

        public static void LogConnClose(IConnection conn, ShutdownEventArgs reason) {
            Console.Error.WriteLine("Closing connection "+conn+" with reason "+reason);
        }
    }
}
