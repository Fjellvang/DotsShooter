// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Config;
using Metaplay.Core.Math;
using Metaplay.Core.Model;
using Metaplay.Core.Schedule;
using Metaplay.Core.Serialization;
using Metaplay.Core.Session;
using Metaplay.Core.Web3;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using static System.FormattableString;

#if NETCOREAPP
using Akka.Actor;
#endif

namespace Metaplay.Core.Json
{
    // NOTE: Unity-compatible Json.NET libraries only support non-generic JsonConverter
    // \todo [nuutti 2021-06-30] Investigate what the above comment means exactly

    public static class JsonSerializationErrorUtility
    {
        public static void CopyErrorHandlerFromSerializerIfNecessary(JsonSerializer serializer, JsonSerializerSettings settings)
        {
            if (serializer.Context.Context is JsonSerializationErrorLogger)
            {
                settings.Context = serializer.Context;
                settings.Error   = HandleAdminApiJsonError;
            }
        }

        /// <summary>
        /// Error handler that allows exceptions to continue and logs the error to the <see cref="JsonSerializationErrorLogger"/> in the context.
        /// </summary>
        public static void HandleAdminApiJsonError(object sender, ErrorEventArgs args)
        {
            if (sender is JsonSerializer ser)
            {
                JsonSerializationErrorLogger jsonErrorLogger = ser.Context.Context as JsonSerializationErrorLogger;
                MetaDebug.Assert(jsonErrorLogger != null, $"Json Context is not of type {nameof(JsonSerializationErrorLogger)}.");
                jsonErrorLogger?.RecordError(args.ErrorContext);
                args.ErrorContext.Handled = true;
            }
        }

        public static JsonSerializerSettings CopySettings(this JsonSerializerSettings settings)
        {
            #if !UNITY_2022_1_OR_NEWER
            var copiedSerializer = new JsonSerializerSettings
            {
                Context                        = settings.Context,
                Culture                        = settings.Culture,
                ContractResolver               = settings.ContractResolver,
                ConstructorHandling            = settings.ConstructorHandling,
                CheckAdditionalContent         = settings.CheckAdditionalContent,
                DateFormatHandling             = settings.DateFormatHandling,
                DateFormatString               = settings.DateFormatString,
                DateParseHandling              = settings.DateParseHandling,
                DateTimeZoneHandling           = settings.DateTimeZoneHandling,
                DefaultValueHandling           = settings.DefaultValueHandling,
                EqualityComparer               = settings.EqualityComparer,
                FloatFormatHandling            = settings.FloatFormatHandling,
                Formatting                     = settings.Formatting,
                FloatParseHandling             = settings.FloatParseHandling,
                MaxDepth                       = settings.MaxDepth,
                MetadataPropertyHandling       = settings.MetadataPropertyHandling,
                MissingMemberHandling          = settings.MissingMemberHandling,
                NullValueHandling              = settings.NullValueHandling,
                ObjectCreationHandling         = settings.ObjectCreationHandling,
                PreserveReferencesHandling     = settings.PreserveReferencesHandling,
                ReferenceResolverProvider      = settings.ReferenceResolverProvider,
                ReferenceLoopHandling          = settings.ReferenceLoopHandling,
                StringEscapeHandling           = settings.StringEscapeHandling,
                TraceWriter                    = settings.TraceWriter,
                TypeNameHandling               = settings.TypeNameHandling,
                SerializationBinder            = settings.SerializationBinder,
                TypeNameAssemblyFormatHandling = settings.TypeNameAssemblyFormatHandling,
                Error                          = settings.Error,
            };
            foreach (var converter in settings.Converters)
            {
                copiedSerializer.Converters.Add(converter);
            }
            return copiedSerializer;

            #else
            return new JsonSerializerSettings(settings);
            #endif
        }
    }

    public static class JsonReaderExtensions
    {
        public static void CheckPropertyName(this JsonReader reader, string expected)
        {
            string propertyName = reader.GetPropertyName();
            if (propertyName != expected)
                throw new InvalidOperationException($"Expected json property name {expected}, got {propertyName}");
        }

        public static string GetPropertyName(this JsonReader reader)
        {
            reader.CheckToken(JsonToken.PropertyName);
            return (string)reader.Value;
        }

        public static void CheckToken(this JsonReader reader, JsonToken expected)
        {
            if (reader.TokenType != expected)
                throw new InvalidOperationException($"Expected json token type {expected}, got {reader.TokenType}");
        }

        public static void MustRead(this JsonReader reader)
        {
            bool ok = reader.Read();
            if (!ok)
                throw new InvalidOperationException($"Expected {nameof(JsonReader)}.{nameof(JsonReader.Read)} to return true, returned false");
        }
    }

