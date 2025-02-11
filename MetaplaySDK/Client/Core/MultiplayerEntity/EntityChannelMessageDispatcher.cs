// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Config;
using Metaplay.Core.IO;
using Metaplay.Core.Message;
using Metaplay.Core.MultiplayerEntity.Messages;
using Metaplay.Core.Network;
using Metaplay.Core.Serialization;
using System;
using System.Collections.Generic;

namespace Metaplay.Core.MultiplayerEntity
{
    /// <summary>
    /// Message dispatcher for a single entity, specified with a Channel.
    /// </summary>
    public class EntityMessageDispatcher : BasicMessageDispatcher
    {
        IMessageDispatcher _rootDispatcher;
        int _channel;
        int _logicVersion;
        ISharedGameConfig _config;
        int _bufferChannel;
        List<MetaSerialized<MetaMessage>> _buffer;
        Func<MetaMessage, bool> _overrideSendMethod;

        public override ServerConnection ServerConnection => _rootDispatcher?.ServerConnection;

        /// <summary>
        /// The channel id of the currently bound entity. -1 if there is no entity bound.
        /// </summary>
        public int ChannelId => _channel;

        public EntityMessageDispatcher(LogChannel log, IMessageDispatcher rootDispatcher) : base(log)
        {
            _rootDispatcher = rootDispatcher;
            ResetPeer();

            _rootDispatcher.AddListener<EntityServerToClientEnvelope>(OnEnvelope);
        }

        public void Dispose()
        {
            _rootDispatcher?.RemoveListener<EntityServerToClientEnvelope>(OnEnvelope);
            _rootDispatcher = null;
        }

        protected override bool SendMessageInternal(MetaMessage message)
        {
            Func<MetaMessage, bool> overrideSendMethod = _overrideSendMethod;
            if (overrideSendMethod != null)
            {
                return overrideSendMethod(message);
            }
            else
            {
                // \note: we allow sending even if Channel == -1 or even if we otherwise know the channel is not valid. A warning will be printed on server.
                return _rootDispatcher.SendMessage(new EntityClientToServerEnvelope(_channel, MetaSerialization.ToMetaSerialized<MetaMessage>(message, MetaSerializationFlags.SendOverNetwork, _logicVersion)));
            }
        }

        public void SetPeer(int channelId, int logicVersion, ISharedGameConfig config)
        {
            _channel = channelId;
            _logicVersion = logicVersion;
            _config = config;
        }

        public void ResetPeer(bool keepBufferedData = false)
        {
            RemoveAllListeners();

            _channel = -1;
            _logicVersion = -1;
            _config = null;
            _overrideSendMethod = null;

            if (!keepBufferedData)
            {
                _bufferChannel = -1;
                _buffer = null;
            }
        }

        /// <summary>
        /// Starts buffering incoming messages of a certain channel. Buffer can be dispatched with <see cref="FlushPeerBuffer"/>.
        /// </summary>
        public void StartPeerBuffering(int bufferedChannelId)
        {
            _bufferChannel = bufferedChannelId;
            _buffer = new List<MetaSerialized<MetaMessage>>();
        }

        /// <summary>
        /// Flushes buffered incoming messages. Messages are dispatched only if peer is set and the peer channel
        /// is the same as the most recently buffered channel id.
        /// </summary>
        public void FlushPeerBuffer()
        {
            // FLush to current if it the same channel
            if (_bufferChannel == _channel && _buffer != null)
            {
                foreach (MetaSerialized<MetaMessage> message in _buffer)
                    DispatchEnvelopeContents(message.Bytes);
            }

            _bufferChannel = -1;
            _buffer = null;
        }

        void OnEnvelope(EntityServerToClientEnvelope envelope)
        {
            if (envelope.ChannelId == _channel)
            {
                DispatchEnvelopeContents(envelope.Message.Bytes);
            }
            else if (envelope.ChannelId == _bufferChannel)
            {
                _buffer.Add(envelope.Message);
            }
        }

        internal void OverrideSendMethod(Func<MetaMessage, bool> sendMethod)
        {
            _overrideSendMethod = sendMethod;
        }

        internal void DispatchEnvelopeContents(ArraySegment<byte> contents)
        {
            // \note: entity-network-protocol is always serialized with SendOverNetwork
            using (IOReader reader = new IOReader(contents))
            {
                MetaMessage message = MetaSerialization.DeserializeTagged<MetaMessage>(reader, MetaSerializationFlags.SendOverNetwork, _config, _logicVersion);
                DispatchMessage(message);
            }
        }
    }
}
