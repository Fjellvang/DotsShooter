// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Model;
using System.Collections.Generic;

namespace Metaplay.Server
{
    [MetaSerializable]
    public class LiveOpsEventsStatistics
    {
        [MetaMember(1)] public Dictionary<MetaGuid, LiveOpsEventOccurrenceStatistics> OccurrenceStatistics { get; private set; } = new();
    }

    [MetaSerializable]
    public class LiveOpsEventOccurrenceStatistics
    {
        [MetaMember(1)] public int ParticipantCount { get; set; } = 0;
    }
}
