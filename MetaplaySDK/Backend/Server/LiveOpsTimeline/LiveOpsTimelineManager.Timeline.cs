// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Entity;
using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.Serialization;
using Metaplay.Core.TypeCodes;
using Metaplay.Server.LiveOpsTimeline.Timeline;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using static System.FormattableString;

namespace Metaplay.Server.LiveOpsTimeline
{
    namespace Timeline
    {
        [MetaSerializable]
        public class TimelineState
        {
            [MetaMember(6)] public MetaDictionary<MetaGuid, Node> Nodes = new();
            [MetaMember(7)] public MetaDictionary<ElementId, Element> Elements = new();

            // \note Update history is transient on purpose:
            //       - Don't need to worry about Update's backwards compatibility.
            //       - It is not currently important to be able to keep history across server restarts.
            //       The protocol doesn't promise that it will keep history forever anyway.
            [MetaMember(8), Transient] public List<UpdateEntry> UpdateHistory = new();
            [MetaMember(10), Transient] public MetaGuid UpdateHistoryOldestPreviousUniqueId = MetaGuid.None;
            public const int UpdateHistoryMaxCount = 100;
            [MetaMember(9)] public int UpdateHistoryRunningId = 0;

            [MetaMember(11), Transient] public MetaGuid DefaultRowId;

            TimelineState() { }

            [MetaOnDeserialized]
            void OnDeserialized()
            {
                // Node.ParentId is not persisted (as it's redundant), so we compute them after deserialization.
                foreach (Node node in Nodes.Values)
                {
                    if (node.ChildNodes == null)
                        continue;

                    foreach (MetaGuid childId in node.ChildNodes)
                        Nodes[childId].ParentId = node.Id;
                }

                // For historical data where Node.IsDefaultRow did not exist, mark it now.
                // \todo This was pre-release (pre-R31) so could be removed eventually.
                if (!Nodes.Values.Any(node => node.IsDefaultRow))
                    Nodes.Values.First(node => node.NodeType == NodeType.Row).IsDefaultRow = true;

                // DefaultRowId is redundant data, compute it here.
                DefaultRowId = Nodes.Values.Single(node => node.IsDefaultRow).Id;

                // Legacy data fixes. Pre-release (rre-R31), remove eventually.
                {
                    Node firstSection = Nodes.Values.First(node => node.NodeType == NodeType.Section);
                    if (firstSection.DisplayName == "Default section")
                        firstSection.DisplayName = InitialSectionName;
                }
                {
                    Node firstGroup = Nodes.Values.First(node => node.NodeType == NodeType.Group);
                    if (firstGroup.DisplayName == "Default group")
                        firstGroup.DisplayName = InitialGroupName;
                }
                {
                    Node firstRow = Nodes.Values.First(node => node.NodeType == NodeType.Row);
                    if (firstRow.DisplayName == "Default row")
                        firstRow.DisplayName = InitialRowName;
                }
            }

            const string InitialSectionName = "LiveOps Events";
            const string InitialGroupName = "Default Group";
            const string InitialRowName = "Default Row";

            public static TimelineState CreateInitialState()
            {
                TimelineState timelineState = new();

                Node root = Node.CreateDefaultState(MetaGuid.New(), NodeType.Root);
                timelineState.Nodes.Add(root.Id, root);

                Node section = Node.CreateDefaultState(MetaGuid.New(), NodeType.Section);
                section.DisplayName = InitialSectionName;
                root.ChildNodes.Add(section.Id);
                section.ParentId = root.Id;
                timelineState.Nodes.Add(section.Id, section);

                Node group = Node.CreateDefaultState(MetaGuid.New(), NodeType.Group);
                group.DisplayName = InitialGroupName;
                section.ChildNodes.Add(group.Id);
                group.ParentId = section.Id;
                timelineState.Nodes.Add(group.Id, group);

                Node row = Node.CreateDefaultState(MetaGuid.New(), NodeType.Row);
                row.DisplayName = InitialRowName;
                row.Description = "This is where LiveOps Events go initially until you move them elsewhere. This row cannot be removed.";
                row.IsDefaultRow = true;
                group.ChildNodes.Add(row.Id);
                row.ParentId = group.Id;
                timelineState.Nodes.Add(row.Id, row);

                timelineState.DefaultRowId = row.Id;

                return timelineState;
            }
        }

        [MetaSerializable]
        public class Node
        {
            [MetaMember(1)] public MetaGuid Id { get; private set; }
            [MetaMember(6)] public uint Version { get; internal set; }
            [MetaMember(2)] public NodeType NodeType { get; private set; }
            /// <summary>
            /// Whether this is the special "default row", i.e. the row that was in the initial timeline state.
            /// The default row is the row to which elements (events etc) belong to implicitly until they're
            /// explicitly assigned elsewhere.
            /// The default row cannot be removed, even if it's empty.
            /// </summary>
            [MetaMember(8)] public bool IsDefaultRow { get; set; }
            /// <remarks>
            /// Note: be sure to keep this in sync with children's <see cref="ParentId"/>!
            /// <para>
            /// Not used for NodeType.Row. The elements inside rows know their parents instead.
            /// </para>
            /// </remarks>
            [MetaMember(3)] public List<MetaGuid> ChildNodes { get; set; }
            /// <remarks>
            /// Note: be sure to keep this in sync with parent's <see cref="ChildNodes"/>!
            /// <para>
            /// Transient, since within the full <see cref="TimelineState"/> it is deducible from the other nodes' ChildNodes.
            /// Set in <see cref="TimelineState.OnDeserialized"/>, and updated in <see cref="TimelineState.CreateInitialState"/> and in edits.
            /// </para>
            /// <para>
            /// Not used for <see cref="NodeType.Root"/>.
            /// </para>
            /// </remarks>
            [MetaMember(7), Transient] public MetaGuid ParentId { get; internal set; }
            [MetaMember(4)] public string DisplayName { get; set; }
            [MetaMember(9)] public string Description { get; set; } = "";
            /// <remarks>
            /// Color is actually used only for some NodeTypes (see <see cref="NodeTypeExtensions.HasMetadataField"/>).
            /// <para>
            /// Even for nodes that use color, it can be null, in which case it just defaults to something in the UI.
            /// </para>
            /// </remarks>
            [MetaMember(5)] public string Color { get; set; }

            Node() { }
            public Node(MetaGuid id, uint version, NodeType nodeType, bool isDefaultRow, List<MetaGuid> childNodes, string displayName, string description, string color)
            {
                Id = id;
                Version = version;
                NodeType = nodeType;
                IsDefaultRow = isDefaultRow;
                ChildNodes = childNodes;
                DisplayName = displayName;
                Description = description;
                Color = color;
            }

            public static Node CreateDefaultState(MetaGuid id, NodeType nodeType)
            {
                return new Node(
                    id,
                    version: 0,
                    nodeType,
                    isDefaultRow: false,
                    nodeType.HasChildNodes() ? new List<MetaGuid>() : null,
                    displayName: nodeType.HasMetadataField(ItemMetadataField.DisplayName) ? $"Unnamed {nodeType.ToCapitalizedHumanString()}" : null,
                    description: "",
                    color: null);
            }

            public string GetMetadataField(ItemMetadataField field)
            {
                switch (field)
                {
                    case ItemMetadataField.DisplayName: return DisplayName;
                    case ItemMetadataField.Description: return Description;
                    case ItemMetadataField.Color: return Color;
                    default: throw new ArgumentException($"Unhandled metadata field {field}");
                }
            }

            public void SetMetadataField(ItemMetadataField field, string value)
            {
                switch (field)
                {
                    case ItemMetadataField.DisplayName: DisplayName = value; break;
                    case ItemMetadataField.Description: Description = value; break;
                    case ItemMetadataField.Color: Color = value; break;
                    default: throw new ArgumentException($"Unhandled metadata field {field}");
                }
            }
        }

        [MetaSerializable]
        public enum NodeType
        {
            Root = 0,
            Section = 1,
            Group = 2,
            Row = 3,
        }

        public static class NodeTypeExtensions
        {
            public static bool IsValidNodeType(this NodeType nodeType)
            {
                switch (nodeType)
                {
                    case NodeType.Root: return true;
                    case NodeType.Section: return true;
                    case NodeType.Group: return true;
                    case NodeType.Row: return true;
                    default: return false;
                }
            }

            public static string ToDashboardString(this NodeType nodeType)
            {
                switch (nodeType)
                {
                    case NodeType.Root: return "root";
                    case NodeType.Section: return "section";
                    case NodeType.Group: return "group";
                    case NodeType.Row: return "row";
                    default: throw new ArgumentException($"Unhandled node type {nodeType}");
                }
            }

            public static string ToLowercaseHumanString(this NodeType nodeType)
            {
                switch (nodeType)
                {
                    case NodeType.Root: return "root";
                    case NodeType.Section: return "section";
                    case NodeType.Group: return "group";
                    case NodeType.Row: return "row";
                    default: throw new ArgumentException($"Unhandled node type {nodeType}");
                }
            }

