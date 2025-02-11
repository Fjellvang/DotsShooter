// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using Metaplay.Server.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Metaplay.Server.StatisticsEvents;

public class DefaultStatisticsTimeSeriesRegistry : StatisticsTimeSeriesRegistryBase
{

}

/// <summary>
/// A registry class for registering business metrics time series. You can extend this class in the game code
/// to add your own time series, or change the sdk provided ones. You can also customize the daily cohorts
/// to add more or reduce the amount of cohorts tracked.
/// </summary>
public abstract class StatisticsTimeSeriesRegistryBase : IMetaIntegrationSingleton<StatisticsTimeSeriesRegistryBase>
{
    public static StatisticsTimeSeriesRegistryBase Instance => IntegrationRegistry.Get<StatisticsTimeSeriesRegistryBase>();

    const string DailyCohortsPageKey = "DailyCohorts";

    public class CategoryDefinition
    {
        public string Name       { get; }
        public int    OrderIndex { get; }

        public CategoryDefinition(string name, int orderIndex)
        {
            Name       = name;
            OrderIndex = orderIndex;
        }
    }

    public static CategoryDefinition EngagementCategory => new CategoryDefinition("Engagement", 100);
    public static CategoryDefinition MonetizationCategory => new CategoryDefinition("Monetization", 200);
    public static CategoryDefinition TechnicalCategory => new CategoryDefinition("Technical", 300);

    protected virtual IEnumerable<int> DailyCohorts { get; } = new [] {
        // Daily for first month (32 total)
        0, 1, 2, 3, 4, 5, 6,
        7, 8, 9, 10, 11, 12, 13,
        14, 15, 16, 17, 18, 19, 20,
        21, 22, 23, 24, 25, 26, 27,
        28, 29, 30, 31,
        // Weekly up to 180 days (22)
        35, 42, 49, 56, 63, 70,
        77, 84, 91, 98, 105, 112,
        119, 126, 133, 140, 147,
        154, 161, 168, 175, 182,
        // Every 28 days up to 3 years (~32)
        196, 224, 252, 280, 308, 336,
        364, 392, 420, 448, 476, 504,
        532, 560, 588, 616, 644, 672,
        700, 728, 756, 784, 812, 840,
        868, 896, 924, 952, 980, 1008,
        1036, 1064, 1092, 1120,
    };

    protected virtual CategoryDefinition[] Categories { get; } = new[]
    {
        EngagementCategory,
        MonetizationCategory,
        TechnicalCategory,
    };

    HashSet<int> _dailyCohortsSet;
    static SystemOptions _systemOptions;

    public static StatisticsSimpleTimeSeries<StatisticsEventPlayerCreated, int> NewUsers = new StatisticsSimpleTimeSeries<StatisticsEventPlayerCreated, int>(
        new StatisticsSimpleTimeSeries<StatisticsEventPlayerCreated, int>.SetupParams(
            seriesKey: "registeredUsers",
            mode: StatisticsBucketAccumulationMode.Count),
        StatisticsTimeSeriesDashboardInfo.Simple<int>(
            name: "New Players",
            purposeDescription: "How many fresh players are entering your game. This is the output of your marketing or virality.",
            implementationDescription: "Number of players who have logged in for the first time, creating an account.",
            category: EngagementCategory));

    public static StatisticsSimpleTimeSeries<StatisticsEventPlayerSession, int> NumSessions = new StatisticsSimpleTimeSeries<StatisticsEventPlayerSession, int>(
        new StatisticsSimpleTimeSeries<StatisticsEventPlayerSession, int>.SetupParams(
            seriesKey: "numSessions",
            mode: StatisticsBucketAccumulationMode.Count),
        StatisticsTimeSeriesDashboardInfo.Simple<int>(
            name: "Sessions",
            purposeDescription: "How often players are playing the game. Sharp increases or decreases can tell you about how players respond to content.",
            implementationDescription: "Number of total game sessions during the given time frame. Sessions are recorded at the end of the player's session.",
            category: EngagementCategory));

