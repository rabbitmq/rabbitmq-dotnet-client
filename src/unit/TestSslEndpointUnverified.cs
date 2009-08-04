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
using NUnit.Framework;
using System;
using RabbitMQ.Client;

[TestFixture]
public class TestSslEndpointUnverified {

    public void SendReceive(IConnection conn) {
        string message = "Hello C# SSL Client World";

        IModel ch = conn.CreateModel();

        ch.ExchangeDeclare("Exchange_TestSslEndPoint", ExchangeType.Direct);
        ch.QueueDeclare("Queue_TestSslEndpoint");
        ch.QueueBind("Queue_TestSslEndpoint", "Exchange_TestSslEndPoint", "Key_TestSslEndpoint", false, null);

        byte[] msgBytes =  System.Text.Encoding.UTF8.GetBytes(message);
        ch.BasicPublish("Exchange_TestSslEndPoint", "Key_TestSslEndpoint", null, msgBytes);

        bool noAck = false;

        BasicGetResult result = ch.BasicGet("Queue_TestSslEndpoint", noAck);
        byte[] body = result.Body;

        string resultMessage = System.Text.Encoding.UTF8.GetString(body);

        ch.Close(200, "Closing the channel");
        conn.Close();

        Assert.AreEqual(message, resultMessage);
    }


    [Test]
    public void TestHostWithPort() {
        string sslDir = Environment.GetEnvironmentVariable("SSL_CERTS_DIR");
        if (null == sslDir) {
            return;
        } else {
            ConnectionFactory cf = new ConnectionFactory();

            cf.Parameters.Ssl.ServerName = System.Net.Dns.GetHostName();
            cf.Parameters.Ssl.Enabled = true;

            IProtocol proto = Protocols.DefaultProtocol;
            IConnection conn = cf.CreateConnection(proto, "localhost", 5671);
            SendReceive(conn);
        }
    }
}
