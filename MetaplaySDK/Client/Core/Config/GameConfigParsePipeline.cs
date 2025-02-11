// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

//#define VERBOSE_PIPELINE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static System.FormattableString;

namespace Metaplay.Core.Config
{
    public class GameConfigParseLibraryPipelineConfig
    {
        public readonly GameConfigSyntaxAdapterAttribute[] SyntaxAdapterAttribs;
        public readonly UnknownConfigMemberHandling        UnknownMemberHandling;

        public GameConfigParseLibraryPipelineConfig(
            GameConfigSyntaxAdapterAttribute[] syntaxAdapterAttribs = null,
            UnknownConfigMemberHandling unknownMemberHandling = UnknownConfigMemberHandling.Error)
        {
            SyntaxAdapterAttribs = syntaxAdapterAttribs;
            UnknownMemberHandling = unknownMemberHandling;
        }
    }

    public class GameConfigParseKeyValuePipelineConfig
    {
        public readonly GameConfigSyntaxAdapterAttribute[] SyntaxAdapterAttribs;
        public readonly UnknownConfigMemberHandling UnknownMemberHandling;

        public GameConfigParseKeyValuePipelineConfig(
            GameConfigSyntaxAdapterAttribute[] syntaxAdapterAttribs = null,
            UnknownConfigMemberHandling unknownMemberHandling = UnknownConfigMemberHandling.Error)
        {
            SyntaxAdapterAttribs = syntaxAdapterAttribs;
            UnknownMemberHandling = unknownMemberHandling;
        }
    }

    /// <summary>
    /// How to treat nonexistent members in config input data, e.g. when a config sheet has
    /// a mistyped column name that does not map to any member in the C# config item type.
    /// </summary>
    public enum UnknownConfigMemberHandling
    {
        /// <summary>
        /// Allow and ignore unknown members.
        /// </summary>
        Ignore,
        /// <summary>
        /// Produce build log warnings about unknown members.
        /// </summary>
        Warning,
        /// <summary>
        /// Produce build log errors about unknown members, causing the config build to fail.
        /// </summary>
        Error,
    }

    /// <summary>
    /// Pipeline for parsing game config input sources (eg, spreadsheets) into syntax tree objects.
    /// </summary>
    public static class GameConfigParsePipeline
    {
        public static void PreprocessSpreadsheet(GameConfigBuildLog buildLog, SpreadsheetContent spreadsheetContent, bool isLibrary, GameConfigSyntaxAdapterAttribute[] syntaxAdapters)
        {
            List<List<SpreadsheetCell>> rows = spreadsheetContent.Cells;

            // Fail on empty sheet
            if (rows.Count == 0)
            {
                GameConfigSourceLocation location = GameConfigSpreadsheetLocation.FromFullSheet(buildLog.SourceInfo);
                buildLog.WithLocation(location).Error("Input sheet is completely empty");
                return;
            }

            // Fail on empty header row (only for libraries)
            if (isLibrary && rows[0].Count == 0)
            {
                GameConfigSourceLocation location = GameConfigSpreadsheetLocation.FromRows(buildLog.SourceInfo, 0, 1);
                buildLog.WithLocation(location).Error("Input sheet header row is empty");
                return;
            }

            // Pad all rows with empty cells to equal length
            PadRowsToEqualLength(spreadsheetContent);

            // If syntax adapters are defined, apply them
            // \note This is done before filtering out comments (below), because syntax adapters might
            //       introduce comments (e.g. by replacing old custom comment prefix with the now-standard
            //       comment prefix "//").
            if (syntaxAdapters != null && syntaxAdapters.Length > 0)
            {
                ApplySyntaxAdapters(syntaxAdapters, spreadsheetContent);
                // Pad rows to equal length again, since syntax adapters might've modified it.
                PadRowsToEqualLength(spreadsheetContent);
            }

            // Filter out commented rows (row starts with '//')
            rows.RemoveAll(row => row[0].Value.StartsWith("//", StringComparison.Ordinal));

            // Filter out commented columns (header cell starts with '//')
            List<bool> isCommentColumn = rows[0].Select(cell => cell.Value.StartsWith("//", StringComparison.Ordinal)).ToList();
            if (isCommentColumn.Contains(true))
            {
                foreach (List<SpreadsheetCell> row in rows)
                {
                    int newCol = 0;
                    for (int oldCol = 0; oldCol < row.Count; oldCol++)
                    {
                        if (!isCommentColumn[oldCol])
                        {
                            row[newCol] = row[oldCol];
                            newCol++;
                        }
                    }
                    row.RemoveRange(newCol, row.Count - newCol);
                }
            }
        }

