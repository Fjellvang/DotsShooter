// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Config;
using Metaplay.Core.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using static System.FormattableString;

namespace Metaplay.Core.Serialization
{
    public partial class TaggedSerializerRoslynGenerator
    {
        // Upper limit for the SpanWriter.EnsureSpace() request in generated code. Larger value allows grouping more writes into a single
        // block but potentially causes more wasted space in the write buffers.
        static readonly int MaxSpanSize = 256;

        static readonly MetaDictionary<Type, ITypeInfo> s_additionalTypeInfos = new MetaDictionary<Type, ITypeInfo>()
        {
            {typeof(System.Guid), new GuidInfo(typeof(Guid))},
            {typeof(System.Guid?), new GuidInfo(typeof(Guid?))}
        };

        MetaSerializerTypeRegistry _typeRegistry;
        MetaDictionary<Type, ITypeInfo> _typeInfoCache;
        /// <summary>
        /// Mapping  T -> types_directly_contained_in_T  (per <see cref="ITypeInfo.DirectlyContainedTypes"/>).
        /// Computed in constructor and cached (used in a few places).
        /// </summary>
        MetaDictionary<Type, List<Type>> _typeDirectlyContains;

        public TaggedSerializerRoslynGenerator(MetaSerializerTypeInfo serializerTypeInfo)
        {
            _typeRegistry  = new MetaSerializerTypeRegistry(serializerTypeInfo);
            _typeInfoCache = CreateTypeInfos();

            _typeDirectlyContains = new MetaDictionary<Type, List<Type>>(capacity: _typeInfoCache.Count);
            foreach (ITypeInfo typeInfo in _typeInfoCache.Values)
                _typeDirectlyContains.Add(typeInfo.Type, typeInfo.DirectlyContainedTypes.ToList());
        }

        /// <summary>
        /// Static (de)serialization depth safety limit, checked at serializer generation time:
        /// roughly, maximum allowed depth, when not counting types that can be part of a cycle.
        /// That is not a completely precise description, but good enough, as this is meant to be
        /// large enough to not be hit in realistic projects.
        /// <para>
        /// "Depth" means roughly speaking the number of objects nested in a chain; that is the
        /// number of Deserialize_*() (or Serialize_*() or TraverseMetaRefs_*()) calls present in
        /// the call stack (by those methods, we refer to the generated per-type serialization methods
        /// used internally by the serializer).
        /// </para>
        /// <para>
        /// As noted above, this does not count types that belong in a cycle (in the graph where
        /// the nodes are types, and edges are the "type X contains object of type Y" relations).
        /// The depth for potential cycles is instead tracked at runtime, using limit
        /// <see cref="MetaSerializationSettings.MaxDepth"/> (default <see cref="MetaSerializationSettings.DefaultMaxDepth"/>).
        /// (The fact that depth is runtime-tracked _only_ for potential cycles, and not
        /// for all objects, is a performance optimization.)
        /// This static limit and the runtime limit together ensure that the factual depth at runtime
        /// does not exceed the sum of these two limits.
        /// </para>
        /// </summary>
        const int MaxAllowedDepthIgnoringTypesBelongingToCycles = 256;

        static string MemberFlagsToMask(MetaMemberFlags flags)
        {
            string[] flagsStr = EnumUtil.GetValues<MetaMemberFlags>().Where(flag => flag != 0 && flags.HasFlag(flag)).Select(v => $"MetaMemberFlags.{v}").ToArray();
            if (flagsStr.Length > 1)
                return $"({string.Join(" | ", flagsStr)})";
            else
                return flagsStr[0];
        }

        static Type NullableType(Type containedType)
        {
            return typeof(Nullable<>).MakeGenericType(containedType);
        }

        string CreateInstanceExpression(MetaSerializableType typeSpec, bool useTrampolines)
        {
            // \note Trampolines are not needed for value types, as they are always publicly default-constructible
            if (useTrampolines && !typeSpec.Type.IsValueType)
                return $"Trampolines.CreateInstance_{MethodSuffixForType(typeSpec.Type)}()";
            else
                return $"new {typeSpec.Type.ToGlobalNamespaceQualifiedTypeString()}()";
        }

        bool TryGetConverterWireTypesPredicateExpression(Type type, string varName, out string predicateStr)
        {
            if (_typeInfoCache.TryGetValue(type, out ITypeInfo typeInfo)
                && typeInfo.SerializableType?.DeserializationConverters.Length > 0)
            {
                IEnumerable<string> comparisons = typeInfo.SerializableType.DeserializationConverters.Select(converter => $"{varName} == WireDataType.{converter.AcceptedWireDataType}");
                string              expression  = string.Join(" || ", comparisons);
                predicateStr = $"({expression})";
                return true;
            }
            else
            {
                predicateStr = null;
                return false;
            }
        }

        /// <summary>
        /// Return a suffix that should be used for a serialization/deserialization
        /// method associated with the given type. This is done to give unique (or
        /// almost unique - doesn't have to be perfect) names for the methods, in
        /// order to avoid overloads. Overloads are avoided because they significantly
        /// increase the duration of compiling the generated serializer.
        /// </summary>
        string MethodSuffixForType(Type type)
        {
            return _typeInfoCache[type].GlobalNamespaceQualifiedTypeString
                .Replace("global::", "")
                .Replace("[]", "Array")
                .Replace("<", "_")
                .Replace(">", "_")
                .Replace(".", "_")
                .Replace(",", "_");
        }

        string SerializationMethodSignature(ITypeInfo typeInfo, string valueParameterName, bool spaceEnsured)
        {
            if (spaceEnsured)
                return $"static void SerializeUnchecked_{MethodSuffixForType(typeInfo.Type)}(ref MetaSerializationContext context, ref SpanWriter writer, {typeInfo.GlobalNamespaceQualifiedTypeString} {valueParameterName})";
            else
                return $"static void Serialize_{MethodSuffixForType(typeInfo.Type)}(ref MetaSerializationContext context, ref SpanWriter writer, {typeInfo.GlobalNamespaceQualifiedTypeString} {valueParameterName})";
        }

        string SerializationExpression(Type type, string valueStr, bool spaceEnsured)
        {
            if (spaceEnsured)
                return Invariant($"SerializeUnchecked_{MethodSuffixForType(type)}(ref context, ref writer, {valueStr})");
            else
                return Invariant($"Serialize_{MethodSuffixForType(type)}(ref context, ref writer, {valueStr})");
        }

        string DeserializationMethodSignature(ITypeInfo typeInfo, string valueParameterName)
        {
            return $"static void Deserialize_{MethodSuffixForType(typeInfo.Type)}(ref MetaSerializationContext context, IOReader reader, WireDataType wireType, out {typeInfo.GlobalNamespaceQualifiedTypeString} {valueParameterName})";
        }
        string DeserializationExpression(Type type, string varName, string wireTypeStr)
        {
            return Invariant($"Deserialize_{MethodSuffixForType(type)}(ref context, reader, {wireTypeStr}, out {type.ToGlobalNamespaceQualifiedTypeString()} {varName})");
        }

        string MembersSerializationMethodSignature(IMembersInfo typeInfo, string valueParameterName, bool spaceEnsured)
        {
            if (spaceEnsured)
                return $"static void SerializeMembersUnchecked_{MethodSuffixForType(typeInfo.Type)}(ref MetaSerializationContext context, ref SpanWriter writer, {typeInfo.GlobalNamespaceQualifiedTypeString} {valueParameterName})";
            else
                return $"static void SerializeMembers_{MethodSuffixForType(typeInfo.Type)}(ref MetaSerializationContext context, ref SpanWriter writer, {typeInfo.GlobalNamespaceQualifiedTypeString} {valueParameterName})";
        }

        string MembersSerializationExpression(Type type, string valueStr, bool spaceEnsured)
        {
            if (spaceEnsured)
                return $"SerializeMembersUnchecked_{MethodSuffixForType(type)}(ref context, ref writer, {valueStr})";
            else
                return $"SerializeMembers_{MethodSuffixForType(type)}(ref context, ref writer, {valueStr})";
        }
        string MembersDeserializationMethodSignature(IMembersInfo typeInfo, string valueParameterName)
        {
            return $"static void DeserializeMembers_{MethodSuffixForType(typeInfo.Type)}(ref MetaSerializationContext context, IOReader reader, out {typeInfo.GlobalNamespaceQualifiedTypeString} {valueParameterName})";
        }
        string MembersDeserializationExpression(Type type, string varName)
        {
            return $"DeserializeMembers_{MethodSuffixForType(type)}(ref context, reader, out {type.ToGlobalNamespaceQualifiedTypeString()} {varName})";
        }

