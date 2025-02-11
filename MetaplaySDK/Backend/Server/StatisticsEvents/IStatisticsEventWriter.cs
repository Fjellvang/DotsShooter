// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Metaplay.Server.StatisticsEvents;

public interface IStatisticsEventWriter
{
    public Task WriteEventsUnsafeAsync(IEnumerable<StatisticsEventBase> events);

    public Task<bool> WriteEventAsync(Func<MetaTime, StatisticsEventBase> createEventFunc);
}
