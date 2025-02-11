// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Server.StatisticsEvents;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Metaplay.Server.Tests;

[TestFixture]
public class StatisticsCohortTimeSeriesTests
{
    static readonly int[] _dailyCohorts = [0, 1, 2, 3, 4, 5, 6, 7, 14, 21, 28];

    class ExpectedCohorts
    {
        public int       days;
        public int[]     registrations;
        /// <summary>
        /// First index is index into _dailyCohorts list, second is day.
        /// </summary>
        public int[][]   loginCohorts;
        /// <summary>
        /// First index is index into _dailyCohorts list, second is day.
        /// </summary>
        public MetaDuration[][] sessionLengthCohorts;
        /// <summary>
        /// First index is index into _dailyCohorts list, second is day.
        /// </summary>
        public float[][] purchaseCohorts;

        public ExpectedCohorts(int days)
        {
            this.days = days;

            registrations        = new int[days];
            loginCohorts         = new int[_dailyCohorts.Length][];
            purchaseCohorts      = new float[_dailyCohorts.Length][];
            sessionLengthCohorts = new MetaDuration[_dailyCohorts.Length][];

            for (int i = 0; i < _dailyCohorts.Length; i++)
            {
                loginCohorts[i]         = new int[days];
                purchaseCohorts[i]      = new float[days];
                sessionLengthCohorts[i] = new MetaDuration[days];
            }
        }
    }

    StatisticsSimpleTimeSeries<int> _untypedRegistrations = new StatisticsSimpleTimeSeries<int>(
        new StatisticsSimpleTimeSeries<int>.SetupParams(
            eventTypes: [typeof(StatisticsEventPlayerCreated)],
            seriesKey: "testUntypedRegistrations",
            mode: StatisticsBucketAccumulationMode.Count), null);

    StatisticsDailyCohortTimeSeries<StatisticsEventPlayerLogin, int> _typedLoginDailyCohort = new StatisticsDailyCohortTimeSeries<StatisticsEventPlayerLogin, int>(
        new StatisticsDailyCohortTimeSeries<StatisticsEventPlayerLogin, int>.SetupParams(
            seriesKey: "testTypedLoginDaily",
            storagePageKey: "",
            dailyCohorts: _dailyCohorts,
            login =>
            {
                int cohort = login.DaysSinceRegistered;
                if (!_dailyCohorts.Contains(login.DaysSinceRegistered))
                    return default;

                return (cohort, 1);
            }), null);

    StatisticsDailyCohortTimeSeries<float> _untypedPurchaseDailyCohort = new StatisticsDailyCohortTimeSeries<float>(
        new StatisticsDailyCohortTimeSeries<float>.SetupParams(
            eventTypes: [typeof(StatisticsEventPlayerPurchase)],
            seriesKey: "testUntypedPurchaseDaily",
            storagePageKey: "",
            dailyCohorts: _dailyCohorts,
            ev =>
            {
                if (ev is not StatisticsEventPlayerPurchase purchase)
                    throw new InvalidOperationException();

                int cohort = purchase.DaysSinceRegistered;
                if (!_dailyCohorts.Contains(purchase.DaysSinceRegistered))
                    return default;

                return (cohort, purchase.DollarValue);
            }), null);

    StatisticsCohortSimpleCombinedSeries<int, int, float> _retentionCohorts;
    StatisticsCohortCombinedSeries<int, float, float> _dailyArpDauCohorts;

    MetaTime                    _epochTime;
    StatisticsPageTimeline      _timeline;
    MockStatisticsPageStorage   _statisticsPageStorage;
    StatisticsPageWriteBuffer  _writeBuffer;
    TieredStatisticsPageStorage _readStorage;
    MockMetaLogger              _logger;

