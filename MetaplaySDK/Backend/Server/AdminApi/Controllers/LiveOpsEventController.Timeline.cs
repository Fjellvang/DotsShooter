// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Utility;
using Metaplay.Core;
using Metaplay.Core.Json;
using Metaplay.Core.LiveOpsEvent;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using Metaplay.Core.Schedule;
using Metaplay.Server.AdminApi.AuditLog;
using Metaplay.Server.LiveOpsEvent;
using Metaplay.Server.LiveOpsTimeline;
using Metaplay.Server.LiveOpsTimeline.Timeline;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using static Metaplay.Server.AdminApi.Controllers.Exceptions;

namespace Metaplay.Server.AdminApi.Controllers
{
    public partial class LiveOpsEventController
    {
        #region Audit log

        [MetaSerializable]
        public struct TimelineNodeAuditLogInfo
        {
            [MetaMember(1)] public MetaGuid Id;
            [MetaMember(2)] public NodeType NodeType;
            [MetaMember(3)] public string DisplayName;

            public TimelineNodeAuditLogInfo(MetaGuid id, NodeType nodeType, string displayName)
            {
                Id = id;
                NodeType = nodeType;
                DisplayName = displayName;
            }

            public TimelineNodeAuditLogInfo(Node node)
                : this(node.Id, node.NodeType, node.DisplayName)
            {
            }

            public override string ToString() => $"{NodeType} '{DisplayName}'";
        }

        [MetaSerializable]
        public struct TimelineElementAuditLogInfo
        {
            [MetaMember(1)] public ElementId Id;
            [MetaMember(2)] public string DisplayName;

            public TimelineElementAuditLogInfo(ElementId id, string displayName)
            {
                Id = id;
                DisplayName = displayName;
            }

            public TimelineElementAuditLogInfo(Element element, LiveOpsTimelineManagerState state)
                : this(element.ElementId, GetDisplayName(element.ElementId, state))
            {
            }

            static string GetDisplayName(ElementId elementId, LiveOpsTimelineManagerState state)
            {
                if (elementId is ElementId.LiveOpsEvent(MetaGuid occurrenceId))
                    return state.LiveOpsEvents.EventOccurrences[occurrenceId].EventParams.DisplayName;
                else if (elementId is ElementId.ServerError(MetaGuid serverErrorId))
                    return "Server Error";
                else if (elementId == null)
                    return "<null>";
                else
                    return "<unknown>";
            }

            public string ElementType => Id?.GetType().Name ?? "<null>";

            public override string ToString() => $"{ElementType} '{DisplayName}'";
        }

        [MetaSerializable]
        public struct TimelineItemMetadataChangeAuditLogInfo
        {
            [MetaMember(1)] public ItemMetadataField MetadataField { get; private set; }
            [MetaMember(2)] public string OldValue { get; private set; }
            [MetaMember(3)] public string NewValue { get; private set; }

            public TimelineItemMetadataChangeAuditLogInfo(ItemMetadataField metadataField, string oldValue, string newValue)
            {
                MetadataField = metadataField;
                OldValue = oldValue;
                NewValue = newValue;
            }

            public override string ToString() => $"{MetadataField} from {ValueToDisplayString(OldValue)} to {ValueToDisplayString(NewValue)}";

            static string ValueToDisplayString(string s)
            {
                if (s == null)
                    return "none";
                else
                    return $"'{s}'";
            }
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.LiveOpsTimelineNodeCreated)]
        public class TimelineNodeCreated : LiveOpsTimelineItemAuditLogEventPayloadBase
        {
            [MetaMember(1)] public TimelineNodeAuditLogInfo Node { get; private set; }
            [MetaMember(2)] public TimelineNodeAuditLogInfo Parent { get; private set; }

            TimelineNodeCreated() { }
            public TimelineNodeCreated(Node node, Node parent)
            {
                Node = new TimelineNodeAuditLogInfo(node);
                Parent = new TimelineNodeAuditLogInfo(parent);
            }

            public override ItemId GetAuditLogTargetItemId() => new ItemId.Node(Node.Id);

