// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Config;
using Metaplay.Core.IO;
using Metaplay.Core.Message;
using Metaplay.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static System.FormattableString;
using InvalidOperationException = System.InvalidOperationException;

namespace Metaplay.Core.Serialization
{
    // MetaSerializationUtil

    public static class MetaSerializationUtil
    {
        /// <summary>
        /// Converts member name to the canonical representation used for matching it to a (similarly converted)
        /// constructor parameter name.
        /// </summary>
        public static string SanitizeMemberName(string name)
        {
            return name
                .Replace("_", "")
                .ToLowerInvariant();
        }

        public static int PeekMessageTypeCode(byte[] serialized, int offset = 0)
        {
            using (IOReader reader = new IOReader(serialized, offset, serialized.Length - offset))
            {
                // Skip prefix byte
                reader.ReadByte();

                // Read & return typeCode
                return reader.ReadVarInt();
            }
        }

        public static int PeekMessageTypeCode(MetaSerialized<MetaMessage> serialized)
        {
            return PeekMessageTypeCode(serialized.Bytes, offset: 0);
        }

        public static string PeekMessageName(byte[] serialized, int offset = 0)
        {
            // Read message typeCode and try to resolve its name (or return typeCode)
            int typeCode = PeekMessageTypeCode(serialized, offset);
            if (MetaMessageRepository.Instance.TryGetFromTypeCode(typeCode, out MetaMessageSpec msgSpec))
                return msgSpec.Name;
            else
                return Invariant($"MetaMessage.Unknown#{typeCode}");
        }

        public static string PeekMessageName(MetaSerialized<MetaMessage> serialized)
        {
            if (serialized.IsEmpty)
                return "<empty>";
            else
                return PeekMessageName(serialized.Bytes, offset: 0);
        }

        public static TModel CloneModel<TModel>(TModel model, IGameConfigDataResolver resolver) where TModel : IModel
        {
            int logicVersion = model.LogicVersion;
            byte[] serialized = MetaSerialization.SerializeTagged<TModel>(model, MetaSerializationFlags.IncludeAll, logicVersion);
            return MetaSerialization.DeserializeTagged<TModel>(serialized, MetaSerializationFlags.IncludeAll, resolver, logicVersion);
        }

        public static object CreateAndPopulate(MetaSerializableType type, IEnumerable<KeyValuePair<MetaSerializableMember, object>> values)
        {
            object keyValue;
            ConstructorInfo constructor = type.DeserializationConstructor;

            if (type.UsesConstructorDeserialization)
            {
                keyValue = constructor.Invoke(
                    type.ConstructorInitializedMembers
                        .Select(
                            x => values.FirstOrDefault(y => y.Key == x)
                                .Value ?? type.ConstructorParameterValues.GetValueOrDefault(x.TagId))
                        .ToArray());
            }
            else
            {
                constructor = FindParameterlessConstructorOrThrow(type.Type);
                keyValue    = constructor.Invoke(Array.Empty<object>());
            }

            foreach ((MetaSerializableMember key, object value) in values)
            {
                if (type.ConstructorInitializedMembers == null || !type.ConstructorInitializedMembers.Contains(key))
                    key.SetValue(keyValue, value);
            }

            return keyValue;
        }

        /// <summary>
        /// Checks whether the instance is compatible with the <paramref name="logicVersion"/>,
        /// if not, <paramref name="minimumLogicVersion"/> is set to the minimum logic version of the first incompatible type we find.
        /// Note that this doesn't mean that it is guaranteed to be compatible with that version as there may be other
        /// types in the instance that are not compatible.
        /// </summary>
        public static bool IsInstanceCompatibleWithLogicVersion<T>(T instance, int logicVersion, out int minimumLogicVersion)
        {
            #if NETCOREAPP
            if (logicVersion == MetaplayCore.Options.SupportedLogicVersions.MaxVersion)
            #else
            if (logicVersion == MetaplayCore.Options.ClientLogicVersion)
            #endif
            {
                minimumLogicVersion = logicVersion;
                return true;
            }

            try
            {
                MetaSerialization.SerializeTagged(instance, MetaSerializationFlags.IncludeAll, logicVersion);
            }
            catch (MetaTypeMissingInLogicVersionSerializationException ex)
            {
                minimumLogicVersion = ex.MinimumLogicVersion;
                return false;
            }

            minimumLogicVersion = logicVersion;
            return true;
        }

        /// <summary>
        /// Checks whether the instance is compatible with the <paramref name="logicVersion"/>.
        /// </summary>
        public static bool IsInstanceCompatibleWithLogicVersion<T>(T instance, int logicVersion)
        {
            return IsInstanceCompatibleWithLogicVersion(instance, logicVersion, out _);
        }

        /// <summary>
        /// Serializes the object even if <paramref name="initialLogicVersion"/> is incompatible with the content, the minimum logic version will be calculated and output to <paramref name="minimumLogicVersion"/>.
        /// </summary>
        /// <returns>The serialized model.</returns>
        public static byte[] SerializeWithFirstCompatibleLogicVersion<T>(T model, MetaSerializationFlags flags, int initialLogicVersion, out int minimumLogicVersion)
        {
            int    logicVersion = initialLogicVersion;
            byte[] serialized   = null;
            do
            {
                try
                {
                    serialized = MetaSerialization.SerializeTagged(model, flags, logicVersion);
                }
                catch (MetaTypeMissingInLogicVersionSerializationException e)
                {
                    logicVersion = e.MinimumLogicVersion;
                }
            } while (serialized == null);

            minimumLogicVersion = logicVersion;

            return serialized;
        }
      
        public static ConstructorInfo FindParameterlessConstructorOrThrow(Type concreteType)
        {
            ConstructorInfo constructorInfo = concreteType.GetConstructor(BindingFlags.Default | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, CallingConventions.Any, Type.EmptyTypes, Array.Empty<ParameterModifier>());
            if (constructorInfo == null)
                throw new InvalidOperationException($"Could not find a parameterless constructor for '{concreteType.ToMemberWithNamespaceQualifiedTypeString()}', please define one.");
            return constructorInfo;
        }

        public static T CreateAndPopulate<T>(IEnumerable<KeyValuePair<MetaSerializableMember, object>> values)
        {
            if (MetaSerializerTypeRegistry.TryGetTypeSpec(typeof(T), out MetaSerializableType type))
                return (T)CreateAndPopulate(type, values);
            else
                throw new InvalidOperationException($"{typeof(T)} is not a MetaSerializable type.");
        }
    }
}
