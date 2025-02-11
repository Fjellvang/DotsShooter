// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;

namespace Metaplay.Server.StatisticsEvents;

public static class StatisticsPageResolutions
{
    public static readonly StatisticsPageResolution Minute =
        new StatisticsPageResolution("minute", MetaDuration.Minute, 60 * 4); // 4 hour pages

    public static readonly StatisticsPageResolution Hourly =
        new StatisticsPageResolution("hourly", MetaDuration.Hour, 24); // 1 day pages

    public static readonly StatisticsPageResolution Daily =
        new StatisticsPageResolution("daily", MetaDuration.Day, 14); // 14 day pages
}
