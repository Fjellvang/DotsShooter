// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.IO;
using Metaplay.Core.Memory;
using Metaplay.Core.Serialization;
using System;
using System.Linq;
using static System.FormattableString;

namespace Metaplay.Core
{
    public static class ChecksumUtil
    {
#if UNITY_2018_1_OR_NEWER
        // Use the XXH3 implementation that works with CPUs that do not support unaligned memory reads.
        // Basically, ARMv7 cannot be relied to support unaligned loads. Approx. 10-15% of Android devices,
        // in 2024, are still such devices.
        static bool _useXxHash3Workaround = false; // default to using Unity's faster implementation

        [UnityEngine.RuntimeInitializeOnLoadMethod]
        private static void Initialize()
        {
            bool isArmCPU = UnityEngine.SystemInfo.processorType.Contains("arm", StringComparison.OrdinalIgnoreCase);
            _useXxHash3Workaround = isArmCPU && !Environment.Is64BitProcess;
        }

        /// <summary>
        /// Unity's implementation of XXH3 is broken for these specific sizes, producing the incorrect
        /// hash output. For them, we fall back to our vendored implementation that has these fixed.
        /// </summary>
        /// <param name="size">The size of the buffer.</param>
        static bool IsBrokenSizeWithUnityHashImplementation(int size) =>
            (size == 16) || (size == 128) || (size == 240);
#endif

        /// <summary>
        /// Compute hash of an <see cref="IOBuffer"/> for the purpose of comparing two model states
        /// to detect Desyncs between the client and the server. The XXH3 hashing algorithm is used.
        /// </summary>
        /// <param name="buffer">Buffer to compute hash for</param>
        /// <returns>32-bit hash value</returns>
        public static uint ComputeHash(IOBuffer buffer)
        {
            return ComputeXxHash3(buffer);
        }

        /// <summary>
        /// Compute hash of a <see cref="ReadOnlySpan{Byte}"/> for the purpose of comparing two model
        /// states to detect Desyncs between the client and the server. The XXH3 hashing algorithm is used.
        /// </summary>
        /// <param name="buffer">Buffer to compute hash for</param>
        /// <returns>32-bit hash value</returns>
        public static uint ComputeHash(ReadOnlySpan<byte> buffer)
        {
            return ComputeXxHash3(buffer);
        }

        public static uint ComputeXxHash3(ReadOnlySpan<byte> buffer, uint seed = 0xc58f1a7b)
        {
#if UNITY_2018_1_OR_NEWER
            // In Unity, use the Unity.Collections implementation
            unsafe
            {
                fixed (byte* ptr = buffer)
                {
                    if (_useXxHash3Workaround || IsBrokenSizeWithUnityHashImplementation(buffer.Length))
                        return (uint)ModifiedUnityCollections.UnityAlignedXxHash3.Hash64(ptr, buffer.Length, seed);
                    else
                        return Unity.Collections.xxHash3.Hash64(ptr, buffer.Length, seed).x; // .x contains the lower 32 bits
                }
            }
#else
            return (uint)System.IO.Hashing.XxHash3.HashToUInt64(buffer, seed);
#endif
        }