        string TableSerializationMethodSignature(MetaSerializableType itemTypeSpec, string itemsParameterName)
        {
            return $"static void SerializeTable_{MethodSuffixForType(itemTypeSpec.Type)}(ref MetaSerializationContext context, ref SpanWriter writer, IReadOnlyList<{itemTypeSpec.GlobalNamespaceQualifiedTypeString}> {itemsParameterName})";
        }
        string TableSerializationExpression(Type itemType, string itemsStr)
        {
            return $"SerializeTable_{MethodSuffixForType(itemType)}(ref context, ref writer, {itemsStr})";
        }
        string TableDeserializationMethodSignature(MetaSerializableType itemTypeSpec, string itemsParameterName)
        {
            return $"static void DeserializeTable_{MethodSuffixForType(itemTypeSpec.Type)}(ref MetaSerializationContext context, IOReader reader, out List<{itemTypeSpec.GlobalNamespaceQualifiedTypeString}> {itemsParameterName})";
        }
        string TableDeserializationExpression(Type itemType, string itemsVarName)
        {
            return $"DeserializeTable_{MethodSuffixForType(itemType)}(ref context, reader, out List<{itemType.ToGlobalNamespaceQualifiedTypeString()}> {itemsVarName})";
        }

        string MetaRefTraverseMethodSignature(ITypeInfo typeInfo, string valueParameterName)
        {
            return $"static void TraverseMetaRefs_{MethodSuffixForType(typeInfo.Type)}(ref MetaSerializationContext context, {typeInfo.GlobalNamespaceQualifiedTypeString} {valueParameterName})";
        }
        string MetaRefTraverseExpression(Type type, string varName)
        {
            return $"TraverseMetaRefs_{MethodSuffixForType(type)}(ref context, {varName})";
        }
        string MembersMetaRefTraverseMethodSignature(Type type, string valueParameterName)
        {
            return $"static void TraverseMembersMetaRefs_{MethodSuffixForType(type)}(ref MetaSerializationContext context, {type.ToGlobalNamespaceQualifiedTypeString()} {valueParameterName})";
        }
        string MembersMetaRefTraverseExpression(Type type, string varName)
        {
            return $"TraverseMembersMetaRefs_{MethodSuffixForType(type)}(ref context, {varName})";
        }
        string TableMetaRefsTraverseMethodSignature(Type itemType, string itemsParameterName)
        {
            return $"static void TraverseTableMetaRefs_{MethodSuffixForType(itemType)}(ref MetaSerializationContext context, List<{itemType.ToGlobalNamespaceQualifiedTypeString()}> {itemsParameterName})";
        }
        string TableMetaRefsTraverseExpression(Type itemType, string valueStr)
        {
            return $"TraverseTableMetaRefs_{MethodSuffixForType(itemType)}(ref context, {valueStr})";
        }

        static void AppendHelpers(IndentedStringBuilder sb)
        {
            sb.AppendLine("static void WriteWireType(ref SpanWriter writer, WireDataType wireType)");
            sb.AppendLine("{");
            sb.AppendLine("    writer.WriteByte((byte)wireType);");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("static WireDataType ReadWireType(IOReader reader)");
            sb.AppendLine("{");
            sb.AppendLine("    WireDataType wireType = (WireDataType)reader.ReadByte();");
            sb.AppendLine("    return wireType;");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("static int ReadTagId(IOReader reader)");
            sb.AppendLine("{");
            sb.AppendLine("    int tagId = reader.ReadVarInt();");
            sb.AppendLine("    return tagId;");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("static void IsConvertibleTo<T>(T _)");
            sb.AppendLine("{");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("static bool IsVersionInRange(ref MetaSerializationContext context, int minVersion, int maxVersion) =>");
            sb.AppendLine("    (context.LogicVersion != null) ? (context.LogicVersion >= minVersion && context.LogicVersion < maxVersion) : true;");
            sb.AppendLine();
            sb.AppendLine("static bool IsVersionInRange(ref MetaSerializationContext context, int minVersion) =>");
            sb.AppendLine("    (context.LogicVersion != null) ? (context.LogicVersion >= minVersion) : true;");
            sb.AppendLine();
            sb.AppendLine("static byte[] ReadBytesForWireObjectStartingAt(IOReader reader, WireDataType wireType, int startOffset)");
            sb.AppendLine("{");
            sb.AppendLine("    reader.Seek(startOffset);");
            sb.AppendLine("    TaggedWireSerializer.SkipWireType(reader, wireType);");
            sb.AppendLine("    int endOffset = reader.Offset;");
            sb.AppendLine("    reader.Seek(startOffset);");
            sb.AppendLine("    byte[] bytes = new byte[endOffset - startOffset];");
            sb.AppendLine("    reader.ReadBytes(bytes);");
            sb.AppendLine("    return bytes;");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("static void ExpectWireType(ref MetaSerializationContext context, IOReader reader, Type type, WireDataType encounteredWireType, WireDataType expectedWireType)");
            sb.AppendLine("{");
            sb.AppendLine("    // Move unlikely control flow to a separate func to skip static inits.");
            sb.AppendLine("    if (encounteredWireType != expectedWireType)");
            sb.AppendLine("        OnExpectWireTypeFail(ref context, reader, type, encounteredWireType, expectedWireType);");
            sb.AppendLine("}");
            sb.AppendLine("static void OnExpectWireTypeFail(ref MetaSerializationContext context, IOReader reader, Type type, WireDataType encounteredWireType, WireDataType expectedWireType)");
            sb.AppendLine("{");
            sb.AppendLine("    TaggedWireSerializer.ExpectWireType(ref context, reader, type, encounteredWireType, expectedWireType);");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("static void SkipWireType(IOReader reader, WireDataType wireType)");
            sb.AppendLine("{");
            sb.AppendLine("    // This is unlikely control flow. Moved to a separate func to skip static inits.");
            sb.AppendLine("    TaggedWireSerializer.SkipWireType(reader, wireType);");
            sb.AppendLine("}");
        }

        internal interface ITypeInfo : DynamicTraversal.ITraversableTypeInfo
        {
            Type Type { get; }
            string GlobalNamespaceQualifiedTypeString { get; }
            IEnumerable<Type> DirectlyContainedTypes { get; }
            bool WriteToDebugStreamAtSerializationStart { get; }
            void AppendSerializationCode(IndentedStringBuilder sb, string valueExpr, bool useTrampolines, bool spaceEnsured);
            void AppendDeserializationCode(IndentedStringBuilder sb, string varName, bool useTrampolines);
            void AppendMetaRefTraverseCode(IndentedStringBuilder sb, string varName, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines);
            // Upper bound for number of serialized bytes for this type, if known at build time.
            int? MaxSerializedBytesStatic();
            MetaSerializableType SerializableType => null;
        }

        internal interface IMembersInfo : DynamicTraversal.ITraversableTypeInfo
        {
            Type Type { get; }
            string GlobalNamespaceQualifiedTypeString { get; }
            IEnumerable<Type> DirectlyContainedTypes { get; }
            void AppendMembersSerializationCode(IndentedStringBuilder sb, string valueExpr, bool useTrampolines, bool spaceEnsured);
            void AppendMembersDeserializationCode(IndentedStringBuilder sb, string varName, bool useTrampolines);
            void AppendMembersMetaRefTraverseCode(IndentedStringBuilder sb, string varName, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines);
            // Upper bound for number of serialized bytes for this members collection, if known at build time.
            int? MaxSerializedBytesStatic();
        }

        static List<ITypeInfo> GetBuiltinTypeInfos()
        {
            List<ITypeInfo> infos = new List<ITypeInfo>();

            foreach (ITypeInfo info in s_primitiveInfos)
            {
                infos.Add(info);

                if (info.Type.IsValueType)
                {
                    Type nullableType = NullableType(info.Type);
                    infos.Add(new NullablePrimitiveInfo(nullableType, info));
                }
            }

            return infos;
        }

