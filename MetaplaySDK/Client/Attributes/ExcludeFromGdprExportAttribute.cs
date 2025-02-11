// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;

namespace Metaplay.Core.Model
{
    /// <summary>
    /// Declares a field to be excluded from potential GDPR data requests. This attribute
    /// may be used to hide fields that are sensitive but do not contain player's personal
    /// information, such as Random generator states. Additionally, the attribute can be
    /// used to prune irrelevant fields, such as many Transient fields.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class ExcludeFromGdprExportAttribute : Attribute
    {
    }
}
