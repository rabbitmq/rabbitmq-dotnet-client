// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2014 GoPivotal, Inc.
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

using System;

using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    public class RecoveryAwareModel : Model, IFullModel, IRecoverable
    {
        private ulong maxSeenDeliveryTag = 0;
        private ulong activeDeliveryTagOffset = 0;

        public ulong MaxSeenDeliveryTag
        {
            get { return maxSeenDeliveryTag; }
        }

        public ulong ActiveDeliveryTagOffset
        {
            get { return activeDeliveryTagOffset; }
        }

        public RecoveryAwareModel(ISession session) : base(session) {}

        public override void HandleBasicDeliver(string consumerTag,
                                                ulong deliveryTag,
                                                bool redelivered,
                                                string exchange,
                                                string routingKey,
                                                IBasicProperties basicProperties,
                                                byte[] body)
        {
            if(deliveryTag > maxSeenDeliveryTag)
            {
                maxSeenDeliveryTag = deliveryTag;
            }

            base.HandleBasicDeliver(consumerTag,
                                    OffsetDeliveryTag(deliveryTag),
                                    redelivered,
                                    exchange,
                                    routingKey,
                                    basicProperties,
                                    body);
        }

        public override void BasicAck(ulong deliveryTag,
                                      bool multiple) {
            var realTag = deliveryTag - activeDeliveryTagOffset;
            if(realTag > 0)
            {
                base.BasicAck(realTag, multiple);
            }
        }

        public override void BasicReject(ulong deliveryTag,
                                         bool requeue) {
            var realTag = deliveryTag - activeDeliveryTagOffset;
            if(realTag > 0)
            {
                base.BasicReject(deliveryTag, requeue);
            }
        }

        public override void BasicNack(ulong deliveryTag,
                                       bool multiple,
                                       bool requeue) {
            var realTag = deliveryTag - activeDeliveryTagOffset;
            if(realTag > 0)
            {
                base.BasicNack(deliveryTag, multiple, requeue);
            }
        }

        public void InheritOffsetFrom(RecoveryAwareModel other)
        {
            this.activeDeliveryTagOffset = other.ActiveDeliveryTagOffset + other.MaxSeenDeliveryTag;
            this.maxSeenDeliveryTag = 0;
        }

        protected ulong OffsetDeliveryTag(ulong deliveryTag)
        {
            return deliveryTag + this.activeDeliveryTagOffset;
        }
    }
}