// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Metaplay.Core.Config
{
    /// <summary>
    /// Represents a single config item along with info about which variant it belongs to (if any).
    /// </summary>
    public struct VariantConfigItem<TRef, TItem> // \todo where TItem : IHasGameConfigKey (but there is no non-generic IHasGameConfigKey)
    {
        public readonly TItem                       Item;
        public readonly string                      VariantIdMaybe;
        public readonly List<TRef>                  Aliases;
        public readonly GameConfigSourceLocation    SourceLocation;

        public VariantConfigItem(TItem item, string variantIdMaybe, List<TRef> aliases, GameConfigSourceLocation sourceLocation)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            Item           = item;
            VariantIdMaybe = variantIdMaybe;
            Aliases        = aliases;
            SourceLocation = sourceLocation;
        }
    }

    /// <summary>
    /// Represents a single key-value structure config member along with info about which variant it belongs to (if any).
    /// </summary>
    public struct VariantConfigStructureMember
    {
        public readonly ConfigStructureMember       Member;
        public readonly string                      VariantIdMaybe;
        public readonly GameConfigSourceLocation    SourceLocation;

        public VariantConfigStructureMember(ConfigStructureMember member, string variantIdMaybe, GameConfigSourceLocation sourceLocation)
        {
            Member = member;
            VariantIdMaybe = variantIdMaybe;
            SourceLocation = sourceLocation;
        }
    }

    /// <summary>
    /// Represents a single key-value structure config member.
    /// </summary>
    public struct ConfigStructureMember
    {
        public readonly string     MemberName;

        /// <summary>
        /// Null if using constructor based deserialization
        /// </summary>
        public readonly MemberInfo MemberInfo;
        public readonly object     MemberValue;

        public ConfigStructureMember(string memberName, object memberValue, MemberInfo memberInfo = null)
        {
            MemberName  = memberName;
            MemberInfo  = memberInfo;
            MemberValue = memberValue;
        }
    }
}