        static void PadRowsToEqualLength(SpreadsheetContent spreadsheetContent)
        {
            List<List<SpreadsheetCell>> rows = spreadsheetContent.Cells;

            int maxColumns = rows.Max(row => row.Count);
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                List<SpreadsheetCell> row = rows[rowIndex];

                if (row.Count == maxColumns)
                    continue;

                // Figure out the row number.
                // Most often, this row already is nonempty so we can get the row number from an existing cell in this row.
                // Otherwise, try to guess the row number from the previous row,
                // or leave as 0 if this is the first row.
                // \todo This is a kludge and is not guaranteed to produce correct results.
                //       Maybe SpreadsheetContent should be guaranteed to always have equal-length rows
                //       so that we don't need to do this here.
                int rowNumber;
                if (row.Count > 0)
                    rowNumber = row[0].Row;
                else if (rowIndex > 0)
                    rowNumber = rows[rowIndex - 1][0].Row + 1;
                else
                    rowNumber = 0;

                while (row.Count < maxColumns)
                    row.Add(new SpreadsheetCell("", row: rowNumber, column: row.Count));
            }
        }

        /// <summary>
        /// Apply any necessary syntax modifications to spreadsheet to convert from old legacy syntax to the
        /// syntax that the new game config builder understands.
        /// </summary>
        static void ApplySyntaxAdapters(GameConfigSyntaxAdapterAttribute[] attribs, SpreadsheetContent content)
        {
            // If requested, ensure a GameConfigKeyValue sheet has its R26-style header:
            // "Member" to identify the member name (or path if complex) column,
            // "Value" to identify the member value column (or the first of the member value
            // columns, in case multi-column arrays are used),
            // and optionally "/Variant" to identify the variant id column.
            if (attribs.Any(attrib => attrib.EnsureHasKeyValueSheetHeader))
            {
                // Do nothing if the header already has a new-style cell.
                bool hasNewStyleHeader = content.Cells.Count > 0
                                         && (content.Cells[0].Any(cell => cell.Value == GameConfigSpreadsheetReader.KeyValueSheetMemberPathHeaderId)
                                             || content.Cells[0].Any(cell => cell.Value == GameConfigSpreadsheetReader.KeyValueSheetMemberValueHeaderId));

                if (!hasNewStyleHeader)
                {
                    // Add the new header.
                    // Note that the pre-R26 column order is quite rigid ("variant, member name, member value"
                    // where variant is optional, and furthermore any extra columns are ignored) so there's only
                    // a few forms the new header can take.

                    // Pre-R26 supported KeyValue configs either with or without header.
                    // We detect if the header is present by checking for "/Variant" in the
                    // first column, which is the only thing the old header can contain.
                    bool hasOldStyleVariantHeader = content.Cells.Count > 0
                                                    && content.Cells[0][0].Value == GameConfigSyntaxTreeUtil.VariantIdKey;

                    if (hasOldStyleVariantHeader)
                    {
                        // Sanity check that there is no other content after /Variant.
                        if (content.Cells.Count > 0
                            && content.Cells[0].Skip(1).Any(cell => !string.IsNullOrEmpty(cell.Value)))
                        {
                            throw new InvalidOperationException($"{content.Name}: {GameConfigSyntaxTreeUtil.VariantIdKey} must be the only nonempty cell in the header row");
                        }

                        // Header row already exists - modify it to include the Member and Value cells.
                        // Also add one comment column to support existing configs which relied on extraneous columns being ignored by the old parser.
                        content.Cells[0] = new List<SpreadsheetCell>
                        {
                            new SpreadsheetCell(GameConfigSyntaxTreeUtil.VariantIdKey,                          row: 0, column: 0),
                            new SpreadsheetCell(GameConfigSpreadsheetReader.KeyValueSheetMemberPathHeaderId,    row: 0, column: 1),
                            new SpreadsheetCell(GameConfigSpreadsheetReader.KeyValueSheetMemberValueHeaderId,   row: 0, column: 2),
                            new SpreadsheetCell("// Comment",                                                   row: 0, column: 3),
                        };
                    }
                    else
                    {
                        // Sanity check that there is no /Variant in the wrong place.
                        if (content.Cells.Count > 0
                            && content.Cells[0].Skip(1).Any(cell => cell.Value == GameConfigSyntaxTreeUtil.VariantIdKey))
                        {
                            throw new InvalidOperationException($"{content.Name}: {GameConfigSyntaxTreeUtil.VariantIdKey} must be in the leftmost column");
                        }

                        // Header row doesn't yet exist - prepend the new-style header.
                        // Because there was no "/Variant" cell, don't add that in this new header either.
                        // Also add one comment column to support existing configs which relied on extraneous columns being ignored by the old parser.
                        content.Cells.Insert(0, new List<SpreadsheetCell>
                        {
                            new SpreadsheetCell(GameConfigSpreadsheetReader.KeyValueSheetMemberPathHeaderId,    row: 0, column: 0),
                            new SpreadsheetCell(GameConfigSpreadsheetReader.KeyValueSheetMemberValueHeaderId,   row: 0, column: 1),
                            new SpreadsheetCell("// Comment",                                                   row: 0, column: 2),
                        });
                    }
                }
            }

            List<SpreadsheetCell> headerRow = content.Cells[0];

            // Apply replacement rules from all adapters
            foreach (GameConfigSyntaxAdapterAttribute attrib in attribs)
            {
                // Apply all replace rules to the header row
                // \note We don't care if a rule doesn't match such that the rules work on already-converted source data
                foreach (GameConfigSyntaxAdapterAttribute.ReplaceRule rule in attrib.HeaderReplaces)
                {
                    for (int ndx = 0; ndx < headerRow.Count; ndx++)
                    {
                        SpreadsheetCell cell = headerRow[ndx];
                        string value = cell.Value;

                        // Replace cell value if matches the rule
                        if (value == rule.From)
                            headerRow[ndx] = new SpreadsheetCell(rule.To, cell.Row, cell.Column);
                    }
                }

                // Apply all prefix replace rules to the header row
                // \note We don't care if a rule doesn't match such that the rules work on already-converted source data
                foreach (GameConfigSyntaxAdapterAttribute.ReplaceRule rule in attrib.HeaderPrefixReplaces)
                {
                    for (int ndx = 0; ndx < headerRow.Count; ndx++)
                    {
                        SpreadsheetCell cell = headerRow[ndx];
                        string value = cell.Value;

                        // Replace cell value if matches the rule
                        if (value.StartsWith(rule.From, StringComparison.Ordinal))
                        {
                            string replaced = rule.To + value.Substring(rule.From.Length);
                            headerRow[ndx] = new SpreadsheetCell(replaced, cell.Row, cell.Column);
                        }
                    }
                }
            }

            // Convert all '[Array]' notation to 'Array[<ndx>]'
            Dictionary<string, int> arrayIndexCounters = new Dictionary<string, int>();
            for (int ndx = 0; ndx < headerRow.Count; ndx++)
            {
                SpreadsheetCell cell = headerRow[ndx];
                string value = cell.Value;

                if (value.StartsWith("[", StringComparison.Ordinal) && value.EndsWith("]", StringComparison.Ordinal))
                {
                    // Resolve index for this value
                    int index = arrayIndexCounters.GetValueOrDefault(value, 0);

                    // Update cell to 'Name[<ndx>]'
                    string name = value.Substring(1, value.Length - 2);
                    headerRow[ndx] = new SpreadsheetCell(Invariant($"{name}[{index}]"), cell.Row, cell.Column);

                    // Update the index counter
                    arrayIndexCounters[value] = index + 1;
                }
            }
        }