            public static string ToCapitalizedHumanString(this NodeType nodeType)
            {
                switch (nodeType)
                {
                    case NodeType.Root: return "Root";
                    case NodeType.Section: return "Section";
                    case NodeType.Group: return "Group";
                    case NodeType.Row: return "Row";
                    default: throw new ArgumentException($"Unhandled node type {nodeType}");
                }
            }

            public static NodeType GetParentNodeType(this NodeType nodeType)
            {
                switch (nodeType)
                {
                    case NodeType.Root: throw new ArgumentException("Root has no parent");
                    case NodeType.Section: return NodeType.Root;
                    case NodeType.Group: return NodeType.Section;
                    case NodeType.Row: return NodeType.Group;
                    default: throw new ArgumentException($"Unhandled node type {nodeType}");
                }
            }

            public static bool HasMetadataField(this NodeType nodeType, ItemMetadataField field)
            {
                switch (field)
                {
                    case ItemMetadataField.DisplayName: return nodeType != NodeType.Root;
                    case ItemMetadataField.Description: return nodeType != NodeType.Root;
                    case ItemMetadataField.Color: return nodeType == NodeType.Group;
                    default: throw new ArgumentException($"Unhandled metadata field name {field}");
                }
            }

            public static bool HasChildNodes(this NodeType nodeType)
            {
                return nodeType != NodeType.Row;
            }
        }

        [MetaSerializable]
        public enum ItemMetadataField
        {
            DisplayName,
            Description,
            Color,
        }

        public static class ItemMetadataFieldExtensions
        {
            public static bool IsValidField(this ItemMetadataField field)
            {
                switch (field)
                {
                    case ItemMetadataField.DisplayName: return true;
                    case ItemMetadataField.Description: return true;
                    case ItemMetadataField.Color: return true;
                    default: return false;
                }
            }

            public static bool ValueIsValidForField(this ItemMetadataField field, string value)
            {
                switch (field)
                {
                    case ItemMetadataField.DisplayName:
                        if (value == null)
                            return false;
                        return true;

                    case ItemMetadataField.Description:
                        if (value == null)
                            return false;
                        return true;

                    case ItemMetadataField.Color:
                        // \note null color is valid. UI has some default color for that case.
                        if (value == null)
                            return true;

                        if (!value.StartsWith('#'))
                            return false;
                        if (value.Length != 7)
                            return false;
                        for (int i = 1; i < 7; i++)
                        {
                            if (!char.IsAsciiHexDigit(value[i]))
                                return false;
                        }
                        return true;

                    default: throw new ArgumentException($"Unhandled metadata field name {field}");
                }
            }
        }

        [MetaSerializable]
        [MetaBlockedMembers(3)] // \note Was [MetaMember(3)] public string Color { get; set; } . Nowadays Color exists in LiveOpsEventParams. Reintroduce if needed for some other element types?
        public class Element
        {
            [MetaMember(1)] public ElementId ElementId { get; private set; }
            [MetaMember(4)] public uint Version { get; set; }
            [MetaMember(2)] public MetaGuid RowId { get; set; }

            Element() { }
            public Element(ElementId elementId, uint version, MetaGuid rowId)
            {
                ElementId = elementId;
                Version = version;
                RowId = rowId;
            }

            public static Element CreateDefaultState(ElementId elementId, TimelineState timelineState)
            {
                return new Element(elementId, version: 0, rowId: timelineState.DefaultRowId);
            }
        }

        [MetaSerializable(MetaSerializableFlags.AutomaticConstructorDetection)]
        [JsonConverter(typeof(ElementIdJsonConverter))]
        public abstract record ElementId
        {
            public abstract bool ElementExists(LiveOpsTimelineManagerState state);

            [MetaSerializableDerived(1)]
            public record LiveOpsEvent([property: MetaMember(1)] MetaGuid OccurrenceId) : ElementId
            {
                public override string ToString() => $"{ElementTypeStrings.LiveOpsEvent}:{OccurrenceId}";

                public override bool ElementExists(LiveOpsTimelineManagerState state)
                {
                    return state.LiveOpsEvents.EventOccurrences.ContainsKey(OccurrenceId);
                }
            }

            [MetaSerializableDerived(2)]
            public record ServerError([property: MetaMember(1)] MetaGuid ErrorId) : ElementId
            {
                public override string ToString() => $"{ElementTypeStrings.ServerError}:{ErrorId}";

                public override bool ElementExists(LiveOpsTimelineManagerState state)
                {
                    // \todo #timeline It doesn't seem easy to check server error existence without
                    //       asking it from StatsCollectorManager. But maybe there will be no need to
                    //       check the existence, if server errors are not editable in the first place.
                    //       However the same issue might eventually come up with other element types.
                    return false;
                }
            }

            public static ElementId Parse(string idString)
            {
                string[] parts = idString.Split(":");
                if (parts.Length != 2)
                    throw new FormatException($"Invalid format in timeline element id '{idString}': expected two colon-separated parts");

                string type = parts[0];
                string typeSpecificId = parts[1];
                switch (type)
                {
                    case ElementTypeStrings.LiveOpsEvent: return new LiveOpsEvent(MetaGuid.Parse(typeSpecificId));
                    case ElementTypeStrings.ServerError: return new ServerError(MetaGuid.Parse(typeSpecificId));

                    default:
                        throw new FormatException($"Invalid format in timeline element id '{idString}': unknown element type {type}");
                }
            }
        }

        public static class ElementTypeStrings
        {
            public const string LiveOpsEvent = "liveopsEvent";
            public const string ServerError = "serverError";
        }

        public class ElementIdJsonConverter : JsonConverter<ElementId>
        {
            public override ElementId ReadJson(JsonReader reader, Type objectType, ElementId existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                string idString = serializer.Deserialize<string>(reader);
                return ElementId.Parse(idString);
            }

            public override void WriteJson(JsonWriter writer, ElementId value, JsonSerializer serializer)
            {
                string idString = value.ToString();
                serializer.Serialize(writer, idString);
            }
        }

        [MetaSerializable(MetaSerializableFlags.AutomaticConstructorDetection)]
        [JsonConverter(typeof(ItemIdJsonConverter))]
        public abstract record ItemId
        {
            const string NodePrefix = "node:";

            [MetaSerializableDerived(1)]
            public record Node([property: MetaMember(1)] MetaGuid NodeId) : ItemId
            {
                public override string ToString() => NodePrefix + NodeId.ToString();
            }

            [MetaSerializableDerived(2)]
            public record Element([property: MetaMember(1)] ElementId ElementId) : ItemId
            {
                public override string ToString() => ElementId.ToString();
            }

            public static ItemId Parse(string idString)
            {
                if (idString.StartsWith(NodePrefix, StringComparison.Ordinal))
                    return new Node(MetaGuid.Parse(idString.Substring(NodePrefix.Length)));
                else
                    return new Element(ElementId.Parse(idString));
            }
        }

        public class ItemIdJsonConverter : JsonConverter<ItemId>
        {
            public override ItemId ReadJson(JsonReader reader, Type objectType, ItemId existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                string idString = serializer.Deserialize<string>(reader);
                return ItemId.Parse(idString);
            }

            public override void WriteJson(JsonWriter writer, ItemId value, JsonSerializer serializer)
            {
                string idString = value.ToString();
                serializer.Serialize(writer, idString);
            }
        }

        [MetaSerializable]
        public abstract class Command
        {
            public readonly struct Result
            {
                public Update Update { get; init; }
                public List<AdminApi.Controllers.LiveOpsTimelineItemAuditLogEventPayloadBase> AuditLogEvents { get; init; }
                public List<LiveOpsEvent.LiveOpsEventOccurrence> LiveOpsEventOccurrenceUpdates { get; init; }
                public List<LiveOpsEvent.LiveOpsEventSpec> LiveOpsEventSpecUpdates { get; init; }

                public ErrorCode ErrorCode { get; init; }
                public string ErrorMessage { get; init; }

                public static Result Ok(
                    Update update,
                    List<AdminApi.Controllers.LiveOpsTimelineItemAuditLogEventPayloadBase> auditLogEvents,
                    List<LiveOpsEvent.LiveOpsEventOccurrence> liveOpsEventOccurrenceUpdates = null,
                    List<LiveOpsEvent.LiveOpsEventSpec> liveOpsEventSpecUpdates = null)
                {
                    return new Result
                    {
                        Update = update,
                        AuditLogEvents = auditLogEvents,
                        LiveOpsEventOccurrenceUpdates = liveOpsEventOccurrenceUpdates,
                        LiveOpsEventSpecUpdates = liveOpsEventSpecUpdates,

                        ErrorCode = default,
                        ErrorMessage = null,
                    };
                }

                public static Result Error(ErrorCode code, string message)
                {
                    return new Result
                    {
                        ErrorCode = code,
                        ErrorMessage = message,
                    };
                }
            }

            [MetaSerializable]
            public enum ErrorCode
            {
                InternalServerError = 0,
                NotFound,
                Conflict,
                UnprocessableContent,
            }