        ITypeInfo GetSerializableTypeInfo(MetaSerializableType typeSpec)
        {
            if (typeSpec.WireType == WireDataType.ValueCollection)
                return new ValueCollectionInfo(this, typeSpec);
            else if (typeSpec.WireType == WireDataType.KeyValueCollection)
                return new KeyValueCollectionInfo(this, typeSpec);
            else if (typeSpec.IsEnum)
                return new EnumInfo(this, typeSpec);
            else if (typeSpec.IsSystemNullable && typeSpec.Type.GetSystemNullableElementType().IsEnum)
                return new NullableEnumInfo(this, typeSpec);
            else if (typeSpec.Type.ImplementsInterface<IStringId>())
                return new StringIdInfo(this, typeSpec);
            else if (typeSpec.Type.ImplementsInterface<IDynamicEnum>())
                return new DynamicEnumInfo(this, typeSpec);
            else if (typeSpec.IsGameConfigData)
                return new GameConfigDataInfo(this, typeSpec);
            else if (typeSpec.Type.IsGenericTypeOf(typeof(GameConfigDataContent<>)))
            {
                MetaSerializableType configDataContentTypeSpec = _typeRegistry.GetSerializableType(typeSpec.Type.GetGenericArguments()[0]);
                return new GameConfigDataContentInfo(this, typeSpec, configDataContentTypeSpec);
            }
            else if (typeSpec.IsMetaRef || typeSpec.IsMetaConfigId)
                return new MetaRefInfo(this, typeSpec);
            else if (typeSpec.IsObject)
            {
                if (typeSpec.Type.IsAbstract)
                    return new AbstractClassOrInterfaceInfo(this, typeSpec);
                else if (typeSpec.Type.IsClass)
                    return new ConcreteClassInfo(this, typeSpec);
                else if (typeSpec.IsSystemNullable)
                    return new NullableStructInfo(this, typeSpec);
                else
                    return new StructInfo(this, typeSpec);
            }
            else
                throw new InvalidOperationException($"Unknown type: {typeSpec.Name}");
        }

        void AppendSerializeTypeFunc(IndentedStringBuilder sb, ITypeInfo typeInfo, OrderedSet<Type> typesBelongingToCycles, bool useTrampolines, bool spaceEnsured)
        {
            const string ValueParamName = "inValue";

            sb.AppendLine();
            sb.AppendLine(SerializationMethodSignature(typeInfo, ValueParamName, spaceEnsured));
            sb.Indent("{");

            int? addedInVersion = typeInfo.SerializableType?.AddedInVersion;
            if (addedInVersion != null)
            {
                string minVersion = addedInVersion?.ToString(CultureInfo.InvariantCulture);
                sb.AppendLine($"if (!IsVersionInRange(ref context, {minVersion}))");
                sb.AppendLine(Invariant($"    throw new MetaTypeMissingInLogicVersionSerializationException(\"{typeInfo.SerializableType.Type.ToGenericTypeString()}\", context.LogicVersion, {addedInVersion});"));
                sb.AppendLine();
            }

            if (typesBelongingToCycles.Contains(typeInfo.Type))
            {
                sb.AppendLine("context.IncrementDepth();");
                sb.AppendLine();
            }

            if (typeInfo.WriteToDebugStreamAtSerializationStart && sb.DebugEnabled)
            {
                sb.AppendDebugLine($"context.DebugStream?.AppendLine($\"@{{writer.Offset}} write type {typeInfo.Type.ToNamespaceQualifiedTypeString()}\");");
                sb.AppendDebugLine();
            }

            typeInfo.AppendSerializationCode(sb, ValueParamName, useTrampolines, spaceEnsured);

            if (typesBelongingToCycles.Contains(typeInfo.Type))
            {
                sb.AppendLine();
                sb.AppendLine("context.DecrementDepth();");
            }

            sb.Unindent("}");
        }

        void AppendDeserializeTypeFunc(IndentedStringBuilder sb, ITypeInfo typeInfo, OrderedSet<Type> typesBelongingToCycles, bool useTrampolines)
        {
            const string ValueParamName = "outValue";

            sb.AppendLine();
            sb.AppendLine(DeserializationMethodSignature(typeInfo, ValueParamName));
            sb.Indent("{");

            int? addedInVersion = typeInfo.SerializableType?.AddedInVersion;
            if (addedInVersion != null)
            {
                string minVersion = addedInVersion?.ToString(CultureInfo.InvariantCulture);
                sb.AppendLine($"if (!IsVersionInRange(ref context, {minVersion}))");
                sb.AppendLine(Invariant($"    throw new MetaTypeMissingInLogicVersionSerializationException(\"{typeInfo.SerializableType.Type.ToGenericTypeString()}\", context.LogicVersion, {addedInVersion});"));
                sb.AppendLine();
            }

            if (typesBelongingToCycles.Contains(typeInfo.Type))
            {
                sb.AppendLine("context.IncrementDepth();");
                sb.AppendLine();
            }

            bool hasAnyConverters = false;
            if (typeInfo.SerializableType?.DeserializationConverters.Length > 0)
            {
                hasAnyConverters = true;

                sb.AppendLine("switch (wireType)");
                sb.Indent("{");

                foreach ((MetaDeserializationConverter converter, int converterIndex) in typeInfo.SerializableType.DeserializationConverters.ZipWithIndex())
                {
                    sb.AppendLine($"case WireDataType.{converter.AcceptedWireDataType}:");
                    sb.Indent("{");

                    if (converter.SourceDeserialization == MetaDeserializationConverter.SourceDeserializationKind.Normal)
                        sb.AppendLine($"{DeserializationExpression(converter.SourceType, "source", "wireType")};");
                    else if (converter.SourceDeserialization == MetaDeserializationConverter.SourceDeserializationKind.Members)
                    {
                        sb.AppendLine($"{MembersDeserializationExpression(converter.SourceType, "source")};");
                    }
                    else
                        throw new InvalidOperationException($"Unhandled {nameof(MetaDeserializationConverter.SourceDeserializationKind)}: {converter.SourceDeserialization}");

                    sb.AppendLine($"MetaSerializableType typeSpec = context.TypeInfo.GetSerializableType(typeof({typeInfo.GlobalNamespaceQualifiedTypeString}));");
                    sb.AppendLine($"MetaDeserializationConverter converter = typeSpec.DeserializationConverters[{converterIndex.ToString(CultureInfo.InvariantCulture)}];");
                    sb.AppendLine($"{ValueParamName} = ({typeInfo.GlobalNamespaceQualifiedTypeString})converter.Convert(source);");
                    sb.AppendLine($"break;");
                    sb.Unindent("}");
                    sb.AppendLine();
                }

                sb.AppendLine("default:");
                sb.Indent("{");
            }

            sb.AppendLine($"ExpectWireType(ref context, reader, typeof({typeInfo.GlobalNamespaceQualifiedTypeString}), wireType, WireDataType.{TaggedWireSerializer.GetWireType(typeInfo.Type, _typeRegistry)});");
            sb.AppendLine();
            typeInfo.AppendDeserializationCode(sb, ValueParamName, useTrampolines);

            if (hasAnyConverters)
            {
                sb.AppendLine("break;");
                sb.Unindent("}"); // Closes the default case
                sb.Unindent("}"); // Closes the switch
            }

            if (typesBelongingToCycles.Contains(typeInfo.Type))
            {
                sb.AppendLine();
                sb.AppendLine("context.DecrementDepth();");
            }

            sb.Unindent("}");
        }

        void AppendTraverseMetaRefsInTypeFunc(IndentedStringBuilder sb, ITypeInfo typeInfo, OrderedSet<Type> metaRefContainingTypes, OrderedSet<Type> typesBelongingToCycles, bool useTrampolines)
        {
            const string ValueParamName = "valueParam";

            sb.AppendLine();
            sb.AppendLine(MetaRefTraverseMethodSignature(typeInfo, ValueParamName));
            sb.Indent("{");

            int? addedInVersion = typeInfo.SerializableType?.AddedInVersion;
            if (addedInVersion != null)
            {
                string minVersion = addedInVersion?.ToString(CultureInfo.InvariantCulture);
                sb.AppendLine($"if (!IsVersionInRange(ref context, {minVersion}))");
                sb.AppendLine(Invariant($"    throw new MetaTypeMissingInLogicVersionSerializationException(\"{typeInfo.SerializableType.Type.ToGenericTypeString()}\", context.LogicVersion, {addedInVersion});"));
                sb.AppendLine();
            }

            if (typesBelongingToCycles.Contains(typeInfo.Type))
            {
                sb.AppendLine("context.IncrementDepth();");
                sb.AppendLine();
            }

            typeInfo.AppendMetaRefTraverseCode(sb, ValueParamName, metaRefContainingTypes, useTrampolines);

            if (typesBelongingToCycles.Contains(typeInfo.Type))
            {
                sb.AppendLine();
                sb.AppendLine("context.DecrementDepth();");
            }

            sb.Unindent("}");
        }

