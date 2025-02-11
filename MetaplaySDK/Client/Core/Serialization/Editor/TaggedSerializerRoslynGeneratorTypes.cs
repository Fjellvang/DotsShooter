// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Config;
using Metaplay.Core.IO;
using Metaplay.Core.Math;
using Metaplay.Core.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using static System.FormattableString;

namespace Metaplay.Core.Serialization
{
    public partial class TaggedSerializerRoslynGenerator
    {
        static readonly string s_UncheckedWriteSuffix = "Unchecked";
        static string GetWriteFuncSuffix(bool spaceEnsured) => spaceEnsured ? s_UncheckedWriteSuffix : "";

        static readonly ITypeInfo[] s_primitiveInfos = new ITypeInfo[]
        {
            SimpleTypeInfo.FromExprs(typeof(bool),       1,                             (s, v) => $"writer.WriteByte{s}((byte)({v} ? 1 : 0))",   "reader.ReadByte() != 0"),
            SimpleTypeInfo.FromExprs(typeof(sbyte),      VarIntUtils.VarIntMaxBytes,    (s, v) => $"writer.WriteVarInt{s}({v})",                 "checked((sbyte)reader.ReadVarInt())"),
            SimpleTypeInfo.FromExprs(typeof(byte),       VarIntUtils.VarIntMaxBytes,    (s, v) => $"writer.WriteVarUInt{s}({v})",                "checked((byte)reader.ReadVarUInt())"),
            SimpleTypeInfo.FromExprs(typeof(short),      VarIntUtils.VarIntMaxBytes,    (s, v) => $"writer.WriteVarInt{s}({v})",                 "checked((short)reader.ReadVarInt())"),
            SimpleTypeInfo.FromExprs(typeof(ushort),     VarIntUtils.VarIntMaxBytes,    (s, v) => $"writer.WriteVarUInt{s}({v})",                "checked((ushort)reader.ReadVarUInt())"),
            SimpleTypeInfo.FromExprs(typeof(char),       VarIntUtils.VarIntMaxBytes,    (s, v) => $"writer.WriteVarUInt{s}({v})",                "checked((char)reader.ReadVarUInt())"),
            SimpleTypeInfo.FromExprs(typeof(int),        VarIntUtils.VarIntMaxBytes,    (s, v) => $"writer.WriteVarInt{s}({v})",                 "reader.ReadVarInt()"),
            SimpleTypeInfo.FromExprs(typeof(uint),       VarIntUtils.VarIntMaxBytes,    (s, v) => $"writer.WriteVarUInt{s}({v})",                "reader.ReadVarUInt()"),
            SimpleTypeInfo.FromExprs(typeof(long),       VarIntUtils.VarLongMaxBytes,   (s, v) => $"writer.WriteVarLong{s}({v})",                "reader.ReadVarLong()"),
            SimpleTypeInfo.FromExprs(typeof(ulong),      VarIntUtils.VarLongMaxBytes,   (s, v) => $"writer.WriteVarULong{s}({v})",               "reader.ReadVarULong()"),
            SimpleTypeInfo.FromExprs(typeof(MetaUInt128), VarIntUtils.VarInt128MaxBytes, (s, v) => $"writer.WriteVarUInt128{s}({v})",             "reader.ReadVarUInt128()"),
            SimpleTypeInfo.FromExprs(typeof(F32),        4,                             (s, v) => $"writer.WriteF32{s}({v})",                    "reader.ReadF32()"),
            SimpleTypeInfo.FromExprs(typeof(F32Vec2),    8,                             (s, v) => $"writer.WriteF32Vec2{s}({v})",                "reader.ReadF32Vec2()"),
            SimpleTypeInfo.FromExprs(typeof(F32Vec3),    12,                            (s, v) => $"writer.WriteF32Vec3{s}({v})",                "reader.ReadF32Vec3()"),
            SimpleTypeInfo.FromExprs(typeof(F64),        8,                             (s, v) => $"writer.WriteF64{s}({v})",                    "reader.ReadF64()"),
            SimpleTypeInfo.FromExprs(typeof(F64Vec2),    16,                            (s, v) => $"writer.WriteF64Vec2{s}({v})",                "reader.ReadF64Vec2()"),
            SimpleTypeInfo.FromExprs(typeof(F64Vec3),    24,                            (s, v) => $"writer.WriteF64Vec3{s}({v})",                "reader.ReadF64Vec3()"),
            SimpleTypeInfo.FromExprs(typeof(float),      4,                             (s, v) => $"writer.WriteFloat{s}({v})",                  "reader.ReadFloat()"),
            SimpleTypeInfo.FromExprs(typeof(double),     8,                             (s, v) => $"writer.WriteDouble{s}({v})",                 "reader.ReadDouble()"),
            SimpleTypeInfo.FromExprs(typeof(MetaGuid),   16,                            (s, v) => $"writer.WriteUInt128{s}({v}.Value)",          "new MetaGuid(reader.ReadUInt128())"),
            SimpleTypeInfo.FromExprs(typeof(EntityKind), VarIntUtils.VarIntMaxBytes,    (s, v) => $"writer.WriteVarInt{s}({v}.Value)",           "EntityKind.FromValue(reader.ReadVarInt())"),
            SimpleTypeInfo.FromExprs(typeof(string),     null,                          (s, v) => $"writer.WriteString({v})",                    "reader.ReadString(context.MaxStringSize)"),
            SimpleTypeInfo.FromExprs(typeof(byte[]),     null,                          (s, v) => $"writer.WriteByteString({v})",                "reader.ReadByteString(context.MaxByteArraySize)"),
            SimpleTypeInfo.FromExprs(typeof(TimeSpan),     VarIntUtils.VarLongMaxBytes,                          (s, v) => $"writer.WriteVarLong{s}({v}.Ticks)", "new TimeSpan(reader.ReadVarLong())"),

#if NETCOREAPP
            new SimpleTypeInfo(
                typeof(Akka.Actor.IActorRef), null,
                emitWriteCode:  (sb, _, valueExpr) => sb.AppendLine($"writer.WriteString(({valueExpr} != null) ? Akka.Serialization.Serialization.SerializedActorPath({valueExpr}) : null);"),
                emitReadCode:   (sb, varName) =>
                {
                    sb.AppendLine("string str = reader.ReadString(2048);");
                    sb.AppendLine($"{varName} = (str != null) ? context.ActorSystem.Provider.ResolveActorRef(str) : null;");
                }),
#endif
        };

        class SimpleTypeInfo : ITypeInfo
        {
            public Type Type { get; }
            public string GlobalNamespaceQualifiedTypeString => Type.ToGlobalNamespaceQualifiedTypeString();
            public IEnumerable<Type> DirectlyContainedTypes => Enumerable.Empty<Type>();
            public bool WriteToDebugStreamAtSerializationStart => false;

            public delegate void EmitWriteCodeFunc(IndentedStringBuilder sb, string suffix, string valueExpr);
            public delegate void EmitReadCodeFunc(IndentedStringBuilder sb, string varName);

            int?                _maxSerializedBytes;
            EmitWriteCodeFunc   _emitWriteCode;
            EmitReadCodeFunc    _emitReadCode;

            public SimpleTypeInfo(Type type, int? maxSerializedBytes, EmitWriteCodeFunc emitWriteCode, EmitReadCodeFunc emitReadCode)
            {
                Type = type;
                _maxSerializedBytes = maxSerializedBytes;
                _emitWriteCode = emitWriteCode ?? throw new ArgumentNullException(nameof(emitWriteCode));
                _emitReadCode = emitReadCode ?? throw new ArgumentNullException(nameof(emitReadCode));
            }

            public delegate string CreateWriteExprFunc(string suffix, string valueExpr);

            public static SimpleTypeInfo FromExprs(Type type, int? maxSerializedBytes, CreateWriteExprFunc createWriteExpression, string readExpression)
            {
                return new SimpleTypeInfo(
                    type,
                    maxSerializedBytes,
                    emitWriteCode: (sb, suffix, valueExpr) => sb.AppendLine($"{createWriteExpression(suffix, valueExpr)};"),
                    emitReadCode:  (sb, varName) => sb.AppendLine($"{varName} = {readExpression};"));
            }

            public void AppendSerializationCode(IndentedStringBuilder sb, string valueExpr, bool useTrampolines, bool spaceEnsured)
            {
                _emitWriteCode(sb, GetWriteFuncSuffix(spaceEnsured), valueExpr);
            }

            public void AppendDeserializationCode(IndentedStringBuilder sb, string varName, bool useTrampolines)
            {
                _emitReadCode(sb, varName);
            }

            public void AppendMetaRefTraverseCode(IndentedStringBuilder sb, string varName, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines)
            {
                // Simple types (primitives etc) do not contain MetaRefs.
            }

            public void Traverse(object obj, DynamicTraversal.IVisitor visitor)
            {
                visitor.VisitSimple(Type, obj);
            }

            public int? MaxSerializedBytesStatic() => _maxSerializedBytes;
        }

        class GuidInfo : ITypeInfo
        {
            Type _type;

            public GuidInfo(Type type)
            {
                _type = type ?? throw new ArgumentNullException(nameof(type));

                IsNullable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);

                var innerType = type;
                if (IsNullable)
                {
                    innerType              = type.GetSystemNullableElementType();
                    DirectlyContainedTypes = new[] {type.GetSystemNullableElementType()};
                }
                else
                    DirectlyContainedTypes = Array.Empty<Type>();

                if (innerType != typeof(Guid))
                    throw new ArgumentException("Parameter must be a Guid", nameof(type));
            }

            public Type              Type                                   => _type;
            public string            GlobalNamespaceQualifiedTypeString     => Type.ToGlobalNamespaceQualifiedTypeString();
            public IEnumerable<Type> DirectlyContainedTypes                 { get; }
            public bool              WriteToDebugStreamAtSerializationStart => false;
            public bool              IsNullable                             { get; }

            public void AppendSerializationCode(IndentedStringBuilder sb, string valueExpr, bool useTrampolines, bool spaceEnsured)
            {
                if (IsNullable)
                    sb.AppendLine($"writer.WriteNullableGuidAsByteString{GetWriteFuncSuffix(spaceEnsured)}({valueExpr});");
                else
                    sb.AppendLine($"writer.WriteGuidAsByteString{GetWriteFuncSuffix(spaceEnsured)}({valueExpr});");
            }

            public void AppendDeserializationCode(IndentedStringBuilder sb, string varName, bool useTrampolines)
            {
                sb.AppendLine("byte[] bytes = reader.ReadByteString(context.MaxByteArraySize);");
                sb.AppendLine("if (bytes != null)");
                sb.Indent("{");

                sb.AppendLine("if (bytes.Length != 16)");
                sb.Indent("{");
                sb.AppendLine($"throw new InvalidOperationException(\"Found {{bytes.Length}} length byte array for Guid while expecting array of length 16.\");");
                sb.Unindent("}");

                sb.AppendLine($"{varName} = new global::System.Guid(bytes);");

                sb.Unindent("}");
                sb.AppendLine("else");
                sb.Indent("{");

                if (IsNullable)
                    sb.AppendLine($"{varName} = null;");
                else
                    sb.AppendLine($"throw new InvalidOperationException(\"Found null byte array for Guid while expecting array of length 16.\");");

                sb.Unindent("}");
            }

