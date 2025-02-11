// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Metaplay.Cloud.Application
{
    internal class MetaplayOnLogEventTrackSink : ILogEventSink
    {
        public MetaplayOnLogEventTrackSink() { }

        public void Emit(LogEvent logEvent)
        {
            // Only track errors
            if (logEvent.Level != LogEventLevel.Error)
            {
                return;
            }

            // Get the log source separately
            string logSource = "";
            if (logEvent.Properties.TryGetValue(Constants.SourceContextPropertyName, out LogEventPropertyValue value) &&
                value is ScalarValue sv &&
                sv.Value is string rawValue)
            {
                logSource = rawValue;
            }

            // Get the timestamp separately
            // \note #gametime This converts logEvent.Timestamp from real time to game time by adding MetaTime.DebugTimeOffset.
            //       It's not clear whether it's better to have these as real or game time:
            //       - For now, choosing to use game time because it's clearer in the dashboard
            //         if most things are game time (rather than mixing real vs game a lot).
            //       - Real time would have the benefit that it is consistent with the log output proper,
            //         and also that then "old" errors would not get pruned immediately when you time-skip
            //         forward by a lot.
            //
            //       Note that whichever (real or game time) is used, it should be consistent
            //       throughout, including in the time retention cutoff in RecentLogEventCounter.
            MetaTime timestamp = MetaTime.FromDateTime(logEvent.Timestamp.UtcDateTime) + MetaTime.DebugTimeOffset;

            // Format the log message correctly
            string logMessage = logEvent.RenderMessage(CultureInfo.InvariantCulture);

            MetaLogger.InvokeLogEventLogged(logEvent.Level, logEvent.Exception, logSource, timestamp, logMessage);
        }
    }
}
