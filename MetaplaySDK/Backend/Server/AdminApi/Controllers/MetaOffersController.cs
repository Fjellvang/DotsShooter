// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Activables;
using Metaplay.Core.Config;
using Metaplay.Core.Json;
using Metaplay.Core.Offers;
using Metaplay.Core.Player;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static Metaplay.Server.AdminApi.Controllers.Exceptions;
using ActionResult = Microsoft.AspNetCore.Mvc.ActionResult;

namespace Metaplay.Server.AdminApi.Controllers
{
    public class MetaOffersController : ActivablesControllerBase
    {
        public class GeneralMetaOfferGroupData : GeneralActivableData
        {
            public MetaDictionary<MetaOfferId, MetaOfferStatistics>  PerOfferStatistics;
            public double                                               Revenue;

            public GeneralMetaOfferGroupData(
                GeneralActivableData baseData,
                MetaDictionary<MetaOfferId, MetaOfferStatistics> perOfferStatistics,
                double revenue)
                : base(baseData)
            {
                PerOfferStatistics = perOfferStatistics;
                Revenue = revenue;
            }
        }

        public class GeneralMetaOfferData
        {
            [ForceSerializeByValue]
            public MetaOfferInfoBase        Config;
            public string                   ReferencePrice;
            public List<OfferUsedByData>    UsedBy;
            public MetaOfferStatistics      Statistics;

            public GeneralMetaOfferData(MetaOfferInfoBase config, string referencePrice, MetaOfferStatistics statistics)
            {
                Config = config ?? throw new ArgumentNullException(nameof(config));
                ReferencePrice = referencePrice;
                UsedBy = new List<OfferUsedByData>();
                Statistics = statistics;
            }
        }

        public class OfferUsedByData
        {
            public string Type;
            public string Id;
            public string DisplayName;

            public OfferUsedByData(string type, string id, string displayName)
            {
                Type = type;
                Id = id;
                DisplayName = displayName;
            }

            public static OfferUsedByData FromOfferGroup(MetaOfferGroupInfoBase group)
            {
                return new OfferUsedByData("OfferGroup", group.GroupId.ToString(), group.DisplayName);
            }

            public static OfferUsedByData FromOffer(MetaOfferInfoBase offer)
            {
                return new OfferUsedByData("Offer", offer.OfferId.ToString(), offer.DisplayName);
            }
        }

        public class GeneralMetaOffersResponse
        {
            public MetaDictionary<MetaOfferGroupId, GeneralMetaOfferGroupData>   OfferGroups;
            public MetaDictionary<MetaOfferId, GeneralMetaOfferData>             Offers;

            public GeneralMetaOffersResponse(MetaDictionary<MetaOfferGroupId, GeneralMetaOfferGroupData> offerGroups, MetaDictionary<MetaOfferId, GeneralMetaOfferData> offers)
            {
                OfferGroups = offerGroups;
                Offers = offers;
            }
        }

        /// <summary>
        /// Helper method to fill in references from offer groups and other offers to each offer's used by data.
        /// </summary>
        protected void FillInUsedByReferences(MetaDictionary<MetaOfferId, GeneralMetaOfferData> offerDatas, GeneralActivablesQueryContext context)
        {
            // Offer UsedBy: offer groups
            foreach (MetaOfferGroupInfoBase offerGroupInfo in context.SharedGameConfig.MetaOfferGroups.Values)
            {
                OfferUsedByData usedByData = OfferUsedByData.FromOfferGroup(offerGroupInfo);

                foreach (MetaOfferInfoBase offerInfo in offerGroupInfo.Offers.MetaRefUnwrap())
                {
                    if (offerDatas.ContainsKey(offerInfo.OfferId))
                        offerDatas[offerInfo.OfferId].UsedBy.Add(usedByData);
                }
            }

            // Offer UsedBy: other offers
            foreach (MetaOfferInfoBase referringOfferInfo in context.SharedGameConfig.MetaOffers.Values)
            {
                OfferUsedByData usedByData = OfferUsedByData.FromOffer(referringOfferInfo);

                foreach (MetaOfferId referencedOfferId in referringOfferInfo.GetReferencedMetaOffers().ToOrderedSet())
                {
                    if (offerDatas.ContainsKey(referencedOfferId)) // \note Check existence, in case configs refer to nonexistent offers
                        offerDatas[referencedOfferId].UsedBy.Add(usedByData);
                }
            }
        }

