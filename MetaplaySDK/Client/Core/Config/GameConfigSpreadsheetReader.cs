// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using static Metaplay.Core.Config.GameConfigSyntaxTree;
using static System.FormattableString;

namespace Metaplay.Core.Config
{
    /// <summary>
    /// Converts from <see cref="SpreadsheetContent"/> to <see cref="GameConfigSyntaxTree"/>.
    /// </summary>
    public static class GameConfigSpreadsheetReader
    {
        public enum PathSegmentType
        {
            Root = 0,
            Member,
            LinearCollection,
            IndexedElement,
        }

        public readonly struct PathSegment : IEquatable<PathSegment>
        {
            public readonly string          Name;
            public readonly string          VariantId;
            public readonly PathSegmentType Type;
            public readonly int?            ElementIndex;

            public NodeMemberId SegmentId => new NodeMemberId(Name, VariantId);

            public static readonly PathSegment Root = new PathSegment(name: "$root", variantId: null, PathSegmentType.Root, elementIndex: null);

            public PathSegment(string name, string variantId, PathSegmentType type, int? elementIndex)
            {
                bool needArrayElement = (type == PathSegmentType.IndexedElement);
                if (needArrayElement && !elementIndex.HasValue)
                    throw new ArgumentException($"PathSegmentType {type} must specify an elementIndex");
                if (!needArrayElement && elementIndex.HasValue)
                    throw new ArgumentException($"PathSegmentType {type} must not specify an elementIndex");

                if (elementIndex.HasValue)
                {
                    if (elementIndex.Value < 0)
                        throw new ArgumentException($"Element indices must be positive, got {elementIndex.Value}");
                }

                Name = name ?? throw new ArgumentNullException(nameof(name));
                VariantId = variantId;
                Type = type;
                ElementIndex = elementIndex;
            }

            public override string ToString()
            {
                switch (Type)
                {
                    case PathSegmentType.Root: return SegmentId.ToString();
                    case PathSegmentType.Member: return SegmentId.ToString();
                    case PathSegmentType.LinearCollection: return $"{SegmentId}[]";
                    case PathSegmentType.IndexedElement: return Invariant($"{SegmentId}[{ElementIndex}]");
                    default:
                        throw new InvalidOperationException($"Invalid PathElementType {Type}");
                }
            }

            public override bool Equals(object obj) => obj is PathSegment segment && Equals(segment);

            public bool Equals(PathSegment other)
            {
                return Name == other.Name &&
                       VariantId == other.VariantId &&
                       Type == other.Type &&
                       ElementIndex == other.ElementIndex;
            }

            public override int GetHashCode() => Util.CombineHashCode(Name?.GetHashCode() ?? 0, VariantId?.GetHashCode() ?? 0, Type.GetHashCode(), ElementIndex?.GetHashCode() ?? 0);

            public static bool operator ==(PathSegment left, PathSegment right) => left.Equals(right);
            public static bool operator !=(PathSegment left, PathSegment right) => !(left == right);
        }

        /// <summary>
        /// Info for either a column or a row, depending on the kind of config: <br/>
        /// - For library sheets, this represents a column which defines a member for each of the many items (rows) in the sheet. The member is identified by the header row at the start of the sheet. <br/>
        /// - For GameConfigKeyValue sheets, this represents a row which defines a member for the single object represented by the sheet. The member is identified by the header column of the sheet. <br/>
        /// Note that "member" above can mean not just a single member (MyMember), but also a nested member (MyMember.SubMember), array element (MyMember.SubMember[0]), etc, as supported by the config sheet syntax.
        /// "Slice" is simply used as a shorthand term meaning either column or row, in contexts where it can be either depending on the config being processed.
        /// </summary>
        public class SliceInfo
        {
            /// <summary>
            /// Index of the column or row in the <see cref="SpreadsheetContent"/> being processed.
            /// <para>
            /// Do not accidentally use this for reporting the source number of the column/row: use <see cref="SourceLocation"/> instead.
            /// This index is the concrete index of the column/row within the <see cref="SpreadsheetContent"/> given to the parser,
            /// which may differ from the original (source) column/row number due to preprocessing.
            /// </para>
            /// </summary>
            public readonly int                             SliceIndex;
            public readonly string                          FullPath;
            public readonly PathSegment[]                   PathSegments;
            public readonly NodeTag[]                       Tags;
            /// <summary>
            /// Source location of the column or row.
            /// For libraries, this is the source location of the column, created with <see cref="GameConfigSpreadsheetLocation.FromColumn"/>.
            /// For GameConfigKeyValue sheets, this is the source location of the row, created with <see cref="GameConfigSpreadsheetLocation.FromRow"/>.
            /// </summary>
            public readonly GameConfigSpreadsheetLocation   SourceLocation;

            /// <summary>
            /// Whether this represents a vertical column (as in libraries) or a horizontal row (as in GameConfigKeyValue configs).
            /// </summary>
            // \todo Implementation is a bit of a hack.
            public bool IsColumn => SourceLocation.Rows.IsRange;

            public bool HasEmptyHeader => string.IsNullOrEmpty(FullPath);

            public SliceInfo(int sliceIndex, string fullPath, PathSegment[] pathSegments, NodeTag[] tags, GameConfigSpreadsheetLocation sourceLocation)
            {
                SliceIndex          = sliceIndex;
                FullPath            = fullPath;
                PathSegments        = pathSegments;
                Tags                = tags;
                SourceLocation      = sourceLocation;
            }

            public bool HasTagWithName(string tagName) => Tags.Any(tag => tag.Name == tagName);

            public bool IsKeySlice => HasTagWithName(BuiltinTags.Key); // \note Ignores tag values -- may want to generalize to multiple identities in the future

            public override string ToString() => Invariant($"SliceInfo(sliceIndex={SliceIndex}, path=[{string.Join(", ", PathSegments)}], tags=[{string.Join<NodeTag>(", ", Tags)}])");
        }

        public abstract class PathNode
        {
            public readonly string Name;

            protected PathNode(string name) => Name = name;

            internal abstract void ToStringBuilder(StringBuilder sb, int depth);

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                ToStringBuilder(sb, 0);
                return sb.ToString();
            }
        }

        public class PathNodeScalar : PathNode
        {
            public readonly SliceInfo SliceInfo;

            public PathNodeScalar(string name, SliceInfo sliceInfo) : base(name) =>
                SliceInfo = sliceInfo;

            internal override void ToStringBuilder(StringBuilder sb, int depth)
            {
                string indent = new string(' ', depth * 2);
                sb.AppendLine($"{indent}{Name} : scalar");
            }
        }

        public class PathNodeCollection : PathNode
        {
            // \note Allows multiple representations statically, only one can be used for each item
            public readonly SliceInfo       ScalarSlice;
            public readonly SliceInfo[]     LinearSlices;
            public readonly PathNode        LinearNode;
            public readonly SliceInfo[]     IndexedSlices;
            public readonly PathNode[]      IndexedNodes;

            public PathNodeCollection(string name, SliceInfo scalarSlice, SliceInfo[] linearSlices, PathNode linearNode, SliceInfo[] indexedSlices, PathNode[] indexedNodes) : base(name)
            {
                ScalarSlice     = scalarSlice;
                LinearSlices    = linearSlices;
                LinearNode      = linearNode;
                IndexedSlices   = indexedSlices;
                IndexedNodes    = indexedNodes;
            }

