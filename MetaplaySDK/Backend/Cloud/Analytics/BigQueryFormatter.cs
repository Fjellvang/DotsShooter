// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Google.Cloud.BigQuery.V2;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using Metaplay.Core.Analytics;
using Metaplay.Core.Config;
using Metaplay.Core.Math;
using Metaplay.Core.Serialization;
using Metaplay.Core.Session;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Metaplay.Cloud.Analytics
{
    public class BigQueryFormatter
    {
        internal BigQueryFormatter(IEnumerable<AnalyticsEventSpec> events, bool addContextFieldsAsExtraEventParams)
        {
            _eventFormatters = new MetaDictionary<Type, EventFormatter>();
            foreach (AnalyticsEventSpec analyticsEvent in events)
                _eventFormatters.Add(analyticsEvent.Type, new EventFormatter(analyticsEvent));

            // Sanity check event size.
            const int maxEventParamCount = 1000;
            foreach ((Type eventType, EventFormatter formatter) in _eventFormatters)
            {
                if (formatter.ExpectedParamCount > maxEventParamCount)
                {
                    throw new InvalidOperationException($"Event {eventType.ToGenericTypeString()} has {formatter.ExpectedParamCount} fields. This is an excessively large event. If you are using [BigQueryAnalyticsRecursionLimit], you may be using too high recursion limit.");
                }
            }

            // Prepare formatters for adding context fields as event_params extra fields.
            _extraFieldContextFormatters = new MetaDictionary<Type, EventFormatter>();
            if (addContextFieldsAsExtraEventParams)
            {
                foreach (Type contextType in TypeScanner.GetConcreteDerivedTypes<AnalyticsContextBase>())
                {
                    if (!contextType.IsMetaFeatureEnabled())
                        continue;

                    EventFormatter ef = new EventFormatter(contextType.Name, contextType, "$c:");

                    // Empty formatters are useless.
                    if (ef.ExpectedParamCount > 0)
                        _extraFieldContextFormatters.Add(contextType, ef);
                }
            }
        }

        public BigQueryFormatter(AnalyticsEventRegistry events, RuntimeOptionsRegistry options) : this(events.EventSpecs.Values, options.GetCurrent<AnalyticsSinkBigQueryOptions>().AddContextFieldsAsExtraEventParams) { }

        class EventFormatter
        {
            public class ParamSpec
            {
                public enum Kind
                {
                    Integer,
                    Float,
                    String,
                    Struct,
                    RuntimeTyped,
                    StringConstant,
                    RuntimeEnumerated,
                    RuntimeExtracted,

                    /// <summary>
                    /// Renders as `__recursion_limit` string. Used in place of a Struct or RuntimeTyped if the recursion limit for that type would be exceeded.
                    /// </summary>
                    RecursionLimit,
                }

                public readonly string                              FullName;
                public readonly Kind                                ValueKind;
                public readonly Func<object, object>                GetValue;
                public readonly ParamSpec[]                         StructMembers;
                public readonly MetaDictionary<Type, ParamSpec>  TypedParams;
                public readonly string                              StringConstant;
                public readonly ParamSpec                           ElementParamSpec;
                public readonly Func<object>                        GetExampleValue;
                public readonly Func<object, string>                ElementNameExtractor;

                public ParamSpec(
                    string fullName,
                    Kind valueKind,
                    Func<object, object> getValue,
                    ParamSpec[] structMembers = null,
                    MetaDictionary<Type, ParamSpec> typedParams = null,
                    string stringConstant = null,
                    ParamSpec elementParamSpec = null,
                    Func<object> getExampleValue = null,
                    Func<object, string> elementNameExtractor = null)
                {
                    FullName = fullName;
                    ValueKind = valueKind;
                    GetValue = getValue;
                    StructMembers = structMembers;
                    TypedParams = typedParams;
                    StringConstant = stringConstant;
                    ElementParamSpec = elementParamSpec;
                    GetExampleValue = getExampleValue;
                    ElementNameExtractor = elementNameExtractor;
                }

                public int GetExpectedParamCount()
                {
                    if (ValueKind == Kind.Struct)
                        return StructMembers.Sum(spec => spec.GetExpectedParamCount());
                    else if (ValueKind == Kind.RuntimeTyped)
                        return TypedParams.Values.Max(spec => spec.GetExpectedParamCount());
                    else if (ValueKind == Kind.RuntimeEnumerated)
                        return 1 * ElementParamSpec.GetExpectedParamCount();
                    else if (ValueKind == Kind.RuntimeExtracted)
                        return 1 * ElementParamSpec.GetExpectedParamCount();
                    else
                        return 1;
                }
            }

            public readonly string      EventName;
            public readonly ParamSpec[] TopLevelParameterSpecs;
            public readonly int         ExpectedParamCount;

            public EventFormatter(AnalyticsEventSpec spec)
            {
                EventName = spec.Type.GetCustomAttribute<BigQueryAnalyticsNameAttribute>()?.Name
                            ?? spec.EventType;

                TopLevelParameterSpecs = GetTopLevelParamSpecs(spec.Type, pathPrefix: "");
                ExpectedParamCount = TopLevelParameterSpecs.Sum(spec => spec.GetExpectedParamCount());
            }
            public EventFormatter(string name, Type type, string pathPrefix)
            {
                EventName = name;
                TopLevelParameterSpecs = GetTopLevelParamSpecs(type, pathPrefix);
                ExpectedParamCount = TopLevelParameterSpecs.Sum(spec => spec.GetExpectedParamCount());
            }

            public List<BigQueryInsertRow> Format(object eventBase)
            {
                List<BigQueryInsertRow> eventParams = new List<BigQueryInsertRow>(ExpectedParamCount);
                foreach (ParamSpec paramSpec in TopLevelParameterSpecs)
                    FlattenAndFormatInto(paramSpec, eventBase, pathPrefix: null, eventParams);
                return eventParams;
            }

            void FlattenAndFormatInto(ParamSpec paramSpec, object container, string pathPrefix, List<BigQueryInsertRow> dst)
            {
                if (paramSpec.ValueKind == ParamSpec.Kind.StringConstant)
                {
                    BigQueryInsertRow row = new BigQueryInsertRow();
                    row.Add("key", pathPrefix + paramSpec.FullName);
                    if (paramSpec.StringConstant != null)
                        row.Add("string_value", paramSpec.StringConstant);
                    dst.Add(row);
                    return;
                }
                else if (paramSpec.ValueKind == ParamSpec.Kind.RecursionLimit)
                {
                    dst.Add(new BigQueryInsertRow()
                    {
                        { "key", pathPrefix + paramSpec.FullName },
                        { "string_value", "__recursion_limit" },
                    });
                    return;
                }

                // Propagate nulls all the way to the leaf-fields. This makes the "schema" of the event
                // stay constant regardless of the contents of the event. This makes the data more readable,
                // but at the cost of emitting null fields.
                // \todo: add configuration switch for emitting / not emitting null fields (if so, we can early exit here)
                object value = container == null ? null : paramSpec.GetValue(container);

                if (paramSpec.ValueKind == ParamSpec.Kind.Struct)
                {
                    foreach (ParamSpec memberParamSpec in paramSpec.StructMembers)
                        FlattenAndFormatInto(memberParamSpec, value, pathPrefix, dst);
                }
                else if (paramSpec.ValueKind == ParamSpec.Kind.RuntimeTyped)
                {
                    if (value == null)
                    {
                        // Runtime typed fields are not handled as if they were null (unlike the others).
                        // To flatten them, we would need to flatten all possible types. While that is possible,
                        // it is probably less readable than not emitting them.
                        return;
                    }
                    FlattenAndFormatInto(paramSpec.TypedParams[value.GetType()], value, pathPrefix, dst);
                }
                else if (paramSpec.ValueKind == ParamSpec.Kind.RuntimeEnumerated)
                {
                    if (value == null)
                    {
                        // Runtime enumerated types are empty.
                        return;
                    }

                    ICollection collection = (ICollection)value;
                    uint index = 0;
                    foreach (object element in collection)
                    {
                        string subRootPath = pathPrefix + $"{paramSpec.FullName}:{index}";
                        FlattenAndFormatInto(paramSpec.ElementParamSpec, element, subRootPath, dst);
                        index++;
                    }
                }
                else if (paramSpec.ValueKind == ParamSpec.Kind.RuntimeExtracted)
                {
                    if (value == null)
                    {
                        // Runtime extracted types are empty.
                        return;
                    }

                    IEnumerable collection = (IEnumerable)value;
                    foreach (object element in collection)
                    {
                        // null elements cannot be extracted.
                        if (element == null)
                            continue;

                        // Empty names cannot be extracted.
                        string elementName = paramSpec.ElementNameExtractor(element);
                        if (elementName == null)
                            continue;

                        string subRootPath = pathPrefix + $"{paramSpec.FullName}:{elementName}";
                        FlattenAndFormatInto(paramSpec.ElementParamSpec, element, subRootPath, dst);
                    }
                }
                else
                {
                    string kindName;
                    switch (paramSpec.ValueKind)
                    {
                        case ParamSpec.Kind.Integer:   kindName = "int_value";     break;
                        case ParamSpec.Kind.Float:     kindName = "double_value";  break;
                        case ParamSpec.Kind.String:    kindName = "string_value";  break;
                        default: throw new InvalidOperationException("invalid kind");
                    }

                    BigQueryInsertRow row = new BigQueryInsertRow();
                    row.Add("key", pathPrefix + paramSpec.FullName);
                    if (value != null)
                        row.Add(kindName, value);
                    dst.Add(row);
                }
            }

            class FlattenContext
            {
                Dictionary<Type, int> _numTypesOnPath = new Dictionary<Type, int>();
                Dictionary<Type, int> _numLimitedRecursions = new Dictionary<Type, int>();
                Stack<Dictionary<Type, int>> _numTypesOnPathStash = new Stack<Dictionary<Type, int>>();

                // Normal recursion check to prevent stack overflow and give proper error messages

                public int GetNormalRecursionCount(Type type)
                {
                    return _numTypesOnPath.GetValueOrDefault(type, 0);
                }

                public void PushNormalRecursionCount(Type type)
                {
                    if (!_numTypesOnPath.TryGetValue(type, out int recursionCount))
                    {
                        _numTypesOnPath[type] = 1;
                        return;
                    }
                    _numTypesOnPath[type] = recursionCount + 1;
                }

                public void PopNormalRecursionCount(Type type)
                {
                    int recursionCount = _numTypesOnPath[type];
                    if (recursionCount == 1)
                        _numTypesOnPath.Remove(type);
                    else
                        _numTypesOnPath[type] = recursionCount - 1;
                }

                // Limited recursion check to enforce Recursion Limit

                public int GetLimitedRecursionCount(Type type)
                {
                    return _numLimitedRecursions.GetValueOrDefault(type, 0);
                }

                public void PushLimitedRecursionType(Type type)
                {
                    if (!_numLimitedRecursions.TryGetValue(type, out int recursionCount))
                    {
                        _numLimitedRecursions[type] = 1;
                        return;
                    }
                    _numLimitedRecursions[type] = recursionCount + 1;
                }

                public void PopLimitedRecursionType(Type type)
                {
                    int recursionCount = _numLimitedRecursions[type];
                    if (recursionCount == 1)
                        _numLimitedRecursions.Remove(type);
                    else
                        _numLimitedRecursions[type] = recursionCount - 1;
                }

                public void StashNormalRecursionCounts()
                {
                    _numTypesOnPathStash.Push(_numTypesOnPath);
                    _numTypesOnPath = new Dictionary<Type, int>();
                }

                public void UnstashNormalRecursionCounts()
                {
                    _numTypesOnPath = _numTypesOnPathStash.Pop();
                }
            }

            static ParamSpec[] GetTopLevelParamSpecs(Type type, string pathPrefix)
            {
                FlattenContext context = new FlattenContext();
                List<ParamSpec> specs = new List<ParamSpec>();
                AddParamFields(context, type, path: pathPrefix, debugPath: "", type, specs);
                return specs.ToArray();
            }

            static void AddParamFields(FlattenContext context, Type eventType, string path, string debugPath, Type type, List<ParamSpec> specs)
            {
                // Note that we choose only serializable members.
                // In particular, we avoid computed and compiler generated fields.

                if (!MetaSerializerTypeRegistry.TryGetTypeSpec(type, out MetaSerializableType serializableTypeSpec))
                    throw new InvalidOperationException($"Non-MetaSerializable type found in analytics event {eventType.ToGenericTypeString()} at {debugPath}: {type.ToGenericTypeString()}");

                foreach (MetaSerializableMember serializableMember in serializableTypeSpec.Members)
                {
                    MemberInfo  member      = serializableMember.MemberInfo;
                    Type        fieldType   = member.GetDataMemberType();

                    // Handle ignore attribute on field
                    BigQueryAnalyticsFormatAttribute fieldFormatAttributeMaybe = member.GetCustomAttribute<BigQueryAnalyticsFormatAttribute>();
                    if (fieldFormatAttributeMaybe != null && fieldFormatAttributeMaybe.Mode == BigQueryAnalyticsFormatMode.Ignore)
                        continue;

                    // Handle ignore attribute on type
                    BigQueryAnalyticsFormatAttribute typeFormatAttributeMaybe = fieldType.GetCustomAttribute<BigQueryAnalyticsFormatAttribute>();
                    if (typeFormatAttributeMaybe != null && typeFormatAttributeMaybe.Mode == BigQueryAnalyticsFormatMode.Ignore)
                        continue;

                    // Use name specified in [BigQueryAnalyticsName] if present, or C# member's name by default.
                    string parameterName = member.GetCustomAttribute<BigQueryAnalyticsNameAttribute>()?.Name
                                           ?? member.Name;

                    string                  fullName        = path + parameterName;
                    string                  fullDebugName   = debugPath + member.Name;
                    Func<object, object>    getter          = member.GetDataMemberGetValueOnDeclaringType();
                    ParamSpec               aggregateSpec;

                    if (fieldFormatAttributeMaybe != null && fieldFormatAttributeMaybe.Mode == BigQueryAnalyticsFormatMode.ExtractDictionaryElements)
                    {
                        ExtractRuntimeExtracted(context, eventType, fullName, fullDebugName, fieldType, getter, out aggregateSpec);
                    }
                    else if (!TryExtract(context, eventType, fullName, fullDebugName, fieldType, getter, out aggregateSpec))
                    {
                        throw new InvalidOperationException(
                            $"Unknown analytics event field format in {eventType.ToGenericTypeString()} in {fullDebugName}, Type = {member.GetDataMemberType().ToGenericTypeString()}. " +
                            "Could not register formatter for this type. You may use BigQueryAnalyticsFormatAttribute to disable or customize formatting of a field.");
                    }

                    specs.Add(aggregateSpec);
                }
            }

            static bool TryExtract(FlattenContext context, Type eventType, string fullName, string debugName, Type type, Func<object, object> getter, out ParamSpec spec)
            {
                // Sanity check attribute on the type itself. The normal case, the ignore is handled and filtered in AddParamFields. But that
                // doesn't handle cases where Ignored type is for example an element type of a field.
                BigQueryAnalyticsFormatAttribute typeFormatAttributeMaybe = type.GetCustomAttribute<BigQueryAnalyticsFormatAttribute>();
                if (typeFormatAttributeMaybe != null && typeFormatAttributeMaybe.Mode == BigQueryAnalyticsFormatMode.Ignore)
                {
                    throw new InvalidOperationException(
                        $"Analytics event {eventType.ToGenericTypeString()} contains indirectly an ignored type. " +
                        $"In {debugName}, the type {type.ToGenericTypeString()} is marked as [BigQueryAnalyticsFormat(Ignore)] but the field itself is not ignored. " +
                        "Ignore the field too with [BigQueryAnalyticsFormat(Ignore)].");
                }

                if (TryExtractScalar(eventType, fullName, debugName, type, getter, out spec))
                    return true;

                // Aggregates may recurse to themselves. Check type recursion limit
                if (context.GetNormalRecursionCount(type) > 0)
                {
                    throw new InvalidOperationException(
                            $"Analytics event {eventType.ToGenericTypeString()} has a member with a recursive type. In {debugName}, the type can be {type.ToGenericTypeString()} which can refer to itself. " +
                            "You may use BigQueryAnalyticsRecursionLimitAttribute to allow limited recursion, or you may use BigQueryAnalyticsFormatAttribute to disable or customize formatting of a field.");
                }

                context.PushNormalRecursionCount(type);
                bool aggregateExtracted = TryExtractAggregate(context, eventType, fullName, debugName, type, getter, out spec);
                context.PopNormalRecursionCount(type);

                if (aggregateExtracted)
                    return true;

                spec = default;
                return false;
            }

            static bool TryExtractScalar(Type eventType, string fullName, string debugName, Type type, Func<object, object> getter, out ParamSpec spec)
            {
                if (type == typeof(sbyte)
                    || type == typeof(short)
                    || type == typeof(int)
                    || type == typeof(long)
                    || type == typeof(byte)
                    || type == typeof(ushort)
                    || type == typeof(uint))
                {
                    // All integers are stored as int64. Support all that it can fit. Notably ulong is not
                    // supported.
                    spec = new ParamSpec(fullName, ParamSpec.Kind.Integer, getter);
                    return true;
                }
                else if (type == typeof(ulong))
                {
                    throw new InvalidOperationException(
                        $"Invalid analytics event field type in {eventType.ToGenericTypeString()} in {debugName}. Uint64 is not supported by BigQuery. " +
                        "You may use BigQueryAnalyticsFormatAttribute to disable formatting of a field.");
                }
                else if (type == typeof(float)
                    || type == typeof(double))
                {
                    // All floats are stored as doubles.
                    spec = new ParamSpec(fullName, ParamSpec.Kind.Float, getter);
                    return true;
                }
                else if (type == typeof(F32))
                {
                    // convert F32 to double
                    Func<object, object> convertedGetter =
                        (object obj) =>
                        {
                            object value = getter(obj);
                            return ((F32)value).Double;
                        };
                    spec = new ParamSpec(fullName, ParamSpec.Kind.Float, convertedGetter);
                    return true;
                }
                else if (type == typeof(F64))
                {
                    // convert F64 to double
                    Func<object, object> convertedGetter =
                        (object obj) =>
                        {
                            object value = getter(obj);
                            return ((F64)value).Double;
                        };
                    spec = new ParamSpec(fullName, ParamSpec.Kind.Float, convertedGetter);
                    return true;
                }
                else if (type == typeof(string))
                {
                    spec = new ParamSpec(fullName, ParamSpec.Kind.String, getter);
                    return true;
                }
                else if (type.IsEnum)
                {
                    // convert to string
                    Func<object, string> convertedGetter =
                        (object obj) =>
                        {
                            object value = getter(obj);
                            return Enum.GetName(type, value);
                        };

                    string exampleValue = TryGetExampleEnum(type);
                    spec = new ParamSpec(fullName, ParamSpec.Kind.String, convertedGetter, getExampleValue: () => exampleValue);
                    return true;
                }
                else if (type == typeof(bool))
                {
                    // convert to int
                    Func<object, object> convertedGetter =
                        (object obj) =>
                        {
                            object value = getter(obj);
                            return ((bool)value) ? 1 : 0;
                        };
                    spec = new ParamSpec(fullName, ParamSpec.Kind.Integer, convertedGetter, getExampleValue: () => 1);
                    return true;
                }
                else if (type == typeof(EntityId))
                {
                    // convert to string
                    Func<object, string> convertedGetter =
                        (object obj) =>
                        {
                            object value = getter(obj);
                            return ((EntityId)value).ToString();
                        };
                    spec = new ParamSpec(fullName, ParamSpec.Kind.String, convertedGetter, getExampleValue: () => EntityId.Create(EntityKindCore.Player, 1).ToString());
                    return true;
                }
                else if (type.ImplementsInterface<IStringId>())
                {
                    // convert to string
                    Func<object, string> convertedGetter =
                        (object obj) =>
                        {
                            object value = getter(obj);
                            return ((IStringId)value)?.Value;
                        };

                    // StringId example is computed at runtime since IDs get populated at a later phase
                    spec = new ParamSpec(fullName, ParamSpec.Kind.String, convertedGetter, getExampleValue: () => TryGetExampleStringId(type));
                    return true;
                }
                else if (type == typeof(byte[]))
                {
                    // encode as Base64
                    Func<object, string> convertedGetter =
                        (object obj) =>
                        {
                            byte[] value = (byte[])getter(obj);
                            if (value == null)
                                return null;
                            return Convert.ToBase64String(value);
                        };
                    spec = new ParamSpec(fullName, ParamSpec.Kind.String, convertedGetter, getExampleValue: () => "bWV0YQ==");
                    return true;
                }
                else if (type == typeof(SessionToken))
                {
                    // convert to string
                    Func<object, string> convertedGetter =
                        (object obj) =>
                        {
                            object value = getter(obj);
                            return $"{((SessionToken)value).Value:X16}";
                        };
                    spec = new ParamSpec(fullName, ParamSpec.Kind.String, convertedGetter, getExampleValue: () => $"{12345678u:X16}".ToString());
                    return true;
                }
                else if (type == typeof(MetaGuid))
                {
                    // convert to string
                    Func<object, string> convertedGetter =
                        (object obj) =>
                        {
                            object value = getter(obj);
                            return ((MetaGuid)value).ToString();
                        };
                    spec = new ParamSpec(fullName, ParamSpec.Kind.String, convertedGetter, getExampleValue: () => MetaGuid.None.ToString());
                    return true;
                }

                spec = default;
                return false;
            }

            static bool TryExtractAggregate(FlattenContext context, Type eventType, string fullName, string debugName, Type type, Func<object, object> getter, out ParamSpec spec)
            {
                if (type.IsCollection())
                {
                    // Flatten list by repeating. The elements are extracted at runtime, so the getter is an identity function.
                    // The elements are extracted with empty fullname. These are dynamically enumerated specs (due to indexing), and
                    // the dynamic prefix of the name will be inserted during traversal.
                    Type elementType = type.GetCollectionElementType();
                    Func<object, object> elementGetter = (obj) => obj;

                    if (TryExtract(context, eventType, fullName: "", debugName: debugName + ":[]", elementType, elementGetter, out ParamSpec elementSpec))
                    {
                        spec = new ParamSpec(fullName, ParamSpec.Kind.RuntimeEnumerated, getter, elementParamSpec: elementSpec);
                        return true;
                    }

                    throw new InvalidOperationException(
                        $"Invalid analytics event field type in {eventType.ToGenericTypeString()} in {debugName}, Type = {type.ToGenericTypeString()}. " +
                        "Could not format the Element type. You may use BigQueryAnalyticsFormatAttribute to disable or customize formatting of a field.");
                }

                // Handle nullable
                if (type.IsGenericTypeOf(typeof(Nullable<>)))
                {
                    // Nullable struct. Extract "Value" without altering the path. We do
                    // this by computing the extractor as if the value Nullable<T> was a
                    // plain T and then add Nullable<T> -> T extractor in between..

                    PropertyInfo            valueProp       = type.GetProperty("Value");

                    // Nullables get boxed as non-nullable type. Hence we can just
                    // take the value directly without having to access "Value"
                    // property. I.e. a separate getter is not needed to access .Value
                    //
                    // However, in the case the Nullable is null, we cannot pass the null
                    // value-type to the extractor as they do not expect null values.
                    // Wrap the getter with a null check.
                    //
                    // As we replace the getter method, we may use trivial getter for the
                    // inner one, and then pass the results in in the outer getter.
                    Func<object, object> trivialGetter = (object obj) => obj;
                    if (TryExtract(context, eventType, fullName, debugName: debugName + ":Value", valueProp.PropertyType, trivialGetter, out var underlyingSpec))
                    {
                        Func<object, object> innerGetter = underlyingSpec.GetValue;
                        Func<object, object> outerGetter =
                            (object obj) =>
                            {
                                object value = getter(obj);
                                if (value == null)
                                    return null;
                                return innerGetter(value);
                            };

                        spec = new ParamSpec(underlyingSpec.FullName, underlyingSpec.ValueKind, outerGetter, underlyingSpec.StructMembers, underlyingSpec.TypedParams, underlyingSpec.StringConstant, underlyingSpec.ElementParamSpec, underlyingSpec.GetExampleValue);
                        return true;
                    }

                    spec = default;
                    return false;
                }

                // Handle game configs (which are aggregate type, but get collapsed as ConfigKeys)
                if (type.ImplementsGenericInterface(typeof(IGameConfigData<>)))
                {
                    // Collapse as ConfigKey without changing path. Very similar to the Nullable<> path.
                    // Note that we get the ConfigKey from the interface and not the concrete object. The concrete
                    // object might have hidden the propery by explicitly implementing the interface.
                    Type                    configType      = type.GetGenericInterface(typeof(IHasGameConfigKey<>));
                    PropertyInfo            configKeyProp   = configType.GetProperty("ConfigKey");
                    Func<object, object>    configKeyGetter = configKeyProp.GetDataMemberGetValueOnDeclaringType();

                    // Convert from IGameConfigData<T> into T by getting the ConfigKey
                    Func<object, object> convertedGetter =
                        (object obj) =>
                        {
                            object configData = getter(obj);
                            if (configData == null)
                                return null;
                            return configKeyGetter(configData);
                        };

                    if (TryExtract(context, eventType, fullName, debugName: debugName + ":ConfigKey", configKeyProp.PropertyType, convertedGetter, out spec))
                        return true;

                    spec = default;
                    return false;
                }

                // KV-pairs are common non-serializable "hidden" type when dictionaries are formatted. Handle.
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                {
                    Type[] typeParams = type.GetGenericArguments();

                    // Require string key. Non-strings are most likely a mistake. This also makes use of BigQueryAnalyticsFormatMode.ExtractDictionaryElements trivial.
                    if (typeParams[0] != typeof(string))
                    {
                        throw new InvalidOperationException(
                            $"Invalid analytics event field type in {eventType.ToGenericTypeString()} in {debugName}, Type = {type.ToGenericTypeString()}. " +
                            $"KeyValuePairs (dictionaries) must have a string key, but found {typeParams[0].ToGenericTypeString()}. " +
                            $"You may use BigQueryAnalyticsFormatAttribute to disable or customize formatting of a field.");
                    }

                    Func<object, object> keyGetter = type.GetProperty("Key").GetGetValueOnDeclaringType();
                    Func<object, object> valueGetter = type.GetProperty("Value").GetGetValueOnDeclaringType();

                    ParamSpec[] kvStructParams = new ParamSpec[2];

                    if (!TryExtractScalar(eventType, fullName + ":key", debugName + ":key", typeParams[0], keyGetter, out kvStructParams[0])
                        || !TryExtractScalar(eventType, fullName + ":value", debugName + ":value", typeParams[1], valueGetter, out kvStructParams[1]))
                    {
                        spec = default;
                        return false;
                    }

                    spec = new ParamSpec(fullName, ParamSpec.Kind.Struct, getter, kvStructParams);
                    return true;
                }

                // "Normal" type:
                // We know the concrete instance in this param is of type T or it inherits T. If there is only one
                // concrete type that fits, just flatten normally. Otherwise we need to add a type switch and handle
                // each case separately.
                // Note that we again piggy-back on MetaSerialized registry.

                if (!MetaSerializerTypeRegistry.TryGetTypeSpec(type, out MetaSerializableType typeSpec))
                {
                    throw new InvalidOperationException(
                        $"Invalid analytics event field type in {eventType.ToGenericTypeString()} in {debugName}, Type = {type.ToGenericTypeString()}. " +
                        "Type not in registry. You may use BigQueryAnalyticsFormatAttribute to disable or customize formatting of a field.");
                }

                List<Type> concreteTypes = new List<Type>();
                if (typeSpec.IsConcreteObject)
                    concreteTypes.Add(typeSpec.Type);
                if (typeSpec.DerivedTypes != null)
                {
                    foreach (Type derived in typeSpec.DerivedTypes.Values)
                    {
                        if (MetaSerializerTypeRegistry.GetTypeSpec(derived).IsConcreteObject)
                            concreteTypes.Add(derived);
                    }
                }

                if (concreteTypes.Count == 0)
                {
                    // Abstract class and there is no implementing type.
                    // This is likely an unused extension point, which are legal. Extract as an
                    // empty struct. The contents do not matter as this value is always null (since
                    // there is no constructible type deriving this).
                    Func<object, object> nullGetter = (object _) => null;
                    spec = new ParamSpec(fullName, ParamSpec.Kind.Struct, nullGetter, Array.Empty<ParamSpec>());
                    return true;
                }
                else if (concreteTypes.Count == 1)
                {
                    // Normal struct. Extract fields. This might not be the type we started with, for example
                    // if the root type is abstract and there is only one concrete deriving type.
                    // Note that even though the types might be different, we don't need to convert to the potential
                    // subclass since boxing hides the types anyway.

                    Type            concreteType    = concreteTypes[0];
                    bool            isRuntimeTyped  = concreteTypes[0] != type;
                    List<ParamSpec> structMembers   = new List<ParamSpec>();

                    // If type resolution would exceed the recursion limit, emit terminal literal.
                    // \note: The GetCustomAttribute gets the attribute from base classes too.
                    BigQueryAnalyticsRecursionLimitAttribute recursionLimitAttributeMaybe = concreteType.GetCustomAttribute<BigQueryAnalyticsRecursionLimitAttribute>();
                    if (recursionLimitAttributeMaybe != null && context.GetLimitedRecursionCount(concreteType) >= recursionLimitAttributeMaybe.RecursionLimit)
                    {
                        structMembers.Add(new ParamSpec(fullName, ParamSpec.Kind.StringConstant, null, stringConstant: "__recursion_limit"));
                    }
                    else
                    {
                        if (recursionLimitAttributeMaybe != null)
                        {
                            // See comment in TryExtract
                            context.StashNormalRecursionCounts();
                            context.PushLimitedRecursionType(concreteType);
                        }

                        AddParamFields(context, eventType, path: fullName + ":", debugPath: debugName + ":" + (isRuntimeTyped ? $"[{concreteType.GetNestedClassName()}]:" : null), concreteType, structMembers);

                        if (recursionLimitAttributeMaybe != null)
                        {
                            // See comment in TryExtract
                            context.UnstashNormalRecursionCounts();
                            context.PopLimitedRecursionType(concreteType);
                        }

                        if (structMembers.Count == 0)
                        {
                            // Could not find any fields in the data. The data is probably some
                            // aggregate type that should have a custom scalar-formatter or the
                            // aggregate type is missing formattable fields.
                            throw new InvalidOperationException(
                                $"Invalid analytics event field type in {eventType.ToGenericTypeString()} in {debugName}, Type = {concreteType.ToGenericTypeString()}. " +
                                "Type has no fields and it cannot be written. You may use BigQueryAnalyticsFormatAttribute to disable or customize formatting of a field.");
                        }

                        // If this is a derived type, add a synthetic type field as in Count > 1 case below.
                        // This makes the data format forwards compatible.
                        if (isRuntimeTyped)
                            structMembers.Insert(0, new ParamSpec(fullName + ":$t", ParamSpec.Kind.StringConstant, null, stringConstant: concreteType.GetNestedClassName()));
                    }

                    // Runtime typed cannot be covnerted to struct. Even if there was only one concrete type it can be, a runtime-typed value may still be null,
                    // in which case the type tag is not emitted.

                    if (isRuntimeTyped)
                    {
                        // RuntimeTyped gets the type reference, the concrete type then just passes the reference in.
                        Func<object, object> typedGetter = (obj) => obj;
                        ParamSpec concreteSpec = new ParamSpec(fullName, ParamSpec.Kind.Struct, typedGetter, structMembers.ToArray());

                        spec = new ParamSpec(fullName, ParamSpec.Kind.RuntimeTyped, getter, null, new MetaDictionary<Type, ParamSpec>() { { concreteType, concreteSpec } });
                    }
                    else
                    {
                        spec = new ParamSpec(fullName, ParamSpec.Kind.Struct, getter, structMembers.ToArray());
                    }
                    return true;
                }
                else
                {
                    // Dynamically typed field. Type switch.
                    MetaDictionary<Type, ParamSpec> typedParamSpecs = new MetaDictionary<Type, ParamSpec>(concreteTypes.Count);
                    foreach (Type concreteType in concreteTypes)
                    {
                        List<ParamSpec> structMembers = new List<ParamSpec>();

                        // If type resolution would exceed the recursion limit, emit terminal literal.
                        // \note: The GetCustomAttribute gets the attribute from base classes too.
                        BigQueryAnalyticsRecursionLimitAttribute recursionLimitAttributeMaybe = concreteType.GetCustomAttribute<BigQueryAnalyticsRecursionLimitAttribute>();
                        if (recursionLimitAttributeMaybe != null && context.GetLimitedRecursionCount(concreteType) >= recursionLimitAttributeMaybe.RecursionLimit)
                        {
                            structMembers.Add(new ParamSpec(fullName, ParamSpec.Kind.StringConstant, null, stringConstant: "__recursion_limit"));
                        }
                        else
                        {
                            if (recursionLimitAttributeMaybe != null)
                            {
                                // See comment in TryExtract
                                context.StashNormalRecursionCounts();
                                context.PushLimitedRecursionType(concreteType);
                            }

                            AddParamFields(context, eventType, path: fullName + ":", debugPath: debugName + ":" + concreteType.GetNestedClassName() + ":", concreteType, structMembers);

                            if (recursionLimitAttributeMaybe != null)
                            {
                                // See comment in TryExtract
                                context.UnstashNormalRecursionCounts();
                                context.PopLimitedRecursionType(concreteType);
                            }

                            if (structMembers.Count == 0)
                            {
                                // Could not find any fields in the data. As with normal struct case, the data is probably some
                                // aggregate that is missing the custom scalar formatter.
                                throw new InvalidOperationException(
                                    $"Invalid analytics event field type in {eventType.ToGenericTypeString()} in {debugName}, Type = {concreteType.ToGenericTypeString()}. " +
                                    "Type has no fields and it cannot be written. You may use BigQueryAnalyticsFormatAttribute to disable or customize formatting of a field.");
                            }

                            // Add a synthetic type field to allow detecting the type from result without having to resort to heuristics.
                            structMembers.Insert(0, new ParamSpec(fullName + ":$t", ParamSpec.Kind.StringConstant, null, stringConstant: concreteType.GetNestedClassName()));
                        }

                        // The object value is already getted at parent RuntimeTyped node. This is a sub-referencetype so no conversion is needed.
                        Func<object, object> typedGetter = (obj) => obj;

                        ParamSpec typedSpec = new ParamSpec(fullName, ParamSpec.Kind.Struct, typedGetter, structMembers.ToArray());
                        typedParamSpecs.Add(concreteType, typedSpec);
                    }

                    spec = new ParamSpec(fullName, ParamSpec.Kind.RuntimeTyped, getter, null, typedParamSpecs);
                    return true;
                }
            }

            static bool ExtractRuntimeExtracted(FlattenContext context, Type eventType, string fullName, string debugName, Type type, Func<object, object> getter, out ParamSpec spec)
            {
                if (!type.IsEnumerable())
                {
                    throw new InvalidOperationException(
                            $"Analytics event {eventType.ToGenericTypeString()} has a non-dictionary-like member with [BigQueryAnalyticsFormat(ExtractDictionaryElements)] in {debugName}. " +
                            $"Type {type.ToGenericTypeString()} must be enumerable.");
                }

                Type elementType = type.GetEnumerableElementType();
                MemberInfo elementKeyMember = (MemberInfo)elementType.GetField("Key", BindingFlags.Public | BindingFlags.Instance) ?? elementType.GetProperty("Key", BindingFlags.Public | BindingFlags.Instance);
                MemberInfo elementValueMember = (MemberInfo)elementType.GetField("Value", BindingFlags.Public | BindingFlags.Instance) ?? elementType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);

                if (elementKeyMember == null || elementKeyMember.GetDataMemberType() != typeof(string) || elementValueMember == null)
                {
                    throw new InvalidOperationException(
                            $"Analytics event {eventType.ToGenericTypeString()} has a non-dictionary-like member with [BigQueryAnalyticsFormat(ExtractDictionaryElements)] in {debugName}. " +
                            $"Enumerable element type {elementType.ToGenericTypeString()} must have accessible Key and Value datamembers, and the Key type must be string.");
                }

                // Extract dictionary. The elements are enumerated at runtime, so the getter is Value field getter.
                // The elements are extracted with empty fullname since the name is chosen at runtime with elementNameExtractor.

                Func<object, object> elementGetter = elementValueMember.GetDataMemberGetValueOnDeclaringType();
                Func<object, object> untypedElementNameExtractor = elementKeyMember.GetDataMemberGetValueOnDeclaringType();
                Func<object, string> elementNameExtractor = (obj) => (string)untypedElementNameExtractor(obj);

                if (TryExtract(context, eventType, fullName: "", debugName: debugName + ":[]", elementValueMember.GetDataMemberType(), elementGetter, out ParamSpec elementSpec))
                {
                    spec = new ParamSpec(fullName, ParamSpec.Kind.RuntimeExtracted, getter, elementParamSpec: elementSpec, elementNameExtractor: elementNameExtractor);
                    return true;
                }

                throw new InvalidOperationException(
                    $"Invalid analytics event field type in {eventType.ToGenericTypeString()} in {debugName}, Type = {type.ToGenericTypeString()}. " +
                    $"Could not create formatter for the Element type {elementValueMember.GetDataMemberType().ToGenericTypeString()}.");
            }

            static string TryGetExampleEnum(Type type)
            {
                Array values = Enum.GetValues(type);
                if (values.Length == 0)
                    return null;
                IEnumerator e = values.GetEnumerator();
                if (!e.MoveNext())
                    return null;
                return e.Current?.ToString();
            }

            static string TryGetExampleStringId(Type type)
            {
                FieldInfo field = type.BaseType?.GetField("s_interned", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (field == null)
                    return null;
                IDictionary dict = field.GetValue(null) as IDictionary;
                if (dict == null)
                    return null;
                IEnumerator e = dict.Values.GetEnumerator();
                if (!e.MoveNext())
                    return null;
                return e.Current?.ToString();
            }
        }

        readonly MetaDictionary<Type, EventFormatter> _eventFormatters;
        readonly MetaDictionary<Type, EventFormatter> _extraFieldContextFormatters;

        /// <summary>
        /// Formats a timestamp into TIMESTAMP REST api format (not the canonical format).
        /// </summary>
        static string FormatTimestampString(MetaTime timestamp)
        {
            // YYYY-MM-DD HH:MM[:SS[.SSSSSS]]
            DateTime dt = timestamp.ToDateTime();
            int microseconds = dt.Millisecond * 1000 + dt.Microsecond;
            return string.Format(CultureInfo.InvariantCulture, "{0:D4}-{1:D2}-{2:D2} {3:D2}:{4:D2}:{5:D2}.{6:D6}", dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, microseconds);
        }

        /// <summary>
        /// Formats an event into the BigQuery analytics table format, and returns the resulting rows.
        /// This should be inserted into event_params column.
        /// </summary>
        List<BigQueryInsertRow> FormatEventArgs(EventFormatter formatter, AnalyticsEventBase eventBase)
        {
            return formatter.Format(eventBase);
        }

        /// <summary>
        /// Formats a context into the BigQuery analytics table format and retuns the (columnName, row)
        /// pair. If there is no context, (null, null) is returned.
        /// </summary>
        (string, BigQueryInsertRow) TryFormatContext(AnalyticsContextBase contextBase, List<BigQueryInsertRow> appendExtraParamsTo)
        {
            EventFormatter extraFieldContextFormatter = contextBase != null ? _extraFieldContextFormatters.GetValueOrDefault(contextBase.GetType()) : null;

            // Populate context from envelope.
            if (contextBase is PlayerAnalyticsContext playerContext)
            {
                BigQueryInsertRow context = new BigQueryInsertRow();
                List<BigQueryInsertRow> experiments = new List<BigQueryInsertRow>();
                foreach ((string experimentId, string variantId) in playerContext.Experiments)
                {
                    experiments.Add(new BigQueryInsertRow()
                    {
                        {"experiment", experimentId },
                        {"variant", variantId },
                    });
                }
                context.Add("session_number", playerContext.SessionNumber);
                context.Add("experiments", experiments);

                if (extraFieldContextFormatter != null)
                    appendExtraParamsTo.AddRange(extraFieldContextFormatter.Format(contextBase));

                return ("player", context);
            }
            else if (contextBase is GuildAnalyticsContext guildContext)
            {
                // Guild context has not yet any fields and empty records are not supported.
                //BigQueryInsertRow context = new BigQueryInsertRow();
                //return ("guild", context);
            }
            else if (contextBase is ServerAnalyticsContext serverContext)
            {
                // Server context has not yet any fields and empty records are not supported.
            }

            return (null, null);
        }

        List<BigQueryInsertRow> FormatEventLabels(MetaDictionary<AnalyticsLabel, string> labels)
        {
            if (labels == null || labels.Count == 0)
                return new List<BigQueryInsertRow>();

            List<BigQueryInsertRow> rows = new List<BigQueryInsertRow>(capacity: labels.Count);
            foreach ((AnalyticsLabel label, string value) in labels)
            {
                BigQueryInsertRow record = new BigQueryInsertRow();
                record.Add("name", label.ToString());
                record.Add("string_value", value);
                rows.Add(record);
            }
            return rows;
        }

        /// <summary>
        /// Formats and writer the analytics event into the write buffer. If <paramref name="enableBigQueryEventDeduplication"/> is set, a deduplication key
        /// is defined for each written event. The BigQuery backend will then pool any deduplicable events over a short window and remove duplicates, which may
        /// occur due to retrying inserts when the attempt had ended up with an undeterminate result (either success or fail, but cannot know which one).
        /// </summary>
        public void WriteEvent(IBigQueryBatchWriter.BatchBuffer buffer, AnalyticsEventEnvelope eventEnvelope, bool enableBigQueryEventDeduplication)
        {
            string id = string.Format(CultureInfo.InvariantCulture, "{0:X16}{1:X16}", eventEnvelope.UniqueId.High, eventEnvelope.UniqueId.Low);

            AnalyticsEventBase eventBase = eventEnvelope.Payload;

            if (!_eventFormatters.TryGetValue(eventBase.GetType(), out EventFormatter eventFormatter))
                throw new ArgumentException($"Type {eventBase.GetType().ToGenericTypeString()} is not a known AnalyticsEvent", nameof(eventEnvelope));

            MetaDictionary<string, object> fields = new MetaDictionary<string, object>();
            fields.Add("source_id", eventEnvelope.Source.ToString());
            fields.Add("event_id", id);
            fields.Add("event_timestamp", FormatTimestampString(eventEnvelope.ModelTime));
            fields.Add("event_name", eventFormatter.EventName);
            fields.Add("event_schema_version", eventEnvelope.SchemaVersion);
            List<BigQueryInsertRow> eventParams = FormatEventArgs(eventFormatter, eventBase);

            (string contextName, BigQueryInsertRow contextRow) = TryFormatContext(eventEnvelope.Context, appendExtraParamsTo: eventParams);
            if (contextName != null)
                fields.Add(contextName, contextRow);

            fields.Add("event_params", eventParams);
            fields.Add("labels", FormatEventLabels(eventEnvelope.Labels));

            BigQueryInsertRow row = new BigQueryInsertRow();
            if (enableBigQueryEventDeduplication)
                row.InsertId = id;
            row.Add(fields);
            buffer.Add(row);
        }

        /// <summary>
        /// Creates an example object that could be a result of formatting the type.
        /// </summary>
        public object GetExampleResultObject(AnalyticsEventSpec spec)
        {
            static object SingleParamContainer(string keyName, string valueName, object value)
            {
                IDictionary<string, object> dst = new ExpandoObject();
                dst.Add("key", keyName);
                if (value != null)
                    dst.Add(valueName, value);
                return dst;
            }
            static void ExtractExampleEventParamData(List<object> dst, string namePrefix, EventFormatter.ParamSpec spec)
            {
                switch (spec.ValueKind)
                {
                    case EventFormatter.ParamSpec.Kind.Integer:
                        dst.Add(SingleParamContainer(namePrefix + spec.FullName, "int_value", spec.GetExampleValue?.Invoke() ?? 123));
                        return;

                    case EventFormatter.ParamSpec.Kind.Float:
                        dst.Add(SingleParamContainer(namePrefix + spec.FullName, "double_value", spec.GetExampleValue?.Invoke() ?? 45.5));
                        return;

                    case EventFormatter.ParamSpec.Kind.String:
                        dst.Add(SingleParamContainer(namePrefix + spec.FullName, "string_value", spec.GetExampleValue?.Invoke() ?? "ABC"));
                        return;

                    case EventFormatter.ParamSpec.Kind.StringConstant:
                        dst.Add(SingleParamContainer(namePrefix + spec.FullName, "string_value", spec.StringConstant));
                        return;

                    case EventFormatter.ParamSpec.Kind.Struct:
                        foreach (EventFormatter.ParamSpec subSpec in spec.StructMembers)
                            ExtractExampleEventParamData(dst, namePrefix, subSpec);
                        return;

                    case EventFormatter.ParamSpec.Kind.RuntimeEnumerated:
                        // This is an array so expand it to 2 elements.
                        // \todo: Add mechanism for caller to select desired amount?
                        ExtractExampleEventParamData(dst, namePrefix + spec.FullName + ":0", spec.ElementParamSpec);
                        ExtractExampleEventParamData(dst, namePrefix + spec.FullName + ":1", spec.ElementParamSpec);
                        return;

                    case EventFormatter.ParamSpec.Kind.RuntimeExtracted:
                        // This is an array so expand it to 2 fields.
                        // \todo: Add mechanism for caller to select desired amount?
                        ExtractExampleEventParamData(dst, namePrefix + spec.FullName + ":foo", spec.ElementParamSpec);
                        ExtractExampleEventParamData(dst, namePrefix + spec.FullName + ":bar", spec.ElementParamSpec);
                        return;

                    case EventFormatter.ParamSpec.Kind.RuntimeTyped:
                        // Expand all types, i.e. if field "A:Reward" can be either SuperReward or HyperReward,
                        // we expand the argument of both of them. The results should remain readable as each set
                        // of field is prefixed with the $t type discriminator.
                        // \todo: Add mechanism for caller to select certain types
                        foreach (EventFormatter.ParamSpec subSpec in spec.TypedParams.Values)
                            ExtractExampleEventParamData(dst, namePrefix, subSpec);
                        return;

                    case EventFormatter.ParamSpec.Kind.RecursionLimit:
                        dst.Add(SingleParamContainer(namePrefix + spec.FullName, "string_value", "__recursion_limit"));
                        return;
                }

                throw new InvalidOperationException("invalid kind");
            }
            static object[] GetExampleEventParams(EventFormatter formatter)
            {
                List<object> dst = new List<object>();
                foreach (EventFormatter.ParamSpec spec in formatter.TopLevelParameterSpecs)
                    ExtractExampleEventParamData(dst, null, spec);
                return dst.ToArray();
            }
            static object[] GetExampleLabels()
            {
                List<object> dst = new List<object>();
                foreach (AnalyticsLabel label in AnalyticsLabel.AllValues)
                {
                    dynamic record = new ExpandoObject();
                    record.name = label.ToString();
                    record.string_value = "labelValue";
                    dst.Add(record);
                }
                return dst.ToArray();
            }

            if (!_eventFormatters.TryGetValue(spec.Type, out EventFormatter formatter))
                throw new ArgumentException($"Type {spec.Type.ToGenericTypeString()} is not a known AnalyticsEvent", nameof(spec));

            dynamic obj = new ExpandoObject();
            obj.source_id = $"{spec.CategoryName}:123";
            obj.event_id = string.Format(CultureInfo.InvariantCulture, "{0:X16}{1:X16}", 0x11223344AABBCCDD, 0x66778899EEFF0011);
            obj.event_timestamp = FormatTimestampString(MetaTime.FromDateTime(new DateTime(2020, 10, 10, 14, 40, 12, DateTimeKind.Utc)));
            obj.event_name = formatter.EventName;
            obj.event_schema_version = spec.SchemaVersion;
            obj.event_params = GetExampleEventParams(formatter);
            obj.labels = GetExampleLabels();

            if (spec.CategoryName == "Player")
            {
                obj.player = new ExpandoObject();
                obj.player.session_number = 4;

                dynamic experimentExample = new ExpandoObject();
                experimentExample.experiment = "experimentId";
                experimentExample.variant = "variantId";
                obj.player.experiments = new List<ExpandoObject> { experimentExample }.ToArray();
            }

            return obj;
        }
    }
}