            public class ValidationException : Exception
            {
                public ValidationException(string message) : base(message)
                {
                }
            }

            public abstract void Validate();

            public abstract Result TryExecute(LiveOpsTimelineManagerState state);

            public static Type TryGetCommandTypeByName(string name)
            {
                // \todo Resolve lookup at init time
                foreach (Type commandType in MetaSerializerTypeRegistry.GetConcreteDerivedTypes<Command>())
                {
                    CommandAttribute attr = commandType.GetCustomAttribute<CommandAttribute>();
                    if (attr == null)
                        throw new InvalidOperationException($"{commandType} is missing {nameof(CommandAttribute)}");
                    if (attr.Name == name)
                        return commandType;
                }
                return null;
            }
        }

        [AttributeUsage(AttributeTargets.Class)]
        public class CommandAttribute : Attribute
        {
            public string Name { get; }

            public CommandAttribute(string name)
            {
                Name = name;
            }
        }

        [MetaSerializable(MetaSerializableFlags.AutomaticConstructorDetection)]
        public abstract record ItemState
        {
            public abstract ItemId Id { get; }

            [MetaSerializableDerived(1)]
            public record NodeState([property: MetaMember(1)] Node Node) : ItemState
            {
                public override ItemId Id => new ItemId.Node(Node.Id);
            }

            [MetaSerializableDerived(2)]
            public record ElementState([property: MetaMember(1)] Element Element) : ItemState
            {
                public override ItemId Id => new ItemId.Element(Element.ElementId);
            }
        }

        [MetaSerializable]
        public class Update
        {
            [MetaMember(1)] public List<ItemUpdate> Items;

            Update() { }
            public Update(List<ItemUpdate> items)
            {
                Items = items;
            }

            [MetaSerializable]
            public class ItemUpdate
            {
                [MetaMember(1)] public ItemId ItemId;
                [MetaMember(2)] public ItemState ItemState; // null if item got removed

                ItemUpdate() { }
                public ItemUpdate(ItemId itemId, ItemState itemState)
                {
                    ItemId = itemId;
                    ItemState = itemState;
                }

                public static ItemUpdate FromNodeIdAndTimelineState(MetaGuid nodeId, TimelineState timelineState)
                {
                    ItemState itemState;
                    if (timelineState.Nodes.TryGetValue(nodeId, out Node node))
                        itemState = new ItemState.NodeState(node);
                    else
                        itemState = null;

                    return new ItemUpdate(new ItemId.Node(nodeId), itemState);
                }
            }
        }

        [MetaSerializable]
        public readonly struct UpdateEntry
        {
            [MetaMember(1)] public int SequentialId { get; }
            [MetaMember(2)] public MetaGuid UniqueId { get; }
            [MetaMember(3)] public Update Update { get; }

            [MetaDeserializationConstructor]
            public UpdateEntry(int sequentialId, MetaGuid uniqueId, Update update)
            {
                SequentialId = sequentialId;
                UniqueId = uniqueId;
                Update = update;
            }
        }

        [MetaSerializableDerived(1)]
        [Command("createNewItem")]
        public class CreateNewItemCommand : Command
        {
            [MetaMember(1)] public NodeType ItemType { get; }
            [MetaMember(2)] public MetaDictionary<ItemMetadataField, string> ItemConfig { get; }
            [MetaMember(3)] public ItemId ParentId { get; }
            [MetaMember(4)] public uint ParentVersion { get; }

            [MetaDeserializationConstructor]
            public CreateNewItemCommand(NodeType itemType, MetaDictionary<ItemMetadataField, string> itemConfig, ItemId parentId, uint parentVersion)
            {
                ItemType = itemType;
                ItemConfig = itemConfig;
                ParentId = parentId;
                ParentVersion = parentVersion;
            }

            public override void Validate()
            {
                if (!ItemType.IsValidNodeType())
                    throw new ValidationException($"{ItemType} is not a valid node type.");
                if (ItemType == NodeType.Root)
                    throw new ValidationException("Cannot create root node.");
                if (!(ParentId is ItemId.Node))
                    throw new ValidationException($"Parent item must be a node, but got {ParentId?.GetType().ToString() ?? "<null>"}.");

                if (ItemConfig == null)
                    throw new ValidationException($"{nameof(ItemConfig)} cannot be null.");
                foreach ((ItemMetadataField field, string value) in ItemConfig)
                {
                    if (!field.IsValidField())
                        throw new ValidationException($"Unknown metadata property {field}.");
                    if (!field.ValueIsValidForField(value))
                        throw new ValidationException($"Invalid value for metadata property {field}: {value} .");

                    if (!ItemType.HasMetadataField(field))
                        throw new InvalidEntityAsk($"Metadata field {field} does not exist for node type {ItemType}");
                }
            }

            public override Result TryExecute(LiveOpsTimelineManagerState state)
            {
                MetaGuid parentNodeId = ((ItemId.Node)ParentId).NodeId;
                if (!state.Timeline.Nodes.TryGetValue(parentNodeId, out Node parentNode))
                    return Result.Error(ErrorCode.NotFound, $"Parent node {ParentId} not found");
                if (parentNode.NodeType != ItemType.GetParentNodeType())
                    return Result.Error(ErrorCode.UnprocessableContent, $"Parent node {ParentId} is a {parentNode.NodeType.ToLowercaseHumanString()}, but expected {ItemType.GetParentNodeType().ToLowercaseHumanString()}");

                // \todo #timeline-version Version conflict checking disabled for now because it's not very useful in this case, and currently gets in the way due to lack of client prediction or other smartness.
                //if (parentNode.Version != ParentVersion)
                //    return Result.Error(ErrorCode.Conflict, $"Parent node {ParentId} actual version {parentNode.Version} conflicts with claimed {ParentVersion}");

                Node node = Node.CreateDefaultState(MetaGuid.New(), ItemType);
                foreach ((ItemMetadataField field, string fieldValue) in ItemConfig)
                    node.SetMetadataField(field, fieldValue);

                parentNode.ChildNodes.Add(node.Id);
                parentNode.Version++;
                node.ParentId = parentNode.Id;
                state.Timeline.Nodes.Add(node.Id, node);

                return Result.Ok(new Update(
                    [
                        Update.ItemUpdate.FromNodeIdAndTimelineState(node.Id, state.Timeline),
                        Update.ItemUpdate.FromNodeIdAndTimelineState(parentNode.Id, state.Timeline),
                    ]),
                    [
                        new AdminApi.Controllers.LiveOpsEventController.TimelineNodeCreated(node: node, parent: parentNode),
                        new AdminApi.Controllers.LiveOpsEventController.TimelineNodeChildAdded(parent: parentNode, child: node),
                    ]);
            }
        }

        [MetaSerializableDerived(2)]
        [Command("deleteItems")]
        public class DeleteItemsCommand : Command
        {
            [MetaMember(1)] public List<ItemDeletion> Items { get; }

            [MetaDeserializationConstructor]
            public DeleteItemsCommand(List<ItemDeletion> items)
            {
                Items = items;
            }

            public override void Validate()
            {
                if (Items == null)
                    throw new ValidationException($"{nameof(Items)} cannot be null.");

                foreach (ItemDeletion item in Items)
                {
                    if (item == null)
                        throw new ValidationException($"{nameof(Items)} cannot contain nulls.");
                    item.Validate();
                }

                Util.CheckPropertyDuplicates(
                    Items,
                    item => item.TargetId,
                    (a, b, targetId) => throw new ValidationException($"Target {targetId} specified multiple times."));
            }

            [MetaSerializable]
            public class ItemDeletion
            {
                [MetaMember(1)] public ItemId TargetId { get; }
                [MetaMember(2)] public uint CurrentVersion { get; }
                [MetaMember(3)] public ItemId ParentId { get; }
                [MetaMember(4)] public uint ParentVersion { get; }

                [MetaDeserializationConstructor]
                public ItemDeletion(ItemId targetId, uint currentVersion, ItemId parentId, uint parentVersion)
                {
                    TargetId = targetId;
                    CurrentVersion = currentVersion;
                    ParentId = parentId;
                    ParentVersion = parentVersion;
                }

                public void Validate()
                {
                    if (!(TargetId is ItemId.Node))
                        throw new ValidationException($"Target item must be a node, but got {TargetId?.GetType().ToString() ?? "<null>"}.");
                    if (!(ParentId is ItemId.Node))
                        throw new ValidationException($"Parent item must be a node, but got {ParentId?.GetType().ToString() ?? "<null>"}.");
                }
            }