            internal override void ToStringBuilder(StringBuilder sb, int depth)
            {
                string indent = new string(' ', depth * 2);
                bool hasScalar = ScalarSlice != null;
                bool hasLinear = LinearNode != null;
                bool hasIndexed = IndexedSlices != null;
                string[] representations = new string[] { hasScalar ? "scalar" : null, hasLinear ? "linear" : null, hasIndexed ? "indexed" : null };
                string representationsStr = string.Join(",", representations.Where(rep => rep != null));
                sb.AppendLine($"{indent}{Name} : collection<{representationsStr}>");

                if (hasScalar)
                    sb.AppendLine($"{indent}  {ScalarSlice.FullPath}");

                if (hasLinear)
                {
                    LinearNode.ToStringBuilder(sb, depth + 1);
                }

                if (hasIndexed)
                {
                    foreach (PathNode element in IndexedNodes)
                    {
                        if (element != null)
                            element.ToStringBuilder(sb, depth + 1);
                        else
                            sb.AppendLine($"{indent}  null");
                    }
                }
            }
        }

        public class PathNodeObject : PathNode
        {
            public MetaDictionary<NodeMemberId, PathNode> Children;

            public PathNodeObject(string name, MetaDictionary<NodeMemberId, PathNode> children) : base(name) => Children = children;

            internal override void ToStringBuilder(StringBuilder sb, int depth)
            {
                string indent = new string(' ', depth * 2);
                sb.AppendLine($"{indent}{Name} : object");

                foreach (PathNode child in Children.Values)
                    child.ToStringBuilder(sb, depth + 1);
            }
        }

        static SliceInfo ParseHeaderCell(int sliceIndex, SpreadsheetCell cell, GameConfigSpreadsheetLocation sliceLocation)
        {
            if (string.IsNullOrEmpty(cell.Value))
                throw new ParseError("Empty header cell!");

            // Special handling of some special slices (/Variant, /Aliases)
            if (cell.Value == GameConfigSyntaxTreeUtil.VariantIdKey || cell.Value == GameConfigSyntaxTreeUtil.AliasesKey)
            {
                return new SliceInfo(
                    sliceIndex,
                    cell.Value,
                    new PathSegment[] { new PathSegment(cell.Value, variantId: null, PathSegmentType.Member, elementIndex: null) },
                    tags: Array.Empty<NodeTag>(),
                    sliceLocation);
            }

            ConfigLexer lexer = new ConfigLexer(cell.Value);

            List<PathSegment> path = new List<PathSegment>();

            // Pseudo-slices start with a tag (and don't have a name).
            if (lexer.CurrentToken.Type != ConfigLexer.TokenType.Hash)
            {
                // Parse the path by element (dot-separated sequence of elements)
                for (; ; )
                {
                    string name = lexer.ParseIdentifier();
                    if (lexer.TryParseToken(ConfigLexer.TokenType.LeftBracket))
                    {
                        if (lexer.CurrentToken.Type == ConfigLexer.TokenType.IntegerLiteral)
                        {
                            int arrayIndex = lexer.ParseIntegerLiteral();
                            lexer.ParseToken(ConfigLexer.TokenType.RightBracket);
                            path.Add(new PathSegment(name, variantId: null, PathSegmentType.IndexedElement, elementIndex: arrayIndex));
                        }
                        else if (lexer.TryParseToken(ConfigLexer.TokenType.RightBracket))
                        {
                            path.Add(new PathSegment(name, variantId: null, PathSegmentType.LinearCollection, elementIndex: null));
                        }
                        else
                            throw new ParseError($"Invalid token {lexer.CurrentToken.Type} ({lexer.GetTokenString(lexer.CurrentToken)}), expecting an integer or ']'");
                    }
                    else // simple member
                    {
                        path.Add(new PathSegment(name, variantId: null, PathSegmentType.Member, elementIndex: null));
                    }

                    // Check if path continues or not
                    if (lexer.TryParseToken(ConfigLexer.TokenType.Dot))
                        continue;
                    else if (lexer.IsAtEnd || lexer.CurrentToken.Type == ConfigLexer.TokenType.Hash)
                        break;
                    else
                        throw new ParseError($"Invalid token in header: {lexer.CurrentToken.Type} ({lexer.GetTokenString(lexer.CurrentToken)})");
                }
            }

            // Parse all hash tags for the
            List<NodeTag> tags = new List<NodeTag>();
            while (lexer.TryParseToken(ConfigLexer.TokenType.Hash))
            {
                // Parse tag identity
                string tagName = lexer.ParseIdentifier();
                string tagValue = null;

                // Parse tag value, if has one (indicated by a colon ':')
                if (lexer.TryParseToken(ConfigLexer.TokenType.Colon))
                {
                    // \todo [petri] currently assumes tag value is a single token -- use a more flexible custom rule?
                    tagValue = lexer.GetTokenString(lexer.CurrentToken);
                    lexer.Advance();
                }

                // \todo Allow unknown tags as well (to support userland customizations)?
                if (!BuiltinTags.All.Contains(tagName))
                    throw new ParseError($"Invalid tag '{tagName}' used on header '{cell.Value}'. Expected one of: {string.Join(", ", BuiltinTags.All)}");
                tags.Add(new NodeTag(tagName, tagValue));
            }

            // \todo Check for conflicting tags (eg, multiple of the same, or id+comment).

            // Must have consumed all input
            if (!lexer.IsAtEnd)
                throw new ParseError($"Failed to parse header '{cell.Value}', got unexpected token '{lexer.CurrentToken}'.");

            // Check that #comments have an empty node path, and non-comments have non-empty node path.
            bool isComment = tags.Any(tag => tag.Name == BuiltinTags.Comment);
            if (isComment && path.Count != 0)
                throw new ParseError($"Failed to parse header '{cell.Value}', got a non-empty node path for a #comment. The node path should be empty for comments.");
            else if (!isComment && path.Count == 0)
                throw new ParseError($"Failed to parse header '{cell.Value}', got an empty node path.");

            // Check that multiple non-indexed collections (a.k.a. linear collections) are not specified in the same path.
            // For example: SomeList[].OtherList[] is not allowed, but SomeList[].OtherList[0] and SomeList[0].OtherList[] are.
            IEnumerable<PathSegment> linearCollectionPathSegments = path.Where(seg => seg.Type == PathSegmentType.LinearCollection);
            if (linearCollectionPathSegments.Count() > 1)
            {
                bool isColumn = sliceLocation.Rows.IsRange;
                string verticalOrHorizontalText = isColumn ? "vertical" : "horizontal";
                throw new ParseError(
                    $"At most 1 non-indexed ({verticalOrHorizontalText}) collection can be specified in the same header cell, " +
                    $"but '{cell.Value}' contains {linearCollectionPathSegments.Count()}: " +
                    $"{string.Join(", ", linearCollectionPathSegments.Select(seg => $"'{seg}'"))}. " +
                    $"All except 1 must have explicit indexes.");
            }

            return new SliceInfo(sliceIndex, cell.Value, path.ToArray(), tags.ToArray(), sliceLocation);
        }

        static SliceInfo CreateVariantOverrideSliceInfo(int sliceIndex, SliceInfo baselineSlice, string variantId, GameConfigSpreadsheetLocation sourceLocation)
        {
            // Variant override slice has the same path as the baseline slice, except that
            // the last path segment specifies the variant id.
            PathSegment[] pathSegments = baselineSlice.PathSegments.ToArray(); // \note Copy with ToArray()
            int lastSegmentNdx = pathSegments.Length - 1;
            pathSegments[lastSegmentNdx] = CreateVariantPathSegment(pathSegments[lastSegmentNdx], variantId);

            string fullPath = $"{baselineSlice.FullPath} (variant {variantId})";
            NodeTag[] tags = baselineSlice.Tags.ToArray(); // \note Defensive copy...
            return new SliceInfo(sliceIndex, fullPath, pathSegments, tags, sourceLocation);
        }

        public static PathSegment CreateVariantPathSegment(PathSegment baseline, string variantId)
        {
            return new PathSegment(name: baseline.Name, variantId: variantId, baseline.Type, baseline.ElementIndex);
        }

