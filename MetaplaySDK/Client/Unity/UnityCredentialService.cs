// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Network;
using Metaplay.Core.Tasks;
#if UNITY_STANDALONE && METAPLAY_HAS_STEAMWORKS
using Steamworks;
#endif
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Metaplay.Unity
{
#if UNITY_STANDALONE && METAPLAY_HAS_STEAMWORKS
    public class SteamCredentialService : ISessionCredentialService
    {
        TaskCompletionSource<byte[]> steamTcs;
        HAuthTicket                  authTicketForWebApi;

        public Task<EntityId> InitializeAsync()
        {
            return Task.FromResult(EntityId.None);
        }

        public async Task<ISessionCredentialService.LoginMethod> GetCurrentLoginMethodAsync()
        {
            if (!SteamAPI.IsSteamRunning())
                throw new InvalidOperationException("Steam is not running");

            if (authTicketForWebApi != HAuthTicket.Invalid)
                CancelSteamAuthTicket();

            var callback = Callback<GetTicketForWebApiResponse_t>.Create(
                t =>
                {
                    if (t.m_eResult == EResult.k_EResultOK)
                        steamTcs.SetResult(t.m_rgubTicket.Take(t.m_cubTicket).ToArray());
                    else
                        steamTcs.SetException(new InvalidOperationException("Steam authentication failed with: " + t.m_eResult));
                });

            steamTcs = new TaskCompletionSource<byte[]>();

            authTicketForWebApi = SteamUser.GetAuthTicketForWebApi(MetaplayCore.Options.ProjectName);

            Task delayTask = MetaTask.Delay(millisecondsDelay: 30_000);
            Task result    = await Task.WhenAny(steamTcs.Task, delayTask);

            callback.Dispose();

            if (result == steamTcs.Task)
                return new ISessionCredentialService.SocialAuthLoginMethod(new SocialAuthenticationClaimSteam(steamTcs.Task.GetCompletedResult()), EntityId.None, false);
            else
                throw new TimeoutException("Steam authentication took longer than expected, if this keeps happened, you might not be invoking `SteamAPI.RunCallbacks`.");
        }

        public void OnConnectionClosed()
        {
            CancelSteamAuthTicket();
        }

        public Task OnGuestAccountCreatedAsync(ISessionCredentialService.GuestCredentials guestCredentials)
        {
            return Task.CompletedTask;
        }

        public Task OnPlayerIdUpdatedAsync(AuthenticationPlatform platform, EntityId playerId)
        {
            // Latest PlayerId change always becomes the current
            MetaplaySDK.PlayerId = playerId;
            return Task.CompletedTask;
        }

        public void CancelSteamAuthTicket()
        {
            if (authTicketForWebApi != HAuthTicket.Invalid)
            {
                SteamUser.CancelAuthTicket(authTicketForWebApi);
                authTicketForWebApi = HAuthTicket.Invalid;
            }
        }
    }
    #endif

    public class UnityCredentialService : ISessionCredentialService
    {
        ISessionCredentialService.GuestCredentials? _guestCredentials;
        string                                      _guestSlot;

        public UnityCredentialService(string guestSlot)
        {
            _guestSlot = guestSlot;
        }

        public async Task<EntityId> InitializeAsync()
        {
            GuestCredentials guestCredentials = await CredentialsStore.TryGetGuestCredentialsAsync(_guestSlot);

            if (guestCredentials == null)
            {
                _guestCredentials = null;
                return EntityId.None;
            }

            _guestCredentials = new ISessionCredentialService.GuestCredentials(guestCredentials.DeviceId, guestCredentials.AuthToken, guestCredentials.PlayerId);
            return _guestCredentials.Value.PlayerIdHint;
        }

        public Task<ISessionCredentialService.LoginMethod> GetCurrentLoginMethodAsync()
        {
            // If there are no guest credentials, create new.
            #if !METAPLAY_DISALLOW_DEVICE_ID_LOGIN
            if (_guestCredentials == null)
                return Task.FromResult<ISessionCredentialService.LoginMethod>(new ISessionCredentialService.NewGuestAccountLoginMethod());

            // Otherwise, use the guest credentials
            return Task.FromResult<ISessionCredentialService.LoginMethod>(new ISessionCredentialService.GuestAccountLoginMethod(_guestCredentials.Value));
            #else
            throw new InvalidOperationException("Device Id authentication not available, please configure an ISessionCredentialService.");
            #endif
        }

        public void OnConnectionClosed() { }

        public async Task OnGuestAccountCreatedAsync(ISessionCredentialService.GuestCredentials guestCredentials)
        {
            _guestCredentials = guestCredentials;

            // The created account PlayerId becomes the current
            MetaplaySDK.PlayerId = guestCredentials.PlayerIdHint;

            GuestCredentials guestCredentialsToSave = new GuestCredentials()
            {
                DeviceId = guestCredentials.DeviceId,
                AuthToken = guestCredentials.AuthToken,
                PlayerId = guestCredentials.PlayerIdHint,
            };
            await CredentialsStore.StoreGuestCredentialsAsync(guestCredentialsToSave, _guestSlot);
        }

        public async Task OnPlayerIdUpdatedAsync(AuthenticationPlatform platform, EntityId playerId)
        {
            // Latest PlayerId change always becomes the current
            MetaplaySDK.PlayerId = playerId;

            // Sync playerId change to the persisted store
            if (platform == AuthenticationPlatform.DeviceId && _guestCredentials.HasValue)
            {
                GuestCredentials guestCredentialsToSave = new GuestCredentials()
                {
                    DeviceId = _guestCredentials.Value.DeviceId,
                    AuthToken = _guestCredentials.Value.AuthToken,
                    PlayerId = playerId,
                };
                await CredentialsStore.StoreGuestCredentialsAsync(guestCredentialsToSave, _guestSlot);
            }
        }

        // For compatibility
        public ISessionCredentialService.GuestCredentials? TryGetGuestCredentials() => _guestCredentials;
    }

    class OfflineCredentialService : ISessionCredentialService
    {
        public Task<ISessionCredentialService.LoginMethod> GetCurrentLoginMethodAsync()
        {
            #if !METAPLAY_DISALLOW_DEVICE_ID_LOGIN
            ISessionCredentialService.GuestCredentials offlineCredentials = new ISessionCredentialService.GuestCredentials("offlinedevice", "offlinetoken", DefaultOfflineServer.OfflinePlayerId);
            return Task.FromResult<ISessionCredentialService.LoginMethod>(new ISessionCredentialService.GuestAccountLoginMethod(offlineCredentials));
            #else
            throw new InvalidOperationException("Offline mode not available, please configure an ISessionCredentialService.");
            #endif
        }

        public Task OnLoginSuccess() => Task.CompletedTask;
        public Task OnLoginFailed(Exception ex) => Task.CompletedTask;
        public Task<EntityId> InitializeAsync() => Task.FromResult<EntityId>(DefaultOfflineServer.OfflinePlayerId);
        public Task OnGuestAccountCreatedAsync(ISessionCredentialService.GuestCredentials guestCredentials) => Task.CompletedTask;
        public Task OnPlayerIdUpdatedAsync(AuthenticationPlatform platform, EntityId playerId) => Task.CompletedTask;
        public void OnConnectionClosed() { }
    }
}
