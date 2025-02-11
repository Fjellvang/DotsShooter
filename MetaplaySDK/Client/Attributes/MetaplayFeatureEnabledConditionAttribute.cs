// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Metaplay.Core
{
    /// <summary>
    /// A generic way to communicate that a property is associated with a Metaplay feature
    /// that can be enabled or disabled. The semantics of what makes a feature enabled is
    /// defined by inherited classes and the semantics of what this means for a given property
    /// is context-dependent.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface  | AttributeTargets.Enum | AttributeTargets.Assembly)]
    public abstract class MetaplayFeatureEnabledConditionAttribute : Attribute
    {
        public abstract bool IsEnabled { get; }
    }

    public static class MetaplayFeatureEnabledConditionAttributeUtil
    {
        public enum InspectResult
        {
            /// <summary>
            /// If no feature flags => enabled
            /// </summary>
            NoFeatureAttributes = 0,

            /// <summary>
            /// All declared features are disabled => not enabled
            /// </summary>
            AllAttributesDisabled = 1,

            /// <summary>
            /// Any feature is enabled => enabled
            /// </summary>
            SomeAttributeEnabled = 2,
        }

        /// <summary>
        /// <inheritdoc cref="IsEnabledForAttributes(IEnumerable{MetaplayFeatureEnabledConditionAttribute})"/>
        /// </summary>
        public static InspectResult InspectFeatureAttributes(IEnumerable<MetaplayFeatureEnabledConditionAttribute> attributes)
        {
            if (attributes == null)
                return InspectResult.NoFeatureAttributes;

            bool hasAnyFeatureRequirements = false;
            foreach (MetaplayFeatureEnabledConditionAttribute attribute in attributes)
            {
                hasAnyFeatureRequirements = true;
                if (attribute.IsEnabled)
                    return InspectResult.SomeAttributeEnabled;
            }

            if (!hasAnyFeatureRequirements)
                return InspectResult.NoFeatureAttributes;
            return InspectResult.AllAttributesDisabled;
        }

        /// <summary>
        /// Checks if a type is enabled, given its attributes. <c>null</c> list is treated identically to an empty list. Supplying attributes is mainly useful for when attributes are cached for performance.
        /// </summary>
        public static bool IsEnabledForAttributes(IEnumerable<MetaplayFeatureEnabledConditionAttribute> attributes)
        {
            InspectResult result = InspectFeatureAttributes(attributes);
            return result != InspectResult.AllAttributesDisabled;
        }
    }

    public static class MetaplayFeatureEnabledConditionAttributeExtensions
    {
        /// <summary>
        /// Checks for presence of <see cref="MetaplayFeatureEnabledConditionAttribute"/> and checks whether the feature is
        /// enabled or not. If no attribute is found this method returns true.
        /// </summary>
        public static bool IsMetaFeatureEnabled(this Type type)
        {
            bool hasSomeAttributes = false;
            for (Type cursor = type; cursor != null; cursor = cursor.BaseType)
            {
                // Feature is enabled if either type or assembly has some feature enabled.

                MetaplayFeatureEnabledConditionAttributeUtil.InspectResult typeResult = MetaplayFeatureEnabledConditionAttributeUtil.InspectFeatureAttributes(type.GetCustomAttributes<MetaplayFeatureEnabledConditionAttribute>(inherit: false));
                if (typeResult == MetaplayFeatureEnabledConditionAttributeUtil.InspectResult.SomeAttributeEnabled)
                    return true;
                if (typeResult == MetaplayFeatureEnabledConditionAttributeUtil.InspectResult.AllAttributesDisabled)
                    hasSomeAttributes = true;

                MetaplayFeatureEnabledConditionAttributeUtil.InspectResult assemblyResult = MetaplayFeatureEnabledConditionAttributeUtil.InspectFeatureAttributes(type.Assembly?.GetCustomAttributes<MetaplayFeatureEnabledConditionAttribute>());
                if (assemblyResult == MetaplayFeatureEnabledConditionAttributeUtil.InspectResult.SomeAttributeEnabled)
                    return true;
                if (assemblyResult == MetaplayFeatureEnabledConditionAttributeUtil.InspectResult.AllAttributesDisabled)
                    hasSomeAttributes = true;
            }

            // If we had some attributes, and they were all false (we short circuit on true), the feature is not enabled.
            return !hasSomeAttributes;
        }

        /// <inheritdoc cref="IsMetaFeatureEnabled(System.Type)"/>
        public static bool IsMetaFeatureEnabled(this MemberInfo member)
        {
            // Since this is a query for a Field or Property, we don't check the Type or Assembly. The expectation is
            // that only attributes on the Member have effect.
            return MetaplayFeatureEnabledConditionAttributeUtil.IsEnabledForAttributes(member.GetCustomAttributes<MetaplayFeatureEnabledConditionAttribute>(inherit: true));
        }

        /// <inheritdoc cref="IsMetaFeatureEnabled(System.Type)"/>
        public static bool IsMetaFeatureEnabled(this Assembly assembly)
        {
            return MetaplayFeatureEnabledConditionAttributeUtil.IsEnabledForAttributes(assembly.GetCustomAttributes<MetaplayFeatureEnabledConditionAttribute>());
        }
    }
}
