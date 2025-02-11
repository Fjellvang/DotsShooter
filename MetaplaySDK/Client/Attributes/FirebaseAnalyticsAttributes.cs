// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;

namespace Metaplay.Core.Analytics
{
    /// <summary>
    /// Specifies the Field, Property or a Type should not be written into Firebase Analytics. This can be
    /// used to annotate and disable unsupported types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct)]
    public class FirebaseAnalyticsIgnoreAttribute : Attribute
    {
    }

    /// <summary>
    /// Specifies the name of an analytics event, or a parameter in an analytics event,
    /// for Firebase. Without this, the default name is used (C# class name for events,
    /// or member name for parameters).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field)]
    public class FirebaseAnalyticsNameAttribute : Attribute
    {
        public string Name { get; private set; }

        public FirebaseAnalyticsNameAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }
}
