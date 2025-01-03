// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
// The MPL v2.0:
//
//---------------------------------------------------------------------------
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
using System.Text;
using RabbitMQ.Client;

ConnectionFactory connectionFactory = new()
{
    AutomaticRecoveryEnabled = true,
    UserName = "guest",
    Password = "guest"
};

var channelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);

var props = new BasicProperties();
byte[] msg = Encoding.UTF8.GetBytes("test");
await using var connection = await connectionFactory.CreateConnectionAsync();
for (int i = 0; i < 300; i++)
{
    try
    {
        await using var channel = await connection.CreateChannelAsync(channelOptions); // New channel for each message
        await Task.Delay(1000);
        try
        {
            await channel.BasicPublishAsync(exchange: string.Empty, routingKey: string.Empty,
                mandatory: false, basicProperties: props, body: msg);
            Console.WriteLine($"Sent message {i}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] message {i} not acked: {ex}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to send message {i}: {ex.Message}");
        await Task.Delay(1000);
    }
}
