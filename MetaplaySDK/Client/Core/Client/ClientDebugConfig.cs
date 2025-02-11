// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Model;

namespace Metaplay.Core.Client
{
    [MetaSerializable]
    public class EntityDebugConfig
    {
        [MetaMember(1)] public bool                ClientConsistencyChecks { get; set; }
        [MetaMember(2)] public bool                ServerConsistencyChecks { get; set; }
        [MetaMember(3)] public ChecksumGranularity ChecksumGranularity     { get; set; }

        public static EntityDebugConfig EnableAll() => new EntityDebugConfig()
        {
            ClientConsistencyChecks = true,
            ServerConsistencyChecks = true,
            ChecksumGranularity     = ChecksumGranularity.PerOperation,
        };
    }
}
