// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Amazon.S3;
using Amazon.S3.Model;
using Metaplay.Cloud;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Services;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Metaplay.Server
{
    [EntityConfig]
    internal sealed class PlayerIncidentPullServiceConfig : EphemeralEntityConfig
    {
        public override EntityKind          EntityKind              => EntityKindCloudCore.PlayerIncidentPullService;
        public override Type                EntityActorType         => typeof(PlayerIncidentPullServiceActor);
        public override NodeSetPlacement    NodeSetPlacement        => NodeSetPlacement.Service;
        public override IShardingStrategy   ShardingStrategy        => ShardingStrategies.CreateSingletonService();
        public override TimeSpan            ShardShutdownTimeout    => TimeSpan.FromSeconds(30);
    }

    public class PlayerIncidentPullServiceActor : EphemeralEntityActor
    {
        protected override AutoShutdownPolicy ShutdownPolicy => AutoShutdownPolicy.ShutdownNever();

        /// <summary>
        /// Minimum time between scans. Each scan reads <see cref="KeysPerScan"/>.
        /// </summary>
        const int ScanMinimumIntervalSeconds = 5;
        const int KeysPerScan = 50;

        readonly struct PendingIncident
        {
            public readonly EntityId    PlayerId;
            public readonly string      IncidentId;
            public readonly string      Key;

            public PendingIncident(EntityId playerId, string incidentId, string key)
            {
                PlayerId = playerId;
                IncidentId = incidentId;
                Key = key;
            }
        }

        readonly Queue<PendingIncident> _pendingIncidents = new Queue<PendingIncident>();
        string                          _bucketName;
        S3BlobStorage                   _s3Storage;
        IAmazonS3                       _client;
        string                          _scanContinuationToken;
        DateTime                        _nextScanAt;

        public PlayerIncidentPullServiceActor(EntityId entityId) : base(entityId)
        {
        }

        protected override Task Initialize()
        {
            BlobStorageOptions blobStorageOptions = RuntimeOptionsRegistry.Instance.GetCurrent<BlobStorageOptions>();
            if (blobStorageOptions.Backend == BlobStorageBackend.S3)
            {
                _s3Storage = (S3BlobStorage)blobStorageOptions.CreatePrivateBlobStorage(PlayerIncidentUploadStorageService.PathPrefix);
                _client = _s3Storage.Client;
                _bucketName = _s3Storage.BucketName;

                StartRandomizedPeriodicTimer(TimeSpan.FromSeconds(1), ActorTick.Instance);
            }

            return Task.CompletedTask;
        }

        protected override Task OnShutdown()
        {
            _s3Storage?.Dispose();
            return Task.CompletedTask;
        }

        [CommandHandler]
        async Task HandleActorTick(ActorTick tick)
        {
            // Scan chunk of incidents, dispatch them to players, repeat

            // Scan next chunk.
            if (_pendingIncidents.Count == 0)
            {
                if (DateTime.UtcNow < _nextScanAt)
                    return;

                // Rate limit scanning
                _nextScanAt = DateTime.UtcNow.AddSeconds(ScanMinimumIntervalSeconds);

                // Scan forward
                ListObjectsV2Request listRequest = new ListObjectsV2Request()
                {
                    BucketName = _bucketName,
                    Prefix = PlayerIncidentUploadStorageService.PathPrefix,
                    ContinuationToken = _scanContinuationToken,
                    MaxKeys = KeysPerScan,
                };

                ListObjectsV2Response listResponse;
                try
                {
                    listResponse = await _client.ListObjectsV2Async(listRequest);
                }
                catch (Exception ex)
                {
                    _log.Warning("Failed to walk incident bucket: {Error}", ex);
                    return;
                }

                // Continue next time from here. If we reached the end, continue from the beginning.
                _scanContinuationToken = listResponse.IsTruncated ? listResponse.NextContinuationToken : null;

                // Find all uploaded but not yet processed incidents (incidents are removed when they are processed).
                foreach (S3Object file in listResponse.S3Objects)
                {
                    if (!file.Key.StartsWith(PlayerIncidentUploadStorageService.PathPrefix, StringComparison.Ordinal)
                        || !PlayerIncidentUploadStorageService.TryParseKey(file.Key.Substring(PlayerIncidentUploadStorageService.PathPrefix.Length), out EntityId playerId, out string incidentId))
                    {
                        _log.Warning("Malformed key format {Key} in incident upload bucket. Deleting.", file.Key);
                        _ = await _client.DeleteObjectAsync(_bucketName, file.Key);
                        continue;
                    }

                    _pendingIncidents.Enqueue(new PendingIncident(playerId, incidentId, file.Key));
                }
            }

            // Process pending incidents.
            // We try to dispatch all incidents between two scans.
            const int numPlayerIncidentsPerSecond = KeysPerScan / ScanMinimumIntervalSeconds;
            for (int ndx = 0; ndx < numPlayerIncidentsPerSecond; ++ndx)
            {
                if (!_pendingIncidents.TryDequeue(out PendingIncident incident))
                    break;

                CastMessage(incident.PlayerId, new InternalPlayerIncidentReportFoundInS3Bucket(incidentId: incident.IncidentId, bucketKey: incident.Key));
            }
        }
    }
}
