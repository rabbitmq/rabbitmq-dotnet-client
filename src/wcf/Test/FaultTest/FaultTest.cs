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

namespace RabbitMQ.ServiceModel.Test.FaultTest
{
    using System;
    using System.ServiceModel;
    using System.ServiceModel.Channels;

    public class FaultTest : IServiceTest<IExplode>
    {
        ServiceHost m_service;
        ExplodingClient m_client;

        public void StartService(Binding binding)
        {
            Util.Write(ConsoleColor.Yellow, "  Binding Service...");
            m_service = new ServiceHost(typeof(ExplodingService), new Uri("soap.amqp:///"));
            m_service.AddServiceEndpoint(typeof(IExplode), binding, "FaultTest");
            m_service.Open();

            System.Threading.Thread.Sleep(500);
            Util.WriteLine(ConsoleColor.Green, "[DONE]");
        }

        public void StopService()
        {
            Util.Write(ConsoleColor.Yellow, "  Stopping Service...");
            m_service.Close();
            Util.WriteLine(ConsoleColor.Green, "[DONE]");
        }

        public IExplode GetClient(Binding binding)
        {
            m_client = new ExplodingClient(binding, new EndpointAddress("soap.amqp:///FaultTest"));
            m_client.Open();

            return m_client;
        }

        public void StopClient(IExplode client)
        {
            Util.Write(ConsoleColor.Yellow, "  Stopping Client...");
            this.m_client.Abort();

            Util.WriteLine(ConsoleColor.Green, "[DONE]");
        }

        public void Run()
        {
            try
            {
                StartService(Program.GetBinding());
                GetClient(Program.GetBinding());
                m_client.GoBang();
            }
            catch (FaultException)
            {
                Util.WriteLine(ConsoleColor.Magenta, "  The Service Faulted (as Expected)");
            }
            finally
            {
                StopClient(m_client);
                StopService();
            }

        }
    }
}
