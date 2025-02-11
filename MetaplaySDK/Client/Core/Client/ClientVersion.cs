// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Model;

namespace Metaplay.Core.Client
{
    /// <summary>
    /// Version identifier of the client, configured in GlobalOptions and communicated to server on client login.
    /// </summary>
    [MetaSerializable]
    public readonly struct ClientVersion
    {
        // The logic version of the client. A client is always at a single logic version, the server can support a range
        // of versions.
        [MetaMember(1)] public readonly int LogicVersion;
        // The patch (hotfix) version of the client of a given logic version. Clients of different patch versions of the
        // same logic version can not differ in their logic, so this is used to track client updates that don't involve
        // changes in the game logic. Incrementing the patch version number when releasing client hotfixes is primarily
        // useful for the purpose of being able to conveniently force updates to hotfix releases from the dashboard.
        // The intention is that whenever logic version is bumped the patch version is reset to 0.
        [MetaMember(2)] public readonly int Patch;

        [MetaDeserializationConstructor]
        public ClientVersion(int logicVersion, int patch)
        {
            LogicVersion = logicVersion;
            Patch     = patch;
        }
    }
}
