// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Math;
using Metaplay.Core.Model;
using System;

namespace Metaplay.Core.Player
{
    [MetaSerializable]
    public struct PlayerLocation : IEquatable<PlayerLocation>
    {
        [MetaMember(1)] public CountryId Country { get; private set; }

        /// <summary>
        /// The 2-letter continent code for the continent/area, with possible values of:
        /// <list type="bullet">
        /// <item><c>null</c> if PlayerLocation created before MetaplaySDK R27.</item>
        /// <item>"AF" - Africa</item>
        /// <item>"AS" - Asia</item>
        /// <item>"EU" - Europe</item>
        /// <item>"NA" - North America</item>
        /// <item>"SA" - South America</item>
        /// <item>"OC" - Oceania</item>
        /// <item>"AN" - Antarctica</item>
        /// </list>
        /// </summary>
        [MetaMember(2)] public string ContinentCodeMaybe { get; private set; }
        /// <summary>
        /// Information about the city, if available.
        /// This may be unavailable if only a Country-precision MaxMind database was used (rather than City),
        /// or if city information was unavailable for the requested IP address.
        /// </summary>
        [MetaMember(3)] public CityInfo CityMaybe { get; private set; }
        /// <summary>
        /// Information about the coordinates, if available.
        /// This may be unavailable if only a Country-precision MaxMind database was used (rather than City),
        /// or if coordinate information was unavailable for the requested IP address.
        /// </summary>
        [MetaMember(4)] public CoordinatesInfo CoordinatesMaybe { get; private set; }

        public PlayerLocation(CountryId country, string continentCodeMaybe, CityInfo cityMaybe = null, CoordinatesInfo coordinatesMaybe = null)
        {
            Country = country;
            ContinentCodeMaybe = continentCodeMaybe;
            CityMaybe = cityMaybe;
            CoordinatesMaybe = coordinatesMaybe;
        }

        public static bool operator== (PlayerLocation a, PlayerLocation b)
        {
            return a.Country == b.Country
                && a.ContinentCodeMaybe == b.ContinentCodeMaybe
                && a.CityMaybe == b.CityMaybe
                && a.CoordinatesMaybe == b.CoordinatesMaybe;
        }
        public static bool operator!= (PlayerLocation a, PlayerLocation b) => !(a == b);
        public override bool Equals(object obj) => obj is PlayerLocation location && this == location;
        public bool Equals(PlayerLocation other) => this == other;
        public override int GetHashCode() => HashCode.Combine(Country, ContinentCodeMaybe, CityMaybe, CoordinatesMaybe);

    }

    [MetaSerializable]
    public struct CountryId
    {
        /// <summary>
        /// The ISO 3166-1 alpha-2 code.
        /// E.g. "FI" or "US"
        /// </summary>
        [MetaMember(1)] public string IsoCode { get; private set; }

        public CountryId(string isoCode)
        {
            IsoCode = isoCode ?? throw new ArgumentNullException(nameof(isoCode));
        }

        public static bool operator== (CountryId a, CountryId b)
        {
            return a.IsoCode == b.IsoCode;
        }
        public static bool operator!= (CountryId a, CountryId b) => !(a == b);
        public override bool Equals(object obj) => obj is CountryId countryId && this == countryId;
        public bool Equals(CountryId other) => this == other;
        public override int GetHashCode() => IsoCode?.GetHashCode() ?? 0;
    }

    [MetaSerializable]
    public class CityInfo : IEquatable<CityInfo>
    {
        /// <summary>
        /// GeoNameId for the city, as reported by MaxMind.
        /// <para>
        /// See:<br/>
        /// https://support.maxmind.com/hc/en-us/articles/4414877149467-IP-Geolocation-Data#h_01FRRNFD5Z5EWNCAXM6SZZ5H2C <br/>
        /// https://www.geonames.org/ <br/>
        /// </para>
        /// </summary>
        [MetaMember(1)] public long GeoNameId { get; private set; }
        /// <summary>
        /// The name of the city in in the English language, as reported by MaxMind.
        /// </summary>
        [MetaMember(2)] public string Name { get; private set; }

        CityInfo() { }
        public CityInfo(long geoNameId, string name)
        {
            GeoNameId = geoNameId;
            Name = name;
        }

        public bool Equals(CityInfo other)
        {
            if (ReferenceEquals(other, null))
                return false;

            return GeoNameId == other.GeoNameId &&
                   Name == other.Name;
        }
        public override bool Equals(object obj) => obj is CityInfo other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(GeoNameId, Name);
        public static bool operator ==(CityInfo left, CityInfo right) => Equals(left, right);
        public static bool operator !=(CityInfo left, CityInfo right) => !(left == right);
    }

    /// <summary>
    /// Approximate coordinates for a location, as reported by MaxMind.
    /// <see cref="AccuracyRadiusKilometers"/> (if available) is MaxMind's 67% confidence radius.
    /// </summary>
    [MetaSerializable]
    public class CoordinatesInfo : IEquatable<CoordinatesInfo>
    {
        [MetaMember(1)] public F32 Latitude { get; private set; }
        [MetaMember(2)] public F32 Longitude { get; private set; }
        [MetaMember(3)] public int? AccuracyRadiusKilometers { get; private set; }

        CoordinatesInfo() { }
        public CoordinatesInfo(F32 latitude, F32 longitude, int? accuracyRadiusKilometers)
        {
            Latitude = latitude;
            Longitude = longitude;
            AccuracyRadiusKilometers = accuracyRadiusKilometers;
        }

        public bool Equals(CoordinatesInfo other)
        {
            if (ReferenceEquals(other, null))
                return false;

            return Latitude == other.Latitude &&
                   Longitude == other.Longitude &&
                   AccuracyRadiusKilometers == other.AccuracyRadiusKilometers;
        }
        public override bool Equals(object obj) => obj is CoordinatesInfo other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Latitude, Longitude, AccuracyRadiusKilometers);
        public static bool operator ==(CoordinatesInfo left, CoordinatesInfo right) => Equals(left, right);
        public static bool operator !=(CoordinatesInfo left, CoordinatesInfo right) => !(left == right);
    }
}
