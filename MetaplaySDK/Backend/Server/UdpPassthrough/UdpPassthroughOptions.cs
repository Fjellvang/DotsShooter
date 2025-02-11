// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Metaplay.Server.UdpPassthrough
{
    [RuntimeOptions("UdpPassthrough", isStatic: true, "UDP packet passthrough configuration options.")]
    class UdpPassthroughOptions : RuntimeOptionsBase
    {
        [MetaDescription("Is UDP passthrough enabled.")]
        public bool Enabled { get; private set; } = false;

        [MetaDescription("The domain of the publicly visible gateway to the server. This is the server (in direct connect mode) or a loadbalancer (in LB mode). Chosen automatically.")]
        public string PublicFullyQualifiedDomainName { get; private set; }

        [MetaDescription("First port in the the public gateway port range. Only used in cloud.")]
        public int GatewayPortRangeStart { get; private set; }

        [MetaDescription("Last (inclusive) port in the the public gateway port range. Only used in cloud.")]
        public int GatewayPortRangeEnd { get; private set; }

        [MetaDescription("Server port where the server listens locally.")]
        public int LocalServerPort { get; private set; }

        [MetaDescription("If set, a debug server is bound to the port instead of the game actor. Send 'help' for more details.")]
        public bool UseDebugServer { get; private set; }

        [MetaDescription("Use an externally allocated IP. Only used in cloud.")]
        public bool UseCloudPublicIp { get; private set; }

        [MetaDescription("The public IP allocated for this node in the cloud. Only used in cloud.")]
        [EnvironmentVariable("Metaplay_Deployment__PublicIpv4")] // Forward compatibility
        public string CloudPublicIpv4 { get; private set; }

        [MetaDescription("The usable port of the public IP allocated for this node in the cloud. Only used in cloud.")]
        public int CloudPublicIpv4Port { get; private set; }

        public override Task OnLoadedAsync()
        {
            // Normalize
            if (string.IsNullOrEmpty(PublicFullyQualifiedDomainName))
                PublicFullyQualifiedDomainName = null;
            if (string.IsNullOrEmpty(CloudPublicIpv4))
                CloudPublicIpv4 = null;

            if (Enabled)
            {
                if (UseCloudPublicIp)
                {
                    // On cloud with explicitly allocated resources we just pass them through
                    // \note: CloudPublicIpv4 can be null if this node has no public IP

                    if ((CloudPublicIpv4 == null) != (CloudPublicIpv4Port == 0))
                        throw new InvalidOperationException("Both or none (UdpPassthrough:CloudPublicIpv4, UdpPassthrough:CloudPublicIpv4Port) must be set when UdpPassthrough:UseCloudPublicIp is true.");

                    // If an IPv4 address is specified, check that is actually is a valid address.
                    if (CloudPublicIpv4 != null)
                    {
                        if (!IPAddress.TryParse(CloudPublicIpv4, out IPAddress ipV4Address))
                            throw new InvalidOperationException($"UdpPassthrough:CloudPublicIpv4 is not a valid IP address: \"{CloudPublicIpv4}\".");
                        if (ipV4Address.AddressFamily != AddressFamily.InterNetwork)
                            throw new InvalidOperationException($"UdpPassthrough:CloudPublicIpv4 is not an IPv4 address: \"{CloudPublicIpv4}\" is for {ipV4Address.AddressFamily}.");
                    }

                    // \note: We set this to some guaranteed wrong value to detect if we accidentally use this
                    PublicFullyQualifiedDomainName = "using-per-node-cloud-ip.invalid";
                }
                else if (IsLocalEnvironment)
                {
                    // On local environment, default to local hostname but allow overriding
                    if (string.IsNullOrEmpty(PublicFullyQualifiedDomainName))
                        PublicFullyQualifiedDomainName = GetLocalhostAddress();

                    if (LocalServerPort == 0)
                        throw new InvalidOperationException("UdpPassthrough:LocalServerPort must be set manually in Local environments if passthrough is enabled.");
                }
                else
                {
                    // On cloud, the infra injects the fields
                    if (string.IsNullOrEmpty(PublicFullyQualifiedDomainName))
                        throw new InvalidOperationException("UdpPassthrough:PublicFullyQualifiedDomainName must be defined by if passthrough is enabled. Is udp passthrough enabled in infra modules?");

                    // Cloud infra should allocate and set port range too.
                    if (GatewayPortRangeStart == 0)
                        throw new InvalidOperationException("UdpPassthrough:GatewayPortRangeStart must be set if passthrough is enabled. Is udp passthrough enabled in infra modules?");
                    if (GatewayPortRangeEnd == 0)
                        throw new InvalidOperationException("UdpPassthrough:GatewayPortRangeEnd must be set if passthrough is enabled. Is udp passthrough enabled in infra modules?");
                    if (LocalServerPort == 0)
                        throw new InvalidOperationException("UdpPassthrough:LocalServerPort must be set if passthrough is enabled. Is udp passthrough enabled in infra modules?");
                }
            }
            return Task.CompletedTask;
        }

        static string GetLocalhostAddress()
        {
            // First IPv4 address, or 127.0.0.1
            IPHostEntry entry = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress addr in entry.AddressList)
            {
                if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return addr.ToString();
            }
            return "127.0.0.1";
        }
    }
}
