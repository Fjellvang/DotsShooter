// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Entity;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.InAppPurchase;
using Metaplay.Core.InGameMail;
using Metaplay.Core.Localization;
using Metaplay.Core.Message;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using Metaplay.Core.Serialization;
using Metaplay.Core.Session;
using Metaplay.Core.TypeCodes;
using Metaplay.Core.Activables;
using System;
using System.Collections.Generic;
using Metaplay.Core.Debugging;
using Metaplay.Server.Authentication;
using Metaplay.Core.MultiplayerEntity.Messages;
using Metaplay.Server.MultiplayerEntity.InternalMessages;

#if !METAPLAY_DISABLE_GUILDS
using Metaplay.Core.Guild;
using Metaplay.Core.Guild.Messages.Core;
using Metaplay.Server.GuildDiscovery;
using Metaplay.Server.Guild.InternalMessages;
#endif

namespace Metaplay.Server
{
    // A place for for game-agnostic server-internal PlayerActor-related messages.

    /// <summary>
    /// Game-agnostic session parameters for a player.
    /// </summary>
    [MetaSerializable(MetaSerializableFlags.ImplicitMembers)]
    [MetaImplicitMembersDefaultRangeForMostDerivedClass(1, 100)]
    [MetaImplicitMembersRange(100, 200)]
    public abstract class PlayerSessionParamsBase
    {
        public EntityId                                SessionId                    { get; private set; } // EntityId of the initiating session
        public SessionToken                            SessionToken                 { get; private set; } // Token for this specific session of the player
        public string                                  DeviceGuid                   { get; private set; } // Device GUID of the initiating device
        public int                                     LogicVersion                 { get; private set; } // LogicVersion to use for session
        public PlayerTimeZoneInfo                      TimeZoneInfo                 { get; private set; }
        public PlayerLocation?                         Location                     { get; private set; } // IP geolocation based location, if available.
        public string                                  ClientVersion                { get; private set; } // Game client version (Unity Application.version) of the initiating device
        public SessionProtocol.SessionResourceProposal SessionResourceProposal      { get; private set; }
        public CompressionAlgorithmSet                 SupportedArchiveCompressions { get; private set; }
        public AuthenticationKey                       AuthKey                      { get; private set; } // AuthenticationKey used for login
        public ISessionStartRequestGamePayload         SessionStartPayload          { get; private set; }
        public SessionProtocol.ClientDeviceInfo        DeviceInfo                   { get; private set; }

        protected PlayerSessionParamsBase() { }
        protected PlayerSessionParamsBase(
            EntityId sessionId,
            SessionToken sessionToken,
            string deviceGuid,
            SessionProtocol.ClientDeviceInfo deviceInfo,
            int logicVersion,
            PlayerTimeZoneInfo timeZoneInfo,
            PlayerLocation? location,
            string clientVersion,
            SessionProtocol.SessionResourceProposal sessionResourceProposal,
            CompressionAlgorithmSet supportedArchiveCompressions,
            AuthenticationKey authKey,
            ISessionStartRequestGamePayload sessionStartPayload)
        {
            if (!sessionId.IsOfKind(EntityKindCore.Session))
                throw new ArgumentException($"Invalid sessionId {sessionId}", nameof(sessionId));

            SessionId                    = sessionId;
            SessionToken                 = sessionToken;
            DeviceGuid                   = deviceGuid;
            DeviceInfo                   = deviceInfo;
            LogicVersion                 = logicVersion;
            TimeZoneInfo                 = timeZoneInfo;
            Location                     = location;
            ClientVersion                = clientVersion;
            SessionResourceProposal      = sessionResourceProposal;
            SupportedArchiveCompressions = supportedArchiveCompressions;
            AuthKey                      = authKey;
            SessionStartPayload          = sessionStartPayload;
        }
    }

    /// <summary>
    /// Default session parameters for a player.
    /// If custom parameters are needed, the game should define a custom subclass of <see cref="PlayerSessionParamsBase"/>.
    /// </summary>
    [MetaSerializableDerived(100)]
    public class DefaultPlayerSessionParams : PlayerSessionParamsBase
    {
        DefaultPlayerSessionParams() { }
        public DefaultPlayerSessionParams(
            EntityId sessionId,
            SessionToken sessionToken,
            string deviceGuid,
            SessionProtocol.ClientDeviceInfo deviceInfo,
            int logicVersion,
            PlayerTimeZoneInfo timeZoneInfo,
            PlayerLocation? location,
            string clientVersion,
            SessionProtocol.SessionResourceProposal sessionResourceProposal,
            CompressionAlgorithmSet supportedArchiveCompressions,
            AuthenticationKey authKey,
            ISessionStartRequestGamePayload sessionStartPayload)
            : base(sessionId, sessionToken, deviceGuid, deviceInfo, logicVersion, timeZoneInfo, location, clientVersion, sessionResourceProposal, supportedArchiveCompressions, authKey, sessionStartPayload)
        {
        }
    }

    /// <summary>
    /// A Subscribe request from session a new player session to be started with the provided session parameters.
    /// </summary>
    [MetaMessage(MessageCodesCore.InternalPlayerSessionSubscribeRequest, MessageDirection.ServerInternal)]
    public class InternalPlayerSessionSubscribeRequest : MetaMessage
    {
        public PlayerSessionParamsBase SessionParams { get; private set; }

        public InternalPlayerSessionSubscribeRequest() { }
        public InternalPlayerSessionSubscribeRequest(PlayerSessionParamsBase sessionParams)
        {
            SessionParams = sessionParams;
        }
    }

