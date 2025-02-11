// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;

namespace Metaplay.Server.TelemetryManager
{
    [RuntimeOptions("Telemetry", isStatic: true, sectionDescription: "Configuration of the telemetry services. Calls home to Metaplay's central server to update server status in the portal and receive useful messages about available upgades and such.")]
    public class TelemetryOptions : RuntimeOptionsBase
    {
        [MetaDescription("Are the telemetry events enabled?")]
        public bool Enabled { get; private set; } = true;
    }
}
