// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2010 LShift Ltd., Cohesive Financial
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
//   Portions created by LShift Ltd are Copyright (C) 2007-2010 LShift
//   Ltd. Portions created by Cohesive Financial Technologies LLC are
//   Copyright (C) 2007-2010 Cohesive Financial Technologies
//   LLC. Portions created by Rabbit Technologies Ltd are Copyright
//   (C) 2007-2010 Rabbit Technologies Ltd.
//
//   All Rights Reserved.
//
//   Contributor(s): ______________________________________.
//
//---------------------------------------------------------------------------
/*
    This test creates few queues with a lot of messages and consumes
    from them. While consuming - it produces adequate number of messages,
    so that the queue size should be more or less constant.

    This test shows how broker behaves in high-throughput high-conncurency
    scenarios. Possible outcomes:
        - one queue is starved
        - throughput doesn't align with Qos setting
        - constant Channel.Flow alarms
*/

using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using RabbitMQ.Client;
using RabbitMQ.Util;
using RabbitMQ.Client.MessagePatterns;
using RabbitMQ.Client.Examples.Utils;

namespace RabbitMQ.Client.Examples {
    public class StressQueue {
        public string QName;
        public int Qos;
        public int AvgSize;
        public int DevSize;
        public int QueueLength;
        public bool Persistent;
        private GaussianRandom Gauss;

        public int Recv = 0;
        public int Send = 0;

        public ManualResetEvent SendEvent = new ManualResetEvent(true);

        public IModel cha;

        public StressQueue(string qname,
                           int qos,
                           int avgsize,
                           int devsize,
                           int queuelength,
                           bool persistent) {
            QName = qname;
            Qos = qos;
            AvgSize = avgsize;
            DevSize = devsize;
            QueueLength = queuelength;
            Persistent = persistent;
            Gauss = new GaussianRandom();
        }

        public void SendMessage() {
            if(Interlocked.Increment(ref Send) < 20) {
                SendEvent.Set();
            }
        }

        public void SenderThread() {
            try {
                IBasicProperties Props = cha.CreateBasicProperties();
                if(Persistent) {
                    Props.DeliveryMode = 2;
                }
                while(true) {
                    int a = Interlocked.Decrement(ref Send);
                    if(a == -1) {
                        SendEvent.Reset();
                        Interlocked.Increment(ref Send); // bring back to 0
                        SendEvent.WaitOne();
                        continue;
                    }
                    int size = Math.Abs((int)Gauss.Gauss(AvgSize, DevSize));
                    cha.BasicPublish("", QName, Props, new byte[size]);
                }
            } catch (Exception e) {
                Console.WriteLine("Exc in SenderThread: " + e.Message);
                throw;
            }
        }
    }

    public class StressAmqp {
        public static StressQueue[] Queues = {
            // qname  - queue name
            // qos - Basic.Qos messages prefetch number
            // avgsize - average size of message
            // devsize - standard deviation of a message size
            // length - desired queue length
            // persistent - should messages be marked as persistent
            //
            //             qname  qos avgsize devsize length persistent
            new StressQueue("a",   10,  4096,  1024,  100000,  false),
            new StressQueue("b",   10,   400,   100,  100000,   true),
            new StressQueue("c",    1,  4096,  1024,  100000,  false),
            new StressQueue("d",   20,  4096,  1024,  100000,  false)
        };

        public static int Main(string[] args) {
            string amqp_url = "amqp://guest:guest@localhost:5672/";
            if (args.Length < 1) {
                Console.Error.WriteLine("Usage: StressAmqp <amqp_url>");
                Console.Error.WriteLine("Where <amqp_url> is like: amqp://user:pass@host:port/vhost");
                Console.Error.WriteLine("RabbitMQ .NET client version "
                                        + typeof(IModel).Assembly.GetName().Version.ToString());
                Console.Error.WriteLine("Running with default amqp_url: {0}\n",
                                        amqp_url);
            } else {
                amqp_url = args[0];
            }
            ConnectionFactory factory = new AmqpUriConnectionFactory(amqp_url);

            using (IConnection conn = factory.CreateConnection()) {
                using (IModel main_ch = conn.CreateModel()) {
                    foreach(StressQueue q in Queues) {
                        main_ch.QueueDeclare(q.QName, false, true, false, false,
                                             false, null);
                        main_ch.BasicPublish("", q.QName, null, new byte[0]);
                        BasicGetResult gres = main_ch.BasicGet(q.QName, false);
                        Console.WriteLine("[*] Filling #{0} {1}/{2}",
                                          q.QName,
                                          gres.MessageCount,
                                          q.QueueLength);
                        for(var i = gres.MessageCount; i < q.QueueLength; i++) {
                            q.SendMessage();
                        }
                    }
                }

                foreach(StressQueue q in Queues) {
                    q.cha = conn.CreateModel();
                    Thread p_thread = new Thread(new ThreadStart(q.SenderThread));
                    p_thread.Start();
                }

                Stopwatch timer = new Stopwatch();
                timer.Start();
                foreach(var q in Queues) {
                    var ch = conn.CreateModel();
                    ch.BasicQos(0, (ushort)q.Qos, false);
                    MyConsumer consumer = new MyConsumer(ch, q);
                    ch.BasicConsume(q.QName, false, null, consumer);
                }

                Console.WriteLine("  [*] Starting loop");
                while(true) {
                    Console.Write("    [thr] ");
                    foreach(StressQueue q in Queues) {
                        double msg_per_sec = q.Recv / (timer.ElapsedMilliseconds / 1000.0);
                        Console.Write("{0}:{1,7:0.00}  ", q.QName, msg_per_sec);
                        q.Recv = 0;
                    }
                    Console.WriteLine("   msg/sec");

                    Console.Write("    [lag] ");
                    foreach(StressQueue q in Queues) {
                        Console.Write("{0}:{1,7:0}  ", q.QName, q.Send);
                    }
                    Console.WriteLine("   msgs to send");

                    timer.Reset();
                    timer.Start();
                    Thread.Sleep(10000);
                }
            }
        }
    }

