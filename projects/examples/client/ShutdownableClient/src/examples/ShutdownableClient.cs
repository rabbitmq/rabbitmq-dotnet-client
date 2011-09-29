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
using RabbitMQ.Client.MessagePatterns;

namespace RabbitMQ.Client.Examples {
    public class ShutdownableClient {
        public static int Main(string[] args) {
            if (args.Length < 1) {
                Console.Error.WriteLine("Usage: ShutdownableClient <hostname>[:<portnumber>] [<secondsdelay>]");
                Console.Error.WriteLine("RabbitMQ .NET client version "+typeof(IModel).Assembly.GetName().Version.ToString());
                return 2;
            }

            ConnectionFactory cf = new ConnectionFactory();
            cf.Uri = args[0];
            using (IConnection conn = cf.CreateConnection()) {
                using (IModel ch = conn.CreateModel()) {
                    object[] callArgs = new object[1];
                    if (args.Length > 1) {
                        callArgs[0] = double.Parse(args[1]);
                    } else {
                        callArgs[0] = (double) 0.0;
                    }

                    SimpleRpcClient client = new SimpleRpcClient(ch, "ShutdownableServer");
                    client.TimeoutMilliseconds = 5000;
                    client.TimedOut += new EventHandler(TimedOutHandler);
                    client.Disconnected += new EventHandler(DisconnectedHandler);
                    object[] reply = client.Call(callArgs);
                    if (reply == null) {
                        Console.WriteLine("Timeout or disconnection.");
                    } else {
                        Console.WriteLine("Reply: {0}", reply[0]);
                    }
                }
            }
            return 0;
        }

	public static void TimedOutHandler(object sender, EventArgs e) {
	    Console.WriteLine("Timed out.");
	}

	public static void DisconnectedHandler(object sender, EventArgs e) {
	    Console.WriteLine("Disconnected.");
	}
    }
}
