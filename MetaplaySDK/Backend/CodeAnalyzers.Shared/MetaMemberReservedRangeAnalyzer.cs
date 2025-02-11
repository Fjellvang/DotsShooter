// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Metaplay.CodeAnalyzers.Shared
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MetaMemberReservedRangeAnalyzer: DiagnosticAnalyzer
    {
        public static readonly DiagnosticDescriptor InReservedRangeRule = new DiagnosticDescriptor(
            "MP_RRMM_00",
            "MetaMember uses tag id from reserved range",
            "MetaMember using tag id '{0}' is in reserved range of a base class",
            "MetaplayCustom",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Ensure that MetaMembers do not uses TagIds from reserved ranges from base classes.");

        public static readonly DiagnosticDescriptor OutsideOfReservedRangeRule = new DiagnosticDescriptor(
            "MP_RRMM_01",
            "MetaMember uses tag id from outside the reserved range",
            "MetaMember using tag id '{0}' is not in the reserved range for this type",
            "MetaplayCustom",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Ensure that MetaMembers uses TagIds from the reserved range for this type.");

        public static readonly DiagnosticDescriptor BlockedMemberRule = new DiagnosticDescriptor(
            "MP_RRMM_02",
            "MetaMember uses blocked tag id",
            "MetaMember uses blocked tag id '{0}'",
            "MetaplayCustom",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Ensure that MetaMembers do not use blocked tag ids.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(InReservedRangeRule, OutsideOfReservedRangeRule, BlockedMemberRule); }
        }

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

            INamedTypeSymbol typeCodeProviderTypeSymbol       = context.Compilation.GetTypeByMetadataName("Metaplay.Core.Model.ISerializableTypeCodeProvider");
            INamedTypeSymbol metaMemberTypeSymbol             = context.Compilation.GetTypeByMetadataName("Metaplay.Core.Model.MetaMemberAttribute");
            INamedTypeSymbol reservedRangeTypeSymbol          = context.Compilation.GetTypeByMetadataName("Metaplay.Core.Model.MetaReservedMembersAttribute");
            INamedTypeSymbol allowNonReservedMemberTypeSymbol = context.Compilation.GetTypeByMetadataName("Metaplay.Core.Model.MetaAllowNonReservedMembersAttribute");
            INamedTypeSymbol blockedMemberTypeSymbol = context.Compilation.GetTypeByMetadataName("Metaplay.Core.Model.MetaBlockedMembersAttribute");

            context.RegisterSyntaxNodeAction(x => AnalyzeNode(x, typeCodeProviderTypeSymbol, metaSerializableTypeSymbol, metaMemberTypeSymbol, reservedRangeTypeSymbol, allowNonReservedMemberTypeSymbol, blockedMemberTypeSymbol), SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration);
        }

        void AnalyzeNode(
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol typeCodeProviderTypeSymbol,
            INamedTypeSymbol metaSerializableTypeSymbol,
            INamedTypeSymbol metaMemberTypeSymbol,
            INamedTypeSymbol reservedRangeTypeSymbol,
            INamedTypeSymbol allowNonReservedMemberTypeSymbol,
            INamedTypeSymbol blockedMemberTypeSymbol)
        {
            INamedTypeSymbol typeSymbol = context.SemanticModel.GetDeclaredSymbol(context.Node) as INamedTypeSymbol;

            if (typeSymbol == null)
                return;

            ImmutableArray<AttributeData> attributes           = typeSymbol.GetAttributes();
            bool                          hasSerializationAttr = false;

            List<(int start, int end)> allowedRanges           = new List<(int start, int end)>();
            HashSet<int>               blockedMembers          = new HashSet<int>();
            bool                       allowNonReservedMembers = false;

            foreach (AttributeData attributeData in attributes)
            {
                if (attributeData.IsMetaSerializableAttribute(typeCodeProviderTypeSymbol, metaSerializableTypeSymbol))
                    hasSerializationAttr = true;
                else if (attributeData.AttributeClass.Equals(reservedRangeTypeSymbol, SymbolEqualityComparer.Default))
                {
                    if (!(attributeData.ConstructorArguments[0].Value is int minValue) || !(attributeData.ConstructorArguments[1].Value is int maxValue) )
                        continue;

                    allowedRanges.Add((minValue, maxValue));
                }
                else if (attributeData.AttributeClass.Equals(allowNonReservedMemberTypeSymbol, SymbolEqualityComparer.Default))
                {
                    allowNonReservedMembers = true;
                }
                else if (attributeData.AttributeClass.Equals(blockedMemberTypeSymbol, SymbolEqualityComparer.Default))
                {
                    foreach (TypedConstant constant in attributeData.ConstructorArguments[0].Values)
                    {
                        if (constant.Value is int value)
                            blockedMembers.Add(value);
                    }
                }
            }

            if (!hasSerializationAttr)
                return;

            var parent = typeSymbol.BaseType;

            List<(int start, int end)> disallowedRanges = new List<(int start, int end)>();

            while (parent != null)
            {
                foreach (AttributeData attributeData in parent.GetAttributes())
                {
                    if (attributeData.AttributeClass.Equals(reservedRangeTypeSymbol, SymbolEqualityComparer.Default))
                    {
                        if (!(attributeData.ConstructorArguments[0].Value is int minValue) || !(attributeData.ConstructorArguments[1].Value is int maxValue) )
                            continue;

                        disallowedRanges.Add((minValue, maxValue));
                    }
                    else if (attributeData.AttributeClass.Equals(blockedMemberTypeSymbol, SymbolEqualityComparer.Default))
                    {
                        foreach (TypedConstant constant in attributeData.ConstructorArguments[0].Values)
                        {
                            if (constant.Value is int value)
                                blockedMembers.Add(value);
                        }
                    }
                }

                parent = parent.BaseType;
            }

            if (disallowedRanges.Count == 0 && allowedRanges.Count == 0 && blockedMembers.Count == 0)
                return;

            if (allowNonReservedMembers && disallowedRanges.Count == 0 && blockedMembers.Count == 0)
                return;

            foreach (ISymbol member in typeSymbol.GetMembers())
            {
                if (member is IFieldSymbol || member is IPropertySymbol)
                {
                    foreach (AttributeData attributeData in member.GetAttributes().Where(x => x.AttributeClass.Equals(metaMemberTypeSymbol, SymbolEqualityComparer.Default)))
                    {
                        object value = attributeData.ConstructorArguments[0].Value;
                        if (value is int intValue)
                        {
                            if(blockedMembers.Contains(intValue))
                                context.ReportDiagnostic(Diagnostic.Create(BlockedMemberRule, member.Locations.FirstOrDefault(), intValue));
                            if (!allowNonReservedMembers && allowedRanges.Count > 0 && allowedRanges.All(x => x.start > intValue || x.end <= intValue))
                                context.ReportDiagnostic(Diagnostic.Create(OutsideOfReservedRangeRule, member.Locations.FirstOrDefault(), intValue));
                            if (disallowedRanges.Any(x => intValue >= x.start && intValue < x.end))
                                context.ReportDiagnostic(Diagnostic.Create(InReservedRangeRule, member.Locations.FirstOrDefault(), intValue));
                        }
                    }
                }
            }
        }
    }
}