            public override Result TryExecute(LiveOpsTimelineManagerState state)
            {
                // \todo Better support for removing multiple items at a time, or else make the requirements more explicit:
                //       - Deleting an item and its parent at the same time should either be explicitly disallowed,
                //         or allowed and better supported. Currently, it'll fail due to "cannot remove
                //         a non-empty {NodeType}" for the parent.
                //       - "Cannot remove the only {NodeType}" can be snuck around by removing multiple at a time.
                //         Though currently this is covered by the unremovable default row, as it happens.

                foreach (ItemDeletion item in Items)
                {
                    MetaGuid targetNodeId = ((ItemId.Node)item.TargetId).NodeId;
                    MetaGuid parentNodeId = ((ItemId.Node)item.ParentId).NodeId;

                    if (!state.Timeline.Nodes.TryGetValue(targetNodeId, out Node targetNode))
                        return Result.Error(ErrorCode.NotFound, $"Node {targetNodeId} not found");
                    if (targetNode.Version != item.CurrentVersion)
                        return Result.Error(ErrorCode.Conflict, $"Node {targetNodeId} actual version {targetNode.Version} conflicts with claimed {item.CurrentVersion}");
                    if (state.Timeline.Nodes.Values.Count(otherNode => otherNode.NodeType == targetNode.NodeType) == 1)
                        return Result.Error(ErrorCode.UnprocessableContent, $"Cannot remove the only {targetNode.NodeType.ToLowercaseHumanString()}");

                    if (targetNode.ChildNodes != null && targetNode.ChildNodes.Count != 0)
                        return Result.Error(ErrorCode.UnprocessableContent, $"Cannot remove a non-empty {targetNode.NodeType.ToLowercaseHumanString()}");
                    if (targetNode.NodeType == NodeType.Row)
                    {
                        if (targetNode.IsDefaultRow)
                            return Result.Error(ErrorCode.UnprocessableContent, "Cannot remove the default row");
                        if (state.Timeline.Elements.Values.Any(ev => ev.RowId == targetNode.Id))
                            return Result.Error(ErrorCode.UnprocessableContent, "Cannot remove a non-empty row");
                    }

                    if (!state.Timeline.Nodes.TryGetValue(parentNodeId, out Node parentNode))
                        return Result.Error(ErrorCode.NotFound, $"Parent node {parentNodeId} not found");
                    if (parentNode.Id != targetNode.ParentId)
                        return Result.Error(ErrorCode.UnprocessableContent, $"{targetNodeId}'s current parent is {targetNode.ParentId}, not {parentNode.Id}");
                    // \todo #timeline-version Version conflict checking disabled for now because it's not very useful in this case, and currently gets in the way due to lack of client prediction or other smartness.
                    //if (parentNode.Version != item.ParentVersion)
                    //    return Result.Error(ErrorCode.Conflict, $"Parent node {parentNode.Id} actual version {parentNode.Version} conflicts with claimed {item.ParentVersion}");
                }

                OrderedSet<MetaGuid> updatedNodes = new();

                List<AdminApi.Controllers.LiveOpsTimelineItemAuditLogEventPayloadBase> auditLogEvents = new();

                foreach (ItemDeletion item in Items)
                {
                    MetaGuid targetNodeId = ((ItemId.Node)item.TargetId).NodeId;
                    MetaGuid parentNodeId = ((ItemId.Node)item.ParentId).NodeId;

                    Node targetNode = state.Timeline.Nodes[targetNodeId];
                    Node parentNode = state.Timeline.Nodes[parentNodeId];

                    state.Timeline.Nodes.Remove(targetNode.Id);
                    parentNode.ChildNodes.Remove(targetNode.Id);
                    parentNode.Version++;

                    updatedNodes.Add(targetNode.Id);
                    updatedNodes.Add(parentNode.Id);

                    auditLogEvents.Add(new AdminApi.Controllers.LiveOpsEventController.TimelineNodeDeleted(targetNode));
                    auditLogEvents.Add(new AdminApi.Controllers.LiveOpsEventController.TimelineNodeChildRemoved(parent: parentNode, child: targetNode));
                }

                return Result.Ok(
                    new Update(updatedNodes.Select(nodeId => Update.ItemUpdate.FromNodeIdAndTimelineState(nodeId, state.Timeline)).ToList()),
                    auditLogEvents);
            }
        }

        [MetaSerializableDerived(3)]
        [Command("moveItems")]
        public class MoveItemsCommand : Command
        {
            [MetaMember(1)] public List<ItemMove> Items { get; }
            [MetaMember(2)] public NewParentInfo NewParent { get; }

            [MetaDeserializationConstructor]
            public MoveItemsCommand(List<ItemMove> items, NewParentInfo newParent)
            {
                Items = items;
                NewParent = newParent;
            }

            public override void Validate()
            {
                if (Items == null)
                    throw new ValidationException($"{nameof(Items)} cannot be null.");

                foreach (ItemMove item in Items)
                {
                    if (item == null)
                        throw new ValidationException($"{nameof(Items)} cannot contain nulls.");
                    item.Validate();
                }

                Util.CheckPropertyDuplicates(
                    Items,
                    item => item.TargetId,
                    (a, b, target) => throw new ValidationException($"Item {target} specified multiple times."));

                if (NewParent == null)
                    throw new ValidationException($"{nameof(NewParent)} cannot be null.");
                NewParent.Validate();
            }

            [MetaSerializable]
            public class ItemMove
            {
                [MetaMember(1)] public ItemId TargetId { get; }
                [MetaMember(2)] public uint CurrentVersion { get; }
                [MetaMember(3)] public uint ParentVersion { get; }

                [MetaDeserializationConstructor]
                public ItemMove(ItemId targetId, uint currentVersion, uint parentVersion)
                {
                    TargetId = targetId;
                    CurrentVersion = currentVersion;
                    ParentVersion = parentVersion;
                }

                public void Validate()
                {
                    if (TargetId == null)
                        throw new ValidationException($"{nameof(TargetId)} cannot be null.");
                }
            }

            [MetaSerializable]
            public class NewParentInfo
            {
                [MetaMember(1)] public ItemId TargetId { get; }
                [MetaMember(2)] public uint CurrentVersion { get; }
                [MetaMember(3)] public int InsertIndex { get; }

                [MetaDeserializationConstructor]
                public NewParentInfo(ItemId targetId, uint currentVersion, int insertIndex)
                {
                    TargetId = targetId;
                    CurrentVersion = currentVersion;
                    InsertIndex = insertIndex;
                }

                public void Validate()
                {
                    if (!(TargetId is ItemId.Node))
                        throw new ValidationException($"Parent {nameof(TargetId)} item must be a node, but got {TargetId?.GetType().ToString() ?? "<null>"}.");
                    if (InsertIndex < 0)
                        throw new ValidationException($"{nameof(InsertIndex)} cannot be negative.");
                }
            }