            public override string EventTitle => $"{Node.NodeType} created";
            public override string EventDescription => $"{Node} was created.";
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.LiveOpsTimelineNodeChildAdded)]
        public class TimelineNodeChildAdded : LiveOpsTimelineItemAuditLogEventPayloadBase
        {
            [MetaMember(1)] public TimelineNodeAuditLogInfo Parent { get; private set; }
            [MetaMember(2)] public TimelineNodeAuditLogInfo Child { get; private set; }

            TimelineNodeChildAdded() { }
            public TimelineNodeChildAdded(Node parent, Node child)
            {
                Parent = new TimelineNodeAuditLogInfo(parent);
                Child = new TimelineNodeAuditLogInfo(child);
            }

            public override ItemId GetAuditLogTargetItemId() => new ItemId.Node(Parent.Id);

            public override string EventTitle => $"{Child.NodeType} added to {Parent.NodeType}";
            public override string EventDescription => $"New {Child} was added to {Parent}";
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.LiveOpsTimelineNodeChildDeleted)]
        public class TimelineNodeDeleted : LiveOpsTimelineItemAuditLogEventPayloadBase
        {
            [MetaMember(1)] public TimelineNodeAuditLogInfo Node { get; private set; }

            TimelineNodeDeleted() { }
            public TimelineNodeDeleted(Node node)
            {
                Node = new TimelineNodeAuditLogInfo(node);
            }

            public override ItemId GetAuditLogTargetItemId() => new ItemId.Node(Node.Id);

            public override string EventTitle => $"{Node.NodeType} deleted";
            public override string EventDescription => $"{Node} was deleted.";
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.TimelineNodeChildRemoved)]
        public class TimelineNodeChildRemoved : LiveOpsTimelineItemAuditLogEventPayloadBase
        {
            [MetaMember(1)] public TimelineNodeAuditLogInfo Parent { get; private set; }
            [MetaMember(2)] public TimelineNodeAuditLogInfo Child { get; private set; }

            TimelineNodeChildRemoved() { }
            public TimelineNodeChildRemoved(Node parent, Node child)
            {
                Parent = new TimelineNodeAuditLogInfo(parent);
                Child = new TimelineNodeAuditLogInfo(child);
            }

            public override ItemId GetAuditLogTargetItemId() => new ItemId.Node(Parent.Id);

            public override string EventTitle => $"{Child.NodeType} removed from {Parent.NodeType}";
            public override string EventDescription => $"{Child} was removed from {Parent}.";
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.TimelineNodeMoved)]
        public class TimelineNodeMoved : LiveOpsTimelineItemAuditLogEventPayloadBase
        {
            [MetaMember(1)] public TimelineNodeAuditLogInfo Node { get; private set; }
            [MetaMember(2)] public TimelineNodeAuditLogInfo OldParent { get; private set; }
            [MetaMember(3)] public TimelineNodeAuditLogInfo NewParent { get; private set; }

            TimelineNodeMoved() { }
            public TimelineNodeMoved(Node node, Node oldParent, Node newParent)
            {
                Node = new TimelineNodeAuditLogInfo(node);
                OldParent = new TimelineNodeAuditLogInfo(oldParent);
                NewParent = new TimelineNodeAuditLogInfo(newParent);
            }

            public override ItemId GetAuditLogTargetItemId() => new ItemId.Node(Node.Id);

            public override string EventTitle => OldParent.Id == NewParent.Id
                                                 ? $"{Node.NodeType} moved within {NewParent.NodeType}"
                                                 : $"{Node.NodeType} moved to different {NewParent.NodeType}";

            public override string EventDescription => OldParent.Id == NewParent.Id
                                                       ? $"{Node} was moved within the same {NewParent}."
                                                       : $"{Node} was moved from {OldParent} to {NewParent}.";
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.TimelineNodeChildMoved)]
        public class TimelineNodeChildMoved : LiveOpsTimelineItemAuditLogEventPayloadBase
        {
            [MetaMember(1)] public TimelineNodeAuditLogInfo Parent { get; private set; }
            [MetaMember(2)] public TimelineNodeAuditLogInfo Child { get; private set; }

            TimelineNodeChildMoved() { }
            public TimelineNodeChildMoved(Node parent, Node child)
            {
                Parent = new TimelineNodeAuditLogInfo(parent);
                Child = new TimelineNodeAuditLogInfo(child);
            }

            public override ItemId GetAuditLogTargetItemId() => new ItemId.Node(Parent.Id);

            public override string EventTitle => $"{Child.NodeType} moved within {Parent.NodeType}";
            public override string EventDescription => $"{Child} was moved within the same {Parent}.";
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.TimelineNodeElementRemoved)]
        public class TimelineNodeElementRemoved : LiveOpsTimelineItemAuditLogEventPayloadBase
        {
            [MetaMember(1)] public TimelineNodeAuditLogInfo Node { get; private set; }
            [MetaMember(2)] public TimelineElementAuditLogInfo Element { get; private set; }

            TimelineNodeElementRemoved() { }
            public TimelineNodeElementRemoved(Node node, Element element, LiveOpsTimelineManagerState state)
            {
                Node = new TimelineNodeAuditLogInfo(node);
                Element = new TimelineElementAuditLogInfo(element, state);
            }

            public override ItemId GetAuditLogTargetItemId() => new ItemId.Node(Node.Id);

            public override string EventTitle => $"{Element.ElementType} removed from {Node.NodeType}";
            public override string EventDescription => $"{Element} was removed from {Node}.";
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.TimelineNodeElementAdded)]
        public class TimelineNodeElementAdded : LiveOpsTimelineItemAuditLogEventPayloadBase
        {
            [MetaMember(1)] public TimelineNodeAuditLogInfo Node { get; private set; }
            [MetaMember(2)] public TimelineElementAuditLogInfo Element { get; private set; }

            TimelineNodeElementAdded() { }
            public TimelineNodeElementAdded(Node node, Element element, LiveOpsTimelineManagerState state)
            {
                Node = new TimelineNodeAuditLogInfo(node);
                Element = new TimelineElementAuditLogInfo(element, state);
            }

            public override ItemId GetAuditLogTargetItemId() => new ItemId.Node(Node.Id);

            public override string EventTitle => $"{Element.ElementType} added to {Node.NodeType}";
            public override string EventDescription => $"{Element} was added to {Node}.";
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.TimelineElementMoved)]
        public class TimelineElementMoved : LiveOpsTimelineItemAuditLogEventPayloadBase
        {
            [MetaMember(1)] public TimelineElementAuditLogInfo Element { get; private set; }
            [MetaMember(2)] public TimelineNodeAuditLogInfo OldParent { get; private set; }
            [MetaMember(3)] public TimelineNodeAuditLogInfo NewParent { get; private set; }

            TimelineElementMoved() { }
            public TimelineElementMoved(Element element, LiveOpsTimelineManagerState state, Node oldParent, Node newParent)
            {
                Element = new TimelineElementAuditLogInfo(element, state);
                OldParent = new TimelineNodeAuditLogInfo(oldParent);
                NewParent = new TimelineNodeAuditLogInfo(newParent);
            }

            public override ItemId GetAuditLogTargetItemId() => new ItemId.Element(Element.Id);

            public override string EventTitle => $"{Element.ElementType} moved in timeline";
            public override string EventDescription => $"{Element} was moved in timeline from {OldParent} to {NewParent}.";
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.TimelineNodeMetadataChanged)]
        public class TimelineNodeMetadataChanged : LiveOpsTimelineItemAuditLogEventPayloadBase
        {
            [MetaMember(1)] public TimelineNodeAuditLogInfo Node { get; private set; }
            [MetaMember(2)] public List<TimelineItemMetadataChangeAuditLogInfo> MetadataChanges { get; private set; }

            TimelineNodeMetadataChanged() { }
            public TimelineNodeMetadataChanged(Node node, List<TimelineItemMetadataChangeAuditLogInfo> metadataChanges)
            {
                Node = new TimelineNodeAuditLogInfo(node);
                MetadataChanges = metadataChanges;
            }

            public override ItemId GetAuditLogTargetItemId() => new ItemId.Node(Node.Id);

            public override string EventTitle => $"{Node.NodeType} metadata changed";
            public override string EventDescription => $"{Node} metadata changed: {string.Join(", ", MetadataChanges)}";
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.TimelineElementMetadataChanged)]
        public class TimelineElementMetadataChanged : LiveOpsTimelineItemAuditLogEventPayloadBase
        {
            [MetaMember(1)] public TimelineElementAuditLogInfo Element { get; private set; }
            [MetaMember(2)] public List<TimelineItemMetadataChangeAuditLogInfo> MetadataChanges { get; private set; }

            TimelineElementMetadataChanged() {}
            public TimelineElementMetadataChanged(Element element, LiveOpsTimelineManagerState state, List<TimelineItemMetadataChangeAuditLogInfo> metadataChanges)
            {
                Element = new TimelineElementAuditLogInfo(element, state);
                MetadataChanges = metadataChanges;
            }

            public override ItemId GetAuditLogTargetItemId() => new ItemId.Element(Element.Id);

            public override string EventTitle => $"{Element.ElementType} timeline metadata changed";
            public override string EventDescription => $"{Element} in timeline metadata changed: {string.Join(", ", MetadataChanges)}";
        }

        #endregion

        // \todo Some nicer way of handling these hardcoded items
        const string ServerErrorsNodeIdPrefix = "node:serverErrors.";
        const string ServerErrorsSectionId = $"{ServerErrorsNodeIdPrefix}Section";
        const string ServerErrorsGroupId = $"{ServerErrorsNodeIdPrefix}Group";
        const string ServerErrorsRowId = $"{ServerErrorsNodeIdPrefix}Row";
        const string ServerErrorsSectionDisplayName = "Server Errors";
        const string ServerErrorsSectionDescription = "Errors that have been logged on the server recently.";
        const string ServerErrorsGroupDisplayName = "Server Errors";
        const string ServerErrorsGroupDescription = "Errors that have been logged on the server recently.";
        const string ServerErrorsRowDisplayName = "Server Errors";
        const string ServerErrorsRowDescription = "Errors that have been logged on the server recently.";
        const string ServerErrorsGroupColor = "#AA5555";
        const string ServerErrorElementColor = "#FF0000";

        static readonly bool EnableServerErrors = false; // \note Server errors disabled for now (but code is kept for reference)

        #region Timeline data getters

        [HttpGet("liveOpsTimelineData")]
        [RequirePermission(MetaplayPermissions.ApiLiveOpsEventsView)] // \todo #timeline More granular permissions - different types of timeline items can have different permissions
        public async Task<ActionResult<GetLiveOpsTimelineDataResponse>> GetLiveOpsTimelineData([FromQuery(Name = "firstInstant")] string firstInstantStr, [FromQuery(Name = "lastInstant")] string lastInstantStr)
        {
            MetaTime firstInstant = MetaTime.FromDateTime(DateTime.ParseExact(firstInstantStr, TimelineInstantDateTimeFormatString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
            MetaTime lastInstant = MetaTime.FromDateTime(DateTime.ParseExact(lastInstantStr, TimelineInstantDateTimeFormatString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

            MetaTime currentTime = MetaTime.Now;

            GetLiveOpsEventsResponse events = await EntityAskAsync(LiveOpsTimelineManager.EntityId, new GetLiveOpsEventsRequest(firstInstant, lastInstant));
            TimelineState timelineState = events.TimelineStateMaybe ?? throw new MetaAssertException($"Expected {nameof(GetLiveOpsEventsResponse)}.{nameof(GetLiveOpsEventsResponse.TimelineStateMaybe)} to be non-null");

            MetaDictionary<MetaGuid, LiveOpsEventSpec> specs = events.Specs.ToMetaDictionary(spec => spec.SpecId);

            StatsCollectorLiveOpsEventStatisticsRequest statisticsRequest = new StatsCollectorLiveOpsEventStatisticsRequest(events.Occurrences.Select(occurrence => occurrence.OccurrenceId).ToOrderedSet());
            StatsCollectorLiveOpsEventStatisticsResponse statisticsResponse = await EntityAskAsync(StatsCollectorManager.EntityId, statisticsRequest);

            RecentLoggedErrorsResponse recentLoggedErrorsResponse;
            if (EnableServerErrors)
                recentLoggedErrorsResponse = await EntityAskAsync(StatsCollectorManager.EntityId, RecentLoggedErrorsRequest.Instance);
            else
                recentLoggedErrorsResponse = null;

            Dictionary<string, ITimelineItem> items = new();
            Dictionary<MetaGuid, TimelineItemNode> rowItems = new();

            foreach (Node node in timelineState.Nodes.Values)
            {
                TimelineItemNode item = new TimelineItemNode
                {
                    Version = node.Version,
                    ItemType = node.NodeType.ToDashboardString(),
                    Hierarchy = new TimelineItemHierarchyInfo
                    {
                        ParentId = node.NodeType == NodeType.Root ? null : $"node:{node.ParentId}",
                        // \note For Rows, ChildIds is set to empty here and populated later.
                        ChildIds = node.ChildNodes?.Select(id => $"node:{id}").ToOrderedSet() ?? [],
                    },
                    Metadata = new TimelineItemNode.MetadataData
                    {
                        DisplayName = node.DisplayName,
                        Color = node.Color,
                    },
                    RenderData = new TimelineItemNode.RenderDataData
                    {
                        CannotRemoveReason = node.IsDefaultRow ? "Cannot remove the default row."
                                           : node.ChildNodes != null && node.ChildNodes.Count > 0 ? $"Cannot remove a non-empty {node.NodeType.ToLowercaseHumanString()}."
                                           : null, // \note For rows, this can still be assigned afterwards (see below)
                    },
                    IsImmutable = node.IsDefaultRow,
                };

                items.Add($"node:{node.Id}", item);

                if (node.NodeType == NodeType.Row)
                    rowItems.Add(node.Id, item);
            }

            foreach (Element element in timelineState.Elements.Values)
            {
                TimelineItemNode row = rowItems[element.RowId];
                if (row.RenderData.CannotRemoveReason == null)
                    row.RenderData.CannotRemoveReason = "Cannot remove a non-empty row.";
            }

            string rootNodeId = $"node:{timelineState.Nodes.Values.Single(node => node.NodeType == NodeType.Root).Id}";

            if (EnableServerErrors)
            {
                // Synthetic section+group+row for server errors.
                items.Add(ServerErrorsSectionId, new TimelineItemNode
                {
                    Version = 0,
                    ItemType = NodeType.Section.ToDashboardString(),
                    Hierarchy = new TimelineItemHierarchyInfo
                    {
                        ParentId = rootNodeId,
                        ChildIds = [ ServerErrorsGroupId ],
                    },
                    Metadata = new TimelineItemNode.MetadataData
                    {
                        DisplayName = ServerErrorsSectionDisplayName,
                        Color = null,
                    },
                    RenderData = new TimelineItemNode.RenderDataData
                    {
                        CannotRemoveReason = "This built-in item cannot be removed.",
                    },
                    IsImmutable = true,
                });
                items[rootNodeId].Hierarchy.ChildIds.Add(ServerErrorsSectionId);
                items.Add(ServerErrorsGroupId, new TimelineItemNode
                {
                    Version = 0,
                    ItemType = NodeType.Group.ToDashboardString(),
                    Hierarchy = new TimelineItemHierarchyInfo
                    {
                        ParentId = ServerErrorsSectionId,
                        ChildIds = [ ServerErrorsRowId ],
                    },
                    Metadata = new TimelineItemNode.MetadataData
                    {
                        DisplayName = ServerErrorsGroupDisplayName,
                        Color = ServerErrorsGroupColor,
                    },
                    RenderData = new TimelineItemNode.RenderDataData
                    {
                        CannotRemoveReason = "This built-in item cannot be removed.",
                    },
                    IsImmutable = true,
                });
                items.Add(ServerErrorsRowId, new TimelineItemNode
                {
                    Version = 0,
                    ItemType = NodeType.Row.ToDashboardString(),
                    Hierarchy = new TimelineItemHierarchyInfo
                    {
                        ParentId = ServerErrorsGroupId,
                        // \note ChildIds (if any) is populated later.
                        ChildIds = new OrderedSet<string>(),
                    },
                    Metadata = new TimelineItemNode.MetadataData
                    {
                        DisplayName = ServerErrorsRowDisplayName,
                        Color = null,
                    },
                    RenderData = new TimelineItemNode.RenderDataData
                    {
                        CannotRemoveReason = "This built-in item cannot be removed.",
                    },
                    IsImmutable = true,
                });
            }

            void AddElementItem(ElementId elementId, ITimelineElement element)
            {
                string elementIdStr = elementId.ToString();
                items.Add(elementIdStr, element);
                items[element.Hierarchy.ParentId].Hierarchy.ChildIds.Add(elementIdStr);
            }

            foreach (LiveOpsEventOccurrence occurrence in events.Occurrences)
            {
                LiveOpsEventSpec spec = specs[occurrence.DefiningSpecId];
                LiveOpsEventOccurrenceStatistics statistics = statisticsResponse.Statistics[occurrence.OccurrenceId];
                ElementId elementId = new ElementId.LiveOpsEvent(occurrence.OccurrenceId);
                Element elementState = timelineState.Elements.GetValueOrDefault(elementId)
                                       ?? Element.CreateDefaultState(elementId, timelineState);
                TimelineItemLiveopsEvent item = CreateTimelineItemLiveopsEvent(occurrence, spec, statistics, elementState, currentTime);
                AddElementItem(elementId, item);
            }

            if (EnableServerErrors)
            {
                foreach (RecentLogEventCounter.LogEventInfo errorDetails in recentLoggedErrorsResponse.ErrorsDetails)
                {
                    ElementId elementId = new ElementId.ServerError(errorDetails.Id);
                    TimelineItemInstantEvent item = new TimelineItemInstantEvent
                    {
                        Version = 0,
                        Hierarchy = new TimelineItemHierarchyInfo
                        {
                            ParentId = ServerErrorsRowId,
                            ChildIds = null,
                        },
                        RenderData = new TimelineItemInstantEvent.RenderDataData
                        {
                            InstantIsoString = errorDetails.Timestamp.ToISO8601(),
                            Color = ServerErrorElementColor,
                        },
                        IsImmutable = true,
                    };
                    AddElementItem(elementId, item);
                }
            }

            // Assert validity and consistency of ParentId and ChildIds
            foreach ((string itemId, ITimelineItem item) in items)
            {
                if (item.ItemType == NodeType.Root.ToDashboardString())
                {
                    if (item.Hierarchy.ParentId != null)
                        throw new MetaAssertException($"{itemId} has parent id, but shouldn't because it's the root node");
                }
                else
                {
                    if (item.Hierarchy.ParentId == null)
                        throw new MetaAssertException($"{itemId} is missing a parent id");

                    if (!items.TryGetValue(item.Hierarchy.ParentId, out ITimelineItem parent))
                        throw new MetaAssertException($"{itemId} has parent {item.Hierarchy.ParentId} which is not found among the items");
                    if (!parent.Hierarchy.ChildIds.Contains(itemId))
                        throw new MetaAssertException($"{itemId} has parent {item.Hierarchy.ParentId} which does not contain {itemId} in its child list");
                }

                if (item is ITimelineElement)
                {
                    if (item.Hierarchy.ChildIds != null)
                        throw new MetaAssertException($"{itemId} has child id list, but shouldn't because it's an Element (not Node)");
                }
                else
                {
                    if (item.Hierarchy.ChildIds == null)
                        throw new MetaAssertException($"{itemId} is missing the child id list");

                    foreach (string childId in item.Hierarchy.ChildIds)
                    {
                        if (!items.TryGetValue(childId, out ITimelineItem child))
                            throw new MetaAssertException($"{itemId} has child {childId} which is not found among the items");
                        if (child.Hierarchy.ParentId != itemId)
                            throw new MetaAssertException($"{itemId} has child {itemId} which has different parent {child.Hierarchy.ParentId}");
                    }
                }
            }

            UpdateCursor timelineUpdateCursor = new UpdateCursor(
                entrySequentialId:
                    timelineState.UpdateHistoryRunningId,
                previousEntryUniqueId:
                    timelineState.UpdateHistory.Count > 0
                    ? timelineState.UpdateHistory[^1].UniqueId
                    : timelineState.UpdateHistoryOldestPreviousUniqueId);

            return new GetLiveOpsTimelineDataResponse
            {
                TimelineData = new TimelineData
                {
                    StartInstantIsoString = firstInstant.ToISO8601(),
                    EndInstantIsoString = lastInstant.ToISO8601(),
                    Items = items,
                },
                UpdateCursor = new TimelineDataUpdateCursor
                {
                    FirstInstant = firstInstant,
                    LastInstant = lastInstant,

                    TimelineUpdateCursor = timelineUpdateCursor,
                    ServerErrorsStartExclusive = EnableServerErrors && recentLoggedErrorsResponse.ErrorsDetails.Length > 0
                                                 ? recentLoggedErrorsResponse.ErrorsDetails[^1].Timestamp
                                                 : MetaTime.FromMillisecondsSinceEpoch(long.MinValue),
                }
            };
        }

        const string TimelinePlainDateTimeFormatString = "yyyy-MM-ddTHH\\:mm\\:ss.FFFFFFF";
        const string TimelineInstantDateTimeFormatString = "yyyy-MM-ddTHH\\:mm\\:ss.FFFFFFFZ";

        TimelineItemLiveopsEvent CreateTimelineItemLiveopsEvent(LiveOpsEventOccurrence occurrence, LiveOpsEventSpec spec, LiveOpsEventOccurrenceStatistics statistics, Element elementState, MetaTime currentTime)
        {
            LiveOpsEventPhase currentPhase = GetCurrentPhase(occurrence);
            (MetaTime startTime, MetaTime? endTime) = LiveOpsEventServerUtil.GetVisibleTimeRange(occurrence, currentTime);

            string state = occurrence.TimeState.GetLifeStage() switch
            {
                LiveOpsEventLifeStage.Upcoming => "scheduled",
                LiveOpsEventLifeStage.Ongoing => "active",
                LiveOpsEventLifeStage.Concluded => "concluded",
                (LiveOpsEventLifeStage other) => throw new MetaAssertException($"Unhandled {nameof(LiveOpsEventLifeStage)}: {other}"),
            };

            MetaRecurringCalendarSchedule metaScheduleMaybe = (MetaRecurringCalendarSchedule)spec.Settings.ScheduleMaybe;

            TimelineLiveOpsEventSchedule schedule;
            if (metaScheduleMaybe == null)
                schedule = null;
            else if (metaScheduleMaybe.TimeMode == MetaScheduleTimeMode.Local)
            {
                schedule = new TimelineLiveOpsEventScheduleLocal
                {
                    PlainTimeStartInstantIsoString = metaScheduleMaybe.Start.ToDateTime().ToString(TimelineInstantDateTimeFormatString, CultureInfo.InvariantCulture),
                    PlainTimeEndInstantIsoString = metaScheduleMaybe.Duration.AddToDateTime(metaScheduleMaybe.Start.ToDateTime()).ToString(TimelineInstantDateTimeFormatString, CultureInfo.InvariantCulture),
                    PreviewDurationIsoString = NullPeriodIfZero(metaScheduleMaybe.Preview)?.ToISO8601String(),
                    ReviewDurationIsoString = NullPeriodIfZero(metaScheduleMaybe.Review)?.ToISO8601String(),
                };
            }
            else
            {
                string phase;
                if (currentPhase == LiveOpsEventPhase.NotStartedYet)
                    phase = null;
                else if (currentPhase == LiveOpsEventPhase.Preview)
                    phase = "preview";
                else if (currentPhase == LiveOpsEventPhase.NormalActive)
                    phase = "active";
                else if (currentPhase == LiveOpsEventPhase.EndingSoon)
                    phase = "endingSoon";
                else if (currentPhase == LiveOpsEventPhase.Review)
                    phase = "review";
                else if (currentPhase == LiveOpsEventPhase.Concluded)
                    phase = null;
                else
                    phase = null;

                schedule = new TimelineLiveOpsEventScheduleUtc
                {
                    CurrentPhase = phase,
                    PreviewDurationIsoString = NullPeriodIfZero(metaScheduleMaybe.Preview)?.ToISO8601String(),
                    ReviewDurationIsoString = NullPeriodIfZero(metaScheduleMaybe.Review)?.ToISO8601String(),
                };
            }

            return new TimelineItemLiveopsEvent
            {
                Version = elementState.Version,
                Hierarchy = new TimelineItemHierarchyInfo
                {
                    ParentId = $"node:{elementState.RowId}",
                    ChildIds = null,
                },
                Metadata = new TimelineItemLiveopsEvent.MetadataData
                {
                    DisplayName = occurrence.EventParams.DisplayName,
                    Color = occurrence.EventParams.Color,
                },
                RenderData = new TimelineItemLiveopsEvent.RenderDataData
                {
                    TimelinePosition = new TimelineLiveOpsEventPosition
                    {
                        StartInstantIsoString = startTime.ToISO8601(),
                        EndInstantIsoString = endTime?.ToISO8601(),
                    },
                    State = state,
                    Schedule = schedule,
                    IsLocked = false,
                    IsTargeted = !occurrence.EventParams.PlayerFilter.IsEmpty,
                    IsRecurring = metaScheduleMaybe?.Recurrence.HasValue ?? false,
                    IsImmutable = false,
                    ParticipantCount = statistics.ParticipantCount,
                },
                IsImmutable = false,
            };
        }

        public class GetLiveOpsTimelineDataResponse
        {
            public required TimelineData TimelineData;
            public required TimelineDataUpdateCursor UpdateCursor;
        }

        public class TimelineData
        {
            public required string StartInstantIsoString;
            public required string EndInstantIsoString;
            public required Dictionary<string, ITimelineItem> Items;
        }

        [HttpPost("getLiveOpsTimelineDataUpdates")]
        [RequirePermission(MetaplayPermissions.ApiLiveOpsEventsView)] // \todo #timeline More granular permissions - different types of timeline items can have different permissions
        public async Task<ActionResult<GetLiveOpsTimelineDataUpdatesResponse>> GetLiveOpsTimelineDataUpdates([FromBody] GetLiveOpsTimelineDataUpdatesRequest request)
        {
            MetaTime currentTime = MetaTime.Now;
            GetLiveOpsTimelineUpdatesResponse timelineUpdatesResponse = await EntityAskAsync(LiveOpsTimelineManager.EntityId, new GetLiveOpsTimelineUpdatesRequest(request.Cursor.TimelineUpdateCursor, firstInstant: request.Cursor.FirstInstant, lastInstant: request.Cursor.LastInstant, currentTime: currentTime));
            if (!timelineUpdatesResponse.IsSuccess)
            {
                return new GetLiveOpsTimelineDataUpdatesResponse
                {
                    IsSuccess = timelineUpdatesResponse.IsSuccess,
                    Error = timelineUpdatesResponse.Error,

                    ItemUpdates = null,
                    ContinuationCursor = null,
                };
            }

            OrderedSet<MetaGuid> liveOpsOccurrenceIds = new();
            foreach (GetLiveOpsTimelineUpdatesResponse.Item item in timelineUpdatesResponse.Updates.Values)
            {
                if (item == null)
                    continue;
                if (item.LiveOpsEvent == null)
                    continue;

                liveOpsOccurrenceIds.Add(item.LiveOpsEvent.Occurrence.OccurrenceId);
            }

            StatsCollectorLiveOpsEventStatisticsRequest statisticsRequest = new StatsCollectorLiveOpsEventStatisticsRequest(liveOpsOccurrenceIds);
            StatsCollectorLiveOpsEventStatisticsResponse statisticsResponse = await EntityAskAsync(StatsCollectorManager.EntityId, statisticsRequest);

            // Start constructing itemUpdates.
            // A null value in itemUpdates means the item was removed or is not within the requested time window.

            Dictionary<string, ITimelineItem> itemUpdates = new();

            // Add itemUpdates entries based on the timelineUpdatesResponse.
            // - Node (section/group/row) changes/removals as well as element changes/removals.
            // - Does not include synthesized data, e.g. server errors (that's done later in this method).

            foreach ((ItemId itemId, GetLiveOpsTimelineUpdatesResponse.Item responseItem) in timelineUpdatesResponse.Updates)
            {
                ITimelineItem item;

                if (responseItem == null)
                    item = null;
                else
                {
                    if (itemId is ItemId.Node(MetaGuid nodeId))
                    {
                        Node node = ((ItemState.NodeState)responseItem.ItemState).Node;

                        item = new TimelineItemNode
                        {
                            Version = node.Version,
                            ItemType = node.NodeType.ToDashboardString(),
                            Hierarchy = new TimelineItemHierarchyInfo
                            {
                                ParentId = node.NodeType == NodeType.Root ? null : $"node:{node.ParentId}",
                                ChildIds = node.ChildNodes?.Select(id => $"node:{id}").ToOrderedSet()
                                           ?? responseItem.RowChildElements.Select(id => id.ToString()).ToOrderedSet(),
                            },
                            Metadata = new TimelineItemNode.MetadataData
                            {
                                DisplayName = node.DisplayName,
                                Color = node.Color,
                            },
                            RenderData = new TimelineItemNode.RenderDataData
                            {
                                CannotRemoveReason = node.IsDefaultRow ? "Cannot remove the default row."
                                                   : node.ChildNodes != null && node.ChildNodes.Count > 0 ? $"Cannot remove a non-empty {node.NodeType.ToLowercaseHumanString()}."
                                                   : responseItem.RowHasChildElementsIgnoringTimeRange ? "Cannot remove a non-empty row."
                                                   : null,
                            },
                            IsImmutable = node.IsDefaultRow,
                        };
                    }
                    else if (itemId is ItemId.Element(ElementId elementId))
                    {
                        Element element = ((ItemState.ElementState)responseItem.ItemState).Element;

                        if (elementId is ElementId.LiveOpsEvent)
                        {
                            LiveOpsEventOccurrence occurrence = responseItem.LiveOpsEvent.Occurrence;
                            LiveOpsEventSpec spec = responseItem.LiveOpsEvent.Spec;
                            LiveOpsEventOccurrenceStatistics statistics = statisticsResponse.Statistics[occurrence.OccurrenceId];
                            TimelineItemLiveopsEvent liveopsEvent = CreateTimelineItemLiveopsEvent(occurrence, spec, statistics, element, currentTime);
                            item = liveopsEvent;
                        }
                        else
                            throw new MetaAssertException($"Unhandled element type {elementId?.GetType().ToString() ?? "<null>"}");
                    }
                    else
                        throw new MetaAssertException($"Unhandled item type {itemId?.GetType().ToString() ?? "<null>"}");
                }

                itemUpdates[itemId.ToString()] = item;
            }

            RecentLoggedErrorsResponse recentLoggedErrorsResponse;
            if (EnableServerErrors)
            {
                // Add itemUpdates entries based on new server errors.
                // - The new errors are added as elements.
                // - If there are any new errors, then the "server errors" row also needs to be updated (for its child list).

                recentLoggedErrorsResponse = await EntityAskAsync(StatsCollectorManager.EntityId, RecentLoggedErrorsRequest.Instance);

                if (recentLoggedErrorsResponse.ErrorsDetails.Any(err => err.Timestamp > request.Cursor.ServerErrorsStartExclusive))
                {
                    TimelineItemNode serverErrorsRow = new TimelineItemNode
                    {
                        Version = 0,
                        ItemType = NodeType.Row.ToDashboardString(),
                        Hierarchy = new TimelineItemHierarchyInfo
                        {
                            ParentId = ServerErrorsGroupId,
                            ChildIds = new OrderedSet<string>(),
                        },
                        Metadata = new TimelineItemNode.MetadataData
                        {
                            DisplayName = ServerErrorsRowDisplayName,
                            Color = null,
                        },
                        RenderData = new TimelineItemNode.RenderDataData
                        {
                            CannotRemoveReason = "This built-in item cannot be removed.",
                        },
                        IsImmutable = true,
                    };

                    foreach (RecentLogEventCounter.LogEventInfo errorDetails in recentLoggedErrorsResponse.ErrorsDetails)
                    {
                        ElementId elementId = new ElementId.ServerError(errorDetails.Id);
                        serverErrorsRow.Hierarchy.ChildIds.Add(elementId.ToString());

                        if (errorDetails.Timestamp > request.Cursor.ServerErrorsStartExclusive)
                        {
                            TimelineItemInstantEvent item = new TimelineItemInstantEvent
                            {
                                Version = 0,
                                Hierarchy = new TimelineItemHierarchyInfo
                                {
                                    ParentId = ServerErrorsRowId,
                                    ChildIds = null,
                                },
                                RenderData = new TimelineItemInstantEvent.RenderDataData
                                {
                                    InstantIsoString = errorDetails.Timestamp.ToISO8601(),
                                    Color = ServerErrorElementColor,
                                },
                                IsImmutable = true,
                            };
                            itemUpdates.Add(elementId.ToString(), item);
                        }
                    }

                    itemUpdates.Add(ServerErrorsRowId, serverErrorsRow);
                }
            }
            else
                recentLoggedErrorsResponse = null;

            // Assert validity of ParentId and ChildIds.
            // Can't generally check inter-item child<->parent consistency,
            // because itemUpdates doesn't contain all the items in the timeline.
            foreach ((string itemId, ITimelineItem item) in itemUpdates)
            {
                if (item == null)
                    continue;

                if (item.ItemType == NodeType.Root.ToDashboardString())
                {
                    if (item.Hierarchy.ParentId != null)
                        throw new MetaAssertException($"{itemId} has parent id, but shouldn't because it's the root node");
                }
                else
                {
                    if (item.Hierarchy.ParentId == null)
                        throw new MetaAssertException($"{itemId} is missing a parent id");
                }

                if (item is ITimelineElement)
                {
                    if (item.Hierarchy.ChildIds != null)
                        throw new MetaAssertException($"{itemId} has child id list, but shouldn't because it's an Element (not Node)");
                }
                else
                {
                    if (item.Hierarchy.ChildIds == null)
                        throw new MetaAssertException($"{itemId} is missing the child id list");
                }
            }

            return new GetLiveOpsTimelineDataUpdatesResponse
            {
                IsSuccess = true,
                Error = null,

                ItemUpdates = itemUpdates,
                ContinuationCursor = new TimelineDataUpdateCursor
                {
                    FirstInstant = request.Cursor.FirstInstant,
                    LastInstant = request.Cursor.LastInstant,

                    TimelineUpdateCursor = timelineUpdatesResponse.ContinuationCursor,

                    ServerErrorsStartExclusive = EnableServerErrors && recentLoggedErrorsResponse.ErrorsDetails.Length > 0
                                                 ? MetaTime.Max(request.Cursor.ServerErrorsStartExclusive, recentLoggedErrorsResponse.ErrorsDetails[^1].Timestamp)
                                                 : request.Cursor.ServerErrorsStartExclusive,
                }
            };
        }

        public class GetLiveOpsTimelineDataUpdatesRequest
        {
            [JsonRequired] public TimelineDataUpdateCursor Cursor;
        }

        // \note MetaSerializable here is a hack to allow deserializing this from JSON with $type.
        //       Our $type binder requires the type to be MetaSerializable.
        //       We don't really care about the $type here, we might as well omit it from the serialization,
        //       but that does not seem easy to achieve.
        [MetaSerializable, MetaAllowNoSerializedMembers]
        public class TimelineDataUpdateCursor
        {
            [JsonRequired] public MetaTime FirstInstant;
            [JsonRequired] public MetaTime LastInstant;

            [JsonRequired] public UpdateCursor TimelineUpdateCursor;

            // \todo #timeline Not totally robust since timestamps are not guaranteed to be consistent.
            [JsonRequired] public MetaTime ServerErrorsStartExclusive;
        }

        public class GetLiveOpsTimelineDataUpdatesResponse
        {
            public required bool IsSuccess;
            public required string Error;

            public required Dictionary<string, ITimelineItem> ItemUpdates; // null value means item was removed or is outside the requested time window
            public required TimelineDataUpdateCursor ContinuationCursor;
        }

        public interface ITimelineItem
        {
            string ItemType { get; }
            uint Version { get; }
            TimelineItemHierarchyInfo Hierarchy { get; }
            object Metadata { get; }
            object RenderData { get; }
            bool IsImmutable { get; }
        }

        public class TimelineItemHierarchyInfo
        {
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public required string ParentId;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public required OrderedSet<string> ChildIds;
        }

        public class TimelineItemNode : ITimelineItem
        {
            public required string ItemType { get; set; }

            public required uint Version { get; set; }
            public required TimelineItemHierarchyInfo Hierarchy { get; set; }
            public required MetadataData Metadata { get; set; }
            [JsonIgnore] object ITimelineItem.Metadata => Metadata;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public required RenderDataData RenderData { get; set; }
            [JsonIgnore] object ITimelineItem.RenderData => RenderData;
            public required bool IsImmutable { get; set; }

            public class MetadataData
            {
                [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
                public required string DisplayName;

                [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
                public required string Color;
            }

            public class RenderDataData
            {
                public string CannotRemoveReason;
            }
        }

        public interface ITimelineElement : ITimelineItem
        {
        }

        public class TimelineItemLiveopsEvent : ITimelineElement
        {
            public string ItemType => ElementTypeStrings.LiveOpsEvent;

            public required uint Version { get; set; }
            public required TimelineItemHierarchyInfo Hierarchy { get; set; }
            public required MetadataData Metadata { get; set; }
            [JsonIgnore] object ITimelineItem.Metadata => Metadata;
            public required RenderDataData RenderData { get; set; }
            [JsonIgnore] object ITimelineItem.RenderData => RenderData;
            public required bool IsImmutable { get; set; }

            public class MetadataData
            {
                public required string DisplayName;

                [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
                public required string Color;
            }

            public class RenderDataData
            {
                public required TimelineLiveOpsEventPosition TimelinePosition;
                public required string State;
                public required TimelineLiveOpsEventSchedule Schedule;
                public required bool IsLocked;
                public required bool IsTargeted;
                public required bool IsRecurring;
                public required bool IsImmutable;
                public required int? ParticipantCount;
            }
        }

        public class TimelineLiveOpsEventPosition
        {
            public string StartInstantIsoString = null;
            public string EndInstantIsoString = null;
        }

        public abstract class TimelineLiveOpsEventSchedule
        {
            public abstract string TimeMode { get; }
        }

        public class TimelineLiveOpsEventScheduleUtc : TimelineLiveOpsEventSchedule
        {
            public override string TimeMode => "utc";

            public string CurrentPhase = null;
            public string PreviewDurationIsoString = null;
            public string ReviewDurationIsoString = null;
        }

        public class TimelineLiveOpsEventScheduleLocal : TimelineLiveOpsEventSchedule
        {
            public override string TimeMode => "playerLocal";

            public string PlainTimeStartInstantIsoString = null;
            public string PlainTimeEndInstantIsoString = null;
            public string PreviewDurationIsoString = null;
            public string ReviewDurationIsoString = null;
        }

        public class TimelineItemInstantEvent : ITimelineElement
        {
            public string ItemType => "instantEvent";

            public required uint Version { get; set; }
            public required TimelineItemHierarchyInfo Hierarchy { get; set; }
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public object Metadata => null;
            public required RenderDataData RenderData { get; set; }
            [JsonIgnore] object ITimelineItem.RenderData => RenderData;
            public required bool IsImmutable { get; set; }

            public class RenderDataData
            {
                public required string InstantIsoString;
                public required string Color;
            }
        }

        MetaCalendarPeriod? NullPeriodIfZero(MetaCalendarPeriod period)
        {
            return period.IsNone ? null : period;
        }

        [HttpPost("liveOpsTimelineItemDetails")]
        [RequirePermission(MetaplayPermissions.ApiLiveOpsEventsView)] // \todo #timeline More granular permissions - different types of timeline items can have different permissions
        public async Task<ActionResult<GetLiveOpsTimelineItemDetailsResponse>> GetLiveOpsTimelineItemDetails([FromBody] GetLiveOpsTimelineItemDetailsBody body)
        {
            OrderedSet<string> serverErrorsNodeIds = new();
            OrderedSet<MetaGuid> nodeIds = new();
            OrderedSet<MetaGuid> liveOpsEventOccurrenceIds = new();
            OrderedSet<MetaGuid> serverErrorIds = new();

            foreach (string itemIdString in body.ItemIds)
            {
                if (itemIdString.StartsWith(ServerErrorsNodeIdPrefix, StringComparison.Ordinal))
                    serverErrorsNodeIds.Add(itemIdString);
                else
                {
                    ItemId itemId = ItemId.Parse(itemIdString);
                    if (itemId is ItemId.Node(MetaGuid nodeId))
                        nodeIds.Add(nodeId);
                    else if (itemId is ItemId.Element(ElementId elementId))
                    {
                        if (elementId is ElementId.LiveOpsEvent(MetaGuid occurrenceId))
                            liveOpsEventOccurrenceIds.Add(occurrenceId);
                        else if (elementId is ElementId.ServerError(MetaGuid serverErrorId))
                            serverErrorIds.Add(serverErrorId);
                        else
                            throw new MetaAssertException($"Unhandled element id type: {elementId?.GetType().ToString() ?? "<null>"}");
                    }
                    else if (itemId == null)
                        throw new MetaplayHttpException(HttpStatusCode.BadRequest, "Null item id not allowed.", "Item details request cannot contain null item ids.");
                    else
                        throw new MetaAssertException($"Unhandled item id type: {itemId?.GetType().ToString() ?? "<null>"}");
                }
            }

            MetaTime currentTime = MetaTime.Now;

            Dictionary<string, ITimelineItemDetails> items = new();

            GetLiveOpsEventsResponse eventsResponse;

            if (nodeIds.Count > 0 || liveOpsEventOccurrenceIds.Count > 0)
                eventsResponse = await EntityAskAsync(LiveOpsTimelineManager.EntityId, new GetLiveOpsEventsRequest(liveOpsEventOccurrenceIds, getTimelineData: nodeIds.Count > 0));
            else
                eventsResponse = null;

            if (nodeIds.Count > 0)
            {
                foreach (MetaGuid nodeId in nodeIds)
                {
                    Node node = eventsResponse.TimelineStateMaybe.Nodes[nodeId];

                    object details;
                    if (node.NodeType.HasMetadataField(ItemMetadataField.Color))
                        details = new { Color = node.Color };
                    else
                        details = new { };

                    items.Add(new ItemId.Node(nodeId).ToString(), new TimelineItemDetailsNode
                    {
                        ItemType = node.NodeType.ToDashboardString(),
                        DisplayName = node.DisplayName,
                        Description = node.Description,
                        Details = details,
                    });
                }
            }

            if (liveOpsEventOccurrenceIds.Count > 0)
            {
                MetaDictionary<MetaGuid, LiveOpsEventSpec> eventSpecs = eventsResponse.Specs.ToMetaDictionary(spec => spec.SpecId);
                StatsCollectorLiveOpsEventStatisticsRequest eventStatisticsRequest = new StatsCollectorLiveOpsEventStatisticsRequest(liveOpsEventOccurrenceIds);
                StatsCollectorLiveOpsEventStatisticsResponse eventStatisticsResponse = await EntityAskAsync(StatsCollectorManager.EntityId, eventStatisticsRequest);

                foreach (LiveOpsEventOccurrence occurrence in eventsResponse.Occurrences)
                {
                    LiveOpsEventSpec spec = eventSpecs[occurrence.DefiningSpecId];
                    LiveOpsEventOccurrenceStatistics statistics = eventStatisticsResponse.Statistics[occurrence.OccurrenceId];

                    ElementId.LiveOpsEvent elementId = new ElementId.LiveOpsEvent(occurrence.OccurrenceId);
                    items.Add(new ItemId.Element(elementId).ToString(), CreateTimelineItemDetailsLiveopsEvent(occurrence, spec, statistics, currentTime));
                }
            }

            if (serverErrorIds.Count > 0)
            {
                RecentLoggedErrorsResponse recentLoggedErrorsResponse = await EntityAskAsync(StatsCollectorManager.EntityId, RecentLoggedErrorsRequest.Instance);
                foreach (RecentLogEventCounter.LogEventInfo errorDetails in recentLoggedErrorsResponse.ErrorsDetails)
                {
                    if (!serverErrorIds.Contains(errorDetails.Id))
                        continue;

                    ElementId.ServerError elementId = new ElementId.ServerError(errorDetails.Id);
                    items.Add(new ItemId.Element(elementId).ToString(), new TimelineItemDetailsInstantEvent
                    {
                        DisplayName = string.IsNullOrEmpty(errorDetails.Source) ? "Server Error" : $"{errorDetails.Source} Error",
                        Description = errorDetails.Message,
                        Details = errorDetails,
                    });
                }
            }

            foreach (string nodeId in serverErrorsNodeIds)
            {
                TimelineItemDetailsNode item;

                if (nodeId == ServerErrorsSectionId)
                {
                    item = new TimelineItemDetailsNode
                    {
                        ItemType = NodeType.Section.ToDashboardString(),
                        DisplayName = ServerErrorsSectionDisplayName,
                        Description = ServerErrorsSectionDescription,
                        Details = new { },
                    };
                }
                else if (nodeId == ServerErrorsGroupId)
                {
                    item = new TimelineItemDetailsNode
                    {
                        ItemType = NodeType.Group.ToDashboardString(),
                        DisplayName = ServerErrorsGroupDisplayName,
                        Description = ServerErrorsGroupDescription,
                        Details = new { Color = ServerErrorsGroupColor },
                    };
                }
                else if (nodeId == ServerErrorsRowId)
                {
                    item = new TimelineItemDetailsNode
                    {
                        ItemType = NodeType.Row.ToDashboardString(),
                        DisplayName = ServerErrorsRowDisplayName,
                        Description = ServerErrorsRowDescription,
                        Details = new { },
                    };
                }
                else
                    throw new MetaAssertException($"Unhandled server error node id {nodeId}");

                items.Add(nodeId, item);
            }

            return new GetLiveOpsTimelineItemDetailsResponse
            {
                Items = items,
            };
        }

        TimelineItemDetailsLiveOpsEvent CreateTimelineItemDetailsLiveopsEvent(LiveOpsEventOccurrence occurrence, LiveOpsEventSpec spec, LiveOpsEventOccurrenceStatistics statistics, MetaTime currentTime)
        {
            (LiveOpsEventPhase currentPhase, LiveOpsEventPhase nextPhase, MetaTime? nextPhaseTime) = GetEventCurrentAndNextPhaseInfo(occurrence);

            return new TimelineItemDetailsLiveOpsEvent
            {
                DisplayName = occurrence.EventParams.DisplayName,
                Description = occurrence.EventParams.Description,
                Details = new TimelineItemDetailsLiveOpsEvent.DetailsData
                {
                    EventId = occurrence.OccurrenceId,
                    EventTypeName = LiveOpsEventTypeRegistry.GetEventTypeInfo(occurrence.EventParams.Content.GetType()).EventTypeName,
                    EventParams = EditableParamsFromOccurrenceAndSpec(occurrence, spec),
                    CurrentPhase = TryConvertEventPhase(currentPhase).Value,
                    NextPhase = TryConvertEventPhase(nextPhase),
                    NextPhaseTime = nextPhaseTime,
                    ParticipantCount = statistics.ParticipantCount,
                },
            };
        }

        public class GetLiveOpsTimelineItemDetailsBody
        {
            public List<string> ItemIds;
        }

        public class GetLiveOpsTimelineItemDetailsResponse
        {
            public Dictionary<string, ITimelineItemDetails> Items;
        }

        public interface ITimelineItemDetails
        {
            string ItemType { get; }
            string DisplayName { get; }
            string Description { get; }
            object Details { get; }
        }

        public class TimelineItemDetailsLiveOpsEvent : ITimelineItemDetails
        {
            public string ItemType => ElementTypeStrings.LiveOpsEvent;
            public required string DisplayName { get; set; }
            public required string Description { get; set; }
            public required DetailsData Details { get; set; }
            [JsonIgnore] object ITimelineItemDetails.Details => Details;

            /// <summary>
            /// \note Mostly same data as in <see cref="EventDetailsInfo"/>.
            /// </summary>
            public class DetailsData
            {
                public required MetaGuid EventId;
                public required string EventTypeName;
                public required EditableEventParams EventParams;

                public required EventPhase CurrentPhase;
                public required EventPhase? NextPhase;
                public required MetaTime? NextPhaseTime;
                public required int ParticipantCount;
            }
        }

        public class TimelineItemTargetingParameters
        {
            public required List<EntityId> TargetPlayers;
            public required PlayerCondition TargetCondition;
        }

        public class TimelineItemDetailsInstantEvent : ITimelineItemDetails
        {
            public string ItemType => "instantEvent";
            public required string DisplayName { get; set; }
            public required string Description { get; set; }
            public required object Details { get; set; }
        }

        public class TimelineItemDetailsNode : ITimelineItemDetails
        {
            public required string ItemType { get; set; }
            public required string DisplayName { get; set; }
            public required string Description { get; set; }
            public required object Details { get; set; }
        }

        #endregion

        [HttpPost("invokeLiveOpsTimelineCommand")]
        [RequirePermission(MetaplayPermissions.ApiLiveOpsEventsEdit)] // \todo #timeline More granular permissions - different types of timeline items can have different permissions
        public async Task<ActionResult> InvokeLiveOpsTimelineCommand([FromBody] InvokeLiveOpsTimelineCommandBody body)
        {
            Command command = ParseCommand(body.Command);

            try
            {
                command.Validate();
            }
            catch (Command.ValidationException ex)
            {
                throw new MetaplayHttpException(HttpStatusCode.BadRequest, "Invalid command.", ex.Message);
            }

            InvokeLiveOpsTimelineCommandResponse response = await EntityAskAsync(LiveOpsTimelineManager.EntityId, new InvokeLiveOpsTimelineCommandRequest(command));
            if (!response.IsSuccess)
                throw new MetaplayHttpException(ToHttpStatusCode(response.ErrorCode), "Command failed.", response.ErrorMessage);

            await WriteRelatedAuditLogEventsAsync(
                response.AuditLogEvents
                .Select(eventPayload =>
                {
                    EventBuilder builder = new LiveOpsTimelineItemAuditLogEventBuilder(eventPayload);
                    return builder;
                })
                .ToList());

            return Ok();
        }

        public class InvokeLiveOpsTimelineCommandBody
        {
            public JObject Command;
        }

        Command ParseCommand(JObject jCommand)
        {
            if (!jCommand.TryGetValue("commandType", out JToken jCommandType))
                throw new MetaplayHttpException(HttpStatusCode.BadRequest, "Malformed command.", "Command is missing commandType.");
            if (jCommandType.Type != JTokenType.String)
                throw new MetaplayHttpException(HttpStatusCode.BadRequest, "Malformed command.", $"commandType is a {jCommandType.Type}, but should be a String.");
            string commandTypeStr = (string)((JValue)jCommandType).Value;

            Type commandType = Command.TryGetCommandTypeByName(commandTypeStr);
            if (commandType == null)
                throw new MetaplayHttpException(HttpStatusCode.BadRequest, "Malformed command.", $"Unknown command type {commandTypeStr}.");

            try
            {
                return (Command)jCommand.ToObject(commandType, AdminApiJsonSerialization.Serializer);
            }
            catch (Exception ex)
            {
                throw new MetaplayHttpException(HttpStatusCode.BadRequest, "Failed to parse command.", ex.Message);
            }
        }

        static HttpStatusCode ToHttpStatusCode(Command.ErrorCode commandErrorCode)
        {
            switch (commandErrorCode)
            {
                case Command.ErrorCode.InternalServerError: return HttpStatusCode.InternalServerError;
                case Command.ErrorCode.NotFound: return HttpStatusCode.NotFound;
                case Command.ErrorCode.Conflict: return HttpStatusCode.Conflict;
                case Command.ErrorCode.UnprocessableContent: return HttpStatusCode.UnprocessableContent;
                default:
                    throw new MetaAssertException($"Unhandled {nameof(Command)}.{nameof(Command.ErrorCode)}: {commandErrorCode}");
            }
        }
    }
}
