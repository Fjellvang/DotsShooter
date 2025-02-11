// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Diagnostics;
using System.Globalization;

namespace Metaplay.Core
{
    /// <summary>
    /// Mark a class as containing EntityKind registrations -- all 'static EntityKind' fields in the class are
    /// considered EntityKind definitions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class EntityKindRegistryAttribute : Attribute
    {
        public readonly int StartIndex;
        public readonly int EndIndex;

        public EntityKindRegistryAttribute(int startIndex, int endIndex)
        {
            Debug.Assert(startIndex >= 0 && endIndex >= 0 && startIndex < endIndex, string.Format(CultureInfo.InvariantCulture, "Invalid range of values {0}..{1}", startIndex, endIndex));
            StartIndex = startIndex;
            EndIndex   = endIndex;
        }
    }
}
