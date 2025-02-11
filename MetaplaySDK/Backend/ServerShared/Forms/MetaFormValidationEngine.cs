// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Forms;
using Metaplay.Core.Localization;
using Metaplay.Core.Math;
using Metaplay.Core.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Metaplay.Server.Forms
{
    public interface IMetaFormValidationEngine
    {
        ICollection<FormValidationResult> ValidateObject(object value, System.Type type, int playerLogicVersion = -1);
    }

    public class MetaFormValidationEngine : IMetaFormValidationEngine
    {
        readonly MetaFormTypeRegistry _typeRegistry;

        public MetaFormValidationEngine(MetaFormTypeRegistry typeRegistry)
        {
            _typeRegistry = typeRegistry;
        }

        void ValidateCollectionInternal(object value, MetaFormContentType.MetaFormContentMember contentMember, FormValidationContext validationContext)
        {
            if (_typeRegistry.TryGetTypeSpec(contentMember.TypeParams[0], out MetaFormContentType valueTypeSpec))
            {
                if (value is IEnumerable enumerable)
                {
                    int index = 0;
                    foreach (object o in enumerable)
                    {
                        validationContext.PushPath(Util.ObjectToStringInvariant(index++));
                        ValidateObjectInternal(o, valueTypeSpec, validationContext);
                        validationContext.PopPath();
                    }
                }
            }
        }

        void ValidateKeyValueCollectionInternal(object value, MetaFormContentType.MetaFormContentMember contentMember, FormValidationContext validationContext)
        {
            if (contentMember.TypeParams.Length == 2 && _typeRegistry.TryGetTypeSpec(contentMember.TypeParams[1], out MetaFormContentType valueTypeSpec))
            {
                if (value is IDictionary dictionary)
                {
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        validationContext.PushPath(Util.ObjectToStringInvariant(entry.Key));
                        ValidateObjectInternal(entry.Value, valueTypeSpec, validationContext);
                        validationContext.PopPath();
                    }
                }
            }
        }

        static bool IsTypeAvailableInLogicVersion(Type type, int logicVersion)
        {
            if (MetaSerializerTypeRegistry.TryGetTypeSpec(type, out MetaSerializableType typeSpecification))
            {
                var minVersion = typeSpecification.AddedInVersion ?? 0;
                return logicVersion >= minVersion;
            }
            else if (TaggedWireSerializer.IsBuiltinType(type))
            {
                return true;
            }
            else
                throw new InvalidOperationException($"{type.ToGenericTypeString()} is not a MetaSerializable type.");
        }

        void ValidateObjectInternal(object value, MetaFormContentType contentType, FormValidationContext validationContext)
        {
            if (contentType is MetaFormClassContentType classContentType && value != null)
            {
                foreach (MetaFormContentType.MetaFormContentMember metaFormContentMember in classContentType.Members)
                {
                    if (validationContext.PlayerLogicVersion >= 0)
                    {
                        if (metaFormContentMember.AddedInVersion > validationContext.PlayerLogicVersion || metaFormContentMember.RemovedInVersion <= validationContext.PlayerLogicVersion)
                            continue;

                        if (!IsTypeAvailableInLogicVersion(metaFormContentMember.Type, validationContext.PlayerLogicVersion))
                            continue;
                    }

                    validationContext.PushPath(metaFormContentMember.FieldName);

                    MemberInfo memberInfo = contentType.Type.GetMember(metaFormContentMember.FieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).FirstOrDefault();
                    object fieldValue = null;

                    if (memberInfo is FieldInfo fieldInfo)
                        fieldValue = fieldInfo.GetValue(value);
                    else if (memberInfo is PropertyInfo propertyInfo)
                        fieldValue = propertyInfo.GetValue(value);

                    if (metaFormContentMember.FieldValidators != null)
                    {
                        foreach (IMetaFormValidator fieldValidator in metaFormContentMember.FieldValidators)
                            fieldValidator.Validate(fieldValue, validationContext);
                    }

                    if (metaFormContentMember.IsKeyValueCollection)
                        ValidateKeyValueCollectionInternal(fieldValue, metaFormContentMember, validationContext);
                    else if (metaFormContentMember.IsCollection)
                        ValidateCollectionInternal(fieldValue, metaFormContentMember, validationContext);
                    else if (_typeRegistry.TryGetTypeSpec(metaFormContentMember.Type, out MetaFormContentType memberTypeSpec))
                        ValidateObjectInternal(fieldValue, memberTypeSpec, validationContext);

                    validationContext.PopPath();
                }
            }

            if (contentType is MetaFormAbstractClassContentType && value != null)
            {
                MetaFormContentType derivedSpec = _typeRegistry.GetTypeSpec(value.GetType());
                ValidateObjectInternal(value, derivedSpec, validationContext);
            }
            else if (contentType.Validators != null)
            {
                foreach (IMetaFormValidator validator in contentType.Validators)
                    validator.Validate(value, validationContext);
            }
        }

        public ICollection<FormValidationResult> ValidateObject(object value, Type type, int playerLogicVersion = -1)
        {
            FormValidationContext context = new FormValidationContext(playerLogicVersion);

            MetaFormContentType spec = _typeRegistry.GetTypeSpec(type);
            ValidateObjectInternal(value, spec, context);

            return context.Results;
        }
    }
}
