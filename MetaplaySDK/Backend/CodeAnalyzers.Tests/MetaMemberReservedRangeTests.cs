// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.CodeAnalyzers.Shared;
using Metaplay.CodeAnalyzers.Tests.Verifiers;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;

namespace Metaplay.CodeAnalyzers.Tests
{
    public class MetaMemberReservedRangeTests
    {
        [Test]
        public async Task TestNoDiagnostics()
        {
            var test = @"";

            await new MetaAnalyzerTest<MetaMemberReservedRangeAnalyzer>()
            {
                TestState =
                {
                    Sources = {test},
                },
            }.RunAsync();
        }

        [Test]
        public async Task TestAllowMemberInReservedRange()
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
    [Metaplay.Core.Model.MetaReservedMembersAttribute(2, 5)]
    class TestClass
    {
        [MetaMember(2)]
        int Test2 {get; set;}

        [MetaMember(4)]
        int Test3 {get; set;}
    }
}";

            await new MetaAnalyzerTest<MetaMemberReservedRangeAnalyzer>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics =
                {
                }
            }.RunAsync();
        }

        [Test]
        public async Task TestAllowMemberInMultipleReservedRanges()
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
    [Metaplay.Core.Model.MetaReservedMembersAttribute(2, 5)]
    [Metaplay.Core.Model.MetaReservedMembersAttribute(8, 10)]
    class TestClass
    {
        [MetaMember(2)]
        int Test2 {get; set;}

        [MetaMember(8)]
        int Test3 {get; set;}
    }
}";

            await new MetaAnalyzerTest<MetaMemberReservedRangeAnalyzer>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics =
                {
                }
            }.RunAsync();
        }

        [Test]
        public async Task TestAllowNonReservedMember()
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
    [Metaplay.Core.Model.MetaReservedMembersAttribute(2, 5)]
    [Metaplay.Core.Model.MetaAllowNonReservedMembersAttribute()]
    class TestClass
    {
        [MetaMember(2)]
        int Test2 {get; set;}

        [MetaMember(8)]
        int Test3 {get; set;}

        [MetaMember(12345)]
        int Test4 {get; set;}
    }
}";

            await new MetaAnalyzerTest<MetaMemberReservedRangeAnalyzer>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics =
                {
                }
            }.RunAsync();
        }

        [Test]
        public async Task TestOutsideOfReservedRange()
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
    [Metaplay.Core.Model.MetaReservedMembersAttribute(2, 5)]
    class TestClass
    {
        [MetaMember(1)]
        int Test1 {get; set;}
    }
}";

            await new MetaAnalyzerTest<MetaMemberReservedRangeAnalyzer>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(MetaMemberReservedRangeAnalyzer.OutsideOfReservedRangeRule).WithSpan(17, 13, 17, 18).WithArguments("1")
                }
            }.RunAsync();
        }

        [Test]
        public async Task TestUsingBlockedMembers()
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
    [Metaplay.Core.Model.MetaBlockedMembersAttribute(2, 5)]
    class TestClass
    {
        [MetaMember(2)]
        int Test1 {get; set;}
    }
}";

            await new MetaAnalyzerTest<MetaMemberReservedRangeAnalyzer>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(MetaMemberReservedRangeAnalyzer.BlockedMemberRule).WithSpan(17, 13, 17, 18).WithArguments("2")
                }
            }.RunAsync();
        }

        [Test]
        public async Task TestUsingBlockedMembersFromBaseClass()
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
    [Metaplay.Core.Model.MetaBlockedMembersAttribute(2, 5)]
    class TestClass
    {
    }

    [Metaplay.Core.Model.MetaSerializableDerived(1)]
    class TestClassDerived : TestClass
    {
        [MetaMember(2)]
        int Test1 {get; set;}
    }
}";

            await new MetaAnalyzerTest<MetaMemberReservedRangeAnalyzer>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(MetaMemberReservedRangeAnalyzer.BlockedMemberRule).WithSpan(22, 13, 22, 18).WithArguments("2")
                }
            }.RunAsync();
        }

        [Test]
        public async Task TestInsideOfBaseReservedRange()
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
    [Metaplay.Core.Model.MetaReservedMembersAttribute(2, 5)]
    class TestClass
    {
        [MetaMember(2)]
        int Test1 {get; set;}
    }

    [Metaplay.Core.Model.MetaSerializableDerived(1)]
    class TestClassDerived : TestClass
    {
        [MetaMember(3)]
        int Test2 {get; set;}
    }
}";

            await new MetaAnalyzerTest<MetaMemberReservedRangeAnalyzer>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(MetaMemberReservedRangeAnalyzer.InReservedRangeRule).WithSpan(24, 13, 24, 18).WithArguments("3"),
                }
            }.RunAsync();
        }

        [Test]
        public async Task TestOutsideOfBaseReservedRange()
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
    [Metaplay.Core.Model.MetaReservedMembersAttribute(2, 5)]
    class TestClass
    {
        [MetaMember(2)]
        int Test1 {get; set;}
    }

    [Metaplay.Core.Model.MetaSerializableDerived(1)]
    class TestClassDerived : TestClass
    {
        [MetaMember(3)]
        int Test2 {get; set;}
    }
}";

            await new MetaAnalyzerTest<MetaMemberReservedRangeAnalyzer>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(MetaMemberReservedRangeAnalyzer.InReservedRangeRule).WithSpan(24, 13, 24, 18).WithArguments("3"),
                }
            }.RunAsync();
        }

        [Test]
        public async Task TestInsideReservedRangeWithReservedBaseClass()
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
    [Metaplay.Core.Model.MetaReservedMembersAttribute(2, 5)]
    class TestClass
    {
        [MetaMember(2)]
        int Test1 {get; set;}
    }

    [Metaplay.Core.Model.MetaSerializableDerived(1)]
    [Metaplay.Core.Model.MetaReservedMembersAttribute(5, 10)]
    class TestClassDerived : TestClass
    {
        [MetaMember(5)]
        int Test2 {get; set;}
    }
}";

            await new MetaAnalyzerTest<MetaMemberReservedRangeAnalyzer>()
            {
                TestState =
                {
                    Sources = { test },
                },
                ExpectedDiagnostics =
                {
                }
            }.RunAsync();
        }
    }
}
