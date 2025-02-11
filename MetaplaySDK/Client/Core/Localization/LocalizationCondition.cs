// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

namespace Metaplay.Core.Localization
{
    public class LocalizationsEnabledCondition : MetaplayFeatureEnabledConditionAttribute
    {
        public override bool IsEnabled => MetaplayCore.Options.FeatureFlags.EnableLocalizations;
    }
}