            public void AppendMetaRefTraverseCode(IndentedStringBuilder sb, string varName, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines)
            {
                // Primitives, and nullable primitives, do not contain MetaRefs.
            }

            public void Traverse(object obj, DynamicTraversal.IVisitor visitor)
            {
                if(IsNullable)
                    visitor.VisitNullablePrimitive(Type, obj);
                else
                    visitor.VisitSimple(Type, obj);
            }

            public int? MaxSerializedBytesStatic()
            {
                return 17;
            }
        }

        class NullablePrimitiveInfo : ITypeInfo
        {
            Type _type;
            ITypeInfo _primitive;

            public NullablePrimitiveInfo(Type type, ITypeInfo primitive)
            {
                _type = type ?? throw new ArgumentNullException(nameof(type));
                _primitive = primitive ?? throw new ArgumentNullException(nameof(primitive));
                UnderlyingType = _type.GetSystemNullableElementType();
            }

            readonly Type UnderlyingType;

            public Type Type => _type;
            public string GlobalNamespaceQualifiedTypeString => Type.ToGlobalNamespaceQualifiedTypeString();
            public IEnumerable<Type> DirectlyContainedTypes => new Type[]{ UnderlyingType };
            public bool WriteToDebugStreamAtSerializationStart => false;

            public void AppendSerializationCode(IndentedStringBuilder sb, string valueExpr, bool useTrampolines, bool spaceEnsured)
            {
                sb.AppendLine($"if ({valueExpr}.HasValue)");
                sb.Indent("{");
                sb.AppendLine($"writer.WriteVarIntConst{GetWriteFuncSuffix(spaceEnsured)}(VarIntConst.One);");
                sb.AppendLine($"{UnderlyingType.ToGlobalNamespaceQualifiedTypeString()} underlyingValue = {valueExpr}.Value;");
                _primitive.AppendSerializationCode(sb, "underlyingValue", useTrampolines, spaceEnsured);
                sb.Unindent("}");
                sb.AppendLine("else");
                sb.Indent("{");
                sb.AppendLine($"writer.WriteVarIntConst{GetWriteFuncSuffix(spaceEnsured)}(VarIntConst.Zero);");
                sb.Unindent("}");
            }

            public void AppendDeserializationCode(IndentedStringBuilder sb, string varName, bool useTrampolines)
            {
                sb.AppendLine("int hasValue = reader.ReadVarInt();");
                sb.AppendLine();
                sb.AppendLine("if (hasValue != 0)");
                sb.Indent("{");
                sb.AppendLine($"{UnderlyingType.ToGlobalNamespaceQualifiedTypeString()} underlyingValue;");
                _primitive.AppendDeserializationCode(sb, "underlyingValue", useTrampolines);
                sb.AppendLine($"{varName} = underlyingValue;");
                sb.Unindent("}");
                sb.AppendLine("else");
                sb.Indent("{");
                sb.AppendLine($"{varName} = null;");
                sb.Unindent("}");
            }

            public void AppendMetaRefTraverseCode(IndentedStringBuilder sb, string varName, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines)
            {
                // Primitives, and nullable primitives, do not contain MetaRefs.
            }

            public void Traverse(object obj, DynamicTraversal.IVisitor visitor)
            {
                visitor.VisitNullablePrimitive(Type, obj);
            }

            public int? MaxSerializedBytesStatic()
            {
                int? primitiveMaxBytes = _primitive.MaxSerializedBytesStatic();
                if (primitiveMaxBytes.HasValue)
                    return primitiveMaxBytes.Value + 1;
                return null;
            }
        }

        abstract class MetaSerializableTypeInfo : ITypeInfo
        {
            protected TaggedSerializerRoslynGenerator _generator { get; }
            protected MetaSerializableType _typeSpec { get; }

            MetaSerializableType ITypeInfo.SerializableType => _typeSpec;

            protected MetaSerializableTypeInfo(TaggedSerializerRoslynGenerator generator, MetaSerializableType typeSpec)
            {
                _generator = generator ?? throw new ArgumentNullException(nameof(generator));
                _typeSpec = typeSpec ?? throw new ArgumentNullException(nameof(typeSpec));
            }

            public Type Type => _typeSpec.Type;
            public string GlobalNamespaceQualifiedTypeString => _typeSpec.GlobalNamespaceQualifiedTypeString;
            public abstract IEnumerable<Type> DirectlyContainedTypes { get; }
            public virtual bool WriteToDebugStreamAtSerializationStart => true;

            public abstract void AppendSerializationCode(IndentedStringBuilder sb, string valueExpr, bool useTrampolines, bool spaceEnsured);
            public abstract void AppendDeserializationCode(IndentedStringBuilder sb, string varName, bool useTrampolines);
            public abstract void AppendMetaRefTraverseCode(IndentedStringBuilder sb, string varName, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines);
            public abstract void Traverse(object obj, DynamicTraversal.IVisitor visitor);

            protected virtual int? MaxSerializedBytes() => null;

            bool _maxSerializedBytesComputed = false;
            int? _maxSerializedBytes;

            int? ITypeInfo.MaxSerializedBytesStatic()
            {
                if (!_maxSerializedBytesComputed)
                {
                    // initialize cached value first to break cycles
                    _maxSerializedBytesComputed = true;
                    _maxSerializedBytes = null;
                    _maxSerializedBytes = MaxSerializedBytes();
                }
                return _maxSerializedBytes;
            }
        }

        class ValueCollectionInfo : MetaSerializableTypeInfo
        {
            public ValueCollectionInfo(TaggedSerializerRoslynGenerator generator, MetaSerializableType typeSpec)
                : base(generator, typeSpec)
            {
                ElementType = _typeSpec.Type.GetCollectionElementType();
                IsArray = _typeSpec.Type.IsArray;
            }

            readonly bool IsArray;
            readonly Type ElementType;

            public override IEnumerable<Type> DirectlyContainedTypes => new Type[]{ ElementType };

            public override void AppendSerializationCode(IndentedStringBuilder sb, string valueExpr, bool useTrampolines, bool spaceEnsured)
            {
                WireDataType elemWireType = TaggedWireSerializer.GetWireType(ElementType, _generator._typeRegistry);

                sb.AppendLine($"if ({valueExpr} != null)");
                sb.Indent("{");
                if (IsArray)
                    sb.AppendLine($"int count = {valueExpr}.Length;");
                else
                    sb.AppendLine($"int count = {valueExpr}.Count;");

                sb.AppendLine($"if (count > context.MemberContext.MaxCollectionSize)");
                sb.AppendLine($"    throw new InvalidOperationException($\"Invalid value collection size {{count}} (maximum allowed is {{context.MemberContext.MaxCollectionSize}})\");");

                sb.AppendLine($"writer.WriteVarInt(count);");
                sb.AppendLine();
                sb.AppendLine($"WriteWireType(ref writer, WireDataType.{elemWireType});");
                sb.AppendLine();
                sb.AppendLine($"foreach (var elem in {valueExpr})");
                sb.Indent("{");
                sb.AppendLine($"{_generator.SerializationExpression(ElementType, "elem", false)};");
                sb.Unindent("}");
                sb.Unindent("}");
                sb.AppendLine("else");
                sb.Indent("{");
                sb.AppendLine("writer.WriteVarIntConst(VarIntConst.MinusOne);");
                sb.Unindent("}");
            }

            public override void AppendDeserializationCode(IndentedStringBuilder sb, string varName, bool useTrampolines)
            {
                WireDataType elemWireType = TaggedWireSerializer.GetWireType(ElementType, _generator._typeRegistry);

                string elemTypeStr = ElementType.ToGlobalNamespaceQualifiedTypeString();

                sb.AppendLine("int count = reader.ReadVarInt();");
                sb.AppendLine("if (count < -1 || count > context.MemberContext.MaxCollectionSize)");
                sb.AppendLine("    throw new InvalidOperationException($\"Invalid value collection size {count} (maximum allowed is {context.MemberContext.MaxCollectionSize})\");");

                sb.AppendLine();
                sb.AppendLine("if (count != -1)");
                sb.Indent("{");
                sb.AppendLine($"WireDataType elemWireType = ReadWireType(reader);");

                string expectPrimaryElemWireTypeExpr = $"ExpectWireType(ref context, reader, typeof({elemTypeStr}), elemWireType, WireDataType.{elemWireType});";

                sb.AppendLine();
                if (_generator.TryGetConverterWireTypesPredicateExpression(ElementType, "elemWireType", out string elemWireTypesPredicate))
                {
                    sb.AppendLine($"bool elemWireTypeIsAcceptedByConverter = {elemWireTypesPredicate};");
                    sb.AppendLine($"if (!elemWireTypeIsAcceptedByConverter)");
                    sb.Indent();
                    sb.AppendLine($"{expectPrimaryElemWireTypeExpr};");
                    sb.Unindent();
                }
                else
                    sb.AppendLine($"{expectPrimaryElemWireTypeExpr};");

                sb.AppendLine();

                Type typeToCreate         = _typeSpec.Type;
                bool isReadOnlyCollection = false;
                if (typeToCreate.IsGenericTypeOf(typeof(ReadOnlyCollection<>)))
                {
                    typeToCreate = typeof(List<>).MakeGenericType(_typeSpec.Type.GenericTypeArguments);
                    isReadOnlyCollection = true;
                }

                bool hasCapacityCtor = typeToCreate.GetConstructor(new Type[] { typeof(int) }) != null;
                if (IsArray)
                    sb.AppendLine($"var temp = new {ReplaceArrayCount(typeToCreate.ToGlobalNamespaceQualifiedTypeString(), "[count]")};");
                else if (hasCapacityCtor)
                    sb.AppendLine($"var temp = new {typeToCreate.ToGlobalNamespaceQualifiedTypeString()}(count);");
                else
                    sb.AppendLine($"var temp = new {typeToCreate.ToGlobalNamespaceQualifiedTypeString()}();");
                sb.AppendLine();

                sb.AppendLine("for (int ndx = 0; ndx < count; ndx++)");
                sb.Indent("{");

                sb.AppendLine($"{_generator.DeserializationExpression(ElementType, "elem", "elemWireType")};");
                if (IsArray)
                    sb.AppendLine($"temp[ndx] = elem;");
                else if (typeToCreate.IsGenericTypeOf(typeof(Queue<>)))
                    sb.AppendLine($"temp.Enqueue(elem);");
                else
                    sb.AppendLine($"temp.Add(elem);");

                sb.Unindent("}");

                if(isReadOnlyCollection)
                    sb.AppendLine($"{varName} = new {_typeSpec.GlobalNamespaceQualifiedTypeString}(temp);");
                else
                    sb.AppendLine($"{varName} = temp;");

                sb.Unindent("}");
                sb.AppendLine("else");
                sb.Indent("{");
                sb.AppendLine($"{varName} = null;");
                sb.Unindent("}");
            }

