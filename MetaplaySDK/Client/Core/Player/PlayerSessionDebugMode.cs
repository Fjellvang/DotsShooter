// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Client;
using Metaplay.Core.Debugging;
using Metaplay.Core.Model;
using Newtonsoft.Json;

namespace Metaplay.Core.Player
{
    /// <summary>
    /// Player session debug mode override behaviour. Gets updated on each session start and is responsible for
    /// providing the current <see cref="EntityDebugConfig"/> for the session when active.
    /// </summary>
    [MetaSerializable]
    [MetaReservedMembers(100, 150)]
    public abstract class PlayerSessionDebugMode
    {
        // \note Member needs to be non-private (at least getter) so JSON serialization sees it.
        [MetaMember(100)] protected PlayerSessionDebugModeParameters parameters;

        public PlayerSessionDebugMode(PlayerSessionDebugModeParameters parameters)
        {
            this.parameters = parameters;

            if (this.parameters == null)
            {
                // \note Migration from legacy data without `parameters`:
                //       Only EntityDebugConfig was supported, so create parameters accordingly.
                this.parameters = new PlayerSessionDebugModeParameters(enableEntityDebugConfig: true, PlayerDebugIncidentUploadMode.Normal);
            }
        }

        public abstract PlayerSessionDebugMode UpdateOnSessionStart();

        public EntityDebugConfig DebugConfigForCurrentSession => parameters.EnableEntityDebugConfig ? EntityDebugConfig.EnableAll() : null;
        public PlayerDebugIncidentUploadMode IncidentUploadMode => parameters.IncidentUploadMode;
    }

    [MetaSerializable]
    public class PlayerSessionDebugModeParameters
    {
        [JsonRequired] [MetaMember(1)] public bool EnableEntityDebugConfig;
        [JsonRequired] [MetaMember(2)] public PlayerDebugIncidentUploadMode IncidentUploadMode;

        [MetaDeserializationConstructor]
        public PlayerSessionDebugModeParameters(bool enableEntityDebugConfig, PlayerDebugIncidentUploadMode incidentUploadMode)
        {
            EnableEntityDebugConfig = enableEntityDebugConfig;
            IncidentUploadMode = incidentUploadMode;
        }
    }

    [MetaSerializableDerived(1)]
    public class PlayerSessionDebugModeCounter : PlayerSessionDebugMode
    {
        [MetaMember(1)] int forNextNumSessions;

        [MetaDeserializationConstructor]
        public PlayerSessionDebugModeCounter(PlayerSessionDebugModeParameters parameters, int forNextNumSessions)
            : base(parameters)
        {
            this.forNextNumSessions = forNextNumSessions;
        }

        public override PlayerSessionDebugMode UpdateOnSessionStart()
        {
            return forNextNumSessions-- <= 0 ? null : this;
        }
    }

    [MetaSerializable]
    public enum PlayerDebugIncidentUploadMode
    {
        /// <summary>
        /// Incident uploads behave normally and are not affected by this debug mode.
        /// </summary>
        Normal,
        /// <summary>
        /// Server won't request any incident uploads from the client, i.e. won't send a <see cref="PlayerRequestIncidentReportUploads"/> to the client.
        /// </summary>
        SilentlyOmitUploads,
        /// <summary>
        /// Server will send an empty <see cref="PlayerRequestIncidentReportUploads"/> when the client reports available incidents;
        /// this will cause the client to remove the proposed incidents.
        /// </summary>
        RejectIncidents,
    }
}
