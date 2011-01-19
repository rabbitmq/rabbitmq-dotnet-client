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
using RabbitMQ.Util;

namespace RabbitMQ.Client.Examples {
    public class AddServer: SimpleRpcServer {
        public static int Main(string[] args) {
            if (args.Length < 1) {
                Console.Error.WriteLine("Usage: AddServer <hostname>[:<portnumber>]");
                Console.Error.WriteLine("RabbitMQ .NET client version "+typeof(IModel).Assembly.GetName().Version.ToString());
                return 2;
            }

            ConnectionFactory cf = new ConnectionFactory();
            cf.Address = args[0];
            using (IConnection conn = cf.CreateConnection()) {
                using (IModel ch = conn.CreateModel()) {
                    ch.QueueDeclare("AddServer", false, false, false, null);
                    Subscription sub = new Subscription(ch, "AddServer");
                    new AddServer(sub).MainLoop();
                }
            }
            return 0;
        }

        public AddServer(Subscription sub): base(sub) {}

        public override void HandleStreamMessageCall(IStreamMessageBuilder replyWriter,
                                                     bool isRedelivered,
                                                     IBasicProperties requestProperties,
                                                     object[] args)
        {
            Console.Out.WriteLine("AddServer received a {0} request.",
                                  isRedelivered ? "redelivered" : "new");
            double sum = 0;
            foreach (double v in args) {
                Console.Out.WriteLine("Adding {0} to {1}, giving {2}.", v, sum, sum + v);
                sum += v;
            }
            Console.Out.WriteLine("The reply is {0}.", sum);
            replyWriter.WriteObject(sum);
        }

        public override void HandleCast(bool isRedelivered,
                                        IBasicProperties requestProperties,
                                        byte[] body)
        {
            Console.Out.WriteLine("AddServer received a {0} one-way message.",
                                  isRedelivered ? "redelivered" : "new");
	    DebugUtil.DumpProperties(requestProperties, Console.Out, 0);
	    DebugUtil.DumpProperties(body, Console.Out, 0);
        }
    }
}
