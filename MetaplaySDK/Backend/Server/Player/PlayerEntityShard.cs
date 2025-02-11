// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Akka.Actor;
using Metaplay.Cloud.Analytics;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.Analytics;
using Metaplay.Core.Debugging;
using Metaplay.Core.Math;
using Metaplay.Core.Player;
using Metaplay.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Metaplay.Server.Player
{
    internal class PlayerEntityShard : EntityShard
    {
        PeriodThrottle _incidentThrottle;

        public PlayerEntityShard(EntityShardConfig shardConfig) : base(shardConfig)
        {
            _incidentThrottle = new PeriodThrottle(TimeSpan.FromSeconds(1), maxEventsPerDuration: 5, DateTime.UtcNow); // 5 events per second
        }

        protected override void OnChildActorCrashed(EntityState entityState)
        {
            try
            {
                // Throttle when incidents are happening too frequently to prevent causing more issues
                if (!_incidentThrottle.TryTrigger(DateTime.UtcNow))
                {
                    _log.Warning("Dropping PlayerActorCrashed incident report and analytics event due to throttling");
                    return;
                }

                string errorType    = entityState.TerminationReason?.GetType().Name ?? "UnexpectedActorShutdown";
                string errorMessage = entityState.TerminationReason?.Message ?? "No TerminationReason available";
                string stackTrace   = "";
                if (entityState.TerminationReason != null)
                {
                    if (entityState.TerminationReason.InnerException != null)
                        stackTrace = entityState.TerminationReason.InnerException.ToString() + "\n--- End of inner exception stack trace ---\n" + entityState.TerminationReason.StackTrace;
                    else
                        stackTrace = entityState.TerminationReason.StackTrace;
                }

                MetaTime collectedAt = MetaTime.Now;
                string incidentId = PlayerIncidentUtil.EncodeIncidentId(collectedAt, RandomPCG.CreateNew().NextULong());
                PlayerIncidentReport.PlayerActorCrashed incidentReport = new PlayerIncidentReport.PlayerActorCrashed(
                    id:               incidentId,
                    occurredAt:       collectedAt,
                    logEntries:       null,
                    systemInfo:       null,
                    platformInfo:     null,
                    gameConfigInfo:   null,
                    applicationInfo:  null,
                    exceptionType:    errorType,
                    exceptionMessage: errorMessage,
                    stackTrace:       stackTrace);

                string incidentFingerprint = PlayerIncidentUtil.ComputeFingerprint(incidentReport.Type, incidentReport.SubType, incidentReport.GetReason());

                // Send analytics event about player crashing
                AnalyticsEventBatcher<PlayerEventBase, PlayerAnalyticsContext> analyticsEventBatcher = new AnalyticsEventBatcher<PlayerEventBase, PlayerAnalyticsContext>(entityState.EntityId, maxBatchSize: 1);
                PlayerEventActorCrashed                                        analyticsEvent        = new PlayerEventActorCrashed(errorType, errorMessage, Util.TruncateStacktrace(stackTrace), incidentId, incidentFingerprint);

                // Metadata
                AnalyticsEventSpec eventSpec = AnalyticsEventRegistry.GetEventSpec(analyticsEvent.GetType());

                // Enqueue event for sending analytics (if enabled)
                if (eventSpec.SendToAnalytics)
                {
                    string      eventType     = eventSpec.EventType;
                    MetaUInt128 uniqueId      = new MetaUInt128((ulong)collectedAt.MillisecondsSinceEpoch, RandomPCG.CreateNew().NextULong());
                    int         schemaVersion = eventSpec.SchemaVersion;
                    analyticsEventBatcher.Enqueue(
                        source:           entityState.EntityId,
                        collectedAt:      collectedAt,
                        modelTime:        collectedAt,
                        uniqueId:         uniqueId,
                        eventType:        eventType,
                        schemaVersion:    schemaVersion,
                        payload:          analyticsEvent,
                        context:          null,
                        labels:           null,
                        resolver:         null,
                        logicVersion:     null);
                    analyticsEventBatcher.Flush();
                }

                // Persist incident report
                Task _ = Task.Run(async () => await PlayerIncidentStorage.SerializeAndPersistIncidentAsync(entityState.EntityId, incidentReport, source: PlayerIncidentStorage.IncidentSource.ServerGenerated, informPlayerActor: false, sourceEntity: null));
            }
            catch (Exception ex)
            {
                _log.Error("PlayerActorCrashed incident report and analytics event failed: {exception}.", ex);
            }
        }
    }
}
