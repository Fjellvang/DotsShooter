// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Model;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using System;
using System.Reflection;

namespace Metaplay.CodeAnalyzers.Tests.Verifiers;

public static class MetaCodeFixTestUtils
{
    public static ReferenceAssemblies GetReferenceAssemblies()
    {
        #if NET9_0
        return ReferenceAssemblies.Net.Net90;
        #elif NET8_0
        return ReferenceAssemblies.Net.Net80;
        #else
        return ReferenceAssemblies.Default;
        #endif
    }
}


public class MetaCodeFixTest<TAnalyzer, TCodeFix> : CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    readonly Type[] _requiredTypes = [ typeof(MetaMemberAttribute), typeof(MetaDictionary<,>) ];

    public MetaCodeFixTest()
    {
        foreach (Type type in _requiredTypes)
            TestState.AdditionalReferences.Add(type.Assembly);
        ReferenceAssemblies = MetaCodeFixTestUtils.GetReferenceAssemblies();
    }
}

public class MetaAnalyzerTest<TAnalyzer> : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    public MetaAnalyzerTest()
    {
        Assembly assembly = Assembly.GetAssembly(typeof(MetaMemberAttribute));
        TestState.AdditionalReferences.Add(assembly);
        ReferenceAssemblies = MetaCodeFixTestUtils.GetReferenceAssemblies();
    }
}
