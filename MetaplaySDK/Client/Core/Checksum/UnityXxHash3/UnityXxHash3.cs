// LICENSE:
//
// com.unity.collections copyright © 2024 Unity Technologies
//
// Licensed under the Unity Companion License for Unity-dependent projects (see https://unity3d.com/legal/licenses/unity_companion_license).
//
// Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED. Please review the license for details on these and other terms and conditions.

// Adapted from original com.unity.collections package by Metaplay.
// Modified to work with ARMv7 which does not reliably support unaligned memory accesses

#if UNITY_2018_1_OR_NEWER

using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Metaplay.ModifiedUnityCollections
{
    /// <summary>
    /// A feature complete hashing API based on xxHash3 (https://github.com/Cyan4973/xxHash)
    /// </summary>
    /// <remarks>
    /// Features:
    ///  - Compute 64bits or 128bits hash keys, based on a private key, with an optional given seed value.
    ///  - Hash on buffer (with or without a ulong based seed value)
    ///  - Hash on buffer while copying the data to a destination
    ///  - Use instances of <see cref="UnityAlignedXxHash3.StreamingState"/> to accumulate data to hash in multiple calls, suited for small data, then retrieve the hash key at the end.
    ///  - xxHash3 has several implementation based on the size to hash to ensure best performances
    ///  - We currently have two implementations:
    ///    - A generic one based on Unity.Mathematics, that should always be executed compiled with Burst.
    ///    - An AVX2 based implementation for platforms supporting it, using Burst intrinsics.
    ///  - Whether or not the call site is compiled with burst, the hashing function will be executed by Burst(*) to ensure optimal performance.
    ///    (*) Only when the hashing size justifies such transition.
    /// </remarks>
    [BurstCompile]
    //[GenerateTestsForBurstCompatibility]
    public static partial class UnityAlignedXxHash3
    {
        #region Public API

        /// <summary>
        /// Compute a 64bits hash of a memory region
        /// </summary>
        /// <param name="input">The memory buffer, can't be null</param>
        /// <param name="length">The length of the memory buffer, can be zero</param>
        /// <returns>The hash result</returns>
        public static unsafe ulong Hash64(void* input, long length)
        {
            fixed (void* secret = xxHashDefaultKey.kSecret)
            {
                return Hash64Internal((byte*)input, length, (byte*) secret, 0);
            }
        }

        /// <summary>
        /// Compute a 64bits hash of a memory region using a given seed value
        /// </summary>
        /// <param name="input">The memory buffer, can't be null</param>
        /// <param name="length">The length of the memory buffer, can be zero</param>
        /// <param name="seed">The seed value to alter the hash computation from</param>
        /// <returns>The hash result</returns>
        public static unsafe ulong Hash64(void* input, long length, ulong seed)
        {
            fixed (byte* secret = xxHashDefaultKey.kSecret)
            {
                return Hash64Internal((byte*)input, length, secret, seed);
            }
        }

        #endregion

        #region Constants

        private const int STRIPE_LEN = 64;
        private const int ACC_NB = STRIPE_LEN / 8; // Accumulators are ulong sized
        private const int SECRET_CONSUME_RATE = 8;
        private const int SECRET_KEY_SIZE = 192;
        private const int SECRET_KEY_MIN_SIZE = 136;
        private const int SECRET_LASTACC_START = 7;
        private const int NB_ROUNDS = (SECRET_KEY_SIZE - STRIPE_LEN) / SECRET_CONSUME_RATE;
        private const int BLOCK_LEN = STRIPE_LEN * NB_ROUNDS;

        private const uint PRIME32_1 = 0x9E3779B1U;
        private const uint PRIME32_2 = 0x85EBCA77U;

        private const uint PRIME32_3 = 0xC2B2AE3DU;

        // static readonly uint PRIME32_4 = 0x27D4EB2FU;
        private const uint PRIME32_5 = 0x165667B1U;
        private const ulong PRIME64_1 = 0x9E3779B185EBCA87UL;
        private const ulong PRIME64_2 = 0xC2B2AE3D27D4EB4FUL;
        private const ulong PRIME64_3 = 0x165667B19E3779F9UL;
        private const ulong PRIME64_4 = 0x85EBCA77C2B2AE63UL;
        private const ulong PRIME64_5 = 0x27D4EB2F165667C5UL;

        private const int MIDSIZE_MAX = 240;
        private const int MIDSIZE_STARTOFFSET = 3;
        private const int MIDSIZE_LASTOFFSET = 17;

        private const int SECRET_MERGEACCS_START = 11;

        #endregion

        internal static unsafe ulong Hash64Internal(byte* input, long length, byte* secret, ulong seed)
        {
            if (length <= 16)
            {
                return Hash64Len0To16(input, length, secret, seed);
            }

            if (length <= 128)
            {
                return Hash64Len17To128(input, length, secret, seed);
            }

            if (length <= MIDSIZE_MAX)
            {
                return Hash64Len129To240(input, length, secret, seed);
            }

            if (seed != 0)
            {
                byte* newSecret = (byte*)UnsafeUtility.Malloc(SECRET_KEY_SIZE, 64, Allocator.Temp);

                EncodeSecretKey(newSecret, secret, seed);
                ulong result = Hash64Long(input, length, newSecret);

                UnsafeUtility.Free(newSecret, Allocator.Temp);

                return result;
            }
            else
            {
                return Hash64Long(input, length, secret);
            }
        }

        #region 64-bits hash, size dependent implementations

        private static unsafe ulong Hash64Len1To3(byte* input, long len, byte* secret, ulong seed)
        {
            unchecked
            {
                byte c1 = input[0];
                byte c2 = input[len >> 1];
                byte c3 = input[len - 1];
                uint combined = ((uint)c1 << 16) | ((uint)c2  << 24) | ((uint)c3 <<  0) | ((uint)len << 8);
                ulong bitflip = (Read32LE(secret) ^ Read32LE(secret+4)) + seed;
                ulong keyed = (ulong)combined ^ bitflip;
                return AvalancheH64(keyed);
            }
        }

        private static unsafe ulong Hash64Len4To8(byte* input, long length, byte* secret, ulong seed)
        {
            unchecked
            {
                seed ^= (ulong)Swap32((uint)seed) << 32;
                uint input1 = Read32LE(input);
                uint input2 = Read32LE(input + length - 4);
                ulong bitflip = (Read64LE(secret+8) ^ Read64LE(secret+16)) - seed;
                ulong input64 = input2 + (((ulong)input1) << 32);
                ulong keyed = input64 ^ bitflip;
                return rrmxmx(keyed, (ulong)length);
            }
        }

        private static unsafe ulong Hash64Len9To16(byte* input, long length, byte* secret, ulong seed)
        {
            unchecked
            {
                ulong bitflip1 = (Read64LE(secret+24) ^ Read64LE(secret+32)) + seed;
                ulong bitflip2 = (Read64LE(secret+40) ^ Read64LE(secret+48)) - seed;
                ulong input_lo = Read64LE(input) ^ bitflip1;
                ulong input_hi = Read64LE(input + length - 8) ^ bitflip2;
                ulong acc = (ulong)length + Swap64(input_lo) + input_hi + Mul128Fold64(input_lo, input_hi);
                return Avalanche(acc);
            }
        }

        private static unsafe ulong Hash64Len0To16(byte* input, long length, byte* secret, ulong seed)
        {
            if (length > 8)
            {
                return Hash64Len9To16(input, length, secret, seed);
            }

            if (length >= 4)
            {
                return Hash64Len4To8(input, length, secret, seed);
            }

            if (length > 0)
            {
                return Hash64Len1To3(input, length, secret, seed);
            }

            return AvalancheH64(seed ^ (Read64LE(secret+56) ^ Read64LE(secret+64)));
        }

        private static unsafe ulong Hash64Len17To128(byte* input, long length, byte* secret, ulong seed)
        {
            unchecked
            {
                ulong acc = (ulong) length * PRIME64_1;
                if (length > 32)
                {
                    if (length > 64)
                    {
                        if (length > 96)
                        {
                            acc += Mix16(input + 48, secret + 96, seed);
                            acc += Mix16(input + length - 64, secret + 112, seed);
                        }

                        acc += Mix16(input + 32, secret + 64, seed);
                        acc += Mix16(input + length - 48, secret + 80, seed);
                    }

                    acc += Mix16(input + 16, secret + 32, seed);
                    acc += Mix16(input + length - 32, secret + 48, seed);
                }

                acc += Mix16(input + 0, secret + 0, seed);
                acc += Mix16(input + length - 16, secret + 16, seed);

                return Avalanche(acc);
            }
        }

        private static unsafe ulong Hash64Len129To240(byte* input, long length, byte* secret, ulong seed)
        {
            unchecked
            {
                ulong acc = (ulong) length * PRIME64_1;
                int nbRounds = (int) length / 16;
                for (int i = 0; i < 8; i++)
                {
                    acc += Mix16(input + (16 * i), secret + (16 * i), seed);
                }

                acc = Avalanche(acc);

                for (int i = 8; i < nbRounds; i++)
                {
                    acc += Mix16(input + (16 * i), secret + (16 * (i - 8)) + MIDSIZE_STARTOFFSET, seed);
                }

                acc += Mix16(input + length - 16, secret + SECRET_KEY_MIN_SIZE - MIDSIZE_LASTOFFSET, seed);
                return Avalanche(acc);
            }
        }

        [BurstCompile]
        private static unsafe ulong Hash64Long(byte* input, long length, byte* secret)
        {
            byte* addr = stackalloc byte[STRIPE_LEN + 31];
            ulong* acc = (ulong*) ((ulong) addr + 31 & 0xFFFFFFFFFFFFFFE0); // Aligned the allocated address on 32 bytes
            acc[0] = PRIME32_3;
            acc[1] = PRIME64_1;
            acc[2] = PRIME64_2;
            acc[3] = PRIME64_3;
            acc[4] = PRIME64_4;
            acc[5] = PRIME32_2;
            acc[6] = PRIME64_5;
            acc[7] = PRIME32_1;

            unchecked
            {
                /*if (X86.Avx2.IsAvx2Supported)
                {
                    Avx2HashLongInternalLoop(acc, input, length, secret, 1);
                }
                else*/
                {
                    DefaultHashLongInternalLoop(acc, input, length, secret);
                }
                return MergeAcc(acc, secret + SECRET_MERGEACCS_START, (ulong) length * PRIME64_1);
            }
        }

        #endregion

        #region Internal helpers

        internal static unsafe void EncodeSecretKey(byte* dst, byte* secret, ulong seed)
        {
            unchecked
            {
                int seedInitCount = SECRET_KEY_SIZE / (8 * 2);
                for (int i = 0; i < seedInitCount; i++)
                {
                    Write64LE(dst + 16 * i + 0, Read64LE(secret + 16 * i + 0) + seed);
                    Write64LE(dst + 16 * i + 8, Read64LE(secret + 16 * i + 8) - seed);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe ulong Read64LEAligned(void* addr) => *(ulong*) addr;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe ulong Read64LE(void* addr) //=> *(ulong*) addr;
        {
            ulong value;
            UnsafeUtility.MemCpy(&value, addr, sizeof(ulong));
            return value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe uint Read32LE(void* addr) //=> *(uint*) addr;
        {
            uint value;
            UnsafeUtility.MemCpy(&value, addr, sizeof(uint));
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void Write64LE(void* addr, ulong value) => *(ulong*) addr = value;
        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // private static unsafe void Read32LE(void* addr, uint value) => *(uint*) addr = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Mul32To64(uint x, uint y) => (ulong) x * y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Swap64(ulong x)
        {
            return ((x << 56) & 0xff00000000000000UL) |
                   ((x << 40) & 0x00ff000000000000UL) |
                   ((x << 24) & 0x0000ff0000000000UL) |
                   ((x <<  8) & 0x000000ff00000000UL) |
                   ((x >>  8) & 0x00000000ff000000UL) |
                   ((x >> 24) & 0x0000000000ff0000UL) |
                   ((x >> 40) & 0x000000000000ff00UL) |
                   ((x >> 56) & 0x00000000000000ffUL);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Swap32(uint x)
        {
            return ((x << 24) & 0xff000000) |
                   ((x <<  8) & 0x00ff0000) |
                   ((x >>  8) & 0x0000ff00) |
                   ((x >> 24) & 0x000000ff);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint RotL32(uint x, int r) => (((x) << (r)) | ((x) >> (32 - (r))));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong RotL64(ulong x, int r) => (((x) << (r)) | ((x) >> (64 - (r))));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong XorShift64(ulong v64, int shift)
        {
            return v64 ^ (v64 >> shift);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Mul128Fold64(ulong lhs, ulong rhs)
        {
            var lo = Common.umul128(lhs, rhs, out var hi);
            return lo ^ hi;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe ulong Mix16(byte* input, byte* secret, ulong seed)
        {
            ulong input_lo = Read64LE(input);
            ulong input_hi = Read64LE(input + 8);
            return Mul128Fold64(
                input_lo ^ (Read64LE(secret + 0) + seed),
                input_hi ^ (Read64LE(secret + 8) - seed));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Avalanche(ulong h64)
        {
            unchecked
            {
                h64 = XorShift64(h64, 37);
                h64 *= 0x165667919E3779F9UL;
                h64 = XorShift64(h64, 32);
                return h64;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong AvalancheH64(ulong h64)
        {
            unchecked
            {
                h64 ^= h64 >> 33;
                h64 *= PRIME64_2;
                h64 ^= h64 >> 29;
                h64 *= PRIME64_3;
                h64 ^= h64 >> 32;
                return h64;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong rrmxmx(ulong h64, ulong length)
        {
            h64 ^= RotL64(h64, 49) ^ RotL64(h64, 24);
            h64 *= 0x9FB21C651E98DF25UL;
            h64 ^= (h64 >> 35) + length ;
            h64 *= 0x9FB21C651E98DF25UL;
            return XorShift64(h64, 28);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe ulong Mix2Acc(ulong acc0, ulong acc1, byte* secret)
        {
            return Mul128Fold64(acc0 ^ Read64LE(secret), acc1 ^ Read64LE(secret+8));
        }

        internal static unsafe ulong MergeAcc(ulong* acc, byte* secret, ulong start)
        {
            unchecked
            {
                ulong result64 = start;

                result64 += Mix2Acc(acc[0], acc[1], secret + 0);
                result64 += Mix2Acc(acc[2], acc[3], secret + 16);
                result64 += Mix2Acc(acc[4], acc[5], secret + 32);
                result64 += Mix2Acc(acc[6], acc[7], secret + 48);

                return Avalanche(result64);
            }
        }

        #endregion

        #region Default Implementation

        private static unsafe void DefaultHashLongInternalLoop(ulong* acc, byte* input, long length, byte* secret)
        {
            // Process blocks of 1024 bytes
            long nb_blocks = (length-1) / BLOCK_LEN;
            for (int n = 0; n < nb_blocks; n++)
            {
                DefaultAccumulate(acc, input + n * BLOCK_LEN, secret, NB_ROUNDS);
                DefaultScrambleAcc(acc, secret + SECRET_KEY_SIZE - STRIPE_LEN);
            }

            // Process full stripes
            long nbStripes = ((length-1) - (BLOCK_LEN * nb_blocks)) / STRIPE_LEN;
            DefaultAccumulate(acc, input + nb_blocks * BLOCK_LEN, secret, nbStripes);

            // Process final stripe
            byte* p = input + length - STRIPE_LEN;
            DefaultAccumulate512(acc, p, secret + SECRET_KEY_SIZE - STRIPE_LEN - SECRET_LASTACC_START);
        }

        internal static unsafe void DefaultAccumulate(ulong* acc, byte* input, byte* secret, long nbStripes)
        {
            // For 8-byte aligned inputs, use the fast-path
            bool isAligned = ((ulong)input & 7) == 0 && ((ulong)secret & 7) == 0;
            if (isAligned)
            {
                for (int n = 0; n < nbStripes; n++)
                    DefaultAccumulate512Aligned(acc, input + n * STRIPE_LEN, secret + n * SECRET_CONSUME_RATE);
            }
            else
            {
                for (int n = 0; n < nbStripes; n++)
                    DefaultAccumulate512(acc, input + n * STRIPE_LEN, secret + n * SECRET_CONSUME_RATE);
            }
        }

        internal static unsafe void DefaultAccumulate512Aligned(ulong* acc, byte* input, byte* secret)
        {
            int count = ACC_NB;
            for (int i = 0; i < count; i++)
            {
                ulong data_val = Read64LEAligned(input + 8 * i);
                ulong data_key = data_val ^ Read64LEAligned(secret + i * 8);

                acc[i ^ 1] += data_val;
                acc[i] += Mul32To64((uint)(data_key & 0xFFFFFFFF), (uint)(data_key >> 32));
            }
        }

        internal static unsafe void DefaultAccumulate512(ulong* acc, byte* input, byte* secret)
        {
            int count = ACC_NB;
            for (int i = 0; i < count; i++)
            {
                ulong data_val = Read64LE(input + 8 * i);
                ulong data_key = data_val ^ Read64LE(secret + i * 8);

                acc[i ^ 1] += data_val;
                acc[i] += Mul32To64((uint) (data_key & 0xFFFFFFFF), (uint) (data_key >> 32));
            }
        }

        internal static unsafe void DefaultScrambleAcc(ulong* acc, byte* secret)
        {
            for (int i = 0; i < ACC_NB; i++)
            {
                ulong key64 = Read64LE(secret + 8 * i);
                ulong acc64 = acc[i];
                acc64 = XorShift64(acc64, 47);
                acc64 ^= key64;
                acc64 *= PRIME32_1;
                acc[i] = acc64;
            }
        }

        #endregion
    }

    static class xxHashDefaultKey
    {
        // The default xxHash3 encoding key, other implementations of this algorithm should use the same key to produce identical hashes
        public static readonly byte[] kSecret =
        {
            0xb8, 0xfe, 0x6c, 0x39, 0x23, 0xa4, 0x4b, 0xbe, 0x7c, 0x01, 0x81, 0x2c, 0xf7, 0x21, 0xad, 0x1c,
            0xde, 0xd4, 0x6d, 0xe9, 0x83, 0x90, 0x97, 0xdb, 0x72, 0x40, 0xa4, 0xa4, 0xb7, 0xb3, 0x67, 0x1f,
            0xcb, 0x79, 0xe6, 0x4e, 0xcc, 0xc0, 0xe5, 0x78, 0x82, 0x5a, 0xd0, 0x7d, 0xcc, 0xff, 0x72, 0x21,
            0xb8, 0x08, 0x46, 0x74, 0xf7, 0x43, 0x24, 0x8e, 0xe0, 0x35, 0x90, 0xe6, 0x81, 0x3a, 0x26, 0x4c,
            0x3c, 0x28, 0x52, 0xbb, 0x91, 0xc3, 0x00, 0xcb, 0x88, 0xd0, 0x65, 0x8b, 0x1b, 0x53, 0x2e, 0xa3,
            0x71, 0x64, 0x48, 0x97, 0xa2, 0x0d, 0xf9, 0x4e, 0x38, 0x19, 0xef, 0x46, 0xa9, 0xde, 0xac, 0xd8,
            0xa8, 0xfa, 0x76, 0x3f, 0xe3, 0x9c, 0x34, 0x3f, 0xf9, 0xdc, 0xbb, 0xc7, 0xc7, 0x0b, 0x4f, 0x1d,
            0x8a, 0x51, 0xe0, 0x4b, 0xcd, 0xb4, 0x59, 0x31, 0xc8, 0x9f, 0x7e, 0xc9, 0xd9, 0x78, 0x73, 0x64,

            0xea, 0xc5, 0xac, 0x83, 0x34, 0xd3, 0xeb, 0xc3, 0xc5, 0x81, 0xa0, 0xff, 0xfa, 0x13, 0x63, 0xeb,
            0x17, 0x0d, 0xdd, 0x51, 0xb7, 0xf0, 0xda, 0x49, 0xd3, 0x16, 0x55, 0x26, 0x29, 0xd4, 0x68, 0x9e,
            0x2b, 0x16, 0xbe, 0x58, 0x7d, 0x47, 0xa1, 0xfc, 0x8f, 0xf8, 0xb8, 0xd1, 0x7a, 0xd0, 0x31, 0xce,
            0x45, 0xcb, 0x3a, 0x8f, 0x95, 0x16, 0x04, 0x28, 0xaf, 0xd7, 0xfb, 0xca, 0xbb, 0x4b, 0x40, 0x7e,
        };
    }
}

#endif