    public static StatisticsSimpleTimeSeries<StatisticsEventPlayerSession, long> SessionLength = new StatisticsSimpleTimeSeries<StatisticsEventPlayerSession, long>(
        new StatisticsSimpleTimeSeries<StatisticsEventPlayerSession, long>.SetupParams(
            seriesKey: "totalSessionLength",
            mode: StatisticsBucketAccumulationMode.Sum,
            eventParser: session => session.SessionLengthApprox.Milliseconds),
        StatisticsTimeSeriesDashboardInfo.Simple<double>(
                name: "Session Length",
                purposeDescription: "How much play time is your player base putting in.",
                implementationDescription: "Total cumulative session length of all users. Sessions are recorded at the end of the player's session.",
                category: TechnicalCategory)
            .WithValueTransformer((value) => value / 60000.0)
            .WithUnit(" min")
            .WithDecimalPrecision(2)
            .WithHidden(true));

    public static StatisticsSimpleCombinedSeries<int, long, long> AvgSessionLength = new StatisticsSimpleCombinedSeries<int, long, long>(
        new StatisticsSimpleCombinedSeries<int, long, long>.SetupParams(
            seriesKey: "avgSessionLength",
            series1: NumSessions,
            series2: SessionLength,
            bucketCombiner: (numSess, totalSessLength) => numSess is > 0 ? totalSessLength / numSess : null),
        StatisticsTimeSeriesDashboardInfo.Simple<double>(
            name: "Average Session Length",
            purposeDescription: "How much time players are spending in your game during each session. Sharp increases or decreases can tell you about how players respond to content.",
            implementationDescription: "The average session length calculated from the total number of sessions and total cumulative session length.",
            category: EngagementCategory)
            .WithValueTransformer((value) => value / 60000.0)
            .WithUnit(" min")
            .WithDecimalPrecision(2));

    public static StatisticsSimpleTimeSeries<int> DailyActiveUsers = new StatisticsSimpleTimeSeries<int>(
        new StatisticsSimpleTimeSeries<int>.SetupParams(
            eventTypes: [typeof(StatisticsEventPlayerCreated), typeof(StatisticsEventPlayerLogin)],
            seriesKey: "dau",
            mode: StatisticsBucketAccumulationMode.Sum,
            eventParser: ev => ev switch
            {
                StatisticsEventPlayerCreated => 1,
                // Don't double count registration date.
                StatisticsEventPlayerLogin login => login.DaysSinceRegistered > 0 ? 1 : 0,
                _ => throw new ArgumentException($"Unexpected event type! {ev.GetType()}"),
            }),
        StatisticsTimeSeriesDashboardInfo.Simple<int>(
            name: "Daily Active Users",
            purposeDescription: "The size of your active player base. This is how popular your game is.",
            implementationDescription: "Number of unique players who logged in during the day. A single player identified by their EntityId will only be counted once during the same UTC 24 hour period, even if they play multiple times. The count resets at UTC midnight.",
            category: EngagementCategory)
            .WithDailyOnly(true)
            .WithOrderIndex(100));

    public static StatisticsSimpleTimeSeries<StatisticsEventPlayerPurchase, double> InAppRevenue = new StatisticsSimpleTimeSeries<StatisticsEventPlayerPurchase, double>(
        new StatisticsSimpleTimeSeries<StatisticsEventPlayerPurchase, double>.SetupParams(
            seriesKey: "inAppRevenue",
            mode: StatisticsBucketAccumulationMode.Sum,
            eventParser: purchase => purchase.DollarValue),
        StatisticsTimeSeriesDashboardInfo.Simple<double>(
            name: "In-App Purchases",
            purposeDescription: "How much money the game is generating from In-App Purchases. This is how good your business is.",
            implementationDescription: "Total sum of IAP gross revenue. This is based on reference USD prices.",
            category: MonetizationCategory)
            .WithUnit("$", true)
            .WithOrderIndex(100));

