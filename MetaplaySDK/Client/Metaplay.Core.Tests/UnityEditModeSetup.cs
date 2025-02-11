// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

#if UNITY_EDITOR

using Metaplay.Core;
using Metaplay.Unity;
using NUnit.Framework;

// Tests is this assembly are only included if UnityEditModeTestsEnabledCondition is set.
[assembly: UnityEditModeTestsEnabledCondition]

// \note In global namespace to make sure it covers all tests
[SetUpFixture]
public class UnityEditModeSetup
{
    IMetaplayServiceProvider _oldProvider;

    [OneTimeSetUp]
    public void Setup()
    {
        UnityEditModeTestsEnabledCondition.IsInTestMode = true;

        _oldProvider = MetaplayCore.Reinitialize(
            initializers =>
                initializers.ConfigureMetaSerialization("UnityEditModeTest", MetaplaySDK.UnityTempDirectory, useMemberAccessTrampolines: true));
    }

    [OneTimeTearDown]
    public void Teardown()
    {
        UnityEditModeTestsEnabledCondition.IsInTestMode = false;
        MetaplayServices.SetServiceProvider(_oldProvider);
    }
}

class UnityEditModeTestsEnabledCondition : MetaplayFeatureEnabledConditionAttribute
{
    public static bool IsInTestMode = false;
    public override bool IsEnabled => IsInTestMode;
}

#endif