        /// <summary>
        /// Helper method to fill in references from offer groups and other offers to one offer's used by data.
        /// </summary>
        protected void FillInUsedByReferences(MetaOfferId offerId, GeneralMetaOfferData offerData, GeneralActivablesQueryContext context)
        {
            // Note: wrapping into a dictionary to reuse code
            FillInUsedByReferences(new MetaDictionary<MetaOfferId, GeneralMetaOfferData>() {{offerId, offerData}}, context);
        }

        /// <summary>
        /// Helper method to create a <see cref="GeneralMetaOfferGroupData"/>.
        /// </summary>
        protected GeneralMetaOfferGroupData CreateGeneralMetaOfferGroupData(MetaOfferGroupInfoBase offerGroupInfo,
            GeneralActivablesQueryContext context,
            MetaOffersStatistics offersStatistics,
            MetaActivableKindId activableKindId)
        {
            MetaOfferGroupStatistics offerGroupStatisticsMaybe = offersStatistics.OfferGroups.GetValueOrDefault(offerGroupInfo.GroupId);

            MetaDictionary<MetaOfferId, MetaOfferStatistics> perOfferStatistics = offerGroupInfo.Offers.MetaRefUnwrap().ToMetaDictionary(
                offerInfo => offerInfo.ConfigKey,
                offerInfo =>
                {
                    MetaOfferStatistics offerPerGroupStatistics = offerGroupStatisticsMaybe?.OfferPerGroupStatistics.GetValueOrDefault(offerInfo.OfferId) ?? new MetaOfferStatistics();
                    return offerPerGroupStatistics;
                });

            return new GeneralMetaOfferGroupData(
                CreateGeneralActivableData(offerGroupInfo.GroupId, offerGroupInfo, activableKindId, context),
                perOfferStatistics,
                revenue: offerGroupStatisticsMaybe?.GetTotalRevenue() ?? 0.0);
        }

        /// <summary>
        /// Helper method to create a <see cref="GeneralMetaOfferData"/>.
        /// </summary>
        protected GeneralMetaOfferData CreateGeneralMetaOfferData(MetaOfferInfoBase offerInfo, MetaOffersStatistics offersStatistics)
        {
            MetaOfferStatistics offerStatistics = offersStatistics.Offers.GetValueOrDefault(offerInfo.OfferId, defaultValue: new MetaOfferStatistics());
            return new GeneralMetaOfferData(offerInfo, offerInfo.TryGetReferencePriceString(), offerStatistics);
        }

        /// <summary>
        /// API endpoint to get MetaOffers and MetaOfferGroups info.
        ///
        /// <paramref name="time"/> determines the time to use
        /// when resolving the phases of local-time schedules.
        /// If it's omitted, current UTC time is used.
        ///
        /// Usage: GET /api/offers
        /// </summary>
        /// <remarks>
        /// Similar to <see cref="ActivablesController.GetActivablesAsync(string)"/>
        /// but specific to MetaOffers.
        /// </remarks>
        [HttpGet("offers")]
        [RequirePermission(MetaplayPermissions.ApiActivablesView)] // \todo [nuutti] Should this have a permission separate from activables generally? #meta-offers
        public async Task<ActionResult> GetMetaOffersAsync([FromQuery] string time)
        {
            GeneralActivablesQueryContext context = await GetGeneralActivablesQueryContextAsync(time).ConfigureAwait(false);

            MetaActivableKindId activableKindId = typeof(ActivableKindMetadataMetaOfferGroup).GetCustomAttribute<MetaActivableKindMetadataAttribute>().Id;

            MetaOffersStatistics offersStatistics = context.StatsCollectorState.MetaOfferStatistics;

            MetaDictionary<MetaOfferGroupId, GeneralMetaOfferGroupData> offerGroupDatas = context.SharedGameConfig.MetaOfferGroups.Values.ToMetaDictionary(
                offerGroupInfo => offerGroupInfo.ConfigKey,
                offerGroupInfo => CreateGeneralMetaOfferGroupData(offerGroupInfo, context, offersStatistics, activableKindId)
                );

            MetaDictionary<MetaOfferId, GeneralMetaOfferData> offerDatas = context.SharedGameConfig.MetaOffers.Values.ToMetaDictionary(
                offerInfo => offerInfo.ConfigKey,
                offerInfo => CreateGeneralMetaOfferData(offerInfo, offersStatistics)
                );

            // Fill in references from offer groups and other offers to each offer's used by data
            FillInUsedByReferences(offerDatas, context);

            return Ok(new GeneralMetaOffersResponse(
                offerGroupDatas,
                offerDatas));
        }

