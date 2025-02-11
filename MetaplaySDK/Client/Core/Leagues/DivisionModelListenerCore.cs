// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

namespace Metaplay.Core.League
{
    public interface IDivisionModelClientListenerCore
    {
        // \note IMPORTANT: If you change the parameters of an existing method here in this interface,
        //       you should leave the overload with the old parameters as [Obsolete("...", error: true)].
        //       This way, uses of the old method will get notified by the compiler, instead of the change
        //       going unnoticed due to the default implementations here.

        /// <summary>
        /// Called when the season concludes. The division will not accept score updates after this.
        /// </summary>
        void OnSeasonConcluded() { }
    }

    public interface IDivisionModelServerListenerCore
    {
        // \note IMPORTANT: If you change the parameters of an existing method here in this interface,
        //       you should leave the overload with the old parameters as [Obsolete("...", error: true)].
        //       This way, uses of the old method will get notified by the compiler, instead of the change
        //       going unnoticed due to the default implementations here.

        void OnSeasonDebugConcluded() { } // \todo remove? Just used for testing now.
        void OnSeasonDebugEnded() { }
    }
}