    public static StatisticsSimpleTimeSeries<StatisticsEventLiveMetrics, long> Concurrents = new StatisticsSimpleTimeSeries<StatisticsEventLiveMetrics, long>(
        new StatisticsSimpleTimeSeries<StatisticsEventLiveMetrics, long>.SetupParams(
            seriesKey: "ccu",
            mode: StatisticsBucketAccumulationMode.Avg,
            eventParser: ccu => ccu.Ccu),
        StatisticsTimeSeriesDashboardInfo.Simple<long>(
            name: "Concurrents",
            purposeDescription: "How many players are playing at the same time.",
            implementationDescription: "The average number of concurrent players playing at the same time, based on live PlayerActor count. Samples are collected every few seconds.",
            category: TechnicalCategory));

    public static StatisticsSimpleCombinedSeries<int, double, double> ArpDau = new StatisticsSimpleCombinedSeries<int, double, double>(
        new StatisticsSimpleCombinedSeries<int, double, double>.SetupParams(
            seriesKey: "arpdau",
            DailyActiveUsers,
            InAppRevenue,
            (dau, revenue) =>
            {
                if (revenue.HasValue && dau is > 0)
                    return revenue.Value / dau.Value;

                return null;
            }),
        StatisticsTimeSeriesDashboardInfo.Simple<double>(
            name: "Average Revenue per Daily Active User",
            purposeDescription: "How much revenue you’re generating per active player each day. This is how well your monetization works.",
            implementationDescription: "Total IAP / DAU. Based on USD reference prices.",
            category: MonetizationCategory)
            .WithUnit("$", true)
            .WithYMinValue(1.0f)
            .WithDailyOnly(true)
            .WithOrderIndex(90));

    public static StatisticsDailyCohortTimeSeries<StatisticsEventPlayerLogin, int>       DailyUniqueLoginsHistory;
    public static StatisticsDailyCohortTimeSeries<StatisticsEventPlayerPurchase, double> DailyPurchaseVolumeHistory;

    public static StatisticsCohortSimpleCombinedSeries<int, int, double> RetentionPerDay;
    public static StatisticsCohortCombinedSeries<int, double, double>    ArpDauPerDay;

    public static StatisticsSimpleTimeSeries<StatisticsEventErrorMessageCount, int> ErrorCount = new StatisticsSimpleTimeSeries<StatisticsEventErrorMessageCount, int>(
        new StatisticsSimpleTimeSeries<StatisticsEventErrorMessageCount, int>.SetupParams(
            seriesKey: "errorCount",
            mode: StatisticsBucketAccumulationMode.Sum,
            eventParser: errCount => errCount.Errors),
        StatisticsTimeSeriesDashboardInfo.Simple<int>(
            name: "Game Server Errors",
            purposeDescription: "Errors are often indicative of a problem with the server, and should be investigated.",
            implementationDescription: "The game server collects logged errors. The count is reset when the game server is rebooted.",
            category: TechnicalCategory));

    public static StatisticsSimpleTimeSeries<StatisticsEventIncidentReport, int> IncidentReportCount = new StatisticsSimpleTimeSeries<StatisticsEventIncidentReport, int>(
        new StatisticsSimpleTimeSeries<StatisticsEventIncidentReport, int>.SetupParams(
            seriesKey: "incidentReportCount",
            mode: StatisticsBucketAccumulationMode.Count),
        StatisticsTimeSeriesDashboardInfo.Simple<int>(
            name: "Player Incident Reports",
            purposeDescription: "Player incidents are often indicative of a problem with the game client, and should be investigated.",
            implementationDescription: "Player incident reports collected and persisted by the game server. The server can decline incident report upload requests from the client if they happen too often or the upload percentage has been set to lower than 100%.",
            category: TechnicalCategory));