    [MetaMessage(MessageCodesCore.InternalPlayerSessionSubscribeResponse, MessageDirection.ServerInternal)]
    public class InternalPlayerSessionSubscribeResponse : MetaMessage
    {
        public SessionProtocol.InitialPlayerState                   PlayerState                 { get; private set; }
        public MetaDictionary<LanguageId, ContentHash>           LocalizationVersions        { get; private set; }
        public List<AssociatedEntityRefBase>                        AssociatedEntities          { get; private set; }
        public int                                                  GuildIncarnation            { get; private set; }
        public string                                               CorrectedDeviceGuid         { get; private set; }

        InternalPlayerSessionSubscribeResponse() { }
        public InternalPlayerSessionSubscribeResponse(
            SessionProtocol.InitialPlayerState playerState,
            MetaDictionary<LanguageId, ContentHash> localizationVersions,
            List<AssociatedEntityRefBase> associatedEntities,
            int guildIncarnation,
            string correctedDeviceGuid)
        {
            PlayerState = playerState;
            LocalizationVersions = localizationVersions;
            AssociatedEntities = associatedEntities;
            GuildIncarnation = guildIncarnation;
            CorrectedDeviceGuid = correctedDeviceGuid;
        }
    }

    [MetaSerializableDerived(MessageCodesCore.InternalPlayerSessionSubscribeRefused)]
    public class InternalPlayerSessionSubscribeRefused : EntityAskRefusal
    {
        [MetaSerializable]
        public enum ResultCode
        {
            TryAgain = 1,
            ResourceCorrectionRequired = 2,
            Banned = 4,
            LogicVersionDowngradeNotAllowed = 5,
            Deleted = 6,
        }
        [MetaMember(1)] public ResultCode                                           Result                      { get; private set; }
        [MetaMember(2)] public SessionProtocol.SessionResourceCorrection            ResourceCorrection          { get; private set; }
        [MetaMember(3)] public List<AssociatedEntityRefBase>                        AssociatedEntities          { get; private set; }
        [MetaMember(4)] public int                                                  GuildIncarnation            { get; private set; }

        public override string Message => $"Session subscribe refused with {Result}";

        InternalPlayerSessionSubscribeRefused() { }
        public InternalPlayerSessionSubscribeRefused(
            ResultCode result,
            SessionProtocol.SessionResourceCorrection resourceCorrection,
            List<AssociatedEntityRefBase> associatedEntities,
            int guildIncarnation)
        {
            Result = result;
            ResourceCorrection = resourceCorrection;
            AssociatedEntities = associatedEntities;
            GuildIncarnation = guildIncarnation;
        }

        public static InternalPlayerSessionSubscribeRefused CreateTryAgain()
        {
            return new InternalPlayerSessionSubscribeRefused(
                ResultCode.TryAgain,
                default,
                new List<AssociatedEntityRefBase>(),
                0
                );
        }

        public static InternalPlayerSessionSubscribeRefused CreateBanned()
        {
            return new InternalPlayerSessionSubscribeRefused(
                ResultCode.Banned,
                default,
                new List<AssociatedEntityRefBase>(),
                0
                );
        }

        public static InternalPlayerSessionSubscribeRefused CreateDeleted()
        {
            return new InternalPlayerSessionSubscribeRefused(
                ResultCode.Deleted,
                default,
                new List<AssociatedEntityRefBase>(),
                0
            );
        }

        public static InternalPlayerSessionSubscribeRefused CreateLogicVersionDowngradeNotAllowed()
        {
            return new InternalPlayerSessionSubscribeRefused(
                ResultCode.LogicVersionDowngradeNotAllowed,
                default,
                new List<AssociatedEntityRefBase>(),
                0
            );
        }
    }

    /// <summary>
    /// Inform the player that the client has requested assigning the product a pending dynamic content.
    /// Server will confirm it and persist state.
    /// </summary>
    [MetaMessage(MessageCodesCore.TriggerConfirmDynamicInAppPurchase, MessageDirection.ServerInternal)]
    public class TriggerConfirmDynamicPurchaseContent : MetaMessage
    {
        public InAppProductId ProductId { get; private set; }

        TriggerConfirmDynamicPurchaseContent(){ }
        public TriggerConfirmDynamicPurchaseContent(InAppProductId productId) { ProductId = productId; }
    }

    /// <summary>
    /// Inform the player that the client has requested assigning the product analytics context.
    /// Server will confirm it and persist state.
    /// </summary>
    [MetaMessage(MessageCodesCore.TriggerConfirmStaticPurchaseContext, MessageDirection.ServerInternal)]
    public class TriggerConfirmStaticPurchaseContext : MetaMessage
    {
        public InAppProductId ProductId { get; private set; }

        TriggerConfirmStaticPurchaseContext(){ }
        public TriggerConfirmStaticPurchaseContext(InAppProductId productId) { ProductId = productId; }
    }

    /// <summary>
    /// Inform the player that the client has reported the completion of an in-app purchase.
    /// Triggers the duplicate detection and full receipt validation.
    /// </summary>
    [MetaMessage(MessageCodesCore.TriggerInAppPurchaseValidation, MessageDirection.ServerInternal)]
    public class TriggerInAppPurchaseValidation : MetaMessage
    {
        public InAppPurchaseTransactionInfo TransactionInfo { get; private set; }
        public bool                         IsRetry         { get; private set; }

        public TriggerInAppPurchaseValidation() { }
        public TriggerInAppPurchaseValidation(InAppPurchaseTransactionInfo transactionInfo, bool isRetry)
        {
            TransactionInfo = transactionInfo;
            IsRetry = isRetry;
        }
    }

    [MetaMessage(MessageCodesCore.TriggerIAPSubscriptionReuseCheck, MessageDirection.ServerInternal)]
    public class TriggerIAPSubscriptionReuseCheck : MetaMessage
    {
        public string OriginalTransactionId { get; private set; }

