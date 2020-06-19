using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.Client.Framing
{
    internal static class MethodConstants
    {
        // Connection = 10
        internal const uint ConnectionStart = (10 << 16) | 10;
        internal const uint ConnectionStartOk = (10 << 16) | 11;
        internal const uint ConnectionSecure = (10 << 16) | 20;
        internal const uint ConnectionSecureOk = (10 << 16) | 21;
        internal const uint ConnectionTune = (10 << 16) | 30;
        internal const uint ConnectionTuneOk = (10 << 16) | 31;
        internal const uint ConnectionOpen = (10 << 16) | 40;
        internal const uint ConnectionOpenOk = (10 << 16) | 41;
        internal const uint ConnectionClose = (10 << 16) | 50;
        internal const uint ConnectionCloseOk = (10 << 16) | 51;
        internal const uint ConnectionBlocked = (10 << 16) | 60;
        internal const uint ConnectionUnblocked = (10 << 16) | 61;
        internal const uint ConnectionUpdateSecret = (10 << 16) | 70;
        internal const uint ConnectionUpdateSecretOk = (10 << 16) | 71;

        // Channel = 20
        internal const uint ChannelOpen = (20 << 16) | 10;
        internal const uint ChannelOpenOk = (20 << 16) | 11;
        internal const uint ChannelFlow = (20 << 16) | 20;
        internal const uint ChannelFlowOk = (20 << 16) | 21;
        internal const uint ChannelClose = (20 << 16) | 40;
        internal const uint ChannelCloseOk = (20 << 16) | 41;

        // Exchange = 40
        internal const uint ExchangeDeclare = (40 << 16) | 10;
        internal const uint ExchangeDeclareOk = (40 << 16) | 11;
        internal const uint ExchangeDelete = (40 << 16) | 20;
        internal const uint ExchangeDeleteOk = (40 << 16) | 21;
        internal const uint ExchangeBind = (40 << 16) | 30;
        internal const uint ExchangeBindOk = (40 << 16) | 31;
        internal const uint ExchangeUnbind = (40 << 16) | 40;
        internal const uint ExchangeUnbindOk = (40 << 16) | 51;

        // Queue = 50
        internal const uint QueueDeclare = (50 << 16) | 10;
        internal const uint QueueDeclareOk = (50 << 16) | 11;
        internal const uint QueueBind = (50 << 16) | 20;
        internal const uint QueueBindOk = (50 << 16) | 21;
        internal const uint QueueUnbind = (50 << 16) | 50;
        internal const uint QueueUnbindOk = (50 << 16) | 51;
        internal const uint QueuePurge = (50 << 16) | 30;
        internal const uint QueuePurgeOk = (50 << 16) | 31;
        internal const uint QueueDelete = (50 << 16) | 40;
        internal const uint QueueDeleteOk = (50 << 16) | 41;

        // Basic = 60
        internal const uint BasicQos = (60 << 16) | 10;
        internal const uint BasicQosOk = (60 << 16) | 11;
        internal const uint BasicConsume = (60 << 16) | 20;
        internal const uint BasicConsumeOk = (60 << 16) | 21;
        internal const uint BasicCancel = (60 << 16) | 30;
        internal const uint BasicCancelOk = (60 << 16) | 31;
        internal const uint BasicPublish = (60 << 16) | 40;
        internal const uint BasicReturn = (60 << 16) | 50;
        internal const uint BasicDeliver = (60 << 16) | 60;
        internal const uint BasicGet = (60 << 16) | 70;
        internal const uint BasicGetOk = (60 << 16) | 71;
        internal const uint BasicGetEmpty = (60 << 16) | 72;
        internal const uint BasicAck = (60 << 16) | 80;
        internal const uint BasicReject = (60 << 16) | 90;
        internal const uint BasicRecoverAsync = (60 << 16) | 100;
        internal const uint BasicRecover = (60 << 16) | 110;
        internal const uint BasicRecoverOk = (60 << 16) | 111;
        internal const uint BasicNack = (60 << 16) | 120;

        // Confirm = 85
        internal const uint ConfirmSelect = (85 << 16) | 10;
        internal const uint ConfirmSelectOk = (85 << 16) | 11;

        // Tx = 90
        internal const uint TxSelect = (90 << 16) | 10;
        internal const uint TxSelectOk = (90 << 16) | 11;
        internal const uint TxCommit = (90 << 16) | 20;
        internal const uint TxCommitOk = (90 << 16) | 21;
        internal const uint TxRollback = (90 << 16) | 30;
        internal const uint TxRollbackOk = (90 << 16) | 31;
    }
}