            public override void AppendMetaRefTraverseCode(IndentedStringBuilder sb, string varName, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines)
            {
                string elemTypeStr = ElementType.ToGlobalNamespaceQualifiedTypeString();

                sb.AppendLine($"if ({varName} != null)");
                sb.Indent("{");

                sb.AppendLine($"foreach ({elemTypeStr} elem in {varName})");
                sb.Indent("{");
                sb.AppendLine($"{_generator.MetaRefTraverseExpression(ElementType, "elem")};");
                sb.Unindent("}");

                sb.Unindent("}");
            }

            public override void Traverse(object obj, DynamicTraversal.IVisitor visitor)
            {
                IEnumerable enumerable = (IEnumerable)obj;
                visitor.BeginTraverseValueCollection(Type, enumerable);
                if (enumerable != null)
                {
                    int index = 0;
                    foreach (object elem in enumerable)
                    {
                        visitor.BeginTraverseValueCollectionElement(Type, enumerable, index, ElementType, elem);
                        visitor.Traverse(ElementType, elem);
                        visitor.EndTraverseValueCollectionElement(Type, enumerable, index, ElementType, elem);
                        index++;
                    }
                }
                visitor.EndTraverseValueCollection(Type, enumerable);
            }

            /// <summary>
            /// Replaces the '[]' of a (potentially nested) array type with <paramref name="newValue"/>.
            /// The replaced '[]' is the first occurrence of the (potentially many) at the end of the string.
            /// Eg, 'List&lt;int[][]&gt;[][]' would return 'List&lt;[][]&gt;[count][]' with '[count]' as newValue.
            /// </summary>
            /// <param name="input">Stringified type to insert the count into</param>
            /// <param name="newValue">Replacement for the matching brackets, eg, '[count']</param>
            /// <returns></returns>
            /// <exception cref="InvalidOperationException"></exception>
            public string ReplaceArrayCount(string input, string newValue)
            {
                // Find the index of the first '[]' in a string suffixed with potentially multiple '[]'
                if (input.Substring(input.Length - 2) != "[]")
                    throw new ArgumentException($"Array type must end with one or more '[]', got '{input}'");
                int firstEmptyBracketsNdx = input.Length - 2;
                for (; ;)
                {
                    if (firstEmptyBracketsNdx < 2)
                        break;
                    else if (input.Substring(firstEmptyBracketsNdx - 2, 2) == "[]")
                        firstEmptyBracketsNdx -= 2;
                    else
                        break;
                }

                return $"{input.Substring(0, firstEmptyBracketsNdx)}{newValue}{input.Substring(firstEmptyBracketsNdx + "[]".Length)}";
            }
        }

        class KeyValueCollectionInfo : MetaSerializableTypeInfo
        {
            public KeyValueCollectionInfo(TaggedSerializerRoslynGenerator generator, MetaSerializableType typeSpec)
                : base(generator, typeSpec)
            {
                (Type keyType, Type valueType) = _typeSpec.Type.GetDictionaryKeyAndValueTypes();
                KeyType = keyType;
                ValueType = valueType;
            }

            readonly Type KeyType;
            readonly Type ValueType;

            public override IEnumerable<Type> DirectlyContainedTypes => new Type[]{ KeyType, ValueType };

            public override void AppendSerializationCode(IndentedStringBuilder sb, string valueExpr, bool useTrampolines, bool spaceEnsured)
            {
                sb.AppendLine($"if ({valueExpr} != null)");
                sb.Indent("{");

                sb.AppendLine($"if ({valueExpr}.Count > context.MemberContext.MaxCollectionSize)");
                sb.AppendLine($"    throw new InvalidOperationException($\"Invalid value collection size {{{valueExpr}.Count}} (maximum allowed is {{context.MemberContext.MaxCollectionSize}})\");");

                sb.AppendLine($"writer.WriteVarInt({valueExpr}.Count);");
                sb.AppendLine();

                WireDataType keyWireType = TaggedWireSerializer.GetWireType(KeyType, _generator._typeRegistry);
                WireDataType valueWireType = TaggedWireSerializer.GetWireType(ValueType, _generator._typeRegistry);

                sb.AppendLine($"WriteWireType(ref writer, WireDataType.{keyWireType});");
                sb.AppendLine($"WriteWireType(ref writer, WireDataType.{valueWireType});");
                sb.AppendLine();

                string keyTypeStr = KeyType.ToGlobalNamespaceQualifiedTypeString();
                string valueTypeStr = ValueType.ToGlobalNamespaceQualifiedTypeString();

                sb.AppendLine($"foreach (({keyTypeStr} key, {valueTypeStr} value) in {valueExpr})");
                sb.Indent("{");
                sb.AppendLine($"{_generator.SerializationExpression(KeyType, "key", false)};");
                sb.AppendLine($"{_generator.SerializationExpression(ValueType, "value", false)};");
                sb.Unindent("}");

                sb.Unindent("}");
                sb.AppendLine("else");
                sb.Indent("{");
                sb.AppendLine("writer.WriteVarIntConst(VarIntConst.MinusOne);");
                sb.Unindent("}");
            }

            public override void AppendDeserializationCode(IndentedStringBuilder sb, string varName, bool useTrampolines)
            {
                WireDataType keyWireType = TaggedWireSerializer.GetWireType(KeyType, _generator._typeRegistry);
                WireDataType valueWireType = TaggedWireSerializer.GetWireType(ValueType, _generator._typeRegistry);

                string keyTypeStr = KeyType.ToGlobalNamespaceQualifiedTypeString();
                string valueTypeStr = ValueType.ToGlobalNamespaceQualifiedTypeString();

                sb.AppendLine("int count = reader.ReadVarInt();");

                sb.AppendLine("if (count < -1 || count > context.MemberContext.MaxCollectionSize)");
                sb.AppendLine("    throw new InvalidOperationException($\"Invalid value collection size {count} (maximum allowed is {context.MemberContext.MaxCollectionSize})\");");

                sb.AppendLine();
                sb.AppendLine("if (count != -1)");
                sb.Indent("{");
                sb.AppendLine($"WireDataType keyWireType = ReadWireType(reader);");
                sb.AppendLine($"WireDataType valueWireType = ReadWireType(reader);");

                string expectPrimaryKeyWireTypeExpr = $"ExpectWireType(ref context, reader, typeof({keyTypeStr}), keyWireType, WireDataType.{keyWireType})";
                string expectPrimaryValueWireTypeExpr = $"ExpectWireType(ref context, reader, typeof({valueTypeStr}), valueWireType, WireDataType.{valueWireType})";

                sb.AppendLine();
                if (_generator.TryGetConverterWireTypesPredicateExpression(KeyType, "keyWireType", out string keyWireTypesPredicate))
                {
                    sb.AppendLine($"bool keyWireTypeIsAcceptedByConverter = {keyWireTypesPredicate};");
                    sb.AppendLine($"if (!keyWireTypeIsAcceptedByConverter)");
                    sb.Indent();
                    sb.AppendLine($"{expectPrimaryKeyWireTypeExpr};");
                    sb.Unindent();
                }
                else
                    sb.AppendLine($"{expectPrimaryKeyWireTypeExpr};");

                sb.AppendLine();
                if (_generator.TryGetConverterWireTypesPredicateExpression(ValueType, "valueWireType", out string valueWireTypesPredicate))
                {
                    sb.AppendLine($"bool valueWireTypeIsAcceptedByConverter = {valueWireTypesPredicate};");
                    sb.AppendLine($"if (!valueWireTypeIsAcceptedByConverter)");
                    sb.Indent();
                    sb.AppendLine($"{expectPrimaryValueWireTypeExpr};");
                    sb.Unindent();
                }
                else
                    sb.AppendLine($"{expectPrimaryValueWireTypeExpr};");

                sb.AppendLine();

                Type typeToCreate    = _typeSpec.Type;
                bool isReadOnlyDictionary = false;
                if (typeToCreate.IsGenericTypeOf(typeof(ReadOnlyDictionary<,>)))
                {
                    typeToCreate         = typeof(MetaDictionary<,>).MakeGenericType(_typeSpec.Type.GenericTypeArguments);
                    isReadOnlyDictionary = true;
                }

                bool hasCapacityCtor = typeToCreate.GetConstructor(new Type[] { typeof(int) }) != null;
                if (hasCapacityCtor)
                    sb.AppendLine($"var temp = new {typeToCreate.ToGlobalNamespaceQualifiedTypeString()}(count);");
                else
                    sb.AppendLine($"var temp = new {typeToCreate.ToGlobalNamespaceQualifiedTypeString()}();");
                sb.AppendLine();

                sb.AppendLine("for (int ndx = 0; ndx < count; ndx++)");
                sb.Indent("{");
                sb.AppendLine($"{_generator.DeserializationExpression(KeyType, "key", "keyWireType")};");
                sb.AppendLine($"{_generator.DeserializationExpression(ValueType, "value", "valueWireType")};");
                sb.AppendLine($"temp.Add(key, value);");
                sb.Unindent("}");

                if (isReadOnlyDictionary)
                    sb.AppendLine($"{varName} = new {_typeSpec.GlobalNamespaceQualifiedTypeString}(temp);");
                else
                    sb.AppendLine($"{varName} = temp;");

                sb.Unindent("}");
                sb.AppendLine("else");
                sb.Indent("{");
                sb.AppendLine($"{varName} = null;");
                sb.Unindent("}");
            }
            public override void AppendMetaRefTraverseCode(IndentedStringBuilder sb, string varName, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines)
            {
                sb.AppendLine($"if ({varName} != null)");
                sb.Indent("{");

                string keyTypeStr = KeyType.ToGlobalNamespaceQualifiedTypeString();
                string valueTypeStr = ValueType.ToGlobalNamespaceQualifiedTypeString();

                sb.AppendLine($"foreach (({keyTypeStr} key, {valueTypeStr} value) in {varName})");
                sb.Indent("{");

                if (metaRefContainingTypes.Contains(KeyType))
                    sb.AppendLine($"{_generator.MetaRefTraverseExpression(KeyType, "key")};");
                else
                    sb.AppendLine("// Key type does not contain MetaRefs");

                if (metaRefContainingTypes.Contains(ValueType))
                    sb.AppendLine($"{_generator.MetaRefTraverseExpression(ValueType, "value")};");
                else
                    sb.AppendLine("// Value type does not contain MetaRefs");

                sb.Unindent("}");
                sb.Unindent("}");
            }

            public override void Traverse(object obj, DynamicTraversal.IVisitor visitor)
            {
                IDictionary dictionary = (IDictionary)obj;
                visitor.BeginTraverseKeyValueCollection(Type, dictionary);
                if (dictionary != null)
                {
                    int index = 0;
                    IDictionaryEnumerator enumerator = dictionary.GetEnumerator();
                    while (enumerator.MoveNext())
                    {
                        DictionaryEntry entry = enumerator.Entry;
                        visitor.BeginTraverseKeyValueCollectionKey(Type, dictionary, index, KeyType, entry.Key);
                        visitor.Traverse(KeyType, entry.Key);
                        visitor.EndTraverseKeyValueCollectionKey(Type, dictionary, index, KeyType, entry.Key);
                        visitor.BeginTraverseKeyValueCollectionValue(Type, dictionary, index, KeyType, entry.Key, ValueType, entry.Value);
                        visitor.Traverse(ValueType, entry.Value);
                        visitor.EndTraverseKeyValueCollectionValue(Type, dictionary, index, KeyType, entry.Key, ValueType, entry.Value);
                        index++;
                    }
                }
                visitor.EndTraverseKeyValueCollection(Type, dictionary);
            }
        }

