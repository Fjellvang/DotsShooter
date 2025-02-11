// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Client;
using Metaplay.Core.Message;
using Metaplay.Core.Network;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Metaplay.Unity.DefaultIntegration
{
    public class DefaultMetaplayConnectionDelegate : IMetaplayClientConnectionDelegate
    {
        public ISessionStartHook        SessionStartHook    { protected get; set; }
        public ISessionContextProvider  SessionContext      { protected get; set; }

        public virtual void Init()
        {
        }

        public virtual void Update()
        {
        }

        public virtual void OnHandshakeComplete()
        {
        }

        public virtual Handshake.ILoginRequestGamePayload GetLoginPayload() => null;
        public virtual ISessionStartRequestGamePayload GetSessionStartRequestPayload() => null;

        public virtual void OnSessionStarted(SessionProtocol.SessionStartSuccess sessionStart, ClientSessionStartResources startResources)
        {
            SessionStartHook?.OnSessionStarted(sessionStart, startResources);
        }

        public virtual LoginDebugDiagnostics GetLoginDebugDiagnostics(bool isSessionResumption)
        {
            try
            {
                DateTime currentTime = DateTime.UtcNow;
                IPlayerClientContext playerContextMaybe = isSessionResumption ? SessionContext?.PlayerContext : null;

                TimeSpan? currentPauseDuration;
                TimeSpan? durationSincePauseEnd;
                if (MetaplaySDK.ApplicationPauseStatus == ApplicationPauseStatus.Pausing)
                {
                    currentPauseDuration = currentTime - MetaplaySDK.ApplicationLastPauseBeganAt;
                    durationSincePauseEnd = null;
                }
                else if (MetaplaySDK.ApplicationPauseStatus == ApplicationPauseStatus.Running
                      && MetaplaySDK.ApplicationLastPauseBeganAt != DateTime.UnixEpoch
                      && MetaplaySDK.ApplicationLastPauseDuration != TimeSpan.Zero)
                {
                    currentPauseDuration = null;
                    durationSincePauseEnd = currentTime - (MetaplaySDK.ApplicationLastPauseBeganAt + MetaplaySDK.ApplicationLastPauseDuration);
                }
                else
                {
                    currentPauseDuration = null;
                    durationSincePauseEnd = null;
                }

                return new LoginDebugDiagnostics
                {
                    Timestamp                           = MetaTime.FromDateTime(currentTime),
                    Session                             = MetaplaySDK.Connection.TryGetLoginSessionDebugDiagnostics(),
                    ServerConnection                    = MetaplaySDK.Connection.TryGetLoginServerConnectionDebugDiagnostics(),
                    Transport                           = MetaplaySDK.Connection.TryGetLoginTransportDebugDiagnostics(),
                    IncidentReport                      = MetaplaySDK.IncidentUploader?.GetLoginIncidentReportDebugDiagnostics(),
                    //MainLoop                            = StateManager.Instance?.GetDebugDiagnostics(), // \todo #helloworld
                    CurrentPauseDuration                = currentPauseDuration?.ToMetaDuration(),
                    DurationSincePauseEnd               = durationSincePauseEnd?.ToMetaDuration(),
                    DurationSinceConnectionUpdate       = (currentTime - MetaplaySDK.ApplicationPreviousEndOfTheFrameAt).ToMetaDuration(),
                    DurationSincePlayerContextUpdate    = (DateTime.UtcNow - playerContextMaybe?.LastUpdateTimeDebug)?.ToMetaDuration(), // \note Nullable (playerContextMaybe may be null).
                    ExpectSessionResumptionPing         = true,
                };
            }
            catch (Exception ex)
            {
                return new LoginDebugDiagnostics{ DiagnosticsError = ex.ToString() };
            }
        }

        public virtual void OnFullProtocolHashMismatch(uint clientProtocolHash, uint serverProtocolHash) { }

        public virtual void FlushPendingMessages()
        {
            // Flush player context
            SessionContext?.PlayerContext?.FlushActions();

            // Flush all clients in EntityClientStore
            SessionContext?.ClientStore?.FlushPendingMessages();
        }

        // The default implementation accepts passing the slot name via application command line argument
        // `--GuestCredentialsSlot`.
        public virtual string GetGuestCredentialsSlotName()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--GuestCredentialsSlot" && args.Length > i + 1)
                {
                    string slotName = args[i + 1];
                    Debug.Log($"Overriding guest credentials from command line to use slot {slotName}");
                    return slotName;
                }
            }

            return "";
        }

        public virtual ISessionCredentialService GetSessionCredentialService()
        {
            return new UnityCredentialService(GetGuestCredentialsSlotName() ?? "");
        }
    }
}
