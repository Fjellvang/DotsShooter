// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.CodeAnalyzers.Shared;
using Metaplay.CodeAnalyzers.Tests.Verifiers;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Verifier =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<Metaplay.CodeAnalyzers.Shared.FixOrderedDictionaryAnalyzer,
        Metaplay.CodeAnalyzers.Shared.FixOrderedDictionaryAnalyzerCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Metaplay.CodeAnalyzers.Tests
{
    public class FixOrderedDictionaryTests
    {
        static Task TestSimpleCodeFix(string code)
        {
            string sanitizedCode = code.ReplaceLineEndings();
            var test = new MetaCodeFixTest<FixOrderedDictionaryAnalyzer, FixOrderedDictionaryAnalyzerCodeFixProvider>()
            {
                TestState =
                {
                    Sources = {sanitizedCode},
                },
                FixedCode = sanitizedCode.Replace("OrderedDictionary", "MetaDictionary"),
            };

            // Find an add all occurrences of "OrderedDictionary" and "ToOrderedDictionary" as expected diagnostic results
            string[] lines = sanitizedCode.Split('\n');
            for (int line = 0; line < lines.Length; line++)
            {
                foreach (int col in Regex.Matches(lines[line], "OrderedDictionary<").Select(m => m.Index))
                {
                    int len = lines[line].Substring(col).IndexOf('>') + 1;
                    test.ExpectedDiagnostics.Add(new DiagnosticResult(FixOrderedDictionaryAnalyzer.TypeRule)
                        .WithSpan(line + 1, col + 1, line + 1, col + len + 1));
                }
                foreach (int col in Regex.Matches(lines[line], "ToOrderedDictionary\\(").Select(m => m.Index))
                {
                    int len = "ToOrderedDictionary".Length;
                    test.ExpectedDiagnostics.Add(new DiagnosticResult(FixOrderedDictionaryAnalyzer.MethodRule)
                        .WithSpan(line + 1, col + 1, line + 1, col + len + 1));
                }
            }
            return test.RunAsync();
        }

        [Test]
        public async Task TestNoDiagnostics()
        {
            const string test = "";
            await new MetaCodeFixTest<FixOrderedDictionaryAnalyzer, FixOrderedDictionaryAnalyzerCodeFixProvider>()
            {
                TestState =
                {
                    Sources = { test },
                },
            }.RunAsync();
        }

        [Test]
        public Task ReplaceOrderedDictionaryInProperty() => TestSimpleCodeFix(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Text;
            using System.Threading.Tasks;
            using System.Diagnostics;
            using Metaplay.Core.Model;
            using Metaplay.Core;

            namespace ConsoleApplication1
            {
                class TestClass
                {
                    const int MetaNumber = 1;

                    [MetaMember(1)]
                    OrderedDictionary<int, int> Test1 {get; set;}
                }
            }
            """);

        [Test]
        public Task ReplaceOrderedDictionaryInLocalField() => TestSimpleCodeFix(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Text;
            using System.Threading.Tasks;
            using System.Diagnostics;
            using Metaplay.Core.Model;
            using Metaplay.Core;

            namespace ConsoleApplication1
            {
                class TestClass
                {
                    public void Test()
                    {
                        OrderedDictionary<int, int> Test1;
                    }
                }
            }
            """);

        [Test]
        public Task ReplaceOrderedDictionaryInBaseClass() => TestSimpleCodeFix(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Text;
            using System.Threading.Tasks;
            using System.Diagnostics;
            using Metaplay.Core.Model;
            using Metaplay.Core;

            namespace ConsoleApplication1
            {
                class TestClass : OrderedDictionary<int, int>
                {
                }
            }
            """);

        [Test]
        public Task ReplaceOrderedDictionaryInReturnValue() => TestSimpleCodeFix(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Text;
            using System.Threading.Tasks;
            using System.Diagnostics;
            using Metaplay.Core.Model;
            using Metaplay.Core;

            namespace ConsoleApplication1
            {
                class TestClass
                {
                    public OrderedDictionary<int, int> Test1 (){ return null; }
                }
            }
            """);

        [Test]
        public Task ReplaceOrderedDictionaryInParameter() => TestSimpleCodeFix(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Text;
            using System.Threading.Tasks;
            using System.Diagnostics;
            using Metaplay.Core.Model;
            using Metaplay.Core;

            namespace ConsoleApplication1
            {
                class TestClass
                {
                    public void Test1 (OrderedDictionary<int, int> test){  }
                }
            }
            """);

        [Test]
        public Task ReplaceOrderedDictionaryInConstructor() => TestSimpleCodeFix(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Text;
            using System.Threading.Tasks;
            using System.Diagnostics;
            using Metaplay.Core.Model;
            using Metaplay.Core;

            namespace ConsoleApplication1
            {
                class TestClass
                {
                    public OrderedDictionary<int, int> Test1 = new OrderedDictionary<int, int>();
                }
            }
            """);

        [Test]
        public Task ReplaceToOrderedDictionary() => TestSimpleCodeFix(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Text;
            using System.Threading.Tasks;
            using System.Diagnostics;
            using Metaplay.Core.Model;
            using Metaplay.Core;

            namespace ConsoleApplication1
            {
                class TestClass
                {
                    public OrderedDictionary<int, int> Test1 = new List<int>().ToOrderedDictionary(x => x, x => x);
                }
            }
            """);

    }
}
