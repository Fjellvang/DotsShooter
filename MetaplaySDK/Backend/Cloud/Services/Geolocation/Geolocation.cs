// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using MaxMind.GeoIP2.Responses;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using Metaplay.Core.Math;
using Metaplay.Core.Player;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Metaplay.Cloud.Services.Geolocation
{
    /// <summary>
    /// Main public interface to IP geolocation.
    /// </summary>
    public class Geolocation : IAsyncDisposable
    {
        public static Geolocation Instance { get; private set; }

        /// <summary> How often to check whether there are updates in the origin, i.e. MaxMind's servers. </summary>
        static readonly TimeSpan OriginUpdateCheckInterval  = TimeSpan.FromHours(1);
        /// <summary> How often to check whether there are updates in the replica storage (e.g. S3). </summary>
        static readonly TimeSpan ReplicaUpdateCheckInterval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Initialize the geolocation utility default instance (<see cref="Instance"/>).
        /// </summary>
        /// <param name="isLeader">
        /// Should be true for exactly one node in the cluster.
        /// The leader will be downloading updates directly from MaxMind and storing to a replica storage,
        /// which the other nodes will access.
        /// </param>
        public static async Task InitializeAsync(IBlobStorage replicaBlobStorage, bool isLeader)
        {
            if (Instance != null)
                throw new InvalidOperationException("Already initialized");

            IMetaLogger                 logger              = MetaLogger.ForContext<Geolocation>();
            GeolocationOptions          options             = RuntimeOptionsRegistry.Instance.GetCurrent<GeolocationOptions>();
            GeolocationReplicaStorage   replicaStorage      = new GeolocationReplicaStorage(replicaBlobStorage);
            GeolocationDatabase?        initialDatabase     = await TryGetInitialDatabaseAsync(logger, options, replicaStorage, CancellationToken.None).ConfigureAwait(false);
            GeolocationResidentStorage  residentStorage     = new GeolocationResidentStorage(initialDatabase);
            List<GeolocationUpdater>    updaters            = new List<GeolocationUpdater>();

            // On leader, start an updater which gets updates from MaxMind's server and puts them to the replica storage.
            if (isLeader)
            {
                GeolocationUpdateSourceMaxMind sourceMaxMind = new GeolocationUpdateSourceMaxMind();

                updaters.Add(new GeolocationUpdater(
                    logger:                 MetaLogger.ForContext("GeolocationUpdaterFromOrigin"),
                    source:                 sourceMaxMind,
                    destination:            replicaStorage,
                    updateCheckInterval:    OriginUpdateCheckInterval));
            }

            // On all nodes, start an updater which gets updates from the replica storage and puts them to the resident storage.
            // \note This is also done on leader. Thus, on leader, an update is first copied from MaxMind to replica,
            //       and then from replica to resident. We could remove that extra fetch from the replica,
            //       but this way code is simpler and gets exercised better also on single-node setups.
            updaters.Add(new GeolocationUpdater(
                logger:                 MetaLogger.ForContext("GeolocationUpdaterFromReplica"),
                source:                 replicaStorage,
                destination:            residentStorage,
                updateCheckInterval:    ReplicaUpdateCheckInterval));

            GeolocationMetricsReporter metricsReporter = new GeolocationMetricsReporter(residentStorage);

            Instance = new Geolocation(residentStorage, updaters, metricsReporter);
        }

        public static async Task DeinitializeAsync()
        {
            if (Instance == null)
                throw new InvalidOperationException($"Cannot deinitialize Geolocation as it isn't currently initialized");
            await Instance.DisposeAsync();
            Instance = null;
        }

        public async ValueTask DisposeAsync()
        {
            List<Task> disposeTasks = new();
            foreach (GeolocationUpdater updater in _updaters)
                disposeTasks.Add(updater.DisposeAsync().AsTask());
            disposeTasks.Add(_metricsReporter.DisposeAsync().AsTask());

            await Task.WhenAll(disposeTasks);
        }

        GeolocationResidentStorage  _residentStorage;
        List<GeolocationUpdater>    _updaters;
        GeolocationMetricsReporter  _metricsReporter;

        Geolocation(GeolocationResidentStorage residentStorage, List<GeolocationUpdater> updaters, GeolocationMetricsReporter metricsReporter)
        {
            _residentStorage = residentStorage ?? throw new ArgumentNullException(nameof(residentStorage));
            _updaters = updaters ?? throw new ArgumentNullException(nameof(updaters));
            _metricsReporter = metricsReporter ?? throw new ArgumentNullException(nameof(metricsReporter));
        }

        /// <summary>
        /// Get location info corresponding to a player's ip address, if available.
        /// The info may be unavailable for various reasons:<br/>
        /// - No info found for the IP address in the geolocation database<br/>
        /// - Geolocation database hasn't been downloaded yet<br/>
        /// - Geolocation is disabled in <see cref="GeolocationOptions"/><br/>
        /// - Geolocation database is over 30 days old
        ///   (probably due to updates being disabled due to <see cref="GeolocationOptions.MaxMindLicenseKeyPath"/> not being set)<br/>
        /// <para>
        /// Note that even a non-null PlayerLocation result may contain
        /// partial information; in particular, <see cref="PlayerLocation.CityMaybe"/>
        /// and <see cref="PlayerLocation.CoordinatesMaybe"/> are not guaranteed
        /// to be available, depending on what kind of MaxMind database
        /// the server is using, and what info is available there for the IP address.
        /// </para>
        /// </summary>
        public PlayerLocation? TryGetPlayerLocation(IPAddress ipAddress)
        {
            GeolocationResidentStorage.ResidentDatabase database = TryGetResidentDatabase();
            if (database == null)
                return null;

            // Get country and possibly city information, based on what kind of
            // MaxMind database we have available.
            // - Country info is always available (in both Country and City databases).
            // - City info is available in City database but not in Country database.
            // Note that AbstractCountryResponse is a base class of AbstractCityResponse,
            // so countryResponse and cityResponseMaybe may end up referring to the same object.

            AbstractCountryResponse countryResponse;
            AbstractCityResponse cityResponseMaybe;

            // \note We must use the correct method, either TryCity or TryCountry,
            //       depending on which kind of database it is.
            //       MaxMind.GeoIP2.DatabaseReader enforces this.
            if (database.Metadata.DatabaseId == GeolocationDatabaseId.GeoLite2City)
            {
                if (!database.Reader.TryCity(ipAddress, out CityResponse response))
                    return null;

                countryResponse = response;
                cityResponseMaybe = response;
            }
            else if (database.Metadata.DatabaseId == GeolocationDatabaseId.GeoLite2Country)
            {
                if (!database.Reader.TryCountry(ipAddress, out CountryResponse response))
                    return null;

                countryResponse = response;
                cityResponseMaybe = null;
            }
            else
                return null;

            // We want country info at minimum. If we don't have that, we don't know the location at all.
            if (countryResponse.Country.IsoCode == null)
                return null;

            // CityInfo and CoordinatesInfo both come from cityResponseMaybe (if available).
            CityInfo cityMaybe = null;
            CoordinatesInfo coordinatesMaybe = null;
            if (cityResponseMaybe != null)
            {
                if (cityResponseMaybe.City.GeoNameId.HasValue && cityResponseMaybe.City.Name != null)
                    cityMaybe = new CityInfo(cityResponseMaybe.City.GeoNameId.Value, cityResponseMaybe.City.Name);

                if (cityResponseMaybe.Location.HasCoordinates)
                {
                    coordinatesMaybe = new CoordinatesInfo(
                        latitude: F32.FromDouble(cityResponseMaybe.Location.Latitude.Value),
                        longitude: F32.FromDouble(cityResponseMaybe.Location.Longitude.Value),
                        accuracyRadiusKilometers: cityResponseMaybe.Location.AccuracyRadius /* \note Nullable */);
                }
            }

            return new PlayerLocation(
                new CountryId(countryResponse.Country.IsoCode),
                continentCodeMaybe: countryResponse.Continent.Code,
                cityMaybe,
                coordinatesMaybe);
        }

        /// <summary>
        /// Helper to get the current <see cref="GeolocationResidentStorage.ResidentDatabase"/> if it's available and not too old.
        /// </summary>
        GeolocationResidentStorage.ResidentDatabase TryGetResidentDatabase()
        {
            GeolocationResidentStorage.ResidentDatabase resident = _residentStorage.ResidentDatabaseMaybe;
            if (resident == null)
                return null;

            // \note According to license, don't use too old database.
            //       Shouldn't happen if things are properly configured;
            //       GeolocationUpdater should take care of keeping it up to date.
            if (DateTime.UtcNow > resident.Metadata.BuildDate + TimeSpan.FromDays(30))
                return null;

            return resident;
        }

        static async Task<GeolocationDatabase?> TryGetInitialDatabaseAsync(IMetaLogger logger, GeolocationOptions options, GeolocationReplicaStorage replicaStorage, CancellationToken ct)
        {
            GeolocationDatabase? initialDatabase;

            if (options.Enabled)
            {
                initialDatabase = await replicaStorage.TryFetchDatabaseAsync(options, ct).ConfigureAwait(false);

                if (initialDatabase.HasValue)
                    logger.Information("Initial geolocation database found from replica. Build date: {BuildDate}", initialDatabase.Value.Metadata.BuildDate);
                else
                    logger.Information("Initial geolocation database not available in replica");
            }
            else
            {
                initialDatabase = null;
                logger.Information("Geolocation is disabled, not fetching initial database");
            }

            return initialDatabase;
        }
    }
}
