// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Amazon.S3;
using Amazon.S3.Model;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Persistence;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Services;
using Metaplay.Core;
using Metaplay.Core.Debugging;
using Metaplay.Core.IO;
using Metaplay.Core.Memory;
using Metaplay.Core.Serialization;
using Metaplay.Server.Database;
using Metaplay.Server.StatisticsEvents;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Metaplay.Server
{
    [RuntimeOptions("PlayerIncident", isStatic: true, "Configuration options for player Incidents.")]
    public class PlayerIncidentOptions : RuntimeOptionsBase
    {
        [MetaDescription("Enables uploading of player incident reports to the server.")]
        public bool     EnableUploads                               { get; private set; } = true;

        [MetaDescription("The percentage likelihood that an incident of a given type is uploaded.")]
        public Dictionary<string, int> UploadPercentagesPerIncidentType { get; private set; } = new Dictionary<string, int>()
        {
            {"TerminalNetworkError", 100},
            {"UnhandledExceptionError", 100},
            {"SessionCommunicationHanged", 100},
            {"SessionStartFailed", 100},
            {"CompanyIdLoginError", 100},
            {"PlayerChecksumMismatch", 100},
        };

        /// <summary>
        /// <inheritdoc cref="Metaplay.Core.Message.Handshake.ServerOptions.PushUploadPercentageSessionStartFailedIncidentReport"/>
        /// </summary>
        [MetaDescription("The percentage likelihood that an incident during session start is reported immediately.")]
        public int      PushUploadPercentageSessionStartFailed      { get; private set; } = 100;

        /// <summary>
        /// After a game session resumption, if the server expects a
        /// <see cref="Metaplay.Core.Message.SessionPing"/> from the client
        /// but does not receive it within this duration, the server
        /// reports a SessionCommunicationHanged incident (with SubType
        /// Metaplay.ServerSideSessionPingDurationThresholdExceeded).
        /// </summary>
        [MetaDescription("A time limit in which the client must acknowldege session resumption. Exceeding this limit results in an incident report.")]
        public TimeSpan SessionPingDurationIncidentThreshold        { get; private set; } = TimeSpan.FromSeconds(10);
        [MetaDescription("The maximum number of server-side `SessionCommunicationHanged` incidents generated per session.")]
        public int      MaxSessionPingDurationIncidentsPerSession   { get; private set; } = 3;

        public override Task OnLoadedAsync()
        {
            // Validate that all upload percentage keys refer to real types
            foreach (KeyValuePair<string, int> uploadPercentage in UploadPercentagesPerIncidentType)
            {
                bool found = false;
                foreach (KeyValuePair<int, Type> serializableType in MetaSerializerTypeRegistry.Instance.GetSerializableType(typeof(PlayerIncidentReport)).DerivedTypes)
                {
                    if (serializableType.Value.Name == uploadPercentage.Key)
                    {
                        found = true;
                    }
                }

                if (!found)
                {
                    throw new ArgumentException($"{nameof(PlayerIncidentOptions)}.{nameof(UploadPercentagesPerIncidentType)} contains a key, '{uploadPercentage.Key}' that is not recognized as a MetaSerializable type or does not derive from {nameof(PlayerIncidentReport)}.");
                }
            }

            return base.OnLoadedAsync();
        }
    }

    /// <summary>
    /// Database-persisted form of <see cref="Metaplay.Core.Debugging.PlayerIncidentReport"/>.
    /// </summary>
    [Table("PlayerIncidents")]
    [Index(nameof(PlayerId))]
    [Index(nameof(PersistedAt))]
    [Index(nameof(Fingerprint), nameof(PersistedAt))]
    public class PersistedPlayerIncident : IPersistedItem
    {
        [Key]
        [Required]
        [MaxLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string               IncidentId      { get; set; }   // \note Also acts as index for OccurredAt (encoded at beginning of IncidentId)

        [Required]
        [PartitionKey]
        [MaxLength(64)]
        [Column(TypeName = "varchar(64)")]
        // \todo [petri] foreign key?
        public string               PlayerId        { get; set; }

        [Required]
        [MaxLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string               Fingerprint     { get; set; }   // Uniquely identifying fingerprint for the kind of error (MD5 of Type, SubType and Reason)

        [Required]
        [MaxLength(128)]
        [Column(TypeName = "varchar(128)")]
        public string               Type            { get; set; }   // Type of incident (type of PlayerIncidentReport, eg, "TerminalNetworkError")

        [Required]
        [MaxLength(128)]
        [Column(TypeName = "varchar(128)")]
        public string               SubType         { get; set; }   // Sub-type of incident (eg, "NullReferenceError" or "SessionForceTerminated")

        [Required]
        [MaxLength(256)]
        [Column(TypeName = "varchar(256)")]
        public string               Reason          { get; set; }   // Uniquely identifying reason for incident (eg, "SessionForceTerminated { KickedByAdminAction }" or first line of stack trace)

        [Required]
        [Column(TypeName = "DateTime")]
        public DateTime             PersistedAt     { get; set; }

        [Required]
        public byte[]               Payload         { get; set; }   // TaggedSerialized<PlayerIncidentReport> (compressed using Compression)

        [Required]
        public CompressionAlgorithm Compression     { get; set; }   // Compression algorithm for Payload

        /// <summary>
        /// Extract the metadata of the incident.
        /// </summary>
        /// <returns></returns>
        public PlayerIncidentHeader ToHeader()
        {
            // \note Keep in sync with HeaderMemberNames, which is used
            //       to fetch just the required data from the database.
            return new PlayerIncidentHeader(
                IncidentId,
                EntityId.ParseFromString(PlayerId),
                Fingerprint,
                Type,
                SubType,
                Reason,
                PersistedAt);
        }

        public static readonly string[] HeaderMemberNames = new string[]
        {
            nameof(IncidentId),
            nameof(PlayerId),
            nameof(Fingerprint),
            nameof(Type),
            nameof(SubType),
            nameof(Reason),
            nameof(PersistedAt),
        };
    }

    /// <summary>
    /// Contains the metadata of a <see cref="PlayerIncidentReport"/>. Used when querying lists of incidents into dashboard.
    /// </summary>
    public class PlayerIncidentHeader
    {
        public string   IncidentId  { get; }
        public EntityId PlayerId    { get; }
        public string   Fingerprint { get; }
        public string   Type        { get; }
        public string   SubType     { get; }
        public string   Reason      { get; }
        public MetaTime OccurredAt  { get; }

        // Used only on server for showing in dashboard, copied from PersistedPlayerIncident when read from db
        public DateTime UploadedAt { get; }

        PlayerIncidentHeader() { }
        public PlayerIncidentHeader(string incidentId, EntityId playerId, string fingerprint, string type, string subType, string reason, DateTime uploadedAt)
        {
            IncidentId  = incidentId;
            PlayerId    = playerId;
            Fingerprint = fingerprint;
            Type        = type;
            SubType     = subType;
            Reason      = reason;
            OccurredAt  = PlayerIncidentUtil.GetOccurredAtFromIncidentId(incidentId);
            UploadedAt  = uploadedAt;
        }
    }

    /// <summary>
    /// Aggregate statistics about player incidents by type (used by when querying into dashboard).
    /// </summary>
    public class PlayerIncidentStatistics
    {
        public string   Fingerprint                 { get; private set; }
        public string   Type                        { get; private set; }
        public string   SubType                     { get; private set; }
        public string   Reason                      { get; private set; }
        public int      Count                       { get; private set; }
        public bool     CountIsLimitedByQuerySize   { get; set; }

        public PlayerIncidentStatistics() { }
        public PlayerIncidentStatistics(string fingerprint, string type, string subType, string reason, int count, bool countIsLimitedByQuerySize)
        {
            Fingerprint = fingerprint;
            Type = type;
            SubType = subType;
            Reason = reason;
            Count = count;
            CountIsLimitedByQuerySize = countIsLimitedByQuerySize;
        }
    }

    public static class PlayerIncidentStorage
    {
        // \note: game_player_incident_reports_persisted is used by alerting rules. Careful if editing.
        static Prometheus.Counter c_incidentReportsPersisted = Prometheus.Metrics.CreateCounter("game_player_incident_reports_persisted", "Number of player incident reports persisted (by kind)", "kind");
        static Prometheus.Counter c_incidentReportSources = Prometheus.Metrics.CreateCounter("game_player_incident_uploads_by_source_total", "Number of player incidents uploaded (by source)", "source");

        public enum IncidentSource
        {
            GameProtocol,
            ServerGenerated,
            SessionStartAbortTrailer,
            HttpUpload,
            OrphanHttpUpload,
        }

        /// <summary>
        /// Serializes and persists an incident report, and optionally informs the PlayerActor about the incident. If <paramref name="informPlayerActor"/> is true, <paramref name="sourceEntity"/> must not be null for the message to be sent.
        /// </summary>
        /// <param name="playerId">ID of the player that the incident is for.</param>
        /// <param name="report">The incident report.</param>
        /// <param name="informPlayerActor">Whether to inform the PlayerActor about the incident being recorded.</param>
        /// <param name="sourceEntity">The entity that will be used to message the PlayerActor if <paramref name="informPlayerActor"/> is true.</param>
        /// <returns></returns>
        public static async Task SerializeAndPersistIncidentAsync(EntityId playerId, PlayerIncidentReport report, IncidentSource source, bool informPlayerActor, IEntityMessagingApi sourceEntity)
        {
            byte[] uncompressedBytes = MetaSerialization.SerializeTagged<PlayerIncidentReport>(report, MetaSerializationFlags.Persisted, logicVersion: null);
            byte[] payload           = CompressUtil.DeflateCompress(uncompressedBytes);

            await PersistIncidentAsync(playerId, report, payload, source);

            // Inform player about persisted incident
            if (sourceEntity != null && informPlayerActor)
            {
                await sourceEntity.EntityAskAsync(
                    playerId,
                    new InternalNewPlayerIncidentRecorded(
                        report.IncidentId,
                        report.OccurredAt,
                        report.Type,
                        report.SubType,
                        PlayerIncidentUtil.TruncateReason(report.GetReason())));
            }
        }

        public static async Task PersistIncidentAsync(EntityId playerId, PlayerIncidentReport report, byte[] deflateCompressedReportPayload, IncidentSource source)
        {
            // \todo [petri] validate incidentId some more, as we use it as key in database

            // Truncate long incident reasons (just in case)
            string reason = PlayerIncidentUtil.TruncateReason(report.GetReason());

            // Persist in database
            PersistedPlayerIncident persisted = new PersistedPlayerIncident
            {
                IncidentId  = report.IncidentId,
                PlayerId    = playerId.ToString(),
                Fingerprint = PlayerIncidentUtil.ComputeFingerprint(report.Type, report.SubType, reason),
                Type        = report.Type,
                SubType     = report.SubType,
                Reason      = reason,
                PersistedAt = MetaTime.Now.ToDateTime(),
                Payload     = deflateCompressedReportPayload,
                Compression = CompressionAlgorithm.Deflate,
            };
            bool wasNew = await MetaDatabase.Get().InsertOrIgnoreAsync(persisted);

            // \todo [petri] handle attachments?

            // Metrics
            // Only report metrics if incident didn't already exist in database
            if (wasNew)
            {
                c_incidentReportsPersisted.WithLabels(GetIncidentKindLabelForMetrics(report)).Inc();
                c_incidentReportSources.WithLabels(GetIncidentSourceLabelForMetrics(source)).Inc();
                
                // Collect statistics
            	await StatisticsEventWriter.WriteEventAsync(ts => new StatisticsEventIncidentReport(ts, playerId, report.Type));
            }
        }

        /// <summary>
        /// Get the percentage of reports that should be uploaded for this specific type.
        /// </summary>
        public static int GetUploadPercentage(IMetaLogger log, ClientAvailableIncidentReport header)
        {
            PlayerIncidentOptions incidentOpts = RuntimeOptionsRegistry.Instance.GetCurrent<PlayerIncidentOptions>();

            if (!incidentOpts.EnableUploads)
                return 0;

            if (incidentOpts.UploadPercentagesPerIncidentType.TryGetValue(header.Type, out int uploadPercentage))
            {
                return uploadPercentage;
            }
            else
            {
                // Default to 100% upload for types that are not explicitly set in runtime options
                return 100;
            }
        }

        static string GetIncidentKindLabelForMetrics(PlayerIncidentReport report)
        {
            if (report is PlayerIncidentReport.SessionCommunicationHanged sessionCommunicationHanged)
            {
                // Include terse information about subtype (whether report
                // comes from client or server) and ping id.

                string subTypeStr;
                if (sessionCommunicationHanged.SubType == "Metaplay.SessionPingPongDurationThresholdExceeded")
                    subTypeStr = "ClientPingPong";
                else if (sessionCommunicationHanged.SubType == "Metaplay.ServerSideSessionPingDurationThresholdExceeded")
                    subTypeStr = "ServerPing";
                else
                    subTypeStr = "Unknown";

                int pingId = sessionCommunicationHanged.PingId;
                // \note Restrict ping id in metrics label, we don't want to spam metrics labels.
                string pingIdStr;
                if (pingId < 0)
                {
                    // Can come from a bad client
                    pingIdStr = "Negative";
                }
                else if (pingId > 10)
                {
                    // Can come from client or server - only number of incidents per session
                    // is restricted, not ping id.
                    pingIdStr = "Over10";
                }
                else
                    pingIdStr = pingId.ToString(CultureInfo.InvariantCulture);

                return $"{report.Type}_{subTypeStr}_{pingIdStr}";
            }
            else
                return report.Type;
        }

        static string GetIncidentSourceLabelForMetrics(IncidentSource source)
        {
            switch (source)
            {
                case IncidentSource.GameProtocol:               return "GameProtocol";
                case IncidentSource.ServerGenerated:            return "ServerGenerated";
                case IncidentSource.SessionStartAbortTrailer:   return "HanshakeTrailer";
                case IncidentSource.HttpUpload:                 return "HttpUpload";
                case IncidentSource.OrphanHttpUpload:           return "OrphanHttpUpload";
                default:
                    return "Unknown";
            }
        }
    }

    class PlayerIncidentUploadStorageService
    {
        static PlayerIncidentUploadStorageService _instance;
        public const string PathPrefix = "IncidentUpload/v1/";
        S3BlobStorage _s3Storage;

        public static PlayerIncidentUploadStorageService GetInstance()
        {
            if (_instance == null)
            {
                BlobStorageOptions blobStorageOptions = RuntimeOptionsRegistry.Instance.GetCurrent<BlobStorageOptions>();
                S3BlobStorage s3Storage;
                if (blobStorageOptions.Backend != BlobStorageBackend.S3)
                    s3Storage = null;
                else
                    s3Storage = (S3BlobStorage)blobStorageOptions.CreatePrivateBlobStorage(PathPrefix);

                _instance = new PlayerIncidentUploadStorageService(s3Storage);
            }
            return _instance;
        }

        PlayerIncidentUploadStorageService(S3BlobStorage s3Storage)
        {
            _s3Storage = s3Storage;
        }

        /// <summary>
        /// Returns Bucket Key and Pre-signed upload url for a new incident upload. If upload is not supported, returns <c>(null, null)</c>.
        /// </summary>
        public (string IncidentDownloadBucketKey, string IncidentUploadUrl) TryCreateIncidentUploadUrl(EntityId playerId, string incidentId, int contentLength)
        {
            if (_s3Storage == null)
                return (null, null);

            // Format key such that:
            // * We can parse PlayerId back from it.
            //    * If we find a stray file in S3, we know whose it is
            // * We can parse IncidentID back from it.
            //    * If we find a stray file in S3, we know what incident it was without opening it for early rejection. Note
            //      that a malicious client can claim one ID in request and one in the serialized report. This is fine as the
            //      request ID is only used for early rejection.
            // * The URL is not predictable
            //   * The Signed URL should not be readable to the public internet. But just in case, let's make it unpredictable.

            (_, string playerIdValue) = playerId.GetKindValueStrings();
            string randomString = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));

            string path = $"{playerIdValue}_{incidentId}_{randomString}";
            string presignedUrl = _s3Storage.GetPresignedUrl(HttpVerb.PUT, path, expiresIn: TimeSpan.FromHours(1), contentLength: contentLength);
            string bucketKey = _s3Storage.GetEntryKeyName(path);

            return (bucketKey, presignedUrl);
        }

        public static bool TryParseKey(string key, out EntityId entityId, out string incidentId)
        {
            // Format {EntityIdValue)_{incident}_{randomHex16}
            string[] parts = key.Split('_', 3);
            if (parts.Length == 3)
            {
                string playerId = $"Player:{parts[0]}";
                if (EntityId.TryParseFromString(playerId, out EntityId validEntityId, out string _)
                    && PlayerIncidentUtil.IsValidIncidentId(parts[1])
                    && parts[2].Length == 16)
                {
                    entityId = validEntityId;
                    incidentId = parts[1];
                    return true;
                }
            }

            entityId = default;
            incidentId = null;
            return false;
        }

        /// <summary>
        /// Downloads an incident from the bucket key and deletes it. Returns null if incident doesn't exist
        /// or if downloading fails.
        /// </summary>
        public async Task<byte[]> TryDownloadAndDeleteIncidentAsync(IMetaLogger log, string bucketKey)
        {
            try
            {
                // Note that we don't need to check the content size before downloading. The
                // upload url has a fixed size.

                // Download
                using SegmentedIOBuffer buffer = new SegmentedIOBuffer(segmentSize: 16384);
                using (GetObjectResponse response = await _s3Storage.Client.GetObjectAsync(_s3Storage.BucketName, bucketKey).ConfigureAwait(false))
                {
                    if (response.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // Already consumed?
                        return null;
                    }
                    else if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
                    {
                        log.Error("Failed to fetch uploaded incident {BucketKey}", bucketKey);
                        return null;
                    }

                    using (Stream writer = new IOWriter(buffer).ConvertToStream())
                    {
                        await response.ResponseStream.CopyToAsync(writer).ConfigureAwait(false);
                    }
                }

                // Delete
                _ = await _s3Storage.Client.DeleteObjectAsync(_s3Storage.BucketName, bucketKey).ConfigureAwait(false);

                return buffer.ToArray();
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Already consumed
                return null;
            }
            catch (Exception ex)
            {
                log.Error("Error while downloading uploaded incident: {Error}", ex);
                return null;
            }
        }

        public async Task DeleteIncidentAsync(IMetaLogger log, string bucketKey)
        {
            try
            {
                _ = await _s3Storage.Client.DeleteObjectAsync(_s3Storage.BucketName, bucketKey).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.Warning("Error while deleting incident report: {Error}", ex);
            }
        }
    }
}