        class ConcreteClassInfo : MetaSerializableTypeInfo
        {
            public ConcreteClassInfo(TaggedSerializerRoslynGenerator generator, MetaSerializableType typeSpec)
                : base(generator, typeSpec)
            {
            }

            public override IEnumerable<Type> DirectlyContainedTypes => new ConcreteClassOrStructMembersInfo(_generator, _typeSpec).DirectlyContainedTypes;

            public override void AppendSerializationCode(IndentedStringBuilder sb, string valueExpr, bool useTrampolines, bool spaceEnsured)
            {
                sb.AppendLine($"if ({valueExpr} != null)");
                sb.Indent("{");
                sb.AppendLine($"writer.WriteVarIntConst{GetWriteFuncSuffix(spaceEnsured)}(VarIntConst.One);");
                sb.AppendLine();
                sb.AppendLine($"{_generator.MembersSerializationExpression(_typeSpec.Type, valueExpr, spaceEnsured)};");
                sb.Unindent("}");
                sb.AppendLine("else");
                sb.Indent("{");
                sb.AppendLine($"writer.WriteVarIntConst{GetWriteFuncSuffix(spaceEnsured)}(VarIntConst.Zero);");
                sb.Unindent("}");
            }

            public override void AppendDeserializationCode(IndentedStringBuilder sb, string varName, bool useTrampolines)
            {
                sb.AppendLine("int hasValue = reader.ReadVarInt();");
                sb.AppendLine("if (hasValue != 0)");
                sb.Indent("{");
                sb.AppendLine($"{_generator.MembersDeserializationExpression(_typeSpec.Type, "result")};");
                sb.AppendLine($"{varName} = result;");
                sb.Unindent("}");
                sb.AppendLine("else");
                sb.Indent("{");
                sb.AppendLine($"{varName} = null;");
                sb.Unindent("}");
            }

            public override void AppendMetaRefTraverseCode(IndentedStringBuilder sb, string varName, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines)
            {
                sb.AppendLine($"if ({varName} != null)");
                sb.Indent("{");
                sb.AppendLine($"{_generator.MembersMetaRefTraverseExpression(_typeSpec.Type, varName)};");
                sb.Unindent("}");
            }

            public override void Traverse(object obj, DynamicTraversal.IVisitor visitor)
            {
                visitor.BeginTraverseConcreteClass(Type, obj);
                if (obj != null)
                    visitor.TraverseMembers(Type, obj);
                visitor.EndTraverseConcreteClass(Type, obj);
            }

            protected override int? MaxSerializedBytes()
            {
                int? membersMaxBytes = new ConcreteClassOrStructMembersInfo(_generator, _typeSpec).MaxSerializedBytesStatic();
                if (membersMaxBytes.HasValue)
                    return membersMaxBytes.Value + 1; // nullable
                return null;
            }
        }

        class StructInfo : MetaSerializableTypeInfo
        {
            public StructInfo(TaggedSerializerRoslynGenerator generator, MetaSerializableType typeSpec)
                : base(generator, typeSpec)
            {
            }

            public override IEnumerable<Type> DirectlyContainedTypes => new ConcreteClassOrStructMembersInfo(_generator, _typeSpec).DirectlyContainedTypes;

            public override void AppendSerializationCode(IndentedStringBuilder sb, string valueExpr, bool useTrampolines, bool spaceEnsured)
            {
                sb.AppendLine($"{_generator.MembersSerializationExpression(_typeSpec.Type, valueExpr, spaceEnsured)};");
            }

            public override void AppendDeserializationCode(IndentedStringBuilder sb, string varName, bool useTrampolines)
            {
                sb.AppendLine($"{_generator.MembersDeserializationExpression(_typeSpec.Type, "result")};");
                sb.AppendLine($"{varName} = result;");
            }

            public override void AppendMetaRefTraverseCode(IndentedStringBuilder sb, string varName, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines)
            {
                sb.AppendLine($"{_generator.MembersMetaRefTraverseExpression(_typeSpec.Type, varName)};");
            }

            public override void Traverse(object obj, DynamicTraversal.IVisitor visitor)
            {
                visitor.BeginTraverseStruct(Type, obj);
                visitor.TraverseMembers(Type, obj);
                visitor.EndTraverseStruct(Type, obj);
            }

            protected override int? MaxSerializedBytes()
            {
                return new ConcreteClassOrStructMembersInfo(_generator, _typeSpec).MaxSerializedBytesStatic();
            }
        }

        class NullableStructInfo : MetaSerializableTypeInfo
        {
            public NullableStructInfo(TaggedSerializerRoslynGenerator generator, MetaSerializableType typeSpec)
                : base(generator, typeSpec)
            {
                UnderlyingType = _typeSpec.Type.GetSystemNullableElementType();
            }

            readonly Type UnderlyingType;

            public override IEnumerable<Type> DirectlyContainedTypes => new Type[]{ UnderlyingType };

            public override void AppendSerializationCode(IndentedStringBuilder sb, string valueExpr, bool useTrampolines, bool spaceEnsured)
            {
                sb.AppendLine($"if ({valueExpr}.HasValue)");
                sb.Indent("{");
                sb.AppendLine($"writer.WriteVarIntConst{GetWriteFuncSuffix(spaceEnsured)}(VarIntConst.One);");
                sb.AppendLine($"{_generator.MembersSerializationExpression(UnderlyingType, $"{valueExpr}.Value", spaceEnsured)};");
                sb.Unindent("}");
                sb.AppendLine("else");
                sb.Indent("{");
                sb.AppendLine($"writer.WriteVarIntConst{GetWriteFuncSuffix(spaceEnsured)}(VarIntConst.Zero);");
                sb.Unindent("}");
            }

            public override void AppendDeserializationCode(IndentedStringBuilder sb, string varName, bool useTrampolines)
            {

                sb.AppendLine("int hasValue = reader.ReadVarInt();");
                sb.AppendLine("if (hasValue != 0)");
                sb.Indent("{");

                sb.AppendLine($"{_generator.MembersDeserializationExpression(UnderlyingType, "result")};");
                sb.AppendLine($"{varName} = result;");
                sb.Unindent("}");
                sb.AppendLine("else");
                sb.Indent("{");
                sb.AppendLine($"{varName} = null;");
                sb.Unindent("}");
            }

            public override void AppendMetaRefTraverseCode(IndentedStringBuilder sb, string varName, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines)
            {
                sb.AppendLine($"if ({varName}.HasValue)");
                sb.Indent("{");
                sb.AppendLine($"{_generator.MembersMetaRefTraverseExpression(UnderlyingType, $"{varName}.Value")};");
                sb.Unindent("}");
            }

            public override void Traverse(object obj, DynamicTraversal.IVisitor visitor)
            {
                visitor.BeginTraverseNullableStruct(Type, obj);
                if (obj != null)
                    visitor.TraverseMembers(UnderlyingType, obj);
                visitor.EndTraverseNullableStruct(Type, obj);
            }

            protected override int? MaxSerializedBytes()
            {
                int? underlyingMaxBytes = _generator._typeInfoCache[UnderlyingType].MaxSerializedBytesStatic();
                if (underlyingMaxBytes.HasValue)
                    return underlyingMaxBytes.Value + 1;
                return null;
            }
        }

        class AbstractClassOrInterfaceInfo : MetaSerializableTypeInfo
        {
            public AbstractClassOrInterfaceInfo(TaggedSerializerRoslynGenerator generator, MetaSerializableType typeSpec)
                : base(generator, typeSpec)
            {
            }

            public override IEnumerable<Type> DirectlyContainedTypes => _typeSpec.DerivedTypes.Values;

            public override void AppendSerializationCode(IndentedStringBuilder sb, string valueExpr, bool useTrampolines, bool spaceEnsured)
            {
                sb.AppendLine($"switch ({valueExpr})");
                sb.Indent("{");

                foreach ((int typeCode, Type derivedType) in _typeSpec.DerivedTypes)
                {
                    MetaSerializableType derivedTypeSpec = _generator._typeInfoCache[derivedType].SerializableType;
                    MetaDebug.Assert(!derivedTypeSpec.IsAbstract, $"{nameof(_typeSpec.DerivedTypes)} of {{0}} contains abstract type {{1}}", _typeSpec.Name, derivedTypeSpec.Name);

                    sb.AppendLine($"case {derivedTypeSpec.GlobalNamespaceQualifiedTypeString} concreteTypedValue:");
                    sb.Indent("{");
                    // Handle member versioning (if has version attributes)
                    int? addedInVersion = derivedTypeSpec.AddedInVersion;

                    if (addedInVersion != null)
                    {
                        string minVersion = addedInVersion?.ToString(CultureInfo.InvariantCulture) ?? "0";
                        sb.AppendLine($"if (!IsVersionInRange(ref context, {minVersion}))");
                        sb.AppendLine(Invariant($"    throw new MetaTypeMissingInLogicVersionSerializationException(\"{derivedTypeSpec.Type.ToGenericTypeString()}\", context.LogicVersion, {addedInVersion});"));
                    }
                    sb.AppendLine(Invariant($"writer.WriteVarInt({typeCode}); // typecode"));
                    sb.AppendLine($"{_generator.MembersSerializationExpression(derivedTypeSpec.Type, "concreteTypedValue", false)};");

                    sb.AppendLine("break;");
                    sb.Unindent("}");
                }

                sb.AppendLine("case null:");
                sb.AppendLine("    writer.WriteVarIntConst(VarIntConst.Zero);");
                sb.AppendLine("    break;");

                sb.AppendLine("default:");
                sb.AppendLine($"    throw new MetaSerializationException($\"Unknown derived type of {_typeSpec.Name}: {{{valueExpr}.GetType().ToGenericTypeString()}}. The derived type must have the [MetaSerializableDerived] attribute.\");");
                sb.Unindent("}");
            }

