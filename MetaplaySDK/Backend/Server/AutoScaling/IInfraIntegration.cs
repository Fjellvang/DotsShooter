// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using k8s;
using k8s.Models;
using Metaplay.Cloud;
using Metaplay.Cloud.Cluster;
using Metaplay.Cloud.Options;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using System;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Server.AutoScaling
{
    public interface IInfraIntegration : IMetaIntegrationConstructible<IInfraIntegration>
    {
        public Task SetReplicaCount(string shardName, int replicaCount);
    }

    public class KubernetesIntegration : IInfraIntegration
    {
        readonly Kubernetes  _kubernetesClient;
        readonly IMetaLogger _log;

        public KubernetesIntegration()
        {
            _log = MetaLogger.ForContext(typeof(KubernetesIntegration));
            try
            {
                var config = KubernetesClientConfiguration.InClusterConfig();

                _kubernetesClient = new Kubernetes(config);
            }
            catch (Exception ex)
            {
                // Not running in a k8s environment, do nothing...
                _log.Warning("Unable to find k8s config, {ex}", ex);
            }
        }

        public async Task SetReplicaCount(string shardName, int replicaCount)
        {
            if (_kubernetesClient == null)
                return;

            string kubernetesNamespace = RuntimeOptionsRegistry.Instance.GetCurrent<DeploymentOptions>().KubernetesNamespace;

            // Using a raw json string patch here as JsonPatch does not seem to work consistently in the cloud
            var patchStr = FormattableString.Invariant(
                $@"{{
    ""spec"": {{
        ""replicas"": {replicaCount}
    }}
}}");

            var patch = new V1Patch(patchStr, V1Patch.PatchType.MergePatch);

            _log.Info(Invariant($"Scaling gameservershards.gameservers.metaplay.io/v0/{kubernetesNamespace}/{shardName} to {replicaCount}"));
            var response = await _kubernetesClient.PatchNamespacedCustomObjectScaleAsync(
                patch,
                "gameservers.metaplay.io",
                "v0",
                kubernetesNamespace,
                "gameservershards",
                shardName);

            _log.Debug(Util.ObjectToStringInvariant(response));
        }
    }
}
