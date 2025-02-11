// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

#if UNITY_EDITOR || NETCOREAPP
#   define ENABLE_SERIALIZER_GENERATION
#endif

using Metaplay.Core.Config;
using Metaplay.Core.Model;
using Metaplay.Core.Profiling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using static System.FormattableString;

namespace Metaplay.Core.Serialization
{
    /// <summary>
    /// Information of a serialized Property or Field in [MetaSerializable] type. The members may
    /// be marked with [MetaMember] attribute or they may be implicitly serializable.
    /// </summary>
    public class MetaSerializableMember
    {
        public readonly int                          TagId;
        public readonly MetaMemberFlags              Flags;
        public readonly MemberInfo                   MemberInfo;
        public          MethodInfo                   OnDeserializationFailureMethod;
        public readonly SerializerGenerationOnlyInfo CodeGenInfo;

        public string                 Name                               => MemberInfo.Name;
        public Type                   Type                               => MemberInfo.GetDataMemberType();
        public Action<object, object> SetValue                           => MemberInfo.GetDataMemberSetValueOnDeclaringType();
        public Func<object, object>   GetValue                           => MemberInfo.GetDataMemberGetValueOnDeclaringType();
        public Type                   DeclaringType                      => MemberInfo.DeclaringType;
        public string                 OnDeserializationFailureMethodName => OnDeserializationFailureMethod?.Name;

        public struct SerializerGenerationOnlyInfo
        {
#if ENABLE_SERIALIZER_GENERATION
            public string GlobalNamespaceQualifiedTypeString { get; }
            public int?   AddedInLogicVersion;
            public int?   RemovedInLogicVersion;
            public int    MaxCollectionSize;

            public SerializerGenerationOnlyInfo(string globalNamespaceQualifiedTypeString, int? addedInLogicVersion, int? removedInLogicVersion, int maxCollectionSize)
            {
                GlobalNamespaceQualifiedTypeString = globalNamespaceQualifiedTypeString;
                AddedInLogicVersion                = addedInLogicVersion;
                RemovedInLogicVersion              = removedInLogicVersion;
                MaxCollectionSize                  = maxCollectionSize;
            }
#endif
        }

        public MetaSerializableMember(
            int tagId,
            MetaMemberFlags flags,
            MemberInfo memberInfo,
            SerializerGenerationOnlyInfo serializerGenerationOnlyInfo)
        {
            TagId       = tagId;
            Flags       = flags;
            MemberInfo  = memberInfo;
            CodeGenInfo = serializerGenerationOnlyInfo;
        }

        public override string ToString()
        {
            return Invariant($"MetaSerializableMember{{ tagId={TagId}, flags={Flags}, type={Type.Name}, name={Name} }}");
        }
    }

    /// <summary>
    /// Information of a [MetaSerializable] type.
    /// </summary>
    public class MetaSerializableType
    {
        public Type Type { get; }

        /// <summary>
        /// Name of type (using ToGenericTypeString())
        /// </summary>
        public string                                     Name                           { get; }
        public bool                                       UsesImplicitMembers            { get; set; }
        public bool                                       UsesConstructorDeserialization { get; set; }
        public bool                                       IsPublic                       { get; set; }
        public WireDataType                               WireType                       { get; }
        public bool                                       HasSerializableAttribute       { get; } // \todo: check if this is still actually needed
        public int                                        TypeCode                       { get; set; }
        public List<MetaSerializableMember>               Members                        { get; set; } // null if not a concrete object

        /// <summary>
        /// This is only set if <see cref="DeserializationConstructor"/> is not null and <see cref="UsesConstructorDeserialization"/> is <c>True</c>.
        /// </summary>
        public List<MetaSerializableMember> ConstructorInitializedMembers { get; set; }

        /// <summary>
        /// Constructor used for deserialization, this is only set if <see cref="UsesConstructorDeserialization"/> is true.
        /// </summary>
        public ConstructorInfo                            DeserializationConstructor     { get; set; }
        public Dictionary<int, object>                    ConstructorParameterValues     { get; set; }

        /// <summary>
        /// The dictionary of all derived classes where key is the concrete type's typecode. This contains
        /// all derived types and not just the immediate descendants. (For example, in A -> B -> C, C would
        /// have both A and B in this set.)
        /// </summary>
        public Dictionary<int, Type>                        DerivedTypes                { get; set; }
        public List<MethodInfo>                             OnDeserializedMethods       { get; set; }

