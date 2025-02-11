// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using System;

namespace Metaplay.Server.AutoScaling
{
    [RuntimeOptions("AutoScaling", isStatic: false, "Configuration options for the autoscaling system.")]
    public class AutoScalingRuntimeOptions : RuntimeOptionsBase
    {
        [MetaDescription("Whether AutoScaling is enabled.")]
        public bool Enabled { get; set; } = false;

        [MetaDescription("The interval of which the state is evaluated at.")]
        public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromMilliseconds(5000);

        [MetaDescription("The duration a node won't be considered for draining after being started.")]
        public TimeSpan NodeStartGracePeriod { get; set; } = TimeSpan.FromMilliseconds(10_000);

        [MetaDescription("The duration a node has to have no work for, before starting to drain and scale down a node.")]
        public TimeSpan MinimumNotEnoughWorkDurationToDrainNode { get; set; } = TimeSpan.FromMilliseconds(10_000);

        [MetaDescription("The duration a node has to be drained for before it is shutdown.")]
        public TimeSpan MinimumDrainDurationToShutdownNode { get; set; } = TimeSpan.FromMilliseconds(300_000);
    }
}