        TriggerIAPSubscriptionReuseCheck() { }
        public TriggerIAPSubscriptionReuseCheck(string originalTransactionId)
        {
            OriginalTransactionId = originalTransactionId;
        }
    }

    /// <summary>
    /// Request execution of a <see cref="PlayerActionBase"/> as an unsynchronized server-issued action. Player replies
    /// to this <c>EntityAsk</c> with <see cref="InternalPlayerExecuteServerActionResponse"/>.
    /// </summary>
    [MetaMessage(MessageCodesCore.InternalPlayerExecuteServerActionRequest, MessageDirection.ServerInternal)]
    public class InternalPlayerExecuteServerActionRequest : EntityAskRequest<InternalPlayerExecuteServerActionResponse>
    {
        public MetaSerialized<PlayerActionBase> Action { get; private set; }

        InternalPlayerExecuteServerActionRequest() { }
        public InternalPlayerExecuteServerActionRequest(MetaSerialized<PlayerActionBase> action)
        {
            Action = action;
        }
    }
    [MetaMessage(MessageCodesCore.InternalPlayerExecuteServerActionResponse, MessageDirection.ServerInternal)]
    public class InternalPlayerExecuteServerActionResponse : EntityAskResponse
    {
        public static readonly InternalPlayerExecuteServerActionResponse Instance = new InternalPlayerExecuteServerActionResponse();
    }

    /// <summary>
    /// Request execution of a <see cref="PlayerActionBase"/> as a synchronized (server-issued, server-enqueued but client-scheduled) action. Player
    /// responds to this <c>EntityAsk</c> with <see cref="InternalPlayerEnqueueServerActionResponse"/>
    /// </summary>
    [MetaMessage(MessageCodesCore.InternalPlayerEnqueueServerActionRequest, MessageDirection.ServerInternal)]
    public class InternalPlayerEnqueueServerActionRequest : EntityAskRequest<InternalPlayerEnqueueServerActionResponse>
    {
        public MetaSerialized<PlayerActionBase> Action { get; private set; }

        InternalPlayerEnqueueServerActionRequest() { }
        public InternalPlayerEnqueueServerActionRequest(MetaSerialized<PlayerActionBase> action)
        {
            Action = action;
        }
    }
    [MetaMessage(MessageCodesCore.InternalPlayerEnqueueServerActionResponse, MessageDirection.ServerInternal)]
    public class InternalPlayerEnqueueServerActionResponse : EntityAskResponse
    {
        public static readonly InternalPlayerEnqueueServerActionResponse Instance = new InternalPlayerEnqueueServerActionResponse();
    }

    [MetaMessage(MessageCodesCore.PlayerResetState, MessageDirection.ServerInternal)]
    public class PlayerResetState : MetaMessage
    {
        public static readonly PlayerResetState Instance = new PlayerResetState();
    }

    /// <summary>
    /// Request that the player changes their name
    /// </summary>
    [MetaMessage(MessageCodesCore.PlayerChangeNameRequest, MessageDirection.ServerInternal)]
    public class PlayerChangeNameRequest : EntityAskRequest<PlayerChangeNameResponse>
    {
        public string NewName { get; private set; }
        public bool ValidateOnly { get; private set; }

        public PlayerChangeNameRequest() { }
        public PlayerChangeNameRequest(string newName, bool validateOnly)
        {
            NewName = newName;
            ValidateOnly = validateOnly;
        }
    }
    [MetaMessage(MessageCodesCore.PlayerChangeNameResponse, MessageDirection.ServerInternal)]
    public class PlayerChangeNameResponse : EntityAskResponse
    {
        public bool NameWasValid { get; private set; }

        public PlayerChangeNameResponse() { }
        public PlayerChangeNameResponse(bool nameWasValid) { NameWasValid = nameWasValid; }
    }

    /// <summary>
    /// Request that the player updates their logic version
    /// </summary>
    [MetaMessage(MessageCodesCore.InternalPlayerUpdateLogicVersionRequest, MessageDirection.ServerInternal)]
    public class PlayerUpdateLogicVersionRequest : MetaMessage
    {
        public int NewLogicVersion { get; private set; }

        [MetaDeserializationConstructor]
        public PlayerUpdateLogicVersionRequest(int newLogicVersion)
        {
            NewLogicVersion = newLogicVersion;
        }
    }

    /// <summary>
    /// Request that the player updates their logic version
    /// </summary>
    [MetaMessage(MessageCodesCore.InternalPlayerUpdateLogicVersionResponse, MessageDirection.ServerInternal)]
    public class PlayerUpdateLogicVersionResponse : MetaMessage
    {
        public int OldLogicVersion { get; private set; }
        public int NewLogicVersion { get; private set; }

        [MetaDeserializationConstructor]
        public PlayerUpdateLogicVersionResponse(int oldLogicVersion, int newLogicVersion)
        {
            OldLogicVersion = oldLogicVersion;
            NewLogicVersion = newLogicVersion;
        }
    }

    /// <summary>
    /// Request that the player imports model data
    /// </summary>
    [MetaMessage(MessageCodesCore.PlayerImportModelDataRequest, MessageDirection.ServerInternal)]
    public class PlayerImportModelDataRequest : EntityAskRequest<PlayerImportModelDataResponse>
    {
        public byte[] Payload { get; private set; }
        public int? SchemaVersion { get; private set; }

        /// <summary>
        /// If true, the receiver entity was just created. If false, we are overwriting an
        /// existing entity.
        /// </summary>
        public bool IsNewEntity { get; private set; }

        PlayerImportModelDataRequest() { }
        public PlayerImportModelDataRequest(byte[] payload, int? schemaVersion, bool isNewEntity)
        {
            Payload = payload;
            SchemaVersion = schemaVersion;
            IsNewEntity = isNewEntity;
        }
    }

