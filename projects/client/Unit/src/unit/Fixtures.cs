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
using System.Collections.Specialized;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Linq;

using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Unit
{

    public class IntegrationFixture
    {
        protected IConnection Conn;
        protected IModel Model;

        protected Encoding encoding = new UTF8Encoding();

        [SetUp]
        public virtual void Init()
        {
            var connFactory = new ConnectionFactory();
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
        // Channels
        //

        protected void WithTemporaryAutorecoveringConnection(Action<AutorecoveringConnection> action)
        {
            var factory = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true
            };

            var connection = (AutorecoveringConnection)factory.CreateConnection();
            try
            {
                action(connection);
            }
            finally
            {
                connection.Abort();
            }
        }

        protected void WithTemporaryModel(IConnection connection, Action<IModel> action)
        {
            IModel model = connection.CreateModel();

            try
            {
                action(model);
            }
            finally
            {
                model.Abort();
            }
        }

        protected void WithTemporaryModel(Action<IModel> action)
        {
            IModel model = Conn.CreateModel();

            try
            {
                action(model);
            }
            finally
            {
                model.Abort();
            }
        }

        protected void WithClosedModel(Action<IModel> action)
        {
            IModel model = Conn.CreateModel();
            model.Close();

            action(model);
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
            return encoding.GetBytes(Guid.NewGuid().ToString());
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

        protected void WithTemporaryQueue(Action<IModel, string> action)
        {
            WithTemporaryQueue(Model, action);
        }

        protected void WithTemporaryNonExclusiveQueue(Action<IModel, string> action)
        {
            WithTemporaryNonExclusiveQueue(Model, action);
        }

        protected void WithTemporaryQueue(IModel model, Action<IModel, string> action)
        {
            WithTemporaryQueue(model, action, GenerateQueueName());
        }

        protected void WithTemporaryNonExclusiveQueue(IModel model, Action<IModel, string> action)
        {
            WithTemporaryNonExclusiveQueue(model, action, GenerateQueueName());
        }

        protected void WithTemporaryQueue(Action<IModel, string> action, string q)
        {
            WithTemporaryQueue(Model, action, q);
        }

        protected void WithTemporaryQueue(IModel model, Action<IModel, string> action, string queue)
        {
            try
            {
                model.QueueDeclare(queue, false, true, false, null);
                action(model, queue);
            } finally
            {
                WithTemporaryModel(x => x.QueueDelete(queue));
            }
        }

        protected void WithTemporaryNonExclusiveQueue(IModel model, Action<IModel, string> action, string queue)
        {
            try
            {
                model.QueueDeclare(queue, false, false, false, null);
                action(model, queue);
            } finally
            {
                WithTemporaryModel(tm => tm.QueueDelete(queue));
            }
        }

        protected void WithTemporaryQueueNoWait(IModel model, Action<IModel, string> action, string queue)
        {
            try
            {
                model.QueueDeclareNoWait(queue, false, true, false, null);
                action(model, queue);
            } finally
            {
                WithTemporaryModel(x => x.QueueDelete(queue));
            }
        }

        protected void EnsureNotEmpty(string q)
        {
            EnsureNotEmpty(q, "msg");
        }

        protected void EnsureNotEmpty(string q, string body)
        {
            WithTemporaryModel(x => x.BasicPublish("", q, null, encoding.GetBytes(body)));
        }

        protected void WithNonEmptyQueue(Action<IModel, string> action)
        {
            WithNonEmptyQueue(action, "msg");
        }

        protected void WithNonEmptyQueue(Action<IModel, string> action, string msg)
        {
            WithTemporaryNonExclusiveQueue((m, q) =>
            {
                EnsureNotEmpty(q, msg);
                action(m, q);
            });
        }

        protected void WithEmptyQueue(Action<IModel, string> action)
        {
            WithTemporaryNonExclusiveQueue((model, queue) =>
            {
                model.QueuePurge(queue);
                action(model, queue);
            });
        }

        protected void AssertMessageCount(string q, int count)
        {
            WithTemporaryModel((m) => {
                QueueDeclareOk ok = m.QueueDeclarePassive(q);
                Assert.AreEqual(count, ok.MessageCount);
            });
        }

        protected void AssertConsumerCount(string q, int count)
        {
            WithTemporaryModel((m) => {
                QueueDeclareOk ok = m.QueueDeclarePassive(q);
                Assert.AreEqual(count, ok.ConsumerCount);
            });
        }

        protected void AssertConsumerCount(IModel m, string q, int count)
        {
            QueueDeclareOk ok = m.QueueDeclarePassive(q);
            Assert.AreEqual(count, ok.ConsumerCount);
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
            if(IsRunningOnMono())
            {
                return ExecCommand("../../../../../../rabbitmq-server/scripts/rabbitmqctl", args);
            }
            return ExecCommand("..\\..\\..\\..\\..\\..\\rabbitmq-server\\scripts\\rabbitmqctl.bat", args);
        }

        protected Process ExecCommand(string command)
        {
            return ExecCommand(command, "");
        }

        protected Process ExecCommand(string command, string args)
        {
            return ExecCommand(command, args, null);
        }

        protected Process ExecCommand(string ctl, string args, string changeDirTo)
        {
            var proc = new Process
            {
                StartInfo = 
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };
            if(changeDirTo != null)
            {
                proc.StartInfo.WorkingDirectory = changeDirTo;
            }

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
            ch.BasicPublish("amq.fanout", "", null, encoding.GetBytes("message"));
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
                return "pid = " + Pid + ", peer port: " + PeerPort;
            }
        }

        protected List<ConnectionInfo> ListConnections()
        {
            Process proc  = ExecRabbitMQCtl("list_connections -q pid peer_port");
            String stdout = proc.StandardOutput.ReadToEnd();

            // {Environment.NewLine} is not sufficient
            string[] splitOn = new string[] { "\r\n", "\n" };
            string[] lines   = stdout.Split(splitOn, StringSplitOptions.RemoveEmptyEntries);
            // line: <rabbit@mercurio.1.11491.0>	58713
            return lines.Select(s => {              
              var columns = s.Split('\t');
              Debug.Assert(!string.IsNullOrEmpty(columns[0]), "columns[0] is null or empty!");
	      Debug.Assert(!string.IsNullOrEmpty(columns[1]), "columns[1] is null or empty!");
              return new ConnectionInfo(columns[0], Convert.ToUInt32(columns[1].Trim()));
            }).ToList();
        }

        protected void CloseConnection(IConnection conn)
        {
            var ci = ListConnections().First(x => conn.LocalPort == x.PeerPort);
            CloseConnection(ci.Pid);
        }

        protected void CloseAllConnections()
        {
            var cs = ListConnections();
            foreach(var c in cs)
            {
                CloseConnection(c.Pid);
            }
        }

        protected void CloseConnection(string pid)
        {
            ExecRabbitMQCtl("close_connection \"" +
                            pid +
                            "\" \"Closed via rabbitmqctl\"");
        }

        protected void RestartRabbitMQ()
        {
            StopRabbitMQ();
            Thread.Sleep(500);
            StartRabbitMQ();
        }

        protected void StopRabbitMQ()
        {
            ExecRabbitMQCtl("stop_app");
        }

        protected void StartRabbitMQ()
        {
            ExecRabbitMQCtl("start_app");
        }
    }

    public class TimingFixture
    {
        public static readonly int TimingInterval = 300;
        public static readonly int SafetyMargin = 150;
        public static readonly int TestTimeout = 5000;
    }
}
