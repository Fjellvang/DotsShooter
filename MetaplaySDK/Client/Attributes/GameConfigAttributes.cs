// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Linq;

namespace Metaplay.Core.Config
{
    // Declare an entry in a GameConfig
    [AttributeUsage(AttributeTargets.Property)]
    public class GameConfigEntryAttribute : Attribute
    {
        public readonly string EntryName;
        public readonly bool   RequireArchiveEntry;
        // Specifies the name of the `GameConfigBuildParameters` member to be used for fetching the source data
        // of the game config build of this entry.
        public readonly string ConfigBuildSource;

        public GameConfigEntryAttribute(string entryName, bool requireArchiveEntry = true, string configBuildSource = null)
        {
            EntryName = entryName ?? throw new ArgumentNullException(nameof(entryName));
            RequireArchiveEntry = requireArchiveEntry;
            ConfigBuildSource   = configBuildSource;
        }
    }

    /// <summary>
    /// Declare a GameConfig build transformation source item type. The source item type must implement interface
    /// IGameConfigSourceItem{TGameConfigKey, TGameConfigData} where TGameConfigData is the type of the GameConfigData that this
    /// attribute is assigned to.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class GameConfigEntryTransformAttribute : Attribute
    {
        public readonly Type SourceItemType;

        public GameConfigEntryTransformAttribute(Type sourceItemType)
        {
            SourceItemType = sourceItemType ?? throw new ArgumentNullException(nameof(sourceItemType));
        }
    }

    /// <summary>
    /// Defines the abstract item should be parsed as the given concrete subtype.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class ParseAsDerivedTypeAttribute : Attribute
    {
        public readonly Type Type;

        public ParseAsDerivedTypeAttribute(Type type)
        {
            Type = type;
        }
    }

    /// <summary>
    /// Marks the constructor that is to be used during GameConfig build. The parameter names are mapped to the source data in case-insensitive manner.
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor)]
    public class MetaGameConfigBuildConstructorAttribute : Attribute
    {
        public MetaGameConfigBuildConstructorAttribute()
        {
        }
    }

    /// <summary>
    /// Attribute to allow converting the legacy game config legacy syntax to be understandable
    /// by the new game config pipeline. Use this to specify replacement rules for the header
    /// row cells. Eg, 'Type -> Type #key' converts any cell with payload 'Type' to 'Type #key'.
    /// All '[Array]' style syntax is also converted to 'Array[&lt;ndx&gt;]' with automatically
    /// computed indexes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class GameConfigSyntaxAdapterAttribute : Attribute
    {
        public struct ReplaceRule
        {
            public readonly string From;
            public readonly string To;

            public ReplaceRule(string from, string to)
            {
                From = from;
                To   = to;
            }
        }

        public readonly ReplaceRule[] HeaderReplaces;
        public readonly ReplaceRule[] HeaderPrefixReplaces;
        public readonly bool          EnsureHasKeyValueSheetHeader;

        public GameConfigSyntaxAdapterAttribute(string[] headerReplaces = null, string[] headerPrefixReplaces = null, bool ensureHasKeyValueSheetHeader = false)
        {
            HeaderReplaces               = ParseReplaceRules(headerReplaces);
            HeaderPrefixReplaces         = ParseReplaceRules(headerPrefixReplaces);
            EnsureHasKeyValueSheetHeader = ensureHasKeyValueSheetHeader;
        }

        static ReplaceRule[] ParseReplaceRules(string[] inputs)
        {
            if (inputs == null)
                return Array.Empty<ReplaceRule>();

            return inputs.Select(str =>
            {
                string[] parts = str.Split(" -> ");
                if (parts.Length != 2)
                    throw new ArgumentException("Header replace rule must be exactly in the format '<from> -> <to>' (must have space before and after the arrow!)");
                return new ReplaceRule(parts[0], parts[1]);
            }).ToArray();
        }
    }
}
