// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Metaplay.Core.Tests
{
    class TypeScannerTests
    {
#pragma warning disable IDE1006 // Naming Styles
        public class CaseA_BaseClass
        {
        }
        public class CaseA_SubClass : CaseA_BaseClass
        {
        }

        public abstract class CaseB_BaseClass
        {
        }
        public class CaseB_SubClass : CaseB_BaseClass
        {
        }

        public interface CaseC_BaseInterface
        {
        }
        public interface CaseC_SubInterface : CaseC_BaseInterface
        {
        }
        public class CaseC_Implementation : CaseC_SubInterface
        {
        }
        public abstract class CaseC_AbstractImplementation : CaseC_SubInterface
        {
        }
#pragma warning restore IDE1006 // Naming Styles

        [Test]
        public void TestGetDerivedTypes()
        {
            AssertCompat.AreEqual(new HashSet<Type> { typeof(CaseA_SubClass) }, TypeScanner.GetDerivedTypes<CaseA_BaseClass>().ToHashSet());
            AssertCompat.AreEqual(new HashSet<Type> { typeof(CaseB_SubClass) }, TypeScanner.GetDerivedTypes<CaseB_BaseClass>().ToHashSet());
            AssertCompat.AreEqual(new HashSet<Type> { }, TypeScanner.GetDerivedTypes<CaseC_BaseInterface>().ToHashSet());
            AssertCompat.AreEqual(new HashSet<Type> { }, TypeScanner.GetDerivedTypes<CaseC_SubInterface>().ToHashSet());
        }

        [Test]
        public void TestGetDerivedTypesAndSelf()
        {
            AssertCompat.AreEqual(new HashSet<Type> { typeof(CaseA_BaseClass), typeof(CaseA_SubClass) }, TypeScanner.GetDerivedTypesAndSelf<CaseA_BaseClass>().ToHashSet());
            AssertCompat.AreEqual(new HashSet<Type> { typeof(CaseB_BaseClass), typeof(CaseB_SubClass) }, TypeScanner.GetDerivedTypesAndSelf<CaseB_BaseClass>().ToHashSet());
            AssertCompat.AreEqual(new HashSet<Type> { typeof(CaseC_BaseInterface) }, TypeScanner.GetDerivedTypesAndSelf<CaseC_BaseInterface>().ToHashSet());
            AssertCompat.AreEqual(new HashSet<Type> { typeof(CaseC_SubInterface) }, TypeScanner.GetDerivedTypesAndSelf<CaseC_SubInterface>().ToHashSet());
        }

        [Test]
        public void TestGetConcreteDerivedTypesGeneric()
        {
            AssertCompat.AreEqual(new HashSet<Type> { typeof(CaseA_SubClass) }, TypeScanner.GetConcreteDerivedTypes<CaseA_BaseClass>().ToHashSet());
            AssertCompat.AreEqual(new HashSet<Type> { typeof(CaseB_SubClass) }, TypeScanner.GetConcreteDerivedTypes<CaseB_BaseClass>().ToHashSet());
            AssertCompat.AreEqual(new HashSet<Type> { }, TypeScanner.GetConcreteDerivedTypes<CaseC_BaseInterface>().ToHashSet());
            AssertCompat.AreEqual(new HashSet<Type> { }, TypeScanner.GetConcreteDerivedTypes<CaseC_SubInterface>().ToHashSet());
        }

        [Test]
        public void TestGetConcreteDerivedTypesParam()
        {
            AssertCompat.AreEqual(new HashSet<Type> { typeof(CaseA_SubClass) }, TypeScanner.GetConcreteDerivedTypes(typeof(CaseA_BaseClass)).ToHashSet());
            AssertCompat.AreEqual(new HashSet<Type> { typeof(CaseB_SubClass) }, TypeScanner.GetConcreteDerivedTypes(typeof(CaseB_BaseClass)).ToHashSet());
            AssertCompat.AreEqual(new HashSet<Type> { }, TypeScanner.GetConcreteDerivedTypes(typeof(CaseC_BaseInterface)).ToHashSet());
            AssertCompat.AreEqual(new HashSet<Type> { }, TypeScanner.GetConcreteDerivedTypes(typeof(CaseC_SubInterface)).ToHashSet());
        }

        [Test]
        public void TestGetInterfaceImplementations()
        {
            AssertCompat.AreEqual(new HashSet<Type> { typeof(CaseC_Implementation), typeof(CaseC_AbstractImplementation) }, TypeScanner.GetInterfaceImplementations<CaseC_BaseInterface>().ToHashSet());
            AssertCompat.AreEqual(new HashSet<Type> { typeof(CaseC_Implementation), typeof(CaseC_AbstractImplementation) }, TypeScanner.GetInterfaceImplementations<CaseC_SubInterface>().ToHashSet());
        }

        [AttributeUsage(AttributeTargets.Class)]
        class TestAttribute1 : Attribute { }
        [TestAttribute1]
        class AttrCaseAClass { }

        [AttributeUsage(AttributeTargets.Class, Inherited = false)]
        class TestAttribute2 : Attribute { }
        [TestAttribute2]
        abstract class AttrCaseBBaseClass { }
        class AttrCaseBSubClass : AttrCaseBBaseClass { }

        [AttributeUsage(AttributeTargets.Class, Inherited = true)]
        class TestAttribute3 : Attribute { }
        [TestAttribute3]
        abstract class AttrCaseCBaseClass { }
        class AttrCaseCSubClass : AttrCaseCBaseClass { }

        [Test]
        public void TestGetClassesWithAttribute()
        {
            AssertCompat.AreEqual(new HashSet<Type> { typeof(AttrCaseAClass) }, TypeScanner.GetClassesWithAttribute<TestAttribute1>().ToHashSet());
            AssertCompat.AreEqual(new HashSet<Type> { typeof(AttrCaseBBaseClass) }, TypeScanner.GetClassesWithAttribute<TestAttribute2>().ToHashSet());
            AssertCompat.AreEqual(new HashSet<Type> { typeof(AttrCaseCBaseClass), typeof(AttrCaseCSubClass) }, TypeScanner.GetClassesWithAttribute<TestAttribute3>().ToHashSet());
        }

        [AttributeUsage(AttributeTargets.Class)]
        class TestAttribute4 : Attribute { }
        [TestAttribute4]
        abstract class AttrCaseDAbstractBaseClass { }
        class AttrCaseDSubClass : AttrCaseDAbstractBaseClass { }
        [TestAttribute4]
        class AttrCaseEAbstractBaseClass { }
        abstract class AttrCaseESubClass : AttrCaseEAbstractBaseClass { }

        [Test]
        public void TestGetConcreteClassesWithAttribute()
        {
            AssertCompat.AreEqual(new HashSet<Type> { typeof(AttrCaseDSubClass), typeof(AttrCaseEAbstractBaseClass) }, TypeScanner.GetConcreteClassesWithAttribute<TestAttribute4>().ToHashSet());
        }
    }
}