    public static StatisticsCohortTimeSeries<int> DauCohorts = new StatisticsCohortTimeSeries<int>(
        new StatisticsCohortTimeSeries<int>.SetupParams(
            eventTypes: [typeof(StatisticsEventPlayerCreated), typeof(StatisticsEventPlayerLogin)],
            seriesKey: "dauCohorts",
            storagePageKey: null,
            cohorts: ["humans", "bots"],
            eventParser: ev => ev switch
            {
                StatisticsEventPlayerCreated created => (IsBotPlayer(created.PlayerId) ? "bots" : "humans", 1),
                // Don't double count registration date.
                StatisticsEventPlayerLogin login => login.DaysSinceRegistered > 0 ? (IsBotPlayer(login.PlayerId) ? "bots" : "humans", 1) : (null, null),
                _                                => throw new ArgumentException($"Unexpected event type! {ev.GetType()}"),
            }),
        StatisticsTimeSeriesDashboardInfo.Cohort<int>(
                name: "Daily Active Users (Separated)",
                purposeDescription: "The size of your active player base. This is how popular your game is.",
                implementationDescription: "DAU divided into real players and bots. Bot players are identified by their EntityId.",
                category: EngagementCategory)
            .WithDailyOnly(true)
            .WithOrderIndex(99)
            .WithHidden(true));

    protected StatisticsTimeSeriesRegistryBase()
    {
        _dailyCohortsSet = new HashSet<int>(DailyCohorts);

        int[] dailyCohorts = DailyCohorts.ToArray();

        DailyUniqueLoginsHistory = new StatisticsDailyCohortTimeSeries<StatisticsEventPlayerLogin, int>(
            new StatisticsDailyCohortTimeSeries<StatisticsEventPlayerLogin, int>.SetupParams(
                seriesKey: "dailyLogins",
                storagePageKey: DailyCohortsPageKey,
                dailyCohorts: dailyCohorts,
                login =>
                {
                    if (!_dailyCohortsSet.Contains(login.DaysSinceRegistered))
                        return default;

                    return (login.DaysSinceRegistered, 1);
                }),
            StatisticsTimeSeriesDashboardInfo.DailyCohort<double>(
                name: "Daily Unique Logins",
                purposeDescription: "DAU history, used for calculating retention.",
                implementationDescription: "DAU cohorted based on X days after registration.",
                category: EngagementCategory)
                .WithHidden(true));

        DailyPurchaseVolumeHistory = new StatisticsDailyCohortTimeSeries<StatisticsEventPlayerPurchase, double>(
            new StatisticsDailyCohortTimeSeries<StatisticsEventPlayerPurchase, double>.SetupParams(
                seriesKey: "dailyIapRevenue",
                storagePageKey: DailyCohortsPageKey,
                dailyCohorts: dailyCohorts,
                purchase =>
                {
                    if (!_dailyCohortsSet.Contains(purchase.DaysSinceRegistered))
                        return default;

                    return (purchase.DaysSinceRegistered, purchase.DollarValue);
                }),
            StatisticsTimeSeriesDashboardInfo.DailyCohort<double>(
                name: "Daily Revenue History",
                purposeDescription: "IAP history, used for calculating ArpDauPerDay",
                implementationDescription: "Daily total revenue from In-App Purchases cohorted based on the player's days after registration.",
                category: MonetizationCategory)
                .WithUnit("$", true)
                .WithHidden(true));

        RetentionPerDay = new StatisticsCohortSimpleCombinedSeries<int, int, double>(
            new StatisticsCohortSimpleCombinedSeries<int, int, double>.SetupParams(
                seriesKey: "retention",
                cohortTimeSeries: DailyUniqueLoginsHistory,
                simpleTimeSeries: NewUsers,
                bucketCombiner:
                (logins, registrations) =>
                {
                    if (logins.HasValue && registrations is > 0)
                        return logins.Value / (double)registrations.Value;

                    return null;
                }),
            StatisticsTimeSeriesDashboardInfo.DailyCohortPercentage<double>(
                name: "Exact-Day Retention",
                purposeDescription: "How likely your players are going to return to the game after X days. This is how good of a hobby your game is.",
                implementationDescription: "Calculated from DAU per day after registration and registered users from the given day.",
                category: EngagementCategory)
                .WithOrderIndex(90));

        ArpDauPerDay = new StatisticsCohortCombinedSeries<int, double, double>(
            new StatisticsCohortCombinedSeries<int, double, double>.SetupParams(
                seriesKey: "arpDauPerDay",
                series1: DailyUniqueLoginsHistory,
                series2: DailyPurchaseVolumeHistory,
                bucketCombiner: (logins, revenue) =>
                {
                    if (revenue.HasValue && logins is > 0)
                        return revenue.Value / logins.Value;

                    return null;
                }),
            StatisticsTimeSeriesDashboardInfo.DailyCohort<double>(
                name: "Average Revenue per Retained Day",
                purposeDescription: "How player purchase behaviour develops over time.",
                implementationDescription: "Calculated from DAU per day after registration and IAP revenue per day after registration.",
                category: MonetizationCategory)
                .WithYMinValue(1.0f)
                .WithUnit("$", true));
    }