        void AppendSerializeMembersFunc(IndentedStringBuilder sb, IMembersInfo membersInfo, bool useTrampolines, bool spaceEnsured)
        {
            const string ValueParamName = "inValue";

            sb.AppendLine();
            sb.AppendLine(MembersSerializationMethodSignature(membersInfo, ValueParamName, spaceEnsured));
            sb.Indent("{");

            int? maxSerializedBytes = membersInfo.MaxSerializedBytesStatic();
            if (!spaceEnsured && maxSerializedBytes.HasValue && maxSerializedBytes.Value <= MaxSpanSize)
            {
                sb.AppendLine(Invariant($"writer.EnsureSpace({maxSerializedBytes.Value});"));
                sb.AppendLine(MembersSerializationExpression(membersInfo.Type, ValueParamName, true) + ";");
            }
            else
            {
                membersInfo.AppendMembersSerializationCode(sb, ValueParamName, useTrampolines, spaceEnsured);
            }
            sb.Unindent("}");
        }

        void AppendDeserializeMembersFunc(IndentedStringBuilder sb, IMembersInfo membersInfo, bool useTrampolines)
        {
            const string ValueParamName = "outValue";

            sb.AppendLine();
            sb.AppendLine(MembersDeserializationMethodSignature(membersInfo, ValueParamName));
            sb.Indent("{");
            membersInfo.AppendMembersDeserializationCode(sb, ValueParamName, useTrampolines);
            sb.Unindent("}");
        }

        void AppendTraverseMetaRefsInTypeMembersFunc(IndentedStringBuilder sb, IMembersInfo membersInfo, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines)
        {
            const string ValueParamName = "valueParam";

            sb.AppendLine();
            sb.AppendLine(MembersMetaRefTraverseMethodSignature(membersInfo.Type, ValueParamName));
            sb.Indent("{");
            membersInfo.AppendMembersMetaRefTraverseCode(sb, ValueParamName, metaRefContainingTypes, useTrampolines);
            sb.Unindent("}");
        }

        static bool IsSerializableByMembers(MetaSerializableType typeSpec)
        {
            if (typeSpec.IsTuple)
                return true;

            if (MetaSerializerTypeScanner.TypeMemberOverrides.ContainsKey(typeSpec.Type))
                return true;

            return typeSpec.IsObject
                && !typeSpec.IsAbstract
                && !typeSpec.IsSystemNullable
                && !typeSpec.IsGameConfigDataContent
                && typeSpec.HasSerializableAttribute;
        }

        void AppendMembersSerializationDelegates(IndentedStringBuilder sb, IEnumerable<MetaSerializableType> allTypes)
        {
            // Generate getter trampolines for all members of all known types
            sb.AppendLine();
            sb.AppendLine("// Member getters");
            sb.AppendLine();

            foreach (MetaSerializableType typeSpec in allTypes)
            {
                if (IsSerializableByMembers(typeSpec))
                {
                    foreach (MetaSerializableMember member in typeSpec.Members)
                    {
                        if ((typeSpec.Type.FindNonPublicComponent() ?? member.Type.FindNonPublicComponent()) is Type nonPrivateComponentType)
                            throw new InvalidOperationException($"Cannot create getter delegate for {typeSpec.Name} field {member.Name}. Reason: {nonPrivateComponentType.Name} is private");

                        string typeName = typeSpec.GlobalNamespaceQualifiedTypeString;
                        string overloadDiscriminator = MethodSuffixForType(typeSpec.Type);
                        string memberTypeName = member.CodeGenInfo.GlobalNamespaceQualifiedTypeString;
                        sb.AppendLine($"internal static GetMemberDelegate<{typeName}, {memberTypeName}> Get_{overloadDiscriminator}_{member.Name} = MemberAccessGenerator.GenerateGetMember<{typeName}, {memberTypeName}>(\"{member.Name}\");");
                    }
                }
            }
        }