    [MetaMessage(MessageCodesCore.PlayerImportModelDataResponse, MessageDirection.ServerInternal)]
    public class PlayerImportModelDataResponse : EntityAskResponse
    {
        public bool Success { get; private set; }
        public string FailureInfo { get; private set; }

        public PlayerImportModelDataResponse() { }
        public PlayerImportModelDataResponse(bool success, string failureInfo) { Success = success; FailureInfo = failureInfo; }
    }

    [MetaSerializable]
    public enum PlayerForceKickOwnerReason
    {
        ReceivedAnotherOwnerSubscriber,
        AdminAction,
        ClientTimeTooFarBehind,             // Client-driven PlayerModel.CurrentTime has fallen too far behind of server wall clock
        ClientTimeTooFarAhead,              // Client-driven PlayerModel.CurrentTime has gone too far ahead of server wall clock
        InternalError,
        PlayerBanned,
        GameConfigUpdated,
        LogicVersionUpdated,
    }

    [MetaMessage(MessageCodesCore.PlayerForceKickOwner, MessageDirection.ServerInternal)]
    public class PlayerForceKickOwner : MetaMessage
    {
        public PlayerForceKickOwnerReason Reason { get; private set; }

        public PlayerForceKickOwner(){ }
        public PlayerForceKickOwner(PlayerForceKickOwnerReason reason) { Reason = reason; }
    }

    /// <summary>
    /// Request that the player or guild immediately persists their state to the database
    /// </summary>
    // \todo: Move to PersistedEntity?
    [MetaMessage(MessageCodesCore.PersistStateRequestRequest, MessageDirection.ServerInternal)]
    public class PersistStateRequestRequest : EntityAskRequest<PersistStateRequestResponse>
    {
        public static readonly PersistStateRequestRequest Instance = new PersistStateRequestRequest();
    }
    [MetaMessage(MessageCodesCore.PersistStateRequestResponse, MessageDirection.ServerInternal)]
    public class PersistStateRequestResponse : EntityAskResponse
    {
        public static readonly PersistStateRequestResponse Instance = new PersistStateRequestResponse();
    }

    /// <summary>
    /// Request that the player marks a purchase as refunded, and optionally revokes
    /// the resources obtained in the purchase.
    /// </summary>
    [MetaMessage(MessageCodesCore.PlayerRefundPurchaseRequest, MessageDirection.ServerInternal)]
    public class PlayerRefundPurchaseRequest : EntityAskRequest<PlayerRefundPurchaseResponse>
    {
        public string TransactionId { get; private set; }

        public PlayerRefundPurchaseRequest() { }
        public PlayerRefundPurchaseRequest(string transactionId)
        {
            TransactionId = transactionId;
        }
    }
    [MetaMessage(MessageCodesCore.PlayerRefundPurchaseResponse, MessageDirection.ServerInternal)]
    public class PlayerRefundPurchaseResponse : EntityAskResponse
    {
        public static readonly PlayerRefundPurchaseResponse Instance = new PlayerRefundPurchaseResponse();
        public PlayerRefundPurchaseResponse() { }
    }

    [MetaMessage(MessageCodesCore.InternalPlayerEvaluateTriggers, MessageDirection.ServerInternal)]
    public class InternalPlayerEvaluateTriggers : MetaMessage
    {
        public PlayerEventBase Event;

        public InternalPlayerEvaluateTriggers() { }

        public InternalPlayerEvaluateTriggers(PlayerEventBase ev)
        {
            Event = ev;
        }
    }

    [MetaMessage(MessageCodesCore.PlayerRemovePushNotificationTokenRequest, MessageDirection.ServerInternal)]
    public class PlayerRemovePushNotificationTokenRequest : EntityAskRequest<PlayerRemovePushNotificationTokenResponse>
    {
        public string FirebaseMessagingToken;

        public PlayerRemovePushNotificationTokenRequest(){ }
        public PlayerRemovePushNotificationTokenRequest(string firebaseMessagingToken)
        {
            FirebaseMessagingToken = firebaseMessagingToken ?? throw new ArgumentNullException(nameof(firebaseMessagingToken));
        }
    }

    [MetaMessage(MessageCodesCore.PlayerRemovePushNotificationTokenResponse, MessageDirection.ServerInternal)]
    public class PlayerRemovePushNotificationTokenResponse : EntityAskResponse
    {
        public PlayerRemovePushNotificationTokenResponse(){ }
    }

    /// <summary>
    /// Request that player schedules deletion. Called by admin operations.
    /// </summary>
    [MetaMessage(MessageCodesCore.InternalPlayerScheduleDeletionRequest, MessageDirection.ServerInternal)]
    public class InternalPlayerScheduleDeletionRequest : EntityAskRequest<InternalPlayerScheduleDeletionResponse>
    {
        public MetaTime?            ScheduledAt { get; init; } // When this is null it means that scheduled deletion was cancelled
        public PlayerDeletionSource Source      { get; init; }
        public string               SourceInfo  { get; init; } // Additional information about the deletion source

        [MetaDeserializationConstructor]
        public InternalPlayerScheduleDeletionRequest(MetaTime? scheduledAt, PlayerDeletionSource source, string sourceInfo = null)
        {
            ScheduledAt = scheduledAt;
            Source      = source;
            SourceInfo  = sourceInfo;
        }
    }
    [MetaMessage(MessageCodesCore.InternalPlayerScheduleDeletionResponse, MessageDirection.ServerInternal)]
    public class InternalPlayerScheduleDeletionResponse : EntityAskResponse
    {
        public static readonly InternalPlayerScheduleDeletionResponse Instance = new InternalPlayerScheduleDeletionResponse();
    }

