// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2016 Pivotal Software, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       https://www.apache.org/licenses/LICENSE-2.0
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
//  at https://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
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
        internal IConnection Conn;
        internal IModel Model;

        internal Encoding encoding = new UTF8Encoding();
        public static TimeSpan RECOVERY_INTERVAL = TimeSpan.FromSeconds(2);

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
        // Connections
        //

        internal AutorecoveringConnection CreateAutorecoveringConnection()
        {
            return CreateAutorecoveringConnection(RECOVERY_INTERVAL);
        }

        internal AutorecoveringConnection CreateAutorecoveringConnection(IList<string> hostnames)
        {
            return CreateAutorecoveringConnection(RECOVERY_INTERVAL, hostnames);
        }

        internal AutorecoveringConnection CreateAutorecoveringConnection(TimeSpan interval)
        {
            var cf = new ConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.NetworkRecoveryInterval = interval;
            return (AutorecoveringConnection)cf.CreateConnection();
        }

        internal AutorecoveringConnection CreateAutorecoveringConnection(TimeSpan interval, IList<string> hostnames)
        {
            var cf = new ConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            // tests that use this helper will likely list unreachable hosts,
            // make sure we time out quickly on those
            cf.RequestedConnectionTimeout = 1000;
            cf.NetworkRecoveryInterval = interval;
            return (AutorecoveringConnection)cf.CreateConnection(hostnames);
        }

        internal AutorecoveringConnection CreateAutorecoveringConnection(IList<AmqpTcpEndpoint> endpoints)
        {
            var cf = new ConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            // tests that use this helper will likely list unreachable hosts,
            // make sure we time out quickly on those
            cf.RequestedConnectionTimeout = 1000;
            cf.NetworkRecoveryInterval = RECOVERY_INTERVAL;
            return (AutorecoveringConnection)cf.CreateConnection(endpoints);
        }

        internal AutorecoveringConnection CreateAutorecoveringConnectionWithTopologyRecoveryDisabled()
        {
            var cf = new ConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.TopologyRecoveryEnabled = false;
            cf.NetworkRecoveryInterval = RECOVERY_INTERVAL;
            return (AutorecoveringConnection)cf.CreateConnection();
        }

        internal IConnection CreateNonRecoveringConnection()
        {
            var cf = new ConnectionFactory();
            cf.AutomaticRecoveryEnabled = false;
            cf.TopologyRecoveryEnabled = false;
            return cf.CreateConnection();
        }

        //
        // Channels
        //

        internal void WithTemporaryAutorecoveringConnection(Action<AutorecoveringConnection> action)
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

        internal void WithTemporaryModel(IConnection connection, Action<IModel> action)
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

        internal void WithTemporaryModel(Action<IModel> action)
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

        internal void WithClosedModel(Action<IModel> action)
        {
            IModel model = Conn.CreateModel();
            model.Close();

            action(model);
        }

        internal bool WaitForConfirms(IModel m)
        {
            return m.WaitForConfirms(TimeSpan.FromSeconds(4));
        }

        //
        // Exchanges
        //

        internal string GenerateExchangeName()
        {
            return "exchange" + Guid.NewGuid().ToString();
        }

        internal byte[] RandomMessageBody()
        {
            return encoding.GetBytes(Guid.NewGuid().ToString());
        }

        internal string DeclareNonDurableExchange(IModel m, string x)
        {
            m.ExchangeDeclare(x, "fanout", false);
            return x;
        }

        internal string DeclareNonDurableExchangeNoWait(IModel m, string x)
        {
            m.ExchangeDeclareNoWait(x, "fanout", false, false, null);
            return x;
        }

        //
        // Queues
        //

        internal string GenerateQueueName()
        {
            return "queue" + Guid.NewGuid().ToString();
        }

        internal void WithTemporaryQueue(Action<IModel, string> action)
        {
            WithTemporaryQueue(Model, action);
        }

        internal void WithTemporaryNonExclusiveQueue(Action<IModel, string> action)
        {
            WithTemporaryNonExclusiveQueue(Model, action);
        }

        internal void WithTemporaryQueue(IModel model, Action<IModel, string> action)
        {
            WithTemporaryQueue(model, action, GenerateQueueName());
        }

        internal void WithTemporaryNonExclusiveQueue(IModel model, Action<IModel, string> action)
        {
            WithTemporaryNonExclusiveQueue(model, action, GenerateQueueName());
        }

        internal void WithTemporaryQueue(Action<IModel, string> action, string q)
        {
            WithTemporaryQueue(Model, action, q);
        }

        internal void WithTemporaryQueue(IModel model, Action<IModel, string> action, string queue)
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

        internal void WithTemporaryNonExclusiveQueue(IModel model, Action<IModel, string> action, string queue)
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

        internal void WithTemporaryQueueNoWait(IModel model, Action<IModel, string> action, string queue)
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

        internal void EnsureNotEmpty(string q)
        {
            EnsureNotEmpty(q, "msg");
        }

        internal void EnsureNotEmpty(string q, string body)
        {
            WithTemporaryModel(x => x.BasicPublish("", q, null, encoding.GetBytes(body)));
        }

        internal void WithNonEmptyQueue(Action<IModel, string> action)
        {
            WithNonEmptyQueue(action, "msg");
        }

        internal void WithNonEmptyQueue(Action<IModel, string> action, string msg)
        {
            WithTemporaryNonExclusiveQueue((m, q) =>
            {
                EnsureNotEmpty(q, msg);
                action(m, q);
            });
        }

        internal void WithEmptyQueue(Action<IModel, string> action)
        {
            WithTemporaryNonExclusiveQueue((model, queue) =>
            {
                model.QueuePurge(queue);
                action(model, queue);
            });
        }

        internal void AssertMessageCount(string q, int count)
        {
            WithTemporaryModel((m) => {
                QueueDeclareOk ok = m.QueueDeclarePassive(q);
                Assert.AreEqual(count, ok.MessageCount);
            });
        }

        internal void AssertConsumerCount(string q, int count)
        {
            WithTemporaryModel((m) => {
                QueueDeclareOk ok = m.QueueDeclarePassive(q);
                Assert.AreEqual(count, ok.ConsumerCount);
            });
        }

        internal void AssertConsumerCount(IModel m, string q, int count)
        {
            QueueDeclareOk ok = m.QueueDeclarePassive(q);
            Assert.AreEqual(count, ok.ConsumerCount);
        }

        //
        // Shutdown
        //

        internal void AssertShutdownError(ShutdownEventArgs args, int code)
        {
            Assert.AreEqual(args.ReplyCode, code);
        }

        internal void AssertPreconditionFailed(ShutdownEventArgs args)
        {
            AssertShutdownError(args, Constants.PreconditionFailed);
        }

        internal bool InitiatedByPeerOrLibrary(ShutdownEventArgs evt)
        {
            return !(evt.Initiator == ShutdownInitiator.Application);
        }

        //
        // Concurrency
        //

        internal void WaitOn(object o)
        {
            lock(o)
            {
                Monitor.Wait(o, TimingFixture.TestTimeout);
            }
        }

        //
        // Shelling Out
        //

        internal Process ExecRabbitMQCtl(string args)
        {
            // Allow the path to the rabbitmqctl.bat to be set per machine
            var envVariable = Environment.GetEnvironmentVariable("RABBITMQ_RABBITMQCTL_PATH");

            string rabbitmqctlPath;
            if (envVariable != null)
            {
                rabbitmqctlPath = envVariable;
            }
            else if (IsRunningOnMonoOrDotNetCore())
            {
                rabbitmqctlPath = "../../../../../../../rabbit/scripts/rabbitmqctl";
            }
            else
            {
                rabbitmqctlPath = @"..\..\..\..\..\..\..\rabbit\scripts\rabbitmqctl.bat";
            }

            return ExecCommand(rabbitmqctlPath, args);
        }

        internal Process ExecCommand(string command)
        {
            return ExecCommand(command, "");
        }

        internal Process ExecCommand(string command, string args)
        {
            return ExecCommand(command, args, null);
        }

        internal Process ExecCommand(string ctl, string args, string changeDirTo)
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
            if(IsRunningOnMonoOrDotNetCore()) {
                cmd  = ctl;
            } else {
                cmd  = "cmd.exe";
                args = "/c \"\"" + ctl + "\" " + args + "\"";
            }

            try {
              proc.StartInfo.FileName = cmd;
              proc.StartInfo.Arguments = args;
              proc.StartInfo.RedirectStandardError = true;
              proc.StartInfo.RedirectStandardOutput = true;

              proc.Start();
              String stderr = proc.StandardError.ReadToEnd();
              proc.WaitForExit();
              if (stderr.Length >  0 || proc.ExitCode > 0)
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

        internal void ReportExecFailure(String cmd, String args, String msg)
        {
            Console.WriteLine("Failure while running " + cmd + " " + args + ":\n" + msg);
        }

        public static bool IsRunningOnMonoOrDotNetCore()
        {
            #if CORECLR
            return true;
            #else
            return Type.GetType("Mono.Runtime") != null;
            #endif
        }

        //
        // Flow Control
        //

        internal void Block()
        {
            ExecRabbitMQCtl("set_vm_memory_high_watermark 0.000000001");
            // give rabbitmqctl some time to do its job
            Thread.Sleep(1200);
            Publish(Conn);
        }

        internal void Unblock()
        {
            ExecRabbitMQCtl("set_vm_memory_high_watermark 0.4");
        }

        internal void Publish(IConnection conn)
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

        internal List<ConnectionInfo> ListConnections()
        {
            Process proc  = ExecRabbitMQCtl("list_connections --silent pid peer_port");
            String stdout = proc.StandardOutput.ReadToEnd();

            try
            {
                // {Environment.NewLine} is not sufficient
                string[] splitOn = new string[] { "\r\n", "\n" };
                string[] lines   = stdout.Split(splitOn, StringSplitOptions.RemoveEmptyEntries);
                // line: <rabbit@mercurio.1.11491.0>	58713
                return lines.Select(s =>
                {
                    var columns = s.Split('\t');
                    Debug.Assert(!string.IsNullOrEmpty(columns[0]), "columns[0] is null or empty!");
                    Debug.Assert(!string.IsNullOrEmpty(columns[1]), "columns[1] is null or empty!");
                    return new ConnectionInfo(columns[0], Convert.ToUInt32(columns[1].Trim()));
                }).ToList();
            }
            catch (Exception)
            {
                Console.WriteLine("Bad response from rabbitmqctl list_connections --silent pid peer_port:" + Environment.NewLine + stdout);
                throw;
            }
        }

        internal void CloseConnection(IConnection conn)
        {
            var ci = ListConnections().First(x => conn.LocalPort == x.PeerPort);
            CloseConnection(ci.Pid);
        }

        internal void CloseAllConnections()
        {
            var cs = ListConnections();
            foreach(var c in cs)
            {
                CloseConnection(c.Pid);
            }
        }

        internal void CloseConnection(string pid)
        {
            ExecRabbitMQCtl("close_connection \"" +
                            pid +
                            "\" \"Closed via rabbitmqctl\"");
        }

        internal void RestartRabbitMQ()
        {
            StopRabbitMQ();
            Thread.Sleep(500);
            StartRabbitMQ();
        }

        internal void StopRabbitMQ()
        {
            ExecRabbitMQCtl("stop_app");
        }

        internal void StartRabbitMQ()
        {
            ExecRabbitMQCtl("start_app");
        }

        //
        // Concurrency and Coordination
        //

        internal void Wait(ManualResetEvent latch)
        {
            Assert.IsTrue(latch.WaitOne(TimeSpan.FromSeconds(10)), "waiting on a latch timed out");
        }

        internal void Wait(ManualResetEvent latch, TimeSpan timeSpan)
        {
            Assert.IsTrue(latch.WaitOne(timeSpan), "waiting on a latch timed out");
        }

        //
        // TLS
        //

        public static string CertificatesDirectory()
        {
            return Environment.GetEnvironmentVariable("SSL_CERTS_DIR");
        }
    }

    public class TimingFixture
    {
        public static readonly int TimingInterval = 300;
        public static readonly int SafetyMargin = 150;
        public static readonly int TestTimeout = 5000;
    }
}
