// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2009 LShift Ltd., Cohesive Financial
//   Technologies LLC., and Rabbit Technologies Ltd.
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
//   The contents of this file are subject to the Mozilla Public License
//   Version 1.1 (the "License"); you may not use this file except in
//   compliance with the License. You may obtain a copy of the License at
//   http://www.rabbitmq.com/mpl.html
//
//   Software distributed under the License is distributed on an "AS IS"
//   basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
//   License for the specific language governing rights and limitations
//   under the License.
//
//   The Original Code is The RabbitMQ .NET Client.
//
//   The Initial Developers of the Original Code are LShift Ltd,
//   Cohesive Financial Technologies LLC, and Rabbit Technologies Ltd.
//
//   Portions created before 22-Nov-2008 00:00:00 GMT by LShift Ltd,
//   Cohesive Financial Technologies LLC, or Rabbit Technologies Ltd
//   are Copyright (C) 2007-2008 LShift Ltd, Cohesive Financial
//   Technologies LLC, and Rabbit Technologies Ltd.
//
//   Portions created by LShift Ltd are Copyright (C) 2007-2009 LShift
//   Ltd. Portions created by Cohesive Financial Technologies LLC are
//   Copyright (C) 2007-2009 Cohesive Financial Technologies
//   LLC. Portions created by Rabbit Technologies Ltd are Copyright
//   (C) 2007-2009 Rabbit Technologies Ltd.
//
//   All Rights Reserved.
//
//   Contributor(s): ______________________________________.
//
//---------------------------------------------------------------------------
using System;
using System.Timers;

using RabbitMQ.Client;
using RabbitMQ.Client.Content;
using RabbitMQ.Client.MessagePatterns;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Examples {
    public class ShutdownableServer: SimpleRpcServer {
        public static int Main(string[] args) {
            try {
                if (args.Length < 1) {
                    Console.Error.WriteLine("Usage: ShutdownableServer <hostname>[:<portnumber>]");
                    Console.Error.WriteLine("RabbitMQ .NET client version "+typeof(IModel).Assembly.GetName().Version.ToString());
                    return 1;
                }

                using (IConnection conn = new ConnectionFactory().CreateConnection(args[0])) {
                    using (IModel ch = conn.CreateModel()) {
                        Subscription sub = new Subscription(ch, "ShutdownableServer");
                        new ShutdownableServer(sub).MainLoop();
                        Console.Out.WriteLine("Returned from MainLoop.");
                    }
                }
                Console.Out.WriteLine("Leaving the program.");
                return 0;
            } catch (Exception e) {
                Console.Error.WriteLine(e);
                return 2;
            }
        }

        public ShutdownableServer(Subscription sub): base(sub) {}

        public override void HandleStreamMessageCall(IStreamMessageBuilder replyWriter,
                                                     bool isRedelivered,
                                                     IBasicProperties requestProperties,
                                                     object[] args)
        {
            Console.Out.WriteLine("ShutdownableServer received a {0} request.",
                                  isRedelivered ? "redelivered" : "new");
            if ((double) args[0] == 0) {
                Console.Out.WriteLine("Shutting down immediately.");
                Close();
                replyWriter.WriteObject("Shut down immediately");
            } else {
                Timer t = new Timer((int) (((double) args[0]) * 1000));
                t.Elapsed += new ElapsedEventHandler(OnTimeout);
                t.Enabled = true;
                replyWriter.WriteObject("Will shut down in " + args[0] + " seconds");
            }
        }

        private void OnTimeout(object source, ElapsedEventArgs e) {
            Console.Out.WriteLine("Delayed shutdown happening now.");
            Close();
        }
    }
}