    public class MyConsumer : DefaultBasicConsumer {
        private StressQueue Q;

        public MyConsumer(IModel ch, StressQueue q) : base(ch) {
            Q = q;
        }

        public override void HandleBasicDeliver(string consumerTag,
                                                ulong deliveryTag,
                                                bool redelivered,
                                                string exchange,
                                                string routingKey,
                                                IBasicProperties properties,
                                                byte[] body)
        {
            try {
                /*
                  Unfortunately, due to dotnet client feature, we can't really
                  send anything from this thread - it breaks just when server
                  sends channel.flow. Instead of that - we have a sender thread
                  per queue.
                */
                Q.SendMessage();
                this.Model.BasicAck(deliveryTag, false);
                Interlocked.Increment(ref Q.Recv);
            } catch (Exception e) {
                Console.WriteLine("exc in MyConsumer : " + e.Message);
                throw;
            }
        }
    }
}


public class AmqpUriConnectionFactory : ConnectionFactory {
    public readonly string AmqpRe = "^amqp://"
        + "((?<username>[^:]*)[:](?<password>[^@]*)[@])?"
        + "((?<host>[^/:]*)([:](?<port>[^/]*))?)"
        + "(?<virtual_host>/[^/]*)/?"
        + "([?](?<query>[^/]*))?$";

    public string Uri;

    public AmqpUriConnectionFactory(string uri) : base() {
        Uri = uri;

        Match amqpMatch = Regex.Match(uri, AmqpRe);

        if(!String.IsNullOrEmpty(amqpMatch.Groups["username"].Value)) {
            UserName = amqpMatch.Groups["username"].Value;
        }
        if(!String.IsNullOrEmpty(amqpMatch.Groups["password"].Value)) {
            Password = amqpMatch.Groups["password"].Value;
        }
        if(!String.IsNullOrEmpty(amqpMatch.Groups["virtual_host"].Value)) {
            VirtualHost = amqpMatch.Groups["virtual_host"].Value;
        }
        if(!String.IsNullOrEmpty(amqpMatch.Groups["host"].Value)) {
            HostName = amqpMatch.Groups["host"].Value;
        }
        if(!String.IsNullOrEmpty(amqpMatch.Groups["port"].Value)) {
            Port = int.Parse(amqpMatch.Groups["port"].Value);
        }
    }
}


namespace RabbitMQ.Client.Examples.Utils {
    // Implementation taken from:
    //    http://imusthaveit.spaces.live.com/blog/cns!B5212D3C9F7D8093!398.entry
    public class GaussianRandom : Random {
        private const double c_0 = 2.515517;
        private const double c_1 = 0.802853;
        private const double c_2 = 0.010328;
        private const double d_1 = 1.432788;
        private const double d_2 = 0.189269;
        private const double d_3 = 0.001308;

        public double Gauss(double dMu, double dSigma) {
            return dMu + CumulativeGaussian(base.NextDouble()) * dSigma;
        }

        private static double CumulativeGaussian(double p) {
            bool fNegate = false;
            if (p > 0.5) {
                p = 1.0 - p;
                fNegate = true;
            }

            double t = Math.Sqrt(Math.Log(1.0 / (p * p)));
            double tt = t * t;
            double ttt = tt * t;
            double X = t - ((c_0 + c_1 * t + c_2 * tt) /
                            (1 + d_1 * t + d_2 * tt + d_3 * ttt));

            return fNegate ? -X : X;
        }
    }
}