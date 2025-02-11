// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using Metaplay.Core.Serialization;
using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Cloud.Options
{
    /// <summary>
    /// Version used by deployments (infrastructure and Helm charts). Of the form 'x.y.z' or 'x.y.z-label'.
    /// </summary>
    public struct DeploymentVersion : IEquatable<DeploymentVersion>, IComparable<DeploymentVersion>, IComparable
    {
        static readonly Regex s_validVersionRegex = new Regex("^([0-9]+)\\.([0-9]+)\\.([0-9]+)(-.+)?$", RegexOptions.Compiled);

        public readonly int     Major;
        public readonly int     Minor;
        public readonly int     Patch;
        public readonly string  Label;

        public DeploymentVersion(int major, int minor, int patch, string label)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Label = label;
        }

        public static DeploymentVersion ParseFromString(string str)
        {
            Match match = s_validVersionRegex.Match(str);
            if (!match.Success)
                throw new InvalidOperationException($"Invalid version format: '{str}', expecting 'x.y.z' or 'x.y.z-label'");

            int major = ParseVersionInt(match.Groups[1].Value);
            int minor = ParseVersionInt(match.Groups[2].Value);
            int patch = ParseVersionInt(match.Groups[3].Value);
            string label = match.Groups[4].Value;
            return new DeploymentVersion(major, minor, patch, label.Length > 0 ? label.Substring(1) : null);
        }

        static int ParseVersionInt(string str)
        {
            // Don't allow numbers starting with '0' (except '0' itself)
            if (str.StartsWith("0", StringComparison.Ordinal) && str != "0")
                throw new InvalidOperationException($"Octal numbers not allowed!");

            return int.Parse(str, CultureInfo.InvariantCulture.NumberFormat);
        }

        public static bool operator ==(DeploymentVersion a, DeploymentVersion b) => a.CompareTo(b) == 0;
        public static bool operator !=(DeploymentVersion a, DeploymentVersion b) => a.CompareTo(b) != 0;
        public static bool operator < (DeploymentVersion a, DeploymentVersion b) => a.CompareTo(b) < 0;
        public static bool operator > (DeploymentVersion a, DeploymentVersion b) => a.CompareTo(b) > 0;

        public bool Equals(DeploymentVersion other) => this == other;

        public int CompareTo(DeploymentVersion other)
        {
            if (Major < other.Major)
                return -1;
            else if (Major > other.Major)
                return +1;
            else if (Minor < other.Minor)
                return -1;
            else if (Minor > other.Minor)
                return +1;
            else if (Patch < other.Patch)
                return -1;
            else if (Patch > other.Patch)
                return +1;
            else // full match
                return 0;
        }

        public override bool Equals(object obj) => obj is DeploymentVersion other ? CompareTo(other) == 0 : false;

        public override string ToString() => (Label != null) ? Invariant($"{Major}.{Minor}.{Patch}-{Label}") : Invariant($"{Major}.{Minor}.{Patch}");

        public override int GetHashCode() => Util.CombineHashCode(Major.GetHashCode(), Minor.GetHashCode(), Patch.GetHashCode());

        int IComparable.CompareTo(object obj) => (obj is DeploymentVersion other) ? CompareTo(other) : 1;
    }

    /// <summary>
    /// Information about the deployment where the server is being run, mainly for informational purposes.
    /// </summary>
    [RuntimeOptions("Deployment", isStatic: true, "Configuration options for server deployment.")]
    public class DeploymentOptions : RuntimeOptionsBase
    {
        static readonly DeploymentVersion MinInfraVersion = new DeploymentVersion(0, 2, 9, null);
        static readonly DeploymentVersion MinChartVersion = new DeploymentVersion(0, 6, 3, null);
        public static DeploymentVersion CurrentMetaplayVersion => new DeploymentVersion(31, 1, 0, null); // add "pre" label in develop when between releases, "null" otherwise

        // Expose some versioning info
        [MetaDescription("The Metaplay SDK version.")]
        public DeploymentVersion    MetaplayVersion     => CurrentMetaplayVersion;
        [MetaDescription("The server build number. This value is populated by the CI build system.")]
        public string               BuildNumber         => CloudCoreVersion.BuildNumber;
        [MetaDescription("The ID or hash of the commit from which the server was built. This value is populated by the CI build system.")]
        public string               CommitId            => CloudCoreVersion.CommitId;
        [MetaDescription("Computed hash of all the serializable types that can be sent over the network between the client and the server.")]
        public uint                 FullProtocolHash    => MetaSerializerTypeRegistry.Instance.FullProtocolHash;

        // Versions passed in from Infra and Helm chart
        [MetaDescription("The version of the infrastructure module used to provision the cloud resources. This value is populated by the CI build system.")]
        public string   InfrastructureVersion   { get; private set; }           // Format: 'x.y.z' or 'x.y.z-extra'
        [MetaDescription("The version of metaplay-gameserver Helm chart. This value is populated by the CI build system.")]
        public string   ChartVersion            { get; private set; }           // Format: 'x.y.z' or 'x.y.z-extra'
        [MetaDescription("The minimum Metaplay SDK version as required by the infrastructure. This value is populated by the CI build system.")]
        public string   RequiredMetaplayVersion { get; private set; }           // Format: 'x.y.z' or 'x.y.z-extra'

        // Public endpoints
        [MetaDescription("The URI to the LiveOps Dashboard, if present in the environment.")]
        public string   AdminUri                { get; private set; } = IsLocalEnvironment ? "http://localhost:5551/" : null;
        [MetaDescription("The URI to the admin API (eg, for uploading game configs).")]
        public string   ApiUri                  { get; private set; } = IsLocalEnvironment ? "http://localhost:5550/api/" : null;

        // Grafana integration
        [MetaDescription("The URI to the Grafana dashboard.")]
        public string   GrafanaUri              { get; private set; } = null;
        [MetaDescription("The Kubernetes namespace where the game server is located.")]
        public string   KubernetesNamespace     { get; private set; } = null;

        // Public IP
        /// <summary> The public IPv4 address of the node or null if node has no public IP. </summary>
        [MetaDescription("The public IPv4 address of the node or null if node has no public IP.")]
        [EnvironmentVariable("Metaplay_UdpPassthrough__CloudPublicIpv4")] // Backward compatibility
        public string   PublicIpv4              { get; private set; } = null;

        public override Task OnLoadedAsync()
        {
            // Check infrastructure version compatibility (if specified)
            if (InfrastructureVersion != null)
            {
                DeploymentVersion infraVersion = DeploymentVersion.ParseFromString(InfrastructureVersion);
                if (infraVersion < MinInfraVersion)
                {
                    throw new InvalidOperationException(
                        $"Infrastructure version ({infraVersion}) is too old, minimum required is {MinInfraVersion}. " +
                        $"Please update the infrastructure. " +
                        $"See https://docs.metaplay.io/miscellaneous/sdk-updates/compatibility.html for details.");
                }
            }

            // Check chart version compatibility (if specified)
            if (ChartVersion != null)
            {
                DeploymentVersion chartVersion = DeploymentVersion.ParseFromString(ChartVersion);
                if (chartVersion < MinChartVersion)
                {
                    throw new InvalidOperationException(
                        $"Metaplay-gameserver Helm chart version ({chartVersion}) is too old, minimum required is {MinChartVersion}. " +
                        $"Please use a newer chart version in your deployment helm values (usually a yaml file). " +
                        $"See https://docs.metaplay.io/miscellaneous/sdk-updates/compatibility.html for details.");
                }
            }

            // Check supported releases (by infra) version compatibility
            if (!string.IsNullOrEmpty(RequiredMetaplayVersion))
            {
                DeploymentVersion requiredMetaplayVersion = DeploymentVersion.ParseFromString(RequiredMetaplayVersion);
                if (MetaplayVersion < requiredMetaplayVersion)
                {
                    throw new InvalidOperationException(
                        $"Infrastructure requires Metaplay version {RequiredMetaplayVersion} or later, but this GameServer uses {MetaplayVersion}. " +
                        $"Please update the GameServer to use a newer Metaplay version. " +
                        $"See https://docs.metaplay.io/miscellaneous/sdk-updates/compatibility.html for details.");
                }
            }

            // Check that GrafanaUri is valid
            if (!string.IsNullOrEmpty((GrafanaUri)))
            {
                if (!Uri.IsWellFormedUriString(GrafanaUri, UriKind.Absolute))
                {
                    throw new InvalidOperationException($"GrafanaUri is not well formatted");
                }

                if (!GrafanaUri.EndsWith("/", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"GrafanaUri must end with a '/'");
                }

                Uri grafanaUri = new Uri(GrafanaUri);
                if (grafanaUri.Scheme != Uri.UriSchemeHttp && grafanaUri.Scheme != Uri.UriSchemeHttps)
                {
                    throw new InvalidOperationException($"GrafanaUri must use http or https");
                }
            }

            // Check if public IPv4 address is specified, check that is actually is a valid address.
            if (PublicIpv4 != null)
            {
                if (!IPAddress.TryParse(PublicIpv4, out IPAddress ipV4Address))
                    throw new InvalidOperationException($"Deployment:PublicIpv4 is not a valid IP address: \"{PublicIpv4}\".");
                if (ipV4Address.AddressFamily != AddressFamily.InterNetwork)
                    throw new InvalidOperationException($"Deployment:PublicIpv4 is not an IPv4 address: \"{PublicIpv4}\" is for {ipV4Address.AddressFamily}.");
            }

            return Task.CompletedTask;
        }
    }
}