        /// <summary>
        /// API endpoint to get a single MetaOffer's info.
        ///
        /// <paramref name="time"/> determines the time to use
        /// when resolving the phases of local-time schedules.
        /// If it's omitted, current UTC time is used.
        ///
        /// Usage: GET /api/offers/offer/{offerIdStr}
        /// </summary>
        [HttpGet("offers/offer/{offerIdStr}")]
        [RequirePermission(MetaplayPermissions.ApiActivablesView)] // \todo [nuutti] Should this have a permission separate from activables generally? #meta-offers
        public async Task<ActionResult> GetMetaOfferAsync(string offerIdStr, [FromQuery] string time)
        {
            if (string.IsNullOrWhiteSpace(offerIdStr))
                throw new MetaplayHttpException(400, "Failed to get MetaOffer!", $"Invalid MetaOffer id: {offerIdStr}");

            MetaOfferId metaOfferId = MetaOfferId.FromString(offerIdStr);

            GeneralActivablesQueryContext context          = await GetGeneralActivablesQueryContextAsync(time).ConfigureAwait(false);
            MetaOffersStatistics          offersStatistics = context.StatsCollectorState.MetaOfferStatistics;

            if (!context.SharedGameConfig.MetaOffers.TryGetValue<MetaOfferId, MetaOfferInfoBase>(metaOfferId, out MetaOfferInfoBase offerInfo))
                throw new MetaplayHttpException(404, $"MetaOffer not found!", $"No MetaOffer exists with the Id: {offerIdStr}");

            GeneralMetaOfferData offerData = CreateGeneralMetaOfferData(offerInfo, offersStatistics);

            // Fill in references from offer groups and other offers to the queried offer's used by data
            FillInUsedByReferences(metaOfferId, offerData, context);

            return Ok(offerData);
        }

        /// <summary>
        /// API endpoint to get a single MetaOfferGroup's info.
        ///
        /// <paramref name="time"/> determines the time to use
        /// when resolving the phases of local-time schedules.
        /// If it's omitted, current UTC time is used.
        ///
        /// Usage: GET /api/offers/offergroup/{metaOfferGroupIdStr}
        /// </summary>
        [HttpGet("offers/offerGroup/{metaOfferGroupIdStr}")]
        [RequirePermission(MetaplayPermissions.ApiActivablesView)] // \todo [nuutti] Should this have a permission separate from activables generally? #meta-offers
        public async Task<ActionResult> GetMetaOfferGroupAsync(string metaOfferGroupIdStr, [FromQuery] string time)
        {
            if (string.IsNullOrWhiteSpace(metaOfferGroupIdStr))
                throw new MetaplayHttpException(400, "Failed to get MetaOfferGroup!", $"Invalid MetaOfferGroup id: {metaOfferGroupIdStr}");

            MetaOfferGroupId metaOfferGroupId = MetaOfferGroupId.FromString(metaOfferGroupIdStr);

            GeneralActivablesQueryContext context = await GetGeneralActivablesQueryContextAsync(time).ConfigureAwait(false);

            MetaActivableKindId activableKindId = typeof(ActivableKindMetadataMetaOfferGroup).GetCustomAttribute<MetaActivableKindMetadataAttribute>().Id;

            MetaOffersStatistics offersStatistics = context.StatsCollectorState.MetaOfferStatistics;

            if (!context.SharedGameConfig.MetaOfferGroups.TryGetValue(metaOfferGroupId, out MetaOfferGroupInfoBase offerGroupInfo))
                throw new MetaplayHttpException(404, "MetaOfferGroup not found!", $"No MetaOfferGroup exists with the Id: {metaOfferGroupId}");

            GeneralMetaOfferGroupData offerGroupData = CreateGeneralMetaOfferGroupData(offerGroupInfo, context, offersStatistics, activableKindId);

            return Ok(offerGroupData);
        }

        public class PlayerMetaOfferGroupData : PlayerActivableData
        {
            public MetaDictionary<MetaOfferId, PlayerMetaOfferData> Offers;

            public PlayerMetaOfferGroupData(
                PlayerActivableData baseData,
                MetaDictionary<MetaOfferId, PlayerMetaOfferData> offers)
                : base(baseData)
            {
                Offers = offers;
            }
        }

        public class PlayerMetaOfferData
        {
            [ForceSerializeByValue]
            public MetaOfferInfoBase        Config;
            public string                   ReferencePrice;
            public PlayerMetaOfferStateData State;

