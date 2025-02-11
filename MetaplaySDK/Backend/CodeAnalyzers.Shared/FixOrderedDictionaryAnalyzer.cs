// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Metaplay.CodeAnalyzers.Shared
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class FixOrderedDictionaryAnalyzer : DiagnosticAnalyzer
    {
        public static readonly DiagnosticDescriptor TypeRule = new DiagnosticDescriptor(
            "MP_OD_01",
            "Deprecated use of OrderedDictionary",
            "OrderedDictionary is deprecated, and should no longer be used anywhere. Prefer MetaDictionary instead.",
            "MetaplayCustom",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "OrderedDictionary is deprecated, and should no longer be used anywhere. Prefer MetaDictionary instead.");

        public static readonly DiagnosticDescriptor MethodRule = new DiagnosticDescriptor(
            "MP_OD_02",
            "Deprecated use of ToOrderedDictionary()",
            "ToOrderedDictionary() is deprecated, and should no longer be used anywhere. Prefer ToMetaDictionary() instead.",
            "MetaplayCustom",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "ToOrderedDictionary() is deprecated, and should no longer be used anywhere. Prefer ToMetaDictionary() instead.");


        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(TypeRule, MethodRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(CompilationStart);
        }

        static void CompilationStart(CompilationStartAnalysisContext context)
        {
            INamedTypeSymbol orderedDictionaryTypeSymbol = context.Compilation.GetTypeByMetadataName("Metaplay.Core.OrderedDictionary`2")?.ConstructUnboundGenericType();
            INamedTypeSymbol metaDictionaryTypeSymbol    = context.Compilation.GetTypeByMetadataName("Metaplay.Core.MetaDictionary`2");

            // Only register action if both old and new names for OrderedDictionary are found.
            if (orderedDictionaryTypeSymbol == null || metaDictionaryTypeSymbol == null)
                return;

            context.RegisterSyntaxNodeAction(x => AnalyzeNode(x, orderedDictionaryTypeSymbol), SyntaxKind.GenericName, SyntaxKind.IdentifierName);
        }

        static void AnalyzeNode(SyntaxNodeAnalysisContext context, INamedTypeSymbol orderedDictionaryTypeSymbol)
        {
            ISymbol symbol = context.SemanticModel.GetSymbolInfo(context.Node).Symbol;

            if (context.Node.IsKind(SyntaxKind.GenericName))
            {
                if (symbol?.OriginalDefinition is INamedTypeSymbol namedTypeSymbol)
                {
                    INamedTypeSymbol unboundType = namedTypeSymbol.ConstructUnboundGenericType();
                    if (unboundType.Equals(orderedDictionaryTypeSymbol, SymbolEqualityComparer.Default))
                        context.ReportDiagnostic(Diagnostic.Create(TypeRule, context.Node.GetLocation()));
                }
            }
            else if (context.Node.IsKind(SyntaxKind.IdentifierName))
            {
                if (symbol is IMethodSymbol methodSymbol)
                {
                    string containingTypeStr = methodSymbol.ContainingType.ToString();
                    string methodName        = methodSymbol.Name;

                    if (containingTypeStr == "Metaplay.Core.EnumerableExtensions" && methodName == "ToOrderedDictionary")
                        context.ReportDiagnostic(Diagnostic.Create(MethodRule, context.Node.GetLocation()));
                }
            }
        }
    }

    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FixOrderedDictionaryAnalyzerCodeFixProvider)), Shared]
    public class FixOrderedDictionaryAnalyzerCodeFixProvider : CodeFixProvider
    {
        const string TypeTitle = "Replace OrderedDictionary with MetaDictionary";
        const string MethodTitle = "Replace ToOrderedDictionary() with ToMetaDictionary()";

        public override sealed ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(FixOrderedDictionaryAnalyzer.TypeRule.Id, FixOrderedDictionaryAnalyzer.MethodRule.Id);

        public override sealed FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override sealed async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
            if (root == null)
                return;

            Diagnostic diagnostic     = context.Diagnostics.First();
            TextSpan   diagnosticSpan = diagnostic.Location.SourceSpan;
            if (diagnostic.Descriptor.Equals(FixOrderedDictionaryAnalyzer.TypeRule))
            {
                // Find the type declaration identified by the diagnostic.
                GenericNameSyntax declaration = (GenericNameSyntax)root.FindNode(diagnosticSpan).DescendantNodesAndSelf().FirstOrDefault(x => x is GenericNameSyntax);
                if (declaration == null)
                    return;

                // Register a code action that will invoke the fix.
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: TypeTitle,
                        createChangedDocument: c => FixTypeAsync(context.Document, declaration, c),
                        equivalenceKey: nameof(FixOrderedDictionaryAnalyzerCodeFixProvider)),
                    diagnostic);
            }
            else if (diagnostic.Descriptor.Equals(FixOrderedDictionaryAnalyzer.MethodRule))
            {
                // Find the type declaration identified by the diagnostic.
                IdentifierNameSyntax identifier = (IdentifierNameSyntax)root.FindNode(diagnosticSpan).DescendantNodesAndSelf().FirstOrDefault(x => x is IdentifierNameSyntax);
                if (identifier == null)
                    return;

                // Register a code action that will invoke the fix.
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: MethodTitle,
                        createChangedDocument: c => FixMethodInvocationAsync(context.Document, identifier, c),
                        equivalenceKey: nameof(FixOrderedDictionaryAnalyzerCodeFixProvider)),
                    diagnostic);
            }
        }

        static async Task<Document> FixTypeAsync(Document document, GenericNameSyntax nameDeclaration, CancellationToken cancellationToken)
        {
            GenericNameSyntax newName = SyntaxFactory.GenericName(
                    SyntaxFactory.Identifier("MetaDictionary"))
                .WithLeadingTrivia(nameDeclaration.GetLeadingTrivia())
                .WithTrailingTrivia(nameDeclaration.GetTrailingTrivia())
                .WithTypeArgumentList(nameDeclaration.TypeArgumentList);

            SyntaxNode oldRoot = await document.GetSyntaxRootAsync(cancellationToken);
            SyntaxNode newRoot = oldRoot.ReplaceNode(nameDeclaration, newName);

            return document.WithSyntaxRoot(newRoot);
        }

        static async Task<Document> FixMethodInvocationAsync(Document document, IdentifierNameSyntax identifier, CancellationToken cancellationToken)
        {
            IdentifierNameSyntax newName = SyntaxFactory.IdentifierName("ToMetaDictionary");
            SyntaxNode oldRoot = await document.GetSyntaxRootAsync(cancellationToken);
            SyntaxNode newRoot = oldRoot.ReplaceNode(identifier, newName);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