    /// <summary>
    /// Request that the player deletes themself
    /// </summary>
    [MetaMessage(MessageCodesCore.PlayerCompleteScheduledDeletionRequest, MessageDirection.ServerInternal)]
    public class PlayerCompleteScheduledDeletionRequest : EntityAskRequest<PlayerCompleteScheduledDeletionResponse>
    {
        public static readonly PlayerCompleteScheduledDeletionRequest Instance = new PlayerCompleteScheduledDeletionRequest();
    }
    [MetaMessage(MessageCodesCore.PlayerCompleteScheduledDeletionResponse, MessageDirection.ServerInternal)]
    public class PlayerCompleteScheduledDeletionResponse : EntityAskResponse
    {
        public bool Success { get; private set; }
        PlayerCompleteScheduledDeletionResponse() { }
        public PlayerCompleteScheduledDeletionResponse(bool success) { Success = success; }
    }

    #if !METAPLAY_DISABLE_GUILDS

    [MetaMessage(MessageCodesCore.InternalGuildDiscoveryPlayerContextRequest, MessageDirection.ServerInternal)]
    public class InternalGuildDiscoveryPlayerContextRequest : EntityAskRequest<InternalGuildDiscoveryPlayerContextResponse>
    {
        public static readonly InternalGuildDiscoveryPlayerContextRequest Instance = new InternalGuildDiscoveryPlayerContextRequest();
        public InternalGuildDiscoveryPlayerContextRequest() { }
    }

    [MetaMessage(MessageCodesCore.InternalGuildDiscoveryPlayerContextResponse, MessageDirection.ServerInternal)]
    public class InternalGuildDiscoveryPlayerContextResponse : EntityAskResponse
    {
        public GuildDiscoveryPlayerContextBase Result;

        public InternalGuildDiscoveryPlayerContextResponse() { }
        public InternalGuildDiscoveryPlayerContextResponse(GuildDiscoveryPlayerContextBase context) { Result = context; }
    }

    /// <summary>
    /// Notification from player to session that the player has just created (and joined) a guild.
    /// </summary>
    [MetaMessage(MessageCodesCore.InternalSessionPlayerJoinedAGuild, MessageDirection.ServerInternal)]
    public class InternalSessionPlayerJoinedAGuild : MetaMessage
    {
        public InternalOwnedGuildAssociationRef     AssociationRef      { get; private set; }
        public bool                                 CreatedTheGuild     { get; private set; }
        public int                                  GuildIncarnation    { get; private set; }

        public InternalSessionPlayerJoinedAGuild() { }
        public InternalSessionPlayerJoinedAGuild(InternalOwnedGuildAssociationRef associationRef, bool createdTheGuild, int guildIncarnation)
        {
            AssociationRef = associationRef;
            CreatedTheGuild = createdTheGuild;
            GuildIncarnation = guildIncarnation;
        }
    }

    /// <summary>
    /// Notification from player to session that the player has just been kicked from the guild.
    /// </summary>
    [MetaMessage(MessageCodesCore.InternalSessionPlayerKickedFromGuild, MessageDirection.ServerInternal)]
    public class InternalSessionPlayerKickedFromGuild : MetaMessage
    {
        public int GuildIncarnation { get; private set; }

        public InternalSessionPlayerKickedFromGuild() { }
        public InternalSessionPlayerKickedFromGuild(int guildIncarnation)
        {
            GuildIncarnation = guildIncarnation;
        }
    }

    [MetaMessage(MessageCodesCore.InternalPlayerJoinGuildRequest, MessageDirection.ServerInternal)]
    public class InternalPlayerJoinGuildRequest : EntityAskRequest<InternalPlayerJoinGuildResponse>
    {
        public GuildJoinRequest OriginalRequest     { get; private set; }
        public int              GuildIncarnation    { get; private set; }

        public InternalPlayerJoinGuildRequest() { }
        public InternalPlayerJoinGuildRequest(GuildJoinRequest originalRequest, int guildIncarnation)
        {
            OriginalRequest = originalRequest;
            GuildIncarnation = guildIncarnation;
        }
    }
    [MetaMessage(MessageCodesCore.InternalPlayerJoinGuildResponse, MessageDirection.ServerInternal)]
    public class InternalPlayerJoinGuildResponse : EntityAskResponse
    {
        public bool                                                 IsSuccess               { get; private set; }
        public InternalOwnedGuildAssociationRef                     AssociationRef          { get; private set; }
        public int                                                  GuildIncarnation        { get; private set; }

        InternalPlayerJoinGuildResponse() { }
        public InternalPlayerJoinGuildResponse(bool isSuccess, InternalOwnedGuildAssociationRef associationRef, int guildIncarnation)
        {
            IsSuccess = isSuccess;
            AssociationRef = associationRef;
            GuildIncarnation = guildIncarnation;
        }

        public static InternalPlayerJoinGuildResponse CreateOk(InternalOwnedGuildAssociationRef associationRef, int guildIncarnation) => new InternalPlayerJoinGuildResponse(true, associationRef, guildIncarnation);
        public static InternalPlayerJoinGuildResponse CreateRefused() => new InternalPlayerJoinGuildResponse(false, null, 0);
    }

    [MetaMessage(MessageCodesCore.InternalPlayerGuildLeaveRequest, MessageDirection.ServerInternal)]
    public class InternalPlayerGuildLeaveRequest : EntityAskRequest<InternalPlayerGuildLeaveResponse>
    {
        public EntityId GuildId             { get; private set; }
        public int      GuildIncarnation    { get; private set; }