            public override void AppendDeserializationCode(IndentedStringBuilder sb, string varName, bool useTrampolines)
            {
                sb.AppendLine("int typeCode = reader.ReadVarInt();");
                sb.AppendLine("if (typeCode != 0)");
                sb.Indent("{");
                sb.AppendLine("switch (typeCode)");
                sb.Indent("{");

                foreach ((int typeCode, Type derivedType) in _typeSpec.DerivedTypes)
                {
                    MetaSerializableType derivedTypeSpec = _generator._typeInfoCache[derivedType].SerializableType;
                    MetaDebug.Assert(!derivedTypeSpec.IsAbstract, $"{nameof(_typeSpec.DerivedTypes)} of {{0}} contains abstract type {{1}}", _typeSpec.Name, derivedTypeSpec.Name);
                    sb.AppendLine(Invariant($"case {typeCode}: "));
                    sb.Indent("{");
                    // Handle member versioning (if has version attributes)
                    int? addedInVersion = derivedTypeSpec.AddedInVersion;

                    if (addedInVersion != null)
                    {
                        string minVersion = addedInVersion?.ToString(CultureInfo.InvariantCulture) ?? "0";
                        sb.AppendLine($"if (!IsVersionInRange(ref context, {minVersion}))");
                        sb.AppendLine(Invariant($"    throw new MetaTypeMissingInLogicVersionSerializationException(\"{derivedTypeSpec.Type.ToGenericTypeString()}\", context.LogicVersion, {addedInVersion});"));
                    }

                    sb.AppendLine($"{_generator.MembersDeserializationExpression(derivedType, "result")};");
                    sb.AppendLine($"{varName} = result;");

                    sb.AppendLine("break;");
                    sb.Unindent("}");
                }

                sb.AppendLine("default:");
                sb.AppendLine($"    throw new MetaUnknownDerivedTypeDeserializationException($\"Unknown derived type of {_typeSpec.Name}: {{typeCode}}\", typeof({_typeSpec.GlobalNamespaceQualifiedTypeString}), typeCode);");

                sb.Unindent("}");

                sb.Unindent("}");
                sb.AppendLine("else");
                sb.Indent("{");
                sb.AppendLine($"{varName} = null;");
                sb.Unindent("}");
            }

            public override void AppendMetaRefTraverseCode(IndentedStringBuilder sb, string varName, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines)
            {
                sb.AppendLine($"switch ({varName})");
                sb.Indent("{");

                foreach ((int typeCode, Type derivedType) in _typeSpec.DerivedTypes)
                {
                    MetaSerializableType derivedTypeSpec = _generator._typeInfoCache[derivedType].SerializableType;
                    MetaDebug.Assert(!derivedTypeSpec.IsAbstract, $"{nameof(_typeSpec.DerivedTypes)} of {{0}} contains abstract type {{1}}", _typeSpec.Name, derivedTypeSpec.Name);
                    sb.AppendLine($"case {derivedTypeSpec.GlobalNamespaceQualifiedTypeString} concreteTypedValue:");
                    if (metaRefContainingTypes.Contains(derivedType))
                    {
                        sb.AppendLine($"    {_generator.MembersMetaRefTraverseExpression(derivedTypeSpec.Type, "concreteTypedValue")};");
                    }
                    else
                    {
                        sb.AppendLine($"    // This concrete type does not contain MetaRefs");
                    }
                    sb.AppendLine("    break;");
                }

                sb.AppendLine("case null:");
                sb.AppendLine("    break;");

                sb.AppendLine("default:");
                sb.AppendLine($"    throw new MetaSerializationException($\"Unknown derived type of {_typeSpec.Name}: {{{varName}.GetType().ToGenericTypeString()}}. The derived type must have the [MetaSerializableDerived] attribute.\");");
                sb.Unindent("}");
            }

            public override void Traverse(object obj, DynamicTraversal.IVisitor visitor)
            {
                visitor.BeginTraverseAbstractClass(Type, obj);
                if (obj != null)
                {
                    Type derivedType = obj.GetType();
                    visitor.BeginTraverseDerivedClass(Type, obj, derivedType);
                    visitor.Traverse(derivedType, obj);
                    visitor.EndTraverseDerivedClass(Type, obj, derivedType);
                }
                visitor.EndTraverseAbstractClass(Type, obj);
            }
        }

        class ConcreteClassOrStructMembersInfo : IMembersInfo
        {
            struct SpanWriteBlock
            {
                public int MaxBytes;
                public int NumMembers;
                public bool LastMemberHasDynamicSize;
            }

            TaggedSerializerRoslynGenerator _generator;
            MetaSerializableType _typeSpec;

            public ConcreteClassOrStructMembersInfo(TaggedSerializerRoslynGenerator generator, MetaSerializableType typeSpec)
            {
                _generator = generator ?? throw new ArgumentNullException(nameof(generator));
                _typeSpec = typeSpec ?? throw new ArgumentNullException(nameof(typeSpec));
            }

            public Type Type => _typeSpec.Type;
            public string GlobalNamespaceQualifiedTypeString => _typeSpec.GlobalNamespaceQualifiedTypeString;
            public IEnumerable<Type> DirectlyContainedTypes => _typeSpec.Members.Select(member => member.Type).Distinct();

            static void AppendWriteWireTypeAndTag(IndentedStringBuilder sb, WireDataType wireType, int tagId)
            {
                byte[] bytes = new byte[VarIntUtils.VarIntMaxBytes];
                int numBytes = VarIntUtils.FormatVarInt(bytes, tagId);
                string s = Invariant($"writer.WriteBytesUnchecked{numBytes + 1}({(byte)wireType}");

                for (int i = 0; i < numBytes; ++i)
                    s += Invariant($", {bytes[i]}");

                s += ");";
                sb.AppendLine(s);
            }

            public int? MaxSerializedBytesStatic()
            {
                int maxBytes = 1; // EndStruct
                foreach (MetaSerializableMember member in _typeSpec.Members)
                {
                    Type memberType = member.Type;
                    int? memberMaxBytesMaybe = _generator._typeInfoCache[memberType].MaxSerializedBytesStatic();
                    if (!memberMaxBytesMaybe.HasValue)
                        return null;
                    int memberMaxBytes = memberMaxBytesMaybe.Value;
                    // Add contribution of wiretype and tag
                    memberMaxBytes += 1 + VarIntUtils.CountVarIntBytes(member.TagId);
                    // Check if member fits in span
                    if (memberMaxBytes > MaxSpanSize)
                        return null;
                    maxBytes += memberMaxBytes;
                }
                return maxBytes;
            }

            void AppendSerializeMember(IndentedStringBuilder sb, MetaSerializableMember member, string valueExpr, bool useTrampolines, bool spaceEnsured)
            {
                Type memberType = member.Type;
                WireDataType wireType = TaggedWireSerializer.GetWireType(memberType, _generator._typeRegistry);

                int? addedInVersion = member.CodeGenInfo.AddedInLogicVersion;
                int? removedInVersion = member.CodeGenInfo.RemovedInLogicVersion;
                bool hasVersionInfo = (addedInVersion != null) || (removedInVersion != null);
                bool hasFlags = member.Flags != MetaMemberFlags.None;
                List<string> conditions = hasVersionInfo || hasFlags ? new List<string>() : null;

                // Handle member versioning (if has version attributes)
                if (hasVersionInfo)
                {
                    string minVersion = addedInVersion?.ToString(CultureInfo.InvariantCulture) ?? "0";
                    string maxVersion = removedInVersion?.ToString(CultureInfo.InvariantCulture) ?? "int.MaxValue";
                    conditions.Add($"IsVersionInRange(ref context, {minVersion}, {maxVersion})");
                }

                // Handle member flags (if has any)
                if (hasFlags)
                {
                    string flagsStr = MemberFlagsToMask(member.Flags);
                    conditions.Add($"((context.ExcludeFlags & {flagsStr}) == 0)");
                }

                if (conditions != null)
                {
                    sb.AppendLine($"if ({string.Join(" && ", conditions)})");
                    sb.Indent("{");
                }

                if (sb.DebugEnabled)
                    sb.AppendDebugLine(Invariant($"context.DebugStream?.AppendLine($\"@{{writer.Offset}} {_typeSpec.Name}.{member.Name} (wireType={wireType}, tagId={member.TagId})\");"));

                AppendWriteWireTypeAndTag(sb, wireType, member.TagId);
                string getMemberStr      = useTrampolines ? $"Trampolines.Get_{_generator.MethodSuffixForType(_typeSpec.Type)}_{member.Name}(ref {valueExpr})" : $"{valueExpr}.{member.Name}";
                sb.AppendLine($"{_generator.SerializationExpression(memberType, getMemberStr, spaceEnsured)};");

                if (conditions != null)
                {
                    sb.Unindent("}");
                }
            }

            public void AppendMembersSerializationCode(IndentedStringBuilder sb, string valueExpr, bool useTrampolines, bool spaceEnsured)
            {
                if (sb.DebugEnabled)
                {
                    sb.AppendDebugLine($"context.DebugStream?.AppendLine($\"@{{writer.Offset}} write {_typeSpec.Name} members\");");
                    sb.AppendDebugLine();
                }

                List<SpanWriteBlock> blocks = new List<SpanWriteBlock>();
                SpanWriteBlock current = new SpanWriteBlock();

                // Form blocks of members that can be written into a single contiguous span. Compute the max serialized size for each
                // block so that we can do a single EnsureSpace call for the entire block of members. A block can be split due to
                //   1. a dynamic size member being encountered, or
                //   2. the MaxSpanSize limit being met
                //
                // In case 1. the dynamic size member is added as the final member of the block and the fact that it is dynamic
                // is recorded by setting `LastMemberHasDynamicSize`.
                foreach (MetaSerializableMember member in _typeSpec.Members)
                {
                    Type memberType = member.Type;
                    int? maxPayloadBytes = _generator._typeInfoCache[memberType].MaxSerializedBytesStatic();
                    int bytesForMember = 1 + VarIntUtils.CountVarIntBytes(member.TagId);
                    bool useStaticSize = maxPayloadBytes.HasValue && ((maxPayloadBytes.Value + bytesForMember) <= MaxSpanSize);
                    if (useStaticSize)
                        bytesForMember += maxPayloadBytes.Value;

                    // Split block by MaxSpanSize
                    if (current.NumMembers > 0 && current.MaxBytes + bytesForMember > MaxSpanSize)
                    {
                        blocks.Add(current);
                        current = new SpanWriteBlock();
                    }

                    // Add to current block
                    current.NumMembers++;
                    current.MaxBytes += bytesForMember;

                    // Flush block if current member max bytes not known => must be written with dynamic size
                    if (!useStaticSize)
                    {
                        current.LastMemberHasDynamicSize = true;
                        blocks.Add(current);
                        current = new SpanWriteBlock();
                    }
                }

                // Add EndStruct, into separate block if would exceed span size
                if (current.MaxBytes + 1 > MaxSpanSize)
                {
                    blocks.Add(current);
                    current = new SpanWriteBlock();
                }
                current.MaxBytes += 1;
                blocks.Add(current);

                MetaDebug.Assert(!spaceEnsured || blocks.Count == 1, "Writing members directly to span requires single block");

                // Write out blocks
                bool isFirst = true;
                int memberIndex = 0;

                foreach (SpanWriteBlock block in blocks)
                {
                    if (!spaceEnsured)
                    {
                        if (isFirst)
                            isFirst = false;
                        else
                            sb.AppendLine();

                        sb.AppendLine(Invariant($"writer.EnsureSpace({block.MaxBytes});"));
                    }

                    int numMembers = block.NumMembers;
                    while (numMembers-- > 0)
                    {
                        MetaSerializableMember member = _typeSpec.Members[memberIndex++];
                        bool spaceEnsuredForMember = numMembers > 0 || !block.LastMemberHasDynamicSize;
                        sb.AppendLine(Invariant($"context.MemberContext.Update(\"{member.DeclaringType.Name}.{member.Name}\", {member.CodeGenInfo.MaxCollectionSize});"));
                        AppendSerializeMember(sb, member, valueExpr, useTrampolines, spaceEnsuredForMember);
                    }
                }
                sb.AppendLine("writer.WriteByteUnchecked((byte)WireDataType.EndStruct);");
            }

