// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Server.StatisticsEvents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Metaplay.Server.Tests
{
    internal class MockReaderWriter : IStatisticsEventReader, IStatisticsEventWriter
    {
        MetaDictionary<string, StatisticsEventBase> _events = new MetaDictionary<string, StatisticsEventBase>();

        /// <inheritdoc />
        public Task<IAsyncEnumerable<StatisticsEventBase>> ReadEventsAsync(MetaTime startTime, MetaTime endTime)
        {
            IEnumerable<StatisticsEventBase> events = _events.Values.OrderBy(e => e.Timestamp)
                .Where(e => e.Timestamp >= startTime && e.Timestamp < endTime);

            return Task.FromResult(events.ToAsyncEnumerable());
        }

        /// <inheritdoc />
        public Task WriteEventsUnsafeAsync(IEnumerable<StatisticsEventBase> events)
        {
            foreach (StatisticsEventBase e in events)
            {
                if (_events.ContainsKey(e.UniqueKey))
                    continue;

                _events.Add(e.UniqueKey, e);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<bool> WriteEventAsync(Func<MetaTime, StatisticsEventBase> createEventFunc)
        {
            StatisticsEventBase statisticsEvent = createEventFunc(MetaTime.Now);

            if (_events.ContainsKey(statisticsEvent.UniqueKey))
                return Task.FromResult(false);

            _events.Add(statisticsEvent.UniqueKey, statisticsEvent);
            return Task.FromResult(true);
        }
    }
}