        void AppendMembersDeserializationDelegates(IndentedStringBuilder sb, IEnumerable<MetaSerializableType> allTypes)
        {
            // Generate setter trampolines for all members of all known types, as well as [MetaOnDeserialized] and [MetaOnMemberDeserializationFailure] method invocation trampolines
            sb.AppendLine();
            sb.AppendLine("// Member setters");
            sb.AppendLine();

            foreach (MetaSerializableType typeSpec in allTypes)
            {
                if (!IsSerializableByMembers(typeSpec))
                    continue;

                foreach (MetaSerializableMember member in typeSpec.Members)
                {
                    if ((typeSpec.Type.FindNonPublicComponent() ?? member.Type.FindNonPublicComponent()) is Type nonPrivateComponentType)
                        throw new InvalidOperationException($"Cannot create setter delegate for {typeSpec.Name} field {member.Name}. Reason: {nonPrivateComponentType.Name} is private");

                    if (typeSpec.UsesConstructorDeserialization && typeSpec.ConstructorInitializedMembers.Contains(member))
                        continue;

                    string typeName = typeSpec.GlobalNamespaceQualifiedTypeString;
                    string overloadDiscriminator = MethodSuffixForType(typeSpec.Type);
                    string memberTypeName = member.CodeGenInfo.GlobalNamespaceQualifiedTypeString;
                    sb.AppendLine($"internal static SetMemberDelegate<{typeName}, {memberTypeName}> Set_{overloadDiscriminator}_{member.Name} = MemberAccessGenerator.GenerateSetMember<{typeName}, {memberTypeName}>(\"{member.Name}\");");
                }
            }

            sb.AppendLine();
            sb.AppendLine("// [MetaOnDeserialized] method invokers");
            sb.AppendLine();

            foreach (MetaSerializableType typeSpec in allTypes)
            {
                if (IsSerializableByMembers(typeSpec))
                {
                    foreach (MethodInfo onDeserializedMethod in typeSpec.OnDeserializedMethods)
                    {
                        string typeName = typeSpec.GlobalNamespaceQualifiedTypeString;
                        string overloadDiscriminator = MethodSuffixForType(typeSpec.Type);

                        if (onDeserializedMethod.GetParameters().Any())
                            sb.AppendLine($"internal static InvokeOnDeserializedMethodWithParamsDelegate<{typeName}> InvokeOnDeserializedWithParams_{overloadDiscriminator}_{onDeserializedMethod.Name} = MemberAccessGenerator.GenerateInvokeOnDeserializedMethodWithParams<{typeName}>(\"{onDeserializedMethod.Name}\");");
                        else
                            sb.AppendLine($"internal static InvokeOnDeserializedMethodDelegate<{typeName}> InvokeOnDeserialized_{overloadDiscriminator}_{onDeserializedMethod.Name} = MemberAccessGenerator.GenerateInvokeOnDeserializedMethod<{typeName}>(\"{onDeserializedMethod.Name}\");");
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine("// [MetaOnMemberDeserializationFailure] method invokers");
            sb.AppendLine();

            foreach (MetaSerializableType typeSpec in allTypes)
            {
                if (IsSerializableByMembers(typeSpec))
                {
                    foreach (MetaSerializableMember memberSpec in typeSpec.Members)
                    {
                        if (memberSpec.OnDeserializationFailureMethod == null)
                            continue;

                        MethodInfo method = memberSpec.OnDeserializationFailureMethod;
                        string typeName = typeSpec.GlobalNamespaceQualifiedTypeString;
                        string overloadDiscriminator = MethodSuffixForType(typeSpec.Type);
                        string resultTypeStr = method.ReturnType.ToGlobalNamespaceQualifiedTypeString();
                        sb.AppendLine($"internal static InvokeOnMemberDeserializationFailureMethodDelegate<{resultTypeStr}> InvokeOnMemberDeserializationFailure_{overloadDiscriminator}_{memberSpec.Name} = MemberAccessGenerator.GenerateInvokeOnMemberDeserializationFailureMethod<{resultTypeStr}>(typeof({typeName}), \"{memberSpec.Name}\");");
                    }
                }
            }
        }

        void AppendCreateInstanceDelegates(IndentedStringBuilder sb, IEnumerable<MetaSerializableType> allTypes)
        {
            // Generate instance creation trampolines for concrete reference types.
            // Not needed for value types; those are always publicly default-constructible.
            // MetaRefs do not have a constructor and need to be resolved using its key type.
            sb.AppendLine();
            sb.AppendLine("// Object instance creators");
            sb.AppendLine();

            foreach (MetaSerializableType typeSpec in allTypes)
            {
                if (!typeSpec.IsObject)
                    continue;
                if (typeSpec.Type.IsValueType && !typeSpec.UsesConstructorDeserialization)
                    continue;
                if (typeSpec.IsAbstract)
                    continue;
                if (typeSpec.IsMetaRef)
                    continue;
                if (typeSpec.IsMetaConfigId)
                    continue;

                string typeName = typeSpec.GlobalNamespaceQualifiedTypeString;
                string typeSuffix = MethodSuffixForType(typeSpec.Type);
                if (typeSpec.UsesConstructorDeserialization)
                {
                    IEnumerable<string> parameterTypes = typeSpec.ConstructorInitializedMembers.Select(x => x.Type).Select(x => $"typeof({x.ToGlobalNamespaceQualifiedTypeString()})");
                    sb.AppendLine($"internal static CreateInstanceWithParametersDelegate<{typeName}> CreateInstance_{typeSuffix} = MemberAccessGenerator.GenerateCreateInstanceWithParameters<{typeName}>({string.Join(", ", parameterTypes)});");
                }
                else
                    sb.AppendLine($"internal static CreateInstanceDelegate<{typeName}> CreateInstance_{typeSuffix} = MemberAccessGenerator.GenerateCreateInstance<{typeName}>();");
            }
        }

        void AppendSerializeObject(IndentedStringBuilder sb, IEnumerable<MetaSerializableType> allTypes)
        {
            // Generate dynamic-typed SerializeObject(context, writer, Type type, object obj)
            sb.AppendLine();
            sb.AppendLine("public static void SerializeObject(ref MetaSerializationContext context, ref SpanWriter writer, Type type, object obj)");
            sb.Indent("{");
            foreach (MetaSerializableType typeSpec in allTypes)
            {
                sb.AppendLine($"if (type == typeof({typeSpec.GlobalNamespaceQualifiedTypeString}))");
                sb.Indent("{");
                sb.AppendLine($"WriteWireType(ref writer, WireDataType.{typeSpec.WireType});");
                sb.AppendLine($"{SerializationExpression(typeSpec.Type, $"({typeSpec.GlobalNamespaceQualifiedTypeString})obj", false)};");
                sb.AppendLine("return;");
                sb.Unindent("}");
            }

            sb.AppendLine("throw new InvalidOperationException($\"SerializeObject(): Unsupported type {type.ToGenericTypeString()}\");");
            sb.Unindent("}");
        }

        void AppendDeserializeObject(IndentedStringBuilder sb, IEnumerable<MetaSerializableType> allTypes)
        {
            // Generate dynamic-typed DeserializeObject(context, writer, Type type, object obj)
            sb.AppendLine();
            sb.AppendLine("public static object DeserializeObject(ref MetaSerializationContext context, IOReader reader, Type type)");
            sb.Indent("{");
            sb.AppendLine("WireDataType wireType = ReadWireType(reader);");
            sb.AppendLine();
            foreach (MetaSerializableType typeSpec in allTypes)
                sb.AppendLine($"if (type == typeof({typeSpec.GlobalNamespaceQualifiedTypeString})) {{ {DeserializationExpression(typeSpec.Type, "obj", "wireType")}; return obj; }}");
            sb.AppendLine("throw new InvalidOperationException($\"DeserializeObject(): Unsupported type {type.ToGenericTypeString()}\");");
            sb.Unindent("}");
        }

        void AppendTraverseMetaRefsInObject(IndentedStringBuilder sb, IEnumerable<MetaSerializableType> allTypes, OrderedSet<Type> metaRefContainingTypes)
        {
            // Generate dynamic-typed TraverseMetaRefsInObject(context, Type type, object obj)
            sb.AppendLine();
            sb.AppendLine("public static void TraverseMetaRefsInObject(ref MetaSerializationContext context, Type type, object obj)");
            sb.Indent("{");
            foreach (MetaSerializableType typeSpec in allTypes)
            {
                if (metaRefContainingTypes.Contains(typeSpec.Type))
                    sb.AppendLine($"if (type == typeof({typeSpec.GlobalNamespaceQualifiedTypeString})) {{ {MetaRefTraverseExpression(typeSpec.Type, $"({typeSpec.GlobalNamespaceQualifiedTypeString})obj")}; return; }}");
                else
                    sb.AppendLine($"if (type == typeof({typeSpec.GlobalNamespaceQualifiedTypeString})) {{ /* This type does not contain MetaRefs */ return; }}");
            }
            sb.AppendLine("throw new InvalidOperationException($\"TraverseMetaRefsInObject(): Unsupported type {type.ToGenericTypeString()}\");");
            sb.Unindent("}");
        }

        static bool IsTableSerializable(MetaSerializableType typeSpec)
        {
            // Only generate table-serialization for concrete objects implementing IGameConfigData (and must also be members-serializable)
            return typeSpec.IsConcreteObject
                && typeSpec.Type.ImplementsInterface<IGameConfigData>()
                && IsSerializableByMembers(typeSpec);
        }

        void AppendSerializeTable(IndentedStringBuilder sb, IEnumerable<MetaSerializableType> allTypes)
        {
            // Generate dynamic-typed SerializeTable(context, writer, List<T> items)
            foreach (MetaSerializableType typeSpec in allTypes)
            {
                if (IsTableSerializable(typeSpec))
                {
                    sb.AppendLine();
                    sb.AppendLine(TableSerializationMethodSignature(typeSpec, "items"));
                    sb.Indent("{");
                    if (sb.DebugEnabled)
                        sb.AppendDebugLine($"context.DebugStream?.AppendLine($\"@{{writer.Offset}} write type Table<{typeSpec.Name}>\");");
                    sb.AppendLine("if (items != null)");
                    sb.AppendLine("{");

                    sb.AppendLine("    if (items.Count > context.MemberContext.MaxCollectionSize)");
                    sb.AppendLine("        throw new InvalidOperationException($\"Invalid value collection size {items.Count} (maximum allowed is {context.MemberContext.MaxCollectionSize})\");");

                    sb.AppendLine("    writer.WriteVarInt(items.Count);");
                    sb.AppendLine($"    foreach ({typeSpec.GlobalNamespaceQualifiedTypeString} item in items)");
                    sb.AppendLine($"        {MembersSerializationExpression(typeSpec.Type, "item", false)};");
                    sb.AppendLine("}");
                    sb.AppendLine("else");
                    sb.AppendLine("    writer.WriteVarIntConst(VarIntConst.MinusOne);");
                    sb.Unindent("}");
                }
            }

            // Generate dynamic-typed SerializeTable(context, writer, Type type, object obj)
            sb.AppendLine();
            sb.AppendLine("public static void SerializeTable(ref MetaSerializationContext context, ref SpanWriter writer, Type itemType, object obj, int maxCollectionSizeOverride)");
            sb.Indent("{");
            sb.AppendLine("context.MemberContext.UpdateCollectionSize(maxCollectionSizeOverride);");
            sb.AppendLine($"WriteWireType(ref writer, WireDataType.ObjectTable);");
            sb.AppendLine();
            foreach (MetaSerializableType typeSpec in allTypes)
            {
                if (IsTableSerializable(typeSpec))
                    sb.AppendLine($"if (itemType == typeof({typeSpec.GlobalNamespaceQualifiedTypeString})) {{ {TableSerializationExpression(typeSpec.Type, $"(IReadOnlyList<{typeSpec.GlobalNamespaceQualifiedTypeString}>)obj")}; return; }}");
            }
            sb.AppendLine("throw new InvalidOperationException($\"SerializeTable(): Unsupported table item type {itemType.ToGenericTypeString()} -- the type must be a concrete class, must implement IGameConfigData<>, and have [MetaSerializable] attribute\");");
            sb.Unindent("}");
        }

        void AppendDeserializeTable(IndentedStringBuilder sb, IEnumerable<MetaSerializableType> allTypes, bool useTrampolines)
        {
            // Generate dynamic-typed DeserializeTable(context, writer, List<T> items)
            foreach (MetaSerializableType typeSpec in allTypes)
            {
                if (IsTableSerializable(typeSpec))
                {
                    sb.AppendLine();
                    sb.AppendLine(TableDeserializationMethodSignature(typeSpec, "items"));
                    sb.Indent("{");
                    sb.AppendLine("int count = reader.ReadVarInt();");
                    sb.AppendLine("if (count < -1 || count > context.MemberContext.MaxCollectionSize)");
                    sb.AppendLine("    throw new InvalidOperationException($\"Invalid value collection size {count} (maximum allowed is {context.MemberContext.MaxCollectionSize})\");");
                    sb.AppendLine("else if (count == -1)");
                    sb.AppendLine("    items = null;");
                    sb.AppendLine("else");
                    sb.AppendLine("{");
                    sb.AppendLine($"    items = new List<{typeSpec.GlobalNamespaceQualifiedTypeString}>(count);");
                    sb.AppendLine("    for (int ndx = 0; ndx < count; ndx++)");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        {MembersDeserializationExpression(typeSpec.Type, "item")};");
                    sb.AppendLine("        items.Add(item);");
                    sb.AppendLine("    }");
                    sb.AppendLine("}");
                    sb.Unindent("}");
                }
            }

            // Generate dynamic-typed DeserializeTable(context, writer, Type type, object obj)
            sb.AppendLine();
            sb.AppendLine("public static object DeserializeTable(ref MetaSerializationContext context, IOReader reader, Type itemType, int maxCollectionSizeOverride)");
            sb.Indent("{");
            sb.AppendLine("WireDataType wireType = ReadWireType(reader);");
            sb.AppendLine("ExpectWireType(ref context, reader, itemType, wireType, WireDataType.ObjectTable);");
            sb.AppendLine("context.MemberContext.UpdateCollectionSize(maxCollectionSizeOverride);");
            sb.AppendLine();
            foreach (MetaSerializableType typeSpec in allTypes)
            {
                if (IsTableSerializable(typeSpec))
                {
                    sb.AppendLine($"if (itemType == typeof({typeSpec.GlobalNamespaceQualifiedTypeString})) {{ {TableDeserializationExpression(typeSpec.Type, "obj")}; return obj; }}");
                }
            }
            sb.AppendLine("throw new InvalidOperationException($\"DeserializeTable(): Unsupported table item type {itemType.ToGenericTypeString()} -- the type must be a concrete class, must implement IGameConfigData<>, and have [MetaSerializable] attribute\");");
            sb.Unindent("}");
        }

        void AppendTraverseMetaRefsInTable(IndentedStringBuilder sb, IEnumerable<MetaSerializableType> allTypes, OrderedSet<Type> metaRefByMembersContainingTypes)
        {
            // Generate internal statically-typed table MetaRef traversing methods
            foreach (MetaSerializableType typeSpec in allTypes)
            {
                if (IsTableSerializable(typeSpec) && metaRefByMembersContainingTypes.Contains(typeSpec.Type))
                {
                    sb.AppendLine();
                    sb.AppendLine(TableMetaRefsTraverseMethodSignature(typeSpec.Type, "items"));
                    sb.Indent("{");
                    sb.AppendLine("if (items != null)");
                    sb.Indent("{");
                    sb.AppendLine($"foreach ({typeSpec.GlobalNamespaceQualifiedTypeString} item in items)");
                    sb.Indent("{");
                    sb.AppendLine($"context.MetaRefTraversal.VisitTableTopLevelConfigItem?.Invoke(ref context, item);");
                    sb.AppendLine($"{MembersMetaRefTraverseExpression(typeSpec.Type, "item")};");
                    sb.Unindent("}");
                    sb.Unindent("}");
                    sb.Unindent("}");
                }
            }

            // Generate dynamic-typed TraverseMetaRefsInTable(context, writer, Type type, object obj)
            sb.AppendLine();
            sb.AppendLine("public static void TraverseMetaRefsInTable(ref MetaSerializationContext context, Type itemType, object obj)");
            sb.Indent("{");
            foreach (MetaSerializableType typeSpec in allTypes)
            {
                if (IsTableSerializable(typeSpec))
                {
                    if (metaRefByMembersContainingTypes.Contains(typeSpec.Type))
                        sb.AppendLine($"if (itemType == typeof({typeSpec.GlobalNamespaceQualifiedTypeString})) {{ {TableMetaRefsTraverseExpression(typeSpec.Type, $"(List<{typeSpec.GlobalNamespaceQualifiedTypeString}>)obj")}; return; }}");
                    else
                        sb.AppendLine($"if (itemType == typeof({typeSpec.GlobalNamespaceQualifiedTypeString})) {{ /* This type's members do not contain MetaRefs */ return; }}");
                }
            }
            sb.AppendLine("throw new InvalidOperationException($\"TraverseMetaRefsInTable(): Unsupported table item type {itemType.ToGenericTypeString()} -- the type must be a concrete class, must implement IGameConfigData<>, and have [MetaSerializable] attribute\");");
            sb.Unindent("}");
        }

        static void AppendILCompatibilityInfoComments(IndentedStringBuilder sb, IEnumerable<MetaSerializableType> allTypes)
        {
            // Add some comments with info that is relevant for generated IL but is not visible
            // in the actual generated code (other than in these generated comments).
            // Without these comments, the serializer cache would erroneously keep the
            // old serializer dll when the only changes to the serializable types have been
            // changes that do not change the code proper.
            //
            // Such relevant information is:
            // - whether a member is a field or a property (accesses in c# code are identical,
            //   but generated IL differs)

            sb.AppendLine();
            sb.AppendLine("// Additional IL compatibility info for serializer caching");
            sb.AppendLine();

            foreach (MetaSerializableType typeSpec in allTypes)
            {
                if (IsSerializableByMembers(typeSpec))
                {
                    sb.AppendLine($"// type {typeSpec.GlobalNamespaceQualifiedTypeString}");
                    foreach (MetaSerializableMember member in typeSpec.Members)
                    {
                        string memberKind = member.MemberInfo.MemberType.ToString();
                        sb.AppendLine($"// {memberKind} {member.Name}");
                    }
                }
            }
        }

        static string CommaSuffixForListElementMaybe(int index, int listLength)
        {
            return index < listLength - 1 ? "," : "";
        }

        static string QuoteString(string s)
        {
            return s == null ? null : $"\"{s}\"";
        }

        static void AppendRuntimeTypeInfo(IndentedStringBuilder sb, IEnumerable<MetaSerializableType> allTypes, uint typeHash)
        {
            sb.AppendLine();
            sb.AppendLine("// Built-in MetaSerializable type info. Used by RuntimeTypeInfoProvider for populating the MetaSerializableTypeRegistry.");
            sb.AppendLine("// The type info construction is split into multiple methods because WASM build duration seems scale badly with respect to method size.");
            sb.AppendLine();

            // The creation of the type infos is split into multiple methods, because WASM build
            // gets very slow if there is just one very long method. Anecdotally, a build time of
            // 30 minutes was decreased to 10 minutes by splitting into multiple methods
            // (that was an incremental build after only changing a MetaMember tag id).

            const int numTypesPerMethod = 20;
            int numMethods = (allTypes.Count() + numTypesPerMethod - 1) / numTypesPerMethod; // divide, round up

            // Write sub-methods

            IEnumerable<MetaSerializableType> typesCursor = allTypes;
            for (int methodNdx = 0; methodNdx < numMethods; methodNdx++)
            {
                sb.AppendLine(Invariant($"static void GetTypeInfoSubMethod{methodNdx}(Dictionary<Type, MetaSerializableType> types)"));
                sb.Indent("{");

                foreach (MetaSerializableType typeSpec in typesCursor.Take(numTypesPerMethod))
                {
                    sb.Indent($"types.Add(typeof({typeSpec.GlobalNamespaceQualifiedTypeString}), MetaSerializableType.CreateFromBuiltinData(");
                    sb.AppendLine($"typeof({typeSpec.GlobalNamespaceQualifiedTypeString}),");
                    sb.AppendLine($"{(typeSpec.IsPublic ? "true" : "false")},");
                    sb.AppendLine($"{(typeSpec.UsesImplicitMembers ? "true" : "false")},");
                    sb.AppendLine($"(WireDataType){(uint)typeSpec.WireType},");
                    sb.AppendLine($"{(typeSpec.HasSerializableAttribute ? "true" : "false")},");
                    sb.AppendLine(Invariant($"{typeSpec.TypeCode},"));
                    if (typeSpec.Members == null)
                    {
                        sb.AppendLine("null,");
                    }
                    else
                    {
                        sb.AppendLine($"new MetaSerializableType.MemberBuiltinData[]");
                        sb.Indent("{");
                        foreach ((MetaSerializableMember member, int index) in typeSpec.Members.ZipWithIndex())
                        {
                            sb.AppendLine(Invariant($"new MetaSerializableType.MemberBuiltinData({member.TagId}, \"{member.Name}\", ") +
                                          $"(MetaMemberFlags){(uint)member.Flags}, typeof({member.MemberInfo.DeclaringType.ToGlobalNamespaceQualifiedTypeString()}), " +
                                          $"{QuoteString(member.OnDeserializationFailureMethodName) ?? "null"}){CommaSuffixForListElementMaybe(index, typeSpec.Members.Count)}");
                        }
                        sb.Unindent("},");
                    }
                    if (typeSpec.DerivedTypes == null)
                    {
                        sb.AppendLine("null,");
                    }
                    else
                    {
                        sb.AppendLine($"new ValueTuple<int, Type>[]");
                        sb.Indent("{");
                        int idx = 0;
                        foreach ((int typeCode, Type type) in typeSpec.DerivedTypes)
                        {
                            sb.AppendLine(Invariant($"({typeCode}, typeof({type.ToGlobalNamespaceQualifiedTypeString()})){CommaSuffixForListElementMaybe(idx++, typeSpec.DerivedTypes.Count)}"));
                        }
                        sb.Unindent("},");
                    }
                    if (typeSpec.OnDeserializedMethods == null)
                    {
                        sb.AppendLine("null,");
                    }
                    else
                    {
                        sb.AppendLine($"new ValueTuple<Type, string>[]");
                        sb.Indent("{");
                        int idx = 0;
                        foreach (MethodInfo m in typeSpec.OnDeserializedMethods)
                        {
                            sb.AppendLine($"(typeof({m.DeclaringType.ToGlobalNamespaceQualifiedTypeString()}), \"{m.Name}\"){CommaSuffixForListElementMaybe(idx++, typeSpec.OnDeserializedMethods.Count)}");
                        }
                        sb.Unindent("},");
                    }
                    sb.AppendLine(typeSpec.ConfigNullSentinelKey == null ? "false," : "true,");
                    sb.AppendLine(typeSpec.DeserializationConverters == null || typeSpec.DeserializationConverters.Length == 0 ? "false," : "true,");
                    sb.AppendLine(typeSpec.UsesConstructorDeserialization ? "true," : "false,");
                    sb.AppendLine(typeSpec.AddedInVersion != null ? Invariant($"{typeSpec.AddedInVersion.Value}") : "null");
                    sb.Unindent("));");
                }
                typesCursor = typesCursor.Skip(numTypesPerMethod);

                sb.Unindent("}");
            }
            MetaDebug.Assert(typesCursor.Count() == 0, "Did not generate GetTypeInfo code for all types");

            // Write top-level method which calls the sub-methods (and returns type hash)

            sb.AppendLine("public static uint GetTypeInfo(Dictionary<Type, MetaSerializableType> types)");
            sb.Indent("{");
            sb.AppendLine(Invariant($"types.EnsureCapacity({allTypes.Count()});"));

            for (int methodNdx = 0; methodNdx < numMethods; methodNdx++)
                sb.AppendLine(Invariant($"GetTypeInfoSubMethod{methodNdx}(types);"));

            sb.AppendLine($"return {typeHash};");
            sb.Unindent("}");
        }

        public readonly struct GeneratedSource
        {
            public readonly string Source;
            public readonly OrderedSet<Assembly> ReferencedAssemblies;

            public GeneratedSource(string source, OrderedSet<Assembly> referencedAssemblies)
            {
                Source = source;
                ReferencedAssemblies = referencedAssemblies;
            }
        }

        public GeneratedSource GenerateSerializerCode(bool useMemberAccessTrampolines, bool generateRuntimeTypeInfo)
        {
            Assembly[] forcedDependencies = new Assembly[]
            {
                // Cloud builds have ActorRefs as a builtin
                #if NETCOREAPP
                typeof(Akka.Actor.IActorRef).Assembly,
                #endif

                // Always add a dependency to Metaplay.Cloud
                typeof(MetaplayCore).Assembly,
                typeof(MetaMemberFlags).Assembly,
            };

            OrderedSet<Assembly> assemblies = GetAssembliesForTypes(_typeRegistry.AllTypes, forcedDependencies);
            string source = GenerateSerializerCodeImpl(useMemberAccessTrampolines, generateRuntimeTypeInfo, assemblies);
            return new GeneratedSource(source, assemblies);
        }

        /// <summary>
        /// Returns all assemblies referenced by the types.
        /// </summary>
        static OrderedSet<Assembly> GetAssembliesForTypes(IEnumerable<MetaSerializableType> types, Assembly[] forcedDependencies)
        {
            OrderedSet<Assembly> allAssemblies = new OrderedSet<Assembly>();
            HashSet<Type> seenTypes = new HashSet<Type>();
            Queue<Type> queue = new Queue<Type>();

            foreach (MetaSerializableType type in types)
                queue.Enqueue(type.Type);

            while (queue.TryDequeue(out Type type))
            {
                if (!seenTypes.Add(type))
                    continue;

                // We only need to check interfaces and base type. All fields
                // are MetaSerializable too so their assemblies will get handled
                // automatically.

                allAssemblies.Add(type.Assembly);

                foreach (Type interfaceType in type.GetInterfaces())
                    queue.Enqueue(interfaceType);
                if (type.BaseType != null)
                    queue.Enqueue(type.BaseType);
            }

            foreach (Assembly assembly in forcedDependencies)
                allAssemblies.Add(assembly);

            return allAssemblies;
        }

        internal MetaDictionary<Type, ITypeInfo> CreateTypeInfos()
        {
            MetaDictionary<Type, ITypeInfo> typeInfos = GetBuiltinTypeInfos().ToMetaDictionary(x => x.Type);
            foreach (MetaSerializableType t in _typeRegistry.AllTypes)
                typeInfos.Add(t.Type, GetSerializableTypeInfo(t));

            foreach ((Type key, ITypeInfo value) in s_additionalTypeInfos)
                typeInfos.Add(key, value);

            return typeInfos;
        }

        internal IEnumerable<IMembersInfo> CreateMembersInfos()
        {
            return
                _typeRegistry.AllTypes
                .Where(IsSerializableByMembers)
                .Select(typeSpec => (IMembersInfo)new ConcreteClassOrStructMembersInfo(this, typeSpec));
        }

        string GenerateSerializerCodeImpl(bool useMemberAccessTrampolines, bool generateRuntimeTypeInfo, IEnumerable<Assembly> assemblies)
        {
            IndentedStringBuilder sb = new IndentedStringBuilder(outputDebugCode: false);

            // Collect all namespaces used in the types
            HashSet<string> namespaces = new HashSet<string>();
            namespaces.Add("System");
            namespaces.Add("System.Collections.Generic");
            namespaces.Add("Metaplay.Core.IO");
            namespaces.Add("Metaplay.Core.Serialization"); // for MemberAccessGenerator
            namespaces.Add("Metaplay.Core.Model"); // for MetaMemberFlags
            namespaces.Add("Metaplay.Core.Config"); // for IGameConfigData
            namespaces.Add("Metaplay.Core"); // for ToGenericTypeString

            //foreach (MetaSerializableType typeSpec in allTypes)
            //{
            //    if (!string.IsNullOrEmpty(typeSpec.Type.Namespace))
            //        namespaces.Add(typeSpec.Type.Namespace);
            //}

            sb.AppendLine("// MACHINE-GENERATED CODE, DO NOT MODIFY");
            sb.AppendLine();

            foreach (string ns in namespaces.OrderBy(v => v, StringComparer.Ordinal))
                sb.AppendLine($"using {ns};");
            sb.AppendLine();

            List<string> assemblyNames = new List<string>();
            foreach (Assembly assembly in assemblies)
                assemblyNames.Add(assembly.GetName().Name);
            assemblyNames.Sort(StringComparer.Ordinal);
            foreach (string assemblyName in assemblyNames)
                sb.AppendLine($"[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo(\"{assemblyName}\")]");
            sb.AppendLine();

            sb.AppendLine("namespace System.Runtime.CompilerServices");
            sb.AppendLine("{");
            sb.AppendLine("    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]");
            sb.AppendLine("    internal sealed class IgnoresAccessChecksToAttribute : Attribute");
            sb.AppendLine("    {");
            sb.AppendLine("        public string AssemblyName { get; }");
            sb.AppendLine("        public IgnoresAccessChecksToAttribute(string assemblyName) => AssemblyName = assemblyName;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("namespace Metaplay.Generated");
            sb.Indent("{");

            sb.AppendLine("public static class TypeSerializer");
            sb.Indent("{");

            AppendHelpers(sb);

            IEnumerable<ITypeInfo> typeInfos = _typeInfoCache.Values;

            // Compute relevant information about the type graph, used for depth limit checks:
            // - nodes belonging to cycles
            // - maximum path, ignoring types belonging to cycles (roughly speaking - see below)
            SerializerGeneratorGraphUtil.GraphInfo<Type> typeGraphInfo = SerializerGeneratorGraphUtil.ResolveGraphInfo(_typeDirectlyContains);

            // Debug
#if false
            DebugLog.Info("{0} is the maximum path length in the graph composed of the strongly-connected components of the type graph.", typeGraphInfo.MaxPathLengthInComponentGraph);
            DebugLog.Info("{0} types belong to a cycle:", typeGraphInfo.NodesBelongingToCycles.Count);
            foreach (Type type in typeGraphInfo.NodesBelongingToCycles)
                DebugLog.Info("  {0}", type);
#endif

            // \note "Max path length in strongly connected component graph" is not exactly the same as "max depth, ignoring types belonging to cycles",
            //       because the cycles _are_ still represented in the component graph -- just as single nodes, thus each component
            //       counting as 1. So this has essentially off-by-1 per each component with cycles. But this should be OK in practice,
            //       this shouldn't get anywhere close to the limit anyway.
            if (typeGraphInfo.MaxPathLengthInComponentGraph > MaxAllowedDepthIgnoringTypesBelongingToCycles)
                throw new InvalidOperationException($"Statically detected possible serialization depth {typeGraphInfo.MaxPathLengthInComponentGraph} exceeds safety limit {MaxAllowedDepthIgnoringTypesBelongingToCycles}");

            OrderedSet<Type> typesBelongingToCycles = typeGraphInfo.NodesBelongingToCycles;

            IEnumerable<IMembersInfo> membersInfos = CreateMembersInfos();

            GetMetaRefContainingTypes(
                typeInfos,
                out OrderedSet<Type> metaRefContainingTypes,
                out OrderedSet<Type> metaRefByMembersContainingTypes);

            sb.AppendLine();
            sb.AppendLine("// Type serializers, deserializers, and MetaRef traversers");
            foreach (ITypeInfo info in typeInfos)
            {
                AppendSerializeTypeFunc(sb, info, typesBelongingToCycles, useMemberAccessTrampolines, false);
                int? maxSerializedBytes = info.MaxSerializedBytesStatic();
                if (maxSerializedBytes.HasValue && maxSerializedBytes <= MaxSpanSize)
                    AppendSerializeTypeFunc(sb, info, typesBelongingToCycles, useMemberAccessTrampolines, true);
                AppendDeserializeTypeFunc(sb, info, typesBelongingToCycles, useMemberAccessTrampolines);

                if (metaRefContainingTypes.Contains(info.Type))
                    AppendTraverseMetaRefsInTypeFunc(sb, info, metaRefContainingTypes, typesBelongingToCycles, useMemberAccessTrampolines);
            }

            sb.AppendLine();
            sb.AppendLine("// Members serializers, deserializers, and MetaRef traversers");
            foreach (IMembersInfo info in membersInfos)
            {
                AppendSerializeMembersFunc(sb, info, useMemberAccessTrampolines, false);
                int? maxSerializedBytes = info.MaxSerializedBytesStatic();
                if (maxSerializedBytes.HasValue && maxSerializedBytes <= MaxSpanSize)
                    AppendSerializeMembersFunc(sb, info, useMemberAccessTrampolines, true);
                AppendDeserializeMembersFunc(sb, info, useMemberAccessTrampolines);

                if (metaRefByMembersContainingTypes.Contains(info.Type))
                    AppendTraverseMetaRefsInTypeMembersFunc(sb, info, metaRefContainingTypes, useMemberAccessTrampolines);
            }

            IEnumerable<MetaSerializableType> allTypes = _typeRegistry.AllTypes;
            uint typeHash = _typeRegistry.FullProtocolHash;

            sb.AppendLine();
            sb.AppendLine("// Table serializers");
            AppendSerializeTable(sb, allTypes);

            sb.AppendLine();
            sb.AppendLine("// Table deserializers");
            AppendDeserializeTable(sb, allTypes, useMemberAccessTrampolines);

            sb.AppendLine();
            sb.AppendLine("// Table MetaRef traversers");
            AppendTraverseMetaRefsInTable(sb, allTypes, metaRefByMembersContainingTypes);

            sb.AppendLine();
            sb.AppendLine("// Top-level object serializer");
            AppendSerializeObject(sb, allTypes);

            sb.AppendLine();
            sb.AppendLine("// Top-level object deserializer");
            AppendDeserializeObject(sb, allTypes);

            sb.AppendLine();
            sb.AppendLine("// Top-level object MetaRef traverser");
            AppendTraverseMetaRefsInObject(sb, allTypes, metaRefContainingTypes);

            AppendILCompatibilityInfoComments(sb, allTypes);

            if (generateRuntimeTypeInfo)
                AppendRuntimeTypeInfo(sb, allTypes, typeHash);

            sb.Unindent("}"); // class TypeSerializer

            if (useMemberAccessTrampolines)
            {
                sb.AppendLine();
                sb.AppendLine("static class Trampolines");
                sb.Indent("{");

                AppendMembersSerializationDelegates(sb, allTypes);
                AppendMembersDeserializationDelegates(sb, allTypes);
                AppendCreateInstanceDelegates(sb, allTypes);

                sb.Unindent("}");
            }

            sb.Unindent("}"); // namespace Metaplay.Generated

            return sb.ToString();
        }

        /// <summary>
        /// Get types that (transitively) contain MetaRefs, i.e. types for which we need to
        /// generate MetaRef traversing code.
        /// </summary>
        /// <remarks>
        /// IGameConfigData types have two different serialization behaviors, used in
        /// different scenarios.
        /// "Normal" behavior: IGameConfigData is serialized by-key.
        /// "Config table" behavior: IGameConfigData is serialized by-members.
        /// In the "normal" case, an IGameConfigData never contains MetaRefs.
        /// However, in the "config table" behavior it might. This behavior is relevant
        /// for us since we want to traverse MetaRefs within config data.
        ///
        /// For this reason, this method produces two sets of types:
        /// Those that (transitively) contain MetaRefs when serialized with the "normal"
        /// behavior, and those that (transitively) contain MetaRefs when serialized by-members.
        /// The "by-members" set is a superset of the "normal" set. The "by-members" set,
        /// in addition to containing the same types as in the "normal" set, contains also
        /// the IGameConfigData types that have at least one member in the "normal" set.
        /// </remarks>
        /// <param name="metaRefContainingTypesOut">
        /// The types that transitively contain MetaRefs, when serialized
        /// with the normal serialization behavior.
        /// </param>
        /// <param name="metaRefByMembersContainingTypesOut">
        /// The types that transitively contain MetaRefs, when serialized
        /// with the "by-members" serialization behavior.
        /// This differs from <paramref name="metaRefContainingTypesOut"/> only
        /// with respect to IGameConfigData types.
        /// </param>
        internal void GetMetaRefContainingTypes(IEnumerable<ITypeInfo> typeInfos, out OrderedSet<Type> metaRefContainingTypesOut, out OrderedSet<Type> metaRefByMembersContainingTypesOut)
        {
            IEnumerable<Type> types = typeInfos.Select(t => t.Type);

            // Construct the inverse mapping of _typeDirectlyContains:  T -> types_that_T_is_directly_contained_in
            MetaDictionary<Type, OrderedSet<Type>> typeIsDirectlyContainedIn = new(capacity: types.Count());
            foreach (Type type in types)
                typeIsDirectlyContainedIn.Add(type, new OrderedSet<Type>());
            foreach ((Type containing, List<Type> containeds) in _typeDirectlyContains)
            {
                foreach (Type contained in containeds)
                    typeIsDirectlyContainedIn[contained].Add(containing);
            }

            // Get all MetaRef and MetaConfigId types
            IEnumerable<Type> metaRefTypes = types.Where(type => type.IsGenericTypeOf(typeof(MetaRef<>)) || type.IsGenericTypeOf(typeof(MetaConfigId<>)));

            // Compute which types transitively contain MetaRefs
            OrderedSet<Type> metaRefContainingTypes = Util.ComputeReachableNodes(startNodes: metaRefTypes, nodeNeighbors: typeIsDirectlyContainedIn);

            // Compute the "by-members" set: in addition to metaRefContainingTypes, this
            // contains the IGameConfigData types whose members contain MetaRefs.
            IEnumerable<Type> configDataTypes = types.Where(type => !type.IsAbstract && type.ImplementsGenericInterface(typeof(IGameConfigData<>)));
            IEnumerable<Type> metaRefByMembersContainingConfigDataTypes =
                configDataTypes
                .Where(type =>
                {
                    MetaSerializableType typeSpec = _typeInfoCache[type].SerializableType;
                    IEnumerable<Type> configDataTypeMembers = new ConcreteClassOrStructMembersInfo(this, typeSpec).DirectlyContainedTypes;
                    return configDataTypeMembers.Any(metaRefContainingTypes.Contains);
                });
            IEnumerable<Type> metaRefByMembersContainingTypes =
                metaRefContainingTypes
                .Concat(metaRefByMembersContainingConfigDataTypes)
                .ToOrderedSet();

            // Outputs

            metaRefContainingTypesOut           = metaRefContainingTypes;
            metaRefByMembersContainingTypesOut  = metaRefByMembersContainingTypes.ToOrderedSet();
        }
    }
}