            public string CreateSafeMemberName(string name)
            {
                return $"GENERATED_TEMP_{name}";
            }

            public void AppendMembersDeserializationCode(IndentedStringBuilder sb, string varName, bool useTrampolines)
            {
                if (_typeSpec.UsesConstructorDeserialization)
                {
                    // Initialize fields with optional values if necessary
                    foreach (MetaSerializableMember member in _typeSpec.Members)
                    {
                        if(!_typeSpec.ConstructorInitializedMembers.Contains(member))
                            sb.AppendLine("bool " + "ASSIGNED_" + CreateSafeMemberName(member.Name) + Invariant($" = false;"));

                        if (_typeSpec.ConstructorParameterValues.ContainsKey(member.TagId))
                            sb.AppendLine(member.Type.ToGlobalNamespaceQualifiedTypeString() + " " + CreateSafeMemberName(member.Name) + Invariant($" = ({member.Type.ToGlobalNamespaceQualifiedTypeString()})context.TypeInfo.GetSerializableType(typeof({_typeSpec.GlobalNamespaceQualifiedTypeString})).ConstructorParameterValues[{member.TagId}];"));
                        else
                        {
                            sb.AppendLine(member.Type.ToGlobalNamespaceQualifiedTypeString() + " " + CreateSafeMemberName(member.Name) + Invariant($" = default({member.Type.ToGlobalNamespaceQualifiedTypeString()});"));
                        }
                    }
                }
                else
                {
                    sb.AppendLine($"{varName} = {_generator.CreateInstanceExpression(_typeSpec, useTrampolines)};");
                }

                sb.AppendLine("for (;;)");
                sb.Indent("{");

                // EndStruct terminates the struct
                sb.AppendLine("WireDataType memberWireType = ReadWireType(reader);");
                sb.AppendLine("if (memberWireType == WireDataType.EndStruct)");
                sb.AppendLine("    break;");
                sb.AppendLine();

                // Read member based on tag (or skip if unknown)
                sb.AppendLine("int tagId = ReadTagId(reader);");
                sb.AppendLine("switch (tagId)");
                sb.Indent("{");
                foreach (MetaSerializableMember member in _typeSpec.Members)
                {
                    // If member has flags, perform flags check before deserializing
                    bool hasFlags = member.Flags != MetaMemberFlags.None;
                    string flagsCheckPrefix = "";
                    string flagsCheckSuffix = "";
                    if (hasFlags)
                    {
                        string flagsStr = MemberFlagsToMask(member.Flags);
                        flagsCheckPrefix = $"if ((context.ExcludeFlags & {flagsStr}) == 0) {{ ";
                        flagsCheckSuffix = " } else SkipWireType(reader, memberWireType);";
                    }

                    string deserializeStr = $"{_generator.DeserializationExpression(member.Type, "member", "memberWireType")};";
                    string setMemberStr;
                    if (!_typeSpec.UsesConstructorDeserialization)
                        setMemberStr = useTrampolines ? $"Trampolines.Set_{_generator.MethodSuffixForType(_typeSpec.Type)}_{member.Name}(ref {varName}, member);" : $"{varName}.{member.Name} = member;";
                    else
                    {
                        setMemberStr = Invariant($"{CreateSafeMemberName(member.Name)} = member;");
                        if (!_typeSpec.ConstructorInitializedMembers.Contains(member))
                            setMemberStr += $" {"ASSIGNED_" + CreateSafeMemberName(member.Name)} = true;";
                    }

                    if (member.OnDeserializationFailureMethod == null)
                        sb.AppendLine(Invariant($"case {member.TagId}: {{ context.MemberContext.Update(\"{member.DeclaringType.Name}.{member.Name}\", {member.CodeGenInfo.MaxCollectionSize}); {flagsCheckPrefix}{deserializeStr} {setMemberStr}{flagsCheckSuffix} break; }}"));
                    else
                    {
                        string onFailureMethodStr;
                        if (useTrampolines)
                            onFailureMethodStr = $"Trampolines.InvokeOnMemberDeserializationFailure_{_generator.MethodSuffixForType(_typeSpec.Type)}_{member.Name}";
                        else
                            onFailureMethodStr = member.OnDeserializationFailureMethod.ToMemberWithGlobalNamespaceQualifiedTypeString();

                        sb.AppendLine(Invariant($"case {member.TagId}:"));
                        sb.Indent("{");

                        if (hasFlags)
                            sb.Indent(flagsCheckPrefix);
                        else
                            MetaDebug.Assert(flagsCheckPrefix == "", "!hasFlags, but check prefix is nonempty");

                        sb.AppendLine("int depthBeforeMember = context.Depth;");
                        sb.AppendLine("int offsetAtMemberStart = reader.Offset;");
                        sb.AppendLine("try");
                        sb.Indent("{");
                        sb.AppendLine(deserializeStr);
                        sb.AppendLine(setMemberStr);
                        sb.Unindent("}");
                        sb.AppendLine("catch (Exception exception)");
                        sb.Indent("{");
                        sb.AppendLine("context.Depth = depthBeforeMember;");
                        sb.AppendLine("byte[] memberPayload = ReadBytesForWireObjectStartingAt(reader, memberWireType, offsetAtMemberStart);");
                        sb.AppendLine($"MetaMemberDeserializationFailureParams failureParams = new MetaMemberDeserializationFailureParams(memberPayload, exception, ref context);");
                        sb.AppendLine($"{member.CodeGenInfo.GlobalNamespaceQualifiedTypeString} member = {onFailureMethodStr}(failureParams);");
                        sb.AppendLine(setMemberStr);
                        sb.Unindent("}");

                        if (hasFlags)
                            sb.Unindent(flagsCheckSuffix);
                        else
                            MetaDebug.Assert(flagsCheckSuffix == "", "!hasFlags, but check suffix is nonempty");

                        sb.AppendLine("break;");
                        sb.Unindent("}");
                    }
                }
                sb.AppendLine("default: SkipWireType(reader, memberWireType); break;");
                sb.Unindent("}"); // switch

                sb.Unindent("}"); // for loop

                // Create instance
                if (_typeSpec.UsesConstructorDeserialization)
                {
                    string parameters = string.Join(
                        ", ",
                        _typeSpec.ConstructorInitializedMembers
                            .Select(
                                x => CreateSafeMemberName(x.Name)));

                    if (useTrampolines)
                        sb.AppendLine($"{varName} = Trampolines.CreateInstance_{_generator.MethodSuffixForType(_typeSpec.Type)}(new object[]{{{parameters}}});");
                    else
                        sb.AppendLine($"{varName} = new {_typeSpec.Type.ToGlobalNamespaceQualifiedTypeString()}({parameters});");

                    foreach (MetaSerializableMember member in _typeSpec.Members)
                    {
                        if (!_typeSpec.ConstructorInitializedMembers.Contains(member))
                        {
                            sb.AppendLine($"if ({"ASSIGNED_" + CreateSafeMemberName(member.Name)})");
                            sb.Indent();
                            sb.AppendLine(useTrampolines ? $"Trampolines.Set_{_generator.MethodSuffixForType(_typeSpec.Type)}_{member.Name}(ref {varName}, {CreateSafeMemberName(member.Name)});" : $"{varName}.{member.Name} = {CreateSafeMemberName(member.Name)};");
                            sb.Unindent();
                        }
                    }
                }

                // Invoke [MetaOnDeserialized]-marked methods
                if (_typeSpec.OnDeserializedMethods.Count > 0)
                {
                    sb.AppendLine();
                    foreach (MethodInfo onDeserializedMethod in _typeSpec.OnDeserializedMethods)
                    {
                        bool takesParams = onDeserializedMethod.GetParameters().Any();

                        if (useTrampolines)
                        {
                            if (takesParams)
                                sb.AppendLine($"Trampolines.InvokeOnDeserializedWithParams_{_generator.MethodSuffixForType(_typeSpec.Type)}_{onDeserializedMethod.Name}(ref {varName}, new MetaOnDeserializedParams(ref context));");
                            else
                                sb.AppendLine($"Trampolines.InvokeOnDeserialized_{_generator.MethodSuffixForType(_typeSpec.Type)}_{onDeserializedMethod.Name}(ref {varName});");
                        }
                        else
                        {
                            if (takesParams)
                                sb.AppendLine($"{varName}.{onDeserializedMethod.Name}(new MetaOnDeserializedParams(ref context));");
                            else
                                sb.AppendLine($"{varName}.{onDeserializedMethod.Name}();");
                        }
                    }
                }
            }

            public void AppendMembersMetaRefTraverseCode(IndentedStringBuilder sb, string varName, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines)
            {
                bool isFirst = true;
                foreach (MetaSerializableMember member in _typeSpec.Members)
                {
                    if (isFirst)
                        isFirst = false;
                    else
                        sb.AppendLine();

                    Type memberType = member.Type;

                    if (metaRefContainingTypes.Contains(memberType))
                    {
                        // Handle member versioning (if has version attributes)
                        int? addedInVersion   = member.CodeGenInfo.AddedInLogicVersion;
                        int? removedInVersion = member.CodeGenInfo.RemovedInLogicVersion;
                        bool hasVersionInfo   = (addedInVersion != null) || (removedInVersion != null);

                        if (hasVersionInfo)
                        {
                            string minVersion = addedInVersion?.ToString(CultureInfo.InvariantCulture) ?? "0";
                            string maxVersion = removedInVersion?.ToString(CultureInfo.InvariantCulture) ?? "int.MaxValue";
                            sb.AppendLine($"if (IsVersionInRange(ref context, {minVersion}, {maxVersion}))");
                            sb.Indent("{");
                        }

                        // Handle member flags (if has any)
                        bool hasFlags = member.Flags != MetaMemberFlags.None;
                        if (hasFlags)
                        {
                            string flagsStr = MemberFlagsToMask(member.Flags);
                            sb.AppendLine($"if ((context.ExcludeFlags & {flagsStr}) == 0)");
                            sb.Indent("{");
                        }

                        string getMemberStr = useTrampolines ? $"Trampolines.Get_{_generator.MethodSuffixForType(_typeSpec.Type)}_{member.Name}(ref {varName})" : $"{varName}.{member.Name}";
                        sb.AppendLine($"{_generator.MetaRefTraverseExpression(memberType, getMemberStr)};");

                        if (hasFlags)
                            sb.Unindent("}");

                        if (hasVersionInfo)
                            sb.Unindent("}");
                    }
                    else
                        sb.AppendLine($"// Type {member.CodeGenInfo.GlobalNamespaceQualifiedTypeString} of member {member.Name} does not contain MetaRefs");
                }
            }

            public void Traverse(object obj, DynamicTraversal.IVisitor visitor)
            {
                foreach (MetaSerializableMember member in _typeSpec.Members)
                {
                    object memberObj = member.GetValue(obj);
                    visitor.BeginTraverseMember(Type, obj, member, memberObj);
                    visitor.Traverse(member.Type, memberObj);
                    visitor.EndTraverseMember(Type, obj, member, memberObj);
                }
            }
        }