        // This is standard to resemble the "standard" pipeline that we could offer out-of-the-box.
        // A bunch of configuration options (or metadata) is needed to customize the behavior for
        // most games' needs. Fully customizing the pipeline should also be supported as an escape
        // hatch for the features we don't have. Perhaps also some injecting of custom phases of
        // processing.
        static VariantConfigItem<TKey, TItem>[] ProcessSpreadsheetLibraryImpl<TKey, TItem>(GameConfigBuildLog buildLog, GameConfigParseLibraryPipelineConfig config, SpreadsheetContent sheet) where TItem : IHasGameConfigKey<TKey>, new()
        {
            buildLog = buildLog.WithNewLocalErrorScope();

#if VERBOSE_PIPELINE
            Console.WriteLine("Parsing spreadsheet:\n{0}", sheet);
#endif

            // Preprocess spreadsheet & bail on error
            PreprocessSpreadsheet(buildLog, sheet, isLibrary: true, config.SyntaxAdapterAttribs);
            if (buildLog.HasLocalErrors())
                return null;

            // Transform the spreadsheet into (sparse) syntax tree objects, with only structural/layout changes.
            List<GameConfigSyntaxTree.RootObject> objects = GameConfigSpreadsheetReader.TransformLibrarySpreadsheet(buildLog, sheet);

#if VERBOSE_PIPELINE
            // \note objects can be null if early errors happened. But also can be non-null if individual items errored.
            if (objects != null)
            {
                for (int ndx = 0; ndx < objects.Count; ndx++)
                    Console.WriteLine("Object #{0}:\n{1}", ndx, objects[ndx].Node.TreeToString(depth: 1));
            }
#endif

            // If any errors, bail out
            if (buildLog.HasLocalErrors())
                return null;

            // Canonicalize "/Aliases" annotations into RootObject.Aliases.
            // Also canonicalize variant annotations into root-level objects with their VariantId assigned.
            // This may produce more root objects due to multiple variants being specified within one input item.
            objects = objects.SelectMany(obj =>
            {
                obj = GameConfigSyntaxTreeUtil.ExtractAliases(obj);
                return GameConfigSyntaxTreeUtil.ExtractVariants(obj);
            }).ToList();


            // Detect duplicate objects: (ItemId, VariantId) pair must be unique
            GameConfigSyntaxTreeUtil.DetectDuplicateObjects(buildLog, objects);
            if (buildLog.HasLocalErrors())
                return null;

            // Fill in missing ids first, because variant inheriting relies on ids.
            // \todo [nuutti] Add support for inheriting empty cells from above where sensible (needs design)
            GameConfigSyntaxTreeUtil.InheritVariantValuesFromBaseline(objects);

            // Parse the final output types from the syntax tree.
            GameConfigOutputItemParser.ParseOptions parseOptions = new GameConfigOutputItemParser.ParseOptions(config.UnknownMemberHandling);
            List<VariantConfigItem<TKey, TItem>> items = new List<VariantConfigItem<TKey, TItem>>();
            foreach (GameConfigSyntaxTree.RootObject obj in objects)
                items.Add(GameConfigOutputItemParser.ParseItem<TKey, TItem>(parseOptions, buildLog, obj));

#if VERBOSE_PIPELINE
            Console.WriteLine("Parsed (variant) items:");
            for (int ndx = 0; ndx < items.Count; ndx++)
            {
                VariantConfigItem<TKey, TItem> item = items[ndx];
                string variantTextMaybe = item.VariantIdMaybe == null ? "" : $" (variant {item.VariantIdMaybe})";
                Console.WriteLine("#{0}{1}: {2}", ndx, variantTextMaybe, PrettyPrint.Verbose(item.Item));
            }
#endif

            // If any errors, bail out
            if (buildLog.HasLocalErrors())
                return null;

            // Check there are no null ConfigKeys. Any missing keys should've been inherited at this point.
            foreach (VariantConfigItem<TKey, TItem> item in items)
            {
                if (item.Item.ConfigKey == null)
                    buildLog.WithVariantId(item.VariantIdMaybe).WithLocation(item.SourceLocation).Error("Item has null ConfigKey, which is unexpected at this point.");

            }

            // If any errors, bail out
            if (buildLog.HasLocalErrors())
                return null;

            return items.ToArray();
        }

