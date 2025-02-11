// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Client.Messages;
using System;
using System.Collections.Generic;

namespace Metaplay.Core.Network
{
    /// <summary>
    /// Tool for tracking ongoing latency measurements on a MessageTransport.
    /// </summary>
    public class MessageTransportPingTracker
    {
        /// <summary>
        /// The first 32 bits of 64-bit ping payloads used for latency samples. These are the lowest bits.
        /// </summary>
        const uint LatencySamplePingPrefix = 0x1234ABAB;

        /// <summary>
        /// Emitted when latency sample completes on a transport.
        /// </summary>
        public class LatencySampleInfo : MessageTransport.Info
        {
            public readonly MessageTransportLatencySampleMessage LatencySample;
            public LatencySampleInfo(MessageTransportLatencySampleMessage latencySample)
            {
                LatencySample = latencySample;
            }
        }

        Dictionary<int, DateTime> _pingSentAt = new Dictionary<int, DateTime>();

        /// <summary>
        /// Starts tracking the given latency sample. This is called when a Ping (with the latency sample Id <paramref name="latencySampleId"/> encoded in its payload) is being sent.
        /// </summary>
        public void OnAboutToSendLatencySample(int latencySampleId)
        {
            _pingSentAt[latencySampleId] = DateTime.UtcNow;
        }

        /// <summary>
        /// Tries to decode any ongoing latency sample from a received PingResponse frame.
        /// </summary>
        public bool TryReceiveLatencyMeasurementFromPingResponse(ReadOnlySpan<byte> payload, out LatencySampleInfo info)
        {
            if (payload.Length != 8 || BitConverter.ToInt32(payload) != LatencySamplePingPrefix)
            {
                info = null;
                return false;
            }

            int latencySampleId = BitConverter.ToInt32(payload.Slice(start: 4));
            if (!_pingSentAt.Remove(latencySampleId, out DateTime sentAt))
            {
                info = null;
                return false;
            }

            info = new LatencySampleInfo(new MessageTransportLatencySampleMessage(latencySampleId, sentAt, pongReceivedAt: DateTime.UtcNow));
            return true;
        }

        /// <summary>
        /// Creates the ping payload for identifying a the given latency sample.
        /// </summary>
        public static ulong EncodePingPayload(int latencySampleId)
        {
            return (((ulong)latencySampleId) << 32) | LatencySamplePingPrefix;
        }
    }
}
