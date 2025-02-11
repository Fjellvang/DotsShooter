// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Utility;
using Metaplay.Core;
using Metaplay.Core.IO;
using Metaplay.Core.League;
using Metaplay.Core.Memory;
using Metaplay.Core.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Metaplay.Server.League
{
    public sealed class DivisionParticipantCount : IEquatable<DivisionParticipantCount>
    {
        public int Division { get; set; }
        public int Count    { get; set; }

        public DivisionParticipantCount(int division, int count)
        {
            Division = division;
            Count    = count;
        }

        public bool Equals(DivisionParticipantCount other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return Division == other.Division && Count == other.Count;
        }

        public override bool Equals(object obj)
        {
            return obj is DivisionParticipantCount other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Division, Count);
        }
    }

    public class LeagueDivisionParticipantCountState
    {
        public class RankState
        {
            /// <summary>
            /// All divisions and their participant counts for this rank.
            /// The list is sorted by division index and has no gaps, so it can be indexed by division index.
            /// </summary>
            public List<DivisionParticipantCount> DivisionParticipantCounts = new List<DivisionParticipantCount>();
            public List<int> NonFullDivisions = new List<int>();
            public int       MaxParticipantCount;

            public RankState(int maxParticipantCount)
            {
                MaxParticipantCount = maxParticipantCount;
            }
        }

        public readonly List<RankState> Ranks;
        readonly        RandomPCG       _random = RandomPCG.CreateNew();

        public LeagueDivisionParticipantCountState(int[] rankMaxParticipantCounts)
        {
            if (rankMaxParticipantCounts == null || rankMaxParticipantCounts.Length == 0)
                throw new ArgumentException("Rank max participant counts must be non-empty.", nameof(rankMaxParticipantCounts));
            if (rankMaxParticipantCounts.Any(count => count <= 0))
                throw new ArgumentException("Rank max participant counts must be positive.", nameof(rankMaxParticipantCounts));

            int numRanks = rankMaxParticipantCounts.Length;
            Ranks   = new List<RankState>(numRanks);
            for (int i = 0; i < numRanks; ++i)
                Ranks.Add(new RankState(rankMaxParticipantCounts[i]));
        }

        public LeagueDivisionParticipantCountState(int[] rankMaxParticipantCounts, IEnumerable<DivisionIndex> participantAssignments)
            : this(rankMaxParticipantCounts)
        {
            // Count the number of participants in each division.
            Dictionary<DivisionIndex, int> participantCounts = new Dictionary<DivisionIndex, int>();
            foreach (DivisionIndex division in participantAssignments)
            {
                if (participantCounts.TryGetValue(division, out int count))
                    participantCounts[division] = count + 1;
                else
                    participantCounts[division] = 1;
            }

            // Add participant counts to the rank states.
            foreach ((DivisionIndex idx, int numParticipants) in participantCounts)
            {
                RankState rankState = Ranks[idx.Rank];
                rankState.DivisionParticipantCounts.Add(new DivisionParticipantCount(idx.Division, numParticipants));

                if (numParticipants < rankState.MaxParticipantCount && numParticipants > 0)
                    rankState.NonFullDivisions.Add(idx.Division);
            }

            // Sort division participant counts by division index.
            foreach (RankState state in Ranks)
                state.DivisionParticipantCounts.Sort((a, b) => a.Division.CompareTo(b.Division));

            // Fill in any gaps in the division participant counts. Should not be any, but just in case.
            foreach (RankState state in Ranks)
            {
                if (state.DivisionParticipantCounts.Count == 0)
                    continue;
                if (state.DivisionParticipantCounts[^1].Division == state.DivisionParticipantCounts.Count - 1)
                    continue;

                int division = 0;
                for (int i = 0; i < state.DivisionParticipantCounts.Count; ++i)
                {
                    DivisionParticipantCount count = state.DivisionParticipantCounts[i];
                    while (count.Division > division)
                    {
                        state.DivisionParticipantCounts.Insert(i, new DivisionParticipantCount(division, 0));
                        ++division;
                    }

                    ++division;
                }
            }
        }

        public LeagueDivisionParticipantCountState(int[] rankMaxParticipantCounts, List<PersistedDivisionCounts> persistedDivisionCounts)
        {
            if (rankMaxParticipantCounts == null || rankMaxParticipantCounts.Length == 0)
                throw new ArgumentException("Rank max participant counts must be non-empty.", nameof(rankMaxParticipantCounts));
            if (rankMaxParticipantCounts.Any(count => count <= 0))
                throw new ArgumentException("Rank max participant counts must be positive.", nameof(rankMaxParticipantCounts));

            int numRanks = Math.Min(rankMaxParticipantCounts.Length, persistedDivisionCounts.Count);
            Ranks = new List<RankState>(numRanks);
            for (int i = 0; i < numRanks; ++i)
            {
                Ranks.Add(new RankState(rankMaxParticipantCounts[i]));
                Ranks[i].DivisionParticipantCounts = persistedDivisionCounts[i].ToDivisionParticipantCounts();

                for (int j = 0; j < Ranks[i].DivisionParticipantCounts.Count; j++)
                {
                    int count = Ranks[i].DivisionParticipantCounts[j].Count;
                    if (count < Ranks[i].MaxParticipantCount && count > 0)
                        Ranks[i].NonFullDivisions.Add(j);
                }
            }
        }

        public void AddParticipant(int rank, int division)
        {
            if (rank < 0 || rank >= Ranks.Count)
                throw new ArgumentOutOfRangeException(nameof(rank), rank,
                    FormattableString.Invariant($"Rank should be between 0 and {Ranks.Count}"));

            RankState rankState = Ranks[rank];
            while (rankState.DivisionParticipantCounts.Count <= division)
            {
                rankState.DivisionParticipantCounts.Add(new DivisionParticipantCount(rankState.DivisionParticipantCounts.Count, 0));
            }
            DivisionParticipantCount divisionParticipantCount = rankState.DivisionParticipantCounts[division];

            // If this is the first participant in the division, add it to the non-full divisions list.
            if (divisionParticipantCount.Count == 0)
                rankState.NonFullDivisions.Add(division);

            divisionParticipantCount.Count++;

            if (divisionParticipantCount.Count >= rankState.MaxParticipantCount)
                rankState.NonFullDivisions.Remove(division);
        }

        public void SetParticipantCount(int rank, int division, int participants)
        {
            if (rank < 0 || rank >= Ranks.Count)
                throw new ArgumentOutOfRangeException(nameof(rank), rank,
                    FormattableString.Invariant($"Rank should be between 0 and {Ranks.Count}"));

            if (participants < 0)
                throw new ArgumentException("Participant count can't be less than 0.", nameof(participants));

            RankState rankState = Ranks[rank];
            while (rankState.DivisionParticipantCounts.Count <= division)
            {
                rankState.DivisionParticipantCounts.Add(new DivisionParticipantCount(rankState.DivisionParticipantCounts.Count, 0));
            }

            DivisionParticipantCount divisionParticipantCount = rankState.DivisionParticipantCounts[division];

            int oldCount = divisionParticipantCount.Count;
            divisionParticipantCount.Count = participants;


            // Add to non-full divisions if the division becomes non-full when removing participants.
            if ((oldCount > participants && participants > 0 && participants < rankState.MaxParticipantCount) ||
                // Add to non-full divisions if the division is set to not full and having participants.
                (oldCount == 0 && participants > 0 && participants < rankState.MaxParticipantCount))
                rankState.NonFullDivisions.Add(division);
            // Remove from non-full divisions if the division becomes full.
            if ((oldCount < participants && participants >= rankState.MaxParticipantCount) ||
                // Remove from non-full divisions if we set the count to 0.
                oldCount > 0 && participants == 0)
                rankState.NonFullDivisions.Remove(division);

        }

        public void RemoveParticipant(int rank, int division)
        {
            if (rank < 0 || rank >= Ranks.Count)
                throw new ArgumentOutOfRangeException(nameof(rank), rank,
                    FormattableString.Invariant($"Rank should be between 0 and {Ranks.Count}"));

            RankState rankState = Ranks[rank];

            if (division < 0 || division >= rankState.DivisionParticipantCounts.Count)
                throw new ArgumentOutOfRangeException(nameof(division), division,
                    FormattableString.Invariant($"Division should be between 0 and {rankState.DivisionParticipantCounts.Count}"));

            DivisionParticipantCount divisionParticipantCount = rankState.DivisionParticipantCounts[division];

            if (divisionParticipantCount.Count == 0)
                throw new InvalidOperationException("Division participant count is already zero.");

            // Add to non-full divisions if the division becomes non-full.
            if (divisionParticipantCount.Count == rankState.MaxParticipantCount)
                rankState.NonFullDivisions.Add(division);
            // Remove from non-full divisions if we remove the last participant.
            else if (divisionParticipantCount.Count == 1)
                rankState.NonFullDivisions.Remove(division);

            divisionParticipantCount.Count--;
        }

        /// <summary>
        /// Gets a random division that is not full for the given rank.
        /// If no divisions are available, returns false.
        /// </summary>
        public bool TryGetNonFullDivisionForRank(int rank, out int division)
        {
            RankState rankState = Ranks[rank];
            if (rankState.NonFullDivisions.Count == 0)
            {
                division = -1;
                return false;
            }

            // Choose two random divisions and pick the one with the fewest participants.
            DivisionParticipantCount division0 = rankState.DivisionParticipantCounts[_random.Choice(rankState.NonFullDivisions)];
            DivisionParticipantCount division1 = rankState.DivisionParticipantCounts[_random.Choice(rankState.NonFullDivisions)];

            division = division0.Count < division1.Count ? division0.Division : division1.Division;

            return true;
        }

        /// <summary>
        /// Calculates the total number of participants in the given rank.
        /// </summary>
        public int CalculateRankParticipantCount(int rank)
        {
            return Ranks[rank].DivisionParticipantCounts.Sum(participantCount => participantCount.Count);
        }

        public List<PersistedDivisionCounts> ToPersisted()
        {
            List<PersistedDivisionCounts> persistedDivisionCounts = new List<PersistedDivisionCounts>(Ranks.Count);
            foreach (RankState rankState in Ranks)
                persistedDivisionCounts.Add(PersistedDivisionCounts.FromDivisionParticipantCounts(rankState.DivisionParticipantCounts));
            return persistedDivisionCounts;
        }

        [Conditional("DEBUG")]
        public void Validate()
        {
            foreach (RankState rank in Ranks)
            {
                int division = 0;
                foreach (DivisionParticipantCount count in rank.DivisionParticipantCounts)
                {
                    MetaDebug.Assert(count.Division == division, "Division participant counts must be contiguous.");
                    MetaDebug.Assert(count.Count >= 0, "Division participant count must be non-negative.");
                    MetaDebug.Assert(!(count.Count == 0 && rank.NonFullDivisions.Contains(division)), "Division participant count is zero but division is in non-full divisions.");
                    ++division;
                }

                MetaDebug.Assert(rank.NonFullDivisions.Distinct().Count() == rank.NonFullDivisions.Count, "Non-full divisions must be unique.");
            }
        }
    }

    /// <summary>
    /// A persisted container for division participant counts.
    /// The counts are stored as a byte array, with each byte representing the number of participants in a division.
    /// The byte array is compressed with deflate if it is longer than 32 bytes, otherwise it is stored as is.
    /// The serializer has an array size limit of 16k, so the compressed division counts can be at most 16k bytes.
    /// </summary>
    [MetaSerializable]
    public class PersistedDivisionCounts
    {
        [MetaMember(1)] public byte[] CompressedDivisionCounts { get; private set; }

        PersistedDivisionCounts() { }

        PersistedDivisionCounts(byte[] compressedDivisionCounts)
        {
            CompressedDivisionCounts = compressedDivisionCounts;
        }

        public static PersistedDivisionCounts FromDivisionParticipantCounts(IEnumerable<DivisionParticipantCount> originalCounts)
        {
            ValidateInputContiguous(originalCounts);

            int counts = originalCounts.Count();

            using FlatIOBuffer buf = new FlatIOBuffer(initialCapacity: counts * 2 + 4);
            using (IOWriter     w   = new IOWriter(buf))
            {
                w.WriteVarInt(counts);

                foreach (DivisionParticipantCount count in originalCounts)
                    w.WriteVarInt(count.Count);
            }

            using FlatIOBuffer compressedBuffer = BlobCompress.CompressBlob(buf, CompressionAlgorithm.LZ4);
            byte[]             compressedCounts = compressedBuffer.ToArray();

            return new PersistedDivisionCounts(compressedCounts);
        }

        [Conditional("DEBUG")]
        static void ValidateInputContiguous(IEnumerable<DivisionParticipantCount> originalCounts)
        {
            // Validate that the input divisions are in order and have no gaps.
            int expectedDivision = 0;
            foreach (DivisionParticipantCount count in originalCounts)
            {
                if (count.Division != expectedDivision)
                    throw new InvalidOperationException("Division participant counts must be contiguous.");

                ++expectedDivision;
            }
        }

        public List<DivisionParticipantCount> ToDivisionParticipantCounts()
        {
            using FlatIOBuffer decompressed = BlobCompress.DecompressBlob(CompressedDivisionCounts);
            using IOReader     r            = new IOReader(decompressed);

            int len = r.ReadVarInt();

            List<DivisionParticipantCount> counts = new List<DivisionParticipantCount>(len);

            for (int i = 0; i < len; ++i)
                counts.Add(new DivisionParticipantCount(i, r.ReadVarInt()));

            return counts;
        }
    }
}
