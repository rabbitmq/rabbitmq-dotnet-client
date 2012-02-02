// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2012 VMware, Inc.
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
//  Copyright (c) 2007-2012 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------


namespace RabbitMQ.ServiceModel.Test.DuplexTest
{
    using System;
    using System.Collections.Generic;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.Threading;

    public class DuplexTest : IServiceTest<IPizzaService>, IPizzaCallback
    {
        Uri serverUri = new Uri("soap.amqp:///"); // TODO: Add Duplex Service Uri
        ServiceHost service;
        ManualResetEvent mre;

        public void StartService(Binding binding)
        {
            Util.Write(ConsoleColor.Yellow, "  Binding Service...");
            service = new ServiceHost(typeof(PizzaService));
            service.AddServiceEndpoint(typeof(IPizzaService), binding, serverUri);
            service.Open();

            Thread.Sleep(500);
            Util.WriteLine(ConsoleColor.Green, "[DONE]");
        }

        public void StopService()
        {
            Util.Write(ConsoleColor.Yellow, "  Stopping Service...");
            service.Close();
            Util.WriteLine(ConsoleColor.Green, "[DONE]");
        }

        public IPizzaService GetClient(Binding binding)
        {
            PizzaClient client = new PizzaClient(new InstanceContext(this), binding, new EndpointAddress(serverUri.ToString()));
            client.Open();
            return client;
        }

        public void StopClient(IPizzaService client)
        {
            Util.Write(ConsoleColor.Yellow, "  Stopping Client...");
            ((PizzaClient)client).Close();
            Util.WriteLine(ConsoleColor.Green, "[DONE]");
        }

        public void Run()
        {
            mre = new ManualResetEvent(false);
            StartService(Program.GetBinding());

            IPizzaService client = GetClient(Program.GetBinding());
            Order lunch = new Order();
            lunch.Items = new List<Pizza>();
            lunch.Items.Add(new Pizza(PizzaBase.ThinCrust, "Meat Feast"));
            client.PlaceOrder(lunch);
            mre.WaitOne();
            try
            {
                StopClient(client);
                StopService();
            }
            catch (Exception)
            {
                Util.WriteLine(ConsoleColor.Red, "  Failed to Close Gracefully.");
            }
        }

        public void OrderReady(Guid id)
        {
            Util.WriteLine(ConsoleColor.Magenta, "  [CLI] Order {0} has been delivered.",id);
            mre.Set();
        }
    }
}