        public InternalPlayerGuildLeaveRequest() { }
        public InternalPlayerGuildLeaveRequest(EntityId guildId, int guildIncarnation)
        {
            GuildId = guildId;
            GuildIncarnation = guildIncarnation;
        }
    }
    [MetaMessage(MessageCodesCore.InternalPlayerGuildLeaveResponse, MessageDirection.ServerInternal)]
    public class InternalPlayerGuildLeaveResponse : EntityAskResponse
    {
        public bool     IsStaleRequest                  { get; private set; }
        public int      GuildIncarnation                { get; private set; }
        public bool     SessionDesynchronized           { get; private set; }

        public InternalPlayerGuildLeaveResponse() { }
        InternalPlayerGuildLeaveResponse(bool isStaleRequest, int guildIncarnation, bool sessionDesynchronized)
        {
            IsStaleRequest = isStaleRequest;
            GuildIncarnation = guildIncarnation;
            SessionDesynchronized = sessionDesynchronized;
        }

        public static InternalPlayerGuildLeaveResponse CreateOk(int guildIncarnation, bool sessionDesynchronized) => new InternalPlayerGuildLeaveResponse(false, guildIncarnation, sessionDesynchronized);
        public static InternalPlayerGuildLeaveResponse CreateStaleRequest() => new InternalPlayerGuildLeaveResponse(true, -1, false);
    }

    [MetaMessage(MessageCodesCore.InternalPlayerPendingGuildOpsCommitted, MessageDirection.ServerInternal)]
    public class InternalPlayerPendingGuildOpsCommitted : MetaMessage
    {
        public EntityId GuildId                 { get; private set; }
        public int      MemberInstanceId        { get; private set; }
        /// <summary>
        /// Inclusive upper bound. Guild has committed all ops BEFORE and ON this epoch.
        /// </summary>
        public int CommittedGuildOpEpoch { get; private set; }

        public InternalPlayerPendingGuildOpsCommitted() { }
        public InternalPlayerPendingGuildOpsCommitted(EntityId guildId, int memberInstanceId, int committedGuildOpEpoch)
        {
            GuildId = guildId;
            MemberInstanceId = memberInstanceId;
            CommittedGuildOpEpoch = committedGuildOpEpoch;
        }
    }

    /// <summary>
    /// Sent to originating player any time guild receives message from a non-member player, or
    /// guild has reason to believe player has inconsistent state with it. This message is only a
    /// hint, and player will do a proper checks upon receipt.
    /// </summary>
    [MetaMessage(MessageCodesCore.InternalPlayerKickedFromGuild, MessageDirection.ServerInternal)]
    public class InternalPlayerKickedFromGuild : MetaMessage
    {
        public EntityId GuildId                 { get; private set; }
        public int      MemberInstanceId        { get; private set; }

        public InternalPlayerKickedFromGuild() { }
        public InternalPlayerKickedFromGuild(EntityId guildId, int memberInstanceId)
        {
            GuildId = guildId;
            MemberInstanceId = memberInstanceId;
        }
    }

    [MetaMessage(MessageCodesCore.InternalSessionPlayerGuildCreateFailed, MessageDirection.ServerInternal)]
    class InternalSessionPlayerGuildCreateFailed : MetaMessage
    {
        public int GuildIncarnation { get; private set; }

        public InternalSessionPlayerGuildCreateFailed() { }
        public InternalSessionPlayerGuildCreateFailed(int guildIncarnation)
        {
            GuildIncarnation = guildIncarnation;
        }
    }

    [MetaMessage(MessageCodesCore.InternalPlayerGetGuildInviterAvatarRequest, MessageDirection.ServerInternal)]
    public class InternalPlayerGetGuildInviterAvatarRequest : EntityAskRequest<InternalPlayerGetGuildInviterAvatarResponse>
    {
        public static readonly InternalPlayerGetGuildInviterAvatarRequest Instance = new InternalPlayerGetGuildInviterAvatarRequest();
        InternalPlayerGetGuildInviterAvatarRequest() { }
    }
    [MetaMessage(MessageCodesCore.InternalPlayerGetGuildInviterAvatarResponse, MessageDirection.ServerInternal)]
    public class InternalPlayerGetGuildInviterAvatarResponse : EntityAskResponse
    {
        public MetaSerialized<GuildInviterAvatarBase> InviterAvatar { get; private set; }

        InternalPlayerGetGuildInviterAvatarResponse() { }
        public InternalPlayerGetGuildInviterAvatarResponse(MetaSerialized<GuildInviterAvatarBase> inviterAvatar)
        {
            InviterAvatar = inviterAvatar;
        }
    }
    #endif

    [MetaMessage(MessageCodesCore.InternalPlayerSetExperimentGroupRequest, MessageDirection.ServerInternal)]
    public class InternalPlayerSetExperimentGroupRequest : EntityAskRequest<InternalPlayerSetExperimentGroupResponse>
    {
        public PlayerExperimentId   PlayerExperimentId  { get; private set; }
        public ExperimentVariantId  VariantId           { get; private set; }
        public uint                 TesterEpoch         { get; private set; }

        InternalPlayerSetExperimentGroupRequest() { }
        public InternalPlayerSetExperimentGroupRequest(PlayerExperimentId playerExperimentId, ExperimentVariantId variantId, uint testerEpoch)
        {
            PlayerExperimentId = playerExperimentId;
            VariantId = variantId;
            TesterEpoch = testerEpoch;
        }
    }
    [MetaMessage(MessageCodesCore.InternalPlayerSetExperimentGroupResponse, MessageDirection.ServerInternal)]
    public class InternalPlayerSetExperimentGroupResponse : EntityAskResponse
    {
        public bool                 IsWaitingForTesterEpochUpdate   { get; private set; }

        InternalPlayerSetExperimentGroupResponse() { }
        public InternalPlayerSetExperimentGroupResponse(bool isWaitingForTesterEpochUpdate)
        {
            IsWaitingForTesterEpochUpdate = isWaitingForTesterEpochUpdate;
        }
    }