    /// <summary>
    /// Serialize GameConfig info classes (which implement <see cref="IGameConfigData{TKey}"/>) with their Id only.
    /// </summary>
    public class GameConfigDataConverter<TKey, TInfo> : JsonConverter where TInfo : IGameConfigData<TKey>
    {
        IGameConfigDataResolver _resolver;

        public GameConfigDataConverter(IGameConfigDataResolver resolver)
        {
            _resolver = resolver;
        }

        public override bool CanConvert(Type type) => type == typeof(TInfo);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            TKey key = serializer.Deserialize<TKey>(reader);

            if (key != null)
            {
                if (_resolver == null)
                    throw new InvalidOperationException($"Trying to json-deserialize a config data reference to '{key}' (type {typeof(TInfo).ToGenericTypeString()}), but reference resolver is null");

                return _resolver.ResolveReference(typeof(TInfo), key);
            }
            else
                return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            TKey key = ((TInfo)value).ConfigKey;
            serializer.Serialize(writer, key);
        }
    }

    /// <summary>
    /// Serialize MetaRef as just its Key.
    /// </summary>
    public class MetaRefConverter<TItem> : JsonConverter
        where TItem : class, IGameConfigData
    {
        IGameConfigDataResolver _resolver;

        public MetaRefConverter(IGameConfigDataResolver resolver)
        {
            _resolver = resolver;
        }

        public override bool CanConvert(Type type) => type == typeof(MetaRef<TItem>);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            Type keyType = typeof(TItem).GetGenericInterfaceTypeArguments(typeof(IGameConfigData<>))[0];
            object key = serializer.Deserialize(reader, keyType);

            if (key != null)
            {
                MetaRef<TItem> metaRef = MetaRef<TItem>.FromKey(key);
                if (_resolver != null)
                    metaRef.ResolveInPlace(_resolver);
                return metaRef;
            }
            else
                return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            MetaRef<TItem> metaRef = (MetaRef<TItem>)value;
            serializer.Serialize(writer, metaRef.KeyObject);
        }
    }

    public class MetaConfigIdConverter<TItem> : JsonConverter
        where TItem : class, IGameConfigData
    {
        public MetaConfigIdConverter() { }

        public override bool CanConvert(Type type) => type == typeof(MetaConfigId<TItem>);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            Type keyType = typeof(TItem).GetGenericInterfaceTypeArguments(typeof(IGameConfigData<>))[0];
            object key = serializer.Deserialize(reader, keyType);
            MetaConfigId<TItem> metaConfigId = new MetaConfigId<TItem>(key);
            return metaConfigId;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            MetaConfigId<TItem> metaConfigId = (MetaConfigId<TItem>)value;
            serializer.Serialize(writer, metaConfigId.KeyObject);
        }
    }

    public class DefaultObjectConverter : JsonConverter
    {
        public static DefaultObjectConverter Instance = new DefaultObjectConverter();

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanConvert(Type objectType)
        {
            throw new NotImplementedException();
        }

        public override bool CanWrite => false;

        public override bool CanRead => false;
    }

    public class SensitiveValueConverter : JsonConverter
    {
        public static readonly SensitiveValueConverter Instance = new SensitiveValueConverter();

        public override bool CanConvert(Type type) => true;

        public override bool CanRead => false;
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Reading sensitive field from json is not supported");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value == null ? "null" : "XXX");
        }
    }

    public class UInt128Converter : JsonConverter //<MetaUInt128>
    {
        public override bool CanConvert(Type type) => type == typeof(MetaUInt128);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            MetaUInt128 v = (MetaUInt128)value;
            serializer.Serialize(writer, Invariant($"{v.High:X16}-{v.Low:X16}"));
        }
    }

    public class F32Converter : JsonConverter //<F32>
    {
        public override bool CanConvert(Type type) => type == typeof(F32);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            object value = reader.Value;
            decimal d = value is decimal decimalValue ? decimalValue
                      : value is long longValue       ? longValue
                      : throw new InvalidOperationException($"Expected JSON number to be either decimal or long, was {value?.GetType()} ({value})");
            // \todo [petri] support direct conversion from decimal to F32
            return F32.Parse(d.ToString(CultureInfo.InvariantCulture));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteRawValue(((F32)value).ToString());
        }
    }

    public class F64Converter : JsonConverter //<F64>
    {
        public override bool CanConvert(Type type) => type == typeof(F64);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            object value = reader.Value;
            decimal d = value is decimal decimalValue ? decimalValue
                      : value is long longValue       ? longValue
                      : throw new InvalidOperationException($"Expected JSON number to be either decimal or long, was {value?.GetType()} ({value})");
            // \todo [petri] support direct conversion from decimal to F64
            return F64.Parse(d.ToString(CultureInfo.InvariantCulture));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteRawValue(((F64)value).ToString());
        }
    }

    public class EntityIdConverter : JsonConverter //<EntityId>
    {
        public override bool CanConvert(Type type) => type == typeof(EntityId);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string str = (string)reader.Value;
            return EntityId.ParseFromString(str);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, ((EntityId)value).ToString());
        }
    }

    /// <summary>
    /// Convert <see cref="EntityKindMask"/> to array of strings.
    /// </summary>
    public class EntityKindMaskConverter : JsonConverter //<EntityKindMask>
    {
        public override bool CanConvert(Type type) => type == typeof(EntityKindMask);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Parsing EntityKindMask from JSON not supported");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartArray();

            EntityKindMask mask = (EntityKindMask)value;
            foreach (EntityKind entityKind in mask.GetKinds())
                writer.WriteValue(entityKind.ToString());

            writer.WriteEndArray();
        }
    }

    public class MetaTimeConverter : JsonConverter //<MetaTime>
    {
        public override bool CanConvert(Type type) => type == typeof(MetaTime);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string str = (string)reader.Value;
            return MetaTime.Parse(str);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, ((MetaTime)value).ToISO8601());
        }
    }

    public class MetaDurationConverter : JsonConverter //<MetaDuration>
    {
        public override bool CanConvert(Type type) => type == typeof(MetaDuration);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // \note JSON.stringify() of moment.js duration outputs an ISO duration (eg, "P30DT1H"), so might want to support that format as well
            // \note Also, might want to loosen up the requirement for precisely 7 decimal digits in fractions of seconds
            string str = (string)reader.Value;
            return MetaDuration.ParseExactFromJson(str);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, ((MetaDuration)value).ToJsonString());
        }
    }

    public class MetaGuidConverter : JsonConverter //<MetaGuid>
    {
        public override bool CanConvert(Type type) => type == typeof(MetaGuid);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return MetaGuid.Parse((string)reader.Value);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, ((MetaGuid)value).ToString());
        }
    }

    public class MetaCalendarDateTimeConverter : JsonConverter //<MetaCalendarDateTime>
    {
        public override bool CanConvert(Type type) => type == typeof(MetaCalendarDateTime);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string str = (string)reader.Value;
            DateTime dt = DateTime.Parse(str, null, DateTimeStyles.RoundtripKind);
            return MetaCalendarDateTime.FromDateTime(dt);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            DateTime dt = ((MetaCalendarDateTime)value).ToDateTime();
            string iso8601 = dt.ToString("s", CultureInfo.InvariantCulture);
            serializer.Serialize(writer, iso8601);
        }
    }

    public class MetaCalendarPeriodConverter : JsonConverter //<MetaCalendarPeriod>
    {
        public override bool CanConvert(Type type) => type == typeof(MetaCalendarPeriod);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string str = (string)reader.Value;
            MetaCalendarPeriod period = MetaCalendarPeriod.ParseISO8601(str);
            return period;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            MetaCalendarPeriod p = ((MetaCalendarPeriod)value);
            string iso8601 = p.ToISO8601String();
            serializer.Serialize(writer, iso8601);
        }
    }

    public class EnumStringConverter<TEnum> : JsonConverter where TEnum : Enum
    {
        public override bool CanConvert(Type type) => type.IsEnum;

        public override object ReadJson(JsonReader reader, Type type, object existingValue, JsonSerializer serializer)
        {
            return (TEnum)Enum.Parse(type, (string)reader.Value);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, ((TEnum)value).ToString());
        }
    }

    public class StringIdConverter : JsonConverter
    {
        public override bool CanConvert(Type type) => type.ImplementsInterface<IStringId>();

        public override object ReadJson(JsonReader reader, Type type, object existingValue, JsonSerializer serializer)
        {
            return StringIdUtil.CreateDynamic(type, (string)reader.Value);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, ((IStringId)value).ToString());
        }
    }

    public class GameConfigConverter : JsonConverter
    {
        JsonSerialization.Options _options;

        public GameConfigConverter(JsonSerialization.Options options)
        {
            _options = options;
        }

        public override bool CanConvert(Type type) => type.IsDerivedFrom<GameConfigBase>();

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, GetGameConfigEntries(value, serializer));
        }

        MetaDictionary<string, IGameConfigEntry> GetGameConfigEntries(object gameConfig, JsonSerializer serializer)
        {
            // This should always be DefaultContractResolver but just in case it isn't...
            NamingStrategy camelCasing = (serializer.ContractResolver as DefaultContractResolver)?.NamingStrategy ?? new CamelCaseNamingStrategy();

            GameConfigTypeInfo gameConfigTypeInfo = GameConfigRepository.Instance.GetGameConfigTypeInfo(gameConfig.GetType());

            return gameConfigTypeInfo.Entries.Values
                .Where(entryInfo =>
                {
                    // Handle only-in-serialization-mode attribute
                    IncludeOnlyInJsonSerializationModeAttribute onlyInModeAttrib = entryInfo.MemberInfo.GetCustomAttribute<IncludeOnlyInJsonSerializationModeAttribute>(inherit: true);
                    if (onlyInModeAttrib != null && onlyInModeAttrib.Mode != _options.Mode)
                        return false;
                    return true;
                })
                .ToMetaDictionary(
                    entryInfo => camelCasing.GetPropertyName(entryInfo.MemberInfo.Name, hasSpecifiedName: false), // \todo [nuutti] Use member name, or the name specified in the GameConfigEntryAttribute? #config-entry-name
                    entryInfo =>
                    {
                        object entryValue = entryInfo.MemberInfo.GetDataMemberGetValueOnDeclaringType()(gameConfig);
                        return (IGameConfigEntry)entryValue;
                    });
        }
    }

    public class GameConfigKeyValueConverter : JsonConverter
    {
        public override bool CanConvert(Type type) => type.IsDerivedFrom<GameConfigKeyValue>();

        public override object ReadJson(JsonReader reader, Type type, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object configKeyValue, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            foreach (MemberInfo dataMemberInfo in configKeyValue.GetType().EnumerateInstanceDataMembersInUnspecifiedOrder())
            {
                object memberValue = dataMemberInfo.GetDataMemberGetValueOnDeclaringType()(configKeyValue);
                writer.WritePropertyName(dataMemberInfo.Name);
                serializer.Serialize(writer, memberValue);
            }
            writer.WriteEndObject();
        }
    }

    public class GameConfigPatchConverter : JsonConverter<FullGameConfigPatch>
    {
        GameConfigPatch ReadPatch(JsonReader reader, JsonSerializer serializer, Type configType)
        {
            GameConfigTypeInfo gameConfigTypeInfo = GameConfigRepository.Instance.GetGameConfigTypeInfo(configType);
            Dictionary<string, GameConfigEntryPatch> entryPatches = new Dictionary<string, GameConfigEntryPatch>();

            reader.CheckToken(JsonToken.StartObject);

            reader.MustRead();
            reader.CheckPropertyName("entryPatches");

            reader.MustRead();
            reader.CheckToken(JsonToken.StartObject);

            while (true)
            {
                reader.MustRead();
                if (reader.TokenType == JsonToken.EndObject)
                    break;

                string      entryMemberName = reader.GetPropertyName();
                string      configEntryName = gameConfigTypeInfo.MemberNameToEntryName[entryMemberName]; // #config-entry-name
                MemberInfo  entryMemberInfo = gameConfigTypeInfo.Entries[configEntryName].MemberInfo;
                Type        entryType       = entryMemberInfo.GetDataMemberType();
                Type        entryPatchType  = GameConfigPatchUtil.GetEntryPatchType(entryType);

                reader.MustRead();
                GameConfigEntryPatch entryPatch = (GameConfigEntryPatch)serializer.Deserialize(reader, entryPatchType);

                entryPatches.Add(configEntryName, entryPatch);
            }

            reader.MustRead();
            reader.CheckToken(JsonToken.EndObject);

            return new GameConfigPatch(configType, entryPatches);
        }

        public override FullGameConfigPatch ReadJson(JsonReader reader, Type objectType, FullGameConfigPatch existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            reader.CheckToken(JsonToken.StartObject);
            reader.MustRead();
            reader.CheckPropertyName(nameof(FullGameConfigPatch.SharedConfigPatch));
            reader.MustRead();
            GameConfigPatch shared = ReadPatch(reader, serializer, GameConfigRepository.Instance.SharedGameConfigType);
            reader.MustRead();
            reader.CheckPropertyName(nameof(FullGameConfigPatch.ServerConfigPatch));
            reader.MustRead();
            GameConfigPatch server = ReadPatch(reader, serializer, GameConfigRepository.Instance.ServerGameConfigType);
            reader.MustRead();
            reader.CheckToken(JsonToken.EndObject);
            return new FullGameConfigPatch(shared, server);
        }

        void WritePatch(JsonWriter writer, GameConfigPatch patch, JsonSerializer serializer)
        {
            GameConfigTypeInfo gameConfigTypeInfo = GameConfigRepository.Instance.GetGameConfigTypeInfo(patch.ConfigType);

            writer.WriteStartObject();
            writer.WritePropertyName("entryPatches");
            writer.WriteStartObject();
            foreach ((string entryName, GameConfigEntryPatch entryPatch) in patch.EnumerateEntryPatches())
            {
                string memberName = gameConfigTypeInfo.Entries[entryName].MemberInfo.Name; // #config-entry-name
                writer.WritePropertyName(memberName);
                serializer.Serialize(writer, entryPatch);
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        public override void WriteJson(JsonWriter writer, FullGameConfigPatch patch, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(nameof(FullGameConfigPatch.SharedConfigPatch));
            WritePatch(writer, patch.SharedConfigPatch, serializer);
            writer.WritePropertyName(nameof(FullGameConfigPatch.ServerConfigPatch));
            WritePatch(writer, patch.ServerConfigPatch, serializer);
            writer.WriteEndObject();
        }
    }

    public class GameConfigStructurePatchConverter<TStructure> : JsonConverter<GameConfigStructurePatch<TStructure>>
        where TStructure : GameConfigKeyValue, new()
    {
        public override GameConfigStructurePatch<TStructure> ReadJson(JsonReader reader, Type objectType, GameConfigStructurePatch<TStructure> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            MetaSerializableType structureTypeSpec = MetaSerializerTypeRegistry.GetTypeSpec(typeof(TStructure));

            MetaDictionary<int, object> replacedMembersByTagId = new MetaDictionary<int, object>();

            reader.CheckToken(JsonToken.StartObject);

            reader.MustRead();
            reader.CheckPropertyName("replacedMembers");

            reader.MustRead();
            reader.CheckToken(JsonToken.StartObject);

            while (true)
            {
                reader.MustRead();
                if (reader.TokenType == JsonToken.EndObject)
                    break;

                string                  memberName  = reader.GetPropertyName();
                MetaSerializableMember  memberSpec  = GameConfigPatchTypeInfoCache.GetTypeMemberByName(structureTypeSpec)[memberName];

                reader.MustRead();
                object                  memberValue = serializer.Deserialize(reader, memberSpec.Type);

                replacedMembersByTagId.Add(memberSpec.TagId, memberValue);
            }

            reader.MustRead();
            reader.CheckToken(JsonToken.EndObject);

            return new GameConfigStructurePatch<TStructure>(replacedMembersByTagId);
        }

        public override void WriteJson(JsonWriter writer, GameConfigStructurePatch<TStructure> structurePatch, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("replacedMembers");
            writer.WriteStartObject();
            foreach ((MetaSerializableMember memberSpec, object memberValue) in structurePatch.EnumerateReplacedMemberValues())
            {
                writer.WritePropertyName(memberSpec.Name);
                serializer.Serialize(writer, memberValue);
            }
            writer.WriteEndObject();

            writer.WriteEndObject();
        }
    }

    public class SessionTokenConverter : JsonConverter
    {
        public override bool CanConvert(Type type) => type == typeof(SessionToken);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            SessionToken token = (SessionToken)value;
            serializer.Serialize(writer, $"{token.Value:X16}");
        }
    }

    public class EmptyJsonContainerConverter : JsonConverter
    {
        public static readonly EmptyJsonContainerConverter Instance = new EmptyJsonContainerConverter();

        public override bool CanConvert(Type type) => type == typeof(EmptyJsonContainerConverter); // \note: this is not used. This converter is set on the property contracts manually.

        public override bool CanRead => false;
        public override object ReadJson(JsonReader reader, Type type, object existingValue, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == EmptyJsonContainer.Dict)
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
            }
            else if (value == EmptyJsonContainer.List)
            {
                writer.WriteStartArray();
                writer.WriteEndArray();
            }
            else
            {
                serializer.Serialize(writer, value);
            }
        }
    }

