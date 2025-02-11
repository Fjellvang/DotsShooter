// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static System.FormattableString;

namespace Metaplay.Core.Config
{
    /// <summary>
    /// Helper methods for parsing and validating GameConfig contents.
    /// </summary>
    public static class GameConfigHelper
    {
        /// <summary>
        /// Parse UTF-8 encoded comma-separated values (.csv) payload to a <see cref="SpreadsheetContent"/>.
        /// </summary>
        public static SpreadsheetContent ParseCsvToSpreadsheet(string filePath, byte[] bytes)
        {
            SpreadsheetFileSourceInfo sourceInfo = new SpreadsheetFileSourceInfo(filePath);

            // \todo [petri] quite wasteful parsing
            using (IOReaderStream ioreader = new IOReader(bytes).ConvertToStream())
            using (CsvStream reader = new CsvStream(filePath, ioreader))
            {
                List<List<string>> cells = new List<List<string>>();
                foreach (string[] row in reader)
                {
                    // Don't include trailing empty cells.
                    // The purpose of this is to produce similar content as Google Sheet fetching,
                    // which does not produce trailing empty cells.
                    int count = row.Length;
                    while (count > 0 && row[count - 1] == "")
                        count--;
                    cells.Add(row.Take(count).ToList());
                }
                return new SpreadsheetContent(filePath, cells, sourceInfo);
            }
        }

        /// <summary>
        /// Validate that the sheet contains no Google Sheet error values ('#REF!', '#NULL!', etc.) fields.
        /// </summary>
        /// <param name="sheet"></param>
        public static void ValidateNoGoogleSheetErrors(SpreadsheetContent sheet)
        {
            for (int rowNdx = 0; rowNdx < sheet.Cells.Count; rowNdx++)
            {
                List<SpreadsheetCell> row = sheet.Cells[rowNdx];
                for (int colNdx = 0; colNdx < row.Count; colNdx++)
                {
                    string value = row[colNdx].Value;
                    if (IsCellValueGoogleSheetError(value))
                        throw new Exception($"Sheet {sheet.Name} has an error value '{value}' in row {rowNdx}, column {colNdx}");
                }
            }
        }

        /// <summary>
        /// Determines if a cell value is a well-known error or invalid value. This means the cell formula errored out and did not compute a valid value for the cell.
        /// See: https://infoinspired.com/google-docs/spreadsheet/different-error-types-in-google-sheets/
        /// </summary>
        static bool IsCellValueGoogleSheetError(string value)
        {
            return value == "#NULL!" || value == "#DIV/0!" || value == "#VALUE!" || value == "#REF!" || value == "#NAME?" || value == "#NUM!" || value == "#N/A";
        }

        /// <summary>
        /// Filter out all rows not matching the specified environment. Columns names starting with '$' (eg, '$dev' or '$prod')
        /// are considered as meta-columns marking which build configuration each of the rows should be included in.
        /// </summary>
        /// <param name="columnName"></param>
        /// <param name="sheet"></param>
        /// <returns></returns>
        public static SpreadsheetContent FilterSelectedEnvironment(string columnName, SpreadsheetContent sheet)
        {
            // Resolve columns that start with '$'
            Dictionary<string, int> envColumns =
                sheet.Cells[0]
                .Select((SpreadsheetCell cell, int colNdx) => new { Name = cell.Value, Ndx = colNdx })
                .Where(col => col.Name.StartsWith("$", StringComparison.Ordinal))
                .ToDictionary(col => col.Name.Substring(1), col => col.Ndx);

            // Resolve env column to use
            if (!envColumns.TryGetValue(columnName, out int useColNdx))
                throw new InvalidOperationException($"No environment column named '${columnName}' found in sheet {sheet.Name}");

            // Filter out any rows not matching the selected environment column
            sheet = sheet.FilterRows((List<string> cells, int rowNdx) => cells[useColNdx] != "");

            // Filter out all environment columns from sheet
            return sheet.FilterColumns((string colName, int colNdx) => envColumns.Values.Contains(colNdx));
        }

        public const string VariantColumnName = "/Variant";
        public const string AliasColumnName = "/Aliases";

        /// <summary>
        /// Split a single sheet with multiple languages to multiple sheets with only a single language in them.
        /// The output sheets are named according to the columns in the input sheet.
        /// </summary>
        /// <param name="sheet">Sheet to parse individual language sheets from</param>
        /// <param name="allowMissingTranslations">If false, any missing translations will throw a ParseError</param>
        /// <returns></returns>
        public static List<SpreadsheetContent> SplitLanguageSheets(SpreadsheetContent sheet, bool allowMissingTranslations = false)
        {
            List<SpreadsheetCell>              header         = sheet.Cells[0];
            IEnumerable<List<SpreadsheetCell>> content        = sheet.Cells.Skip(1);
            Dictionary<string, int>            languageColumn = new Dictionary<string, int>();

            if (header[0].Value != "TranslationId")
                throw new ParseError($"Expected first column name to be 'TranslationId', but it's '{header[0].Value}'");

            List<SpreadsheetContent> languageSheets = new List<SpreadsheetContent>();
            for (int colNdx = 1; colNdx < header.Count; colNdx++)
            {
                string languageCode = header[colNdx].Value;

                if (languageCode.StartsWith("//", StringComparison.Ordinal))
                    continue;

                if (languageColumn.TryGetValue(languageCode, out int preexistingColumn))
                    throw new ParseError(Invariant($"Language '{languageCode}' is defined multiple times. It is defined on columns {preexistingColumn+1} and {colNdx+1}."));
                languageColumn[languageCode] = colNdx;

                List<List<SpreadsheetCell>> newCells =
                    content
                        .Where(x => x.Count > 0)
                        .Where(x => !x[0].Value.StartsWith("//", StringComparison.Ordinal))
                        .Select(row =>
                        {
                            SpreadsheetCell translationId = row[0];
                            SpreadsheetCell valueCell     = (colNdx < row.Count) ? row[colNdx] : new SpreadsheetCell();

                            // Handle missing values
                            if (string.IsNullOrEmpty(valueCell.Value))
                            {
                                if (!allowMissingTranslations)
                                    throw new ParseError($"Missing translation for '{translationId}' in language '{languageCode}'");
                                else
                                    valueCell = new SpreadsheetCell($"#missing#{translationId}", valueCell.Row, valueCell.Column); // use prefix to distinguish between missing translations and untranslated text
                            }

                            return new List<SpreadsheetCell> { translationId, valueCell };
                        })
                        .ToList();

                languageSheets.Add(new SpreadsheetContent(languageCode, newCells, sheet.SourceInfo));
            }

            return languageSheets;
        }

        private static readonly Regex SpreadsheetUrlRegex = new Regex("\\/d\\/(?<id>[a-zA-Z0-9-_]*?)(\\/|$)", RegexOptions.Compiled);

        public static string ParseIdFromSpreadsheetUrlOrId(string SpreadsheetUrlOrId)
        {
            Match match = SpreadsheetUrlRegex.Match(SpreadsheetUrlOrId);
            if (match.Success)
                return match.Groups["id"].Value;
            else
                return SpreadsheetUrlOrId;
        }
    }
}
