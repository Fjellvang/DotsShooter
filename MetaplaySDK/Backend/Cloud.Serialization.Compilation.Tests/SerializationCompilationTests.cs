// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Application;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Model;
using Metaplay.Core.Serialization;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace Cloud.Serialization.Compilation.Tests
{
    [TestFixture(true)]
    [TestFixture(false)]
    [NonParallelizable]
    public class SerializationCompilationTests
    {
        string            _appDomainDir;
        bool              useTrampolines;

        public SerializationCompilationTests(bool useTrampolines)
        {
            this.useTrampolines = useTrampolines;
        }

        [OneTimeSetUp]
        public void SetUp()
        {
            // \note Using the AppDomain base directory as that returns the directory where the build outputs
            //       reside in `dotnet test`, Visual Studio, and Rider (unlike `Assembly.GetEntryAssembly()).
            //       It is also more stable than `GetCurrentDirectory()` as the working directory can change.
            _appDomainDir = AppDomain.CurrentDomain.BaseDirectory;
        }

        void InitializeAndCompile(List<Type> typesToSerialize)
        {
            MetaSerializerTypeInfo typeInfo = new TestSerializerTypeScanner(typesToSerialize).GetTypeInfo();
            RoslynSerializerCompileCache.EnsureDllUpToDate(
                outputDir: _appDomainDir,
                dllFileName: "Metaplay.Generated.Test.dll",
                errorDir: Path.Join(_appDomainDir, "Errors"),
                enableCaching: true,
                forceRoslyn: false,
                useMemberAccessTrampolines: useTrampolines,
                generateRuntimeTypeInfo: false,
                types: typeInfo);
        }

        [MetaSerializable]
        public class MetaRefInDictionary
        {
            [MetaMember(1)]
            public Dictionary<MetaRef<TestData>, TestData> Dictionary { get; set; }
        }
        [MetaSerializable]
        public class MetaRefInReadOnlyDictionary
        {
            [MetaMember(1)]
            public ReadOnlyDictionary<MetaRef<TestData>, TestData> Dictionary { get; set; }
        }

        [MetaSerializable]
        public class MetaRefInMetaDictionary
        {
            [MetaMember(1)]
            public MetaDictionary<MetaRef<TestData>, TestData> Dictionary { get; set; }
        }

        [MetaSerializable]
        public class MetaRefInHashSet
        {
            [MetaMember(1)]
            public HashSet<MetaRef<TestData>> Set { get; set; }
        }
        [MetaSerializable]
        public class MetaRefInOrderedSet
        {
            [MetaMember(1)]
            public OrderedSet<MetaRef<TestData>> Set { get; set; }
        }

        [MetaSerializable(MetaSerializableFlags.AutomaticConstructorDetection)]
        public class PrivateDeserializationConstructor
        {
            [MetaMember(1)] public string Test { get; set; }

            PrivateDeserializationConstructor(string test) { }
        }

        [MetaSerializable(MetaSerializableFlags.AutomaticConstructorDetection)]
        public class PartialDeserializationConstructorWithExtraneousReadOnlyProperties
        {
            [MetaMember(1)] public string Test { get; }
            [MetaMember(2)] public string Test2 { get; }

            public PartialDeserializationConstructorWithExtraneousReadOnlyProperties(string test) { }
        }

        [MetaSerializable(MetaSerializableFlags.AutomaticConstructorDetection)]
        public class MismatchingTypesDeserializationConstructor
        {
            [MetaMember(1)] public int Test { get; set; }

            public MismatchingTypesDeserializationConstructor(string test) { }
        }

        [MetaSerializable()]
        public class NoParameterlessConstructor
        {
            [MetaMember(1)] public int Test { get; set; }

            public NoParameterlessConstructor(int test)
            {
                Test = test;
            }
        }

        [MetaSerializable]
        public class IncorrectAttributeDeserializationConstructor
        {
            [MetaMember(1)] public int Test { get; set; }

            [MetaDeserializationConstructor]
            public IncorrectAttributeDeserializationConstructor(string test) { }
        }

        [MetaSerializable]
        public class InvalidTypeOptionalParameterDeserializationConstructor
        {
            [MetaMember(1)] public int Test { get; set; }

            [MetaDeserializationConstructor]
            public InvalidTypeOptionalParameterDeserializationConstructor(string test = "defaultTest") { }
        }

        /// <summary>
        /// Similar to <see cref="InvalidTypeOptionalParameterDeserializationConstructor"/>
        /// but the invalid-typed parameter is "compatible" in the sense that the member type
        /// is assignable to the parameter type.
        /// </summary>
        [MetaSerializable]
        public class InvalidButCompatibleOptionalParameterDeserializationConstructor
        {
            [MetaMember(1)] public int Test { get; set; }

            [MetaDeserializationConstructor]
            public InvalidButCompatibleOptionalParameterDeserializationConstructor(long test = 123) { }
        }

        [MetaSerializable]
        public class MembersWithoutMatchingParametersInvolvingOptionalsDeserializationConstructor
        {
            [MetaMember(1)] public int Test { get; set; }

            [MetaDeserializationConstructor]
            public MembersWithoutMatchingParametersInvolvingOptionalsDeserializationConstructor(string mismatchingName = "defaultTest") { }
        }

        [MetaSerializable]
        public class ExtraneousParameterInDeserializationConstructor
        {
            [MetaMember(1)] public int Test { get; set; }

            [MetaDeserializationConstructor]
            public ExtraneousParameterInDeserializationConstructor(int test, string test2) { }
        }

        [MetaSerializable]
        public class DuplicateParameterAttributeDeserializationConstructor
        {
            [MetaMember(1)] public int Test { get; set; }
            [MetaMember(2)] public int test { get; set; }

            [MetaDeserializationConstructor]
            public DuplicateParameterAttributeDeserializationConstructor(int test, int Test) { }
        }

        [MetaSerializable]
        public class DuplicateCleanedParameterAttributeDeserializationConstructor
        {
            [MetaMember(1)] public int Test { get; set; }
            [MetaMember(2)] public int test { get; set; }

            [MetaDeserializationConstructor]
            public DuplicateCleanedParameterAttributeDeserializationConstructor(int test, int _test) { }
        }

        [MetaSerializable]
        public class DuplicateOptionalParameterAttributeDeserializationConstructor
        {
            [MetaMember(1)] public int Test { get; set; }
            [MetaMember(2)] public int test { get; set; }

            [MetaDeserializationConstructor]
            public DuplicateOptionalParameterAttributeDeserializationConstructor(int test = 1, int Test = 1) { }
        }

        [MetaSerializable]
        public class ConstructorHasMultipleMatchingMetaMembers
        {
            [MetaMember(1)] public int Test  { get; set; }
            [MetaMember(2)] public int _Test { get; set; }

            [MetaDeserializationConstructor]
            public ConstructorHasMultipleMatchingMetaMembers(int test) { }
        }

        [MetaSerializable]
        public class MultipleConstructorsWithAttributeDeserializationConstructor
        {
            [MetaMember(1)] public int    Test  { get; set; }
            [MetaMember(2)] public string Test2 { get; set; }

            [MetaDeserializationConstructor]
            public MultipleConstructorsWithAttributeDeserializationConstructor(int test, string test2) { }

            [MetaDeserializationConstructor]
            public MultipleConstructorsWithAttributeDeserializationConstructor(string test2, int test) { }
        }

        [MetaSerializable(MetaSerializableFlags.AutomaticConstructorDetection)]
        public class MultipleConstructorsDeserializationConstructor
        {
            [MetaMember(1)] public int    Test  { get; set; }
            [MetaMember(2)] public string Test2 { get; set; }

            public MultipleConstructorsDeserializationConstructor(int test, string test2) { }

            public MultipleConstructorsDeserializationConstructor(string test2, int test) { }
        }

        [MetaSerializable]
        public class ConstructorDeserializationVariableNameConflict
        {
            [MetaMember(1)] public string member          { get; }
            [MetaMember(2)] public string second          { get; set; }
            [MetaMember(3)] public string ASSIGNED_second { get; set; }

            [MetaDeserializationConstructor]
            public ConstructorDeserializationVariableNameConflict(string member)
            {
                this.member = member;
            }
        }

        [MetaSerializable]
        public class TestId : StringId<TestId>
        {

        }

        [MetaSerializable]
        public class TestData : IGameConfigData<TestId>
        {
            [MetaMember(1)]
            public TestId ConfigKey { get; }

            [MetaDeserializationConstructor]
            public TestData(TestId configKey)
            {
                ConfigKey = configKey;
            }
        }

        [MetaSerializable]
        public class ConstructorDeserializationMetaRef
        {
            [MetaMember(1)] public MetaRef<TestData> member { get; private set; }

            [MetaDeserializationConstructor]
            public ConstructorDeserializationMetaRef(MetaRef<TestData> member)
            {
                this.member = member;
            }
        }

        [MetaSerializable]
        public class ConstructorDeserializationMetaRefWithoutSetter
        {
            [MetaMember(1)] public MetaRef<TestData> member { get; }

            [MetaDeserializationConstructor]
            public ConstructorDeserializationMetaRefWithoutSetter(MetaRef<TestData> member)
            {
                this.member = member;
            }
        }

        [Test]
        public void ConstructorHasMultipleMatchingMetaMembersThrows()
        {
            Assert.That(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(ConstructorHasMultipleMatchingMetaMembers)};
                    InitializeAndCompile(typesToSerialize);
                },
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo($"Constructor parameter in '{typeof(ConstructorHasMultipleMatchingMetaMembers).FullName}' matches multiple members (Test, _Test) after sanitization, this is not supported for constructor based deserialization."));
        }

        [Test]
        public void MetaRefInReadOnlyDictionaryThrows()
        {
            Assert.That(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(MetaRefInReadOnlyDictionary)};
                    InitializeAndCompile(typesToSerialize);
                },
                Throws.TypeOf<MetaSerializerTypeScanner.TypeRegistrationError>().With.Message.EqualTo($"Failed to process System.Collections.ObjectModel.ReadOnlyDictionary<Metaplay.Core.MetaRef<Cloud.Serialization.Compilation.Tests.SerializationCompilationTests.TestData>,Cloud.Serialization.Compilation.Tests.SerializationCompilationTests.TestData>. Required by Cloud.Serialization.Compilation.Tests.SerializationCompilationTests.MetaRefInReadOnlyDictionary.").With.InnerException.Message.EqualTo("MetaRef's are not supported as keys for dictionary types."));
        }

        [Test]
        public void MetaRefInDictionaryThrows()
        {
            Assert.That(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(MetaRefInDictionary)};
                    InitializeAndCompile(typesToSerialize);
                },
                Throws.TypeOf<MetaSerializerTypeScanner.TypeRegistrationError>().With.Message.EqualTo($"Failed to process System.Collections.Generic.Dictionary<Metaplay.Core.MetaRef<Cloud.Serialization.Compilation.Tests.SerializationCompilationTests.TestData>,Cloud.Serialization.Compilation.Tests.SerializationCompilationTests.TestData>. Required by Cloud.Serialization.Compilation.Tests.SerializationCompilationTests.MetaRefInDictionary.").With.InnerException.Message.EqualTo("MetaRef's are not supported as keys for dictionary types."));
        }

        [Test]
        public void MetaRefInMetaDictionaryThrows()
        {
            Assert.That(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(MetaRefInMetaDictionary)};
                    InitializeAndCompile(typesToSerialize);
                },
                Throws.TypeOf<MetaSerializerTypeScanner.TypeRegistrationError>().With.Message.EqualTo($"Failed to process Metaplay.Core.MetaDictionary<Metaplay.Core.MetaRef<Cloud.Serialization.Compilation.Tests.SerializationCompilationTests.TestData>,Cloud.Serialization.Compilation.Tests.SerializationCompilationTests.TestData>. Required by Cloud.Serialization.Compilation.Tests.SerializationCompilationTests.MetaRefInMetaDictionary.").With.InnerException.Message.EqualTo("MetaRef's are not supported as keys for dictionary types."));
        }
        [Test]
        public void MetaRefInHashSetThrows()
        {
            Assert.That(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(MetaRefInHashSet)};
                    InitializeAndCompile(typesToSerialize);
                },
                Throws.TypeOf<MetaSerializerTypeScanner.TypeRegistrationError>().With.Message.EqualTo($"Failed to process System.Collections.Generic.HashSet<Metaplay.Core.MetaRef<Cloud.Serialization.Compilation.Tests.SerializationCompilationTests.TestData>>. Required by Cloud.Serialization.Compilation.Tests.SerializationCompilationTests.MetaRefInHashSet.").With.InnerException.Message.EqualTo("MetaRef's are not supported as items in hash based collections."));
        }

        [Test]
        public void MetaRefInOrderedSetThrows()
        {
            Assert.That(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(MetaRefInOrderedSet)};
                    InitializeAndCompile(typesToSerialize);
                },
                Throws.TypeOf<MetaSerializerTypeScanner.TypeRegistrationError>().With.Message.EqualTo($"Failed to process Metaplay.Core.OrderedSet<Metaplay.Core.MetaRef<Cloud.Serialization.Compilation.Tests.SerializationCompilationTests.TestData>>. Required by Cloud.Serialization.Compilation.Tests.SerializationCompilationTests.MetaRefInOrderedSet.").With.InnerException.Message.EqualTo("MetaRef's are not supported as items in hash based collections."));
        }

        [Test]
        public void PrivateConstructorDoesNotThrow()
        {
            Assert.DoesNotThrow(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(PrivateDeserializationConstructor)};
                    InitializeAndCompile(typesToSerialize);
                });
        }

        [Test]
        public void PartialDeserializationConstructorWithExtraneousReadOnlyPropertiesThrows()
        {
            Assert.That(() =>
            {
                List<Type> typesToSerialize = new List<Type>() {typeof(PartialDeserializationConstructorWithExtraneousReadOnlyProperties)};
                InitializeAndCompile(typesToSerialize);
            },
            Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo($"'{typeof(PartialDeserializationConstructorWithExtraneousReadOnlyProperties).FullName}' uses constructor deserialization but has read-only members (Test2) which are not populated by the best matching constructor, please make them writeable or add them to the deserialization constructor."));
        }

        [Test]
        public void TestMetaRefDoesNotThrow()
        {
            Assert.DoesNotThrow(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(ConstructorDeserializationMetaRef), typeof(TestData), typeof(TestId)};
                    InitializeAndCompile(typesToSerialize);
                });
        }

        [Test]
        public void TestNoParameterlessConstructor()
        {
            Assert.That(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(NoParameterlessConstructor)};
                    InitializeAndCompile(typesToSerialize);
                },
                Throws.TypeOf<MetaSerializerTypeScanner.TypeRegistrationError>().With.Message.EqualTo($"Failed to process {typeof(NoParameterlessConstructor).ToNamespaceQualifiedTypeString()}.").With.InnerException.Message.EqualTo($"No parameterless ctor found for {typeof(NoParameterlessConstructor).ToGenericTypeString()}."));
        }

        [Test]
        public void TestVariableNameConflictDoesNotThrow()
        {
            Assert.DoesNotThrow(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(ConstructorDeserializationVariableNameConflict)};
                    InitializeAndCompile(typesToSerialize);
                });
        }

        [Test]
        public void TestMismatchedConstructorParams()
        {
            Assert.That(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(MismatchingTypesDeserializationConstructor)};
                    InitializeAndCompile(typesToSerialize);
                },
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo($"Could not find matching constructor for type '{typeof(MismatchingTypesDeserializationConstructor).FullName}'. Constructor parameters must have the same name and type as the MetaMembers defined in this type."));
        }

        [Test]
        public void TestConstructorAttributeHasInvalidSignature()
        {
            Assert.That(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(IncorrectAttributeDeserializationConstructor)};
                    InitializeAndCompile(typesToSerialize);
                },
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo($"Constructor in '{typeof(IncorrectAttributeDeserializationConstructor).FullName}' with attribute {nameof(MetaDeserializationConstructorAttribute)} does not have a valid signature. Constructor parameters must have the same name and type as the MetaMembers defined in this type."));
        }

        [Test]
        public void TestInvalidTypeOptionalParameterDeserializationConstructor()
        {
            Assert.That(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(InvalidTypeOptionalParameterDeserializationConstructor)};
                    InitializeAndCompile(typesToSerialize);
                },
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo($"Constructor in '{typeof(InvalidTypeOptionalParameterDeserializationConstructor).FullName}' with attribute {nameof(MetaDeserializationConstructorAttribute)} does not have a valid signature. Constructor parameters must have the same name and type as the MetaMembers defined in this type."));
        }

        [Test]
        public void TestInvalidButCompatibleOptionalParameterDeserializationConstructor()
        {
            Assert.That(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(InvalidButCompatibleOptionalParameterDeserializationConstructor)};
                    InitializeAndCompile(typesToSerialize);
                },
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo($"Constructor in '{typeof(InvalidButCompatibleOptionalParameterDeserializationConstructor).FullName}' with attribute {nameof(MetaDeserializationConstructorAttribute)} does not have a valid signature. Constructor parameters must have the same name and type as the MetaMembers defined in this type."));
        }

        [Test]
        public void TestMembersWithoutMatchingParametersInvolvingOptionalsDeserializationConstructor()
        {
            Assert.That(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(MembersWithoutMatchingParametersInvolvingOptionalsDeserializationConstructor)};
                    InitializeAndCompile(typesToSerialize);
                },
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo($"Constructor in '{typeof(MembersWithoutMatchingParametersInvolvingOptionalsDeserializationConstructor).FullName}' with attribute {nameof(MetaDeserializationConstructorAttribute)} does not have a valid signature. Constructor parameters must have the same name and type as the MetaMembers defined in this type."));
        }

        [Test]
        public void TestExtraneousParameterInDeserializationConstructor()
        {
            Assert.That(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(ExtraneousParameterInDeserializationConstructor)};
                    InitializeAndCompile(typesToSerialize);
                },
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo($"Constructor in '{typeof(ExtraneousParameterInDeserializationConstructor).FullName}' with attribute {nameof(MetaDeserializationConstructorAttribute)} does not have a valid signature. Constructor parameters must have the same name and type as the MetaMembers defined in this type."));
        }

        [Test]
        public void TestConstructorAttributeHasDuplicateNames()
        {
            Assert.That(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(DuplicateParameterAttributeDeserializationConstructor)};
                    InitializeAndCompile(typesToSerialize);
                },
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo($"Constructor in '{typeof(DuplicateParameterAttributeDeserializationConstructor).FullName}' has duplicate parameters (test) after sanitization, this is not supported for constructor based deserialization."));
        }

        [Test]
        public void TestConstructorAttributeHasDuplicateCleanedNames()
        {
            Assert.That(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(DuplicateCleanedParameterAttributeDeserializationConstructor)};
                    InitializeAndCompile(typesToSerialize);
                },
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo($"Constructor in '{typeof(DuplicateCleanedParameterAttributeDeserializationConstructor).FullName}' has duplicate parameters (test) after sanitization, this is not supported for constructor based deserialization."));
        }

        [Test]
        public void TestConstructorAttributeHasDuplicateOptionalParameters()
        {
            Assert.That(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(DuplicateOptionalParameterAttributeDeserializationConstructor)};
                    InitializeAndCompile(typesToSerialize);
                },
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo($"Constructor in '{typeof(DuplicateOptionalParameterAttributeDeserializationConstructor).FullName}' has duplicate parameters (test) after sanitization, this is not supported for constructor based deserialization."));
        }

        [Test]
        public void TestMultipleConstructorsWithAttributeDeserializationConstructor()
        {
            Assert.That(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(MultipleConstructorsWithAttributeDeserializationConstructor)};
                    InitializeAndCompile(typesToSerialize);
                },
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo($"Type '{typeof(MultipleConstructorsWithAttributeDeserializationConstructor).FullName} has multiple constructors with the {nameof(MetaDeserializationConstructorAttribute)} attribute, this is not valid. Please ensure there is only one {nameof(MetaDeserializationConstructorAttribute)} attribute."));
        }

        [Test]
        public void TestMultipleConstructorsDeserializationConstructor()
        {
            Assert.That(
                () =>
                {
                    List<Type> typesToSerialize = new List<Type>() {typeof(MultipleConstructorsDeserializationConstructor)};
                    InitializeAndCompile(typesToSerialize);
                },
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo($"Multiple valid deserialization constructors found for '{typeof(MultipleConstructorsDeserializationConstructor).FullName}', please specify the correct constructor using [{nameof(MetaDeserializationConstructorAttribute)}]."));
        }
    }
}
