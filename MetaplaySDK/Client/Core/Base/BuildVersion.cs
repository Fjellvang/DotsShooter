// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Model;
using System;

namespace Metaplay.Core
{
    [MetaSerializable]
    public struct BuildVersion : IEquatable<BuildVersion>
    {
        [MetaMember(1)] public string Version;
        [MetaMember(2)] public string BuildNumber;
        [MetaMember(3)] public string CommitId;

        public BuildVersion(string version, string buildNumber, string commitId)
        {
            Version = version;
            BuildNumber = buildNumber;
            CommitId = commitId;
        }

        public bool Equals(BuildVersion other)
        {
            return Version == other.Version
                && BuildNumber == other.BuildNumber
                && CommitId == other.CommitId;
        }

        public override bool Equals(object obj) => obj is BuildVersion key && Equals(key);
        public override int GetHashCode() => Util.CombineHashCode(Version?.GetHashCode() ?? 0, BuildNumber?.GetHashCode() ?? 0, CommitId?.GetHashCode() ?? 0);
    }
}
