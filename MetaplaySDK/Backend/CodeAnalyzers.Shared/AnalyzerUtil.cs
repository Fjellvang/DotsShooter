// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Microsoft.CodeAnalysis;
using System.Linq;

namespace Metaplay.CodeAnalyzers.Shared
{
    public static class AnalyzerUtil
    {
        public static bool IsMetaSerializableAttribute(this AttributeData attributeData, INamedTypeSymbol typeCodeProviderTypeSymbol, INamedTypeSymbol metaSerializableTypeSymbol)
        {
            return attributeData.AttributeClass.Equals(metaSerializableTypeSymbol, SymbolEqualityComparer.Default) ||
                attributeData.AttributeClass.AllInterfaces.Any(y => y.Equals(typeCodeProviderTypeSymbol, SymbolEqualityComparer.Default));
        }

        public static bool HasMetaSerializableAttribute(this ISymbol symbol, INamedTypeSymbol typeCodeProviderTypeSymbol, INamedTypeSymbol metaSerializableTypeSymbol)
        {
            return symbol.GetAttributes().Any(x => x.IsMetaSerializableAttribute(typeCodeProviderTypeSymbol, metaSerializableTypeSymbol));
        }
    }
}
