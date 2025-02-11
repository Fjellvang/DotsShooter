// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using MaxMind.GeoIP2;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Metaplay.Cloud.Services.Geolocation
{
    /// <summary>
    /// Holds the currently-active in-memory copy of a geolocation database.
    /// </summary>
    internal class GeolocationResidentStorage : IGeolocationUpdateDestination
    {
        /// <summary>
        /// Like <see cref="GeolocationDatabase"/>, but holds an instantiated <see cref="DatabaseReader"/>
        /// instead of the .mmdb payload.
        /// </summary>
        public class ResidentDatabase
        {
            public readonly GeolocationDatabaseMetadata Metadata;
            public readonly DatabaseReader              Reader;

            public ResidentDatabase(GeolocationDatabaseMetadata metadata, DatabaseReader reader)
            {
                Metadata = metadata;
                Reader = reader ?? throw new ArgumentNullException(nameof(reader));
            }
        }

        volatile ResidentDatabase   _residentDatabaseMaybe;
        public ResidentDatabase     ResidentDatabaseMaybe => _residentDatabaseMaybe;

        public GeolocationResidentStorage(GeolocationDatabase? initialDatabase)
        {
            if (initialDatabase.HasValue)
                SetResidentDatabase(initialDatabase.Value);
        }

        void SetResidentDatabase(GeolocationDatabase database)
        {
            // \note Ideally we should Dispose the old DatabaseReader (if any).
            //       But that'd need some locking here and around the usage of the reader, complicating things a bit.
            //       And in practice, in our use case, DatabaseReader doesn't have anything to dispose,
            //       as it's not created from a memory-mapped file.
            _residentDatabaseMaybe = new ResidentDatabase(
                database.Metadata,
                database.CreateMaxMindDatabaseReader());
        }

        #region IGeolocationUpdateDestination

        public Task StoreDatabaseAsync(GeolocationOptions options, GeolocationDatabase database)
        {
            SetResidentDatabase(database);
            return Task.CompletedTask;
        }

        /// <summary> When geolocation disabled: unset resident database in order to disable lookups. </summary>
        public Task OnGeolocationDisabledAsync(GeolocationOptions options)
        {
            _residentDatabaseMaybe = null;
            return Task.CompletedTask;
        }

        public Task<GeolocationDatabaseMetadata?> TryFetchMetadataAsync(GeolocationOptions options, CancellationToken ct)
        {
            GeolocationDatabaseMetadata? metadata = ResidentDatabaseMaybe?.Metadata;

            // \note In this implementation, we store at most one resident database (and its metadata) at a time.
            //       Therefore, changing options.MaxMindDatabaseId (from Country to City or vice versa) replaces the database.
            //       We detect this based on the stored DatabaseId, and report missing database in case we don't
            //       have the wanted one.
            //       Note that this is different from GeolocationReplicaStorage, which stores different databases
            //       in different blobs for technical reasons.
            if (metadata.HasValue && metadata.Value.DatabaseId != options.MaxMindDatabaseId)
                metadata = null;

            return Task.FromResult(metadata);
        }

        #endregion
    }
}
