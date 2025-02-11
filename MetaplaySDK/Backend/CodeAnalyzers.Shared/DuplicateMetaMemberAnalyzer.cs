using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.Serialization;

namespace Metaplay.CodeAnalyzers.Shared
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DuplicateMetaMemberAnalyzer : DiagnosticAnalyzer
    {
        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            "MP_DMM_00",
            "Duplicate MetaMember tag id",
            "MetaMember tag id '{0}' is duplicated by {1}",
            "MetaplayCustom",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Ensure that MetaMembers do not share the same TagId.");

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
            INamedTypeSymbol metaMemberTypeSymbol       = context.Compilation.GetTypeByMetadataName("Metaplay.Core.Model.MetaMemberAttribute");

            context.RegisterSyntaxNodeAction(x => AnalyzeNode(x, typeCodeProviderTypeSymbol, metaSerializableTypeSymbol, metaMemberTypeSymbol), SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration);
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(Rule); }
        }

        void AnalyzeNode(SyntaxNodeAnalysisContext context, INamedTypeSymbol typeCodeProviderTypeSymbol, INamedTypeSymbol metaSerializableTypeSymbol, INamedTypeSymbol metaMemberTypeSymbol)
        {
            INamedTypeSymbol typeSymbol = context.SemanticModel.GetDeclaredSymbol(context.Node) as INamedTypeSymbol;

            if (typeSymbol == null)
                return;

            bool hasSerializationAttr = typeSymbol.HasMetaSerializableAttribute(typeCodeProviderTypeSymbol, metaSerializableTypeSymbol);

            if (!hasSerializationAttr)
                return;

            List<(Location location, ISymbol member, int tagId)> values      = new List<(Location location, ISymbol member, int tagId)>();
            INamedTypeSymbol                                     currentType = typeSymbol;

            do
            {
                foreach (ISymbol member in currentType.GetMembers())
                {
                    if (member is IFieldSymbol || member is IPropertySymbol)
                    {
                        foreach (AttributeData attributeData in member.GetAttributes().Where(x => x.AttributeClass.Equals(metaMemberTypeSymbol, SymbolEqualityComparer.Default)))
                        {
                            object value = attributeData.ConstructorArguments[0].Value;
                            if (value is int intValue)
                            {
                                values.Add((member.Locations.FirstOrDefault(), member, intValue));
                            }
                        }
                    }
                }

                currentType = currentType.BaseType;
            }
            while (currentType != null);

            foreach (var group in values.GroupBy(x => x.tagId))
            {
                int count = group.Count();
                if (count > 1)
                {
                    foreach ((Location location, ISymbol member, int tagId) in group)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Rule, location, tagId, string.Join(", ", group.Where(x=> x.location != location).Select(x=> $"{x.member.ContainingType.Name}.{x.member.Name}"))));
                    }
                }
            }
        }
    }
}
