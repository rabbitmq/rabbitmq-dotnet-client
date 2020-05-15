using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.Framing.Impl
{
    internal sealed partial class Connection
    {
        private readonly CancellationTokenSource _heartbeatCancellation = new CancellationTokenSource();
        private TimeSpan _heartbeat = TimeSpan.Zero;
        private TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(0);
        private DateTime _lastHeartbeat;
        private int _missedHeartbeats = 0;

        private Task _heartbeatWriteTask;
        private Task _heartbeatReadTask;

        public void MaybeStartHeartbeatTimers()
        {
            if (Heartbeat != TimeSpan.Zero)
            {
                _heartbeatWriteTask = Task.Run(WriteHeartbeats);
                _heartbeatReadTask = Task.Run(ReadHeartbeats);
            }
        }

        public async ValueTask ReadHeartbeats()
        {
            bool shouldTerminate = false;
            _lastHeartbeat = DateTime.UtcNow;
            try
            {
                while (!_heartbeatCancellation.IsCancellationRequested)
                {
                    if ((DateTime.UtcNow - _lastHeartbeat) > _heartbeat)
                    {
                        _missedHeartbeats++;
                    }
                    else
                    {
                        _missedHeartbeats = 0;
                    }

                    // If we have two missed heartbeats, consider the connection dead.
                    if (_missedHeartbeats == 2)
                    {
                        string description = string.Format("Heartbeat missing with heartbeat == {0} seconds", _heartbeat);
                        var eose = new EndOfStreamException(description);
                        ESLog.Error(description, eose);
                        ShutdownReport.Add(new ShutdownReportEntry(description, eose));
                        await HandleMainLoopException(new ShutdownEventArgs(ShutdownInitiator.Library, 0, "End of stream", eose)).ConfigureAwait(false);
                        shouldTerminate = true;
                    }

                    // Let's sleep for just a little less than the heartbeat interval
                    await Task.Delay(_heartbeatInterval, _heartbeatCancellation.Token).ConfigureAwait(false);
                }

                if (shouldTerminate)
                {
                    TerminateMainloop();
                    await FinishClose().ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                // Swallow exceptions if the connection is being closed.
            }
            catch (NullReferenceException)
            {
                // timer has already been disposed from a different thread after null check
                // this event should be rare
            }
        }

        public async ValueTask WriteHeartbeats()
        {
            try
            {
                while (!_heartbeatCancellation.IsCancellationRequested)
                {
                    if (!_writeLock.Wait(0))
                    {
                        await _writeLock.WaitAsync().ConfigureAwait(false);
                    }

                    try
                    {
                        WriteEmptyFrame();
                        await Flush().ConfigureAwait(false);
                    }
                    finally
                    {
                        _writeLock.Release();
                    }

                    await Task.Delay(_heartbeatInterval, _heartbeatCancellation.Token).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                // Swallow task cancellations.
            }
            catch (Exception)
            {
                // ignore, let the read callback detect
                // peer unavailability. See rabbitmq/rabbitmq-dotnet-client#638 for details.
            }

            void WriteEmptyFrame()
            {
                EmptyOutboundFrame emptyFrame = new EmptyOutboundFrame();
                _frameHandler.WriteFrame(in emptyFrame);
            }
        }

        void MaybeStopHeartbeatTimers()
        {
            NotifyHeartbeatListener();
            _heartbeatCancellation.Cancel();
        }

        public void NotifyHeartbeatListener()
        {
            _lastHeartbeat = DateTime.UtcNow;
        }
    }
}
