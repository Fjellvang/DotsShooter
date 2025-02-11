// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Metaplay.CodeAnalyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MakeInvariantCodeFixProvider)), Shared]
    public class MakeInvariantCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(MiscStringAnalyzer.StringInterpolationDiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic     = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<InterpolatedStringExpressionSyntax>().First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Make Invariant",
                    createChangedDocument: c => MakeConstAsync(context.Document, declaration, c),
                    equivalenceKey: "Make Invariant"),
                diagnostic);
        }

        private async Task<Document> MakeConstAsync(Document document, InterpolatedStringExpressionSyntax interpolatedString, CancellationToken cancellationToken)
        {
            var         firstToken     = interpolatedString.GetFirstToken();
            SyntaxToken lastToken      = interpolatedString.GetLastToken();
            var         leadingTrivia  = firstToken.LeadingTrivia;
            var         trailingTrivia = lastToken.TrailingTrivia;
            var trimmedInterpolatedString = interpolatedString.ReplaceToken(
                    firstToken,
                    firstToken.WithLeadingTrivia(SyntaxTriviaList.Empty))
                .ReplaceToken(
                    lastToken,
                    lastToken.WithoutTrivia());

            InvocationExpressionSyntax invocationExpressionSyntax = InvocationExpression(IdentifierName(Identifier("Invariant")))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(trimmedInterpolatedString.WithoutTrailingTrivia()))))
                .WithAdditionalAnnotations(Formatter.Annotation)
                .WithLeadingTrivia(leadingTrivia)
                .WithTrailingTrivia(trailingTrivia);

            var editor = await DocumentEditor.CreateAsync(document, cancellationToken);

            var tree = await document.GetSyntaxTreeAsync(cancellationToken);
            var root = await tree.GetRootAsync(cancellationToken);

            var compUnit = root.DescendantNodesAndSelf().OfType<CompilationUnitSyntax>().First();

            SyntaxList<UsingDirectiveSyntax> usings = compUnit.Usings;
            if (usings.All(x => !x.ToString().Contains("using static System.FormattableString")))
                usings = usings.Add(UsingDirective(
                    QualifiedName(IdentifierName("System"), IdentifierName("FormattableString"))).WithStaticKeyword(Token(SyntaxKind.StaticKeyword)));

            // Adding usings and replacing nodes together is a bit tricky, if you update a node the tree changes then the old references are no longer valid.
            // DocumentEditor should help a little with this however if you replace the root node (aka compUnit) then it still can't do it.
            // And for some reason if you modify updatedCompUnity with new usings, it for some reason also does not apply...
            CompilationUnitSyntax updatedCompUnit = compUnit.ReplaceNode(interpolatedString, invocationExpressionSyntax);

            CompilationUnitSyntax newCompUnit = CompilationUnit(compUnit.Externs, usings, compUnit.AttributeLists, updatedCompUnit.Members, compUnit.EndOfFileToken);
            editor.ReplaceNode(compUnit, newCompUnit);

            var newDocument = editor.GetChangedDocument();

            return newDocument;
        }
    }
}