            public override Result TryExecute(LiveOpsTimelineManagerState state)
            {
                MetaGuid newParentNodeId = ((ItemId.Node)NewParent.TargetId).NodeId;

                if (!state.Timeline.Nodes.TryGetValue(newParentNodeId, out Node newParent))
                    return Result.Error(ErrorCode.NotFound, $"New parent node {newParentNodeId} not found.");
                // \todo #timeline-version Version conflict checking disabled for now because it's not very useful in this case, and currently gets in the way due to lack of client prediction or other smartness.
                //if (newParent.Version != NewParent.CurrentVersion)
                //    return Result.Error(ErrorCode.Conflict, $"New parent node {newParent.Id} actual version {newParent.Version} conflicts with claimed {NewParent.CurrentVersion}.");

                if (newParent.ChildNodes != null)
                {
                    if (NewParent.InsertIndex > newParent.ChildNodes.Count)
                        return Result.Error(ErrorCode.UnprocessableContent, Invariant($"Insert index {NewParent.InsertIndex} exceeds new parent's current child count {newParent.ChildNodes.Count}."));
                }

                foreach (ItemMove item in Items)
                {
                    if (item.TargetId is ItemId.Node(MetaGuid nodeId))
                    {
                        if (!state.Timeline.Nodes.TryGetValue(nodeId, out Node node))
                            return Result.Error(ErrorCode.NotFound, $"Node {nodeId} not found.");
                        // \todo #timeline-version Version conflict checking disabled for now because it's not very useful in this case, and currently gets in the way due to lack of client prediction or other smartness.
                        //if (node.Version != item.CurrentVersion)
                        //    return Result.Error(ErrorCode.Conflict, $"Node {nodeId} actual version {node.Version} conflicts with claimed {item.CurrentVersion}");
                        if (node.NodeType == NodeType.Root)
                            return Result.Error(ErrorCode.UnprocessableContent, "Root node cannot be moved.");
                        if (newParent.NodeType != node.NodeType.GetParentNodeType())
                            throw new InvalidEntityAsk($"New parent node {newParentNodeId} is a {newParent.NodeType.ToLowercaseHumanString()}, but item {item.TargetId} expects parent type {node.NodeType.GetParentNodeType().ToLowercaseHumanString()}.");

                        // \todo #timeline-version Version conflict checking disabled for now because it's not very useful in this case, and currently gets in the way due to lack of client prediction or other smartness.
                        //Node oldParent = state.Timeline.Nodes[node.ParentId];
                        //if (oldParent.Version != item.ParentVersion)
                        //    return Result.Error(ErrorCode.Conflict, $"For node {nodeId}, old parent node {oldParent.Id} actual version {oldParent.Version} conflicts with claimed {item.ParentVersion}.");
                    }
                    else if (item.TargetId is ItemId.Element(ElementId elementId))
                    {
                        if (!elementId.ElementExists(state))
                            return Result.Error(ErrorCode.NotFound, $"Element {elementId} not found.");
                        // \todo #timeline-version Version conflict checking disabled for now because it's not very useful in this case, and currently gets in the way due to lack of client prediction or other smartness.
                        //Element element = state.Timeline.Elements.GetValueOrDefault(elementId) ?? Element.CreateDefaultState(elementId, state.Timeline);
                        //if (element.Version != item.CurrentVersion)
                        //    return Result.Error(ErrorCode.Conflict, $"Element {elementId} actual version {element.Version} conflicts with claimed {item.CurrentVersion}.");
                        if (newParent.NodeType != NodeType.Row)
                            return Result.Error(ErrorCode.UnprocessableContent, $"New parent node {newParentNodeId} is a {newParent.NodeType.ToLowercaseHumanString()}, but item {item.TargetId} expects parent type {NodeType.Row.ToLowercaseHumanString()}.");

                        // \todo #timeline-version Version conflict checking disabled for now because it's not very useful in this case, and currently gets in the way due to lack of client prediction or other smartness.
                        //Node oldParent = state.Timeline.Nodes[element.RowId];
                        //if (oldParent.Version != item.ParentVersion)
                        //    return Result.Error(ErrorCode.Conflict, $"For element {elementId}, old parent node {oldParent.Id} actual version {oldParent.Version} conflicts with claimed {item.ParentVersion}.");
                    }
                    else
                        throw new MetaAssertException($"Unhandled item type {item.TargetId?.GetType().ToString() ?? "<null>"}, should be caught be earlier validation");
                }

                List<Update.ItemUpdate> itemUpdates;
                List<AdminApi.Controllers.LiveOpsTimelineItemAuditLogEventPayloadBase> auditLogEvents = new();

                if (newParent.ChildNodes != null)
                {
                    // Sanity/clarity assertion, should be covered by invariants and the above non-assertion checks by this point.
                    if (!Items.All(item => item.TargetId is ItemId.Node))
                        throw new MetaAssertException($"When a parent has an explicit child list, the children must be Nodes (not Elements).");

                    OrderedSet<MetaGuid> updatedNodes = new();

                    List<MetaGuid> insertedChildNodeIds = new List<MetaGuid>(capacity: Items.Count);
                    foreach (ItemMove item in Items)
                    {
                        MetaGuid nodeId = ((ItemId.Node)item.TargetId).NodeId;
                        insertedChildNodeIds.Add(nodeId);

                        Node node = state.Timeline.Nodes[nodeId];
                        Node oldParent = state.Timeline.Nodes[node.ParentId];

                        auditLogEvents.Add(new AdminApi.Controllers.LiveOpsEventController.TimelineNodeMoved(node: node, oldParent: oldParent, newParent: newParent));

                        if (node.ParentId == newParent.Id)
                        {
                            int oldIndex = newParent.ChildNodes.IndexOf(nodeId);
                            newParent.ChildNodes[oldIndex] = MetaGuid.None;

                            // \note Moving a node merely to a different place within the same parent
                            //       does not count as a change to the moved node.
                            //       (The moved node's Version is not bumped, and ParentId is not changed.)
                            //       Whereas in the else branch below, the node moves to a different parent,
                            //       so the moved node is considered to be changed.

                            auditLogEvents.Add(new AdminApi.Controllers.LiveOpsEventController.TimelineNodeChildMoved(parent: newParent, child: node));
                        }
                        else
                        {
                            oldParent.ChildNodes.Remove(nodeId);
                            oldParent.Version++;
                            updatedNodes.Add(oldParent.Id);

                            node.ParentId = newParent.Id;
                            node.Version++;
                            updatedNodes.Add(node.Id);

                            auditLogEvents.Add(new AdminApi.Controllers.LiveOpsEventController.TimelineNodeChildRemoved(parent: oldParent, child: node));
                            auditLogEvents.Add(new AdminApi.Controllers.LiveOpsEventController.TimelineNodeChildAdded(parent: newParent, child: node));
                        }
                    }
                    newParent.ChildNodes.InsertRange(NewParent.InsertIndex, insertedChildNodeIds);
                    newParent.ChildNodes.RemoveAll(childId => childId == MetaGuid.None);
                    newParent.Version++;
                    updatedNodes.Add(newParent.Id);

                    itemUpdates = updatedNodes.Select(nodeId => Update.ItemUpdate.FromNodeIdAndTimelineState(nodeId, state.Timeline)).ToList();
                }
                else
                {
                    // Sanity/clarity assertion, should be covered by invariants and the above non-assertion checks by this point.
                    if (!Items.All(item => item.TargetId is ItemId.Element))
                        throw new MetaAssertException($"When a parent does not have an explicit child list, the children must be Elements (not Nodes).");

                    OrderedSet<MetaGuid> updatedNodes = new();
                    itemUpdates = new();

                    foreach (ItemMove item in Items)
                    {
                        ElementId elementId = ((ItemId.Element)item.TargetId).ElementId;

                        if (!state.Timeline.Elements.TryGetValue(elementId, out Element element))
                        {
                            element = Element.CreateDefaultState(elementId, state.Timeline);
                            state.Timeline.Elements.Add(element.ElementId, element);
                        }

                        Node oldParent = state.Timeline.Nodes[element.RowId];

                        element.RowId = newParent.Id;
                        element.Version++;
                        oldParent.Version++;
                        newParent.Version++;

                        updatedNodes.Add(oldParent.Id);
                        updatedNodes.Add(newParent.Id);
                        itemUpdates.Add(new Update.ItemUpdate(new ItemId.Element(element.ElementId), new ItemState.ElementState(element)));

                        auditLogEvents.Add(new AdminApi.Controllers.LiveOpsEventController.TimelineNodeElementRemoved(node: oldParent, element: element, state));
                        auditLogEvents.Add(new AdminApi.Controllers.LiveOpsEventController.TimelineNodeElementAdded(node: newParent, element: element, state));
                        auditLogEvents.Add(new AdminApi.Controllers.LiveOpsEventController.TimelineElementMoved(element: element, state, oldParent: oldParent, newParent: newParent));
                    }

                    foreach (MetaGuid nodeId in updatedNodes)
                        itemUpdates.Add(Update.ItemUpdate.FromNodeIdAndTimelineState(nodeId, state.Timeline));
                }

                return Result.Ok(new Update(itemUpdates), auditLogEvents);
            }
        }

        [MetaSerializableDerived(7)]
        [Command("changeMetadata")]
        public class ChangeMetadataCommand : Command
        {
            [MetaMember(1)] public List<ItemChange> Items { get; }

            public override void Validate()
            {
                if (Items == null)
                    throw new ValidationException($"{nameof(Items)} cannot be null.");

                foreach (ItemChange item in Items)
                {
                    if (item == null)
                        throw new ValidationException($"{nameof(Items)} cannot contain nulls.");
                    item.Validate();
                }

                Util.CheckPropertyDuplicates(
                    Items,
                    item => item.TargetId,
                    (a, b, target) => throw new ValidationException($"Item {target} specified multiple times."));
            }

            [MetaDeserializationConstructor]
            public ChangeMetadataCommand(List<ItemChange> items)
            {
                Items = items;
            }

            [MetaSerializable]
            public class ItemChange
            {
                [MetaMember(1)] public ItemId TargetId { get; }
                [MetaMember(2)] public uint CurrentVersion { get; }
                [MetaMember(3)] public List<MetadataChange> Changes { get; }

                [MetaDeserializationConstructor]
                public ItemChange(ItemId targetId, uint currentVersion, List<MetadataChange> changes)
                {
                    TargetId = targetId;
                    CurrentVersion = currentVersion;
                    Changes = changes;
                }

                public void Validate()
                {
                    if (TargetId == null)
                        throw new ValidationException($"{nameof(TargetId)} cannot be null.");
                    if (Changes == null)
                        throw new ValidationException($"{nameof(Changes)} cannot be null.");

                    foreach (MetadataChange change in Changes)
                    {
                        if (change == null)
                            throw new ValidationException($"{nameof(Changes)} cannot contain nulls.");
                        change.Validate();
                    }

                    Util.CheckPropertyDuplicates(
                        Changes,
                        change => change.Property,
                        (a, b, field) => throw new ValidationException($"Property {field} specified multiple times for item {TargetId}."));
                }
            }

            [MetaSerializable]
            public class MetadataChange
            {
                [MetaMember(1)] public ItemMetadataField Property { get; }
                [MetaMember(2)] public string NewValue { get; }

                [MetaDeserializationConstructor]
                public MetadataChange(ItemMetadataField property, string newValue)
                {
                    Property = property;
                    NewValue = newValue;
                }

                public void Validate()
                {
                    if (!Property.IsValidField())
                        throw new ValidationException($"Unknown metadata property {Property}.");
                    if (!Property.ValueIsValidForField(NewValue))
                        throw new ValidationException($"Invalid value for metadata property {Property}: {NewValue} .");
                }
            }

