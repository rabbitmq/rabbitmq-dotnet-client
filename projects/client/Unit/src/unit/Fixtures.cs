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

#pragma warning disable 2002

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Linq;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Unit
{

    public class IntegrationFixture
    {
        protected IConnection Conn;
        protected IModel Model;

        protected Encoding enc = new UTF8Encoding();

        [SetUp]
        public void Init()
        {
            ConnectionFactory connFactory = new ConnectionFactory();
            Conn = connFactory.CreateConnection();
            Model = Conn.CreateModel();
        }

        [TearDown]
        public void Dispose()
        {
            if(Model.IsOpen)
            {
                Model.Close();
            }
            if(Conn.IsOpen)
            {
                Conn.Close();
            }

            ReleaseResources();
        }

        protected virtual void ReleaseResources()
        {
            // no-op
        }

        //
        // Delegates
        //

        protected delegate void ModelOp(IModel m);
        protected delegate void QueueOp(IModel m, string q);

        //
        // Channels
        //

        protected void WithTemporaryModel(ModelOp fn)
        {
            IModel m = Conn.CreateModel();

            try { fn(m); } finally { m.Abort(); }
        }

        protected void WithClosedModel(ModelOp fn)
        {
            IModel m = Conn.CreateModel();
            m.Close();

            fn(m);
        }

        protected bool WaitForConfirms(IModel m)
        {
            return m.WaitForConfirms(TimeSpan.FromSeconds(4));
        }

        //
        // Exchanges
        //

        protected string GenerateExchangeName()
        {
            return "exchange" + Guid.NewGuid().ToString();
        }

        protected byte[] RandomMessageBody()
        {
            return enc.GetBytes(Guid.NewGuid().ToString());
        }

        protected string DeclareNonDurableExchange(IModel m, string x)
        {
            m.ExchangeDeclare(x, "fanout", false);
            return x;
        }

        protected string DeclareNonDurableExchangeNoWait(IModel m, string x)
        {
            m.ExchangeDeclareNoWait(x, "fanout", false, false, null);
            return x;
        }

        //
        // Queues
        //

        protected string GenerateQueueName()
        {
            return "queue" + Guid.NewGuid().ToString();
        }

        protected void WithTemporaryQueue(QueueOp fn)
        {
            WithTemporaryQueue(Model, fn);
        }

        protected void WithTemporaryQueue(IModel m, QueueOp fn)
        {
            string q = GenerateQueueName();
            try
            {
                m.QueueDeclare(q, false, true, false, null);
                fn(m, q);
            } finally
            {
                WithTemporaryModel((tm) => tm.QueueDelete(q));
            }
        }

        protected void EnsureNotEmpty(string q)
        {
            EnsureNotEmpty(q, "msg");
        }

        protected void EnsureNotEmpty(string q, string body)
        {
            WithTemporaryModel((m) => m.BasicPublish("", q, null, enc.GetBytes(body)));
        }

        protected void WithNonEmptyQueue(QueueOp fn)
        {
            WithNonEmptyQueue(fn, "msg");
        }

        protected void WithNonEmptyQueue(QueueOp fn, string msg)
        {
            WithTemporaryQueue((m, q) => {
                EnsureNotEmpty(q, msg);
                fn(m, q);
            });
        }

        protected void WithEmptyQueue(QueueOp fn)
        {
            WithTemporaryQueue((m, q) => {
                m.QueuePurge(q);
                fn(m, q);
            });
        }

        protected void AssertMessageCount(string q, int count)
        {
            WithTemporaryModel((m) => {
                QueueDeclareOk ok = m.QueueDeclarePassive(q);
                Assert.AreEqual(count, ok.MessageCount);
            });
        }

        //
        // Shutdown
        //

        protected void AssertShutdownError(ShutdownEventArgs args, int code)
        {
            Assert.AreEqual(args.ReplyCode, code);
        }

        protected void AssertPreconditionFailed(ShutdownEventArgs args)
        {
            AssertShutdownError(args, Constants.PreconditionFailed);
        }

        //
        // Concurrency
        //

        protected void WaitOn(object o)
        {
            lock(o)
            {
                Monitor.Wait(o, TimingFixture.TestTimeout);
            }
        }

        //
        // Shelling Out
        //

        protected Process ExecRabbitMQCtl(string args)
        {
            if(IsRunningOnMono()) {
                return ExecCommand("../../../../../../rabbitmq-server/scripts/rabbitmqctl", args);
            } else {
                return ExecCommand("..\\..\\..\\..\\..\\..\\rabbitmq-server\\scripts\\rabbitmqctl.bat", args);
            }
        }

        protected Process ExecCommand(string ctl, string args)
        {
            Process proc = new Process();
            proc.StartInfo.CreateNoWindow  = true;
            proc.StartInfo.UseShellExecute = false;

            string cmd;
            if(IsRunningOnMono()) {
                cmd  = ctl;
            } else {
                cmd  = "cmd.exe";
                args = "/c " + ctl + " -n rabbit@" + (Environment.GetEnvironmentVariable("COMPUTERNAME")).ToLower() + " " + args;
            }

            try {
              proc.StartInfo.FileName = cmd;
              proc.StartInfo.Arguments = args;
              proc.StartInfo.RedirectStandardError = true;
              proc.StartInfo.RedirectStandardOutput = true;

              proc.Start();
              String stderr = proc.StandardError.ReadToEnd();
              proc.WaitForExit();
              if (stderr.Length > 0)
              {
                  String stdout = proc.StandardOutput.ReadToEnd();
                  ReportExecFailure(cmd, args, stderr + "\n" + stdout);
              }

              return proc;
            }
            catch (Exception e)
            {
                ReportExecFailure(cmd, args, e.Message);
                throw e;
            }
        }

        protected void ReportExecFailure(String cmd, String args, String msg)
        {
            Console.WriteLine("Failure while running " + cmd + " " + args + ":\n" + msg);
        }

        public static bool IsRunningOnMono()
        {
            return Type.GetType("Mono.Runtime") != null;
        }

        //
        // Flow Control
        //

        protected void Block()
        {
            ExecRabbitMQCtl("set_vm_memory_high_watermark 0.000000001");
            // give rabbitmqctl some time to do its job
            Thread.Sleep(800);
            Publish(Conn);
        }

        protected void Unblock()
        {
            ExecRabbitMQCtl("set_vm_memory_high_watermark 0.4");
        }

        protected void Publish(IConnection conn)
        {
            IModel ch = conn.CreateModel();
            ch.BasicPublish("amq.fanout", "", null, enc.GetBytes("message"));
        }

        //
        // Connection Closure
        //

        public class ConnectionInfo
        {
            public string Pid
            {
                get; set;
            }

            public uint PeerPort
            {
                get; set;
            }

            public ConnectionInfo(string pid, uint peerPort)
            {
                Pid = pid;
                PeerPort = peerPort;
            }

            public override string ToString()
            {
                return "pid = " + Pid + ", peer port: " + PeerPort.ToString();
            }
        }

        protected List<ConnectionInfo> ListConnections()
        {
            Process proc  = ExecRabbitMQCtl("list_connections -q pid peer_port");
            String stdout = proc.StandardOutput.ReadToEnd();

            string[] splitOn = {Environment.NewLine};
            string[] lines   = stdout.Split(splitOn, StringSplitOptions.RemoveEmptyEntries);

            // line: <rabbit@mercurio.1.11491.0>	58713
            return lines.Select(s => {
              var columns = s.Split('\t');
              return new ConnectionInfo(columns[0], Convert.ToUInt32(columns[1]));
            }).ToList();
        }

        protected void CloseConnection(IConnection conn)
        {
            var ci = ListConnections().First(x => conn.LocalPort == x.PeerPort);
            CloseConnection(ci.Pid);
        }

        protected void CloseConnection(string pid)
        {
            ExecRabbitMQCtl("close_connection '" +
                            pid +
                            "' 'Closed via rabbitmqctl'");
        }
    }

    public class TimingFixture
    {
        public static readonly int TimingInterval = 300;
        public static readonly int SafetyMargin = 150;
        public static readonly int TestTimeout = 5000;
    }
}