    protected virtual IReadOnlyList<IStatisticsTimeSeries> SharedTimeSeries =>
        [NewUsers, NumSessions, SessionLength, InAppRevenue, Concurrents, ErrorCount, IncidentReportCount, AvgSessionLength];
    protected virtual IReadOnlyList<IStatisticsTimeSeries> DailyOnlyTimeSeries =>
        [DailyUniqueLoginsHistory, DailyActiveUsers, DailyPurchaseVolumeHistory, RetentionPerDay, ArpDauPerDay, ArpDau, DauCohorts];

    protected IEnumerable<IReadableTimeSeries> ReadableSharedTimeSeries =>
        SharedTimeSeries.Select(timeseries => timeseries as IReadableTimeSeries).Where(t => t != null);

    protected IEnumerable<IReadableTimeSeries> ReadableDailyOnlyTimeSeries =>
        DailyOnlyTimeSeries.Select(timeseries => timeseries as IReadableTimeSeries).Where(t => t != null);

    public virtual IEnumerable<CategoryDefinition> GetCategories()
    {
        return Categories;
    }

    public virtual IEnumerable<IStatisticsTimeSeries> GetTimeSeriesForResolution(StatisticsPageResolution resolution)
    {
        if (resolution == StatisticsPageResolutions.Daily)
            return SharedTimeSeries.Concat(DailyOnlyTimeSeries);

        return SharedTimeSeries;
    }

    public IReadableTimeSeries GetMetricByIdReadableTimeseries(string metricId)
    {
        foreach (IStatisticsTimeSeries statisticsTimeSeries in SharedTimeSeries)
        {
            if (statisticsTimeSeries.SeriesKey == metricId && statisticsTimeSeries is IReadableTimeSeries readableTimeseries)
                return readableTimeseries;
        }

        foreach (IStatisticsTimeSeries statisticsTimeSeries in DailyOnlyTimeSeries)
        {
            if (statisticsTimeSeries.SeriesKey == metricId && statisticsTimeSeries is IReadableTimeSeries readableTimeseries)
                return readableTimeseries;
        }

        return null;
    }

    public IStatisticsTimeSeries GetMetricById(string metricId)
    {
        foreach (IStatisticsTimeSeries statisticsTimeSeries in SharedTimeSeries)
        {
            if (statisticsTimeSeries.SeriesKey == metricId)
                return statisticsTimeSeries;
        }

        foreach (IStatisticsTimeSeries statisticsTimeSeries in DailyOnlyTimeSeries)
        {
            if (statisticsTimeSeries.SeriesKey == metricId)
                return statisticsTimeSeries;
        }

        return null;
    }

    public List<IStatisticsTimeSeries> GetSharedMetricsList()
    {
        return SharedTimeSeries.ToList();
    }

    public List<IStatisticsTimeSeries> GetDailyOnlyMetricsList()
    {
        return DailyOnlyTimeSeries.ToList();
    }

    public List<IStatisticsTimeSeries> GetMetricsList()
    {
        return SharedTimeSeries.Concat(DailyOnlyTimeSeries).ToList();
    }

    public StatisticsTimeSeriesDashboardInfo GetDashboardInfoForMetric(string metricId)
    {
        IStatisticsTimeSeries metric = GetMetricById(metricId);
        return metric?.DashboardInfo;
    }

    protected static bool IsBotPlayer(EntityId playerId)
    {
        _systemOptions ??= RuntimeOptionsRegistry.Instance.GetCurrent<SystemOptions>();
        if (!_systemOptions.AllowBotAccounts)
            return false;
        return playerId.Value < Authenticator.NumReservedBotIds;
    }

}