        class EnumInfo : MetaSerializableTypeInfo
        {
            public EnumInfo(TaggedSerializerRoslynGenerator generator, MetaSerializableType typeSpec)
                : base(generator, typeSpec)
            {
                UnderlyingType = _typeSpec.Type.GetEnumUnderlyingType();
            }

            readonly Type UnderlyingType;

            public override IEnumerable<Type> DirectlyContainedTypes => new Type[]{ UnderlyingType };

            public override void AppendSerializationCode(IndentedStringBuilder sb, string valueExpr, bool useTrampolines, bool spaceEnsured)
            {
                string underlyingTypeName = UnderlyingType.ToGlobalNamespaceQualifiedTypeString();
                sb.AppendLine($"{_generator.SerializationExpression(UnderlyingType, $"({underlyingTypeName}){valueExpr}", spaceEnsured)};");
            }

            public override void AppendDeserializationCode(IndentedStringBuilder sb, string varName, bool useTrampolines)
            {
                WireDataType underlyingWireType = TaggedWireSerializer.GetWireType(UnderlyingType, _generator._typeRegistry);
                sb.AppendLine($"{_generator.DeserializationExpression(UnderlyingType, "underlyingValue", $"WireDataType.{underlyingWireType}")};");
                sb.AppendLine($"{varName} = ({_typeSpec.GlobalNamespaceQualifiedTypeString})underlyingValue;");
            }

            public override void AppendMetaRefTraverseCode(IndentedStringBuilder sb, string varName, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines)
            {
                // Enums do not contain MetaRefs.
            }

            public override void Traverse(object obj, DynamicTraversal.IVisitor visitor)
            {
                visitor.VisitEnum(Type, obj);
            }

            protected override int? MaxSerializedBytes()
            {
                return _generator._typeInfoCache[UnderlyingType].MaxSerializedBytesStatic();
            }
        }

        class NullableEnumInfo : MetaSerializableTypeInfo
        {
            public NullableEnumInfo(TaggedSerializerRoslynGenerator generator, MetaSerializableType typeSpec)
                : base(generator, typeSpec)
            {
                EnumType = _typeSpec.Type.GetSystemNullableElementType();
                EnumUnderlyingType = EnumType.GetEnumUnderlyingType();
            }

            readonly Type EnumType;
            readonly Type EnumUnderlyingType;

            public override IEnumerable<Type> DirectlyContainedTypes => new Type[]{ NullableType(EnumUnderlyingType) };

            public override void AppendSerializationCode(IndentedStringBuilder sb, string valueExpr, bool useTrampolines, bool spaceEnsured)
            {
                string enumUnderlyingTypeName = EnumUnderlyingType.ToGlobalNamespaceQualifiedTypeString();
                sb.AppendLine($"{_generator.SerializationExpression(NullableType(EnumUnderlyingType), $"({enumUnderlyingTypeName}?){valueExpr}", spaceEnsured)};");
            }

            public override void AppendDeserializationCode(IndentedStringBuilder sb, string varName, bool useTrampolines)
            {
                WireDataType nullableUnderlyingWireType = TaggedWireSerializer.PrimitiveTypeWrapNullable(TaggedWireSerializer.GetWireType(EnumUnderlyingType, _generator._typeRegistry));
                sb.AppendLine($"{_generator.DeserializationExpression(NullableType(EnumUnderlyingType), "nullableUnderlyingValue", $"WireDataType.{nullableUnderlyingWireType}")};");
                sb.AppendLine($"{varName} = ({_typeSpec.GlobalNamespaceQualifiedTypeString})nullableUnderlyingValue;");
            }

            public override void AppendMetaRefTraverseCode(IndentedStringBuilder sb, string varName, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines)
            {
                // Nullable enums do not contain MetaRefs.
            }

            public override void Traverse(object obj, DynamicTraversal.IVisitor visitor)
            {
                visitor.VisitNullableEnum(Type, obj);
            }

            protected override int? MaxSerializedBytes()
            {
                return _generator._typeInfoCache[NullableType(EnumUnderlyingType)].MaxSerializedBytesStatic();
            }
        }

        class StringIdInfo : MetaSerializableTypeInfo
        {
            public StringIdInfo(TaggedSerializerRoslynGenerator generator, MetaSerializableType typeSpec)
                : base(generator, typeSpec)
            {
            }

            public override IEnumerable<Type> DirectlyContainedTypes => Enumerable.Empty<Type>();

            public override void AppendSerializationCode(IndentedStringBuilder sb, string valueExpr, bool useTrampolines, bool spaceEnsured)
            {
                sb.AppendLine($"writer.WriteString({valueExpr} != null ? {valueExpr}.Value : null);");
            }

            public override void AppendDeserializationCode(IndentedStringBuilder sb, string varName, bool useTrampolines)
            {
                sb.AppendLine($"{varName} = {_typeSpec.GlobalNamespaceQualifiedTypeString}.FromString(reader.ReadString(context.MaxStringSize));");
            }

            public override void Traverse(object obj, DynamicTraversal.IVisitor visitor)
            {
                visitor.VisitStringId(Type, obj);
            }

            public override void AppendMetaRefTraverseCode(IndentedStringBuilder sb, string varName, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines)
            {
                // StringIds do not contain MetaRefs.
            }
        }

        class DynamicEnumInfo : MetaSerializableTypeInfo
        {
            public DynamicEnumInfo(TaggedSerializerRoslynGenerator generator, MetaSerializableType typeSpec)
                : base(generator, typeSpec)
            {
            }

            public override IEnumerable<Type> DirectlyContainedTypes => Enumerable.Empty<Type>();

            public override void AppendSerializationCode(IndentedStringBuilder sb, string valueExpr, bool useTrampolines, bool spaceEnsured)
            {
                sb.AppendLine($"if ({valueExpr} != null)");
                sb.Indent("{");
                sb.AppendLine($"writer.WriteVarInt{GetWriteFuncSuffix(spaceEnsured)}({valueExpr}.Id);");
                sb.Unindent("}");
                sb.AppendLine("else");
                sb.Indent("{");
                sb.AppendLine($"writer.WriteVarIntConst{GetWriteFuncSuffix(spaceEnsured)}(VarIntConst.MinusOne);");
                sb.Unindent("}");
            }

            public override void AppendDeserializationCode(IndentedStringBuilder sb, string varName, bool useTrampolines)
            {
                sb.AppendLine("int typeCode = reader.ReadVarInt();");
                sb.AppendLine("if (typeCode != -1)");
                sb.Indent("{");
                sb.AppendLine($"{varName} = {_typeSpec.GlobalNamespaceQualifiedTypeString}.FromId(typeCode) as {_typeSpec.GlobalNamespaceQualifiedTypeString} ?? throw new MetaSerializationException($\"Unknown enum element of {_typeSpec.Name}: {{typeCode}}\");");
                sb.Unindent("}");
                sb.AppendLine("else");
                sb.Indent("{");
                sb.AppendLine($"{varName} = null;");
                sb.Unindent("}");
            }

            public override void AppendMetaRefTraverseCode(IndentedStringBuilder sb, string varName, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines)
            {
                // DynamicEnums do not contain MetaRefs.
            }

            public override void Traverse(object obj, DynamicTraversal.IVisitor visitor)
            {
                visitor.VisitDynamicEnum(Type, obj);
            }

            protected override int? MaxSerializedBytes() => VarIntUtils.VarIntMaxBytes;
        }

        class GameConfigDataInfo : MetaSerializableTypeInfo
        {
            public GameConfigDataInfo(TaggedSerializerRoslynGenerator generator, MetaSerializableType typeSpec)
                : base(generator, typeSpec)
            {
                ConfigKeyType = _typeSpec.Type.GetGenericInterfaceTypeArguments(typeof(IHasGameConfigKey<>))[0];
            }

            readonly Type ConfigKeyType;

            public override IEnumerable<Type> DirectlyContainedTypes => new Type[]{ ConfigKeyType };

            public override void AppendSerializationCode(IndentedStringBuilder sb, string valueExpr, bool useTrampolines, bool spaceEnsured)
            {
                bool keyCanBeNull = ConfigKeyType.CanBeNull();
                string configKeyTypeName = ConfigKeyType.ToGlobalNamespaceQualifiedTypeString();

                sb.AppendLine($"IsConvertibleTo<IHasGameConfigKey<{configKeyTypeName}>>({valueExpr});");
                sb.AppendLine($"if ({valueExpr} != null)");
                sb.Indent("{");
                sb.AppendLine($"{_generator.SerializationExpression(ConfigKeyType, $"((IHasGameConfigKey<{configKeyTypeName}>){valueExpr}).ConfigKey", spaceEnsured)};");
                sb.Unindent("}");
                sb.AppendLine("else");
                sb.Indent("{");

                if (keyCanBeNull)
                    sb.AppendLine($"{_generator.SerializationExpression(ConfigKeyType, $"({configKeyTypeName})null", spaceEnsured)};");
                else if (_typeSpec.ConfigNullSentinelKey != null)
                {
                    sb.AppendLine($"{configKeyTypeName} sentinel = ({configKeyTypeName})context.TypeInfo.GetSerializableType(typeof({_typeSpec.GlobalNamespaceQualifiedTypeString})).ConfigNullSentinelKey;");
                    sb.AppendLine($"{_generator.SerializationExpression(ConfigKeyType, "sentinel", spaceEnsured)};");
                }
                else
                    sb.AppendLine($"throw new MetaSerializationException(\"Null {_typeSpec.Name} cannot be serialized, because its key type {ConfigKeyType.ToGenericTypeString()} is not nullable and {_typeSpec.Name} has no ConfigNullSentinelKey defined. A config reference is serialized as its key.\");");

                sb.Unindent("}");
            }

            public override void AppendDeserializationCode(IndentedStringBuilder sb, string varName, bool useTrampolines)
            {
                string          typeStr         = _typeSpec.GlobalNamespaceQualifiedTypeString;
                WireDataType    wireType        = TaggedWireSerializer.GetWireType(_typeSpec.Type, _generator._typeRegistry);

                sb.AppendLine($"{_generator.DeserializationExpression(ConfigKeyType, "configKey", $"WireDataType.{wireType}")};");
                // \todo [petri] implement generic getter to avoid boxing?

                bool keyCanBeNull = ConfigKeyType.CanBeNull();
                string configKeyTypeName = ConfigKeyType.ToGlobalNamespaceQualifiedTypeString();

                if (keyCanBeNull)
                {
                    sb.AppendLine("if (configKey != null)");
                    sb.Indent("{");
                }
                else if (_typeSpec.ConfigNullSentinelKey != null)
                {
                    sb.AppendLine($"if (!configKey.Equals(({configKeyTypeName})context.TypeInfo.GetSerializableType(typeof({_typeSpec.GlobalNamespaceQualifiedTypeString})).ConfigNullSentinelKey))");
                    sb.Indent("{");
                }

                sb.AppendLine($"IGameConfigDataResolver resolver = context.Resolver ?? throw new InvalidOperationException($\"Trying to deserialize a config data reference to '{{configKey}}' (type {_typeSpec.Type.ToGenericTypeString()}), but reference resolver is null. Note: if this happened while loading the game config, it probably means the reference needs to be wrapped in MetaRef<>, i.e. MetaRef<{_typeSpec.Type.ToGenericTypeString()}> .\");");
                sb.AppendLine($"{varName} = ({typeStr})resolver.ResolveReference(typeof({typeStr}), configKey);");

                if (keyCanBeNull || _typeSpec.ConfigNullSentinelKey != null)
                {
                    sb.Unindent("}");
                    sb.AppendLine("else");
                    sb.Indent("{");
                    sb.AppendLine($"{varName} = null;");
                    sb.Unindent("}");
                }
            }

