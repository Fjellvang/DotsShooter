// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

namespace Metaplay.Core
{
    /// <summary>
    /// Environment family is used to configure environment-dependent default values for various Runtime Options.
    /// There can be multiple individual environments in each family (eg, dev-0, dev-1 can belong to family Development).
    /// </summary>
    public enum EnvironmentFamily
    {
        Local,
        Development,
        Staging,
        Production
    }
}
