// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.CodeAnalyzers.Shared;
using Metaplay.CodeAnalyzers.Tests.Verifiers;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;

namespace Metaplay.CodeAnalyzers.Tests
{
    public class MetaMemberInNonSerializedTypeTests
    {
        [Test]
        public async Task TestNoDiagnostics()
        {
            var test = @"";

            await new MetaCodeFixTest<MetaMemberInNonSerializedTypeAnalyzer, MetaMemberInNonSerializedTypeAnalyzerCodeFixProvider>()
            {
                TestState =
                {
                    Sources = { test },
                },
            }.RunAsync();
        }

        [Test]
        public async Task TestNonSerializedClass()
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
    class TestClass
    {
        const int MetaNumber = 1;

        [MetaMember(1)]
        int Test1 {get; set;}
    }
}";

            await new MetaCodeFixTest<MetaMemberInNonSerializedTypeAnalyzer, MetaMemberInNonSerializedTypeAnalyzerCodeFixProvider>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics = { new DiagnosticResult(MetaMemberInNonSerializedTypeAnalyzer.Rule).WithSpan(16, 9, 17, 30).WithArguments("Test1", "TestClass") }
            }.RunAsync();
        }

        [Test]
        public async Task TestMetaMemberInDerivedWithSerializedBaseClass()
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
        [MetaMember(1)]
        int Test1;
    }

    class TestClassDerived : TestClass
    {
        [MetaMember(2)]
        int Test1;
    }
}";

            await new MetaCodeFixTest<MetaMemberInNonSerializedTypeAnalyzer, MetaMemberInNonSerializedTypeAnalyzerCodeFixProvider>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics = {  }
            }.RunAsync();
        }

        [Test]
        public async Task TestFieldInNonSerializedStruct()
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
    struct TestClass
    {
        [MetaMember(1)]
        int Test1;
    }
}";

            await new MetaCodeFixTest<MetaMemberInNonSerializedTypeAnalyzer, MetaMemberInNonSerializedTypeAnalyzerCodeFixProvider>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics = { new DiagnosticResult(MetaMemberInNonSerializedTypeAnalyzer.Rule).WithSpan(14, 9, 15, 19).WithArguments("Test1", "TestClass") }
            }.RunAsync();
        }

        [Test]
        public async Task TestNonSerializedStruct()
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
    struct TestClass
    {
        [MetaMember(1)]
        int Test1 {get; set;}
    }
}";

            await new MetaCodeFixTest<MetaMemberInNonSerializedTypeAnalyzer, MetaMemberInNonSerializedTypeAnalyzerCodeFixProvider>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics = { new DiagnosticResult(MetaMemberInNonSerializedTypeAnalyzer.Rule).WithSpan(14, 9, 15, 30).WithArguments("Test1", "TestClass") }
            }.RunAsync();
        }

        //Diagnostic triggered and checked for
        [Test]
        public async Task TestNonSerializedInterface()
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
    interface TestClass
    {
        [MetaMember(1)]
        int Test2{get;set;}
    }
}";

            await new MetaCodeFixTest<MetaMemberInNonSerializedTypeAnalyzer, MetaMemberInNonSerializedTypeAnalyzerCodeFixProvider>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics = { new DiagnosticResult(MetaMemberInNonSerializedTypeAnalyzer.Rule).WithSpan(14, 9, 15, 28).WithArguments("Test2", "TestClass") }
            }.RunAsync();
        }

        //Diagnostic triggered and checked for
        [Test]
        public async Task TestCodeFixMetaMemberInNonSerializedClass()
        {
            var test = @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Metaplay.Core.Model;

namespace ConsoleApplication1
{
    class TestClass
    {
        [MetaMember(1)]
        int Test2 {get; set;}
    }
}".ReplaceLineEndings();
            var fixedSource = @"using System;
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
        [MetaMember(1)]
        int Test2 {get; set;}
    }
}".ReplaceLineEndings();

            await new MetaCodeFixTest<MetaMemberInNonSerializedTypeAnalyzer, MetaMemberInNonSerializedTypeAnalyzerCodeFixProvider>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics = { new DiagnosticResult(MetaMemberInNonSerializedTypeAnalyzer.Rule).WithSpan(13, 9, 14, 30).WithArguments("Test2", "TestClass") },
                FixedCode           = fixedSource
            }.RunAsync();
        }
    }
}
