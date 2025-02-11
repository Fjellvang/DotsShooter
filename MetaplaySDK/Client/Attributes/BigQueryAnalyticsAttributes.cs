// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;

namespace Metaplay.Core.Analytics
{
    /// <summary>
    /// Determines the formatting mode of an field in Analytics Event when processed by
    /// BigQuery Analytics Sink.
    /// </summary>
    public enum BigQueryAnalyticsFormatMode
    {
        /// <summary>
        /// Field, Property or Type is ignored and data is not written
        /// </summary>
        Ignore = 0,

        /// <summary>
        /// Field or Property is an enumerable with Key field. The enumerables are extracted as members.
        /// <para>
        /// Practically this formats string-keyed Dictionary as if it was an object with where the fields correspond
        /// to the dictionary elements. The type must be enumerable, and the element type must contain fields or
        /// properties "Key" and "Value".
        /// </para>
        /// </summary>
        ExtractDictionaryElements = 1,
    }

    /// <summary>
    /// Specifies formatting rule for the Field, Property or a Type. If formatting is defined
    /// for both the type and the field (or property), the mode in field (or property) declaration
    /// is used.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct)]
    public class BigQueryAnalyticsFormatAttribute : Attribute
    {
        public BigQueryAnalyticsFormatMode Mode { get; private set; }
        public BigQueryAnalyticsFormatAttribute(BigQueryAnalyticsFormatMode mode)
        {
            Mode = mode;
        }
    }

    /// <summary>
    /// Specifies the name of an analytics event, or a parameter in an analytics event,
    /// for BigQuery. Without this, the default name is used (C# class name for events,
    /// or member name for parameters).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field)]
    public class BigQueryAnalyticsNameAttribute : Attribute
    {
        public string Name { get; private set; }

        public BigQueryAnalyticsNameAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }

    /// <summary>
    /// Specifies that the type is allowed to be recursed N times during flattening. Exceeding
    /// the N in recursion will results in "__recursion_limit" string value to be formatted instead.
    /// Note that the recursion limit is counted on <i>TYPES</i>, not <i>INSTANCES</i>. Recursion limit
    /// of 1 means that only the top-most type is flattened, and any recursive type inside it would
    /// not be flattened.
    ///
    /// <para>
    /// If attribute is not set, recursion is not allowed. If potentially recursive type is encountered
    /// in server start, server start fails with an error.
    /// </para>
    /// <para>
    /// In Metaplay BigQuery implementation, flattening means the process of converting object tree to
    /// a key-value list.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class BigQueryAnalyticsRecursionLimitAttribute : Attribute
    {
        public int RecursionLimit { get; private set; }

        public BigQueryAnalyticsRecursionLimitAttribute(int recursionLimit)
        {
            if (recursionLimit <= 0)
                throw new ArgumentOutOfRangeException(paramName: nameof(recursionLimit), message: "recursionLimit must be at least 1. Recursion limit of 1 denotes the top-most type encountered is processed, but any same type inside the type would be skipped.");
            RecursionLimit = recursionLimit;
        }
    }
}
