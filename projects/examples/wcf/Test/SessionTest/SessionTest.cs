// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2013 GoPivotal, Inc.
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


namespace RabbitMQ.ServiceModel.Test.SessionTest
{
    using System;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.Threading;

    public class SessionTest : IServiceTest<ICart>
    {
        ServiceHost m_service;
        ChannelFactory<ICart> m_factory;

        public void StartService(Binding binding)
        {
            Util.Write(ConsoleColor.Yellow, "  Binding Service...");
            m_service = new ServiceHost(typeof(Cart), new Uri("soap.amqp:///"));
            m_service.AddServiceEndpoint(typeof(ICart), binding, "Cart");
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

        public ICart GetClient(Binding binding)
        {
            m_factory = new ChannelFactory<ICart>(binding, "soap.amqp:///Cart");
            m_factory.Open();
            return m_factory.CreateChannel();
        }

        public void StopClient(ICart client)
        {
            Util.Write(ConsoleColor.Yellow, "  Stopping Client...");
            ((IChannel)client).Close();
            m_factory.Close();
            Util.WriteLine(ConsoleColor.Green, "[DONE]");
        }

        private void AddToCart(ICart cart, string name, double price) {
            CartItem item = new CartItem();
            item.Name = name;
            item.Price = price;
            Util.WriteLine(ConsoleColor.Magenta, "  Adding {0} to cart", name);
            cart.Add(item);
        }

        public void Run()
        {
            StartService(Program.GetBinding());

            ICart cart = GetClient(Program.GetBinding());

            AddToCart(cart, "Beans", 0.49);
            AddToCart(cart, "Bread", 0.89);
            AddToCart(cart, "Toaster", 4.99);

            double total = cart.GetTotal();
            if (total != (0.49 + 0.89 + 4.99))
                throw new Exception("Incorrect Total");

            Util.WriteLine(ConsoleColor.Magenta, "  Total: {0}", total);

            try
            {
                StopClient(cart);
                StopService();
            }
            catch (Exception)
            {
                Util.WriteLine(ConsoleColor.Red, "  Failed to Close Gracefully.");
            }
        }
    }
}
