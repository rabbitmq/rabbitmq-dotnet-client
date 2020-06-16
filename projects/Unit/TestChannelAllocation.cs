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

using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Unit
{

    [TestFixture]
  public class TestIModelAllocation
  {
    public const int CHANNEL_COUNT = 100;

    IConnection _c;

    public int ModelNumber(IModel model)
    {
      return ((ModelBase)((AutorecoveringModel)model).Delegate).Session.ChannelNumber;
    }

    [SetUp] public async ValueTask Connect()
    {
      _c = await new ConnectionFactory().CreateConnection();
    }

    [TearDown] public void Disconnect()
    {
      _c.Close();
    }


    [Test] public async ValueTask AllocateInOrder()
    {
      for(int i = 1; i <= CHANNEL_COUNT; i++)
        Assert.AreEqual(i, ModelNumber(await _c.CreateModel()));
    }

    [Test] public async ValueTask AllocateAfterFreeingLast() {
      IModel ch = await _c.CreateModel();
      Assert.AreEqual(1, ModelNumber(ch));
            await ch.Close();
      ch = await _c.CreateModel();
      Assert.AreEqual(1, ModelNumber(ch));
    }

    public int CompareModels(IModel x, IModel y)
    {
      int i = ModelNumber(x);
      int j = ModelNumber(y);
      return (i < j) ? -1 : (i == j) ? 0 : 1;
    }

    [Test] public async ValueTask AllocateAfterFreeingMany() {
      List<IModel> channels = new List<IModel>();

      for(int i = 1; i <= CHANNEL_COUNT; i++)
        channels.Add(await _c.CreateModel());

      foreach(IModel channel in channels){
                await channel.Close();
      }

      channels = new List<IModel>();

      for(int j = 1; j <= CHANNEL_COUNT; j++)
        channels.Add(await _c.CreateModel());

      // In the current implementation the list should actually
      // already be sorted, but we don't want to force that behaviour
      channels.Sort(CompareModels);

      int k = 1;
      foreach(IModel channel in channels)
        Assert.AreEqual(k++, ModelNumber(channel));
    }
  }
}