            public override Result TryExecute(LiveOpsTimelineManagerState state)
            {
                foreach (ItemChange item in Items)
                {
                    if (item.TargetId is ItemId.Node(MetaGuid nodeId))
                    {
                        if (!state.Timeline.Nodes.TryGetValue(nodeId, out Node node))
                            return Result.Error(ErrorCode.NotFound, $"Node {nodeId} not found");
                        if (node.IsDefaultRow)
                            return Result.Error(ErrorCode.UnprocessableContent, "Cannot edit the default row");
                        // \todo #timeline-version Version conflict checking disabled for now because it's not very useful in this case, and currently gets in the way due to lack of client prediction or other smartness.
                        //if (node.Version != item.CurrentVersion)
                        //    return Result.Error(ErrorCode.Conflict, $"Node {nodeId} actual version {node.Version} conflicts with claimed {item.CurrentVersion}");

                        foreach (MetadataChange change in item.Changes)
                        {
                            if (!node.NodeType.HasMetadataField(change.Property))
                                return Result.Error(ErrorCode.UnprocessableContent, $"Metadata field {change.Property} does not exist for node type {node.NodeType.ToLowercaseHumanString()}");
                        }
                    }
                    else if (item.TargetId is ItemId.Element(ElementId elementId))
                    {
                        if (!elementId.ElementExists(state))
                            return Result.Error(ErrorCode.NotFound, $"Element {elementId} not found");
                        // \todo #timeline-version Version conflict checking disabled for now because it's not very useful in this case, and currently gets in the way due to lack of client prediction or other smartness.
                        //uint actualVersion = (state.Timeline.Elements.GetValueOrDefault(elementId) ?? Element.CreateDefaultState(elementId, state.Timeline)).Version;
                        //if (actualVersion != item.CurrentVersion)
                        //    return Result.Error(ErrorCode.Conflict, $"Element {elementId} actual version {actualVersion} conflicts with claimed {item.CurrentVersion}");

                        foreach (MetadataChange change in item.Changes)
                        {
                            if (change.Property == ItemMetadataField.Color
                             || change.Property == ItemMetadataField.DisplayName
                             || change.Property == ItemMetadataField.Description)
                            {
                                if (elementId is ElementId.LiveOpsEvent(MetaGuid occurrenceId))
                                {
                                    // These properties are not part of `Element`,
                                    // that is, not officially part of the "metadata" in the timeline manager,
                                    // but we still handle the property change, by somewhat sneakily changing
                                    // the actual LiveOpsEventOccurrence state (EventParams).
                                    // #timeline-event-editing
                                    // \todo This should get more thought when we implement timeline editing
                                    //       for more kinds of elements, such as broadcasts, notification campaigns,
                                    //       etc, most of which already have some display name in their state.
                                }
                                else
                                    return Result.Error(ErrorCode.UnprocessableContent, $"{change.Property} editing not supported for {elementId.GetType()}");
                            }
                            else
                                throw new ArgumentException($"Unhandled metadata field name {change.Property}");
                        }
                    }
                    else
                        return Result.Error(ErrorCode.UnprocessableContent, $"Unhandled item type {item?.GetType().ToString() ?? "<null>"}");
                }

                List<Update.ItemUpdate> itemUpdates = new();
                List<AdminApi.Controllers.LiveOpsTimelineItemAuditLogEventPayloadBase> auditLogEvents = new();
                List<LiveOpsEvent.LiveOpsEventOccurrence> liveOpsEventOccurrenceUpdates = new();
                List<LiveOpsEvent.LiveOpsEventSpec> liveOpsEventSpecUpdates = new();

                foreach (ItemChange item in Items)
                {
                    Update.ItemUpdate itemUpdate;

                    if (item.TargetId is ItemId.Node { NodeId: MetaGuid nodeId })
                    {
                        Node node = state.Timeline.Nodes[nodeId];

                        List<AdminApi.Controllers.LiveOpsEventController.TimelineItemMetadataChangeAuditLogInfo> changesForAudit = new();

                        foreach (MetadataChange change in item.Changes)
                        {
                            string oldValue = node.GetMetadataField(change.Property);
                            changesForAudit.Add(new(change.Property, oldValue: oldValue, newValue: change.NewValue));

                            node.SetMetadataField(change.Property, change.NewValue);
                        }

                        node.Version++;

                        itemUpdate = new Update.ItemUpdate(item.TargetId, new ItemState.NodeState(node));

                        auditLogEvents.Add(new AdminApi.Controllers.LiveOpsEventController.TimelineNodeMetadataChanged(node, changesForAudit));
                    }
                    else if (item.TargetId is ItemId.Element { ElementId: ElementId elementId })
                    {
                        List<AdminApi.Controllers.LiveOpsEventController.TimelineItemMetadataChangeAuditLogInfo> changesForAudit = new();

                        if (elementId is ElementId.LiveOpsEvent(MetaGuid occurrenceId))
                        {
                            // \note See above about #timeline-event-editing.
                            //
                            //       We don't need to bump LiveOpsEventOccurrence.EditVersion here,
                            //       because EditVersion is really only used by player actor to track changes,
                            //       and DisplayName/Description/Color is not relevant for player actor anyway.
                            //
                            //       This is a bit sneaky to bypass the general liveops event update,
                            //       which normally happens with UpdateLiveOpsEventRequest.

                            LiveOpsEvent.LiveOpsEventOccurrence occurrence = state.LiveOpsEvents.EventOccurrences[occurrenceId];
                            LiveOpsEvent.LiveOpsEventSpec spec = state.LiveOpsEvents.EventSpecs[occurrence.DefiningSpecId];

                            foreach (MetadataChange change in item.Changes)
                            {
                                if (change.Property == ItemMetadataField.DisplayName
                                 || change.Property == ItemMetadataField.Description
                                 || change.Property == ItemMetadataField.Color)
                                {
                                    string oldValue;
                                    if (change.Property == ItemMetadataField.DisplayName)
                                        oldValue = occurrence.EventParams.DisplayName;
                                    else if (change.Property == ItemMetadataField.Description)
                                        oldValue = occurrence.EventParams.Description;
                                    else if (change.Property == ItemMetadataField.Color)
                                        oldValue = occurrence.EventParams.Color;
                                    else
                                        throw new MetaAssertException($"Unhandled metadata field {change.Property}");

                                    changesForAudit.Add(new(change.Property, oldValue: oldValue, newValue: change.NewValue));

                                    LiveOpsEvent.LiveOpsEventOccurrence updatedOccurrence = new LiveOpsEvent.LiveOpsEventOccurrence(
                                        occurrenceId: occurrence.OccurrenceId,
                                        editVersion: occurrence.EditVersion,
                                        definingSpecId: occurrence.DefiningSpecId,
                                        scheduleTimeMode: occurrence.ScheduleTimeMode,
                                        utcScheduleOccasionMaybe: occurrence.UtcScheduleOccasionMaybe,
                                        eventParams:
                                            EventParamsWithNewMetadataValue(occurrence.EventParams, change.Property, change.NewValue),
                                        explicitlyConcludedAt: occurrence.ExplicitlyConcludedAt,
                                        timeState: occurrence.TimeState);

                                    LiveOpsEvent.LiveOpsEventSpec updatedSpec = new LiveOpsEvent.LiveOpsEventSpec(
                                        specId: spec.SpecId,
                                        editVersion: spec.EditVersion,
                                        settings: new LiveOpsEvent.LiveOpsEventSettings(
                                            spec.Settings.ScheduleMaybe,
                                            EventParamsWithNewMetadataValue(spec.Settings.EventParams, change.Property, change.NewValue)),
                                        createdAt: spec.CreatedAt);

                                    occurrence = updatedOccurrence;
                                    spec = updatedSpec;
                                }
                            }

                            state.LiveOpsEvents.SetSpec(spec);
                            state.LiveOpsEvents.SetOccurrence(occurrence);

                            liveOpsEventSpecUpdates.Add(spec);
                            liveOpsEventOccurrenceUpdates.Add(occurrence);
                        }

                        // \note It's ok to not have an explicit state for the timeline element yet.
                        //       (All liveops events etc elements have an implicit initial default state.)
                        //       We'll create the state now.
                        if (!state.Timeline.Elements.TryGetValue(elementId, out Element element))
                        {
                            element = Element.CreateDefaultState(elementId, state.Timeline);
                            state.Timeline.Elements.Add(element.ElementId, element);
                        }

                        element.Version++;

                        itemUpdate = new Update.ItemUpdate(item.TargetId, new ItemState.ElementState(element));

                        auditLogEvents.Add(new AdminApi.Controllers.LiveOpsEventController.TimelineElementMetadataChanged(element, state, changesForAudit));
                    }
                    else
                        throw new MetaAssertException($"Unhandled item type {item?.GetType().ToString() ?? "<null>"}, should be caught be earlier validation");

                    itemUpdates.Add(itemUpdate);
                }

                return Result.Ok(
                    new Update(itemUpdates),
                    auditLogEvents,
                    liveOpsEventOccurrenceUpdates: liveOpsEventOccurrenceUpdates,
                    liveOpsEventSpecUpdates: liveOpsEventSpecUpdates);
            }