        public static uint ComputeXxHash3(IOBuffer buffer, uint seed = 0xc58f1a7b)
        {
            buffer.BeginRead();
            try
            {
#if UNITY_2018_1_OR_NEWER
                // In Unity, use the Unity.Collections implementation
                if (_useXxHash3Workaround || IsBrokenSizeWithUnityHashImplementation(buffer.Count))
                {
                    ModifiedUnityCollections.UnityAlignedXxHash3.StreamingState xxHash3 = new(seed);
                    unsafe
                    {
                        for (int segmentIndex = 0; segmentIndex < buffer.NumSegments; ++segmentIndex)
                        {
                            IOBufferSegment segment = buffer.GetSegment(segmentIndex);
                            fixed (byte* ptr = segment.AsSpan())
                                xxHash3.Update(ptr, segment.Size);
                        }
                    }
                    return (uint)xxHash3.DigestHash64();
                }
                else
                {
                    Unity.Collections.xxHash3.StreamingState xxHash3 = new(isHash64: true, seed);
                    unsafe
                    {
                        for (int segmentIndex = 0; segmentIndex < buffer.NumSegments; ++segmentIndex)
                        {
                            IOBufferSegment segment = buffer.GetSegment(segmentIndex);
                            fixed (byte* ptr = segment.AsSpan())
                                xxHash3.Update(ptr, segment.Size);
                        }
                    }
                    return xxHash3.DigestHash64().x; // .x contains the lower 32 bits
                }
#else
                // No-allocation path for single-segment buffers
                if (buffer.NumSegments == 1)
                    return (uint)System.IO.Hashing.XxHash3.HashToUInt64(buffer.GetSegment(0).AsSpan(), seed);

                // Hash each IOBuffer segment separately
                System.IO.Hashing.XxHash3 xxHash3 = new(seed);
                for (int segmentIndex = 0; segmentIndex < buffer.NumSegments; ++segmentIndex)
                {
                    IOBufferSegment segment = buffer.GetSegment(segmentIndex);
                    xxHash3.Append(segment.AsSpan());
                }
                return (uint)xxHash3.GetCurrentHashAsUInt64();
#endif
            }
            finally
            {
                buffer.EndRead();
            }
        }

        public static string PrintDifference<TModel>(LogChannel log, string firstName, TModel first, byte[] firstSerialized, string secondName, TModel second, byte[] secondSerialized)
        {
            string stateDiff;
            Exception compareException = null;
            try
            {
                // \note Resolve state diff last in case there's some case that causes it to crash (it's not super robust)
                stateDiff = PrettyPrinter.Difference<TModel>(first, second);
            }
            catch (Exception ex)
            {
                compareException = ex;
                stateDiff = $"Exception during compare: {ex}";
            }

            string serializedFirst = TaggedWireSerializer.ToString(firstSerialized);
            string serializedSecond = TaggedWireSerializer.ToString(secondSerialized);
            bool serializedAreIdentical = (serializedFirst == serializedSecond);

            if (compareException != null)
                log.Error("Failed to compare states between {A} and {B}: {Exception}", firstName, secondName, compareException);
            else if (stateDiff != "")
                log.Warning("State difference ({A} vs {B}): {Diff}", firstName, secondName, stateDiff);
            else if (serializedAreIdentical)
                log.Warning("No difference detected between states ({A} vs {B}) detected.", firstName, secondName);
            else
                log.Warning("Serialization difference ({A} vs {B}) detected, but resulting state is equal. This may be for example due to different member ordering, some members only being serialized in one version, etc. Please compare the serialized states below.", firstName, secondName);

#if UNITY_EDITOR
#pragma warning disable MP_WGL_00 // "Feature is poorly supported in WebGL". False positive, this is editor-only.
            // Dump states to files for comparison using external diff tools
            System.IO.File.WriteAllText($"StateDump_{firstName}.txt",
                $"Full state of {firstName}:\n" + PrettyPrinter.Verbose(first) +
                $"\nSerialized state of {firstName}:\n" + serializedFirst);

            System.IO.File.WriteAllText($"StateDump_{secondName}.txt",
                $"Full state of {secondName}:\n" + PrettyPrinter.Verbose(second) +
                $"\nSerialized state of {secondName}:\n" + serializedSecond);
#pragma warning restore MP_WGL_00
#endif

            // Print states to log
            log.Warning("Full {A} state: {State}", firstName, PrettyPrinter.Verbose(first));
            log.Warning("Full {B} state: {State}", secondName, PrettyPrinter.Verbose(second));

            // Print serialized states to log (in some cases, the model diff can be empty and serialized states still differ)
            log.Warning("Serialized {A} state: {SerializedState}", firstName, serializedFirst);
            log.Warning("Serialized {B} state: {SerializedState}", secondName, serializedSecond);

            return stateDiff;
        }
    }

