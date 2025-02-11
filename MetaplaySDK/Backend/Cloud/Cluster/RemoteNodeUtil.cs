// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Akka.Actor;
using Metaplay.Core;
using static System.FormattableString;

namespace Metaplay.Cloud.Cluster
{
    public static class RemoteNodeUtil
    {
        /// <summary>
        /// Creates an <see cref="ActorSelection"/> for the given <paramref name="actorPath"/> on the remote node at <paramref name="address"/>.
        /// </summary>
        public static ActorSelection GetRemoteActorSelection(ActorSystem actorSystem, ClusterNodeAddress address, string actorPath)
        {
            string actorSystemName = MetaplayCore.Options.ProjectName;
            return actorSystem.ActorSelection(Invariant($"akka.tcp://{actorSystemName}@{address.HostName}:{address.Port}/user/{actorPath}"));
        }
    }
}