    public StatisticsCohortTimeSeriesTests()
    {
        _retentionCohorts = new StatisticsCohortSimpleCombinedSeries<int, int, float>(
            new StatisticsCohortSimpleCombinedSeries<int, int, float>.SetupParams(
                seriesKey: "retentionTest",
                cohortTimeSeries: _typedLoginDailyCohort,
                simpleTimeSeries: _untypedRegistrations,
                bucketCombiner: (logins, registrations) =>
                {
                    if (registrations is not > 0 || logins is null)
                        return null;

                    return logins / (float)registrations;
                }), null);
        _dailyArpDauCohorts = new StatisticsCohortCombinedSeries<int, float, float>(
            new StatisticsCohortCombinedSeries<int, float, float>.SetupParams(
                seriesKey: "arpDauTest",
                series1: _typedLoginDailyCohort,
                series2: _untypedPurchaseDailyCohort,
                bucketCombiner: (logins, purchases) =>
                {
                    if (logins is not > 0 || purchases is null)
                        return null;

                    return purchases / (float)logins;
                }), null);
    }

    [SetUp]
    public void SetupTests()
    {
        _epochTime = MetaTime.FromDateTime(
            new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        _timeline              = new StatisticsPageTimeline(_epochTime, StatisticsPageResolutions.Daily);
        _statisticsPageStorage = new MockStatisticsPageStorage();
        _writeBuffer          = new StatisticsPageWriteBuffer(_timeline);

        _writeBuffer.SetBackingStorage(_statisticsPageStorage);
        _readStorage = new TieredStatisticsPageStorage(_statisticsPageStorage, _writeBuffer);

        _logger = new MockMetaLogger();
    }


    /// <summary>
    /// Take in daysSinceStart and return number of new registrations for that day
    /// </summary>
    delegate int RegistrationDelegate(int daysSinceStart);

    /// <summary>
    /// Take in daysSinceRegistered and return sessions count and length
    /// </summary>
    delegate MetaDuration[] LoginDelegate(int user, int daysSinceRegistered);

    /// <summary>
    /// Take in daysSinceRegistered and return a list of purchases for that day
    /// </summary>
    delegate float[] PurchaseDelegate(int user, int daysSinceRegistered);


    static ExpectedCohorts GetExpectedCohorts(
        int days,
        RegistrationDelegate registrationFunc,
        LoginDelegate loginFunc,
        PurchaseDelegate purchaseFunc)
    {
        // Int stored here is registration date. Index into list is user id.
        List<int> users = new List<int>();

        ExpectedCohorts result = new ExpectedCohorts(days);

        for (int day = 0; day < days; day++)
        {
            int newRegistrations = registrationFunc(day);

            for (int n = 0; n < newRegistrations; n++)
            {
                result.registrations[day]++;
                users.Add(day);
            }

            for (int user = 0; user < users.Count; user++)
            {
                int            daysSinceRegistered = day - users[user];
                MetaDuration[] logins              = loginFunc(user, daysSinceRegistered);
                float[]        purchases           = purchaseFunc(user, daysSinceRegistered);

                int registrationDay = users[user];
                int cohortIndex     = Array.IndexOf(_dailyCohorts, daysSinceRegistered);

                if (cohortIndex == -1)
                    continue;

                if (logins.Any())
                {
                    result.loginCohorts[cohortIndex][registrationDay]++;
                    result.sessionLengthCohorts[cohortIndex][registrationDay] += MetaDuration.FromMilliseconds(logins.Sum(l => l.Milliseconds));
                }

                if (purchases.Any())
                    result.purchaseCohorts[cohortIndex][registrationDay] += purchases.Sum();
            }
        }

        return result;
    }

    static IEnumerable<StatisticsEventBase> GenerateTestData(MetaTime startDate, int days, RegistrationDelegate registrationFunc, LoginDelegate loginFunc, PurchaseDelegate purchaseFunc)
    {
        static EntityId ToEntityId(int user) => EntityId.Create(EntityKindCore.Player, (ulong)(user));

        // Int stored here is registration date. Index into list is user id.
        List<int>                               users    = new List<int>();
        Dictionary<string, StatisticsEventBase> testData = new Dictionary<string, StatisticsEventBase>();

        MetaTime time = startDate;

        for (int day = 0; day < days; day++)
        {
            int newRegistrations = registrationFunc(day);

            for (int n = 0; n < newRegistrations; n++)
            {
                var registration = new StatisticsEventPlayerCreated(time, ToEntityId(users.Count));
                testData.Add(registration.UniqueKey, registration);
                users.Add(day);
            }

            for (int user = 0; user < users.Count; user++)
            {
                int            daysSinceRegistered = day - users[user];
                MetaDuration[] logins              = loginFunc(user, daysSinceRegistered);
                float[]        purchases           = purchaseFunc(user, daysSinceRegistered);
                EntityId       userId              = ToEntityId(user);
                MetaTime       loginTime           = time;

                foreach (var sessionLength in logins)
                {
                    var login = new StatisticsEventPlayerLogin(loginTime, userId, daysSinceRegistered);
                    // \todo [nomi]: session events
                    loginTime += sessionLength;
                    testData.TryAdd(login.UniqueKey, login);
                }

                foreach (float value in purchases)
                {
                    var purchase = new StatisticsEventPlayerPurchase(time, userId,Guid.NewGuid().ToString(), value, daysSinceRegistered);
                    testData.TryAdd(purchase.UniqueKey, purchase);
                }
            }

            time += MetaDuration.Day;
        }
        return testData.Values;
    }

    static (IEnumerable<StatisticsEventBase> events, ExpectedCohorts expected) GenerateStandardTestData(MetaTime startDate, int days)
    {
        RegistrationDelegate registrations = (int daysSinceStart) => 7 + daysSinceStart * 2;

        // Simulate different users playing for different amount of days
        int[] playTimes = [
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 1, 1, 1, 1, 1, 1, 1,
            2, 2, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3,
            4, 4, 4, 4, 4,
            5, 5, 5, 5, 5,
            6, 6, 6, 6,
            7, 7, 7,
            10, 10, 11, 11, 12, 12, 14, 14, 16, 16, 20, 20, 40];

        int GetPlayTime (int user)
        {
            int hashedValue = (user ^ (user >> 16)) * 0x45d9f3b;
            hashedValue = Math.Abs(hashedValue);
            int idx = hashedValue % playTimes.Length;
            return playTimes[idx];
        }

        LoginDelegate logins = (int user, int daysSinceRegistered) =>
        {
            int playTime = GetPlayTime(user);
            if (daysSinceRegistered <= playTime)
                return [MetaDuration.FromMinutes(10), MetaDuration.FromMinutes(daysSinceRegistered)];
            return [];
        };

        PurchaseDelegate purchases = (int user, int daysSinceRegistered) =>
        {
            if (daysSinceRegistered <= 0)
                return [0.5f];
            int playTime = GetPlayTime(user);

            if (daysSinceRegistered > playTime)
                return [];

            float spend = MathF.Sqrt(user) - daysSinceRegistered;
            if (spend <= 0)
                return [];

            // Divide spend over multiple purchases
            if (daysSinceRegistered >= 10)
                return [spend * 0.5f, spend * 0.3f, spend * 0.2f];
            if (daysSinceRegistered >= 5)
                return [spend * 0.1f, spend * 0.05f, spend * 0.85f];

            return [spend];
        };

        return (GenerateTestData(startDate, days, registrations, logins, purchases), GetExpectedCohorts(days, registrations, logins, purchases));
    }


    [Test]
    public async Task TestDefaultDataCohorts()
    {
        const int days = 30;

        StatisticsTimeSeriesPageWriter writer    = new StatisticsTimeSeriesPageWriter(_writeBuffer, [_untypedRegistrations, _typedLoginDailyCohort, _untypedPurchaseDailyCohort], _logger);

        MetaTime startTime = _epochTime;
        MetaTime endTime   = startTime + MetaDuration.FromDays(days);

        // Generate test data
        (IEnumerable<StatisticsEventBase> events, ExpectedCohorts expected) = GenerateStandardTestData(startTime, days);
        foreach (StatisticsEventBase ev in events)
            writer.Write(ev);

        await writer.FlushAsync();

        // Read registration data (not cohorted)
        ITimeSeriesReader<int> registrationReader = _untypedRegistrations.GetReader(_timeline, _readStorage);
        StatisticsTimeSeriesView<int> registrationData = await registrationReader.ReadTimeSpan(startTime, endTime);

        Assert.AreEqual(days, registrationData.NumBuckets);
        Assert.AreEqual(startTime, registrationData.StartTime);
        Assert.AreEqual(endTime, registrationData.EndTime);

        // Construct readers for all cohorts.
        ITimeSeriesReader<int>[] loginReaders = _dailyCohorts.Select(
            cohort =>
                _typedLoginDailyCohort.GetReader(
                    _timeline,
                    _readStorage,
                    cohort.ToString(CultureInfo.InvariantCulture)))
            .ToArray();

        ITimeSeriesReader<float>[] purchaseReaders = _dailyCohorts.Select(
                cohort =>
                    _untypedPurchaseDailyCohort.GetReader(
                        _timeline,
                        _readStorage,
                        cohort.ToString(CultureInfo.InvariantCulture)))
            .ToArray();


        ITimeSeriesReader<float>[] retentionReaders = _dailyCohorts.Select(
                cohort =>
                    _retentionCohorts.GetReader(
                        _timeline,
                        _readStorage,
                        cohort.ToString(CultureInfo.InvariantCulture)))
            .ToArray();

        ITimeSeriesReader<float>[] arpDauReaders = _dailyCohorts.Select(
                cohort =>
                    _dailyArpDauCohorts.GetReader(
                        _timeline,
                        _readStorage,
                        cohort.ToString(CultureInfo.InvariantCulture)))
            .ToArray();

        // Verify reader amount.
        Assert.AreEqual(_dailyCohorts.Length, loginReaders.Length);
        Assert.AreEqual(_dailyCohorts.Length, purchaseReaders.Length);
        Assert.AreEqual(_dailyCohorts.Length, retentionReaders.Length);
        Assert.AreEqual(_dailyCohorts.Length, arpDauReaders.Length);

        for (int cohort = 0; cohort < loginReaders.Length; cohort++)
        {
            // Read data for cohort
            StatisticsTimeSeriesView<int> cohortLoginData = await loginReaders[cohort].ReadTimeSpan(startTime, endTime);
            StatisticsTimeSeriesView<float> cohortPurchaseData = await purchaseReaders[cohort].ReadTimeSpan(startTime, endTime);
            StatisticsTimeSeriesView<float> cohortRetentionData = await retentionReaders[cohort].ReadTimeSpan(startTime, endTime);
            StatisticsTimeSeriesView<float> cohortArpDauData = await arpDauReaders[cohort].ReadTimeSpan(startTime, endTime);

            Assert.AreEqual(days, cohortLoginData.NumBuckets);
            Assert.AreEqual(startTime, cohortLoginData.StartTime);
            Assert.AreEqual(endTime, cohortLoginData.EndTime);

            Assert.AreEqual(days, cohortPurchaseData.NumBuckets);
            Assert.AreEqual(startTime, cohortPurchaseData.StartTime);
            Assert.AreEqual(endTime, cohortPurchaseData.EndTime);

            Assert.AreEqual(days, cohortRetentionData.NumBuckets);
            Assert.AreEqual(startTime, cohortRetentionData.StartTime);
            Assert.AreEqual(endTime, cohortRetentionData.EndTime);

            Assert.AreEqual(days, cohortArpDauData.NumBuckets);
            Assert.AreEqual(startTime, cohortArpDauData.StartTime);
            Assert.AreEqual(endTime, cohortArpDauData.EndTime);

            for (int day = 0; day < days; day++)
            {
                int   registrationValue = registrationData.Buckets[day] ?? 0;
                int   loginValue        = cohortLoginData.Buckets[day] ?? 0;
                float purchaseValue     = cohortPurchaseData.Buckets[day] ?? 0;
                float retentionValue    = cohortRetentionData.Buckets[day] ?? 0;
                float dailyArpDauValue  = cohortArpDauData.Buckets[day] ?? 0;

                float expectedRetention   = registrationValue > 0 ? loginValue / (float)registrationValue : 0;
                float expectedDailyArpDau = loginValue > 0 ? purchaseValue / loginValue : 0;

                // Verify that each day's data matches expected data.
                Assert.AreEqual(expected.registrations[day], registrationValue);
                Assert.AreEqual(expected.loginCohorts[cohort][day], loginValue);

                Assert.That(purchaseValue, Is.EqualTo(expected.purchaseCohorts[cohort][day]).Within(0.01f).Percent);
                Assert.That(retentionValue, Is.EqualTo(expectedRetention).Within(0.01f).Percent);
                Assert.That(dailyArpDauValue, Is.EqualTo(expectedDailyArpDau).Within(0.01f).Percent);
            }
        }
    }
}
