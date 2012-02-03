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


namespace RabbitMQ.ServiceModel.Test.TwoWayTest
{
    using System;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.Threading;
    using RabbitMQ.Client;

    public class TwoWayTest : IServiceTest<ICalculator>
    {
        ServiceHost m_service; 
        ChannelFactory<ICalculator> m_fac;

        public void StartService(Binding binding)
        {
            Util.Write(ConsoleColor.Yellow, "  Binding Service...");
            m_service = new ServiceHost(typeof(Calculator), new Uri("soap.amqp:///"));
            m_service.AddServiceEndpoint(typeof(ICalculator), binding, "Calculator");
            m_service.Open();
            
            Thread.Sleep(500);
            Util.WriteLine(ConsoleColor.Green, "[DONE]");
        }

        public void StopService()
        {

            Util.Write(ConsoleColor.Yellow, "  Stopping Service...");
            m_service.Close();
            Util.WriteLine(ConsoleColor.Green, "[DONE]");
        }

        public ICalculator GetClient(Binding binding)
        {
            m_fac = new ChannelFactory<ICalculator>(binding, "soap.amqp:///Calculator");
            m_fac.Open();
            return m_fac.CreateChannel();
        }

        public void StopClient(ICalculator client)
        {
            Util.Write(ConsoleColor.Yellow, "  Stopping Client...");
            ((IChannel)client).Close();

            // Factories share a *SINGLE* input channel for *all* proxies managed by them.
            // Closing the individual proxy closes only its output channel.
            // To close the sole input channel, closing the actual factory is required.
            m_fac.Close();
            Util.WriteLine(ConsoleColor.Green, "[DONE]");
        }

        public void Run()
        {
            StartService(Program.GetBinding());

            ICalculator calc = GetClient(Program.GetBinding());

            int result = 0, x = 3, y = 4;
            Util.WriteLine(ConsoleColor.Magenta, "  {0} + {1} = {2}", x, y, result = calc.Add(x, y));
            if (result != x + y)
                throw new Exception("Test Failed");

            Util.WriteLine(ConsoleColor.Magenta, "  {0} - {1} = {2}", x, y, result = calc.Subtract(x, y));
            if (result != x - y)
                throw new Exception("Test Failed");

            try
            {
                StopClient(calc);
                StopService();
            }
            catch (Exception e)
            {
                Util.WriteLine(ConsoleColor.Red, "  Failed to Close Gracefully: "+e);
            }
        }
    }
}