            static LiveOpsEvent.LiveOpsEventParams EventParamsWithNewMetadataValue(LiveOpsEvent.LiveOpsEventParams existingParams, ItemMetadataField field, string newValue)
            {
                return new LiveOpsEvent.LiveOpsEventParams(
                    displayName: field == ItemMetadataField.DisplayName ? newValue : existingParams.DisplayName,
                    description: field == ItemMetadataField.Description ? newValue : existingParams.Description,
                    color: field == ItemMetadataField.Color ? newValue : existingParams.Color,
                    targetPlayersMaybe: existingParams.TargetPlayersMaybe,
                    targetConditionMaybe: existingParams.TargetConditionMaybe,
                    templateIdMaybe: existingParams.TemplateIdMaybe,
                    content: existingParams.Content);
            }
        }

        [MetaSerializable]
        public class UpdateCursor
        {
            [MetaMember(1)] public int EntrySequentialId { get; }
            [MetaMember(2)] public MetaGuid PreviousEntryUniqueId { get; }

            [MetaDeserializationConstructor]
            public UpdateCursor(int entrySequentialId, MetaGuid previousEntryUniqueId)
            {
                EntrySequentialId = entrySequentialId;
                PreviousEntryUniqueId = previousEntryUniqueId;
            }

            public override string ToString() => Invariant($"{EntrySequentialId}_{PreviousEntryUniqueId}");