    [MetaMessage(MessageCodesCore.InternalPlayerSetExperimentGroupWaitRequest, MessageDirection.ServerInternal)]
    public class InternalPlayerSetExperimentGroupWaitRequest : EntityAskRequest<InternalPlayerSetExperimentGroupWaitResponse>
    {
        public PlayerExperimentId   PlayerExperimentId  { get; private set; }
        public uint                 TesterEpoch         { get; private set; }

        InternalPlayerSetExperimentGroupWaitRequest() { }
        public InternalPlayerSetExperimentGroupWaitRequest(PlayerExperimentId playerExperimentId, uint testerEpoch)
        {
            PlayerExperimentId = playerExperimentId;
            TesterEpoch = testerEpoch;
        }
    }
    [MetaMessage(MessageCodesCore.InternalPlayerSetExperimentGroupWaitResponse, MessageDirection.ServerInternal)]
    public class InternalPlayerSetExperimentGroupWaitResponse : EntityAskResponse
    {
        public bool                 IsWaitingForTesterEpochUpdate   { get; private set; }

        InternalPlayerSetExperimentGroupWaitResponse() { }
        public InternalPlayerSetExperimentGroupWaitResponse(bool isWaitingForTesterEpochUpdate)
        {
            IsWaitingForTesterEpochUpdate = isWaitingForTesterEpochUpdate;
        }
    }

    [MetaSerializable(MetaSerializableFlags.ImplicitMembers)]
    [MetaImplicitMembersRange(1,100)]
    public struct PlayerExperimentDetails
    {
        [MetaSerializable]
        public enum NotActiveReason
        {
            PlayerIsNotEnrolled = 0,
            ExperimentIsMissing,
            ExperimentIsInvalid,
            VariantIsUnknown,
            VariantIsDisabled,
            ExperimentIsInTestingPhaseForNonTester,
            ExperimentIsPausedForNonTester,
            ExperimentIsConcluded,
        }

        [MetaSerializable]
        public enum NotEligibleReason
        {
            ExperimentIsMissing = 0,
            ExperimentIsInvalid,
            ExperimentIsConcluded,
            NotInTargetSegments,
            NotInRolloutRatio,
            CapacityReached,
            ForTestersOnly,
            NewPlayersOnly,
            RolloutDisabled,
        }

        /// <summary>
        /// Does the experiment state exist (or has it been deleted).
        /// </summary>
        public bool                     ExperimentExists;

        /// <summary>
        /// Phase of the experiment if it exists.
        /// </summary>
        public PlayerExperimentPhase    ExperimentPhase;

        /// <summary>
        /// Is player a Tester in this experiment.
        /// </summary>
        public bool                     IsPlayerTester;

        /// <summary>
        /// Has player been enrolled into this experiment, i.e. is this player a member of this experiment.
        /// </summary>
        public bool                     IsPlayerEnrolled;

        /// <summary>
        /// If player is enrolled, the variant into which it was enrolled or null for control variant.
        /// </summary>
        public ExperimentVariantId      EnrolledVariant;

        /// <summary>
        /// The reason why experiment has no effect on player.
        /// </summary>
        public NotActiveReason?         WhyNotActive;

        /// <summary>
        /// The reason why player is not eligible
        /// </summary>
        public NotEligibleReason?       WhyNotEligible;

        public PlayerExperimentDetails(bool experimentExists, PlayerExperimentPhase experimentPhase, bool isPlayerTester, bool isPlayerEnrolled, ExperimentVariantId enrolledVariant, NotActiveReason? whyNotActive, NotEligibleReason? whyNotEligible)
        {
            ExperimentExists = experimentExists;
            ExperimentPhase = experimentPhase;
            IsPlayerTester = isPlayerTester;
            IsPlayerEnrolled = isPlayerEnrolled;
            EnrolledVariant = enrolledVariant;
            WhyNotActive = whyNotActive;
            WhyNotEligible = whyNotEligible;
        }
    }
    [MetaMessage(MessageCodesCore.InternalPlayerGetPlayerExperimentDetailsRequest, MessageDirection.ServerInternal)]
    public class InternalPlayerGetPlayerExperimentDetailsRequest : EntityAskRequest<InternalPlayerGetPlayerExperimentDetailsResponse>
    {
        public static readonly InternalPlayerGetPlayerExperimentDetailsRequest Instance = new InternalPlayerGetPlayerExperimentDetailsRequest();
    }
    [MetaMessage(MessageCodesCore.InternalPlayerGetPlayerExperimentDetailsResponse, MessageDirection.ServerInternal)]
    public class InternalPlayerGetPlayerExperimentDetailsResponse : EntityAskResponse
    {
        public MetaDictionary<PlayerExperimentId, PlayerExperimentDetails> Details { get; private set; }

        InternalPlayerGetPlayerExperimentDetailsResponse() { }
        public InternalPlayerGetPlayerExperimentDetailsResponse(MetaDictionary<PlayerExperimentId, PlayerExperimentDetails> details)
        {
            Details = details;
        }
    }

    [MetaMessage(MessageCodesCore.TriggerPlayerBanFlagChanged, MessageDirection.ServerInternal)]
    public class TriggerPlayerBanFlagChanged : MetaMessage
    {
        public static readonly TriggerPlayerBanFlagChanged Instance = new TriggerPlayerBanFlagChanged();
    }

    [MetaMessage(MessageCodesCore.InternalPlayerForceActivablePhaseMessage, MessageDirection.ServerInternal)]
    public class InternalPlayerForceActivablePhaseMessage : MetaMessage
    {
        public MetaActivableKindId KindId { get; private set; }
        public string ActivableIdStr { get; private set; } // \note #activable-id-type
        public MetaActivableState.DebugPhase? Phase { get; private set; }

