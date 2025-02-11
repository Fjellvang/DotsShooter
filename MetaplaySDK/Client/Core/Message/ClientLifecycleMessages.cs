// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.TypeCodes;
using System;

namespace Metaplay.Core.Message
{
    /// <summary>
    /// Sent to server when client application goes into the background
    /// </summary>
    [MetaMessage(MessageCodesCore.ClientLifecycleHintPausing, MessageDirection.ClientToServer), MessageRoutingRuleSession]
    public class ClientLifecycleHintPausing : MetaMessage
    {
        public TimeSpan? MaxPauseDuration { get; private set; } // null if not declared
        public string PauseReason { get; private set; } // null if no reason
        public DateTimeOffset ClientTimestamp { get; private set; }

        ClientLifecycleHintPausing() { }
        public ClientLifecycleHintPausing(TimeSpan? maxPauseDuration, string pauseReason, DateTimeOffset clientTimestamp)
        {
            MaxPauseDuration = maxPauseDuration;
            PauseReason = pauseReason;
            ClientTimestamp = clientTimestamp;
        }
    }

    /// <summary>
    /// Sent to server when client application resumes from background.
    /// </summary>
    [MetaMessage(MessageCodesCore.ClientLifecycleHintUnpausing, MessageDirection.ClientToServer), MessageRoutingRuleSession]
    public class ClientLifecycleHintUnpausing : MetaMessage
    {
        public DateTimeOffset ClientTimestamp { get; private set; }

        ClientLifecycleHintUnpausing() { }
        public ClientLifecycleHintUnpausing(DateTimeOffset clientTimestamp)
        {
            ClientTimestamp = clientTimestamp;
        }
    }

    /// <summary>
    /// Sent to server when client application has resumed from background and has run one frame.
    /// </summary>
    [MetaMessage(MessageCodesCore.ClientLifecycleHintUnpaused, MessageDirection.ClientToServer), MessageRoutingRuleSession]
    public class ClientLifecycleHintUnpaused : MetaMessage
    {
        public DateTimeOffset ClientTimestamp { get; private set; }

        ClientLifecycleHintUnpaused() { }
        public ClientLifecycleHintUnpaused(DateTimeOffset clientTimestamp)
        {
            ClientTimestamp = clientTimestamp;
        }
    }
}