    // \todo Migrate to some test project
    public class ChecksumUtilTest
    {
        static readonly (int, uint)[] _hashes =
        {
            (1, 0xf0f33cd5),
            (2, 0xc9a97827),
            (3, 0x3ddc74c9),
            (4, 0x2d73516d),
            (5, 0xa0e4c4bb),
            (6, 0x17c43fdf),
            (7, 0xbe8abdfd),
            (8, 0xcb416ce9),
            (9, 0xf45fc5f3),
            (10, 0x2078ae2a),
            (13, 0xcd1d8099),
            (16, 0x4fb9a590), // broken in Unity implementation!
            (19, 0xfdd1ef46),
            (22, 0x20869614),
            (25, 0x5e42199a),
            (28, 0xef2a0fb5),
            (31, 0x10b6d589),
            (34, 0x0611e49d),
            (37, 0x4c048f30),
            (40, 0x546ac51e),
            (43, 0xa3017e07),
            (46, 0x677b0227),
            (49, 0x165c719c),
            (52, 0xa0262e1a),
            (55, 0x85843cde),
            (58, 0xd63bacd0),
            (61, 0x57bfefef),
            (64, 0xf57f7d0e),
            (67, 0x2195a6b8),
            (70, 0xbc1229bf),
            (73, 0xc782cf30),
            (76, 0x33c9b392),
            (79, 0x657107c4),
            (82, 0xc4c32a92),
            (85, 0xc0aff6e4),
            (88, 0xe70e743e),
            (91, 0xd3e6f8e2),
            (94, 0xf5268766),
            (97, 0x582a9e61),
            (100, 0xd8a04be5),
            (113, 0xece76000),
            (126, 0x29b63617),
            (128, 0xdad07d0d), // broken in Unity implementation!
            (139, 0x55b33b70),
            (152, 0x7bf91180),
            (165, 0x4ea4ba6b),
            (178, 0x3cf96702),
            (191, 0xc85824f6),
            (204, 0xea5a8bd9),
            (217, 0xe5a39c70),
            (230, 0x60efe7f5),
            (240, 0xb7151af0), // broken in Unity implementation!
            (243, 0xc9bd2e22),
            (256, 0xab28c7b4),
            (269, 0x32ac51a7),
            (282, 0x53074c39),
            (295, 0x7e33b2fb),
            (308, 0x3092545c),
            (321, 0x2ab85fa1),
            (334, 0x335d7750),
            (347, 0xa9871fce),
            (360, 0xf76c7231),
            (373, 0xe7195237),
            (386, 0x877a47b8),
            (399, 0x88bc8e64),
            (412, 0xcac4d4eb),
            (425, 0xb6f65e31),
            (438, 0xbb341d4d),
            (451, 0xf65cf695),
            (464, 0x9a2d322e),
            (477, 0xfa6d2836),
            (490, 0x83f922bd),
            (503, 0x7c523c64),
            (516, 0xf6b48f15),
            (529, 0x2159aa07),
            (542, 0x08e3f811),
            (555, 0xa82a610f),
            (568, 0xb7198db9),
            (581, 0xadfd8d50),
            (594, 0x734357bb),
            (607, 0xa448cb76),
            (620, 0x96bb64be),
            (633, 0xf85b74f6),
            (646, 0x1a60db57),
            (659, 0xad3b36eb),
            (672, 0x05e5016f),
            (685, 0x7b4056fe),
            (698, 0x49b23ee2),
            (711, 0xd07fe1ed),
            (724, 0x37485648),
            (737, 0xed01ec7c),
            (750, 0x5e345f54),
            (763, 0x736d740c),
            (776, 0x1e3008ae),
            (789, 0xf74403e8),
            (802, 0x4c2b72d3),
            (815, 0x34951471),
            (828, 0xaed7cc95),
            (841, 0xaaa239ed),
            (854, 0x230226f5),
            (867, 0x1411fa39),
            (880, 0xa20a23ce),
            (893, 0x48094b72),
            (906, 0x0d77f71f),
            (919, 0x92df92c4),
            (932, 0x86a54754),
            (945, 0xfa2d1732),
            (958, 0xb271640c),
            (971, 0x9d50d4b5),
            (984, 0xa4a0820f),
            (997, 0x2556201e),
            (1010, 0x993cb431),
            (1123, 0x7bcbdc18),
            (1236, 0xe034d371),
            (1349, 0x3f523b60),
            (1462, 0x13b83e73),
            (1575, 0x5f6fca87),
            (1688, 0xce0e5043),
            (1801, 0x25643fa5),
            (1914, 0x5da81ff2),
            (2027, 0xcb9ff2d4),
            (2140, 0xbf687da0),
            (2253, 0x6530458c),
            (2366, 0xa2089bd5),
            (2479, 0x5c391d82),
            (2592, 0x80d9dce9),
            (2705, 0xe8843138),
            (2818, 0xe491c40e),
            (2931, 0x654d9eb9),
            (3044, 0x66b9d26e),
            (3157, 0x32eb66df),
            (3270, 0x3d00f051),
            (3383, 0xd5cdbef9),
            (3496, 0x2598be37),
            (3609, 0x12782282),
            (3722, 0x9f5ac150),
            (3835, 0x4064fbef),
            (3948, 0x19c6f080),
            (4061, 0xbe0350b8),
            (4174, 0x627423d2),
            (4287, 0xbac60e97),
            (4400, 0x1204d9df),
            (4513, 0xef381b60),
            (4626, 0x46ff609c),
            (4739, 0x2af53d79),
            (4852, 0x9a5a4c9a),
            (4965, 0x89b92c01),
            (5078, 0xd8af58b6),
            (5191, 0x60778c00),
            (5304, 0x4d2ba6a6),
            (5417, 0x6474a7bf),
            (5530, 0x17113272),
            (5643, 0xc421d871),
            (5756, 0x6be3b753),
            (5869, 0xf87f530e),
            (5982, 0xb1c8bc51),
            (6095, 0x9a963fdc),
            (6208, 0xbc49fd77),
            (6321, 0xebf20942),
            (6434, 0x7ef3ca7e),
            (6547, 0x673b5318),
            (6660, 0xedb3282e),
            (6773, 0x33970833),
            (6886, 0x82920575),
            (6999, 0x074ba1e2),
            (7112, 0x9537f8bf),
            (7225, 0x46c01e54),
            (7338, 0x1d3251f2),
            (7451, 0x3d1c75c3),
            (7564, 0x08e7100b),
            (7677, 0xabd1963a),
            (7790, 0x08d040a4),
            (7903, 0xaf6e42e1),
            (8016, 0x24933b22),
            (8129, 0xd6cd4af4),
            (8242, 0x55b4aaff),
            (8355, 0x0849c2fb),
            (8468, 0x57122635),
            (8581, 0x376806c0),
            (8694, 0x2dcc5eb3),
            (8807, 0x82d0cbc8),
            (8920, 0x6e1d541c),
            (9033, 0x87b936c1),
            (9146, 0xd48bd6ab),
            (9259, 0xd871bae0),
            (9372, 0x49ec64bc),
            (9485, 0xfb2f008b),
            (9598, 0xf601d238),
            (9711, 0x18941259),
            (9824, 0xa74cb69d),
            (9937, 0x3ab82090),
        };

