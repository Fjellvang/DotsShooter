// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;

namespace Metaplay.Server
{
    [RuntimeOptions("GameTime", isStatic: false, "Configuration options for game time skipping.")]
    public class GameTimeOptions : RuntimeOptionsBase
    {
        [MetaDescription("Whether game time skipping is enabled.")]
        public bool EnableTimeSkipping { get; set; } = IsLocalEnvironment || IsDevelopmentEnvironment;
    }
}
