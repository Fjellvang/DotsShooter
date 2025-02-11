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

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Metaplay.ModifiedUnityCollections
{
    //[GenerateTestsForBurstCompatibility]
    public static partial class UnityAlignedXxHash3
    {
        /// <summary>
        /// Type used to compute hash based on multiple data feed
        /// </summary>
        /// <remarks>
        /// Allow to feed the internal hashing accumulators with data through multiple calls to <see cref="Update"/>, then retrieving the final hash value using <see cref="DigestHash64"/> or <see cref="DigestHash128"/>.
        /// More info about how to use this class in its constructor.
        /// </remarks>
        //[GenerateTestsForBurstCompatibility]
        public struct StreamingState
        {
            #region Public API

            /// <summary>
            /// Create a StreamingState object, ready to be used with the streaming API
            /// </summary>
            /// <param name="seed">A seed value to be used to compute the hash, default is 0</param>
            /// <remarks>
            /// Once the object is constructed, you can call the <see cref="Update"/> method as many times as you want to accumulate data to hash.
            /// When all the data has been sent, call <see cref="DigestHash64"/> or <see cref="DigestHash128"/> to retrieve the corresponding key, the <see cref="StreamingState"/>
            /// instance will then be reset, using the same hash key size and same Seed in order to be ready to be used again.
            /// </remarks>
            public StreamingState(ulong seed=0)
            {
                State = default;
                Reset(seed);
            }

            /// <summary>
            /// Reset the state of the streaming instance using the given seed value.
            /// </summary>
            /// <param name="seed">The seed value to alter the computed hash value from</param>
            /// <remarks> Call this method to start a new streaming session based on this instance</remarks>
            public unsafe void Reset(ulong seed=0UL)
            {
                // Reset the whole buffer to 0
                var size = UnsafeUtility.SizeOf<StreamingStateData>();
                UnsafeUtility.MemClear(UnsafeUtility.AddressOf(ref State), size);

                // Set back the saved states
                State.IsHash64 = 1; // only support 64-bit hash

                // Init the accumulator with the prime numbers
                var acc = Acc;
                acc[0] = PRIME32_3;
                acc[1] = PRIME64_1;
                acc[2] = PRIME64_2;
                acc[3] = PRIME64_3;
                acc[4] = PRIME64_4;
                acc[5] = PRIME32_2;
                acc[6] = PRIME64_5;
                acc[7] = PRIME32_1;

                State.Seed = seed;

                fixed (byte* secret = xxHashDefaultKey.kSecret)
                {
                    if (seed != 0)
                    {
                        // Must encode the secret key if we're using a seed, we store it in the state object
                        EncodeSecretKey(SecretKey, secret, seed);
                    }
                    else
                    {
                        // Otherwise just copy it
                        UnsafeUtility.MemCpy(SecretKey, secret, SECRET_KEY_SIZE);
                    }
                }
            }

            /// <summary>
            /// Add some data to be hashed
            /// </summary>
            /// <param name="input">The memory buffer, can't be null</param>
            /// <param name="length">The length of the data to accumulate, can be zero</param>
            /// <remarks>This API allows you to feed very small data to be hashed, avoiding you to accumulate them in a big buffer, then computing the hash value from.</remarks>
            public unsafe void Update(void* input, int length)
            {
                byte* bInput = (byte*)input;
                byte* bEnd = bInput + length;
                byte* secret = SecretKey;
                State.TotalLength += length;

                // If inputs fits into buffer fully, just copy it there
                if (State.BufferedSize + length <= INTERNAL_BUFFER_SIZE)
                {
                    UnsafeUtility.MemCpy(Buffer + State.BufferedSize, bInput, length);
                    State.BufferedSize += length;
                    return;
                }

                // If any bytes in buffer, fill it up (if enough input) and process
                if (State.BufferedSize != 0)
                {
                    int loadSize = INTERNAL_BUFFER_SIZE - State.BufferedSize;
                    UnsafeUtility.MemCpy(Buffer + State.BufferedSize, bInput, loadSize);
                    bInput += loadSize;

                    ConsumeStripes(Acc, ref State.NbStripesSoFar, Buffer, INTERNAL_BUFFER_STRIPES, secret);

                    State.BufferedSize = 0;
                }

                // Process full buffer-sized blocks (256 bytes) from input
                // \todo Looks like there's a (safe) off-by-one: should probably use <= instead of <
                if (bInput + INTERNAL_BUFFER_SIZE < bEnd)
                {
                    byte* limit = bEnd - INTERNAL_BUFFER_SIZE;
                    bool isAligned = ((ulong)bInput & 7) == 0;
                    if (isAligned)
                    {
                        // For aligned input, process directly from the input buffer
                        do
                        {
                            ConsumeStripes(Acc, ref State.NbStripesSoFar, bInput, INTERNAL_BUFFER_STRIPES, secret);
                            bInput += INTERNAL_BUFFER_SIZE;
                        } while (bInput < limit);
                    }
                    else
                    {
                        // For unaligned input, copy to buffer to get 8-byte aligned input
                        do
                        {
                            UnsafeUtility.MemCpy(Buffer, bInput, INTERNAL_BUFFER_SIZE);
                            ConsumeStripes(Acc, ref State.NbStripesSoFar, Buffer, INTERNAL_BUFFER_STRIPES, secret);
                            bInput += INTERNAL_BUFFER_SIZE;
                        } while (bInput < limit);
                    }

                    // \note Uses the end of the buffer as memory to repeat bytes for last stripe at digest
                    UnsafeUtility.MemCpy(Buffer + INTERNAL_BUFFER_SIZE - STRIPE_LEN, bInput - STRIPE_LEN, STRIPE_LEN);
                }

                // Copy any remaining bytes to buffer
                if (bInput < bEnd)
                {
                    long newBufferedSize = bEnd - bInput;
                    UnsafeUtility.MemCpy(Buffer, bInput, newBufferedSize);
                    State.BufferedSize = (int) newBufferedSize;
                }
            }

            /// <summary>
            /// Compute the 64bits value based on all the data that have been accumulated
            /// </summary>
            /// <returns>The hash value</returns>
            public unsafe ulong DigestHash64()
            {
                unchecked
                {
                    byte* secret = SecretKey;
                    if (State.TotalLength > MIDSIZE_MAX)
                    {
                        DigestLong(secret);
                        return MergeAcc(Acc, secret + SECRET_MERGEACCS_START, (ulong) State.TotalLength * PRIME64_1);
                    }
                    else
                    {
                        return Hash64(Buffer, State.TotalLength, State.Seed);
                    }
                }
            }

            #endregion

            #region Constants

            private static readonly int SECRET_LIMIT = SECRET_KEY_SIZE - STRIPE_LEN;
            private static readonly int NB_STRIPES_PER_BLOCK = SECRET_LIMIT / SECRET_CONSUME_RATE;
            private static readonly int INTERNAL_BUFFER_SIZE = 256;
            private static readonly int INTERNAL_BUFFER_STRIPES = INTERNAL_BUFFER_SIZE / STRIPE_LEN;

            #endregion

            #region Wrapper to internal data storage

            unsafe ulong* Acc
            {
                [DebuggerStepThrough]
                get => (ulong*) UnsafeUtility.AddressOf(ref State.Acc);
            }

            unsafe byte* Buffer
            {
                [DebuggerStepThrough]
                get => (byte*) UnsafeUtility.AddressOf(ref State.Buffer);
            }

            unsafe byte* SecretKey
            {
                [DebuggerStepThrough]
                get => (byte*) UnsafeUtility.AddressOf(ref State.SecretKey);
            }

            #endregion

            #region Data storage

            private StreamingStateData State;

            [StructLayout(LayoutKind.Explicit)]
            struct StreamingStateData
            {
                [FieldOffset(0)] public ulong Acc; // 64 bytes
                [FieldOffset(64)] public byte Buffer; // 256 bytes
                [FieldOffset(320)] public int IsHash64; // 4 bytes
                [FieldOffset(324)] public int BufferedSize; // 4 bytes
                [FieldOffset(328)] public int NbStripesSoFar; // 4 bytes + 4 padding
                [FieldOffset(336)] public long TotalLength; // 8 bytes
                [FieldOffset(344)] public ulong Seed; // 8 bytes
                [FieldOffset(352)] public byte SecretKey; // 192 bytes
                [FieldOffset(540)] public byte _PadEnd;
            }

            #endregion

            #region Internals

            private unsafe void DigestLong(byte* secret)
            {
                if (State.BufferedSize >= STRIPE_LEN)
                {
                    int totalNbStripes = (State.BufferedSize - 1) / STRIPE_LEN;
                    ConsumeStripes(Acc, ref State.NbStripesSoFar, Buffer, totalNbStripes, secret);

                    /*if (X86.Avx2.IsAvx2Supported)
                    {
                        Avx2Accumulate512(acc, Buffer + State.BufferedSize - STRIPE_LEN, null,
                            secret + SECRET_LIMIT - SECRET_LASTACC_START);
                    }
                    else*/
                    {
                        DefaultAccumulate512(Acc, Buffer + State.BufferedSize - STRIPE_LEN, secret + SECRET_LIMIT - SECRET_LASTACC_START);
                    }
                }
                else
                {
                    byte* lastStripe = stackalloc byte[STRIPE_LEN];
                    int catchupSize = STRIPE_LEN - State.BufferedSize;
                    UnsafeUtility.MemCpy(lastStripe, Buffer + INTERNAL_BUFFER_SIZE - catchupSize, catchupSize);
                    UnsafeUtility.MemCpy(lastStripe + catchupSize, Buffer, State.BufferedSize);
                    /*if (X86.Avx2.IsAvx2Supported)
                    {
                        Avx2Accumulate512(acc, lastStripe, null, secret+SECRET_LIMIT-SECRET_LASTACC_START);
                    }
                    else*/
                    {
                        DefaultAccumulate512(Acc, lastStripe, secret+SECRET_LIMIT-SECRET_LASTACC_START);
                    }
                }
            }

            private unsafe void ConsumeStripes(ulong* acc, ref int nbStripesSoFar, byte* input, long totalStripes, byte* secret)
            {
                if (NB_STRIPES_PER_BLOCK - nbStripesSoFar <= totalStripes)
                {
                    var nbStripes = NB_STRIPES_PER_BLOCK - nbStripesSoFar;
                    /*if (X86.Avx2.IsAvx2Supported)
                    {
                        Avx2Accumulate(acc, input, null, secret + nbStripesSoFar * SECRET_CONSUME_RATE, nbStripes);
                        Avx2ScrambleAcc(acc, secret + SECRET_LIMIT);
                        Avx2Accumulate(acc, input + nbStripes * STRIPE_LEN, null, secret, totalStripes - nbStripes);
                    }
                    else*/
                    {
                        DefaultAccumulate(acc, input, secret + nbStripesSoFar * SECRET_CONSUME_RATE, nbStripes);
                        DefaultScrambleAcc(acc, secret + SECRET_LIMIT);
                        DefaultAccumulate(acc, input + nbStripes * STRIPE_LEN, secret, totalStripes - nbStripes);
                    }

                    nbStripesSoFar = (int) totalStripes - nbStripes;
                }
                else
                {
                    /*if (X86.Avx2.IsAvx2Supported)
                    {
                        Avx2Accumulate(acc, input, null, secret + nbStripesSoFar * SECRET_CONSUME_RATE, totalStripes);
                    }
                    else*/
                    {
                        DefaultAccumulate(acc, input, secret + nbStripesSoFar * SECRET_CONSUME_RATE, totalStripes);
                    }

                    nbStripesSoFar += (int) totalStripes;
                }
            }

            #endregion
        }
    }
}

#endif
