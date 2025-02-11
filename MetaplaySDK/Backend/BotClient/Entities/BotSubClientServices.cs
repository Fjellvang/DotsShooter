// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud;
using Metaplay.Core;
using Metaplay.Core.Client;
using Metaplay.Core.Config;
using Metaplay.Core.Message;
using Metaplay.Core.Model;
using Metaplay.Core.MultiplayerEntity.Messages;
using Metaplay.Core.Network;
using Metaplay.Core.Player;
using System;
using System.Threading.Tasks;

namespace Metaplay.BotClient
{
    public sealed class BotSubClientServices : IMetaplaySubClientServices
    {
        public IMessageDispatcher MessageDispatcher { get; }
        public MetaplayClientStore ClientStore { get; }
        public ITimelineHistory TimelineHistory => null;
        public TransportQosMonitor TransportQosMonitor { get; }

        IMetaLogger _logger;

        public BotSubClientServices(IMessageDispatcher messageDispatcher, MetaplayClientStore clientStore, TransportQosMonitor transportQosMonitor, IMetaLogger logger)
        {
            MessageDispatcher = messageDispatcher;
            ClientStore = clientStore;
            TransportQosMonitor = transportQosMonitor;
            _logger = logger;
        }

        public LogChannel CreateLogChannel(string name)
        {
            return new LogChannel(name, _logger, MetaLogger.MetaLogLevelSwitch);
        }

        public void DefaultHandleConfigFetchFailed(Exception configLoadError)
        {
            throw configLoadError;
        }

        public void DefaultHandleEntityTimelineUpdateFailed()
        {
            throw new InvalidOperationException("Entity timeline update failed.");
        }

        public async Task<ISharedGameConfig> GetConfigAsync(SessionProtocol.InitialPlayerState playerState)
        {
            MetaDictionary<PlayerExperimentId, ExperimentVariantId> assignment = ClientSessionStartResources.GetInitialStateExperimentAssignment(playerState);
            return await BotGameConfigProvider.Instance.GetSpecializedGameConfigAsync(playerState.SharedConfigPatchesVersion, playerState.SharedConfigPatchesVersion, assignment);
        }

        public async Task<ISharedGameConfig> GetConfigAsync(EntitySerializedState entityState)
        {
            MetaDictionary<PlayerExperimentId, ExperimentVariantId> assignment = ClientSessionStartResources.GetInitialStateExperimentAssignment(entityState);
            return await BotGameConfigProvider.Instance.GetSpecializedGameConfigAsync(entityState.SharedGameConfigVersion, entityState.SharedConfigPatchesVersion, assignment);
        }
    }
}
