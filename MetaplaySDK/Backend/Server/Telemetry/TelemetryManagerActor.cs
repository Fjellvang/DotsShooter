// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud;
using Metaplay.Cloud.Application;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Metrics;
using Metaplay.Cloud.Options;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Services;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.Json;
using Metaplay.Core.Model;
using Metaplay.Core.TypeCodes;
using Metaplay.Server.KeyManager;
using Metaplay.Server.StatisticsEvents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Metaplay.Server.TelemetryManager
{
    [EntityConfig]
    public class TelemetryManagerEntityConfig : EphemeralEntityConfig
    {
        public override EntityKind EntityKind => EntityKindCloudCore.TelemetryManager;
        public override EntityShardGroup EntityShardGroup => EntityShardGroup.Workloads;
        public override NodeSetPlacement NodeSetPlacement => NodeSetPlacement.Service;
        public override IShardingStrategy ShardingStrategy => ShardingStrategies.CreateSingletonService();
        public override TimeSpan ShardShutdownTimeout => TimeSpan.FromSeconds(60);
        public override Type EntityActorType => typeof(TelemetryManagerActor);
    }

    /// <summary>
    /// Telemetry manager communicates with the Metaplay portal to inform it of the status of the running
    /// server process: project information, the runtime environment it's running in and various component
    /// versions used (Metaplay SDK, .NET runtime, Helm chart, etc.). In return, we get messages about
    /// outdated components to be visualized on the dashboard.
    ///
    /// The events are sent as HTTP requests with JWT signatures generated with <see cref="KeyManagerActor"/>
    /// so that the portal can securely identify who is sending the event for environments running in the cloud.
    /// For locally running servers, the sending game server is treated as untrusted source but we still get
    /// back the response messages.
    /// </summary>
    public class TelemetryManagerActor : EphemeralEntityActor
    {
        public static readonly EntityId EntityId = EntityId.Create(EntityKindCloudCore.TelemetryManager, 0);

        static readonly string TargetBaseUrl = "https://portal.metaplay.dev"; //"http://localhost:3000";
        static readonly TimeSpan SendServerStatusInterval = TimeSpan.FromHours(1);

        protected sealed override AutoShutdownPolicy ShutdownPolicy => AutoShutdownPolicy.ShutdownNever();

        DateTime _messagesUpdatedAt = DateTime.UnixEpoch;
        TelemetryMessage[] _latestMessages;

        DateTime                 _lastMetricsSentAt                  = DateTime.UtcNow - _metricsSendInterval + _metricsRandomSendTimeWithinOneHour;
        static readonly TimeSpan _metricsSendInterval                = TimeSpan.FromHours(1);
        static readonly TimeSpan _metricsRandomSendTimeWithinOneHour = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000 * 3600));
        IStatisticsPageStorage   _readStorage;

        public TelemetryManagerActor(EntityId entityId) : base(entityId)
        {
        }

        protected override async Task Initialize()
        {
            StartPeriodicTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), ActorTick.Instance);

            await base.Initialize();

            IStatisticsPageStorage dbStorage    = new DatabaseStatisticsPageStorage();
            IStatisticsPageStorage actorStorage = new CollectorActorStatisticsPageStorage(this, StatsCollectorManager.EntityId);

            _readStorage = dbStorage.AsTiered(actorStorage).AsCached(1024 * 64, true);
        }

        [CommandHandler]
        async Task HandleActorTick(ActorTick _)
        {
            // If telemetry not enabled, bail out
            TelemetryOptions opts = RuntimeOptionsRegistry.Instance.GetCurrent<TelemetryOptions>();
            if (!opts.Enabled)
                return;

            // Periodically send server status event & get telemetry messages back
            if (DateTime.UtcNow >= _messagesUpdatedAt + SendServerStatusInterval)
                await SendServerStatusAsync();

            // Periodically send metrics event
            if (DateTime.UtcNow >= _lastMetricsSentAt + _metricsSendInterval)
            {
                _lastMetricsSentAt = DateTime.UtcNow;
                await SendMetricsAsync();
            }
        }

        /// <summary>
        /// Payload that is sent with the 'ServerStatus' telemetry event to the portal.
        /// </summary>
        public class ServerStatusBody
        {
            public required string ProjectName { get; init; } // Project name
            public required string EnvironmentName { get; init; } // Environment name
            public required EnvironmentFamily EnvironmentFamily { get; init; } // Environment family
            public required bool IsRunningInContainer { get; init; } // Is the server running within a containerized environment
            public required string DotnetVersion { get; init; } // Version of .NET runtime
            public required string MetaplaySdkVersion { get; init; } // Version of the Metaplay SDK
            public required string HelmChartVersion { get; init; } // Version of the Helm chart used to deploy the server (if running in cloud)
            public required string InfrastructureVersion { get; init; } // Version of the infrastructure where the server is deployed on (if running in cloud)
        }

        /// <summary>
        /// Message that the portal responds with when sending the 'ServerStatus' event.
        /// </summary>
        [MetaSerializable]
        public class TelemetryMessage
        {
            [MetaSerializable]
            public struct LinkSpec
            {
                [MetaMember(1)] public string Text { get; set; }
                [MetaMember(2)] public string Url { get; set; }

                public LinkSpec(string text, string url)
                {
                    Text = text;
                    Url = url;
                }
            }

            [MetaMember(2)] public string Category { get; set; } // \note Using string for now, switch to enum when the categories stabilize
            [MetaMember(1)] public string Level { get; set; } // \note Using string for now, switch to enum when the levels stabilize
            [MetaMember(3)] public string Title { get; set; }
            [MetaMember(4)] public string Body { get; set; }
            [MetaMember(5)] public LinkSpec[] Links { get; set; }

            TelemetryMessage() { }
            public TelemetryMessage(string category, string level, string title, string body, LinkSpec[] links)
            {
                Category = category;
                Level = level;
                Title = title;
                Body = body;
                Links = links;
            }
        }

        /// <summary>
        /// Response type from the developer portal to the 'ServerStatus' telemetry event.
        /// </summary>
        public class TelemetryStatusResponse
        {
            public TelemetryMessage[] Messages { get; set; }
        }

        /// <summary>
        /// Send a telemetry event to the Metaplay portal about the component versions used. The endpoint
        /// responds with a list of messages regarding outdated components and similar. Print the messages
        /// into the server log, and remember them for visualizing on the dashboard.
        /// </summary>
        /// <returns></returns>
        async Task SendServerStatusAsync()
        {
#if false
            // DEBUG DEBUG: Mock messages to help with testing
            _messagesUpdatedAt = DateTime.UtcNow;
            _latestMessages = [
                new ServerStatusMessage(
                    "OutdatedComponent",
                    "Information",
                    "Metaplay SDK v29.0 is available",
                    "Metaplay SDK version 29.0 has been published. We recommend updating to the latest version to get access to the latest features and stability improvements!",
                    [
                        new ServerStatusMessage.LinkSpec("Metaplay SDK release notes", "https://docs.metaplay.io/miscellaneous/release-notes/release-28.html"),
                        new ServerStatusMessage.LinkSpec("Metaplay SDK upgrade guide", "https://docs.metaplay.io/TODO/guide/sdk-upgrade.html")
                    ]),
                new ServerStatusMessage(
                    "OutdatedComponent",
                    "Information",
                    "Helm chart v0.7.0 is available",
                    "Metaplay Helm chart v0.7.0 has been published. We recommend updating to the latest version to get access to the latest features and stability improvements!",
                    null)
                ];
            return;
#endif

            // Fetch options
            MetaplayCoreOptions coreOpts = IntegrationRegistry.Get<IMetaplayCoreOptionsProvider>().Options;
            (EnvironmentOptions envOpts, DeploymentOptions deployOpts) = RuntimeOptionsRegistry.Instance.GetCurrent<EnvironmentOptions, DeploymentOptions>();

            // URL where to send the telemetry event
            string targetUrl = $"{TargetBaseUrl}/api/external/gameserver/status";

            try
            {
                // Encode the request body
                ServerStatusBody body = new ServerStatusBody
                {
                    ProjectName             = coreOpts.ProjectName,
                    EnvironmentName         = envOpts.Environment,
                    EnvironmentFamily       = RuntimeEnvironmentInfo.Instance.EnvironmentFamily,
                    IsRunningInContainer    = RuntimeEnvironmentInfo.Instance.IsRunningInContainer,
                    DotnetVersion           = RuntimeCollector.DotnetVersion,
                    MetaplaySdkVersion      = deployOpts.MetaplayVersion.ToString(),
                    HelmChartVersion        = deployOpts.ChartVersion,
                    InfrastructureVersion   = deployOpts.InfrastructureVersion,
                };
                string reqBodyJson = JsonSerialization.SerializeToString(body);

                // Sign the request
                string jwt = await SignRequestAsync(TargetBaseUrl, reqBodyJson, "ServerStatus");
                // _log.Debug("Sending telemetry event:\n  TargetUrl: {TargetUrl}\n  Authorization: Bearer {Token}\n  Body: {Body}", targetUrl, jwt, reqBodyJson);

                // Make the request (with signature JWT as bearer header)
                AuthenticationHeaderValue authHeader = new("Bearer", jwt);
                TelemetryStatusResponse statusResponse = await HttpUtil.RequestJsonPostAsync<TelemetryStatusResponse>(HttpUtil.SharedJsonClient, targetUrl, reqBodyJson, authHeader);

                // Handle the response
                if (statusResponse != null)
                {
                    // Print messages received from the portal (if any)
                    if (statusResponse.Messages != null)
                    {
                        foreach (TelemetryMessage msg in statusResponse.Messages)
                        {
                            bool hasLinks = (msg.Links != null && msg.Links.Length > 0);
                            string linksText = hasLinks ? string.Join("", msg.Links.Select(link => $"\n{link.Text}: {link.Url}")) : "";
                            _log.Information("Received message from portal: [{Level}] {Title}\n{Body}{Links}", msg.Level, msg.Title, msg.Body, linksText);
                        }
                    }

                    // Remember the messages for visualizing on the dashboard
                    _messagesUpdatedAt = DateTime.UtcNow;
                    _latestMessages = statusResponse.Messages;
                }
                else
                    _log.Warning("Empty response to status event from developer portal");
            }
            catch (Exception ex)
            {
                _log.Warning("Failed to send server status telemetry event to {TargetUrl}: {Exception}", targetUrl, ex.Message);
            }
        }

        public class MetricsBody
        {
            public class MetricDataSetGroup
            {
                public required string                            Id            { get; init; }
                public required List<MetricDataSet>               DataSets      { get; init; }
            }

            public class MetricDataSet
            {
                public string                               Resolution { get; init; }
                public string                               Cohort     { get; init; }
                public MetaDictionary<DateTime, double?> Data       { get; init; }
            }

            public required string                   ProjectName       { get; init; }
            public required string                   EnvironmentName   { get; init; }
            public required EnvironmentFamily        EnvironmentFamily { get; init; }
            public required List<MetricDataSetGroup> DataSetGroups     { get; init; }
        }

        /// <summary>
        /// Send a metrics event to the portal with DAU and in the future possibly other metrics.
        /// </summary>
        public async Task SendMetricsAsync()
        {
            if (RuntimeEnvironmentInfo.Instance.EnvironmentFamily == EnvironmentFamily.Local)
            {
                // Don't try to send metrics from local environments
                return;
            }

            // Fetch options
            MetaplayCoreOptions coreOpts = IntegrationRegistry.Get<IMetaplayCoreOptionsProvider>().Options;
            EnvironmentOptions envOpts = RuntimeOptionsRegistry.Instance.GetCurrent<EnvironmentOptions>();

            // URL where to send the telemetry event
            string targetUrl = $"{TargetBaseUrl}/api/v1/metrics/ingest";

            try
            {
                StatsCollectorStatisticsTimelineResponse timelinesResponse = await EntityAskAsync<StatsCollectorStatisticsTimelineResponse>(StatsCollectorManager.EntityId, StatsCollectorStatisticsTimelineRequest.Instance);

                StatisticsPageTimeline dailyTimeline = timelinesResponse.Daily;

                // Get the DAU data
                string                          dauId = "dau";
                List<MetricsBody.MetricDataSet> dauData    = await GetCohortMetricData(StatisticsTimeSeriesRegistryBase.DauCohorts, dailyTimeline);

                if (dauData == null || dauData.Count == 0)
                {
                    _log.Error("Failed to send metrics event: DAU data not available");
                    return;
                }

                // Encode the request body
                MetricsBody body = new MetricsBody
                {
                    ProjectName = coreOpts.ProjectName,
                    EnvironmentName = envOpts.Environment,
                    EnvironmentFamily = RuntimeEnvironmentInfo.Instance.EnvironmentFamily,
                    DataSetGroups = new List<MetricsBody.MetricDataSetGroup>()
                    {
                        new MetricsBody.MetricDataSetGroup
                        {
                            Id       = dauId,
                            DataSets = dauData,
                        },
                    },
                };
                string reqBodyJson = JsonSerialization.SerializeToString(body);

                // Sign the request
                string jwt = await SignRequestAsync(TargetBaseUrl, reqBodyJson, "metrics");
                _log.Debug("Sending metrics event:\n  TargetUrl: {TargetUrl}\n Body: {Body}", targetUrl, reqBodyJson);

                // Make the request (with signature JWT as bearer header)
                AuthenticationHeaderValue authHeader = new("Bearer", jwt);
                object response = await HttpUtil.RequestJsonPostAsync<object>(HttpUtil.SharedJsonClient, targetUrl, reqBodyJson, authHeader);
                _log.Debug("Response from portal for metrics event: {response}", response);
            }
            catch (Exception ex)
            {
                _log.Warning("Failed to send metrics event to {TargetUrl}: {Exception}", targetUrl, ex.Message);
            }
        }

        async Task<MetricsBody.MetricDataSet> GetSimpleMetricData<TNumber>(ISimpleReadableTimeSeries<TNumber> timeSeries, StatisticsPageTimeline timeline)
            where TNumber : struct, INumber<TNumber>
        {
            if (timeSeries == null)
                return null;

            // Define the time range for the past 30 days
            MetaTime endTime   = MetaTime.Now;
            MetaTime startTime = endTime - MetaDuration.FromDays(30);
            startTime = MetaTime.Max(startTime, timeline.EpochTime);

            StatisticsTimeSeriesView<TNumber> view = await timeSeries.GetReader(timeline, _readStorage).ReadTimeSpan(startTime, endTime);

            MetaDictionary<DateTime, double?> values = new MetaDictionary<DateTime, double?>(view.NumBuckets);
            MetaTime                             time   = view.StartTime;

            for (int i = 0; i < view.NumBuckets; i++)
            {
                if (!view.Buckets[i].HasValue)
                    values.Add(time.ToDateTime(), null);
                else
                {
                    TNumber bucketValue = view.Buckets[i].Value;
                    double? dbl         = double.CreateSaturating(bucketValue);
                    if (timeSeries.DashboardInfo.ValueTransformer != null)
                        dbl = timeSeries.DashboardInfo.ValueTransformer(dbl);
                    values.Add(time.ToDateTime(), dbl);
                }
                time += view.BucketInterval;
            }

            return new MetricsBody.MetricDataSet
            {
                Cohort     = string.Empty,
                Data       = values,
                Resolution = timeline.Resolution.Name,
            };
        }

        async Task<List<MetricsBody.MetricDataSet>> GetCohortMetricData<TNumber>(ICohortReadableTimeSeries<TNumber> timeSeries, StatisticsPageTimeline timeline)
            where TNumber : struct, INumber<TNumber>
        {
            if (timeSeries == null)
                return null;

            // Define the time range for the past 30 days
            MetaTime endTime   = MetaTime.Now;
            MetaTime startTime = endTime - MetaDuration.FromDays(30);
            startTime = MetaTime.Max(startTime, timeline.EpochTime);

            List<MetricsBody.MetricDataSet> result = new List<MetricsBody.MetricDataSet>();

            foreach (string cohort in timeSeries.Cohorts)
            {
                StatisticsTimeSeriesView<TNumber> view = await timeSeries.GetReader(timeline, _readStorage, cohort).ReadTimeSpan(startTime, endTime);

                MetaDictionary<DateTime, double?> values = new MetaDictionary<DateTime, double?>(view.NumBuckets);
                MetaTime                          time   = view.StartTime;

                for (int i = 0; i < view.NumBuckets; i++)
                {
                    if (!view.Buckets[i].HasValue)
                        values.Add(time.ToDateTime(), null);
                    else
                    {
                        TNumber bucketValue = view.Buckets[i].Value;
                        double? dbl         = double.CreateSaturating(bucketValue);
                        if (timeSeries.DashboardInfo.ValueTransformer != null)
                            dbl = timeSeries.DashboardInfo.ValueTransformer(dbl);
                        values.Add(time.ToDateTime(), dbl);
                    }
                    time += view.BucketInterval;
                }

                result.Add(new MetricsBody.MetricDataSet
                {
                    Cohort     = cohort,
                    Data       = values,
                    Resolution = timeline.Resolution.Name,
                });
            }

            return result;
        }

        async Task<string> SignRequestAsync(string audience, string payload, string subject)
        {
            // Compute hash using: base64(sha256(toUTF8(payload)))
            string payloadHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

            // Request the KeyManager to sign the request (including payload hash)
            KeyManagerActor.CreateSignatureJwtRequest req = new KeyManagerActor.CreateSignatureJwtRequest(
                audience: audience,
                subject: subject,
                payloadHash: payloadHash);
            KeyManagerActor.CreateSignatureJwtResponse signResponse = await EntityAskAsync(KeyManagerActor.EntityId, req);

            return signResponse.Jwt;
        }

        #region Fetch latest messages

        [MetaMessage(MessageCodesCore.TelemetryManagerGetMessagesRequest, MessageDirection.ServerInternal)]
        public class GetMessagesRequest : EntityAskRequest<GetMessagesResponse>
        {
            public static readonly GetMessagesRequest Instance = new GetMessagesRequest();

            GetMessagesRequest() { }
        }

        [MetaMessage(MessageCodesCore.TelemetryManagerGetMessagesResponse, MessageDirection.ServerInternal)]
        public class GetMessagesResponse : EntityAskResponse
        {
            public MetaTime UpdatedAt { get; private set; }
            public TelemetryMessage[] Messages { get; private set; }

            GetMessagesResponse() { }
            public GetMessagesResponse(MetaTime updatedAt, TelemetryMessage[] messages)
            {
                UpdatedAt = updatedAt;
                Messages = messages;
            }
        }

        [EntityAskHandler]
        public GetMessagesResponse HandleGetMessagesRequest(GetMessagesRequest req)
        {
            return new GetMessagesResponse(MetaTime.FromDateTime(_messagesUpdatedAt), _latestMessages);
        }

        #endregion
    }
}