#if NETCOREAPP || NET_STANDARD_2_1
    public class TupleConverter : JsonConverter
    {
        public override bool CanConvert(Type type) => type.ImplementsInterface<ITuple>(); // \note: this is not used. This converter is set on the property contracts manually.

        public override bool CanRead => false;
        public override object ReadJson(JsonReader reader, Type type, object existingValue, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                ITuple tuple = (ITuple)value;

                writer.WriteStartArray();
                for (int ndx = 0; ndx < tuple.Length; ++ndx)
                    serializer.Serialize(writer, tuple[ndx]);
                writer.WriteEndArray();
            }
        }
    }
#endif

    /// <summary>
    /// Serializes <see cref="NftId"/> as JSON string instead of number,
    /// to avoid large integers which javascript might struggle with.
    /// </summary>
    public class NftIdConverter : JsonConverter
    {
        public override bool CanConvert(Type type) => type == typeof(NftId);

        public override object ReadJson(JsonReader reader, Type type, object existingValue, JsonSerializer serializer)
        {
            // \note We check for string explicitly - serializer.Deserialize<string> silently converts JSON primitives to strings.
            if (reader.TokenType != JsonToken.String)
                throw new JsonSerializationException($"NFT id must be encoded as a string in JSON, got {reader.TokenType}: {reader.Value}");

            string idString = serializer.Deserialize<string>(reader);
            return NftId.ParseFromString(idString);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, ((NftId)value).ToString());
        }
    }