        static SliceInfo[] ParseHeaderRow(GameConfigBuildLog buildLog, List<SpreadsheetCell> headerRow)
        {
            GameConfigSourceInfo sourceInfo = buildLog.SourceInfo;

            // Parse the header cells
            List<SliceInfo> columns = new List<SliceInfo>();
            SliceInfo lastNonVariantColumn = null;
            for (int colNdx = 0; colNdx < headerRow.Count; colNdx++)
            {
                SpreadsheetCell cell = headerRow[colNdx];
                GameConfigSpreadsheetLocation cellLocation = GameConfigSpreadsheetLocation.FromCell(sourceInfo, cell);
                GameConfigSpreadsheetLocation columnLocation = GameConfigSpreadsheetLocation.FromColumn(sourceInfo, cell.Column);

                if (string.IsNullOrEmpty(cell.Value))
                {
                    // Empty header. This will only be used for checking that the column is fully empty.
                    columns.Add(new SliceInfo(colNdx, fullPath: "", Array.Empty<PathSegment>(), Array.Empty<NodeTag>(), columnLocation));
                }
                else if (cell.Value.StartsWith("/:", StringComparison.Ordinal))
                {
                    // Variant column. Specifies a variation of the previous non-variant column.

                    if (lastNonVariantColumn == null)
                        buildLog.WithLocation(cellLocation).Error(message: "Found variant override column but there is no corresponding normal column to its left");
                    else
                    {
                        IEnumerable<string> variantIds = cell.Value.Substring(2).Split(',').Select(x => x.Trim()).Where(x => x != string.Empty);
                        if (!variantIds.Any())
                            buildLog.WithLocation(cellLocation).Error("Malformed column override header");
                        else
                        {
                            foreach (string variantId in variantIds)
                                columns.Add(CreateVariantOverrideSliceInfo(colNdx, lastNonVariantColumn, variantId, columnLocation));
                        }
                    }
                }
                else
                {
                    // Column with a path.

                    try
                    {
                        SliceInfo column = ParseHeaderCell(colNdx, cell, columnLocation);
                        columns.Add(column);
                        lastNonVariantColumn = column;
                    }
                    catch (ParseError ex)
                    {
                        buildLog.WithLocation(cellLocation).Error(message: $"Failed to parse column header '{cell.Value}': {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        buildLog.WithLocation(cellLocation).Error(message: $"Internal error while parsing column header '{cell.Value}'", ex);
                    }
                }
            }
            return columns.ToArray();
        }

        static SpreadsheetCell? TryFindNonemptyCellInColumn(int columnIndex, IEnumerable<List<SpreadsheetCell>> rows)
        {
            foreach (List<SpreadsheetCell> row in rows)
            {
                SpreadsheetCell cell = row[columnIndex];

                if (!string.IsNullOrEmpty(cell.Value))
                    return cell;
            }

            return null;
        }

        static SpreadsheetCell? TryFindNonemptyCellInRow(int rowIndex, List<List<SpreadsheetCell>> rows)
        {
            List<SpreadsheetCell> row = rows[rowIndex];

            foreach (SpreadsheetCell cell in row)
            {
                if (!string.IsNullOrEmpty(cell.Value))
                    return cell;
            }

            return null;
        }

        static PathNode CreateLinearCollectionElementNode(GameConfigBuildLog buildLog, SliceInfo[] slices, int depth)
        {
            bool isComplexElement = slices.Length > 1 || slices[0].PathSegments.Length > depth + 1;
            if (isComplexElement)
                return CreateObjectNode(buildLog, nodeName: "[]", slices, depth + 1);
            else
                return new PathNodeScalar(name: "[]", slices[0]);
        }

        static PathNode[] CreateIndexedCollectionElementNodes(GameConfigBuildLog buildLog, SliceInfo[] slices, int depth)
        {
            // Group slices by their element index
            MetaDictionary<int, SliceInfo[]> byElemIndex =
                slices
                .GroupBy(slice => slice.PathSegments[depth].ElementIndex.Value)
                .ToMetaDictionary(g => g.Key, g => g.ToArray());

            // Size of collection (maximum index + 1), can be sparse
            int collectionSize = byElemIndex.Keys.Max() + 1;

            // Iterate over all elements & create nodes for them
            List<PathNode> elementNodes = new List<PathNode>();
            for (int elemNdx = 0; elemNdx < collectionSize; elemNdx++)
            {
                if (byElemIndex.TryGetValue(elemNdx, out SliceInfo[] elemSlices))
                {
                    bool isComplexElement = elemSlices.Length > 1 || elemSlices[0].PathSegments.Length > depth + 1;
                    if (isComplexElement)
                    {
                        string childName = Invariant($"[{elemNdx}]");
                        PathNodeObject childNode = CreateObjectNode(buildLog, childName, elemSlices, depth + 1);
                        elementNodes.Add(childNode);
                    }
                    else
                    {
                        string childName = Invariant($"[{elemNdx}]");
                        PathNode childNode = new PathNodeScalar(childName, elemSlices[0]);
                        elementNodes.Add(childNode);
                    }
                }
                else
                {
                    // Element not specified
                    elementNodes.Add(null);
                }
            }

            return elementNodes.ToArray();
        }

        static PathNodeObject CreateObjectNode(GameConfigBuildLog buildLog, string nodeName, SliceInfo[] slices, int depth)
        {
            // If `depth` is out of bounds of a path in any of the slice headers, it means there were duplicate or conflicting headers.
            // For example, duplicate: multiple occurrences of `Foo.Bar`; or conflicting: both `Foo` and `Foo.Bar` .
            if (slices.Any(slice => slice.PathSegments.Length <= depth))
            {
                if (slices.Length == 1)
                {
                    // \note This is an error case that should never happen. Could use a better error message but this is an SDK internal error.
                    throw new MetaAssertException($"Encountered too short path in slice but there is only 1 slice, which should never happen.");
                }

                SliceInfo firstSliceWithTooShortPath = slices.First(slice => slice.PathSegments.Length <= depth);
                GameConfigSourceLocation firstLocation = firstSliceWithTooShortPath.SourceLocation;

                foreach (SliceInfo sliceInfo in slices)
                {
                    if (ReferenceEquals(sliceInfo, firstSliceWithTooShortPath))
                        continue;

                    GameConfigSourceLocation location = sliceInfo.SourceLocation;

                    if (sliceInfo.FullPath == firstSliceWithTooShortPath.FullPath)
                        buildLog.WithLocation(location).Error($"Duplicate header cell for '{sliceInfo.FullPath}'. First occurrence is at {firstLocation.SourceInfo.GetLocationUrl(firstLocation)} .");
                    else
                        buildLog.WithLocation(location).Error($"Conflicting header cells '{firstSliceWithTooShortPath.FullPath}' and '{sliceInfo.FullPath}'. The former occurs at {firstLocation.SourceInfo.GetLocationUrl(firstLocation)} . The same value cannot be both a scalar and a compound object.");
                }

                return new PathNodeObject(nodeName, new MetaDictionary<NodeMemberId, PathNode>());
            }

            MetaDictionary<NodeMemberId, PathNode> childNodes = new MetaDictionary<NodeMemberId, PathNode>();

            // Group children by the name of the next path segment (arrays and indexing are ignored)
            foreach (IGrouping<NodeMemberId, SliceInfo> group in slices.GroupBy(sliceInfo => sliceInfo.PathSegments[depth].SegmentId))
            {
                NodeMemberId    childId         = group.Key;
                SliceInfo[]     childSlices     = group.ToArray();

                PathSegmentType[] uniqueSegmentTypes = childSlices.Select(slice => slice.PathSegments[depth].Type).Distinct().ToArray();
                bool isCollection = uniqueSegmentTypes.Contains(PathSegmentType.IndexedElement) || uniqueSegmentTypes.Contains(PathSegmentType.LinearCollection);
                if (isCollection)
                {
                    // Handle collections: can have multiple representations (only one is allowed for each item)
                    SliceInfo[] scalarSlices    = childSlices.Where(slice => slice.PathSegments[depth].Type == PathSegmentType.Member).ToArray();
                    SliceInfo   scalarSlice     = (scalarSlices.Length >= 1) ? scalarSlices[0] : null;
                    SliceInfo[] linearSlices    = childSlices.Where(slice => slice.PathSegments[depth].Type == PathSegmentType.LinearCollection).ToArray();
                    SliceInfo[] indexedSlices   = childSlices.Where(slice => slice.PathSegments[depth].Type == PathSegmentType.IndexedElement).ToArray();

                    // Can only have one scalar slice
                    if (scalarSlices.Length > 1)
                    {
                        SliceInfo dupeSlice = scalarSlices[1];
                        string columnsOrRowsText = dupeSlice.IsColumn ? "columns" : "rows";
                        buildLog.WithLocation(dupeSlice.SourceLocation).Error($"Duplicate {columnsOrRowsText} specifying collection '{dupeSlice.FullPath}'");
                        continue;
                    }

                    // Convert arrays to nulls, if empty
                    if (!linearSlices.Any())
                        linearSlices = null;
                    if (!indexedSlices.Any())
                        indexedSlices = null;

                    // Convert linear (non-indexed) collection slices into a hierarchical representation (if specified)
                    PathNode linearNode = null;
                    if (linearSlices != null)
                        linearNode = CreateLinearCollectionElementNode(buildLog, linearSlices, depth);

                    // Convert indexed-collection slices into a hierarchical representation (if specified)
                    PathNode[] indexedNodes = null;
                    if (indexedSlices != null)
                        indexedNodes = CreateIndexedCollectionElementNodes(buildLog, indexedSlices, depth);

                    PathNodeCollection collection = new PathNodeCollection(
                        $"{childId}[]",
                        scalarSlice: scalarSlice,
                        linearSlices: linearSlices,
                        linearNode: linearNode,
                        indexedSlices: indexedSlices,
                        indexedNodes: indexedNodes);
                    childNodes.Add(childId, collection);
                }
                else
                {
                    // Handle non-collections (objects and scalars)
                    // \todo Allow multi-representation for objects as well?
                    if (uniqueSegmentTypes.Length != 1)
                        throw new InvalidOperationException($"Multiple conflicting segment types for non-collection: {string.Join(", ", uniqueSegmentTypes)}");

                    PathSegment childSegment = childSlices[0].PathSegments[depth];
                    bool isNested = childSlices.Length > 1 || childSlices[0].PathSegments.Length > depth + 1;
                    if (isNested)
                    {
                        PathNode childNode = CreateObjectNode(buildLog, childSegment.SegmentId.ToString(), childSlices, depth + 1);
                        childNodes.Add(childId, childNode);
                    }
                    else // scalar element
                    {
                        SliceInfo childSlice = childSlices[0];
                        childNodes.Add(childId, new PathNodeScalar(childId.ToString(), childSlice));
                    }
                }
            }

            return new PathNodeObject(nodeName, childNodes);
        }

        /// <summary>
        /// A transposing helper used for accessing (a part of) a spreadsheet in a manner agnostic
        /// to its row/column orientation, so the same code can be used for both library and KeyValue
        /// config sheets.
        /// </summary>
        class ItemSheetContent
        {
            public enum Orientation
            {
                /// <summary>
                /// Item's members (or nested members) are listed horizontally, i.e. each member is on its own column.
                /// This is the case in library config sheets.
                /// </summary>
                Horizontal,
                /// <summary>
                /// Item's members (or nested members) are listed vertically, i.e. each member is on its own row.
                /// This is the case in KeyValue config sheets.
                /// </summary>
                Vertical,
            }

            List<List<SpreadsheetCell>> _cells;
            Orientation _orientation;
            /// <summary>
            /// Base offset for "cell index within slice" when indexing <see cref="_cells"/> in <see cref="GetCell"/>.
            /// This is either row offset or column offset, for <see cref="Orientation.Horizontal"/> or <see cref="Orientation.Vertical"/>
            /// respectively.
            /// This is an internal detail for being able to implement <see cref="GetSubSliceContent"/> without needing to copy
            /// a concrete sub-2d-list of <see cref="_cells"/>.
            /// </summary>
            int _cellOffsetWithinSlice;

            /// <summary>
            /// This is either the number of rows (for <see cref="Orientation.Horizontal"/>)
            /// or columns (for <see cref="Orientation.Vertical"/>) for the item.
            /// In other words, the number of cells per slice in the item; where "slice"
            /// means column for <see cref="Orientation.Horizontal"/>
            /// and row for <see cref="Orientation.Vertical"/>.
            /// </summary>
            public int NumCellsPerSlice { get; }

            public ItemSheetContent(List<List<SpreadsheetCell>> cells, Orientation orientation)
                : this(
                    cells,
                    orientation,
                    cellOffsetWithinSlice: 0,
                    numCellsPerSlice: GetNumCellsPerSlice(cells, orientation))
            {
            }

            ItemSheetContent(List<List<SpreadsheetCell>> cells, Orientation orientation, int cellOffsetWithinSlice, int numCellsPerSlice)
            {
                _cells = cells;
                _orientation = orientation;
                _cellOffsetWithinSlice = cellOffsetWithinSlice;
                NumCellsPerSlice = numCellsPerSlice;
            }

            static int GetNumCellsPerSlice(List<List<SpreadsheetCell>> cells, Orientation orientation)
            {
                switch (orientation)
                {
                    case Orientation.Horizontal: return cells.Count;
                    case Orientation.Vertical: return cells.Count == 0 ? 0 : cells[0].Count;
                    default:
                        throw new InvalidEnumArgumentException(nameof(orientation));
                }
            }

            /// <summary>
            /// Get the cell at the given position within the item.
            /// For <see cref="Orientation.Horizontal"/>, <paramref name="cellIndexWithinSlice"/>
            /// is the row index within the item and <paramref name="sliceNdx"/> is
            /// the column index within the item.
            /// For <see cref="Orientation.Vertical"/>, they're the other way around.
            /// </summary>
            public SpreadsheetCell GetCell(int cellIndexWithinSlice, int sliceNdx)
            {
                if (cellIndexWithinSlice < 0)
                    throw new ArgumentOutOfRangeException(nameof(cellIndexWithinSlice), cellIndexWithinSlice, "Cell index within slice can't be negative");
                if (cellIndexWithinSlice >= NumCellsPerSlice)
                    throw new ArgumentOutOfRangeException(nameof(cellIndexWithinSlice), cellIndexWithinSlice, $"Cell index within slice must be less than number of cells per slice ({NumCellsPerSlice})");

                // Apply base offset to cell-index-within-slice.
                // See comment on _cellOffsetWithinSlice for purpose.
                int actualCellNdx = _cellOffsetWithinSlice + cellIndexWithinSlice;

                switch (_orientation)
                {
                    case Orientation.Horizontal: return _cells[actualCellNdx][sliceNdx];
                    case Orientation.Vertical: return _cells[sliceNdx][actualCellNdx];
                    default:
                        throw new InvalidOperationException("unreachable");
                }
            }

            /// <summary>
            /// Get a sub-part of this content, by taking a given subsequence of cells within each slice;
            /// the subsequence is specified by <paramref name="offset"/> and <paramref name="count"/>.
            /// For <see cref="Orientation.Horizontal"/>, this takes a subsequence of _rows_ from this content (but keeps all columns);
            /// for <see cref="Orientation.Vertical"/>, this takes a subsequence of _columns_ from this content (but keeps all rows).
            /// </summary>
            public ItemSheetContent GetSubSliceContent(int offset, int count)
            {
                if (offset < 0)
                    throw new ArgumentOutOfRangeException(nameof(offset), offset, $"Offset can't be negative (got offset = {offset}, count = {count})");
                if (offset > NumCellsPerSlice)
                    throw new ArgumentOutOfRangeException(nameof(offset), offset, $"Offset can't be exceed number of cells per slice ({NumCellsPerSlice}) (got offset = {offset}, count = {count})");
                if (count < 0)
                    throw new ArgumentOutOfRangeException(nameof(count), count, "Count can't be negative (got offset = {offset}, count = {count})");
                if (offset + count > NumCellsPerSlice)
                    throw new ArgumentOutOfRangeException(nameof(count), count, $"Offset+count ({offset + count}) can't exceed number of cells per slice ({NumCellsPerSlice}) (got offset = {offset}, count = {count})");

                return new ItemSheetContent(_cells, _orientation, cellOffsetWithinSlice: offset, numCellsPerSlice: count);
            }
        }

        static CollectionNode ConvertLinearCollection(GameConfigBuildLog buildLog, ItemSheetContent content, int depth, PathNode linearNode)
        {
            // Parse all element of collection (rows of sheet, in case of library) -- element can be simple or complex
            List<NodeBase> elements = new List<NodeBase>();
            for (int cellNdx = 0; cellNdx < content.NumCellsPerSlice; cellNdx++)
            {
                switch (linearNode)
                {
                    case PathNodeScalar scalarNode:
                        SliceInfo sliceInfo = scalarNode.SliceInfo;
                        SpreadsheetCell cell = content.GetCell(cellNdx, sliceInfo.SliceIndex);
                        if (!string.IsNullOrEmpty(cell.Value))
                        {
                            // Fill inbetween skipped elements.
                            while (elements.Count < cellNdx)
                                elements.Add(null);

                            ScalarNode element = new ScalarNode(cell.Value, GameConfigSpreadsheetLocation.FromCell(buildLog.SourceInfo, cell));
                            elements.Add(element);
                        }

                        break;

                    case PathNodeObject objectNode:
                        // Recursively convert object nodes (only keep children with some items).
                        // Here we take a sub-section of `content`: just 1 cell from each slice
                        // (in the case of a library sheet, this is a single row in a vertical collection),
                        // and recurse into that.
                        // Due to the containing loop, this gets done for each element of the linear collection.
                        ItemSheetContent elementContent = content.GetSubSliceContent(offset: cellNdx, count: 1);
                        ObjectNode elemObj = ConvertItemToObjectNode(buildLog, objectNode, elementContent, depth + 1);
                        if (elemObj.Members.Count > 0)
                        {
                            // Fill inbetween skipped elements.
                            while (elements.Count < cellNdx)
                                elements.Add(null);

                            elements.Add(elemObj);
                        }

                        break;

                    case PathNodeCollection:
                        throw new InvalidOperationException("Nested collections are not supported"); // \todo Put error message in buildLog

                    case null:
                        break;

                    default:
                        throw new InvalidOperationException($"Invalid node type {linearNode.GetType()}");
                }
            }

            return new CollectionNode(elements);
        }

        static CollectionNode ConvertIndexedCollection(GameConfigBuildLog buildLog, ItemSheetContent content, int depth, PathNode[] indexedElements)
        {
            List<NodeBase> elements = new List<NodeBase>();

            for (int elemNdx = 0; elemNdx < indexedElements.Length; elemNdx++)
            {
                // Parse all slices of the element
                PathNode elemNode = indexedElements[elemNdx];
                switch (elemNode)
                {
                    case PathNodeScalar scalarNode:
                        SliceInfo sliceInfo = scalarNode.SliceInfo;
                        SpreadsheetCell cell = content.GetCell(0, sliceInfo.SliceIndex);
                        if (!string.IsNullOrEmpty(cell.Value))
                        {
                            // Fill inbetween skipped elements.
                            while (elements.Count < elemNdx)
                                elements.Add(null);

                            ScalarNode element = new ScalarNode(cell.Value, GameConfigSpreadsheetLocation.FromCell(buildLog.SourceInfo, cell));
                            elements.Add(element);
                        }

                        // Check other cells (if any) in the same slice are empty
                        for (int cellNdx = 1; cellNdx < content.NumCellsPerSlice; cellNdx++)
                        {
                            SpreadsheetCell extraCell = content.GetCell(cellNdx, sliceInfo.SliceIndex);
                            if (!string.IsNullOrEmpty(extraCell.Value))
                            {
                                GameConfigSourceLocation location = GameConfigSpreadsheetLocation.FromCell(buildLog.SourceInfo, extraCell);
                                string columnOrRowText = sliceInfo.IsColumn ? "column" : "row";
                                buildLog.WithLocation(location).Error($"Extraneous content in {columnOrRowText} '{sliceInfo.FullPath}'.");
                            }
                        }

                        break;

                    case PathNodeObject objectNode:
                        // Recursively convert object nodes (only keep children with some items)
                        ObjectNode elemObj = ConvertItemToObjectNode(buildLog, objectNode, content, depth + 1);
                        if (elemObj.Members.Count > 0)
                        {
                            // Fill inbetween skipped elements.
                            while (elements.Count < elemNdx)
                                elements.Add(null);

                            elements.Add(elemObj);
                        }

                        break;

                    case PathNodeCollection:
                        throw new InvalidOperationException("Nested collections are not supported"); // \todo Put error message in buildLog

                    case null:
                        break;

                    default:
                        throw new InvalidOperationException($"Invalid node type {elemNode.GetType()}");
                }
            }

            return new CollectionNode(elements);
        }

        static ObjectNode ConvertItemToObjectNode(GameConfigBuildLog buildLog, PathNodeObject node, ItemSheetContent content, int depth)
        {
            MetaDictionary<NodeMemberId, NodeBase> children = new MetaDictionary<NodeMemberId, NodeBase>();

            // Iterate all children: parse any leaf nodes & recurse into any non-leaf nodes.
            foreach ((NodeMemberId childId, PathNode childNode) in node.Children)
            {
                switch (childNode)
                {
                    case PathNodeScalar scalarNode:
                    {
                        SliceInfo   sliceInfo   = scalarNode.SliceInfo;
                        int         sliceIndex  = sliceInfo.SliceIndex;

                        // Read a scalar value from the first cell
                        SpreadsheetCell cell = content.GetCell(0, sliceIndex);
                        if (!string.IsNullOrEmpty(cell.Value))
                        {
                            ScalarNode scalar = new ScalarNode(cell.Value, GameConfigSpreadsheetLocation.FromCell(buildLog.SourceInfo, cell));
                            children.Add(childId, scalar);
                        }

                        // Check other cells (if any) in the same slice are empty
                        for (int cellNdx = 1; cellNdx < content.NumCellsPerSlice; cellNdx++)
                        {
                            SpreadsheetCell extraCell = content.GetCell(cellNdx, sliceIndex);
                            if (!string.IsNullOrEmpty(extraCell.Value))
                            {
                                GameConfigSourceLocation location = GameConfigSpreadsheetLocation.FromCell(buildLog.SourceInfo, extraCell);
                                string columnOrRowText = sliceInfo.IsColumn ? "column" : "row";
                                buildLog.WithLocation(location).Error($"Extraneous content in {columnOrRowText} '{sliceInfo.FullPath}'.");
                            }
                        }

                        break;
                    }

                    case PathNodeCollection collectionNode:
                    {
                        bool HasValuesInCells(SliceInfo[] slices)
                        {
                            foreach (SliceInfo slice in slices)
                            {
                                int sliceNdx = slice.SliceIndex;
                                for (int elemNdx = 0; elemNdx < content.NumCellsPerSlice; elemNdx++)
                                {
                                    if (!string.IsNullOrEmpty(content.GetCell(elemNdx, sliceNdx).Value))
                                        return true;
                                }
                            }

                            return false;
                        }

                        // Check which representations have any values defined for this item
                        bool hasScalarValue     = collectionNode.ScalarSlice != null ? HasValuesInCells(new[] { collectionNode.ScalarSlice }) : false;
                        bool hasLinearValues    = collectionNode.LinearSlices != null ? HasValuesInCells(collectionNode.LinearSlices) : false;
                        bool hasIndexedValues   = collectionNode.IndexedSlices != null ? HasValuesInCells(collectionNode.IndexedSlices) : false;

                        // Only allow one representation in each item
                        int numRepresentationsUsed = (hasScalarValue ? 1 : 0) + (hasLinearValues ? 1 : 0) + (hasIndexedValues ? 1 : 0);
                        if (numRepresentationsUsed > 1)
                        {
                            // For the error message, compute a source location that spans from the top-left
                            // cell to the bottom-right cell of the collections in this item.

                            List<SliceInfo> involvedSlices = new List<SliceInfo>();
                            if (hasScalarValue)
                                involvedSlices.Add(collectionNode.ScalarSlice);
                            if (hasLinearValues)
                                involvedSlices.AddRange(collectionNode.LinearSlices);
                            if (hasIndexedValues)
                                involvedSlices.AddRange(collectionNode.IndexedSlices);

                            int minSliceNdx = involvedSlices.Min(c => c.SliceIndex);
                            int maxSliceNdx = involvedSlices.Max(c => c.SliceIndex);

                            SpreadsheetCell minCell = content.GetCell(0, minSliceNdx);
                            SpreadsheetCell maxCell = content.GetCell(content.NumCellsPerSlice - 1, maxSliceNdx);
                            GameConfigSourceLocation location = new GameConfigSpreadsheetLocation(buildLog.SourceInfo,
                                rows: new GameConfigSpreadsheetLocation.CellRange(minCell.Row, maxCell.Row + 1),
                                columns: new GameConfigSpreadsheetLocation.CellRange(minCell.Column, maxCell.Column + 1));

                            buildLog.WithLocation(location).Error(Invariant($"Collection {collectionNode.Name} specified using multiple representations. Only one representation is allowed in a single item."));
                            break;
                        }

                        // Handle the representation used by the item
                        if (hasScalarValue)
                        {
                            // The whole collection is parsed from a single "scalar" cell
                            SliceInfo scalarSlice = collectionNode.ScalarSlice;
                            SpreadsheetCell cell = content.GetCell(0, scalarSlice.SliceIndex);
                            ScalarNode scalar = new ScalarNode(cell.Value, GameConfigSpreadsheetLocation.FromCell(buildLog.SourceInfo, cell));
                            children.Add(childId, scalar);

                            // Check other cells (if any) in the same slice are empty
                            for (int cellNdx = 1; cellNdx < content.NumCellsPerSlice; cellNdx++)
                            {
                                SpreadsheetCell extraCell = content.GetCell(cellNdx, scalarSlice.SliceIndex);
                                if (!string.IsNullOrEmpty(extraCell.Value))
                                {
                                    GameConfigSourceLocation location = GameConfigSpreadsheetLocation.FromCell(buildLog.SourceInfo, extraCell);
                                    string columnOrRowText = scalarSlice.IsColumn ? "column" : "row";
                                    buildLog.WithLocation(location).Error($"Extraneous content in {columnOrRowText} '{scalarSlice.FullPath}'.");
                                }
                            }
                        }
                        else if (hasLinearValues)
                        {
                            CollectionNode collection = ConvertLinearCollection(buildLog, content, depth, collectionNode.LinearNode);
                            children.Add(childId, collection);
                        }
                        else if (hasIndexedValues)
                        {
                            CollectionNode collection = ConvertIndexedCollection(buildLog, content, depth, collectionNode.IndexedNodes);
                            children.Add(childId, collection);
                        }
                        else // no values for any representation
                        {
                            // Output implicitly empty collection: in spreadsheets, the default behavior for parsing a collection
                            // that has no elements specified is to output an empty collection. Implicitly empty collections
                            // behave specially in the variant inheritance logic where implicitly empty collections in the
                            // variants use the value from the baseline.
                            children.Add(childId, new CollectionNode(Array.Empty<NodeBase>()));
                        }

                        break;
                    }

                    case PathNodeObject objectNode:
                    {
                        // Recursively convert nested nodes (only keep children with some items)
                        ObjectNode childObj = ConvertItemToObjectNode(buildLog, objectNode, content, depth + 1);
                        if (childObj.Members.Count > 0)
                            children.Add(childId, childObj);
                        break;
                    }

                    default:
                        throw new InvalidOperationException($"Invalid PathNode type {childNode.GetType().ToGenericTypeString()}");
                }
            }

            return new ObjectNode(children);
        }

        static bool RowStartsNewItem(List<SpreadsheetCell> row, SliceInfo[] identityColumns, SliceInfo[] variantIdColumns)
        {
            // If any cell in an index column has a value, this row starts a new item
            foreach (SliceInfo info in identityColumns)
            {
                if (!string.IsNullOrEmpty(row[info.SliceIndex].Value))
                    return true;
            }

            // If any cell in a variant id column has a value, this row starts a new item
            foreach (SliceInfo info in variantIdColumns)
            {
                if (!string.IsNullOrEmpty(row[info.SliceIndex].Value))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Convert the input spreadsheet data (<paramref name="contentRows"/>) into a set of syntax tree objects (<see cref="GameConfigSyntaxTree.RootObject"/>).
        /// The input spreadsheet is divided into items such that any non-empty cell for <paramref name="identityColumns"/> or <paramref name="variantIdColumns"/>
        /// begins a new item. The identity columns are inherited from the previous entries where they are not defined.
        /// </summary>
        /// <param name="buildLog"></param>
        /// <param name="rootNode"></param>
        /// <param name="contentRows"></param>
        /// <param name="identityColumns"></param>
        /// <param name="variantIdColumns"></param>
        /// <returns></returns>
        static List<RootObject> ParseItems(GameConfigBuildLog buildLog, PathNodeObject rootNode, List<List<SpreadsheetCell>> contentRows, SliceInfo[] identityColumns, SliceInfo[] variantIdColumns)
        {
            List<RootObject> resultNodes = new List<RootObject>();

            // First, skip fully empty rows at the start.
            // However, empty rows after the first nonempty row are _not_ ignored, because
            // an empty row can be a legitimate part of a multi-row item.
            int startNdx = 0;
            while (startNdx < contentRows.Count && contentRows[startNdx].All(cell => string.IsNullOrEmpty(cell.Value)))
                startNdx++;

            // Parse items.
            int numRows = contentRows.Count;
            string[] identityValues = new string[identityColumns.Length]; // keep track of identity column values to handle inheritance (from above)
            while (startNdx < contentRows.Count)
            {
                // Resolve identity values from first row of item
                for (int ndx = 0; ndx < identityColumns.Length; ndx++)
                {
                    SliceInfo       info = identityColumns[ndx];
                    SpreadsheetCell cell = contentRows[startNdx][info.SliceIndex];
                    if (!string.IsNullOrEmpty(cell.Value))
                        identityValues[ndx] = cell.Value;
                    else
                    {
                        // Fill the identity values also into the sheet row itself.
                        // This is needed to make sure the item gets the correct ConfigKey value, since it's parsed from the sheet
                        // just like other members of the object.
                        // \todo It's not nice to mutate the sheet rows. Fix more cleanly.
                        contentRows[startNdx][info.SliceIndex] = new SpreadsheetCell(identityValues[ndx], row: cell.Row, column: cell.Column);
                    }

                    // Each item must have valid values for all identity columns
                    if (string.IsNullOrEmpty(identityValues[ndx]))
                        buildLog.WithLocation(GameConfigSpreadsheetLocation.FromCell(buildLog.SourceInfo, cell)).Error("Empty value for identity column -- cannot infer from above either");
                }

                // Find the endNdx (start of next item or end-of-sheet)
                int endNdx = startNdx + 1;
                while (endNdx < numRows)
                {
                    if (RowStartsNewItem(contentRows[endNdx], identityColumns, variantIdColumns))
                        break;
                    endNdx += 1;
                }

                // Resolve rows in the original sheet for the object's location: this works also in case some rows were removed from the input, but not if rows were re-ordered
                int startRowNdx = contentRows[startNdx][0].Row;
                int endRowNdx = contentRows[endNdx - 1][0].Row + 1; // \note Use the last existing row to avoid out-of-bounds accesses
                GameConfigSpreadsheetLocation objectLocation = GameConfigSpreadsheetLocation.FromRows(buildLog.SourceInfo, startRowNdx, endRowNdx);

                // Parse the object id & value from the rows
                List<List<SpreadsheetCell>> itemRows = contentRows.GetRange(startNdx, endNdx - startNdx);
                ObjectId objectId = new ObjectId(identityValues.ToArray()); // \note defensive copy
                ItemSheetContent itemContent = new ItemSheetContent(itemRows, ItemSheetContent.Orientation.Horizontal);
                ObjectNode objectNode = ConvertItemToObjectNode(buildLog, rootNode, itemContent, depth: 0);
                resultNodes.Add(new RootObject(objectId, objectNode, objectLocation, aliases: null, variantId: null)); // \note aliases and variantId are filled later

                // Start parsing next element
                startNdx = endNdx;
            }

            return resultNodes;
        }

        static List<RootObject> TransformSpreadsheetImpl(GameConfigBuildLog buildLog, List<SpreadsheetCell> headerRow, List<List<SpreadsheetCell>> contentRows)
        {
            buildLog = buildLog.WithNewLocalErrorScope();

            // \note Assumes rectangular input spreadsheet (all rows must be padded to same length)

            // Parse header columns
            SliceInfo[] columns = ParseHeaderRow(buildLog, headerRow);
            //Console.WriteLine("Columns:");
            //foreach (ColumnInfo column in columns)
            //    Console.WriteLine("  {0}", column);
            //Console.WriteLine("");

            // Filter out comment columns
            columns = columns.Where(col => !col.HasTagWithName(BuiltinTags.Comment)).ToArray();

            // Check nonempty columns don't have missing header; i.e. that empty-header columns are fully empty.
            foreach (SliceInfo col in columns)
            {
                if (!col.HasEmptyHeader)
                    continue;

                SpreadsheetCell? nonemptyCell = TryFindNonemptyCellInColumn(col.SliceIndex, contentRows);
                if (!nonemptyCell.HasValue)
                    continue;

                GameConfigSourceLocation nonemptyCellLocation = GameConfigSpreadsheetLocation.FromCell(buildLog.SourceInfo, nonemptyCell.Value);
                buildLog.WithLocation(nonemptyCellLocation).Error($"This column contains nonempty cells, but its header cell is empty, which is not supported. If this column is meant to be ignored, put two slashes (//) in the header cell.");
            }

            // Filter out empty columns now that we're done with the above check.
            columns = columns.Where(col => !col.HasEmptyHeader).ToArray();

            // Create hierarchy from the columns (for nested members)
            PathNodeObject rootNode = CreateObjectNode(buildLog, PathSegment.Root.Name, columns, depth: 0);
            //Console.WriteLine("Hierarchy:\n{0}", rootNode.ToString());

            // Resolve key/identity columns. At least one key column must be specified.
            SliceInfo[] keyColumns = columns.Where(col => col.IsKeySlice).ToArray();
            if (keyColumns.Length == 0)
                buildLog.Error($"No key columns were specified. At least one column must have the '#{BuiltinTags.Key}' tag.");

            // Resolve variant id column, if any (there is at most 1, identified by the fixed VariantIdKey).
            SliceInfo[] variantIdColumns = columns.Where(col => col.FullPath == GameConfigSyntaxTreeUtil.VariantIdKey).ToArray();

            // Check that identity columns do not have variants.
            foreach (SliceInfo column in columns)
            {
                if (column.IsKeySlice && column.PathSegments[column.PathSegments.Length - 1].VariantId != null)
                {
                    SpreadsheetCell cell = headerRow[column.SliceIndex];
                    GameConfigSourceLocation location = GameConfigSpreadsheetLocation.FromCell(buildLog.SourceInfo, cell);
                    buildLog.WithLocation(location).Error(message: $"Identity column {column.FullPath} has a variant override, which isn't supported");
                }
            }

            // If any errors from the header, bail out
            if (buildLog.HasLocalErrors())
                return null;

            // Split into items, group by item and variant id, and parse each item-with-variants to a syntax tree object
            List<RootObject> parsedObjects = ParseItems(buildLog, rootNode, contentRows, keyColumns, variantIdColumns);
            return parsedObjects;
        }

        public static List<RootObject> TransformLibrarySpreadsheet(GameConfigBuildLog buildLog, SpreadsheetContent content)
        {
            // \note Assumes rectangular input spreadsheet (all rows must be padded to same length)

            return TransformSpreadsheetImpl(
                buildLog,
                headerRow: content.Cells[0],
                contentRows: content.Cells.Skip(1).ToList());
        }

        static SliceInfo[] ParseKeyValueHeader(GameConfigBuildLog buildLog, List<SpreadsheetCell> memberPathColumn, List<SpreadsheetCell> variantIdColumnMaybe)
        {
            GameConfigSourceInfo sourceInfo = buildLog.SourceInfo;

            // Parse the header cells
            List<SliceInfo> rows = new List<SliceInfo>();
            SliceInfo lastNonVariantRow = null;
            for (int ndx = 0; ndx < memberPathColumn.Count; ndx++)
            {
                SpreadsheetCell memberPathCell = memberPathColumn[ndx];
                GameConfigSpreadsheetLocation cellLocation = GameConfigSpreadsheetLocation.FromCell(sourceInfo, memberPathCell);
                GameConfigSpreadsheetLocation rowLocation = GameConfigSpreadsheetLocation.FromRow(sourceInfo, memberPathCell.Row);

                SpreadsheetCell? variantIdCellMaybe = variantIdColumnMaybe != null ? variantIdColumnMaybe[ndx] : null;

                if (string.IsNullOrEmpty(memberPathCell.Value) && string.IsNullOrEmpty(variantIdCellMaybe?.Value))
                {
                    // Empty header. This will only be used for checking that the row is fully empty.
                    rows.Add(new SliceInfo(ndx, fullPath: "", Array.Empty<PathSegment>(), Array.Empty<NodeTag>(), rowLocation));
                }
                else if (string.IsNullOrEmpty(memberPathCell.Value))
                {
                    string variantId = variantIdCellMaybe.Value.Value;
                    if (lastNonVariantRow == null)
                        buildLog.WithLocation(cellLocation).Error(message: "Found variant override but there is no corresponding normal row above it");
                    else
                        rows.Add(CreateVariantOverrideSliceInfo(ndx, lastNonVariantRow, variantId, rowLocation));
                }
                else if (memberPathCell.Value.StartsWith("/:", StringComparison.Ordinal))
                {
                    // \todo More useful message
                    buildLog.WithLocation(cellLocation).Error($"The '/:' syntax is not supported in the header column of a {nameof(GameConfigKeyValue)} sheet");
                }
                else
                {
                    // Row with a path.

                    string variantIdMaybe = variantIdCellMaybe?.Value;

                    try
                    {
                        SliceInfo row = ParseHeaderCell(ndx, memberPathCell, rowLocation);

                        if (row.FullPath == GameConfigSyntaxTreeUtil.VariantIdKey
                         || row.FullPath == GameConfigSyntaxTreeUtil.AliasesKey)
                        {
                            buildLog.WithLocation(cellLocation).Error($"'{row.FullPath}' is not supported in the header column of a {nameof(GameConfigKeyValue)} sheet");
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(variantIdMaybe))
                            {
                                rows.Add(row);
                                lastNonVariantRow = row;
                            }
                            else
                            {
                                row = CreateVariantOverrideSliceInfo(ndx, row, variantIdMaybe, rowLocation);
                                rows.Add(row);
                            }
                        }
                    }
                    catch (ParseError ex)
                    {
                        buildLog.WithLocation(cellLocation).Error(message: $"Failed to parse row header '{memberPathCell.Value}': {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        buildLog.WithLocation(cellLocation).Error(message: $"Internal error while parsing row header '{memberPathCell.Value}'", ex);
                    }
                }
            }
            return rows.ToArray();
        }

        public const string KeyValueSheetMemberPathHeaderId     = "Member";
        public const string KeyValueSheetMemberValueHeaderId    = "Value";

        public static ObjectNode TransformKeyValueSpreadsheet(GameConfigBuildLog buildLog, SpreadsheetContent content)
        {
            buildLog = buildLog.WithNewLocalErrorScope();

            // A KeyValue sheet is similar to (but not exactly like) a transposed library sheet with just 1 item (possibly with variants).
            // The "schema" header (which specifies the member names (or generally paths)) is a column,
            // instead of a row like for libraries.
            // The member values are accordingly also in a column (or multiple columns for implicitly-indexed Collection[]).
            // A KeyValue also has a header row, but that is a different kind of header - see below.
            //
            // Due to the header differences, a KeyValue sheet cannot be naively just transposed and then
            // parsed as a library. However, some parts of library parsing can be reused, in particular
            // CreateObjectNode (resolving the hierarchy from the schema header) and ConvertItemToObjectNode
            // (parsing the members into an ObjectNode).

            List<SpreadsheetCell> headerRow = content.Cells[0];
            List<List<SpreadsheetCell>> contentRows = content.Cells.Skip(1).ToList();

            // Parse the header row which specifies /Variant (optional), Member, and Value columns.
            // The columns can be in any order, and Value may be followed by empty header cells,
            // indicating that the value can consist of multiple columns (used for implicitly-indexed
            // collections, which are equivalent to vertical collections in library sheets, but in
            // KeyValue sheets they are horizontal).
            int variantIdColNdx = -1;
            int memberPathColNdx = -1;
            int memberValueStartColNdx = -1;
            int memberValueEndColNdx = -1;
            {
                int colNdx = 0;
                while (colNdx < headerRow.Count)
                {
                    SpreadsheetCell cell = headerRow[colNdx];
                    GameConfigSpreadsheetLocation location = GameConfigSpreadsheetLocation.FromCell(content.SourceInfo, cell);

                    if (cell.Value == KeyValueSheetMemberValueHeaderId)
                    {
                        if (memberValueStartColNdx >= 0)
                            buildLog.WithLocation(location).Error($"Duplicate '{KeyValueSheetMemberValueHeaderId}' header cell");

                        int endNdx = colNdx+1;
                        while (endNdx < headerRow.Count && string.IsNullOrEmpty(headerRow[endNdx].Value))
                            endNdx++;

                        memberValueStartColNdx = colNdx;
                        memberValueEndColNdx = endNdx;

                        colNdx = endNdx;
                    }
                    else
                    {
                        if (cell.Value == GameConfigSyntaxTreeUtil.VariantIdKey)
                        {
                            if (variantIdColNdx >= 0)
                                buildLog.WithLocation(location).Error($"Duplicate '{GameConfigSyntaxTreeUtil.VariantIdKey}' header cell");
                            variantIdColNdx = colNdx;
                        }
                        else if (cell.Value == KeyValueSheetMemberPathHeaderId)
                        {
                            if (memberPathColNdx >= 0)
                                buildLog.WithLocation(location).Error($"Duplicate '{KeyValueSheetMemberPathHeaderId}' header cell");
                            memberPathColNdx = colNdx;
                        }
                        else if (string.IsNullOrEmpty(cell.Value))
                        {
                            // Check nonempty columns don't have missing header; i.e. that empty-header columns are fully empty.

                            SpreadsheetCell? nonemptyCell = TryFindNonemptyCellInColumn(colNdx, contentRows);
                            if (nonemptyCell.HasValue)
                            {
                                GameConfigSpreadsheetLocation nonemptyCellLocation = GameConfigSpreadsheetLocation.FromCell(buildLog.SourceInfo, nonemptyCell.Value);
                                buildLog.WithLocation(nonemptyCellLocation).Error($"This column contains nonempty cells, but its header cell is empty, which is not supported. If this column is meant to be ignored, put two slashes (//) in the header cell.");
                            }
                        }
                        else
                            buildLog.WithLocation(location).Error($"Unknown cell '{cell.Value}' in header row. Valid header cells in a {nameof(GameConfigKeyValue)} sheet are '{KeyValueSheetMemberPathHeaderId}', '{KeyValueSheetMemberValueHeaderId}', and '{GameConfigSyntaxTreeUtil.VariantIdKey}' .");

                        colNdx++;
                    }
                }
            }

            // Some header validation
            GameConfigSpreadsheetLocation headerLocation = GameConfigSpreadsheetLocation.FromRows(content.SourceInfo, headerRow[0].Row, headerRow[0].Row + 1);
            if (memberPathColNdx < 0)
                buildLog.WithLocation(headerLocation).Error($"Header row is missing '{KeyValueSheetMemberPathHeaderId}'");
            if (memberValueStartColNdx < 0)
                buildLog.WithLocation(headerLocation).Error($"Header row is missing '{KeyValueSheetMemberValueHeaderId}'");

            // If any errors from the header, bail out
            if (buildLog.HasLocalErrors())
                return null;

            // Parse the header column, which contains the member paths ("Member").
            // Additionally there may be another column ("/Variant") which defines the variant id.
            // This is similar to the header *row* of library sheets.
            SliceInfo[] rows = ParseKeyValueHeader(
                buildLog,
                memberPathColumn: contentRows.Select(row => row[memberPathColNdx]).ToList(),
                variantIdColumnMaybe:
                    variantIdColNdx < 0
                    ? null
                    : contentRows.Select(row => row[variantIdColNdx]).ToList());

            // Filter out comments
            rows = rows.Where(row => !row.HasTagWithName(BuiltinTags.Comment)).ToArray();

            // Check nonempty rows don't have missing header; i.e. that empty-header rows are fully empty.
            foreach (SliceInfo row in rows)
            {
                if (!row.HasEmptyHeader)
                    continue;

                SpreadsheetCell? nonemptyCell = TryFindNonemptyCellInRow(row.SliceIndex, contentRows);
                if (nonemptyCell.HasValue)
                {
                    GameConfigSourceLocation nonemptyCellLocation = GameConfigSpreadsheetLocation.FromCell(buildLog.SourceInfo, nonemptyCell.Value);
                    buildLog.WithLocation(nonemptyCellLocation).Error($"This row contains nonempty cells, but does not specify the member name. If this row is meant to be ignored, put two slashes (//) in its leftmost cell.");
                }
            }
            // Filter out empty rows now that we're done with the above check.
            rows = rows.Where(row => !row.HasEmptyHeader).ToArray();

            // Create hierarchy from the rows (for nested members)
            PathNodeObject rootNode = CreateObjectNode(buildLog, PathSegment.Root.Name, rows, depth: 0);
            //Console.WriteLine("Hierarchy:\n{0}", rootNode.ToString());

            // If any errors from the header, bail out
            if (buildLog.HasLocalErrors())
                return null;

            // Parse the item from its rows.
            // Here we use the same ConvertItemToObjectNode as for library sheets, but with a transposed ItemSheetContent.
            int numValueColumns = memberValueEndColNdx-memberValueStartColNdx;
            List<List<SpreadsheetCell>> itemRows = contentRows.Select(row => row.GetRange(memberValueStartColNdx, numValueColumns)).ToList();
            ItemSheetContent itemContent = new ItemSheetContent(itemRows, ItemSheetContent.Orientation.Vertical);
            ObjectNode node = ConvertItemToObjectNode(buildLog, rootNode, itemContent, depth: 0);
            return node;
        }
    }
}