        InternalPlayerForceActivablePhaseMessage(){ }
        public InternalPlayerForceActivablePhaseMessage(MetaActivableKindId kindId, string activableIdStr, MetaActivableState.DebugPhase? phase)
        {
            KindId = kindId;
            ActivableIdStr = activableIdStr;
            Phase = phase;
        }
    }

    [MetaMessage(MessageCodesCore.InternalPlayerDeveloperStatusChangedMessage, MessageDirection.ServerInternal)]
    public class InternalPlayerDeveloperStatusChanged : MetaMessage
    {
        public bool IsDeveloper { get; private set; }

        InternalPlayerDeveloperStatusChanged(){ }
        public InternalPlayerDeveloperStatusChanged(bool isDeveloper)
        {
            IsDeveloper = isDeveloper;
        }
    }

    /// <summary>
    /// Tell player to execute a <see cref="PlayerActionBase"/> as an unsynchronized server-issued action. Player does not reply to this
    /// Message.
    /// <para>
    /// <b>Warning: </b> This is a fire-and-forget message and there will be no feedback whether the action got run or not. For most cases,
    /// you should use and await for <see cref="InternalPlayerExecuteServerActionRequest"/> EntityAsk. This fire-and-forget message is only
    /// suitable in places where loss of messages in error cases is acceptable, or if synchronous asks are not suitable due to risk of deadlocking.
    /// </para>
    /// </summary>
    [MetaMessage(MessageCodesCore.InternalPlayerExecuteServerActionMessage, MessageDirection.ServerInternal)]
    public class InternalPlayerExecuteServerActionMessage : MetaMessage
    {
        public MetaSerialized<PlayerActionBase> Action { get; private set; }

        InternalPlayerExecuteServerActionMessage() { }
        public InternalPlayerExecuteServerActionMessage(MetaSerialized<PlayerActionBase> action)
        {
            Action = action;
        }
    }

    /// <summary>
    /// Tell player to execute a <see cref="PlayerActionBase"/> as a synchronized (server-issued, server-enqueued but client-scheduled) action.
    /// Player does not reply to this Message.
    /// <para>
    /// <b>Warning: </b> This is a fire-and-forget message and there will be no feedback whether the action got run or not. For most cases,
    /// you should use and await for <see cref="InternalPlayerEnqueueServerActionRequest"/> EntityAsk. This fire-and-forget message is only
    /// suitable in places where loss of messages in error cases is acceptable, or if synchronous asks are not suitable due to risk of deadlocking.
    /// </para>
    /// </summary>
    [MetaMessage(MessageCodesCore.InternalPlayerEnqueueServerActionMessage, MessageDirection.ServerInternal)]
    public class InternalPlayerEnqueueServerActionMessage : MetaMessage
    {
        public MetaSerialized<PlayerActionBase> Action { get; private set; }

        InternalPlayerEnqueueServerActionMessage() { }
        public InternalPlayerEnqueueServerActionMessage(MetaSerialized<PlayerActionBase> action)
        {
            Action = action;
        }
    }

    [MetaMessage(MessageCodesCore.InternalPlayerToggleSessionDebugMode, MessageDirection.ServerInternal)]
    public class InternalPlayerToggleSessionDebugMode : EntityAskRequest<EntityAskOk>
    {
        public bool Enabled { get; }

        public int ForNextNumSessions { get; }
        public PlayerSessionDebugModeParameters Parameters { get; }

        [MetaDeserializationConstructor]
        public InternalPlayerToggleSessionDebugMode(bool enabled, int forNextNumSessions, PlayerSessionDebugModeParameters parameters)
        {
            Enabled = enabled;
            ForNextNumSessions = forNextNumSessions;
            Parameters = parameters;
        }
    }

    /// <summary>
    /// Add an incident report to a player. This is for server-generated incidents.
    /// </summary>
    [MetaMessage(MessageCodesCore.InternalPlayerAddIncidentReport, MessageDirection.ServerInternal)]
    public class InternalPlayerAddIncidentReport : MetaMessage
    {
        public byte[]   Payload     { get; private set; }   // Deflate-compressed, TaggedSerialized<PlayerIncidentReport>

        [MetaDeserializationConstructor]
        public InternalPlayerAddIncidentReport(byte[] payload)
        {
            Payload = payload;
        }
    }

    /// <summary>
    /// Add an incident report to a player from an S3 bucket. This is for S3-uploaded incidents.
    /// </summary>
    [MetaMessage(MessageCodesCore.InternalPlayerIncidentReportFoundInS3Bucket, MessageDirection.ServerInternal)]
    public class InternalPlayerIncidentReportFoundInS3Bucket : MetaMessage
    {
        public string IncidentId { get; private set; }
        public string BucketKey { get; private set; }

        [MetaDeserializationConstructor]
        public InternalPlayerIncidentReportFoundInS3Bucket(string incidentId, string bucketKey)
        {
            IncidentId = incidentId;
            BucketKey = bucketKey;
        }
    }

    [MetaMessage(MessageCodesCore.InternalNewPlayerIncidentRecorded, MessageDirection.ServerInternal)]
    public class InternalNewPlayerIncidentRecorded : EntityAskRequest<EntityAskOk>
    {
        public string IncidentId { get; private set; }
        public MetaTime OccurredAt { get; private set; }
        public string Type { get; private set; }
        public string SubType { get; private set; }
        public string Reason { get; private set; }

        InternalNewPlayerIncidentRecorded() { }
        public InternalNewPlayerIncidentRecorded(string incidentId, MetaTime occurredAt, string type, string subType, string reason)
        {
            IncidentId = incidentId;
            OccurredAt = occurredAt;
            Type = type;
            SubType = subType;
            Reason = reason;
        }
    }
}