        /// <summary>
        /// Optionally present for IGameConfigData types.
        /// If present, this is the value of the static member called ConfigNullSentinelKey,
        /// used as a sentinel for serializing a null config reference when the key type
        /// is non-nullable and would thus otherwise be unable to represent null config references.
        /// </summary>
        public object                                       ConfigNullSentinelKey       { get; set; }

        public MetaDeserializationConverter[]               DeserializationConverters { get; set; }

        public int?                                         AddedInVersion            { get; set; }

#if ENABLE_SERIALIZER_GENERATION
        /// <summary>
        /// Name of type (using ToGenericTypeString())
        /// </summary>
        public string                                       GlobalNamespaceQualifiedTypeString { get; }
#endif

        public bool IsEnum                  => Type.IsEnum;
        public bool IsCollection            => WireType == WireDataType.ValueCollection || WireType == WireDataType.KeyValueCollection;
        public bool IsConcreteObject        => WireType == WireDataType.Struct || WireType == WireDataType.NullableStruct;
        public bool IsObject                => WireType == WireDataType.Struct || WireType == WireDataType.NullableStruct || WireType == WireDataType.AbstractStruct;
        public bool IsTuple                 => Type.ImplementsInterface<ITuple>();
        public bool IsAbstract              => WireType == WireDataType.AbstractStruct;
        public bool IsGameConfigData        => Type.ImplementsGenericInterface(typeof(IGameConfigData<>));
        public bool IsGameConfigDataContent => Type.IsGenericTypeOf(typeof(GameConfigDataContent<>));
        public bool IsMetaRef               => Type.IsGenericTypeOf(typeof(MetaRef<>));
        public bool IsMetaConfigId          => Type.IsGenericTypeOf(typeof(MetaConfigId<>));
        public bool IsSystemNullable        => Type.IsGenericTypeOf(typeof(Nullable<>));

        public MetaSerializableType(Type type, WireDataType wireType, bool hasSerializableAttribute)
        {
            Type                        = type;
            Name                        = type.ToGenericTypeString();
            WireType                    = wireType;
            HasSerializableAttribute    = hasSerializableAttribute;
#if ENABLE_SERIALIZER_GENERATION
            GlobalNamespaceQualifiedTypeString = type.ToGlobalNamespaceQualifiedTypeString();
#endif
        }

        // Helper struct for capturing member data for re-creation from generated code.
        public readonly struct MemberBuiltinData
        {
            public readonly int TagId;
            public readonly string Name;
            public readonly MetaMemberFlags Flags;
            public readonly Type DeclaringType;
            public readonly string OnDeserializationFailureMethodName;

            public MemberBuiltinData(
                int tagId,
                string name,
                MetaMemberFlags flags,
                Type declaringType,
                string onDeserializationFailureMethodName)
            {
                TagId                              = tagId;
                Name                               = name;
                Flags                              = flags;
                DeclaringType                      = declaringType;
                OnDeserializationFailureMethodName = onDeserializationFailureMethodName;
            }
        }

