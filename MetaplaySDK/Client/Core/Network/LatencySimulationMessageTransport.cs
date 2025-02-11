// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Client.Messages;
using Metaplay.Core;
using Metaplay.Core.Message;
using Metaplay.Core.MultiplayerEntity.Messages;
using Metaplay.Core.Network;
using Metaplay.Core.Tasks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Metaplay.Core.Network
{
    public class LatencySimulationMessageTransport : MessageTransport
    {
        /// <summary>
        /// The amount of latency to add to sent and received messages in milliseconds.
        /// </summary>
        public int ArtificialLatency
        {
            get;
            private set;
        }

        IMessageTransport _innerTransport;
        readonly object   _lock;

        readonly TaskQueueExecutor<LatencySimulationMessageTransport, Action<LatencySimulationMessageTransport, object>, object, long> _sendQueueExecutor;
        readonly TaskQueueExecutor<LatencySimulationMessageTransport, Action<LatencySimulationMessageTransport, object>, object, long> _receiveQueueExecutor;

        // Latency is halved because it applies to both directions
        int _halvedLatency;

        public LatencySimulationMessageTransport(IMessageTransport innerTransport, LogChannel log = null)
        {
            _innerTransport           =  innerTransport;
            _innerTransport.OnConnect += HandleOnConnect;
            _innerTransport.OnReceive += HandleOnReceive;
            _innerTransport.OnInfo    += HandleOnInfo;
            _innerTransport.OnError   += HandleOnError;

            // For WEBGL platforms, we use the Unity TaskScheduler as threading is not available.
            // Otherwise we use the default task scheduler as the Unity TaskScheduler does not always appear to execute tasks
            // in the right order, causing checksum mismatches.
            #if UNITY_WEBGL && !UNITY_EDITOR
            _sendQueueExecutor    = new TaskQueueExecutor<LatencySimulationMessageTransport, Action<LatencySimulationMessageTransport, object>, object, long>(MetaTask.UnityMainScheduler, log);
            _receiveQueueExecutor = new TaskQueueExecutor<LatencySimulationMessageTransport, Action<LatencySimulationMessageTransport, object>, object, long>(MetaTask.UnityMainScheduler, log);
            #else
            _sendQueueExecutor    = new TaskQueueExecutor<LatencySimulationMessageTransport, Action<LatencySimulationMessageTransport, object>, object, long>(TaskScheduler.Default, log);
            _receiveQueueExecutor = new TaskQueueExecutor<LatencySimulationMessageTransport, Action<LatencySimulationMessageTransport, object>, object, long>(TaskScheduler.Default, log);
            #endif

            _lock = new object();
        }

        /// <summary>
        /// Update the latency added to sent and received messages in milliseconds
        /// </summary>
        public void UpdateLatency(int newLatency)
        {
            if (ArtificialLatency == newLatency)
                return;

            if (newLatency < 0)
                newLatency = 0;

            ArtificialLatency = newLatency;

            lock (_lock)
            {
                _halvedLatency = newLatency / 2;
            }
        }

        void HandleOnConnect(Handshake.ServerHello serverHello, TransportHandshakeReport handshakeReport)
        {
            lock(_lock)
            {
                EnqueueReceiveMessage(() => InvokeOnConnect(serverHello, handshakeReport));
            }
        }

        void HandleOnReceive(MetaMessage message)
        {
            lock(_lock)
            {
                EnqueueReceiveMessage(message, (self,  msgObject) => self.InvokeOnReceive((MetaMessage)msgObject));
            }
        }

        void HandleOnInfo(Info info)
        {
            if (_halvedLatency > 0 && info is MessageTransportPingTracker.LatencySampleInfo sampleInfo)
            {
                MessageTransportLatencySampleMessage latencySample = new MessageTransportLatencySampleMessage(sampleInfo.LatencySample.LatencySampleId, sampleInfo.LatencySample.PingSentAt.AddMilliseconds(-ArtificialLatency), sampleInfo.LatencySample.PongReceivedAt);
                info = new MessageTransportPingTracker.LatencySampleInfo(latencySample);
            }

            lock(_lock)
            {
                EnqueueReceiveMessage(info, (self,  infoObj) => self.InvokeOnInfo((Info)infoObj));
            }
        }

        void HandleOnError(Error error)
        {
            lock(_lock)
            {
                EnqueueReceiveMessage(error, (self, payload) => self.InvokeOnError((Error)payload));
            }
        }

        public override void SetDebugDiagnosticsRef(LoginTransportDebugDiagnostics debugDiagnostics)
        {
            _innerTransport?.SetDebugDiagnosticsRef(debugDiagnostics);
        }

        public override void Open()
        {
            lock(_lock)
            {
                EnqueueSendMessage(() => _innerTransport.Open());
            }
        }

        public override void EnqueueSendMessage(MetaMessage message)
        {
            lock (_lock)
            {
                EnqueueSendMessage(message, (self, payload) => self._innerTransport.EnqueueSendMessage((MetaMessage)payload));
            }
        }

        public override void EnqueueClose(object closedErrorPayload)
        {
            lock (_lock)
            {
                EnqueueSendMessage(closedErrorPayload, (self, payload) => self._innerTransport.EnqueueClose(payload));
            }
        }

        public override MessageTransportWriteFence EnqueueWriteFence()
        {
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();

            lock (_lock)
            {
                EnqueueSendMessage(
                    () =>
                    {
                        #pragma warning disable VSTHRD110
                        // Don't need to await here since we're using ContinueWith
                        _innerTransport.EnqueueWriteFence().WhenComplete.ContinueWithCtx((x, tcs) => ((TaskCompletionSource<int>)tcs).TrySetResult(0), tcs).ConfigureAwaitFalse();
                        #pragma warning restore VSTHRD110
                    });
            }

            return new MessageTransportWriteFence(tcs.Task);
        }

        public override void EnqueueInfo(Info info)
        {
            lock (_lock)
            {
                EnqueueSendMessage(info, (self, payload) => self._innerTransport.EnqueueInfo((Info)payload));
            }
        }

        public override void EnqueueLatencySampleMeasurement(int latencySampleId)
        {
            lock (_lock)
            {
                EnqueueSendMessage(latencySampleId, (self, payload) => self._innerTransport.EnqueueLatencySampleMeasurement((int)payload));
            }
        }

        public override void Dispose()
        {
            _innerTransport.Dispose();
            _innerTransport = null;
        }

        void EnqueueSendMessage(Action action)
        {
            PostEvent(_sendQueueExecutor, action);
        }

        void EnqueueSendMessage(object payload, Action<LatencySimulationMessageTransport, object> action)
        {
            PostEvent(_sendQueueExecutor, payload, action);
        }

        void EnqueueReceiveMessage(Action action)
        {
            PostEvent(_receiveQueueExecutor, action);
        }

        void EnqueueReceiveMessage(object payload, Action<LatencySimulationMessageTransport, object> action)
        {
            PostEvent(_receiveQueueExecutor, payload, action);
        }

        void PostEvent(TaskQueueExecutor<LatencySimulationMessageTransport, Action<LatencySimulationMessageTransport, object>, object, long> executor, Action action)
        {
            PostEvent(executor, action, (self,  action_) =>
            {
                Action action = (Action)action_;
                action();
            });
        }

        void PostEvent(TaskQueueExecutor<LatencySimulationMessageTransport, Action<LatencySimulationMessageTransport, object>, object, long> executor, object payload, Action<LatencySimulationMessageTransport, object> action)
        {
            executor.EnqueueAsync(this, action, payload, DateTimeOffset.UtcNow.Ticks, async (self,  action,  payload, timestamp) =>
            {
                long startTime   = timestamp / TimeSpan.TicksPerMillisecond;
                long currentTime = DateTimeOffset.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
                if (_halvedLatency > 0 && startTime + _halvedLatency > currentTime)
                    await MetaTask.Delay((int)((startTime + _halvedLatency) - currentTime)).ConfigureAwaitFalse();

                // Don't push events after dispose
                bool isDisposed = (self._innerTransport == null);
                if (isDisposed)
                    return;

                action(self, payload);
            });
        }
    }
}
