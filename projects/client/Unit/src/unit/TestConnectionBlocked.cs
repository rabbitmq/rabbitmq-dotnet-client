// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2013 VMware, Inc.
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
//  Copyright (c) 2007-2013 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Threading;
using System.Diagnostics;

using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using RabbitMQ.Util;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Unit {
    [TestFixture]
    public class TestConnectionBlocked : IntegrationFixture {

        Object lockObject = new Object();
        UTF8Encoding enc = new UTF8Encoding();
        bool notified = false;

        [Test]
        public void TestConnectionBlockedNotification()
        {
	    Conn.ConnectionBlocked   += new ConnectionBlockedEventHandler(HandleBlocked);
	    Conn.ConnectionUnblocked += new ConnectionUnblockedEventHandler(HandleUnblocked);

	    Block();
	    Publish(Conn);
	    lock (lockObject) {
	        if(!notified) {
	            Monitor.Wait(lockObject, TimeSpan.FromSeconds(10));
	        }
                Assert.IsTrue(notified);
	    }
        }



        public void HandleBlocked(IConnection sender, ConnectionBlockedEventArgs args)
        {
	    Unblock();
        }


        public void HandleUnblocked(IConnection sender)
        {
	    notified = true;
	    Monitor.PulseAll(lockObject);
        }


        protected void Block()
        {
	    ExecRabbitMQCtl("set_vm_memory_high_watermark 0.000000001");
        }

        protected void Unblock()
        {
	    ExecRabbitMQCtl("set_vm_memory_high_watermark 0.4");
        }

        protected void ExecRabbitMQCtl(string args)
        {
	    if(IsRunningOnMono()) {
	        ExecCommand("../../../../../../rabbitmq-server/scripts/rabbitmqctl", args);
	    } else {
	        ExecCommand("..\\..\\..\\..\\..\\..\\rabbitmq-server\\scripts\\rabbitmqctl.bat", args);
	    }
	}

        protected void ExecCommand(string ctl, string args)
        {
	    Process proc = new Process();
	    proc.StartInfo.CreateNoWindow  = true;
	    proc.StartInfo.UseShellExecute = false;

	    string cmd;
	    if(IsRunningOnMono()) {
	        cmd  = ctl;
	    } else {
	        cmd  = "C:\\winnt\\system32\\cmd.exe";
		args = "/y /c " + ctl + " " + args;
	    }

	    try {
	      proc.StartInfo.FileName = cmd;
	      proc.StartInfo.Arguments = args;

	      proc.Start();
	    } catch (Exception e) {
	        Console.WriteLine("Failed to run subprocess with args " + args + " : " + e.Message);
	    }
	}

        public static bool IsRunningOnMono()
        {
	    return Type.GetType("Mono.Runtime") != null;
	}

        protected void Publish(IConnection conn)
        {
	    IModel ch = conn.CreateModel();
	    ch.BasicPublish("", "amq.fanout", null, enc.GetBytes("message"));
        }

        protected override void ReleaseResources()
        {
	    Unblock();
        }
    }
}