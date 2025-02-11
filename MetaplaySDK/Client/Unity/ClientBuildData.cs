// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;

namespace Metaplay.Unity
{
    /// <summary>
    /// Base class for data captured during client build and exposed to the built client.
    /// </summary>
    ///
    /// The <see cref="GenerateClientBuildData()"/> build script will generate and inject
    /// a class that derives from this class and overrides the properties with values given
    /// as input to the build via command-line arguments or environment variables. The generated
    /// class can be accessed via `MetaplaySDK.GetClientBuildData()`.
    ///
    /// Additionally, an integration can derive from this class to add more properties populated
    /// during the build. In that case the generated class will derive from the integration class
    /// and also override integration class properties when given as build inputs.
    ///
    /// The mapping from build inputs to properties is done as follows:
    /// Command-line argument `-Metaplay<PropertyName>=<value>`, for example `-MetaplayCommitId=xxx`.
    /// Environment variable `METAPLAY_<PROPERTYNAME>=<value>`, for example `METAPLAY_COMMITID=xxx`.
    public class ClientBuildData : IMetaIntegrationSingleton<ClientBuildData>
    {
        public virtual string CommitId => "undefined";
        public virtual string BuildNumber => "undefined";
    }
}
