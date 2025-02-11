// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Web3;
using Metaplay.Server.AdminApi.AuditLog;
using Metaplay.Server.LiveOpsTimeline.Timeline;
using System;

namespace Metaplay.Server.AdminApi.Controllers
{
    /// <summary>
    /// Base class for all game-server log events
    /// </summary>
    public abstract class GameServerEventPayloadBase : EventPayloadBase
    {
        abstract public string SubsystemName { get; }
    }
    public class GameServerEventBuilder : EventBuilder
    {
        public GameServerEventBuilder(GameServerEventPayloadBase payload) : base(payload, EventTarget.FromGameServer(payload.SubsystemName))
        {
            if (string.IsNullOrEmpty(payload.SubsystemName))
                throw new ArgumentException("Payload's SubsystemName must not be empty");
        }
    }

    /// <summary>
    /// Base class for all player log events
    /// </summary>
    public abstract class PlayerEventPayloadBase : EventPayloadBase
    {
    }
    public class PlayerEventBuilder : EventBuilder
    {
        public PlayerEventBuilder(EntityId playerId, PlayerEventPayloadBase payload) : base(payload, EventTarget.FromEntityId(playerId))
        {
            if (playerId.Kind != EntityKindCore.Player)
                throw new ArgumentException("Entity kind must be Player");
        }
    }

#if !METAPLAY_DISABLE_GUILDS
    /// <summary>
    /// Base class for all guild log events
    /// </summary>
    public abstract class GuildEventPayloadBase : EventPayloadBase
    {
    }
    public class GuildEventBuilder : EventBuilder
    {
        public GuildEventBuilder(EntityId guildId, GuildEventPayloadBase payload) : base(payload, EventTarget.FromEntityId(guildId))
        {
            if (guildId.Kind != EntityKindCore.Guild)
                throw new ArgumentException("Entity kind must be Guild");
        }
    }
#endif

    /// <summary>
    /// Base class for all broadcast log events
    /// </summary>
    public abstract class BroadcastEventPayloadBase : EventPayloadBase
    {
        abstract public int BroadcastId { get; }
    }

    public class BroadcastEventBuilder : EventBuilder
    {
        public BroadcastEventBuilder(BroadcastEventPayloadBase payload) : base(payload, EventTarget.FromBroadcast(payload.BroadcastId))
        {
        }
    }

    /// <summary>
    /// Base class for all notification log events
    /// </summary>
    public abstract class NotificationEventPayloadBase : EventPayloadBase
    {
        abstract public int Id { get; }
    }
    public class NotificationEventBuilder : EventBuilder
    {
        public NotificationEventBuilder(NotificationEventPayloadBase payload) : base(payload, EventTarget.FromNotification(payload.Id))
        {
        }
    }

    /// <summary>
    /// Base class for all experiment log events
    /// </summary>
    public abstract class ExperimentEventPayloadBase : EventPayloadBase
    {
    }
    public class ExperimentEventBuilder : EventBuilder
    {
        public ExperimentEventBuilder(string experimentId, ExperimentEventPayloadBase payload) : base(payload, EventTarget.FromExperiment(experimentId))
        {
        }
    }

    /// <summary>
    /// Base class for all game config log events
    /// </summary>
    public abstract class GameConfigEventPayloadBase : EventPayloadBase
    {
    }
    public class GameConfigEventBuilder : EventBuilder
    {
        public GameConfigEventBuilder(MetaGuid configId, GameConfigEventPayloadBase payload) : base(payload, EventTarget.FromGameConfig(configId))
        {
        }
    }

    /// <summary>
    /// Base class for localization log events
    /// </summary>
    public abstract class LocalizationEventPayloadBase : EventPayloadBase
    {
    }

    public class LocalizationEventBuilder : EventBuilder
    {
        public LocalizationEventBuilder(MetaGuid localizationId, LocalizationEventPayloadBase payload) : base(payload, EventTarget.FromLocalization(localizationId))
        {
        }
    }