            public override void AppendMetaRefTraverseCode(IndentedStringBuilder sb, string varName, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines)
            {
                // A game config data reference is just a reference, so we do not recurse into it.
            }

            public override void Traverse(object obj, DynamicTraversal.IVisitor visitor)
            {
                visitor.VisitGameConfigData(Type, obj);
            }

            protected override int? MaxSerializedBytes()
            {
                return _generator._typeInfoCache[ConfigKeyType].MaxSerializedBytesStatic();
            }
        }

        class MetaRefInfo : MetaSerializableTypeInfo
        {
            public MetaRefInfo(TaggedSerializerRoslynGenerator generator, MetaSerializableType typeSpec)
                : base(generator, typeSpec)
            {
                ConfigDataType = _typeSpec.Type.GetGenericArguments()[0];
                ConfigKeyType = ConfigDataType.GetGenericInterfaceTypeArguments(typeof(IHasGameConfigKey<>))[0];
                ConfigDataTypeName = ConfigDataType.ToGlobalNamespaceQualifiedTypeString();
                ConfigKeyTypeName = ConfigKeyType.ToGlobalNamespaceQualifiedTypeString();
            }

            readonly Type ConfigDataType;
            readonly Type ConfigKeyType;
            readonly string ConfigDataTypeName;
            readonly string ConfigKeyTypeName;

            public override IEnumerable<Type> DirectlyContainedTypes => new Type[]{ ConfigKeyType };

            public override void AppendSerializationCode(IndentedStringBuilder sb, string valueExpr, bool useTrampolines, bool spaceEnsured)
            {
                bool keyCanBeNull = ConfigKeyType.CanBeNull();
                object nullSentinelKey = _generator._typeInfoCache[ConfigDataType].SerializableType.ConfigNullSentinelKey;

                sb.AppendLine($"if ({valueExpr} != null)");
                sb.Indent("{");
                sb.AppendLine($"{_generator.SerializationExpression(ConfigKeyType, $"({ConfigKeyTypeName}){valueExpr}.KeyObject", spaceEnsured)};");
                sb.Unindent("}");
                sb.AppendLine("else");
                sb.Indent("{");

                if (keyCanBeNull)
                    sb.AppendLine($"{_generator.SerializationExpression(ConfigKeyType, $"({ConfigKeyTypeName})null", spaceEnsured)};");
                else if (nullSentinelKey != null)
                {
                    sb.AppendLine($"{ConfigKeyTypeName} sentinel = ({ConfigKeyTypeName})context.TypeInfo.GetSerializableType(typeof({ConfigDataTypeName})).ConfigNullSentinelKey;");
                    sb.AppendLine($"{_generator.SerializationExpression(ConfigKeyType, "sentinel", spaceEnsured)};");
                }
                else
                    sb.AppendLine($"throw new MetaSerializationException(\"Null {_typeSpec.Name} cannot be serialized, because its key type {ConfigKeyType.ToGenericTypeString()} is not nullable and {ConfigDataTypeName} has no ConfigNullSentinelKey defined. A config reference is serialized as its key.\");");

                sb.Unindent("}");
            }

            public override void AppendDeserializationCode(IndentedStringBuilder sb, string varName, bool useTrampolines)
            {
                WireDataType wireType = TaggedWireSerializer.GetWireType(_typeSpec.Type, _generator._typeRegistry);

                sb.AppendLine($"{_generator.DeserializationExpression(ConfigKeyType, "configKey", $"WireDataType.{wireType}")};");

                bool keyCanBeNull = ConfigKeyType.CanBeNull();
                object nullSentinelKey = _generator._typeInfoCache[ConfigDataType].SerializableType.ConfigNullSentinelKey;

                if (keyCanBeNull)
                {
                    sb.AppendLine("if (configKey != null)");
                    sb.Indent("{");
                }
                else if (nullSentinelKey != null)
                {
                    sb.AppendLine($"if (!configKey.Equals(({ConfigKeyType.ToGlobalNamespaceQualifiedTypeString()})context.TypeInfo.GetSerializableType(typeof({ConfigDataType.ToGlobalNamespaceQualifiedTypeString()})).ConfigNullSentinelKey))");
                    sb.Indent("{");
                }

                if (Type.IsGenericTypeOf(typeof(MetaRef<>)))
                {
                    sb.AppendLine($"MetaRef<{ConfigDataTypeName}> metaRef = MetaRef<{ConfigDataTypeName}>.FromKey(configKey);");
                    sb.AppendLine("if (context.Resolver != null)");
                    sb.AppendLine("    metaRef.ResolveInPlace(context.Resolver);");
                    sb.AppendLine($"{varName} = metaRef;");
                }
                else
                {
                    sb.AppendLine($"MetaConfigId<{ConfigDataTypeName}> metaConfigId = MetaConfigId<{ConfigDataTypeName}>.FromKey(configKey);");
                    sb.AppendLine($"{varName} = metaConfigId;");
                }

                if (keyCanBeNull || nullSentinelKey != null)
                {
                    sb.Unindent("}");
                    sb.AppendLine("else");
                    sb.Indent("{");
                    sb.AppendLine($"{varName} = null;");
                    sb.Unindent("}");
                }
            }

            public override void AppendMetaRefTraverseCode(IndentedStringBuilder sb, string varName, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines)
            {
                sb.AppendLine($"if ({varName} != null)");
                sb.Indent("{");
                if (Type.IsGenericTypeOf(typeof(MetaRef<>)))
                    sb.AppendLine($"context.VisitMetaRef({varName});");
                else
                    sb.AppendLine($"context.VisitMetaConfigId({varName});");
                sb.Unindent("}");
            }

            public override void Traverse(object obj, DynamicTraversal.IVisitor visitor)
            {
                if (Type.IsGenericTypeOf(typeof(MetaRef<>)))
                    visitor.VisitMetaRef(Type, obj);
                else
                    visitor.VisitMetaConfigId(Type, obj);
            }

            protected override int? MaxSerializedBytes()
            {
                return _generator._typeInfoCache[ConfigKeyType].MaxSerializedBytesStatic();
            }
        }

        class GameConfigDataContentInfo : MetaSerializableTypeInfo
        {
            public GameConfigDataContentInfo(TaggedSerializerRoslynGenerator generator, MetaSerializableType typeSpec, MetaSerializableType gameConfigDataTypeSpec)
                : base(generator, typeSpec)
            {
                if (gameConfigDataTypeSpec.Type != typeSpec.Type.GetGenericArguments()[0])
                    throw new ArgumentException($"{gameConfigDataTypeSpec.Type.ToNamespaceQualifiedTypeString()} does not match config data content type of {typeSpec.Type.ToNamespaceQualifiedTypeString()}");

                _gameConfigDataTypeSpec = gameConfigDataTypeSpec;
            }

            MetaSerializableType _gameConfigDataTypeSpec;

            Type GameConfigDataType => _gameConfigDataTypeSpec.Type;

            public override IEnumerable<Type> DirectlyContainedTypes => new ConcreteClassOrStructMembersInfo(_generator, _gameConfigDataTypeSpec).DirectlyContainedTypes;

            public override void AppendSerializationCode(IndentedStringBuilder sb, string valueExpr, bool useTrampolines, bool spaceEnsured)
            {
                // \note GameConfigDataContent<TConfigData> has `class` constraint on TConfigData,
                //       so we know it is nullable. If you remove the constraint, please update
                //       this code to not assume nullability.
                //       #config-data-content-nullable
                // \note IsClass isn't exactly the same as the `class` constraint, but good enough.
                MetaDebug.Assert(GameConfigDataType.IsClass, $"Expected {_typeSpec.Type.ToGenericTypeString()}'s content type to be a class type but it isn't");

                sb.AppendLine($"if ({valueExpr}.ConfigData != null)");
                sb.Indent("{");
                sb.AppendLine("writer.WriteVarIntConst(VarIntConst.One);");
                sb.AppendLine();
                sb.AppendLine($"{_generator.MembersSerializationExpression(GameConfigDataType, $"{valueExpr}.ConfigData", false)};");
                sb.Unindent("}");
                sb.AppendLine("else");
                sb.Indent("{");
                sb.AppendLine("writer.WriteVarIntConst(VarIntConst.Zero);");
                sb.Unindent("}");
            }

            public override void AppendDeserializationCode(IndentedStringBuilder sb, string varName, bool useTrampolines)
            {
                // \note GameConfigDataContent<TConfigData> has `class` constraint on TConfigData,
                //       so we know it is nullable. If you remove the constraint, please update
                //       this code to not assume nullability.
                //       #config-data-content-nullable
                // \note IsClass isn't exactly the same as the `class` constraint, but good enough.
                MetaDebug.Assert(GameConfigDataType.IsClass, $"Expected {_typeSpec.Type.ToGenericTypeString()}'s content type to be a class type but it isn't");

                sb.AppendLine("int hasValue = reader.ReadVarInt();");
                sb.AppendLine("if (hasValue != 0)");
                sb.Indent("{");
                sb.AppendLine($"{_generator.MembersDeserializationExpression(GameConfigDataType, "configData")};");
                sb.AppendLine($"{varName} = new {_typeSpec.GlobalNamespaceQualifiedTypeString}(configData);");
                sb.Unindent("}");
                sb.AppendLine("else");
                sb.Indent("{");
                sb.AppendLine($"{varName} = new {_typeSpec.GlobalNamespaceQualifiedTypeString}(null);");
                sb.Unindent("}");
            }

            public override void AppendMetaRefTraverseCode(IndentedStringBuilder sb, string varName, OrderedSet<Type> metaRefContainingTypes, bool useTrampolines)
            {
                sb.AppendLine($"if ({varName}.ConfigData != null)");
                sb.Indent("{");
                sb.AppendLine($"{_generator.MembersMetaRefTraverseExpression(GameConfigDataType, $"{varName}.ConfigData")};");
                sb.Unindent("}");
            }

            public override void Traverse(object obj, DynamicTraversal.IVisitor visitor)
            {
                IGameConfigDataContent configDataContent = (IGameConfigDataContent)obj;
                visitor.BeginTraverseGameConfigDataContent(Type, obj);
                if (obj != null)
                    visitor.TraverseMembers(GameConfigDataType, configDataContent.ConfigDataObject);
                visitor.EndTraverseGameConfigDataContent(Type, obj);
            }
        }
    }
}
