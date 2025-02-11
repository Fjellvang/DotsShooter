// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;

namespace Metaplay.Core
{
    /// <summary>
    /// Basic throttling mechanism for only allowing X events during a single period of Y duration.
    /// Thread-safe via locking.
    /// </summary>
    public class PeriodThrottle
    {
        public readonly TimeSpan        PeriodDuration;
        public readonly int             MaxEventsPerPeriod;

        object                          _lock = new object();
        DateTime                        _periodStart;
        int                             _numEventsInPeriod = 0;

        // Statistics
        public int                      TotalEvents         { get; private set; }
        public int                      TotalEventsDropped  { get; private set; }

        public PeriodThrottle(TimeSpan duration, int maxEventsPerDuration, DateTime now)
        {
            PeriodDuration = duration;
            MaxEventsPerPeriod = maxEventsPerDuration;

            _periodStart = now;
        }

        /// <summary>
        /// Returns true if a new event is allowed now, i.e. the event is not throttled.
        /// </summary>
        public bool TryTrigger(DateTime now)
        {
            lock (_lock)
            {
                // Sanity check: if clock is rewinded, reset the period
                if (now < _periodStart)
                    _periodStart = now;

                TotalEvents += 1;

                DateTime periodEnd = _periodStart + PeriodDuration;
                if (now >= periodEnd)
                {
                    // If previous period over, start a new one with one event
                    _periodStart = now;
                    _numEventsInPeriod = 1;
                    return true;
                }
                else
                {
                    // For continuing periods, check if number of events is exceeded
                    _numEventsInPeriod += 1;
                    bool shouldTrigger = _numEventsInPeriod <= MaxEventsPerPeriod;
                    if (!shouldTrigger)
                        TotalEventsDropped += 1;
                    return shouldTrigger;
                }
            }
        }
    }
}
