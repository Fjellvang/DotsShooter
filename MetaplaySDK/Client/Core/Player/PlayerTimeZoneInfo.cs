// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Model;
using System;

namespace Metaplay.Core.Player
{
    /// <summary>
    /// Info about a player's time zone.
    /// </summary>
    /// <remarks>
    /// Doesn't contain comprehensive information about a time zone, such as daylight saving time adjustments.
    /// Should be good enough for short-term usage, e.g. per session.
    /// Shouldn't be assumed to be fresh, in case player's actual time zone or offset changes.
    /// </remarks>
    [MetaSerializable]
    public class PlayerTimeZoneInfo
    {
        [MetaMember(1)] public MetaDuration CurrentUtcOffset { get; set; }

        public PlayerTimeZoneInfo() {}
        public PlayerTimeZoneInfo(MetaDuration currentUtcOffset)
        {
            CurrentUtcOffset = currentUtcOffset;
        }

        public PlayerTimeZoneInfo GetCorrected()
        {
            MetaDuration correctedCurrentUtcOffset = Util.Clamp(CurrentUtcOffset, MinimumUtcOffset, MaximumUtcOffset);
            return new PlayerTimeZoneInfo(correctedCurrentUtcOffset);
        }

        // \note We restrict UTC offset to within [-12h, 14h] because those are the current real world limits.
        //       There are historical time zones which can go beyond this, and Noda Time's Offset allows [-18h, 18h],
        //       but we do not need to deal with historical time zones so we can restrict it like this.
        //       These limits are used not only for clamping the client-provided offsets, but also for player-local-time
        //       LiveOps Events, for which "event has started in the earliest time zone" and "event has ended in the
        //       latest time zone" are meaningful things, and for those it is better that these limits are not excessive.
        // \note This used to be [-18h, 18h], but was changed to [-12h, 14h] in R28 for the above-mentioned
        //       LiveOps Events purpose.
        public static readonly MetaDuration MinimumUtcOffset = MetaDuration.FromHours(-12);
        public static readonly MetaDuration MaximumUtcOffset = MetaDuration.FromHours(14);

        public static PlayerTimeZoneInfo CreateForCurrentDevice()
        {
            TimeSpan utcOffsetTimeSpan = TimeZoneInfo.Local.GetUtcOffset(MetaTime.Now.ToDateTime());
            MetaDuration utcOffset = MetaDuration.FromMilliseconds((long)utcOffsetTimeSpan.TotalMilliseconds);
            return new PlayerTimeZoneInfo(utcOffset);
        }
    }
}
