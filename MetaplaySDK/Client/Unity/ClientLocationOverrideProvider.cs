// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Player;
using System;

namespace Metaplay.Unity
{
    /// <summary>
    /// Utility for getting override location on Unity.
    /// </summary>
    public static class ClientLocationOverrideProvider
    {
#if UNITY_EDITOR
        /// <summary>
        /// Editor only internal hook.
        /// </summary>
        public static Func<PlayerLocation?> EditorHookGetOverrideLocation = null;
#endif

        public static PlayerLocation? GetOverrideLocation()
        {
#if UNITY_EDITOR
            if (EditorHookGetOverrideLocation != null)
                return EditorHookGetOverrideLocation();
#endif
            return null;
        }
    }
}
