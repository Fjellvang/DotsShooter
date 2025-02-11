// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

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

namespace Metaplay.Unity
{
    /// <summary>
    /// Default client services implementation for the Unity client.
    /// </summary>
    public class MetaplayUnitySubClientServices : IMetaplaySubClientServices
    {
        public IMessageDispatcher MessageDispatcher => MetaplaySDK.MessageDispatcher;
        public ITimelineHistory TimelineHistory => MetaplaySDK.TimelineHistory;
        public MetaplayClientStore ClientStore { get; private set; }
        public TransportQosMonitor TransportQosMonitor => MetaplaySDK.Connection.QosMonitor;

        public MetaplayUnitySubClientServices(MetaplayClientStore clientStore)
        {
            ClientStore = clientStore;
        }

        public void DefaultHandleConfigFetchFailed(Exception configLoadError)
        {
            MetaplaySDK.Connection.CloseWithError(flushEnqueuedMessages: true, new Metaplay.Unity.ConnectionStates.TransientError.ConfigFetchFailed(configLoadError, Metaplay.Unity.ConnectionStates.TransientError.ConfigFetchFailed.FailureSource.ResourceFetch));
        }

        public void DefaultHandleEntityTimelineUpdateFailed()
        {
            MetaplaySDK.Connection.CloseWithError(flushEnqueuedMessages: true, new Metaplay.Unity.ConnectionStates.TerminalError.Unknown());
        }

        public Task<ISharedGameConfig> GetConfigAsync(SessionProtocol.InitialPlayerState playerState)
        {
            MetaDictionary<PlayerExperimentId, ExperimentVariantId> assignment = ClientSessionStartResources.GetInitialStateExperimentAssignment(playerState);
            return MetaplaySDK.Connection.GetSpecializedGameConfigAsync(playerState.SharedConfigPatchesVersion, playerState.SharedConfigPatchesVersion, assignment);
        }

        public Task<ISharedGameConfig> GetConfigAsync(EntitySerializedState entityState)
        {
            MetaDictionary<PlayerExperimentId, ExperimentVariantId> assignment = ClientSessionStartResources.GetInitialStateExperimentAssignment(entityState);
            return MetaplaySDK.Connection.GetSpecializedGameConfigAsync(entityState.SharedGameConfigVersion, entityState.SharedConfigPatchesVersion, assignment);
        }

        public LogChannel CreateLogChannel(string name)
        {
            return MetaplaySDK.Logs.CreateChannel(name);
        }
    }
}
