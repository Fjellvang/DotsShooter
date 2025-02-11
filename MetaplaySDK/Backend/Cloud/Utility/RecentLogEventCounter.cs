// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Model;
using System;
using System.Collections.Generic;

namespace Metaplay.Cloud.Utility
{
    /// <summary>
    /// Counts errors and provides detailed info of the errors for showing the errors on dashboard.
    /// </summary>
    public class RecentLogEventCounter
    {
        [MetaSerializable]
        public class LogEventInfo
        {
            public LogEventInfo()
            {
                Id = MetaGuid.New();
            }

            [MetaMember(1)] public MetaTime Timestamp;
            [MetaMember(2)] public string   Message;
            [MetaMember(3)] public string   LogEventType;
            [MetaMember(4)] public string   Source;
            [MetaMember(5)] public string   SourceType;
            [MetaMember(6)] public string   Exception;
            [MetaMember(7)] public string   StackTrace;
            [MetaMember(8)] public MetaGuid Id { get; private set; }
        }

        [MetaSerializable]
        public class LogEventBatch
        {
            public LogEventBatch() { }
            public LogEventBatch(List<LogEventInfo> logEvents)
            {
                LogEvents = logEvents;
            }

            [MetaMember(1)] public List<LogEventInfo> LogEvents;
        }
        
        public Queue<LogEventInfo> RecentLogEvents { get; set; }
        /// <summary>
        /// For displaying in dash that the limit has been reached. If true, the maximum number of log events
        /// in the recent time frame has been reached. If false, the total log event count is accurate in the recent time frame.
        /// </summary>
        public bool OverMaxRecentLogEvents => RecentLogEvents.Count > _maxLogEvents;
        /// <summary>
        /// Maximum log events that will be returned out of the counter. The internal collections will hold
        /// max+1 items to determine when the collection is over the limit.
        /// </summary>
        readonly int _maxLogEvents;

        List<LogEventInfo> _recentLogEventsSortingList;

        public RecentLogEventCounter(int maxLogEvents)
        {
            RecentLogEvents = new Queue<LogEventInfo>(maxLogEvents + 1);
            _recentLogEventsSortingList = new List<LogEventInfo>(maxLogEvents * 2);
            _maxLogEvents = maxLogEvents;
        }

        public LogEventBatch GetBatch()
        {
            return new LogEventBatch(new List<LogEventInfo>(RecentLogEvents));
        }

        /// <summary>
        /// Record a log event, its source and its time stamp.
        /// </summary>
        public void Add(LogEventInfo logEventInfo)
        {
            if (RecentLogEvents.Count >= _maxLogEvents + 1)
            {
                RecentLogEvents.Dequeue();
            }

            RecentLogEvents.Enqueue(logEventInfo);
        }

        public void AddBatch(LogEventBatch logEventBatch)
        {
            if (logEventBatch.LogEvents.Count == 0)
                return;
            
            // Combine existing log events and new ones from the batch into a list and sort by time stamp
            _recentLogEventsSortingList.Clear();
            _recentLogEventsSortingList.AddRange(RecentLogEvents);
            _recentLogEventsSortingList.AddRange(logEventBatch.LogEvents);
            _recentLogEventsSortingList.Sort((x,y) => x.Timestamp.CompareTo(y.Timestamp));
            RecentLogEvents.Clear();

            // Remove items over limit
            if (_recentLogEventsSortingList.Count > _maxLogEvents + 1)
                _recentLogEventsSortingList.RemoveRange(0, _recentLogEventsSortingList.Count - _maxLogEvents - 1);

            // Add log events back to the queue
            foreach (LogEventInfo logEventInfo in _recentLogEventsSortingList)
            {
                RecentLogEvents.Enqueue(logEventInfo);
            }
        }

        /// <summary>
        /// Removes log events older than <paramref name="errorsKeepDuration"/>.
        /// </summary>
        public void Update(MetaDuration errorsKeepDuration)
        {
            // Cache and use the same "now" time to loop through all log events
            MetaTime nowTime = MetaTime.Now;

            // Remove old log events
            while (true)
            {
                if (RecentLogEvents.TryPeek(out LogEventInfo next))
                {
                    if (next.Timestamp <= nowTime - errorsKeepDuration)
                    {
                        RecentLogEvents.Dequeue();
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Returns the count of recent log events.
        /// </summary>
        public int GetCount()
        {
            if (RecentLogEvents.Count > _maxLogEvents)
                return RecentLogEvents.Count - 1;

            return RecentLogEvents.Count;
        }
        
        public LogEventInfo[] GetErrorsDetails()
        {
            List<LogEventInfo> logEvents = new List<LogEventInfo>(RecentLogEvents);
            if (logEvents.Count > _maxLogEvents)
                logEvents.RemoveRange(0, logEvents.Count - _maxLogEvents);
            return logEvents.ToArray();
        }
        
        public void Clear()
        {
            RecentLogEvents.Clear();
        }
    }
}
