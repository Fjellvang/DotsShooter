// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.CodeAnalyzers.Shared;
using Metaplay.CodeAnalyzers.Tests.Verifiers;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;

namespace Metaplay.CodeAnalyzers.Tests
{
    public class DuplicateMetaMemberTests
    {
        [Test]
        public async Task TestNoDiagnostics()
        {
            var test = @"";

            await new MetaAnalyzerTest<DuplicateMetaMemberAnalyzer>()
            {
                TestState =
                {
                    Sources = { test },
                },
            }.RunAsync();
        }

        [Test]
        public async Task TestFullyQualifiedNamespace()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using Metaplay.Core.Model;

    namespace ConsoleApplication1
    {
        [Metaplay.Core.Model.MetaSerializable]
        class TestClass
        {
            const int MetaNumber = 1;

            [MetaMember(1)]
            int Test1 {get; set;}

            [MetaMember(1)]
            int Test2 {get; set;}
        }
    }";

            await new MetaAnalyzerTest<DuplicateMetaMemberAnalyzer>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(DuplicateMetaMemberAnalyzer.Rule).WithSpan(18, 17, 18, 22).WithArguments("1", "TestClass.Test2"),
                    new DiagnosticResult(DuplicateMetaMemberAnalyzer.Rule).WithSpan(21, 17, 21, 22).WithArguments("1", "TestClass.Test1")
                }
            }.RunAsync();
        }

        [Test]
        public async Task TestStruct()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using Metaplay.Core.Model;

    namespace ConsoleApplication1
    {
        [MetaSerializable]
        struct Test
        {
            const int MetaNumber = 1;

            [MetaMember(1)]
            int Test1 {get; set;}

            [MetaMember(1)]
            int Test2 {get; set;}
        }
    }";

            await new MetaAnalyzerTest<DuplicateMetaMemberAnalyzer>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(DuplicateMetaMemberAnalyzer.Rule).WithSpan(18, 17, 18, 22).WithArguments("1", "Test.Test2"),
                    new DiagnosticResult(DuplicateMetaMemberAnalyzer.Rule).WithSpan(21, 17, 21, 22).WithArguments("1", "Test.Test1")
                }
            }.RunAsync();
        }

        [Test]
        public async Task TestDuplicateLiteral()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using Metaplay.Core.Model;

    namespace ConsoleApplication1
    {
        [MetaSerializable]
        class TestClass
        {
            const int MetaNumber = 1;

            [MetaMember(1)]
            int Test1 {get; set;}

            [MetaMember(1)]
            int Test2 {get; set;}
        }
    }";

            await new MetaAnalyzerTest<DuplicateMetaMemberAnalyzer>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(DuplicateMetaMemberAnalyzer.Rule).WithSpan(18, 17, 18, 22).WithArguments("1", "TestClass.Test2"),
                    new DiagnosticResult(DuplicateMetaMemberAnalyzer.Rule).WithSpan(21, 17, 21, 22).WithArguments("1", "TestClass.Test1")
                }
            }.RunAsync();
        }

        [Test]
        public async Task TestDuplicateBaseClass()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using Metaplay.Core.Model;

    namespace ConsoleApplication1
    {
        [MetaSerializable]
        class TestClass
        {
            const int MetaNumber = 1;

            [MetaMember(1)]
            int Test1 {get; set;}
        }

        [MetaSerializableDerived(1)]
        class Derived : TestClass
        {
            [MetaMember(1)]
            int Test2 {get; set;}
        }
    }";

            await new MetaAnalyzerTest<DuplicateMetaMemberAnalyzer>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(DuplicateMetaMemberAnalyzer.Rule).WithSpan(18, 17, 18, 22).WithArguments("1", "Derived.Test2"),
                    new DiagnosticResult(DuplicateMetaMemberAnalyzer.Rule).WithSpan(25, 17, 25, 22).WithArguments("1", "TestClass.Test1")
                }
            }.RunAsync();
        }

        [Test]
        public async Task TestDuplicateEnum()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using Metaplay.Core.Model;

    namespace ConsoleApplication1
    {
        enum MetaType {
            a = 1, b = 2, c = 3
        }

        [MetaSerializable]
        class TestClass
        {
            const int MetaNumber = 1;

            [MetaMember(1)]
            int Test1 {get; set;}

            [MetaMember((int)MetaType.a)]
            int Test2 {get; set;}
        }
    }";

            await new MetaAnalyzerTest<DuplicateMetaMemberAnalyzer>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(DuplicateMetaMemberAnalyzer.Rule).WithSpan(22, 17, 22, 22).WithArguments("1", "TestClass.Test2"),
                    new DiagnosticResult(DuplicateMetaMemberAnalyzer.Rule).WithSpan(25, 17, 25, 22).WithArguments("1", "TestClass.Test1")
                }
            }.RunAsync();
        }

        [Test]
        public async Task TestDuplicateConstant()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using Metaplay.Core.Model;

    namespace ConsoleApplication1
    {
        [MetaSerializable]
        class TestClass
        {
            const int MetaNumber = 1;

            [MetaMember(1)]
            int Test2{get;set;}

            [MetaMember(MetaNumber)]
            int Test3 {get;set;}
        }
    }";

            await new MetaAnalyzerTest<DuplicateMetaMemberAnalyzer>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(DuplicateMetaMemberAnalyzer.Rule).WithSpan(18, 17, 18, 22).WithArguments("1", "TestClass.Test3"),
                    new DiagnosticResult(DuplicateMetaMemberAnalyzer.Rule).WithSpan(21, 17, 21, 22).WithArguments("1", "TestClass.Test2")
                }
            }.RunAsync();
        }

        [Test]
        public async Task TestNamedArguments()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using Metaplay.Core.Model;

    namespace ConsoleApplication1
    {
        [MetaSerializable]
        class TestClass
        {
            [MetaMember(flags: MetaMemberFlags.Hidden, tagId: 2)]
            int Test2{get;set;}

            [MetaMember(1)]
            int Test3 {get;set;}
        }
    }";

            await new MetaAnalyzerTest<DuplicateMetaMemberAnalyzer>()
            {
                TestState =
                {
                    Sources = { test },
                },
            }.RunAsync();
        }

        [Test]
        public async Task TestNamedArgumentConflicting()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using Metaplay.Core.Model;

    namespace ConsoleApplication1
    {
        [MetaSerializable]
        class TestClass
        {
            [MetaMember(flags: MetaMemberFlags.Hidden, tagId: 1)]
            int Test2{get;set;}

            [MetaMember(1)]
            int Test3 = 0;
        }
    }";

            await new MetaAnalyzerTest<DuplicateMetaMemberAnalyzer>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(DuplicateMetaMemberAnalyzer.Rule).WithSpan(16, 17, 16, 22).WithArguments("1", "TestClass.Test3"),
                    new DiagnosticResult(DuplicateMetaMemberAnalyzer.Rule).WithSpan(19, 17, 19, 22).WithArguments("1", "TestClass.Test2")
                }
            }.RunAsync();
        }
    }
}
