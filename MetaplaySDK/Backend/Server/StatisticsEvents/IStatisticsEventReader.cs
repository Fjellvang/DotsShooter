// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Metaplay.Server.StatisticsEvents;

public interface IStatisticsEventReader
{
    public Task<IAsyncEnumerable<StatisticsEventBase>> ReadEventsAsync(MetaTime startTime, MetaTime endTime);
}
