// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Metaplay.CodeAnalyzers.Shared
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MetaMemberInNonSerializedTypeAnalyzer : DiagnosticAnalyzer
    {
        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            "MP_MM_01",
            "MetaMember in non-serialized type",
            "Found MetaMember '{0}' in non-serialized type '{1}'",
            "MetaplayCustom",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "MetaMembers are only valid when defined in a type that is tagged by MetaSerializable(Derived).");

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(CompilationStart);
        }

        void CompilationStart(CompilationStartAnalysisContext context)
        {
            INamedTypeSymbol metaSerializableTypeSymbol = context.Compilation.GetTypeByMetadataName("Metaplay.Core.Model.MetaSerializableAttribute");
            if (metaSerializableTypeSymbol == null)
                return;

            INamedTypeSymbol typeCodeProviderTypeSymbol = context.Compilation.GetTypeByMetadataName("Metaplay.Core.Model.ISerializableTypeCodeProvider");

            context.RegisterSyntaxNodeAction(x => AnalyzeNode(x, typeCodeProviderTypeSymbol, metaSerializableTypeSymbol), SyntaxKind.PropertyDeclaration, SyntaxKind.FieldDeclaration);
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(Rule); }
        }

        void AnalyzeNode(SyntaxNodeAnalysisContext context, INamedTypeSymbol typeCodeProviderTypeSymbol, INamedTypeSymbol metaSerializableTypeSymbol)
        {
            var memberDeclaration = (MemberDeclarationSyntax)context.Node;

            bool hasMetaMemberAttr = memberDeclaration.AttributeLists.Any(x => x.Attributes.Any(y => y.Name.ToString().Contains("MetaMember")));
            if (!hasMetaMemberAttr)
                return;

            var parentType = memberDeclaration.FirstAncestorOrSelf<TypeDeclarationSyntax>();
            INamedTypeSymbol parentTypeSymbol = context.SemanticModel.GetDeclaredSymbol(parentType, context.CancellationToken);

            bool anyInterfaceHasAttribute = parentTypeSymbol.AllInterfaces.Any(
                i => i.HasMetaSerializableAttribute(typeCodeProviderTypeSymbol, metaSerializableTypeSymbol));

            bool hasAttr = false;
            do
            {
                if (parentTypeSymbol.HasMetaSerializableAttribute(typeCodeProviderTypeSymbol, metaSerializableTypeSymbol))
                {
                    hasAttr = true;
                    break;
                }
                parentTypeSymbol = parentTypeSymbol.BaseType;
            } while (parentTypeSymbol != null);

            if (!hasAttr && !anyInterfaceHasAttribute)
            {
                var identifier = (memberDeclaration as PropertyDeclarationSyntax)?.Identifier.Text ?? (memberDeclaration as FieldDeclarationSyntax)?.Declaration.Variables.FirstOrDefault()?.Identifier.Text;

                Diagnostic diagnostic = Diagnostic.Create(Rule, memberDeclaration.GetLocation(), identifier, parentType.Identifier.Text);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MetaMemberInNonSerializedTypeAnalyzerCodeFixProvider)), Shared]
    public class MetaMemberInNonSerializedTypeAnalyzerCodeFixProvider : CodeFixProvider
    {
        protected virtual string Title { get; set; } = "Add MetaSerializable Attribute To Type";
        protected virtual string Type  { get; set; } = "MetaSerializable";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(MetaMemberInNonSerializedTypeAnalyzer.Rule.Id); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);

            var diagnostic     = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindNode(diagnosticSpan).FirstAncestorOrSelf<TypeDeclarationSyntax>();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: c => FixAsync(context.Document, declaration, c),
                    equivalenceKey: Title),
                diagnostic);
        }

        private async Task<Document> FixAsync(Document document, TypeDeclarationSyntax typeDeclaration, CancellationToken cancellationToken)
        {
            TypeDeclarationSyntax typeDeclarationSyntax = typeDeclaration.AddAttributeLists(
                SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(Type)))));

            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = oldRoot.ReplaceNode(typeDeclaration, typeDeclarationSyntax);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
