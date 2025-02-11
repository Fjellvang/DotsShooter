// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;

namespace Metaplay.Core
{
    /// <summary>
    /// Field represents secure information (such as passwords) and should be sanitized when outputted into logs
    /// or sent to the dashboard. Also works when serializing to json. In json-serialization, all non-null values
    /// are converted to strings.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class SensitiveAttribute : Attribute
    {
    }

    [Flags]
    public enum PrettyPrintFlag
    {
        None       = 0,
        SizeOnly   = 1 << 0, // Only print size of a collection
        Shorten    = 1 << 2, // Show a shortened version of the data to avoid spamming logs.
        Hide       = 1 << 3, // Hide member field from printing
        HideInDiff = 1 << 4, // Hide member field from printing, but only when printing object diffs
    }

    public class PrettyPrintAttribute : Attribute
    {
        public PrettyPrintFlag Flags { get; private set; }

        public PrettyPrintAttribute(PrettyPrintFlag flags)
        {
            Flags = flags;
        }
    }
}