#if NETCOREAPP
    /// <summary>
    /// IActorRefs are intended to be opaque handles. Printing their contents is not only useless but possibly
    /// emits very large amount of data.
    /// </summary>
    public class IActorRefConverter : JsonConverter
    {
        public override bool CanConvert(Type type) => type.IsAssignableTo(typeof(IActorRef));

        public override bool CanRead => false;

        public override object ReadJson(JsonReader reader, Type type, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, ((IActorRef)value).ToString());
        }
    }
#endif

    public class MetaplayJsonContractResolver : DefaultContractResolver
    {
        JsonSerialization.Options _options;

        public MetaplayJsonContractResolver(JsonSerialization.Options options)
        {
            _options = options;
            NamingStrategy = options.NamingStrategy;
        }

        public JsonSerialization.Options CurrentOptions => _options;

        bool ShouldIncludeMember(Type objectType, MemberInfo member)
        {
            // If doing GDPR export, ignore members with [ExcludeFromGdprExport] attribute
            if (_options.ExcludeGdprExportMembers && member.HasCustomAttribute<ExcludeFromGdprExportAttribute>())
                return false;

            // If serializing an event log, ignore members with [ExcludeFromEventLog] attribute
            if (_options.ExcludeEventLogExcludedMembers && member.HasCustomAttribute<ExcludeFromEventLogAttribute>())
                return false;

            // Handle only-in-serialization-mode attribute
            IncludeOnlyInJsonSerializationModeAttribute onlyInModeAttrib = member.GetCustomAttribute<IncludeOnlyInJsonSerializationModeAttribute>(inherit: true);
            if (onlyInModeAttrib != null)
            {
                if (onlyInModeAttrib.Mode != _options.Mode)
                    return false;
            }

            if (member is PropertyInfo property)
            {
                // If wanted, exclude read-only members.
                if (_options.ExcludeReadOnlyProperties && onlyInModeAttrib == null && !(_options.IncludeReadOnlyMetaMembers && property.HasCustomAttribute<MetaMemberAttribute>()))
                {
                    if (property.GetSetMethodOnDeclaringType() == null)
                        return false;
                }

                // Indexer properties are essentially methods with parameters. Cannot serialize those.
                if (property.GetIndexParameters().Length > 0)
                    return false;
            }

            return true;
        }

        protected override List<MemberInfo> GetSerializableMembers(Type objectType)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // Anonymous types have CompilerGeneratedAttribute (skip their fields, as they're all backing fields)
            bool isAnonymousType = objectType.HasCustomAttribute<CompilerGeneratedAttribute>();
            IEnumerable<MemberInfo> fields = isAnonymousType ? Enumerable.Empty<MemberInfo>() : objectType.GetFields(Flags).Where(field => !field.HasCustomAttribute<CompilerGeneratedAttribute>());
            IEnumerable<PropertyInfo> props = objectType.GetProperties(Flags);

            List<MemberInfo> result =
                fields.Concat(props)
                .Where(member => ShouldIncludeMember(objectType, member))
                .ToList();

            return result;
        }

        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            return base.CreateProperties(type, MemberSerialization.Fields);
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty prop = base.CreateProperty(member, memberSerialization);

            // Properties must have a setter to be writable
            if (!prop.Writable)
            {
                // Make properties with private setters also writable
                if (member is PropertyInfo propInfo)
                    prop.Writable = propInfo.GetSetMethodOnDeclaringType() != null;
            }

            // Handle base classes' properties with private setters:
            // Create the ValueProvider based on the property's declaring type when it's not the same as the reflected type.
            if (member is PropertyInfo)
            {
                PropertyInfo propInfo = (PropertyInfo)member;
                if (!propInfo.CanWrite && propInfo.DeclaringType != propInfo.ReflectedType)
                    prop.ValueProvider = base.CreateMemberValueProvider(propInfo.GetPropertyBaseDefinition());
            }

            if (prop.Writable && member.HasCustomAttribute<SensitiveAttribute>())
            {
                prop.Converter = SensitiveValueConverter.Instance;
            }
            else if (prop.Writable && member.HasCustomAttribute<ForceSerializeByValueAttribute>())
            {
                // Members tagged with [ForceSerializeByValue] override the default behaviour of serializing
                // by id only for IGameConfigData types

                // \note If at some point new types (other than IGameConfigData) are introduced that get
                //       serialized as references by default, this should be extended to support those.

                Type memberType = (member is PropertyInfo propType) ? propType.PropertyType : ((FieldInfo)member).FieldType;
                if (!typeof(IGameConfigData).IsAssignableFrom(memberType))
                    throw new InvalidOperationException($"[ForceSerializeByValue] attribute is currently only supported for {nameof(IGameConfigData)}-implementing types, {member.DeclaringType.ToGenericTypeString()}.{member.Name} is {memberType.ToGenericTypeString()}");

                // This will force the use of the standard by-value object conversion, overriding the per-type converter
                prop.Converter = DefaultObjectConverter.Instance;
                // Never include type info when serializing by value
                prop.TypeNameHandling = TypeNameHandling.None;
            }
            else if (prop.Writable && member.HasCustomAttribute<JsonSerializeNullCollectionAsEmptyAttribute>())
            {
                IValueProvider baseProvider = base.CreateMemberValueProvider(member);
                prop.Converter = EmptyJsonContainerConverter.Instance;
                prop.ValueProvider = new MetaplaySerializeNullCollectionAsEmptyValueProvider(baseProvider, member);
            }

            if (IsTypeHandledAsNoneTypeNameHandling(prop.PropertyType))
                prop.TypeNameHandling = TypeNameHandling.None;

            return prop;
        }

        protected override JsonContract CreateContract(Type type)
        {
            JsonContract contract = base.CreateContract(type);

            if (contract.Converter == null)
            {
                JsonConverter converterMaybe = TryCreateConverter(type);
                if (converterMaybe != null)
                    contract.Converter = converterMaybe;
            }
            return contract;
        }

        JsonConverter TryCreateConverter(Type type)
        {
            if (ShouldSerializeAsGameConfigId(type))
            {
                Type keyType = type.GetGenericInterfaceTypeArguments(typeof(IGameConfigData<>))[0];
                return (JsonConverter)Activator.CreateInstance(
                    type: typeof(GameConfigDataConverter<,>).MakeGenericType(keyType, type),
                    args: _options.GameConfigDataResolver);
            }
            else if (type.IsGenericTypeOf(typeof(MetaRef<>)))
            {
                Type[] metaRefTypeArgs = type.GetGenericArguments();
                return (JsonConverter)Activator.CreateInstance(
                    type: typeof(MetaRefConverter<>).MakeGenericType(metaRefTypeArgs),
                    args: _options.GameConfigDataResolver);
            }
            else if (type.IsGenericTypeOf(typeof(MetaConfigId<>)))
            {
                Type[] refTypeArgs = type.GetGenericArguments();
                return (JsonConverter)Activator.CreateInstance(
                    type: typeof(MetaConfigIdConverter<>).MakeGenericType(refTypeArgs));
            }
            else if (type == typeof(MetaUInt128))
                return new UInt128Converter();
            else if (type == typeof(F32))
                return new F32Converter();
            else if (type == typeof(F64))
                return new F64Converter();
            else if (type == typeof(EntityId))
                return new EntityIdConverter();
            else if (type == typeof(EntityKindMask))
                return new EntityKindMaskConverter();
            else if (type == typeof(MetaTime))
                return new MetaTimeConverter();
            else if (type == typeof(MetaDuration))
                return new MetaDurationConverter();
            else if (type == typeof(MetaCalendarDateTime))
                return new MetaCalendarDateTimeConverter();
            else if (type == typeof(MetaCalendarPeriod))
                return new MetaCalendarPeriodConverter();
            else if (type.ImplementsInterface<IStringId>())
                return new StringIdConverter();
            else if (type.IsDerivedFrom<GameConfigBase>())
                return new GameConfigConverter(_options);
            else if (type.IsDerivedFrom<GameConfigKeyValue>())
                return new GameConfigKeyValueConverter();
            else if (type == typeof(FullGameConfigPatch))
                return new GameConfigPatchConverter();
            else if (type.HasGenericAncestor(typeof(GameConfigStructurePatch<>)))
                return (JsonConverter)Activator.CreateInstance(typeof(GameConfigStructurePatchConverter<>).MakeGenericType(type.GetGenericAncestorTypeArguments(typeof(GameConfigStructurePatch<>))));
            else if (type.IsGenericTypeOf(typeof(Nullable<>)))
                return TryCreateConverter(type.GetGenericArguments()[0]);
            else if (type == typeof(SessionToken))
                return new SessionTokenConverter();
            else if (type == typeof(MetaGuid))
                return new MetaGuidConverter();
            else if (type == typeof(EmptyJsonContainer))
                return new EmptyJsonContainerConverter();
#if NETCOREAPP || NET_STANDARD_2_1
            else if (type.ImplementsInterface<ITuple>())
                return new TupleConverter();
#endif
            else if (type == typeof(NftId))
                return new NftIdConverter();
#if NETCOREAPP
            else if (type.IsAssignableTo(typeof(IActorRef)))
                return new IActorRefConverter();
#endif
            else
                return null;
        }

        bool ShouldSerializeAsGameConfigId(Type type)
        {
            if (!type.ImplementsGenericInterface(typeof(IGameConfigData<>)))
                return false;

            bool shouldTraverse = type == _options.TraverseGameConfigDataType;

            return !shouldTraverse;
        }

        protected override JsonObjectContract CreateObjectContract(Type objectType)
        {
            JsonObjectContract contract = base.CreateObjectContract(objectType);

            // Type info for generated types is useless.
            if (objectType.HasCustomAttribute<CompilerGeneratedAttribute>())
                contract.ItemTypeNameHandling = TypeNameHandling.None;

            return contract;
        }

        protected override JsonArrayContract CreateArrayContract(Type objectType)
        {
            JsonArrayContract contract  = base.CreateArrayContract(objectType);
            Type              valueType = typeof(object);

            if (objectType.IsArray)
                valueType = objectType.GetElementType();
            else if (objectType.ImplementsGenericInterface(typeof(IEnumerable<>)))
                valueType = objectType.GetGenericInterface(typeof(IEnumerable<>)).GetGenericArguments()[0];
            else if (objectType.IsGenericTypeOf(typeof(IEnumerable<>)))
                valueType = objectType.GetGenericArguments()[0];

            if (IsTypeHandledAsNoneTypeNameHandling(valueType))
                contract.ItemTypeNameHandling = TypeNameHandling.None;

            return contract;
        }

        static bool IsTypeHandledAsNoneTypeNameHandling(Type valueType)
        {
            return valueType != null &&
                (valueType == typeof(IGameConfigMember) ||
                    valueType.ImplementsInterface(typeof(IGameConfigMember)) ||
                    valueType == typeof(IDictionary) ||
                    valueType.ImplementsInterface(typeof(IDictionary)) ||
                    valueType.ImplementsGenericInterface(typeof(IDictionary<,>)) ||
                    valueType.ImplementsGenericInterface(typeof(IReadOnlyDictionary<,>)) ||
                    valueType.IsGenericTypeOf(typeof(IReadOnlyDictionary<,>)) ||
                    valueType.IsGenericTypeOf(typeof(IDictionary<,>)));
        }

        protected override JsonDictionaryContract CreateDictionaryContract(Type objectType)
        {
            JsonDictionaryContract contract = base.CreateDictionaryContract(objectType);

            Type valueType = typeof(object);

            // Serialize config items as objects when within library
            if (objectType.ImplementsGenericInterface(typeof(IGameConfigLibrary<,>)))
            {
                contract.ItemConverter = DefaultObjectConverter.Instance;
                Type genericInterface = objectType.GetGenericInterface(typeof(IGameConfigLibrary<,>));
                valueType = genericInterface.GetGenericArguments()[1];
            }
            else if (objectType.ImplementsGenericInterface(typeof(IDictionary<,>)))
            {
                Type genericInterface = objectType.GetGenericInterface(typeof(IDictionary<,>));
                valueType = genericInterface.GetGenericArguments()[1];
            }
            else if (objectType.ImplementsGenericInterface(typeof(IReadOnlyDictionary<,>)))
            {
                Type genericInterface = objectType.GetGenericInterface(typeof(IReadOnlyDictionary<,>));
                valueType = genericInterface.GetGenericArguments()[1];
            }
            else if (objectType.IsGenericTypeOf(typeof(IGameConfigLibrary<,>)) || objectType.IsGenericTypeOf(typeof(IDictionary<,>)) || objectType.IsGenericTypeOf(typeof(IReadOnlyDictionary<,>)))
                valueType = objectType.GetGenericArguments()[1];

            if (IsTypeHandledAsNoneTypeNameHandling(valueType))
                contract.ItemTypeNameHandling = TypeNameHandling.None;

            return contract;
        }
    }

    sealed class EmptyJsonContainer
    {
        public static readonly EmptyJsonContainer List = new EmptyJsonContainer(false);
        public static readonly EmptyJsonContainer Dict = new EmptyJsonContainer(true);

        readonly bool _isDictLike;
        EmptyJsonContainer(bool isDictLike)
        {
            _isDictLike = isDictLike;
        }
    }

    class MetaplaySerializeNullCollectionAsEmptyValueProvider : IValueProvider
    {
        readonly IValueProvider _baseProvider;
        readonly bool _isDictionaryLike;

        public MetaplaySerializeNullCollectionAsEmptyValueProvider(IValueProvider baseProvider, MemberInfo member)
        {
            Type collectionType = member.GetDataMemberType();
            if (!typeof(IEnumerable).IsAssignableFrom(collectionType))
                throw new InvalidOperationException($"[JsonSerializeNullCollectionAsEmpty] may only be set to an collection type. Type was {collectionType.ToGenericTypeString()} in {member.ToMemberWithGenericDeclaringTypeString()}");

            _baseProvider = baseProvider;
            _isDictionaryLike = typeof(IDictionary).IsAssignableFrom(collectionType);
        }

        public object GetValue(object target)
        {
            object baseValue = _baseProvider.GetValue(target);
            if (baseValue == null)
            {
                if (_isDictionaryLike)
                    return EmptyJsonContainer.Dict;
                else
                    return EmptyJsonContainer.List;
            }
            return baseValue;
        }

        public void SetValue(object target, object value)
        {
            _baseProvider.SetValue(target, value);
        }
    }
}