    /// <summary>
    /// Base class for all database entities.
    /// </summary>
    public abstract class DatabaseEntityEventPayloadBase : EventPayloadBase
    {
    }
    public class DatabaseEntityEventBuilder : EventBuilder
    {
        public DatabaseEntityEventBuilder(EntityId entityId, DatabaseEntityEventPayloadBase payload) : base(payload, EventTarget.FromEntityId(entityId))
        {
            if (!entityId.IsValid)
                throw new ArgumentException("Entity must be valid");
        }
    }

    /// <summary>
    /// Base class for all matchmaker log events
    /// </summary>
    public abstract class MatchmakerEventPayloadBase : EventPayloadBase
    {
    }
    public class MatchmakerEventBuilder : EventBuilder
    {
        public MatchmakerEventBuilder(EntityId matchmakerId, MatchmakerEventPayloadBase payload) : base(payload, EventTarget.FromEntityId(matchmakerId))
        {
            if (!matchmakerId.IsValid)
                throw new ArgumentException("Entity must be valid");
        }
    }

    public abstract class NftEventPayloadBase : EventPayloadBase
    {
    }
    public class NftEventBuilder : EventBuilder
    {
        public NftEventBuilder(NftKey nftKey, NftEventPayloadBase payload) : base(payload, EventTarget.FromNftKey(nftKey))
        {
        }
    }

    public abstract class NftCollectionEventPayloadBase : EventPayloadBase
    {
    }
    public class NftCollectionEventBuilder : EventBuilder
    {
        public NftCollectionEventBuilder(NftCollectionId collectionId, NftCollectionEventPayloadBase payload) : base(payload, EventTarget.FromNftCollectionId(collectionId))
        {
        }
    }

    /// <summary>
    /// Base class for all league log events
    /// </summary>
    public abstract class LeagueEventPayloadBase : EventPayloadBase
    {
    }
    public class LeagueEventBuilder : EventBuilder
    {
        public LeagueEventBuilder(EntityId leagueId, LeagueEventPayloadBase payload) : base(payload, EventTarget.FromEntityId(leagueId))
        {
            if (!leagueId.IsValid)
                throw new ArgumentException("Entity must be valid");
        }
    }

    public abstract class LiveOpsEventAuditLogEventPayloadBase : EventPayloadBase
    {
    }
    public class LiveOpsEventAuditLogEventBuilder : EventBuilder
    {
        public LiveOpsEventAuditLogEventBuilder(MetaGuid occurrenceId, LiveOpsEventAuditLogEventPayloadBase payload) : base(payload, EventTarget.FromLiveOpsEventOccurrenceId(occurrenceId))
        {
        }
    }

    public abstract class LiveOpsTimelineItemAuditLogEventPayloadBase : EventPayloadBase
    {
        public abstract ItemId GetAuditLogTargetItemId();
    }
    public class LiveOpsTimelineItemAuditLogEventBuilder : EventBuilder
    {
        public LiveOpsTimelineItemAuditLogEventBuilder(LiveOpsTimelineItemAuditLogEventPayloadBase payload) : base(payload, ItemIdToAuditLogEventTarget(payload.GetAuditLogTargetItemId()))
        {
        }

        static EventTarget ItemIdToAuditLogEventTarget(ItemId itemId)
        {
            if (itemId is ItemId.Node(MetaGuid nodeId))
                return EventTarget.FromLiveOpsTimelineNode(nodeId);
            else if (itemId is ItemId.Element(ElementId elementId))
            {
                if (elementId is ElementId.LiveOpsEvent(MetaGuid occurrenceId))
                    return EventTarget.FromLiveOpsEventOccurrenceId(occurrenceId);
                else if (elementId is ElementId.ServerError(MetaGuid serverErrorId))
                    return EventTarget.FromGameServer("ServerErrors"); // \note Not expected to happen because you can't edit server errors
                else
                    throw new InvalidOperationException($"Unhandled liveops timeline element type {elementId?.GetType().ToString() ?? "<null>"}");
            }
            else
                throw new ArgumentException($"Unhandled liveops timeline item type {itemId?.GetType().ToString() ?? "<null>"}", nameof(itemId));
        }
    }
}
