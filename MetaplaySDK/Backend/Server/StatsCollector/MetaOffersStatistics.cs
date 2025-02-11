// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Config;
using Metaplay.Core.InAppPurchase;
using Metaplay.Core.Math;
using Metaplay.Core.Model;
using Metaplay.Core.Offers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Metaplay.Server
{
    // \note Statistics about activables in general are handled separately, see MetaActivableStatistics.cs

    [MetaSerializable]
    [MetaBlockedMembers(3)]
    public class MetaOffersStatistics
    {
        [MetaMember(1)] public Dictionary<MetaOfferGroupId, MetaOfferGroupStatistics>   OfferGroups { get; private set; } = new Dictionary<MetaOfferGroupId, MetaOfferGroupStatistics>();
        [MetaMember(2)] public Dictionary<MetaOfferId, MetaOfferStatistics>             Offers      { get; private set; } = new Dictionary<MetaOfferId, MetaOfferStatistics>();

        public bool HasAny => OfferGroups.Count > 0
                           || Offers.Count > 0;
    }

    [MetaSerializable]
    public class MetaOfferGroupStatistics
    {
        [MetaMember(1)] public Dictionary<MetaOfferId, MetaOfferStatistics> OfferPerGroupStatistics { get; private set; } = new Dictionary<MetaOfferId, MetaOfferStatistics>();

        public double GetTotalRevenue()
        {
            return OfferPerGroupStatistics.Values.Sum(offer => offer.Revenue);
        }
    }

    [MetaSerializable]
    public struct MetaOfferStatistics
    {
        [MetaMember(1)] public long NumActivated;
        [MetaMember(2)] public long NumActivatedForFirstTime;
        [MetaMember(3)] public long NumPurchased;
        [MetaMember(4)] public long NumPurchasedForFirstTime;
        [MetaMember(5)] public double Revenue;

        public MetaOfferStatistics(long numActivated, long numActivatedForFirstTime, long numPurchased, long numPurchasedForFirstTime, double revenue)
        {
            NumActivated = numActivated;
            NumActivatedForFirstTime = numActivatedForFirstTime;
            NumPurchased = numPurchased;
            NumPurchasedForFirstTime = numPurchasedForFirstTime;
            Revenue = revenue;
        }

        public static MetaOfferStatistics ForSingleActivation(bool isFirstActivation)
        {
            return new MetaOfferStatistics(
                numActivated:               1,
                numActivatedForFirstTime:   isFirstActivation ? 1 : 0,
                numPurchased:               0,
                numPurchasedForFirstTime:   0,
                revenue:                    0.0);
        }

        public static MetaOfferStatistics ForSinglePurchase(bool isFirstPurchase, double revenue)
        {
            return new MetaOfferStatistics(
                numActivated:               0,
                numActivatedForFirstTime:   0,
                numPurchased:               1,
                numPurchasedForFirstTime:   isFirstPurchase ? 1 : 0,
                revenue:                    revenue);
        }

        public static MetaOfferStatistics JustRevenue(double revenue)
        {
            return new MetaOfferStatistics(
                numActivated:               0,
                numActivatedForFirstTime:   0,
                numPurchased:               0,
                numPurchasedForFirstTime:   0,
                revenue:                    revenue);
        }

        public static MetaOfferStatistics Sum(MetaOfferStatistics a, MetaOfferStatistics b)
        {
            return new MetaOfferStatistics(
                numActivated:               a.NumActivated              + b.NumActivated,
                numActivatedForFirstTime:   a.NumActivatedForFirstTime  + b.NumActivatedForFirstTime,
                numPurchased:               a.NumPurchased              + b.NumPurchased,
                numPurchasedForFirstTime:   a.NumPurchasedForFirstTime  + b.NumPurchasedForFirstTime,
                revenue:                    a.Revenue                   + b.Revenue);
        }
    }
}
