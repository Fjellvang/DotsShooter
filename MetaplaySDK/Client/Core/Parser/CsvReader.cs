// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Metaplay.Core
{
    /// <summary>
    /// A read-only stream of .csv file, pre-split into rows and cells.
    ///
    /// Support auto-detection of separator (checks whether comma or semi-colon is more common on first line).
    /// </summary>
    public class CsvStream : IDisposable, IEnumerable<string[]>
    {
        public readonly string  FilePath;

        private StreamReader    _reader;
        private char            _separator = '\0';

        public CsvStream(string filePath, Stream input, char separator = '\0')
        {
            FilePath = filePath;
            _reader = new StreamReader(input, Encoding.UTF8);
            _separator = separator;
        }

        public void Dispose()
        {
            _reader.Dispose();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<string[]> GetEnumerator()
        {
            while (!_reader.EndOfStream)
            {
                string line = _reader.ReadLine();

                // Skip comments (lines starting with '//')
                if (line.StartsWith("//", StringComparison.Ordinal))
                {
                    // Comments parse as empty lines (needed for row counters to stay in sync)
                    yield return new string[] { };
                }
                else
                {
                    // If no separator given, detect one (only from non-empty lines)
                    if (_separator == '\0')
                        _separator = DetectSeparator(line);

                    yield return SplitLine(line);
                }
            }
        }

        char DetectSeparator(string line)
        {
            // Count number of commas and semicolons on line
            int numCommas = 0;
            int numSemicolons = 0;
            foreach (char ch in line)
            {
                if (ch == ',') numCommas++;
                if (ch == ';') numSemicolons++;
            }

            // If no commas or semicolons, assume comma to be separator (single-column file)
            if (numCommas == 0 && numSemicolons == 0)
                return ',';

            if (numCommas == numSemicolons)
                throw new ParseError($"Failed to detect character separator in {FilePath} due to same number of commas and semicolons on first line");

            return (numCommas > numSemicolons) ? ',' : ';';
        }

        string ParseQuoted(string str)
        {
            if (str.Length >= 2 && str[0] == '"' && str[str.Length - 1] == '"')
            {
                char[] dst = new char[str.Length];
                int dstNdx = 0;
                for (int srcNdx = 1; srcNdx < str.Length - 1; srcNdx++)
                {
                    char c = str[srcNdx];
                    if (c == '"' && str[srcNdx + 1] == '"')
                    {
                        dst[dstNdx++] = '"';
                        srcNdx++; // skip double quote
                    }
                    else
                        dst[dstNdx++] = c;
                }
                return new string(dst, 0, dstNdx);
            }
            else // not quoted
                return str;
        }

        public string[] SplitLine(string line)
        {
            List<string> cells = new List<string>();

            int startNdx = 0;
            bool isQuoted = false;
            for (int ndx = 0; ndx < line.Length; ndx++)
            {
                char c = line[ndx];
                if (c == _separator && !isQuoted)
                {
                    cells.Add(ParseQuoted(line.Substring(startNdx, ndx - startNdx)));
                    startNdx = ndx + 1;
                }
                else if (c == '"')
                {
                    if (isQuoted)
                    {
                        if (line.Length > ndx + 1 && line[ndx + 1] == '"')
                            ndx++;
                        else
                            isQuoted = false;
                    }
                    else
                        isQuoted = true;
                }
            }

            // Final cell
            cells.Add(ParseQuoted(line.Substring(startNdx)));
            return cells.ToArray();
        }
    }
}
