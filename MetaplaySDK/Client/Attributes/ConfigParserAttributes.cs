// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;

namespace Metaplay.Core
{
    /// <summary>
    /// When a base class has this attribute, it uses the
    /// custom config parser (if any) defined for a subclass.
    /// </summary>
    public class UseCustomParserFromDerivedAttribute : Attribute
    {
    }
}
