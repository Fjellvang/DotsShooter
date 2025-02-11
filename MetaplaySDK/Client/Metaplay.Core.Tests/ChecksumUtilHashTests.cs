// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.IO;
using Metaplay.Core.Memory;
using NUnit.Framework;
using System.Collections.Generic;

namespace Metaplay.Core.Tests
{
    class ChecksumUtilHashTests
    {
        static IEnumerable<(uint, byte[])> GetTestVectors()
        {
            byte[] GenVector(int size)
            {
                byte[] buf = new byte[size];
                for (int ndx = 0; ndx < size; ++ndx)
                    buf[ndx] = (byte)(size * 13 + ndx * 25547 + (ndx * 25547) >> 15);
                return buf;
            }

            // \note Values are the XXHash3 (we're assuming ChecksumUtil.ComputeHash() uses it)
            yield return (2515881945, new byte[] { });
            yield return (4042472661, new byte[] { 0 });
            yield return (758450742, new byte[] { 0, 0 });
            yield return (2595596451, new byte[] { 0, 0, 0 });
            yield return (4199027862, new byte[] { 0, 0, 0, 0 });
            yield return (883074852, new byte[] { 0, 0, 0, 0, 0 });
            yield return (1755714465, new byte[] { 1, 2, 3, 4, 5 });
            yield return (3991527961, new byte[] { 1, 2, 3, 4, 5, 6 });
            yield return (1219537456, new byte[] { 1, 2, 3, 4, 5, 6, 7 });
            yield return (796872491, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            yield return (3514274885, new byte[] { 1, 2, 3, 4, 5, 6, 8, 9, 10, 11 });
            yield return (1058408193, new byte[] { 1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 12, 13, 14, 15, 16 });
            yield return (854103905, GenVector(255));
            yield return (3115696448, GenVector(256));
            yield return (7113807, GenVector(257));
            yield return (2917542961, GenVector(1023));
            yield return (797312290, GenVector(1024));
            yield return (3806503513, GenVector(1025));
        }

        [Test]
        public void TestWithRawArray()
        {
            foreach ((uint result, byte[] bytes) in GetTestVectors())
                Assert.AreEqual(result, ChecksumUtil.ComputeHash(bytes));
        }

        [Test]
        public void TestWithFlatIOBuffer()
        {
            foreach ((uint result, byte[] bytes) in GetTestVectors())
            {
                using FlatIOBuffer buffer = new FlatIOBuffer();
                using (var writer = new IOWriter(buffer))
                    writer.WriteBytes(bytes, 0, bytes.Length);
                Assert.AreEqual(result, ChecksumUtil.ComputeHash(buffer));
            }
        }

        [Test]
        public void TestWithSegmentedIOBuffer()
        {
            foreach ((uint result, byte[] bytes) in GetTestVectors())
            {
                using SegmentedIOBuffer buffer = new SegmentedIOBuffer(segmentSize: 13);
                using (var writer = new IOWriter(buffer))
                    writer.WriteBytes(bytes, 0, bytes.Length);
                Assert.AreEqual(result, ChecksumUtil.ComputeHash(buffer));
            }
        }
    }
}
