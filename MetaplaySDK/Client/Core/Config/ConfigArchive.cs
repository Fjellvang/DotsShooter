// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

#if UNITY_WEBGL && !UNITY_EDITOR
#   define UNITY_WEBGL_BUILD
#endif

using Metaplay.Core.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Metaplay.Core.Config
{
    public class ConfigArchiveEntry
    {
        public string             Name  { get; }
        public ContentHash        Hash  { get; }
        public ReadOnlyMemory<byte> Bytes { get; }

        public ConfigArchiveEntry(string name, ContentHash hash, ReadOnlyMemory<byte> bytes)
        {
            Name  = name ?? throw new ArgumentNullException(nameof(name));
            Hash  = hash;
            Bytes = bytes;
        }

        public static ConfigArchiveEntry FromBlob(string name, byte[] bytes)
        {
            ContentHash hash = ContentHash.ComputeFromBytes(bytes);
            return new ConfigArchiveEntry(name, hash, bytes);
        }

        public override int GetHashCode() => Hash.GetHashCode();

        public override string ToString() => $"{Name}#{Hash}";
    }

    /// <summary>
    /// A config archive is a collection of <see cref="ConfigArchiveEntry"/> and provides a read-only view over the entries,
    /// these entries are nested <see cref="ConfigArchive"/>s, or MetaSerialized blobs of config libraries.
    /// Upon load, entries are decompressed if necessary.
    /// </summary>
    public class ConfigArchive
    {
        readonly Dictionary<string, ConfigArchiveEntry> _entries;
        public   ContentHash                             Version   { get; }
        public   MetaTime                                CreatedAt { get; }
        public IReadOnlyList<ConfigArchiveEntry> Entries { get; }

        /// <remarks>
        /// <paramref name="entries"/> will be taken as is and will not be sorted by this constructor.
        /// </remarks>
        ConfigArchive(ContentHash version, MetaTime createdAt, List<ConfigArchiveEntry> entries)
        {
            Version   = version;
            CreatedAt = createdAt;
            Entries   = entries;
            _entries  = Entries.ToDictionary(x=>x.Name);
        }

        /// <summary>
        /// Create a new <see cref="ConfigArchive"/> with the given content.
        /// <paramref name="unsortedEntries"/> will be sorted before assigning to the archive.
        /// <see cref="Version"/> will be computed according to the sorted entries.
        /// </summary>
        /// <remarks>
        /// This is meant to be used when building a new archive, in contrast with <see cref="CreateLoaded"/> which is meant to be used when loading a previously serialized archive.
        /// </remarks>
        public static ConfigArchive CreateFromBuild(MetaTime createdAt, IEnumerable<ConfigArchiveEntry> unsortedEntries)
        {
            List<ConfigArchiveEntry> sortedEntries = unsortedEntries.OrderBy(x => x.Name, StringComparer.Ordinal).ToList();

            return new ConfigArchive(
                ConfigArchiveBuildUtility.ComputeVersionHashForEntries(sortedEntries),
                createdAt,
                sortedEntries);
        }

        /// <summary>
        /// Create a <see cref="ConfigArchive"/> from data loaded from a previously serialized archive.
        /// <paramref name="entries"/> will taken in the order it is given - it will not be sorted by this method.
        /// <paramref name="version"/> will be assigned to <see cref="Version"/> - it will not be recomputed from <paramref name="entries"/> by this method.
        /// </summary>
        /// <remarks>
        /// This is meant to be used when loading a previously serialized archive, in contrast with <see cref="CreateFromBuild"/> which is meant for creating a new archive.
        /// </remarks>
        public static ConfigArchive CreateLoaded(ContentHash version, MetaTime createdAt, IEnumerable<ConfigArchiveEntry> entries)
        {
            return new ConfigArchive(
                version,
                createdAt,
                entries.ToList());
        }

        public IOReader ReadEntry(string name)
        {
            ReadOnlyMemory<byte> entry = GetEntryBytes(name);
            return new IOReader(entry);
        }

        public Stream ReadEntryStream(string name)
        {
            return ReadEntry(name).ConvertToStream();
        }

        public ReadOnlyMemory<byte> GetEntryBytes(string name)
        {
            return _entries[name].Bytes;
        }

        public bool ContainsEntryWithName(string name)
        {
            return _entries.ContainsKey(name);
        }

        public ConfigArchiveEntry GetEntryByName(string name)
        {
            if (!_entries.TryGetValue(name, out ConfigArchiveEntry entry))
                throw new KeyNotFoundException($"Entry '{name}' not found in ConfigArchive");
            return entry;
        }

        /// Creates a read-only ConfigArchive view over the serialized representation. The contents of <paramref name="bytes"/> are
        /// NOT COPIED and must not be modified as long as the loaded ConfigArchive is in use.
        public static ConfigArchive FromBytes(byte[] bytes)
        {
            return Load(new ReadOnlyMemory<byte>(bytes));
        }

        /// Creates a read-only ConfigArchive view over the serialized representation. The contents of <paramref name="bytes"/> are
        /// NOT COPIED and must not be modified as long as the loaded ConfigArchive is in use.
        public static ConfigArchive FromBytes(ReadOnlyMemory<byte> bytes)
        {
            return Load(bytes);
        }

        public static async Task<ConfigArchive> FromFileAsync(string fileName)
        {
            byte[] bytes = await FileUtil.ReadAllBytesAsync(fileName);
            return FromBytes(bytes);
        }

#if !UNITY_WEBGL_BUILD
        public static ConfigArchive FromFile(string fileName)
        {
            byte[] bytes = FileUtil.ReadAllBytes(fileName);
            return FromBytes(bytes);
        }
#endif

        static ConfigArchive Load(ReadOnlyMemory<byte> bytes)
        {
            using (IOReader reader = new IOReader(bytes))
            {
                (int schemaVersion, ContentHash archiveVersion, MetaTime timestamp, int numEntries) = ConfigArchiveBuildUtility.ReadArchiveHeader(reader);

                // Read entry headers
                List<(string entryName, ContentHash entryHash, CompressionAlgorithm compression, int length)> headers = new List<(string, ContentHash, CompressionAlgorithm, int)>(numEntries);
                for (int ndx = 0; ndx < numEntries; ndx++)
                    headers.Add(ConfigArchiveBuildUtility.ReadEntryHeader(reader, schemaVersion));

                if (schemaVersion < 4)
                    archiveVersion = ConfigArchiveBuildUtility.ComputeVersionHashForEntries(headers.Select(e => (e.entryName, e.entryHash)));

                // Read entry payloads
                List<ConfigArchiveEntry> entries = new List<ConfigArchiveEntry>();
                for (int ndx = 0; ndx < numEntries; ndx++)
                {
                    var entry = headers[ndx];

                    ReadOnlyMemory<byte> entryBuf = bytes.Slice(reader.Offset, entry.length);

                    reader.SkipBytes(entry.length);

                    if (entry.compression != CompressionAlgorithm.None)
                    {
                        if (entry.compression == CompressionAlgorithm.Deflate)
                            entryBuf = CompressUtil.DeflateDecompress(entryBuf);
                        else
                            throw new NotSupportedException($"Unsupported compression method {entry.compression}");
                    }

                    entries.Add(new ConfigArchiveEntry(entry.entryName, entry.entryHash, entryBuf));
                }

                return CreateLoaded(archiveVersion, timestamp, entries);
            }
        }
    }
}
