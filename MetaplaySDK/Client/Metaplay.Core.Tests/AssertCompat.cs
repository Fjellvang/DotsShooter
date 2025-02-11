// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Metaplay.Core.Tests
{
    /// <summary>
    /// Compatibility shims for various NUnit <see cref="Assert"/> versions.
    /// </summary>
    static class AssertCompat
    {
        /// <summary>
        /// Verifies the values are equal, similar to how <see cref="Assert.AreEqual"/> would.
        /// This method adds custom equality tests for types where current NUnit version has known issues.
        /// </summary>
        public static void AreEqual<T>(T expected, T actual)
        {
            // Trivial checks. We do these always
            if (expected is null && actual is null)
                return;
            if (expected is null && !(actual is null))
            {
                Assert.Fail("Expected null, got not-null");
                return;
            }
            if (!(expected is null) && actual is null)
            {
                Assert.Fail("Expected not-null, got null");
                return;
            }

            // \todo: more precise version detection
#if UNITY_EDITOR

            Type type = typeof(T);
            if (type.IsArray)
            {
                if (type.GetElementType().IsGenericTypeOf(typeof(ValueTuple<,>)))
                {
                    dynamic dynExpected = expected;
                    dynamic dynActual = actual;
                    AssertEqual_ValueTuple2(dynExpected, dynActual);
                    return;
                }
                else if (type.GetElementType().IsGenericTypeOf(typeof(ValueTuple<,,>)))
                {
                    dynamic dynExpected = expected;
                    dynamic dynActual = actual;
                    AssertEqual_ValueTuple3(dynExpected, dynActual);
                    return;
                }
            }
            else if (type.IsGenericTypeOf(typeof(HashSet<>)))
            {
                dynamic dynExpected = expected;
                dynamic dynActual = actual;
                AssertEqual_HashSet(dynExpected, dynActual);
                return;
            }

#endif

            Assert.AreEqual(expected, actual);
        }

        static void AssertEqual_ValueTuple2<T1, T2>((T1, T2)[] expected, (T1, T2)[] actual)
        {
            // Manually check row by row for compatibility with Unity's "Assert.AreEqual"
            Assert.AreEqual(expected.Length, actual.Length);
            for (int ndx = 0; ndx < expected.Length; ++ndx)
            {
                Assert.AreEqual(expected[ndx].Item1, actual[ndx].Item1);
                Assert.AreEqual(expected[ndx].Item2, actual[ndx].Item2);
            }
        }

        static void AssertEqual_ValueTuple3<T1, T2, T3>((T1, T2, T3)[] expected, (T1, T2, T3)[] actual)
        {
            // Manually check row by row for compatibility with Unity's "Assert.AreEqual"
            Assert.AreEqual(expected.Length, actual.Length);
            for (int ndx = 0; ndx < expected.Length; ++ndx)
            {
                Assert.AreEqual(expected[ndx].Item1, actual[ndx].Item1);
                Assert.AreEqual(expected[ndx].Item2, actual[ndx].Item2);
                Assert.AreEqual(expected[ndx].Item3, actual[ndx].Item3);
            }
        }

        static void AssertEqual_HashSet<T>(HashSet<T> expected, HashSet<T> actual)
        {
            // Manually check in unordered way. Check this way to get nice error messages.
            foreach (T element in expected)
            {
                if (!actual.Contains(element))
                    Assert.Fail("Expected HashSet to contain {0}, the actual value did not.", element);
            }
            foreach (T element in actual)
            {
                if (!expected.Contains(element))
                    Assert.Fail("Actual value contains {0}, which is not in the expected set.", element);
            }
        }
    }
}
