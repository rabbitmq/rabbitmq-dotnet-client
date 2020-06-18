// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2020 VMware, Inc.
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
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

#pragma warning disable 2002

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Unit
{

    public class IntegrationFixture : IDisposable
    {
        internal IConnectionFactory ConnFactory;
        internal IConnection Conn;
        internal IModel Model;

        internal Encoding encoding = new UTF8Encoding();
        public static TimeSpan RECOVERY_INTERVAL = TimeSpan.FromSeconds(2);

        [SetUp]
        public virtual async ValueTask Init()
        {

            ConnFactory = new ConnectionFactory();
            ConnFactory.ClientProvidedName = GetType().Name;
            Conn = await ConnFactory.CreateConnection();
            Model = await Conn.CreateModel();
        }

        [TearDown]

        public async ValueTask TearDown()
        {
            if (Model.IsOpen)
            {
                await Model.Close();
            }

            if (Conn.IsOpen)
            {
                await Conn.Close();
            }

            ReleaseResources();
        }

        public void Dispose()
        {

        }

        protected virtual void ReleaseResources()
        {
            // no-op
        }

        //
        // Connections
        //

        internal ValueTask<AutorecoveringConnection> CreateAutorecoveringConnection([CallerMemberName] string name = "")
        {
            return CreateAutorecoveringConnection(RECOVERY_INTERVAL, name);
        }

        internal ValueTask<AutorecoveringConnection> CreateAutorecoveringConnection(IList<string> hostnames, [CallerMemberName] string name = "")
        {
            return CreateAutorecoveringConnection(RECOVERY_INTERVAL, hostnames, name);
        }

        internal async ValueTask<AutorecoveringConnection> CreateAutorecoveringConnection(TimeSpan interval, [CallerMemberName] string name = "")
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = interval,
                ClientProvidedName = name
            };
            return (AutorecoveringConnection)await cf.CreateConnection();
        }

        internal async ValueTask<AutorecoveringConnection> CreateAutorecoveringConnection(TimeSpan interval, IList<string> hostnames, string name)
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                // tests that use this helper will likely list unreachable hosts,
                // make sure we time out quickly on those
                RequestedConnectionTimeout = TimeSpan.FromSeconds(1),
                NetworkRecoveryInterval = interval,
                ClientProvidedName = name
            };
            return (AutorecoveringConnection)await cf.CreateConnection(hostnames);
        }

        internal async ValueTask<AutorecoveringConnection> CreateAutorecoveringConnection(IList<AmqpTcpEndpoint> endpoints, [CallerMemberName] string name = "")
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                // tests that use this helper will likely list unreachable hosts,
                // make sure we time out quickly on those
                RequestedConnectionTimeout = TimeSpan.FromSeconds(1),
                NetworkRecoveryInterval = RECOVERY_INTERVAL,
                ClientProvidedName = name
            };
            return (AutorecoveringConnection)await cf.CreateConnection(endpoints);
        }

        internal async ValueTask<AutorecoveringConnection> CreateAutorecoveringConnectionWithTopologyRecoveryDisabled([CallerMemberName] string name = "")
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = false,
                NetworkRecoveryInterval = RECOVERY_INTERVAL
            };

            return (AutorecoveringConnection)await cf.CreateConnection(name);
        }

        internal ValueTask<IConnection> CreateNonRecoveringConnection()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = false,
                TopologyRecoveryEnabled = false
            };
            return cf.CreateConnection($"UNIT_CONN:{Guid.NewGuid()}");
        }

        internal ValueTask<IConnection> CreateConnectionWithContinuationTimeout(bool automaticRecoveryEnabled, TimeSpan continuationTimeout)
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = automaticRecoveryEnabled,
                ContinuationTimeout = continuationTimeout
            };
            return cf.CreateConnection($"UNIT_CONN:{Guid.NewGuid()}");
        }

        //
        // Channels
        //

        internal async ValueTask WithTemporaryAutorecoveringConnection(Action<AutorecoveringConnection> action)
        {
            var factory = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true
            };

            var connection = (AutorecoveringConnection)await factory.CreateConnection($"UNIT_CONN:{Guid.NewGuid()}");
            try
            {
                action(connection);
            }
            finally
            {
                await connection.Abort();
            }
        }

        internal async ValueTask WithTemporaryModel(IConnection connection, Func<IModel, ValueTask> action)
        {
            IModel model = await connection.CreateModel();

            try
            {
                await action(model);
            }
            finally
            {
                model.Abort();
            }
        }

        internal async ValueTask WithTemporaryModel(Func<IModel, ValueTask> action)
        {
            IModel model = await Conn.CreateModel();

            try
            {
                await action(model);
            }
            finally
            {
                model.Abort();
            }
        }

        internal async ValueTask WithClosedModel(Func<IModel, ValueTask> action)
        {
            IModel model = await Conn.CreateModel();
            await model.Close();

            await action(model);
        }

        internal ValueTask<bool> WaitForConfirms(IModel m)
        {
            return m.WaitForConfirms(TimeSpan.FromSeconds(4));
        }

        //
        // Exchanges
        //

        internal string GenerateExchangeName()
        {
            return $"exchange{Guid.NewGuid()}";
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
            return $"queue{Guid.NewGuid()}";
        }

        internal async ValueTask WithTemporaryQueue(Func<IModel, string, ValueTask> action)
        {
            await WithTemporaryQueue(Model, action);
        }

        internal async ValueTask WithTemporaryNonExclusiveQueue(Func<IModel, string, ValueTask> action)
        {
            await WithTemporaryNonExclusiveQueue(Model, action);
        }

        internal async ValueTask WithTemporaryQueue(IModel model, Func<IModel, string, ValueTask> action)
        {
            await WithTemporaryQueue(model, action, GenerateQueueName());
        }

        internal async ValueTask WithTemporaryNonExclusiveQueue(IModel model, Func<IModel, string, ValueTask> action)
        {
            await WithTemporaryNonExclusiveQueue(model, action, GenerateQueueName());
        }

        internal async ValueTask WithTemporaryQueue(Func<IModel, string, ValueTask> action, string q)
        {
            await WithTemporaryQueue(Model, action, q);
        }

        internal async ValueTask WithTemporaryQueue(IModel model, Func<IModel, string, ValueTask> action, string queue)
        {
            try
            {
                await model.QueueDeclare(queue, false, true, false, null);
                await action(model, queue);
            } finally
            {
                await WithTemporaryModel(async x => await x.QueueDelete(queue));
            }
        }

        internal async ValueTask WithTemporaryNonExclusiveQueue(IModel model, Func<IModel, string, ValueTask> action, string queue)
        {
            try
            {
                await model.QueueDeclare(queue, false, false, false, null);
                await action(model, queue);
            } finally
            {
                await WithTemporaryModel(async tm => await tm.QueueDelete(queue));
            }
        }

        internal async ValueTask WithTemporaryQueueNoWait(IModel model, Func<IModel, string, ValueTask> action, string queue)
        {
            try
            {
                await model.QueueDeclareNoWait(queue, false, true, false, null);
                await action(model, queue);
            } finally
            {
                await WithTemporaryModel(async x => await x.QueueDelete(queue));
            }
        }

        internal async ValueTask EnsureNotEmpty(string q)
        {
            await EnsureNotEmpty(q, "msg");
        }

        internal async ValueTask EnsureNotEmpty(string q, string body)
        {
            await WithTemporaryModel(async x => { await x.BasicPublish("", q, null, encoding.GetBytes(body)); await Task.Delay(100);; });
        }

        internal async ValueTask WithNonEmptyQueue(Func<IModel, string, ValueTask> action)
        {
            await WithNonEmptyQueue(action, "msg");
        }

        internal async ValueTask WithNonEmptyQueue(Func<IModel, string, ValueTask> action, string msg)
        {
            await WithTemporaryNonExclusiveQueue(async (m, q) =>
            {
                await EnsureNotEmpty(q, msg);
                await action(m, q);
            });
        }

        internal async ValueTask WithEmptyQueue(Func<IModel, string, ValueTask> action)
        {
            await WithTemporaryNonExclusiveQueue(async (model, queue) =>
            {
                await model.QueuePurge(queue);
                await action(model, queue);
            });
        }

        internal async ValueTask AssertMessageCount(string q, int count)
        {
            await WithTemporaryModel(async (m) => {
                QueueDeclareOk ok = await m.QueueDeclarePassive(q);
                Assert.AreEqual(count, ok.MessageCount);
            });
        }

        internal async ValueTask AssertConsumerCount(string q, int count)
        {
            await WithTemporaryModel(async (m) => {
                QueueDeclareOk ok = await m.QueueDeclarePassive(q);
                Assert.AreEqual(count, ok.ConsumerCount);
            });
        }

        internal async ValueTask AssertConsumerCount(IModel m, string q, int count)
        {
            QueueDeclareOk ok = await m.QueueDeclarePassive(q);
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
            string envVariable = Environment.GetEnvironmentVariable("RABBITMQ_RABBITMQCTL_PATH");
            string rabbitmqctlPath;

            if (envVariable != null)
            {
                var regex = new Regex(@"^DOCKER:(?<dockerMachine>.+)$");
                Match match = regex.Match(envVariable);

                if (match.Success)
                {
                    return ExecRabbitMqCtlUsingDocker(args, match.Groups["dockerMachine"].Value);
                } else {
                    rabbitmqctlPath = envVariable;
                }
            }
            else
            {
                // provided by the umbrella
                string umbrellaRabbitmqctlPath;
                // provided in PATH by a RabbitMQ installation
                string providedRabbitmqctlPath;

                if (IsRunningOnMonoOrDotNetCore())
                {
                    umbrellaRabbitmqctlPath = "../../../../../../rabbit/scripts/rabbitmqctl";
                    providedRabbitmqctlPath = "rabbitmqctl";
                } else {
                    umbrellaRabbitmqctlPath = @"..\..\..\..\..\..\rabbit\scripts\rabbitmqctl.bat";
                    providedRabbitmqctlPath = "rabbitmqctl.bat";
                }

                if (File.Exists(umbrellaRabbitmqctlPath)) {
                    rabbitmqctlPath = umbrellaRabbitmqctlPath;
                } else {
                    rabbitmqctlPath = providedRabbitmqctlPath;
                }
            }

            return ExecCommand(rabbitmqctlPath, args);
        }

        private Process ExecRabbitMqCtlUsingDocker(string args, string dockerMachineName)
        {
            var proc = new Process
            {
                StartInfo =
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };

            try {
                proc.StartInfo.FileName = "docker";
                proc.StartInfo.Arguments = $"exec {dockerMachineName} rabbitmqctl {args}";
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.RedirectStandardOutput = true;

                proc.Start();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                if (stderr.Length >  0 || proc.ExitCode > 0)
                {
                    string stdout = proc.StandardOutput.ReadToEnd();
                    ReportExecFailure("rabbitmqctl", args, $"{stderr}\n{stdout}");
                }

                return proc;
            }
            catch (Exception e)
            {
                ReportExecFailure("rabbitmqctl", args, e.Message);
                throw;
            }
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
                args = $"/c \"\"{ctl}\" {args}\"";
            }

            try {
              proc.StartInfo.FileName = cmd;
              proc.StartInfo.Arguments = args;
              proc.StartInfo.RedirectStandardError = true;
              proc.StartInfo.RedirectStandardOutput = true;

              proc.Start();
                string stderr = proc.StandardError.ReadToEnd();
              proc.WaitForExit();
              if (stderr.Length >  0 || proc.ExitCode > 0)
              {
                    string stdout = proc.StandardOutput.ReadToEnd();
                  ReportExecFailure(cmd, args, $"{stderr}\n{stdout}");
              }

              return proc;
            }
            catch (Exception e)
            {
                ReportExecFailure(cmd, args, e.Message);
                throw;
            }
        }

        internal void ReportExecFailure(string cmd, string args, string msg)
        {
            Console.WriteLine($"Failure while running {cmd} {args}:\n{msg}");
        }

        public static bool IsRunningOnMonoOrDotNetCore()
        {
            #if NETCOREAPP
            return true;
            #else
            return Type.GetType("Mono.Runtime") != null;
            #endif
        }

        //
        // Flow Control
        //

        internal async ValueTask Block()
        {
            ExecRabbitMQCtl("set_vm_memory_high_watermark 0.000000001");
            // give rabbitmqctl some time to do its job
            await Task.Delay(1200);
            await Publish(Conn);
        }

        internal void Unblock()
        {
            ExecRabbitMQCtl("set_vm_memory_high_watermark 0.4");
        }

        internal async ValueTask Publish(IConnection conn)
        {
            IModel ch = await conn.CreateModel();
            await ch.BasicPublish("amq.fanout", "", null, encoding.GetBytes("message"));
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

            public string Name
            {
                get; set;
            }

            public ConnectionInfo(string pid, string name)
            {
                Pid = pid;
                Name = name;
            }

            public override string ToString()
            {
                return $"pid = {Pid}, name: {Name}";
            }
        }

        private static readonly Regex GetConnectionName = new Regex(@"\{""connection_name"",""(?<connection_name>[^""]+)""\}");

        internal List<ConnectionInfo> ListConnections()
        {
            Process proc  = ExecRabbitMQCtl("list_connections --silent pid client_properties");
            string stdout = proc.StandardOutput.ReadToEnd();

            try
            {
                // {Environment.NewLine} is not sufficient
                string[] splitOn = new string[] { "\r\n", "\n" };
                string[] lines   = stdout.Split(splitOn, StringSplitOptions.RemoveEmptyEntries);
                // line: <rabbit@mercurio.1.11491.0>	{.../*client_properties*/...}
                return lines.Select(s =>
                {
                    string[] columns = s.Split('\t');
                    Debug.Assert(!string.IsNullOrEmpty(columns[0]), "columns[0] is null or empty!");
                    Debug.Assert(!string.IsNullOrEmpty(columns[1]), "columns[1] is null or empty!");
                    Match match = GetConnectionName.Match(columns[1]);
                    Debug.Assert(match.Success, "columns[1] is not in expected format.");
                    return new ConnectionInfo(columns[0], match.Groups["connection_name"].Value);
                }).ToList();
            }
            catch (Exception)
            {
                Console.WriteLine($"Bad response from rabbitmqctl list_connections --silent pid client_properties{Environment.NewLine}{stdout}");
                throw;
            }
        }

        internal void CloseConnection(IConnection conn)
        {
            ConnectionInfo ci = ListConnections().First(x => conn.ClientProvidedName == x.Name);
            CloseConnection(ci.Pid);
        }

        internal void CloseAllConnections()
        {
            List<ConnectionInfo> cs = ListConnections();
            foreach(ConnectionInfo c in cs)
            {
                CloseConnection(c.Pid);
            }
        }

        internal void CloseConnection(string pid)
        {
            ExecRabbitMQCtl($"close_connection \"{pid}\" \"Closed via rabbitmqctl\"");
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

        internal void Wait(ManualResetEventSlim latch)
        {
            Assert.IsTrue(latch.Wait(TimeSpan.FromSeconds(20)), "waiting on a latch timed out");
        }

        internal void Wait(ManualResetEventSlim latch, TimeSpan timeSpan)
        {
            Assert.IsTrue(latch.Wait(timeSpan), "waiting on a latch timed out");
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
        public static readonly TimeSpan TimingInterval = TimeSpan.FromMilliseconds(300);
        public static readonly TimeSpan TimingInterval_2X = TimeSpan.FromMilliseconds(600);
        public static readonly TimeSpan SafetyMargin = TimeSpan.FromMilliseconds(150);
        public static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    }
}
