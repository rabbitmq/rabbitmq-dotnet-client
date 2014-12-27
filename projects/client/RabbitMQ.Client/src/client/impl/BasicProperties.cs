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

namespace RabbitMQ.Client.Impl
{
    public abstract class BasicProperties : ContentHeaderBase, IBasicProperties
    {
        public abstract string AppId { get; set; }
        public abstract string ClusterId { get; set; }
        public abstract string ContentEncoding { get; set; }
        public abstract string ContentType { get; set; }
        public abstract string CorrelationId { get; set; }
        public abstract byte DeliveryMode { get; set; }
        public abstract string Expiration { get; set; }
        public abstract IDictionary<string, object> Headers { get; set; }
        public abstract string MessageId { get; set; }
        public abstract byte Priority { get; set; }
        public abstract string ReplyTo { get; set; }

        public PublicationAddress ReplyToAddress
        {
            get { return PublicationAddress.Parse(ReplyTo); }
            set { ReplyTo = value.ToString(); }
        }

        public abstract AmqpTimestamp Timestamp { get; set; }
        public abstract string Type { get; set; }
        public abstract string UserId { get; set; }
        public abstract void ClearAppId();
        public abstract void ClearClusterId();
        public abstract void ClearContentEncoding();
        public abstract void ClearContentType();
        public abstract void ClearCorrelationId();
        public abstract void ClearDeliveryMode();
        public abstract void ClearExpiration();
        public abstract void ClearHeaders();
        public abstract void ClearMessageId();
        public abstract void ClearPriority();
        public abstract void ClearReplyTo();
        public abstract void ClearTimestamp();
        public abstract void ClearType();
        public abstract void ClearUserId();
        public abstract bool IsAppIdPresent();
        public abstract bool IsClusterIdPresent();
        public abstract bool IsContentEncodingPresent();
        public abstract bool IsContentTypePresent();
        public abstract bool IsCorrelationIdPresent();
        public abstract bool IsDeliveryModePresent();
        public abstract bool IsExpirationPresent();
        public abstract bool IsHeadersPresent();
        public abstract bool IsMessageIdPresent();
        public abstract bool IsPriorityPresent();
        public abstract bool IsReplyToPresent();
        public abstract bool IsTimestampPresent();
        public abstract bool IsTypePresent();
        public abstract bool IsUserIdPresent();

        public void SetPersistent(bool persistent)
        {
            if (persistent)
            {
                DeliveryMode = 2;
            }
            else
            {
                DeliveryMode = 1;
            }
        }

        public override object Clone()
        {
            var clone = MemberwiseClone() as BasicProperties;
            if (IsHeadersPresent())
            {
                clone.Headers = new Dictionary<string, object>();
                foreach (KeyValuePair<string, object> entry in Headers)
                {
                    clone.Headers[entry.Key] = entry.Value;
                }
            }

            return clone;
        }
    }
}
