// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using NUnit.Framework;
using System;

namespace Metaplay.Core.Tests
{
    namespace ValidUsage
    {
        public class SingletonApi : IMetaIntegrationSingleton<SingletonApi>
        {
        }

        public class SingletonIntegration : SingletonApi
        {
        }

        public abstract class AbstractAPI : IMetaIntegrationSingleton<AbstractAPI>
        {
        }
        public class DefaultAbstractAPI : AbstractAPI
        {
        }
        public class OverridingAbstractAPI : DefaultAbstractAPI
        {
        }

        public abstract class AbstractAPI2 : IMetaIntegrationSingleton<AbstractAPI2>
        {
        }
        public abstract class AbstractHelperForAbstractAPI2 : AbstractAPI2
        {
        }
        public class OverridingAPI2 : AbstractHelperForAbstractAPI2
        {
        }

        public class PrivateConstructor : IMetaIntegrationConstructible<PrivateConstructor>
        {
            private PrivateConstructor() {}
        }
    }

    namespace UnknownTypes
    {
        public class ConstructibleApi : IMetaIntegrationConstructible<ConstructibleApi>
        {
        }

        public class ConstructibleIntegration : ConstructibleApi
        {
        }
    }

    [TestFixture]
    public class IntegrationRegistryTests
    {
        [Test]
        public void SingletonUsage()
        {
            IIntegrationRegistry registry = new IntegrationRegistry(type => type.Namespace == "Metaplay.Core.Tests.ValidUsage");
            Assert.AreEqual(registry.Get<ValidUsage.SingletonApi>().GetType(), typeof(ValidUsage.SingletonIntegration));
        }

        [Test]
        public void PrivateConstructor()
        {
            IIntegrationRegistry registry = new IntegrationRegistry(type => type.Namespace == "Metaplay.Core.Tests.ValidUsage");
            Assert.AreEqual(registry.Create<ValidUsage.PrivateConstructor>().GetType(), typeof(ValidUsage.PrivateConstructor));
        }

        [Test]
        public void UnknownTypes()
        {
            IIntegrationRegistry registry = new IntegrationRegistry(type => type.Namespace == "Metaplay.Core.Tests.ValidUsage");
            Assert.Throws<InvalidOperationException>(() => registry.Create<UnknownTypes.ConstructibleApi>());
        }

        [Test]
        public void IsMetaIntegrationType()
        {
            Assert.AreEqual(true, IntegrationRegistry.IsMetaIntegrationType(typeof(ValidUsage.SingletonApi)));
            Assert.AreEqual(false, IntegrationRegistry.IsMetaIntegrationType(typeof(ValidUsage.SingletonIntegration)));

            Assert.AreEqual(true, IntegrationRegistry.IsMetaIntegrationType(typeof(ValidUsage.AbstractAPI)));
            Assert.AreEqual(false, IntegrationRegistry.IsMetaIntegrationType(typeof(ValidUsage.DefaultAbstractAPI)));
            Assert.AreEqual(false, IntegrationRegistry.IsMetaIntegrationType(typeof(ValidUsage.OverridingAbstractAPI)));

            Assert.AreEqual(true, IntegrationRegistry.IsMetaIntegrationType(typeof(ValidUsage.AbstractAPI2)));
            Assert.AreEqual(false, IntegrationRegistry.IsMetaIntegrationType(typeof(ValidUsage.AbstractHelperForAbstractAPI2)));
            Assert.AreEqual(false, IntegrationRegistry.IsMetaIntegrationType(typeof(ValidUsage.OverridingAPI2)));
        }
    }
}