        public static VariantConfigItem<TKey, TItem>[] ProcessSpreadsheetLibrary<TKey, TItem>(GameConfigBuildLog buildLog, GameConfigParseLibraryPipelineConfig config, SpreadsheetContent sheet) where TItem : IHasGameConfigKey<TKey>, new()
        {
            VariantConfigItem<TKey, TItem>[] items = ProcessSpreadsheetLibraryImpl<TKey, TItem>(buildLog, config, sheet);

#if VERBOSE_PIPELINE
            // Report errors if any
            if (buildLog.HasErrors())
            {
                Console.WriteLine("ERRORS OCCURRED DURING BUILD:");
                foreach (GameConfigBuildMessage msg in buildLog.Messages)
                    Console.WriteLine("{0}", msg);
                Console.WriteLine("");
            }
#endif

            return items;
        }

        static VariantConfigStructureMember[] ProcessSpreadsheetKeyValueImpl<TKeyValue>(GameConfigBuildLog buildLog, GameConfigParseKeyValuePipelineConfig config, SpreadsheetContent sheet)
        {
            buildLog = buildLog.WithNewLocalErrorScope();

            // Preprocess spreadsheet & bail on error
            PreprocessSpreadsheet(buildLog, sheet, isLibrary: false, config.SyntaxAdapterAttribs);
            if (buildLog.HasLocalErrors())
                return null;

            // Parse the sheet into one ObjectNode.
            // At this point, transformedObj contains both baseline and variant members (possibly nested).
            // ExtractVariants is used below to separate them.
            GameConfigSyntaxTree.ObjectNode transformedObj = GameConfigSpreadsheetReader.TransformKeyValueSpreadsheet(buildLog, sheet);

#if VERBOSE_PIPELINE
            if (transformedObj != null)
                Console.WriteLine("Object:\n{0}", transformedObj.TreeToString());
#endif

            // If any errors, bail out
            if (buildLog.HasLocalErrors())
                return null;

            // Separate baseline and variants from transformedObj into separate objects.
            // variantObjects contains one root object per variant (+ baseline) present within transformedObj,
            // each root object containing just the members overridden by that variant.
            List<GameConfigSyntaxTree.RootObject> objects = GameConfigSyntaxTreeUtil.ExtractVariants(
                new GameConfigSyntaxTree.RootObject(
                    new GameConfigSyntaxTree.ObjectId(new string[] { }),
                    transformedObj,
                    transformedObj.Location,
                    aliases: null,
                    variantId: null)).ToList();

            // Inherit from baseline into variants.
            // This also removes top-level empty collections from variants.
            // See comment on InheritKeyValueVariantValuesFromBaseline for details.
            GameConfigSyntaxTreeUtil.InheritKeyValueVariantValuesFromBaseline(objects);

#if VERBOSE_PIPELINE
            foreach (GameConfigSyntaxTree.RootObject obj in objects)
            {
                string variantDescription = obj.VariantId == null ? "baseline" : $"variant {obj.VariantId}";
                Console.WriteLine("Variant-extracted object ({0}):\n{1}", variantDescription, obj.Node.TreeToString());
            }
#endif

            // Parse the final output typed members from the syntax tree.
            GameConfigOutputItemParser.ParseOptions parseOptions = new GameConfigOutputItemParser.ParseOptions(config.UnknownMemberHandling);
            List<VariantConfigStructureMember> members = GameConfigOutputItemParser.ParseKeyValueStructureMembers<TKeyValue>(parseOptions, buildLog, objects);

#if VERBOSE_PIPELINE
            Console.WriteLine("Parsed (variant) members:");
            for (int ndx = 0; ndx < members.Count; ndx++)
            {
                VariantConfigStructureMember member = members[ndx];
                string variantTextMaybe = member.VariantIdMaybe == null ? "" : $" (variant {member.VariantIdMaybe})";
                Console.WriteLine("#{0}{1}: {2} = {3}", ndx, variantTextMaybe, member.Member.MemberInfo.Name, PrettyPrint.Verbose(member.Member.MemberValue));
            }
            Console.WriteLine();
#endif

            // If any errors, bail out
            if (buildLog.HasLocalErrors())
                return null;

            return members.ToArray();
        }

        public static VariantConfigStructureMember[] ProcessSpreadsheetKeyValue<TKeyValue>(GameConfigBuildLog buildLog, GameConfigParseKeyValuePipelineConfig config, SpreadsheetContent sheet)
        {
            VariantConfigStructureMember[] members = ProcessSpreadsheetKeyValueImpl<TKeyValue>(buildLog, config, sheet);

#if VERBOSE_PIPELINE
            // Report errors if any
            if (buildLog.HasErrors())
            {
                Console.WriteLine("ERRORS OCCURRED DURING BUILD:");
                foreach (GameConfigBuildMessage msg in buildLog.Messages)
                    Console.WriteLine("{0}", msg);
                Console.WriteLine("");
            }
#endif

            return members;
        }
    }
}
