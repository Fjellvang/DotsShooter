// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using NUnit.Framework;
using System.Collections.Generic;

namespace Cloud.Tests
{
    class EnvironmentRuntimeOptionsTests
    {
        [Test]
        public void Empty()
        {
            List<(string, string)> aliases = new List<(string, string)>();
            Assert.IsTrue(EnvironmentRuntimeOptionsSource.ParseFromEnvironment("Metaplay_", aliases, new MetaDictionary<string, string>() { }).Definitions.Count == 0);
        }

        [TestCase("Metaplay_Ip")]
        [TestCase("METAPLAY_EXTRA_OPTIONS")]
        [TestCase("METAPLAY_clienT_SVC_12")]
        [TestCase("something__else")]
        public void Ignored(string key)
        {
            List<(string, string)> aliases = new List<(string, string)>();
            Assert.IsTrue(EnvironmentRuntimeOptionsSource.ParseFromEnvironment("Metaplay_", aliases, new MetaDictionary<string, string>() { { key, "value" } }).Definitions.Count == 0);
        }

        [TestCase("Metaplay_Foo__Bar", "Foo:Bar")]
        [TestCase("Metaplay_Bar__Foo__Extra", "Bar:Foo:Extra")]
        public void Value(string key, string config)
        {
            List<(string, string)> aliases = new List<(string, string)>();
            IReadOnlyDictionary<string, string> definitions = EnvironmentRuntimeOptionsSource.ParseFromEnvironment("Metaplay_", aliases, new MetaDictionary<string, string>() { { key, "value" }  }).Definitions;
            Assert.IsTrue(definitions.Count == 1);
            Assert.IsTrue(definitions[config] == "value");
        }

        [TestCase("CustomSource")]
        [TestCase("customSOURCE")]
        public void Alias(string envVariableName)
        {
            List<(string, string)> aliases = new List<(string, string)>()
            {
                ("CustomSource", "Foo:Bar")
            };
            MetaDictionary<string, string> env = new MetaDictionary<string, string>()
            {
                { envVariableName, "value" }
            };

            IReadOnlyDictionary<string, string> definitions = EnvironmentRuntimeOptionsSource.ParseFromEnvironment("Metaplay_", aliases, env).Definitions;
            Assert.IsTrue(definitions.Count == 1);
            Assert.IsTrue(definitions["Foo:Bar"] == "value");
        }

        [Test]
        public void AliasConflict()
        {
            List<(string, string)> aliases = new List<(string, string)>()
            {
                ("MyAlias", "Foo:Bar")
            };
            MetaDictionary<string, string> env = new MetaDictionary<string, string>()
            {
                { "MyAlias", "value_A" },
                { "Metaplay_Foo__Bar", "value_B" },
            };

            Assert.Throws<EnvironmentRuntimeOptionsSource.AmbiguousEnvironmentError>(() => EnvironmentRuntimeOptionsSource.ParseFromEnvironment("Metaplay_", aliases, env));
        }
    }
}
