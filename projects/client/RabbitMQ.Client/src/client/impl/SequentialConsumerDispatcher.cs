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
using System.Collections.Generic;
using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client
{
    sealed class SequentialConsumerDispatcher : IConsumerDispatcher
    {
        private ModelBase model;
        public SequentialConsumerDispatcher(ModelBase model)
        {
            this.model = model;
        }

        public void HandleBasicConsumeOk(IBasicConsumer consumer,
                                         string consumerTag)
        {
            try
            {
                consumer.HandleBasicConsumeOk(consumerTag);
            }
            catch (Exception e)
            {
                this.model.OnCallbackException(CallbackExceptionEventArgs.Build(e, new Dictionary<string, object>()
                    {
                        {"consumer", consumer},
                        {"context",  "HandleBasicConsumeOk"}
                    }));
            }
        }

        public void HandleBasicDeliver(IBasicConsumer consumer,
                            string consumerTag,
                            ulong deliveryTag,
                            bool redelivered,
                            string exchange,
                            string routingKey,
                            IBasicProperties basicProperties,
                            byte[] body)
        {
            try
            {
                consumer.HandleBasicDeliver(consumerTag, deliveryTag, redelivered,
                                            exchange, routingKey, basicProperties, body);
            }
            catch (Exception e)
            {
                this.model.OnCallbackException(CallbackExceptionEventArgs.Build(e, new Dictionary<string, object>()
                    {
                        {"consumer", consumer},
                        {"context",  "HandleBasicDeliver"}
                    }));
            }
        }

        public void HandleBasicCancelOk(IBasicConsumer consumer,
                            string consumerTag)
        {
            try
            {
                consumer.HandleBasicCancelOk(consumerTag);
            }
            catch (Exception e)
            {
                this.model.OnCallbackException(CallbackExceptionEventArgs.Build(e, new Dictionary<string, object>()
                    {
                        {"consumer", consumer},
                        {"context",  "HandleBasicCancelOk"}
                    }));
            }
        }

        public void HandleBasicCancel(IBasicConsumer consumer,
                          string consumerTag)
        {
            try
            {
                consumer.HandleBasicCancel(consumerTag);
            }
            catch (Exception e)
            {
                this.model.OnCallbackException(CallbackExceptionEventArgs.Build(e, new Dictionary<string, object>()
                    {
                        {"consumer", consumer},
                        {"context",  "HandleBasicCancel"}
                    }));
            }
        }

        public void Quiesce()
        {
            // no-op
        }
    }
}