        // Helper function for re-creating a MetaSerializableType instance from generated source code.
        public static MetaSerializableType CreateFromBuiltinData(
            Type type,
            bool isPublic,
            bool usesImplicitMembers,
            WireDataType wireType,
            bool hasSerializableAttribute,
            int typeCode,
            MemberBuiltinData[] members,
            ValueTuple<int, Type>[] derivedTypes,
            ValueTuple<Type, string>[] onDeserializedMethods,
            bool hasConfigNullSentinelKey,
            bool hasDeserializationConverters,
            bool usesConstructorDeserialization,
            int? addedInVersion)
        {
            MetaSerializableType ret = new MetaSerializableType(type, wireType, hasSerializableAttribute);
            ret.TypeCode            = typeCode;
            ret.IsPublic            = isPublic;
            ret.UsesImplicitMembers = usesImplicitMembers;
            ret.AddedInVersion      = addedInVersion;

            if (members != null)
            {
                ret.Members = new List<MetaSerializableMember>(members.Length);
                foreach (MemberBuiltinData memberData in members)
                {
                    MemberInfo memberInfo =
                        (MemberInfo)memberData.DeclaringType.GetProperty(memberData.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?? memberData.DeclaringType.GetField(memberData.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (memberInfo == null)
                    {
                        DebugLog.Error("Can't find member {name} in type {type}", memberData.Name, memberData.DeclaringType.FullName);
                        throw new InvalidOperationException($"Can't find member {memberData.Name} in type {memberData.DeclaringType.FullName}");
                    }

                    MetaSerializableMember member = new MetaSerializableMember(memberData.TagId, memberData.Flags, memberInfo, serializerGenerationOnlyInfo: default);
                    if (memberData.OnDeserializationFailureMethodName != null)
                    {
                        member.OnDeserializationFailureMethod = memberData.DeclaringType.GetMethod(memberData.OnDeserializationFailureMethodName,
                            BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    }

                    ret.Members.Add(member);
                }
            }

            ret.DerivedTypes = derivedTypes?.ToDictionary(x => x.Item1, x => x.Item2);
            ret.OnDeserializedMethods = onDeserializedMethods?.Select(x => {
                return x.Item1.GetMethod(x.Item2, BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }).ToList();
            if (hasConfigNullSentinelKey)
                ret.ConfigNullSentinelKey = type.GetMember("ConfigNullSentinelKey", BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single().GetDataMemberGetValueOnDeclaringType()(null);
            if (hasDeserializationConverters)
            {
                List<MetaDeserializationConverter> converters = new List<MetaDeserializationConverter>();
                foreach (MetaDeserializationConverterAttributeBase converterAttribute in type.GetCustomAttributes<MetaDeserializationConverterAttributeBase>(inherit: false))
                    converters.AddRange(converterAttribute.CreateConverters(type));
                ret.DeserializationConverters = converters.ToArray();
            }
            else
            {
                ret.DeserializationConverters = Array.Empty<MetaDeserializationConverter>();
            }

            ret.UsesConstructorDeserialization = usesConstructorDeserialization;

            if (ret.UsesConstructorDeserialization)
            {
                (ConstructorInfo bestMatching, List<MetaSerializableMember> constructorInitializedMembers) = ret.FindBestMatchingConstructor();

                ret.DeserializationConstructor    = bestMatching;
                ret.ConstructorInitializedMembers = constructorInitializedMembers;
                ret.ConstructorParameterValues    = ret.CollectConstructorParameterValues();
            }

            return ret;
        }

        public override string ToString() => Name;

        public (ConstructorInfo bestMatching, List<MetaSerializableMember> constructorInitializedMembers) FindBestMatchingConstructor()
        {
            ConstructorInfo              bestMatching                      = null;
            bool                         bestMatchingHasAttribute          = false;
            bool                         foundMultipleMatchingConstructors = false;
            List<MetaSerializableMember> bestMatchingMembers               = new List<MetaSerializableMember>();
            foreach (ConstructorInfo constructorInfo in Type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                ParameterInfo[] parameterInfos = constructorInfo.GetParameters();

                // Early exit for types that use a parameterless constructor
                if (!UsesConstructorDeserialization && parameterInfos.Length == 0)
                    return (constructorInfo, null);

                HashSet<string>              parameterNames           = new HashSet<string>(parameterInfos.Length);
                List<string>                 duplicateParameterNames  = new List<string>();
                bool                         allMatch                 = true;
                bool                         duplicateMatchingMembers = false;
                List<MetaSerializableMember> matchingMembers          = new List<MetaSerializableMember>();

                foreach (ParameterInfo info in parameterInfos)
                {
                    string sanitizedParamName = MetaSerializationUtil.SanitizeMemberName(info.Name);

                    bool matching = false;

                    foreach (MetaSerializableMember member in Members)
                    {
                        if (sanitizedParamName.Equals(MetaSerializationUtil.SanitizeMemberName(member.Name)))
                        {
                            if (info.ParameterType == member.Type)
                            {
                                if (matching)
                                    duplicateMatchingMembers = true;

                                matchingMembers.Add(member);
                                matching = true;
                            }
                        }
                    }

                    if (matching)
                    {
                        if (!parameterNames.Add(sanitizedParamName))
                            duplicateParameterNames.Add(sanitizedParamName);
                    }
                    else
                        allMatch = false;
                }

                bool hasDuplicateParameters  = duplicateParameterNames.Count > 0;

                bool hasAttribute = constructorInfo.GetCustomAttributes<MetaDeserializationConstructorAttribute>().Any();
                if (hasAttribute)
                {
                    if (hasDuplicateParameters)
                        throw new InvalidOperationException($"Constructor in '{Type.FullName}' has duplicate parameters ({string.Join(", ", duplicateParameterNames)}) after sanitization, this is not supported for constructor based deserialization.");
                    if (!allMatch)
                        throw new InvalidOperationException($"Constructor in '{Type.FullName}' with attribute {nameof(MetaDeserializationConstructorAttribute)} does not have a valid signature. Constructor parameters must have the same name and type as the MetaMembers defined in this type.");
                    if (bestMatchingHasAttribute)
                        throw new InvalidOperationException($"Type '{Type.FullName} has multiple constructors with the {nameof(MetaDeserializationConstructorAttribute)} attribute, this is not valid. Please ensure there is only one {nameof(MetaDeserializationConstructorAttribute)} attribute.");

                    if (duplicateMatchingMembers)
                    {
                        IGrouping<string, MetaSerializableMember> duplicateMembers = matchingMembers.GroupBy(x => MetaSerializationUtil.SanitizeMemberName(x.Name)).First(x => x.Count() > 1);
                        throw new InvalidOperationException($"Constructor parameter in '{Type.FullName}' matches multiple members ({string.Join(", ", duplicateMembers.Select(x => x.Name))}) after sanitization, this is not supported for constructor based deserialization.");
                    }

                    bestMatchingMembers      = matchingMembers;
                    bestMatching             = constructorInfo;
                    bestMatchingHasAttribute = true;
                }

                if (allMatch && !hasDuplicateParameters && !duplicateMatchingMembers)
                {
                    if (bestMatching == null || (parameterInfos.Length > bestMatching.GetParameters().Length && !bestMatchingHasAttribute))
                    {
                        bestMatchingMembers              = matchingMembers;
                        bestMatching                     = constructorInfo;
                        foundMultipleMatchingConstructors = false;
                    }
                    else if (!bestMatchingHasAttribute && parameterInfos.Length == bestMatching.GetParameters().Length)
                        foundMultipleMatchingConstructors = true;
                }
            }

            if (bestMatching == null)
            {
                if (UsesConstructorDeserialization)
                    throw new InvalidOperationException($"Could not find matching constructor for type '{Type.FullName}'. Constructor parameters must have the same name and type as the MetaMembers defined in this type.");
                else
                    throw new InvalidOperationException($"Could not find empty constructor for type '{Type.FullName}'.");
            }
            else
            {
                if(foundMultipleMatchingConstructors && !bestMatchingHasAttribute)
                    throw new InvalidOperationException($"Multiple valid deserialization constructors found for '{Type.FullName}', please specify the correct constructor using [{nameof(MetaDeserializationConstructorAttribute)}].");
                if (bestMatchingMembers.Count < Members.Count)
                {
                    List<MetaSerializableMember> readOnlyMembers = new List<MetaSerializableMember>();
                    foreach (MetaSerializableMember member in Members)
                    {
                        if (bestMatchingMembers.Contains(member))
                            continue;

                        switch (member.MemberInfo)
                        {
                            case FieldInfo fieldInfo:
                                if (fieldInfo.IsInitOnly)
                                    readOnlyMembers.Add(member);
                                break;
                            case PropertyInfo propertyInfo:
                                if (propertyInfo.SetMethod == null)
                                    readOnlyMembers.Add(member);
                                break;
                        }
                    }

                    if (readOnlyMembers.Count > 0)
                        throw new InvalidOperationException($"'{Type.FullName}' uses constructor deserialization but has read-only members ({string.Join(", ", readOnlyMembers.Select(x=>x.Name))}) which are not populated by the best matching constructor, please make them writeable or add them to the deserialization constructor.");
                }
            }

            return (bestMatching, constructorInitializedMembers: bestMatchingMembers);
        }

        public Dictionary<int, object> CollectConstructorParameterValues()
        {
            Dictionary<int, object> members        = new Dictionary<int, object>();
            ParameterInfo[]         parameterInfos = DeserializationConstructor.GetParameters();

            foreach (MetaSerializableMember member in ConstructorInitializedMembers)
            {
                ParameterInfo parameterInfo = parameterInfos.FirstOrDefault(x => MetaSerializationUtil.SanitizeMemberName(x.Name).Equals(MetaSerializationUtil.SanitizeMemberName(member.Name)));

                if (parameterInfo.TryGetDefaultValue(out object defaultValue))
                    members[member.TagId] = defaultValue;
            }

            return members;
        }
    }

    public class MetaSerializerTypeInfo
    {
        public Dictionary<Type, MetaSerializableType> Specs;
        public uint FullTypeHash;
    }

    // MetaSerializerTypeRegistry

    public class MetaSerializerTypeRegistry
    {
        public static MetaSerializerTypeRegistry Instance => MetaplayServices.Get<MetaSerializerTypeRegistry>();

        MetaSerializerTypeInfo                        _typeInfo;
        public IEnumerable<MetaSerializableType>      AllTypes => _typeInfo.Specs.Values;
        public MetaSerializerTypeInfo                 TypeInfo => _typeInfo;

        // Full hash of all MetaMember-tagged members of all MetaSerializable classes (for checking client/server compatibility)
        // \todo [petri] the computation is really naive and misses things like swapping members
        public uint FullProtocolHash => _typeInfo.FullTypeHash;

        public MetaSerializableType GetSerializableType(Type type)
        {
            if (_typeInfo.Specs.TryGetValue(type, out MetaSerializableType typeSpec))
                return typeSpec;
            else
                throw new KeyNotFoundException($"Type {type.ToGenericTypeString()} has not been registered in MetaSerializerTypeRegistry");
        }

        public bool TryGetSerializableType(Type type, out MetaSerializableType typeSpec)
        {
            return _typeInfo.Specs.TryGetValue(type, out typeSpec);
        }

        public bool TryGetDerivedSerializableType(Type type, int derivedTypeCode, out MetaSerializableType derivedTypeSpec)
        {
            if (_typeInfo.Specs.TryGetValue(type, out MetaSerializableType typeSpec))
            {
                if (typeSpec.DerivedTypes != null)
                {
                    if (typeSpec.DerivedTypes.TryGetValue(derivedTypeCode, out Type derivedType))
                        return _typeInfo.Specs.TryGetValue(derivedType, out derivedTypeSpec);
                }
            }

            derivedTypeSpec = null;
            return false;
        }

        #region Legacy static accessors

        public static MetaSerializableType GetTypeSpec(Type type)
        {
            return Instance.GetSerializableType(type);
        }

        public static bool TryGetTypeSpec(Type type, out MetaSerializableType typeSpec)
        {
            return Instance.TryGetSerializableType(type, out typeSpec);
        }

        public static bool TryGetDerivedType(Type type, int derivedTypeCode, out MetaSerializableType derivedTypeSpec)
        {
            return Instance.TryGetDerivedSerializableType(type, derivedTypeCode, out derivedTypeSpec);
        }

        #endregion

        public MetaSerializerTypeRegistry(MetaSerializerTypeInfo typeInfo)
        {
            _typeInfo = typeInfo;
        }

        public static Dictionary<Type, Dictionary<int, object>> CollectConstructorParameterValuesFromTypeSpec(Dictionary<Type, MetaSerializableType> typeSpecs)
        {
            Dictionary<Type, Dictionary<int, object>> parameterValues = new Dictionary<Type, Dictionary<int, object>>();
            foreach ((Type key, MetaSerializableType value) in typeSpecs)
            {
                ConstructorInfo constructorInfo = value.DeserializationConstructor;

                if (constructorInfo == null)
                    continue;

                Dictionary<int, object> members        = new Dictionary<int, object>();
                ParameterInfo[]         parameterInfos = constructorInfo.GetParameters();

                foreach (MetaSerializableMember member in value.Members)
                {
                    ParameterInfo parameterInfo = parameterInfos.FirstOrDefault(x => MetaSerializationUtil.SanitizeMemberName(x.Name).Equals(MetaSerializationUtil.SanitizeMemberName(member.Name)));

                    if (parameterInfo != null && parameterInfo.TryGetDefaultValue(out object defaultValue))
                        members[member.TagId] = defaultValue;
                }

                parameterValues[value.Type] = members;
            }

            return parameterValues;
        }

        /// <summary>
        /// Gets all non-abstract enabled MetaSerializable types that derive from <typeparamref name="TBaseType"/>. The type itself is not included in the set.
        /// </summary>
        public static IEnumerable<Type> GetConcreteDerivedTypes<TBaseType>()
        {
            MetaSerializableType spec = MetaSerializerTypeRegistry.GetTypeSpec(typeof(TBaseType));
            return spec.DerivedTypes.Values;
        }
    }
}