            public PlayerMetaOfferData(MetaOfferInfoBase config, string referencePrice, PlayerMetaOfferStateData state)
            {
                Config = config;
                ReferencePrice = referencePrice;
                State = state;
            }
        }

        public class PlayerMetaOfferStateData
        {
            public bool     IsActive;
            public int      TotalNumActivated;
            public int      NumActivatedInGroup;
            public int      TotalNumPurchased;
            public int      NumPurchasedInGroup;
            public int?     NumPurchasedInCurrentActivation;

            public PlayerMetaOfferStateData(bool isActive, int totalNumActivated, int numActivatedInGroup, int totalNumPurchased, int numPurchasedInGroup, int? numPurchasedInCurrentActivation)
            {
                IsActive = isActive;
                TotalNumActivated = totalNumActivated;
                NumActivatedInGroup = numActivatedInGroup;
                TotalNumPurchased = totalNumPurchased;
                NumPurchasedInGroup = numPurchasedInGroup;
                NumPurchasedInCurrentActivation = numPurchasedInCurrentActivation;
            }
        }

        public class PlayerMetaOffersResponse
        {
            public MetaDictionary<MetaOfferGroupId, PlayerMetaOfferGroupData> OfferGroups;

            public PlayerMetaOffersResponse(MetaDictionary<MetaOfferGroupId, PlayerMetaOfferGroupData> offerGroups)
            {
                OfferGroups = offerGroups;
            }
        }

        [HttpGet("offers/{playerIdStr}")] // \todo [nuutti] Should this have a permission separate from activables generally? #meta-offers
        [RequirePermission(MetaplayPermissions.ApiActivablesView)]
        public async Task<ActionResult> GetMetaOffersForPlayerAsync(string playerIdStr)
        {
            PlayerActivablesQueryContext context = await GetPlayerActivablesQueryContextAsync(playerIdStr).ConfigureAwait(false);

            IPlayerModelBase player = context.PlayerModel;

            // \note See comment on PlayerMetaOfferGroupsModelBase.CustomCanStartActivation for why
            //       placement availability is checked here.
            //       #offer-group-placement-condition
            HashSet<OfferPlacementId> availablePlacements =
                context.SharedGameConfig.MetaOfferGroups.Values
                .Select(offerGroup => offerGroup.Placement)
                .Distinct()
                .Where(placement => player.MetaOfferGroups.PlacementIsAvailable(player, placement))
                .ToHashSet();

            MetaDictionary<MetaOfferGroupId, PlayerMetaOfferGroupData> offerGroupDatas = context.SharedGameConfig.MetaOfferGroups.Values.ToMetaDictionary(
                offerGroupInfo => offerGroupInfo.ConfigKey,
                offerGroupInfo =>
                {
                    MetaDictionary<MetaOfferId, PlayerMetaOfferData> offerDatas = offerGroupInfo.Offers.MetaRefUnwrap().ToMetaDictionary(
                        offerInfo => offerInfo.ConfigKey,
                        offerInfo =>
                        {
                            MetaOfferStatus offerStatus = player.MetaOfferGroups.GetOfferStatus(player, offerGroupInfo, offerInfo);

                            return new PlayerMetaOfferData(
                                offerInfo,
                                referencePrice: offerInfo.TryGetReferencePriceString(),
                                new PlayerMetaOfferStateData(
                                    isActive:                           offerStatus.IsActive,
                                    totalNumActivated:                  offerStatus.NumActivatedByPlayer,
                                    numActivatedInGroup:                offerStatus.NumActivatedInGroup,
                                    totalNumPurchased:                  offerStatus.NumPurchasedByPlayer,
                                    numPurchasedInGroup:                offerStatus.NumPurchasedInGroup,
                                    numPurchasedInCurrentActivation:    offerStatus.NumPurchasedDuringActivation));
                        });

                    PlayerActivableData baseActivableData = CreatePlayerActivableData(offerGroupInfo, player.MetaOfferGroups, context);

                    // If the activable's conditions are otherwise fulfilled, but placement is not available,
                    // adjust the phase from Tentative to Inactive.
                    // #offer-group-placement-condition
                    if (baseActivableData.Phase == ActivablePhase.Tentative && !availablePlacements.Contains(offerGroupInfo.Placement))
                        baseActivableData.Phase = ActivablePhase.Inactive;

                    return new PlayerMetaOfferGroupData(
                        baseActivableData,
                        offerDatas);
                });

            return Ok(new PlayerMetaOffersResponse(
                offerGroupDatas));
        }
    }
}
