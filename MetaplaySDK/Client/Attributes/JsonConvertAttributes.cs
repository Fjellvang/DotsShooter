// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Config;
using System;

namespace Metaplay.Core.Json
{
    public enum JsonSerializationMode
    {
        /// <summary>
        /// Default serialization mode
        /// </summary>
        Default,

        /// <summary>
        /// Serialize for GDPR export
        /// </summary>
        GdprExport,

        /// <summary>
        /// Serialize analytics events
        /// </summary>
        AnalyticsEvents,

        /// <summary>
        /// Serialize for use in AdminAPI (dashboard).
        /// </summary>
        AdminApi,
    }

    /// <summary>
    /// On a member that is of a type that's normally serialized as a reference,
    /// this attribute causes that member to be serialized as its contents instead.
    ///
    /// For example, IGameConfigData-implementing types are
    /// normally serialized as just the ConfigKey. This attribute causes such a member
    /// to be serialized as the contents of the Game Config data class instead.
    /// </summary>
    /// <remarks>
    /// This only affects the member it is directly applied to; it does not propagate
    /// to nested members.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ForceSerializeByValueAttribute : Attribute
    {
    }

    /// <summary>
    /// On a member, excludes serialization of any read-only properties in the
    /// value. This applies recursively to all submembers in the value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class JsonExcludeReadOnlyProperties : Attribute
    {
    }

    /// <summary>
    /// On collection member, causes <c>null</c> member to be serialized as empty container
    /// of the same type. For List-like, results in an empty list <c>[]</c>, and for Dictionary-likes
    /// results in an empty object <c>{}</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class JsonSerializeNullCollectionAsEmptyAttribute : Attribute
    {
    }

    /// <summary>
    /// The field or property is only included in the JSON output in the defined serialization mode. In other modes, the field or property is ignored.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class IncludeOnlyInJsonSerializationModeAttribute : Attribute
    {
        public readonly JsonSerializationMode Mode;

        public IncludeOnlyInJsonSerializationModeAttribute(JsonSerializationMode mode) => Mode = mode;
    }
}
