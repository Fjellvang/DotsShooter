using NUnit.Framework;
using System.Threading.Tasks;
using Verifier =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<Metaplay.CodeAnalyzers.MiscStringAnalyzer,
        Metaplay.CodeAnalyzers.MakeInvariantCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Metaplay.CodeAnalyzers.Tests;

public class InterpolatedStringCodeFixProviderTests
{
    [Test]
    public async Task ReplaceInterpolatedStringWithFormattableString()
    {
        const string text =
            """
            using static System.FormattableString;

            namespace Test;
            public class CommonClass
            {
                public static string Test()
                {
                    float t = 10.0f;
                    return $"test: {t}";
                }
            }
            """;

        const string newText =
            """
            using static System.FormattableString;

            namespace Test;
            public class CommonClass
            {
                public static string Test()
                {
                    float t = 10.0f;
                    return Invariant($"test: {t}");
                }
            }
            """;

        var expected = Verifier.Diagnostic(MiscStringAnalyzer.StringInterpolationRule).WithSpan(9, 24, 9, 27).WithArguments("{t}", "float");
        await Verifier.VerifyCodeFixAsync(text.ReplaceLineEndings(), expected, newText.ReplaceLineEndings()).ConfigureAwait(false);
    }

    [Test]
    public async Task ReplaceInterpolatedStringWithFormattableStringMissingUsing()
    {
        const string text =
            """
            namespace Test;
            public class CommonClass
            {
                public static string Test()
                {
                    float t = 10.0f;
                    return $"test: {t}";
                }
            }
            """;

        const string newText =
            """
            using static System.FormattableString;

            namespace Test;
            public class CommonClass
            {
                public static string Test()
                {
                    float t = 10.0f;
                    return Invariant($"test: {t}");
                }
            }
            """;

        var expected = Verifier.Diagnostic(MiscStringAnalyzer.StringInterpolationRule).WithSpan(7, 24, 7, 27).WithArguments("{t}", "float");
        await Verifier.VerifyCodeFixAsync(text.ReplaceLineEndings(), expected, newText.ReplaceLineEndings()).ConfigureAwait(false);
    }

    [Test]
    public async Task ReplaceInterpolatedStringWithFormattableStringNamespaceMissingUsing()
    {
        const string text =
            """
            namespace Test;
            public class CommonClass
            {
                public static string Test()
                {
                    float t = 10.0f;
                    return $"test: {t}";
                }
            }
            """;

        const string newText =
            """
            using static System.FormattableString;

            namespace Test;
            public class CommonClass
            {
                public static string Test()
                {
                    float t = 10.0f;
                    return Invariant($"test: {t}");
                }
            }
            """;

        var expected = Verifier.Diagnostic(MiscStringAnalyzer.StringInterpolationRule).WithSpan(7, 24, 7, 27).WithArguments("{t}", "float");
        await Verifier.VerifyCodeFixAsync(text.ReplaceLineEndings(), expected, newText.ReplaceLineEndings()).ConfigureAwait(false);
    }

    [Test]
    public async Task ReplaceInterpolatedStringWithFormattableStringAddition()
    {
        const string text =
            """
            namespace Test;
            public class CommonClass
            {
                public static string Test()
                {
                    float t = 10.0f;
                    return $"test: {t}"
                       + "Another String";
                }
            }
            """;

        const string newText =
            """
            using static System.FormattableString;

            namespace Test;
            public class CommonClass
            {
                public static string Test()
                {
                    float t = 10.0f;
                    return Invariant($"test: {t}")
                       + "Another String";
                }
            }
            """;

        var expected = Verifier.Diagnostic(MiscStringAnalyzer.StringInterpolationRule).WithSpan(7, 24, 7, 27).WithArguments("{t}", "float");
        await Verifier.VerifyCodeFixAsync(text.ReplaceLineEndings(), expected, newText.ReplaceLineEndings()).ConfigureAwait(false);
    }
}