        public static void TestHashing()
        {
            foreach ((int size, uint expectedHash) in _hashes)
            {
                byte[] bytes = Enumerable.Range(0, size).Select(ndx => (byte)ndx).ToArray();

                using SegmentedIOBuffer buffer = new SegmentedIOBuffer(segmentSize: 64);
                using (IOWriter writer = new IOWriter(buffer))
                {
                    RandomPCG rng = RandomPCG.CreateNew();
                    int offset = 0;
                    int remaining = size;
                    while (remaining > 0)
                    {
                        int spanSize = 1 + rng.NextInt(System.Math.Min(remaining, 30));
                        writer.WriteSpan(bytes.AsSpan(offset, spanSize));
                        offset += spanSize;
                        remaining -= spanSize;
                    }
                }

                uint hashBytes = ChecksumUtil.ComputeXxHash3(bytes);
                uint hashBuffer = ChecksumUtil.ComputeXxHash3(buffer);

                if (hashBytes != expectedHash || hashBuffer != expectedHash)
                    DebugLog.WriteLine(Invariant($"ComputeXxHash3() failed for size={bytes.Length}: ref=0x{expectedHash:x8}, hashBytes=0x{hashBytes:x8}, hashBuffer=0x{hashBuffer:x8}"));
            }
        }
    }
}
