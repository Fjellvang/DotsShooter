// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Forms;
using Metaplay.Server.AdminApi;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Metaplay.Server.Forms
{
    public interface IMetaFormSchemaProvider
    {
        bool TryGetJsonSchema(string namespaceQualifiedTypeName, out JObject schema);
        IEnumerable<JObject> GetAllSchemas();
    }

    public class MetaFormSchemaProvider : IMetaFormSchemaProvider
    {
        readonly Dictionary<string, JObject> _schemas = new Dictionary<string, JObject>();

        public bool TryGetJsonSchema(string namespaceQualifiedTypeName, out JObject schema)
        {
            lock (_schemas)
            {
                if (!_schemas.TryGetValue(namespaceQualifiedTypeName, out schema))
                {
                    JObject builtSchema = BuildSchema(namespaceQualifiedTypeName);
                    if (builtSchema == null)
                        return false;

                    _schemas.Add(namespaceQualifiedTypeName, builtSchema);
                    schema = builtSchema;
                    return true;
                }

                return true;
            }
        }

        public IEnumerable<JObject> GetAllSchemas()
        {
            lock (_schemas)
            {
                JObject[] arr = new JObject[_schemas.Count];
                _schemas.Values.CopyTo(arr, 0);
                return arr;
            }
        }

        static JObject BuildSchema(string nameSpaceQualifiedTypeName)
        {
            if (MetaFormTypeRegistry.Instance.TryGetPrimitiveByName(nameSpaceQualifiedTypeName, out System.Type primitiveType))
            {
                JObject primitiveSchema = new JObject();
                primitiveSchema.Add("typeKind", new JValue(MetaFormContentTypeKind.Primitive.ToString()));
                primitiveSchema.Add("typeName", new JValue(primitiveType.ToNamespaceQualifiedTypeString()));
                primitiveSchema.Add("jsonType", new JValue(primitiveType.FullName));
                return primitiveSchema;
            }

            if (MetaFormTypeRegistry.Instance.TryGetNullablePrimitiveByName(nameSpaceQualifiedTypeName, out System.Type nullablePrimitiveType))
            {
                JObject nullablePrimitiveSchema = new JObject();
                nullablePrimitiveSchema.Add("typeKind", new JValue(MetaFormContentTypeKind.Nullable.ToString()));
                nullablePrimitiveSchema.Add("typeName", new JValue(nullablePrimitiveType.ToNamespaceQualifiedTypeString()));
                nullablePrimitiveSchema.Add("jsonType", new JValue(nullablePrimitiveType.FullName));

                Type valueType = null;
                if (nullablePrimitiveType.IsGenericTypeOf(typeof(Nullable<>)))
                    valueType = nullablePrimitiveType.GenericTypeArguments[0];
                nullablePrimitiveSchema.Add("valueType", new JValue(valueType.ToString()));
                return nullablePrimitiveSchema;
            }

            if (!MetaFormTypeRegistry.Instance.TryGetByName(nameSpaceQualifiedTypeName, out MetaFormContentType type))
                return null;

            JObject schema = new JObject();
            schema.Add("typeKind", new JValue(type.TypeKind.ToString()));
            schema.Add("typeName", new JValue(type.Name));
            schema.Add("jsonType", new JValue(type.Type.FullName));

            if (type.SerializableType.AddedInVersion != null)
                schema.Add("addedInVersion", new JValue(type.SerializableType.AddedInVersion));

            if (type.TypeKind == MetaFormContentTypeKind.Abstract)
            {
                JArray                           derivedTypes = new JArray();
                MetaFormAbstractClassContentType abstractType = type as MetaFormAbstractClassContentType;

                foreach (MetaFormClassContentType derived in abstractType.DerivedTypes)
                {
                    JObject dObject = new JObject();
                    dObject.Add("typeName", new JValue(derived.Name));
                    dObject.Add("isDeprecated", new JValue(derived.IsDeprecated));
                    dObject.Add("jsonType", new JValue(derived.Type.FullName));

                    if (derived.SerializableType.AddedInVersion != null)
                        dObject.Add("addedInVersion", new JValue(derived.SerializableType.AddedInVersion));

                    derivedTypes.Add(dObject);
                }

                schema.Add("isLocalized", new JValue(abstractType.IsLocalized));
                schema.Add("isGeneric", new JValue(abstractType.IsGeneric));
                schema.Add("derived", derivedTypes);
            }
            else if (type.TypeKind == MetaFormContentTypeKind.Class || type.TypeKind == MetaFormContentTypeKind.Localized)
            {
                JArray                   fields    = new JArray();
                MetaFormClassContentType classType = type as MetaFormClassContentType;

                object classObj = null;
                try
                {
                    classObj = Activator.CreateInstance(classType.Type, true);
                } catch(MissingMethodException) {}


                foreach (MetaFormContentType.MetaFormContentMember member in classType.Members)
                {
                    JObject field = new JObject();
                    field.Add("fieldName", new JValue(member.FieldName));

                    if (member.IsValueCollection)
                        field.Add("fieldType", new JValue("[]"));
                    else if (member.IsKeyValueCollection)
                        field.Add("fieldType", new JValue("{}"));
                    else
                        field.Add("fieldType", new JValue(member.TypeName));

                    if (member.TypeParams != null)
                        field.Add("typeParams", new JArray(member.TypeParams.Select(x => x.ToNamespaceQualifiedTypeString())));
                    else if (member.Type.IsArray)
                        field.Add("typeParams", new JArray(new object[] {member.Type.GetElementType()}));

                    field.Add("typeKind", new JValue(member.TypeKind));
                    field.Add("isLocalized", new JValue(member.IsLocalized));

                    if (member.AddedInVersion != null)
                        field.Add("addedInVersion", new JValue(member.AddedInVersion));

                    if (member.RemovedInVersion != null)
                        field.Add("removedInVersion", new JValue(member.RemovedInVersion));

                    if (member.CaptureDefault && classObj != null)
                    {
                        try
                        {
                            object defaultValue = classType.Type.GetMember(member.FieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).FirstOrDefault()?
                                .GetDataMemberGetValueOnDeclaringType().Invoke(classObj);

                            if (!(defaultValue?.Equals(member.Type.GetDefaultValue()) ?? true)) // If not same as default
                                field.Add("default", JToken.FromObject(defaultValue, AdminApiJsonSerialization.Serializer));
                        }
                        catch (Exception)
                        {
                            // \todo [nomi] not this.
                        }
                    }

                    foreach (IMetaFormFieldDecorator decorator in member.FieldDecorators.Where(d => !d.IsMultiDecorator))
                        field[decorator.FieldDecoratorKey] = JToken.FromObject(decorator.FieldDecoratorValue);

                    foreach (IGrouping<string, IMetaFormFieldDecorator> group in member.FieldDecorators.Where(d => d.IsMultiDecorator).GroupBy(d => d.FieldDecoratorKey))
                        field[group.Key] = new JArray(group.Select(d => JToken.FromObject(d.FieldDecoratorValue)));

                    fields.Add(field);
                }

                schema.Add("isLocalized", new JValue(classType.IsLocalized));
                schema.Add("isGeneric", new JValue(classType.IsGeneric));
                schema.Add("useAsContext", new JValue(classType.UseAsContext));
                schema.Add("fields", fields);
            }
            else if (type.TypeKind == MetaFormContentTypeKind.Enum || type.TypeKind == MetaFormContentTypeKind.DynamicEnum)
            {
                MetaFormEnumContentType enumType = type as MetaFormEnumContentType;
                schema.Add("possibleValues", new JArray(enumType.PossibleValues));
            }
            else if (type.TypeKind == MetaFormContentTypeKind.StringId || type.TypeKind == MetaFormContentTypeKind.ConfigLibraryItem)
            {
                MetaFormConfigReferenceContentType refType = type as MetaFormConfigReferenceContentType;
                schema.Add("configLibrary", refType.ConfigLibrary);
            }
            else if (type.TypeKind == MetaFormContentTypeKind.Nullable)
            {
                MetaformNullableContentType nullableType = type as MetaformNullableContentType;
                schema.Add("valueType", nullableType.valueType.ToString());
            }

            return schema;
        }
    }
}