            public static UpdateCursor Parse(string cursorStr)
            {
                string[] parts = cursorStr.Split("_");
                if (parts.Length != 2)
                    throw new FormatException("Expected two underscore-separated integer parts");

                return new UpdateCursor(
                    entrySequentialId: int.Parse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture),
                    previousEntryUniqueId: MetaGuid.Parse(parts[1]));
            }
        }
    }

    [MetaMessage(MessageCodesCore.InvokeLiveOpsTimelineCommandRequest, MessageDirection.ServerInternal)]
    public class InvokeLiveOpsTimelineCommandRequest : EntityAskRequest<InvokeLiveOpsTimelineCommandResponse>
    {
        public Command Command { get; }

        [MetaDeserializationConstructor]
        public InvokeLiveOpsTimelineCommandRequest(Command command)
        {
            Command = command;
        }
    }
    [MetaMessage(MessageCodesCore.InvokeLiveOpsTimelineCommandResponse, MessageDirection.ServerInternal)]
    public class InvokeLiveOpsTimelineCommandResponse : EntityAskResponse
    {
        public bool IsSuccess { get; }
        public List<AdminApi.Controllers.LiveOpsTimelineItemAuditLogEventPayloadBase> AuditLogEvents { get; }
        public Command.ErrorCode ErrorCode { get; }
        public string ErrorMessage { get; }

        [MetaDeserializationConstructor]
        public InvokeLiveOpsTimelineCommandResponse(bool isSuccess, List<AdminApi.Controllers.LiveOpsTimelineItemAuditLogEventPayloadBase> auditLogEvents, Command.ErrorCode errorCode, string errorMessage)
        {
            IsSuccess = isSuccess;
            AuditLogEvents = auditLogEvents;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
    }

    [MetaMessage(MessageCodesCore.GetLiveOpsTimelineUpdatesRequest, MessageDirection.ServerInternal)]
    public class GetLiveOpsTimelineUpdatesRequest : EntityAskRequest<GetLiveOpsTimelineUpdatesResponse>
    {
        public UpdateCursor Cursor { get; }
        public MetaTime FirstInstant { get; }
        public MetaTime LastInstant { get; }
        public MetaTime CurrentTime { get; }

        [MetaDeserializationConstructor]
        public GetLiveOpsTimelineUpdatesRequest(UpdateCursor cursor, MetaTime firstInstant, MetaTime lastInstant, MetaTime currentTime)
        {
            Cursor = cursor;
            FirstInstant = firstInstant;
            LastInstant = lastInstant;
            CurrentTime = currentTime;
        }
    }
    [MetaMessage(MessageCodesCore.GetLiveOpsTimelineUpdatesResponse, MessageDirection.ServerInternal)]
    public class GetLiveOpsTimelineUpdatesResponse : EntityAskResponse
    {
        public bool IsSuccess { get; }
        public Dictionary<ItemId, Item> Updates { get; }
        public UpdateCursor ContinuationCursor { get; }
        public string Error { get; }

        [MetaSerializable]
        public class Item
        {
            [MetaMember(1)] public ItemState ItemState;
            /// <summary>
            /// Only set if the item is a row.
            /// <para>
            /// This is used because <see cref="ItemState"/>, when it is a row (i.e. a <see cref="ItemState.NodeState"/> containing a row <see cref="Node"/>)
            /// does not contain the row's child elements, but rather the child elements just know their parents.
            /// But we want to report the children in this <see cref="GetLiveOpsTimelineUpdatesResponse"/>
            /// because the requester will need them, and we're in a better position to know them than the requested is;
            /// therefore we fill them here.
            /// </para>
            /// </summary>
            [MetaMember(2)] public List<ElementId> RowChildElements;
            [MetaMember(4)] public bool RowHasChildElementsIgnoringTimeRange;

            // Extra data used only for specific element types...
            // \todo Do more cleanly.

            [MetaMember(3)] public LiveOpsEventData LiveOpsEvent;

            [MetaSerializable]
            public class LiveOpsEventData
            {
                [MetaMember(1)] public LiveOpsEvent.LiveOpsEventOccurrence Occurrence { get; }
                [MetaMember(2)] public LiveOpsEvent.LiveOpsEventSpec Spec { get; }

                [MetaDeserializationConstructor]
                public LiveOpsEventData(LiveOpsEvent.LiveOpsEventOccurrence occurrence, LiveOpsEvent.LiveOpsEventSpec spec)
                {
                    Occurrence = occurrence;
                    Spec = spec;
                }
            }
        }

        [MetaDeserializationConstructor]
        public GetLiveOpsTimelineUpdatesResponse(bool isSuccess, Dictionary<ItemId, Item> updates, UpdateCursor continuationCursor, string error)
        {
            IsSuccess = isSuccess;
            Updates = updates;
            ContinuationCursor = continuationCursor;
            Error = error;
        }

        public static GetLiveOpsTimelineUpdatesResponse CreateSuccess(Dictionary<ItemId, Item> updates, UpdateCursor continuationCursor)
        {
            return new GetLiveOpsTimelineUpdatesResponse(isSuccess: true, updates, continuationCursor, error: null);
        }

        public static GetLiveOpsTimelineUpdatesResponse CreateError(string error)
        {
            return new GetLiveOpsTimelineUpdatesResponse(isSuccess: false, updates: null, continuationCursor: null, error);
        }
    }

    public partial class LiveOpsTimelineManager
    {
        [EntityAskHandler]
        InvokeLiveOpsTimelineCommandResponse HandleInvokeLiveOpsTimelineCommandRequest(InvokeLiveOpsTimelineCommandRequest request)
        {
            // Requester should validate the command beforehand, but let's make sure.
            try
            {
                request.Command.Validate();
            }
            catch (Command.ValidationException ex)
            {
                throw new InvalidEntityAsk($"Invalid command: {ex.Message}");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Unexpected error in validation of command {Command}", request.Command);
                throw new InvalidEntityAsk($"Unexpected error in command validation: {ex.Message}");
            }

            Command.Result result = request.Command.TryExecute(_state);

            if (result.Update == null)
                return new InvokeLiveOpsTimelineCommandResponse(isSuccess: false, auditLogEvents: null, result.ErrorCode, result.ErrorMessage);

            TimelineRecordUpdate(result.Update);

            // \todo #timeline Persist?

            if (result.LiveOpsEventOccurrenceUpdates?.Count > 0 || result.LiveOpsEventSpecUpdates?.Count > 0)
            {
                PublishMessage(EntityTopic.Member, new SetLiveOpsEventsMessage(
                    result.LiveOpsEventOccurrenceUpdates ?? new(),
                    result.LiveOpsEventSpecUpdates ?? new()));
            }

            return new InvokeLiveOpsTimelineCommandResponse(isSuccess: true, result.AuditLogEvents, errorCode: default, errorMessage: null);
        }

        [EntityAskHandler]
        GetLiveOpsTimelineUpdatesResponse HandleGetLiveOpsTimelineUpdatesRequest(GetLiveOpsTimelineUpdatesRequest request)
        {
            if (request.Cursor.EntrySequentialId < 0)
                throw new InvalidEntityAsk($"Entry sequential id cannot be negative, got {request.Cursor.EntrySequentialId} .");

            TimelineState timeline = _state.Timeline;
            int availableIdStart = timeline.UpdateHistoryRunningId - timeline.UpdateHistory.Count;
            int availableIdEnd = timeline.UpdateHistoryRunningId;

            if (request.Cursor.EntrySequentialId < availableIdStart)
                return GetLiveOpsTimelineUpdatesResponse.CreateError(Invariant($"Requested entry is no longer available: requested {request.Cursor.EntrySequentialId}, available [{availableIdStart} .. {availableIdEnd}) ."));
            if (request.Cursor.EntrySequentialId > availableIdEnd)
                return GetLiveOpsTimelineUpdatesResponse.CreateError(Invariant($"Desync: requested start entry id is too high: requested {request.Cursor.EntrySequentialId}, available [{availableIdStart} .. {availableIdEnd}) ."));

            int entryIndex = request.Cursor.EntrySequentialId - availableIdStart;
            MetaGuid startPreviousEntryUniqueId = entryIndex == 0
                                                  ? timeline.UpdateHistoryOldestPreviousUniqueId
                                                  : timeline.UpdateHistory[entryIndex - 1].UniqueId;
            if (request.Cursor.PreviousEntryUniqueId != startPreviousEntryUniqueId)
                return GetLiveOpsTimelineUpdatesResponse.CreateError(Invariant($"Desync: requested start previous entry unique id does not match actual: requested {request.Cursor.PreviousEntryUniqueId}, actual {startPreviousEntryUniqueId} ."));

            List<UpdateEntry> updates = timeline.UpdateHistory.GetRange(entryIndex, timeline.UpdateHistory.Count - entryIndex);
            UpdateCursor continuationCursor = new UpdateCursor(
                entrySequentialId: timeline.UpdateHistoryRunningId,
                previousEntryUniqueId: updates.Count > 0
                                       ? updates[^1].UniqueId
                                       : request.Cursor.PreviousEntryUniqueId);

            MetaTime currentTime = request.CurrentTime;
            Dictionary<ItemId, GetLiveOpsTimelineUpdatesResponse.Item> itemUpdates = ConvertTimelineUpdatesForUpdateRequestResponse(updates, firstInstant: request.FirstInstant, lastInstant: request.LastInstant, currentTime: currentTime);

            return GetLiveOpsTimelineUpdatesResponse.CreateSuccess(itemUpdates, continuationCursor);
        }

        Dictionary<ItemId, GetLiveOpsTimelineUpdatesResponse.Item> ConvertTimelineUpdatesForUpdateRequestResponse(List<UpdateEntry> timelineUpdateEntries, MetaTime firstInstant, MetaTime lastInstant, MetaTime currentTime)
        {
            if (timelineUpdateEntries.Count == 0)
            {
                // Fast path
                return new Dictionary<ItemId, GetLiveOpsTimelineUpdatesResponse.Item>();
            }

            Dictionary<ItemId, GetLiveOpsTimelineUpdatesResponse.Item> itemUpdates = new();

            TimelineState timelineState = _state.Timeline;

            foreach (UpdateEntry timelineUpdateEntry in timelineUpdateEntries)
            {
                Update timelineUpdate = timelineUpdateEntry.Update;

                foreach (Update.ItemUpdate timelineItemUpdate in timelineUpdate.Items)
                {
                    ItemId itemId = timelineItemUpdate.ItemId;
                    ItemState itemState = timelineItemUpdate.ItemState;

                    GetLiveOpsTimelineUpdatesResponse.Item responseItemUpdate;

                    if (itemState == null)
                        responseItemUpdate = null;
                    else
                    {
                        if (itemId is ItemId.Node(MetaGuid nodeId))
                        {
                            Node node = ((ItemState.NodeState)itemState).Node;

                            responseItemUpdate = new GetLiveOpsTimelineUpdatesResponse.Item
                            {
                                ItemState = itemState,
                                RowChildElements = node.NodeType == NodeType.Row ? new List<ElementId>() : null,
                            };
                        }
                        else if (itemId is ItemId.Element(ElementId elementId))
                        {
                            if (elementId is ElementId.LiveOpsEvent(MetaGuid occurrenceId))
                            {
                                LiveOpsEvent.LiveOpsEventOccurrence occurrence = _state.LiveOpsEvents.EventOccurrences[occurrenceId];

                                if (!OccurrenceVisibilityOverlapsWithTimeRange(occurrence, requestedStartTime: firstInstant, requestedEndTime: lastInstant, currentTime: currentTime))
                                    responseItemUpdate = null;
                                else
                                {
                                    LiveOpsEvent.LiveOpsEventSpec spec = _state.LiveOpsEvents.EventSpecs[occurrence.DefiningSpecId];
                                    responseItemUpdate = new GetLiveOpsTimelineUpdatesResponse.Item
                                    {
                                        ItemState = itemState,
                                        RowChildElements = null,

                                        LiveOpsEvent = new GetLiveOpsTimelineUpdatesResponse.Item.LiveOpsEventData(occurrence, spec),
                                    };
                                }
                            }
                            else
                                throw new MetaAssertException($"Unhandled element type {elementId?.GetType().ToString() ?? "<null>"}");
                        }
                        else
                            throw new MetaAssertException($"Unhandled item type {itemId?.GetType().ToString() ?? "<null>"}");
                    }

                    itemUpdates[itemId] = responseItemUpdate;
                }
            }

            // Based on all liveops events visible in the requested time range (and not just the updated events!),
            // add the events as children to rows that are present in itemUpdates.
            foreach (LiveOpsEvent.LiveOpsEventOccurrence occurrence in _state.LiveOpsEvents.EventOccurrences.Values)
            {
                if (!OccurrenceVisibilityOverlapsWithTimeRange(occurrence, requestedStartTime: firstInstant, requestedEndTime: lastInstant, currentTime: currentTime))
                    continue;

                ElementId elementId = new ElementId.LiveOpsEvent(occurrence.OccurrenceId);
                Element elementState = timelineState.Elements.GetValueOrDefault(elementId)
                                       ?? Element.CreateDefaultState(elementId, timelineState);

                ItemId rowId = new ItemId.Node(elementState.RowId);
                if (itemUpdates.TryGetValue(rowId, out GetLiveOpsTimelineUpdatesResponse.Item row)
                    && row != null)
                {
                    row.RowChildElements.Add(elementId);
                }
            }

            // Based on all Elements, even outside the time range, updated or not,
            // set parent rows' RowHasChildElementsIgnoringTimeRange.
            // \note This does not account implicit elements which are in the default row.
            //       That's ok because this is only used for marking non-empty rows non-removable,
            //       and the default row (even if empty) is non-removable anyway.
            foreach (Element element in _state.Timeline.Elements.Values)
            {
                if (itemUpdates.TryGetValue(new ItemId.Node(element.RowId), out GetLiveOpsTimelineUpdatesResponse.Item row))
                    row.RowHasChildElementsIgnoringTimeRange = true;
            }

            return itemUpdates;
        }

        void TimelineRecordLiveOpsEventUpdates(IEnumerable<MetaGuid> occurrenceIds)
        {
            List<Update.ItemUpdate> itemUpdates = new(capacity: occurrenceIds.Count());

            foreach (MetaGuid occurrenceId in occurrenceIds)
            {
                ElementId elementId = new ElementId.LiveOpsEvent(occurrenceId);

                if (!_state.Timeline.Elements.TryGetValue(elementId, out Element element))
                {
                    element = Element.CreateDefaultState(elementId, _state.Timeline);

                    // \note This may have been the creation of a new event, or merely an edit of an existing event without an explicit Element state.
                    //       We don't know that based on our state here, so we don't know whether we need to report an update for the containing (default) row
                    //       as well (in case its child list changed).
                    //       Since we don't know, we do the conservative thing and do report an update for the row.
                    //
                    //       If/when we refactor the timeline state to hold explicit Element state for all elements (getting rid of the "default row" business),
                    //       we can know better.
                    itemUpdates.Add(new Update.ItemUpdate(new ItemId.Node(element.RowId), new ItemState.NodeState(_state.Timeline.Nodes[element.RowId])));
                }

                itemUpdates.Add(new Update.ItemUpdate(new ItemId.Element(elementId), new ItemState.ElementState(element)));
            }

            Update update = new Update(itemUpdates);
            TimelineRecordUpdate(update);
        }

        void TimelineRecordUpdate(Update update)
        {
            TimelineState timeline = _state.Timeline;

            timeline.UpdateHistory.Add(new UpdateEntry(
                sequentialId: timeline.UpdateHistoryRunningId,
                uniqueId: MetaGuid.New(),
                MetaSerialization.CloneTagged(update, MetaSerializationFlags.IncludeAll, logicVersion: null, resolver: null)));
            timeline.UpdateHistoryRunningId++;

            if (timeline.UpdateHistory.Count > TimelineState.UpdateHistoryMaxCount)
            {
                timeline.UpdateHistoryOldestPreviousUniqueId = timeline.UpdateHistory[^(TimelineState.UpdateHistoryMaxCount+1)].UniqueId;
                timeline.UpdateHistory.RemoveRange(0, timeline.UpdateHistory.Count - TimelineState.UpdateHistoryMaxCount);
            }
        }
    }
}
