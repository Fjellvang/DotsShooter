// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Config;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Metaplay.Core
{
    public static class TypeExtensions
    {
        public static bool TryGetDefaultValue(this ParameterInfo parameterInfo, out object defaultValue)
        {
            if (!parameterInfo.HasDefaultValue)
            {
                defaultValue = null;
                return false;
            }

            Type parameterType = parameterInfo.ParameterType;

            bool isNullableType = parameterType.IsGenericTypeOf(typeof(Nullable<>));
            if (isNullableType)
                parameterType = parameterType.GenericTypeArguments[0];

            if (!parameterType.IsValueType ||
                parameterType.IsPrimitive ||
                parameterType == typeof(decimal) ||
                parameterType.IsEnum)
            {
                // For some reason, Nullable<Enum>s are returned as numbers rather than the enum value, so we have to cast them.
                if (parameterType.IsEnum && isNullableType && parameterInfo.DefaultValue != null)
                    defaultValue = Enum.ToObject(parameterType, parameterInfo.DefaultValue);
                else
                    defaultValue = parameterInfo.DefaultValue;
                return true;
            }
            else if (parameterInfo.DefaultValue == null)
            {
                if (isNullableType)
                {
                    defaultValue = null;
                }
                else
                {
                    defaultValue = parameterType.GetDefaultValue();
                }

                return true;
            }
            else
                throw new InvalidOperationException($"Parameter '{parameterInfo.Name}' in '{parameterInfo.Member.ToMemberWithGenericDeclaringTypeString()}' has a default value that is not handled correctly. This is a bug, please report it to Metaplay.");
        }

        private static string ToTypeString(Type type, bool includeNamespace, bool includeGlobalNamespace)
        {
            List<string> components = new List<string>();

            GatherTypeStringComponents(components, type, includeNamespace, includeGlobalNamespace);

            components.Reverse();
            return string.Concat(components);
        }

        private static void GatherTypeStringComponents(List<string> components, Type type, bool includeNamespace, bool includeGlobalNamespace)
        {
            Type cursor = type;

            if (cursor.IsGenericParameter)
                throw new ArgumentException($"Type must be a concrete type, or generic type declaration. It cannot be a generic parameter. Got: {cursor}");

            if (cursor.IsArray)
            {
                // Gather all levels of a potentially-nested array.
                // E.g. for "int[,][]" gather the "[,][]", leaving cursor at "int".
                // Note the reversal of the range of `components` that represents
                // the array; this is needed due to the order in which the brackets
                // are written in the nested array syntax.

                int firstArrayComponentIndex = components.Count;

                while (cursor.IsArray)
                {
                    components.Add("[");
                    components.Add(new String(',', cursor.GetArrayRank() - 1));
                    components.Add("]");
                    cursor = cursor.GetElementType();
                }

                components.Reverse(firstArrayComponentIndex, components.Count - firstArrayComponentIndex);
            }

            Type[]  genericTypeArguments        = cursor.GenericTypeArguments;
            bool    isGenericTypeDefinition     = cursor.IsGenericTypeDefinition;
            int     numGenericArgumentsConsumed = 0;

            for (;;)
            {
                if (cursor.IsGenericType)
                {
                    string name = cursor.Name;
                    int tickIndex = name.IndexOf('`');
                    int numGenericArgumentsInThisComponent;
                    if (tickIndex != -1)
                        numGenericArgumentsInThisComponent = int.Parse(name.Substring(tickIndex+1), CultureInfo.InvariantCulture.NumberFormat);
                    else
                        numGenericArgumentsInThisComponent = 0;

                    if (numGenericArgumentsInThisComponent > 0)
                    {
                        components.Add(">");
                        if (isGenericTypeDefinition)
                        {
                            components.Add(new String(',', numGenericArgumentsInThisComponent - 1));
                        }
                        else
                        {
                            for (int ndx = numGenericArgumentsInThisComponent - 1; ndx >= 0; --ndx)
                            {
                                GatherTypeStringComponents(components, genericTypeArguments[genericTypeArguments.Length - numGenericArgumentsConsumed - numGenericArgumentsInThisComponent + ndx], includeNamespace, includeGlobalNamespace);

                                if (ndx != 0)
                                    components.Add(",");
                            }
                            numGenericArgumentsConsumed += numGenericArgumentsInThisComponent;
                        }
                        components.Add("<");
                    }

                    if (tickIndex != -1)
                        components.Add(name.Substring(0, tickIndex));
                    else
                        components.Add(name);
                }
                else
                    components.Add(cursor.Name);

                if (cursor.IsNested)
                {
                    components.Add(".");
                    cursor = cursor.DeclaringType;
                    continue;
                }
                else
                    break;
            }

            if (includeNamespace && cursor.Namespace != null)
            {
                components.Add(".");
                components.Add(cursor.Namespace);
            }
            if (includeGlobalNamespace)
                components.Add("global::");
        }

        /// <summary>
        /// Returns the full type name prefixed with any parent class names in C# syntax, i.e. how C# code would refer
        /// to this type.
        /// For example "NestingClass.NestedClass"
        /// </summary>
        public static string GetNestedClassName(this Type type) => ToTypeString(type, includeNamespace: false, includeGlobalNamespace: false);

        /// <summary>
        /// Returns the full type name in C# syntax, i.e. how C# code would refer
        /// to this type. For example "MyClass.MySubclass&lt;TKey&gt;"
        /// </summary>
        public static string ToGenericTypeString(this Type type) => ToTypeString(type, includeNamespace: false, includeGlobalNamespace: false);

        /// <summary>
        /// Returns the full type name in C# syntax, including the namespace, i.e. how C# code would refer
        /// to this type from global namespace. For example "MyNamespace.MyClass.MySubclass&lt;TKey&gt;"
        /// </summary>
        public static string ToNamespaceQualifiedTypeString(this Type type) => ToTypeString(type, includeNamespace: true, includeGlobalNamespace: false);

        /// <summary>
        /// Returns the full type name in C# syntax, including the namespace, including global:: pseudo namespace, i.e. how
        /// C# code would refer to this type from arbitrary namespace. For example "global::MyNamespace.MyClass.MySubclass&lt;TKey&gt;"
        /// </summary>
        public static string ToGlobalNamespaceQualifiedTypeString(this Type type) => ToTypeString(type, includeNamespace: true, includeGlobalNamespace: true);

        /// <summary>
        /// Returns the full path of the member without namespace. For example, "MyClass.m_myField"
        /// </summary>
        public static string ToMemberWithGenericDeclaringTypeString(this MemberInfo member)
        {
            return member.DeclaringType == null // is member a global method or variable on A MODULE (not on a class, VB/CLR feature)
                 ? member.Name
                 : member.DeclaringType.ToGenericTypeString() + "." + member.Name;
        }

        /// <summary>
        /// Returns the full path of the member with the namespace. For example, "MyNamespace.MyClass.m_myField"
        /// </summary>
        public static string ToMemberWithNamespaceQualifiedTypeString(this MemberInfo member)
        {
            return member.DeclaringType == null // is member a global method or variable on A MODULE (not on a class, VB/CLR feature)
                 ? member.Name
                 : member.DeclaringType.ToNamespaceQualifiedTypeString() + "." + member.Name;
        }

        /// <summary>
        /// Returns the full path of the member with the namespace including the global:: pseudo namespace. For example, "global::MyNamespace.MyClass.m_myField"
        /// </summary>
        public static string ToMemberWithGlobalNamespaceQualifiedTypeString(this MemberInfo member)
        {
            return member.DeclaringType == null // is member a global method or variable on A MODULE (not on a class, VB/CLR feature)
                 ? member.Name
                 : member.DeclaringType.ToGlobalNamespaceQualifiedTypeString() + "." + member.Name;
        }

        public static bool IsGenericTypeOf(this Type type, Type typeOf)
        {
            if (type.IsGenericType)
            {
                Type generic = type.GetGenericTypeDefinition();
                return generic == typeOf;
            }
            return false;
        }

        public static bool ImplementsInterface<TInterface>(this Type type)
        {
            return type.GetInterfaces().Contains(typeof(TInterface));
        }

        public static bool ImplementsInterface(this Type type, Type interfaceType)
        {
            return type.GetInterfaces().Contains(interfaceType);
        }

        public static Type[] GetGenericInterfaceTypeArguments(this Type type, Type genericInterfaceDefinition)
        {
            return type.GetGenericInterface(genericInterfaceDefinition).GetGenericArguments();
        }

        public static bool ImplementsGenericInterface(this Type type, Type genericInterfaceDefinition)
        {
            return type.GetGenericInterfaces(genericInterfaceDefinition).Any();
        }

        public static Type GetGenericInterface(this Type type, Type genericInterfaceDefinition)
        {
            IEnumerable<Type> interfaces = type.GetGenericInterfaces(genericInterfaceDefinition);
            if (!interfaces.Any())
                throw new InvalidOperationException($"Type {type.ToGenericTypeString()} does not implement an interface type based on generic definition {genericInterfaceDefinition.ToGenericTypeString()}");
            if (interfaces.Skip(1).Any())
                throw new InvalidOperationException($"Type {type.ToGenericTypeString()} implements multiple interface types based on generic definition {genericInterfaceDefinition.ToGenericTypeString()}");

            return interfaces.Single();
        }

        static IEnumerable<Type> GetGenericInterfaces(this Type type, Type genericInterfaceDefinition)
        {
            if (!genericInterfaceDefinition.IsInterface)
                throw new ArgumentException("Expecting an interface type");
            if (!genericInterfaceDefinition.IsGenericTypeDefinition)
                throw new ArgumentException("Expecting a generic type definition");

            return type.GetInterfaces().Where(i => i.IsGenericTypeOf(genericInterfaceDefinition));
        }

        public static Type[] GetGenericAncestorTypeArguments(this Type type, Type genericAncestorDefinition)
        {
            return type.GetGenericAncestor(genericAncestorDefinition).GetGenericArguments();
        }

        /// <inheritdoc cref="Implements"/>
        public static bool Implements<TTargetType>(this Type type)
        {
            return type.Implements(typeof(TTargetType));
        }

        /// <summary>
        /// Checks whether <paramref name="type"/> implements <paramref name="targetType"/> in any way.
        /// * <paramref name="type"/> is assignable to <paramref name="targetType"/> (e.g. `int` to `int?`)
        /// * <paramref name="type"/> derives from a class/interface
        /// * <paramref name="type"/> derives from a generic interface
        /// * <paramref name="type"/> derives from a generic class
        /// * <paramref name="type"/> derives from a generic type definition (e.g. `Dictionary{int, string}` implements `IDictionary{,}`
        /// * <paramref name="type"/> is of the same type as <paramref name="targetType"/>
        /// * <paramref name="targetType"/> is the generic type definition (IDictionary{,}) of <paramref name="type"/> (IDictionary{int, string}).
        /// </summary>
        public static bool Implements(this Type type, Type targetType)
        {
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));

            if (type == targetType)
                return true;

            if (targetType.IsAssignableFrom(type))
                return true;

            if (targetType.IsInterface)
            {
                if (targetType.IsGenericTypeDefinition)
                {
                    if (type.ImplementsGenericInterface(targetType))
                        return true;

                    if (type.IsGenericType && type.GetGenericTypeDefinition() == targetType)
                        return true;
                }

                return type.ImplementsInterface(targetType);
            }

            if (targetType.IsClass)
            {
                if (targetType.IsGenericTypeDefinition)
                {
                    if (type.HasGenericAncestor(targetType))
                        return true;

                    if (type.IsGenericType && type.GetGenericTypeDefinition() == targetType)
                        return true;
                }
            }

            if (targetType.IsValueType)
            {
                if (targetType.IsGenericTypeDefinition)
                {
                    if (type.IsGenericType)
                        return type.GetGenericTypeDefinition() == targetType;

                    return type.HasGenericAncestor(targetType);
                }
            }

            return false;
        }

        public static bool HasGenericAncestor(this Type type, Type genericAncestorDefinition)
        {
            return type.GetGenericAncestors(genericAncestorDefinition).Any();
        }

        public static Type TryGetGenericAncestor(this Type type, Type genericAncestorDefinition)
        {
            IEnumerable<Type> ancestors = type.GetGenericAncestors(genericAncestorDefinition);
            if (ancestors.Skip(1).Any())
                throw new InvalidOperationException($"Type {type.ToGenericTypeString()} has multiple ancestors based on generic definition {genericAncestorDefinition.ToGenericTypeString()}");

            return ancestors.SingleOrDefault();
        }

        public static Type GetGenericAncestor(this Type type, Type genericAncestorDefinition)
        {
            return TryGetGenericAncestor(type, genericAncestorDefinition) ??
                throw new InvalidOperationException($"Type {type.ToGenericTypeString()} does not have an ancestor based on generic definition {genericAncestorDefinition.ToGenericTypeString()}");
        }

        static IEnumerable<Type> GetGenericAncestors(this Type type, Type genericAncestorDefinition)
        {
            if (!genericAncestorDefinition.IsClass)
                throw new ArgumentException("Expecting a class type");
            if (!genericAncestorDefinition.IsGenericTypeDefinition)
                throw new ArgumentException("Expecting a generic type definition");

            return type.EnumerateTypeAndBases().Where(i => i.IsGenericTypeOf(genericAncestorDefinition));
        }

        public static bool IsDerivedFrom<TBase>(this Type type) where TBase : class
        {
            return typeof(TBase).IsAssignableFrom(type);
        }

        /// <summary>
        /// Enumerate <paramref name="type"/> and all its base ancestor types,
        /// starting from <paramref name="type"/>.
        /// </summary>
        public static IEnumerable<Type> EnumerateTypeAndBases(this Type type)
        {
            for (Type it = type; it != null; it = it.BaseType)
                yield return it;
        }

        //public static bool IsSubclassOfRawGeneric(this Type type, Type ofType)
        //{
        //    while (type != null && type != typeof(object))
        //    {
        //        var cur = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        //        if (ofType == cur)
        //            return true;
        //        type = type.BaseType;
        //    }
        //    return false;
        //}

        public static List<T> GetStaticFieldsOfType<T>(this Type type)
        {
            return type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(p => typeof(T).IsAssignableFrom(p.FieldType))
                .Select(pi => (T)pi.GetValue(null))
                .ToList();
        }

        public static bool GetRecursiveVisibility(this Type type)
        {
            if (!type.IsVisible)
                return false;
            if (type.HasElementType && !GetRecursiveVisibility(type.GetElementType()))
                return false;
            foreach (Type argType in type.GetGenericArguments())
            {
                // skip recursive types
                if (argType == type)
                    continue;
                if (!GetRecursiveVisibility(argType))
                    return false;
            }
            return true;
        }

        public static bool CanBeNull(this Type type)
        {
            return !type.IsValueType || type.IsGenericTypeOf(typeof(Nullable<>));
        }

        public static Type FindNonPublicComponent(this Type type)
        {
            if (type.IsGenericParameter)
                throw new ArgumentException($"Type must be a concrete type, or generic type declaration. It cannot be a generic parameter. Got: {type}");
            if (type.IsNotPublic ||type.IsNestedPrivate)
                return type;
            if (type.HasElementType)
            {
                Type nonPublicType = FindNonPublicComponent(type.GetElementType());
                if (nonPublicType != null)
                    return nonPublicType;
            }
            // Check type arguments if they are specified. Type parametes do not play into NonPublicity.
            if (!type.IsGenericTypeDefinition)
            {
                foreach (Type argType in type.GetGenericArguments())
                {
                    // skip recursive types
                    if (argType == type)
                        continue;
                    Type nonPublicType = FindNonPublicComponent(argType);
                    if (nonPublicType != null)
                        return nonPublicType;
                }
            }
            if (type.IsNested)
            {
                Type nonPublicType = FindNonPublicComponent(type.DeclaringType);
                if (nonPublicType != null)
                    return nonPublicType;
            }
            return null;
        }

        public static MethodInfo GetGetMethodOnDeclaringType(this PropertyInfo propInfo)
        {
            MethodInfo methodInfo = propInfo.GetGetMethod(nonPublic: true);
            if (methodInfo != null)
                return methodInfo;
            else
                return propInfo.DeclaringType.GetProperty(propInfo.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)?.GetGetMethod(nonPublic: true);
        }

        public static MethodInfo GetSetMethodOnDeclaringType(this PropertyInfo propInfo)
        {
            MethodInfo methodInfo = propInfo.GetSetMethod(nonPublic: true);
            if (methodInfo != null)
                return methodInfo;
            else
                return propInfo.DeclaringType.GetProperty(propInfo.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)?.GetSetMethod(nonPublic: true);
        }

        public static Func<object, object> GetGetValueOnDeclaringType(this PropertyInfo propInfo)
        {
            MethodInfo methodInfo = propInfo.GetGetMethod(nonPublic: true);
            if (methodInfo != null)
                return propInfo.GetValue;
            else
            {
                PropertyInfo propOnDeclaringType = propInfo.DeclaringType.GetProperty(propInfo.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                // GetValue() throws if there is no get method. Check it (like we just did at root level).
                if (propOnDeclaringType.GetGetMethod(nonPublic: true) != null)
                    return propOnDeclaringType.GetValue;
                return null;
            }
        }

        public static Action<object, object> GetSetValueOnDeclaringType(this PropertyInfo propInfo)
        {
            MethodInfo methodInfo = propInfo.GetSetMethod(nonPublic: true);
            if (methodInfo != null)
                return propInfo.SetValue;
            else
            {
                PropertyInfo propOnDeclaringType = propInfo.DeclaringType.GetProperty(propInfo.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                // SetValue() throws if there is no set method. Check it (like we just did at root level).
                if (propOnDeclaringType.GetSetMethod(nonPublic: true) != null)
                    return propOnDeclaringType.SetValue;
                return null;
            }
        }

        public static bool MethodIsOverridable(this MethodInfo methodInfo)
        {
            return methodInfo.IsVirtual && !methodInfo.IsFinal;
        }

        public static bool PropertyIsOverridable(this PropertyInfo propInfo)
        {
            MethodInfo getter = propInfo.GetGetMethod(nonPublic: true);
            MethodInfo setter = propInfo.GetSetMethod(nonPublic: true);

            if (getter == null && setter == null)
                MetaDebug.AssertFail("{0} has neither setter nor getter", propInfo.ToMemberWithGenericDeclaringTypeString());

            bool hasOverridableGetter = getter != null && getter.MethodIsOverridable();
            bool hasOverridableSetter = setter != null && setter.MethodIsOverridable();

            if (getter != null && setter != null && hasOverridableGetter != hasOverridableSetter)
                MetaDebug.AssertFail("{0}'s getter and setter disagree on overridability", propInfo.ToMemberWithGenericDeclaringTypeString());

            return hasOverridableGetter || hasOverridableSetter;
        }

        public static PropertyInfo GetPropertyBaseDefinition(this PropertyInfo propInfo)
        {
            MethodInfo getter = propInfo.GetGetMethod(nonPublic: true);
            MethodInfo setter = propInfo.GetSetMethod(nonPublic: true);

            if (getter == null && setter == null)
                MetaDebug.AssertFail("{0} has neither setter nor getter", propInfo.ToMemberWithGenericDeclaringTypeString());

            MethodInfo getterBase = getter?.GetBaseDefinition();
            MethodInfo setterBase = setter?.GetBaseDefinition();

            if (getter != null && setter != null && getterBase.DeclaringType != setterBase.DeclaringType)
                MetaDebug.AssertFail("{0}'s getter and setter have their base definitions declared in different types: {1} vs {2}", propInfo.ToMemberWithGenericDeclaringTypeString(), getterBase.DeclaringType.ToGenericTypeString(), setterBase.DeclaringType.ToGenericTypeString());

            return (getterBase ?? setterBase).DeclaringType.GetProperty(propInfo.Name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        public static Type GetDataMemberType(this MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case PropertyInfo propInfo: return propInfo.PropertyType;
                case FieldInfo fieldInfo: return fieldInfo.FieldType;
                default:
                    throw new ArgumentException($"{memberInfo.ToMemberWithGenericDeclaringTypeString()} is neither {nameof(PropertyInfo)} nor {nameof(FieldInfo)} (it is {memberInfo.GetType().ToGenericTypeString()})");
            }
        }

        public static Func<object, object> GetDataMemberGetValueOnDeclaringType(this MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case PropertyInfo propInfo: return propInfo.GetGetValueOnDeclaringType();
                case FieldInfo fieldInfo: return fieldInfo.GetValue;
                default:
                    throw new ArgumentException($"{memberInfo.ToMemberWithGenericDeclaringTypeString()} is neither {nameof(PropertyInfo)} nor {nameof(FieldInfo)} (it is {memberInfo.GetType().ToGenericTypeString()})");
            }
        }

        public static Action<object, object> GetDataMemberSetValueOnDeclaringType(this MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case PropertyInfo propInfo: return propInfo.GetSetValueOnDeclaringType();
                case FieldInfo fieldInfo: return fieldInfo.SetValue;
                default:
                    throw new ArgumentException($"{memberInfo.ToMemberWithGenericDeclaringTypeString()} is neither {nameof(PropertyInfo)} nor {nameof(FieldInfo)} (it is {memberInfo.GetType().ToGenericTypeString()})");
            }
        }

        public static bool DataMemberIsOverridable(this MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case PropertyInfo property: return property.PropertyIsOverridable();
                case FieldInfo _: return false;
                default:
                    throw new ArgumentException($"{memberInfo.ToMemberWithGenericDeclaringTypeString()} is neither {nameof(PropertyInfo)} nor {nameof(FieldInfo)} (it is {memberInfo.GetType().ToGenericTypeString()})");
            }
        }

        public static MemberInfo GetDataMemberBaseDefinition(this MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case PropertyInfo property: return property.GetPropertyBaseDefinition();
                case FieldInfo _: return memberInfo;
                default:
                    throw new ArgumentException($"{memberInfo.ToMemberWithGenericDeclaringTypeString()} is neither {nameof(PropertyInfo)} nor {nameof(FieldInfo)} (it is {memberInfo.GetType().ToGenericTypeString()})");
            }
        }

        public static IEnumerable<MemberInfo> EnumerateInstanceDataMembersInUnspecifiedOrder(this Type rootType)
        {
            return rootType.EnumerateInstancePropertiesInUnspecifiedOrder().Cast<MemberInfo>()
                   .Concat(rootType.EnumerateInstanceFieldsInUnspecifiedOrder());
        }

        /// <summary>
        /// Enumerate instance properties of <paramref name="rootType"/>,
        /// including those declared in its ancestors,
        /// but excluding properties with CompilerGeneratedAttribute.
        /// Ancestors' private properties are enumerated as well.
        /// The enumeration order should be considered unspecified.
        /// </summary>
        /// <remarks>
        /// Behavior for overrides and for shadowing of non-privates
        /// is whatever GetProperties does.
        /// (For overrides, only one entry is returned, not all
        /// definitions in base classes.)
        /// Inconsistently with this, for shadowing privates, all
        /// definitions with that name are returned.
        /// </remarks>
        public static IEnumerable<PropertyInfo> EnumerateInstancePropertiesInUnspecifiedOrder(this Type rootType)
        {
            // Type.GetProperties (with the right flags) returns properties also up in the hierarchy, *except* ancestor types' private properties!
            IEnumerable<PropertyInfo> properties = rootType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            // Need to get ancestors' private properties separately.
            for (Type ancestor = rootType.BaseType; ancestor != null; ancestor = ancestor.BaseType)
            {
                IEnumerable<PropertyInfo> ancestorPrivateProperties =
                    ancestor.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.NonPublic)
                    .Where(prop =>
                    {
                        bool getterIsMissingOrPrivate = prop.GetGetMethod(true)?.IsPrivate ?? true;
                        bool setterIsMissingOrPrivate = prop.GetSetMethod(true)?.IsPrivate ?? true;
                        return getterIsMissingOrPrivate && setterIsMissingOrPrivate;
                    });

                properties = properties.Concat(ancestorPrivateProperties);
            }

            return properties.Where(prop => !prop.HasCustomAttribute<CompilerGeneratedAttribute>());
        }

        /// <summary>
        /// Enumerate instance fields of <paramref name="rootType"/>,
        /// including those declared in its ancestors,
        /// but excluding fields with CompilerGeneratedAttribute.
        /// Ancestors' private fields are enumerated as well.
        /// The enumeration order should be considered unspecified.
        /// </summary>
        /// <remarks>
        /// Behavior for shadowing of non-privates
        /// is whatever GetFields does.
        /// Inconsistently with this, for shadowing privates, all
        /// definitions with that name are returned.
        /// </remarks>
        public static IEnumerable<FieldInfo> EnumerateInstanceFieldsInUnspecifiedOrder(this Type rootType)
        {
            // Type.GetFields (with the right flags) returns fields also up in the hierarchy, *except* ancestor types' private fields!
            IEnumerable<FieldInfo> fields = rootType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            // Need to get ancestors' private fields separately.
            for (Type ancestor = rootType.BaseType; ancestor != null; ancestor = ancestor.BaseType)
            {
                IEnumerable<FieldInfo> ancestorPrivateFields =
                    ancestor.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.NonPublic)
                    .Where(field => field.IsPrivate);

                fields = fields.Concat(ancestorPrivateFields);
            }

            return fields.Where(field => !field.HasCustomAttribute<CompilerGeneratedAttribute>());
        }

        /// <summary>
        /// Returns true if <paramref name="type"/> implements <see cref="ICollection"/> or <see cref="ICollection{T}"/>.
        /// </summary>
        /// <remarks>
        /// This method exists mainly for discoverablity and to allows referring into as a precondition
        /// for other methods, such as <see cref="GetCollectionElementType"/>.
        /// </remarks>
        public static bool IsCollection(this Type type)
        {
            if (typeof(ICollection).IsAssignableFrom(type))
                return true;
            // For example, HashSet is not a ICollection but is ICollection<>
            return type.ImplementsGenericInterface(typeof(ICollection<>));
        }

        /// <summary>
        /// Returns the element type of an ICollection or an Array. For Arrays, this is the type of the array
        /// element. For non-array types, this is usually the generic parameter of the ICollection. This method
        /// never returns null.
        /// </summary>
        public static Type GetCollectionElementType(this Type type)
        {
            if (!type.IsCollection())
                throw new ArgumentException($"Argument type must be a collection (got {type.ToGenericTypeString()})", nameof(type));

            if (type.HasElementType)
                return type.GetElementType();

            // Note that we fetch the element type from IEnumerable<> instead of the ICollection. This is
            // a workaround for weird collections like Queue<> which do implement ICollection but not the
            // generic version.
            return type.GetGenericInterface(typeof(IEnumerable<>)).GetGenericArguments()[0];
        }

        /// <summary>
        /// Returns true if <paramref name="type"/> implements <see cref="IEnumerable"/> or <see cref="IEnumerable{T}"/>.
        /// </summary>
        public static bool IsEnumerable(this Type type)
        {
            if (typeof(IEnumerable).IsAssignableFrom(type))
                return true;
            if ((type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)) || type.ImplementsGenericInterface(typeof(IEnumerable<>)))
                return true;

            return false;
        }

        /// <summary>
        /// Returns the element type of an Enumerable. This method never returns null.
        /// </summary>
        public static Type GetEnumerableElementType(this Type type)
        {
            if (!type.IsEnumerable())
                throw new ArgumentException($"Argument type must be an enumerable (got {type.ToGenericTypeString()})", nameof(type));

            if (type.HasElementType)
                return type.GetElementType();

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return type.GetGenericArguments()[0];
            else
                return type.GetGenericInterface(typeof(IEnumerable<>)).GetGenericArguments()[0];
        }

        /// <summary>
        /// Returns true if Type is IDictionary.
        /// </summary>
        /// <remarks>
        /// This method exists mainly for discoverablity and to allows referring into as a precondition
        /// for other methods, such as <see cref="GetDictionaryKeyAndValueTypes"/>.
        /// </remarks>
        public static bool IsDictionary(this Type type)
        {
            return typeof(IDictionary).IsAssignableFrom(type);
        }

        /// <summary>
        /// Returns the key and value types of an IDictionary. This method never returns null types.
        /// </summary>
        public static (Type KeyType, Type ValueType) GetDictionaryKeyAndValueTypes(this Type type)
        {
            Type[] genericArgs = type.GetGenericInterface(typeof(IDictionary<,>)).GetGenericArguments();
            if (genericArgs.Length != 2)
                MetaDebug.AssertFail("Dictionaries must have two generic args: <TKey, TValue>");
            return (genericArgs[0], genericArgs[1]);
        }

        /// <summary>
        /// Return the element type of a <see cref="Nullable{T}"/>. For <c>Nullable&lt;int&gt;</c> this would be <c>int</c>.
        /// </summary>
        public static Type GetSystemNullableElementType(this Type systemNullableType)
        {
            if (!systemNullableType.IsGenericTypeOf(typeof(Nullable<>)))
                throw new ArgumentException($"Argument type must be a nullable (got {systemNullableType.ToGenericTypeString()})", nameof(systemNullableType));
            return systemNullableType.GetGenericArguments()[0];
        }

        /// <summary>
        /// Returns the default value of type. I.e. default(T)
        /// </summary>
        public static object GetDefaultValue(this Type type)
        {
            if (type.IsValueType
             && !type.IsGenericTypeOf(typeof(Nullable<>))) // \note GetUninitializedObject for Nullable<T> returns the same as for T, whereas we want null.
            {
                return RuntimeHelpers.GetUninitializedObject(type);
            }
            else
                return null;
        }

        public static List<MemberInfo> GetDataMembers(this Type type, BindingFlags flags, params string[] memberNames)
        {
            List<MemberInfo> members = new List<MemberInfo>();
            foreach (string memberName in memberNames)
            {
                MemberInfo member = type.GetProperty(memberName, flags);
                if (member == null)
                    member = type.GetField(memberName, flags);

                if (member == null)
                    throw new InvalidOperationException($"Field or Property '{memberName}' not found on {type.ToGenericTypeString()}");

                members.Add(member);
            }

            return members;
        }

        /// <summary>
        /// Determines if a type has the given attribute.
        /// If <paramref name="inherit"/> is set, determines if the type or any of its base types has the attribute.
        /// <para>
        /// A type T is considered to a have an attribute A, if T has an attribute A, or if T has an attribute A' that is a subtype of A.
        /// </para>
        /// <para>
        /// If an attribute sets <c>Inherited = false</c> in its <see cref="AttributeUsageAttribute"/>, then the attribute set on a type T
        /// does not cause subtypes T' to have the attribute, regardless of <paramref name="inherit"/> parameter.
        /// </para>
        /// </summary>
        public static bool HasCustomAttribute<TAttribute>(this Type type, bool inherit = true) where TAttribute : Attribute
        {
            return HasCustomAttribute(type, typeof(TAttribute), inherit);
        }

        /// <summary>
        /// <inheritdoc cref="HasCustomAttribute{TAttribute}(Type, bool)"/>
        /// </summary>
        public static bool HasCustomAttribute(this Type type, Type attributeType, bool inherit = true)
        {
            return type.IsDefined(attributeType, inherit);
        }

        /// <summary>
        /// Determines if a method has the given attribute.
        /// <para>
        /// A method M is considered to a have an attribute A, if M has an attribute A, or if M has an attribute A' that is a subtype of A.
        /// </para>
        /// <para>
        /// If <paramref name="inherit"/> is set, a method M is considered to a have an attribute A, if M overrides a method M' and M' has an attribute A.
        /// </para>
        /// <para>
        /// If an attribute sets <c>Inherited = false</c> in its <see cref="AttributeUsageAttribute"/>, then the attribute set on a method M
        /// does not cause overriding methods M' to have the attribute, regardless of <paramref name="inherit"/> parameter.
        /// </para>
        /// <para>
        /// <b>PLATFORM BUG:</b> GetCustomAttribute (and HasCustomAttribute) returns different results on different platforms if an attribute is declared with
        /// <see cref="AttributeUsageAttribute"/> <c>Inherited = false</c>, this attribute is derived by another attribute with <see cref="AttributeUsageAttribute"/>
        /// <c>Inherited = true</c> or default, this deriving attribute is used on a method, the method is overriden, and the overriden method is queried
        /// with HasCustomAttribute against the base attribute type with <paramref name="inherit"/> <c>true</c>.
        /// In DEBUG mode, this case will be detected and an exception will be thrown.
        /// Avoid inheriting Attributes with Inherited=false in the base attribute.
        /// </para>
        /// </summary>
        public static bool HasCustomAttribute<TAttribute>(this MethodInfo mi, bool inherit = true) where TAttribute : Attribute
        {
            return HasCustomAttribute(mi, typeof(TAttribute), inherit);
        }

        /// <summary>
        /// <inheritdoc cref="HasCustomAttribute{TAttribute}(MethodInfo, bool)"/>
        /// </summary>
        public static bool HasCustomAttribute(this MethodInfo mi, Type attributeType, bool inherit = true)
        {
            CheckUnityMethodAttributeBug(mi, attributeType, inherit);
            return Attribute.IsDefined(mi, attributeType, inherit);
        }

        [Conditional("DEBUG")]
        static void CheckUnityMethodAttributeBug(MethodInfo mi, Type attributeType, bool inherit)
        {
            // If we have:
            //  * Attribute A with AttributeUsageAttribute(Inherit=false)
            //  * Attribute A' that inherits A
            //  * Method M with attribute A'
            //  * Method M' that overrides method M
            // And we query:
            //  * HasCustomAttribute(M', A, inherit=true)
            //  * Or GetCustomAttribute(M', A, inherit=true)
            // We get:
            //  * On Unity, false (null)
            //  * On Dotnet, true (not-null)

            if (inherit == false)
                return;

            bool runtimeResult = Attribute.IsDefined(mi, attributeType, inherit);

            #if !NETCOREAPP

            // True is consistent. False is inconsistent.
            if (runtimeResult == true)
                return;

            // On Unity, we check if base type has the attribute.
            // \note: We don't check the intermediate methods between the method and the base definition.
            //        It's too difficult. Instead, we let the NetCore path handle those.
            if (!Attribute.IsDefined(mi.GetBaseDefinition(), attributeType, inherit: true))
            {
                // Not on base member either. This is fine
                return;
            }

            // Since attribute was set on base method but not on the given method, we know Inherited=false
            // for the attribute or its base type.

            // Inspect the Attribute on the base definition. The found attribute must inherit the given type.
            Attribute definedAttribute = mi.GetBaseDefinition().GetCustomAttribute(attributeType, inherit: false);
            if (definedAttribute.GetType() == attributeType)
            {
                // The attribute has correct type. This is fine.
                return;
            }
            #else

            // False is consistent. True is inconsistent.
            if (runtimeResult == false)
                return;

            if (Attribute.IsDefined(mi, attributeType, inherit: false))
            {
                // Attribute on method (not overriden method). This is fine.
                return;
            }

            // Detect if this is inherited attribute
            Attribute attribute = mi.GetCustomAttribute(attributeType, inherit: true);
            if (attribute.GetType() == attributeType)
            {
                // Attribute is not inherited. This is fine.
                return;
            }

            // Require attribute has Inherited=false
            AttributeUsageAttribute usage = attributeType.GetCustomAttribute<AttributeUsageAttribute>();
            if (usage.Inherited)
            {
                // Attribute inherits to overriden methods. This is fine.
                return;
            }

            #endif

            // We have inconsistent result form runtime, throw to inform developer.

            throw new InvalidOperationException(
                "Inherited CustomAttributes on overriden methods are inconsistent and should not be used. If attribute A with AttributeUsageAttribute(Inherited=false) is derived" +
                "by attribute A', and you have a method M with attribute A and method M is overriden by M'. Now whether M' has Custom Attribute A (with inherit=true) depends on " +
                $"the current runtime environment. This query for attribute {attributeType.ToGenericTypeString()} on {mi.ToMemberWithGenericDeclaringTypeString()} matches this. " +
                "Avoid inheriting Attributes with Inherited=false in the base attribute."
                );
        }

        /// <summary>
        /// Determines if a member has the given attribute.
        /// If <paramref name="inherit"/> is set, determines if the property or the overriden property has the attribute.
        /// <para>
        /// A field or property M is considered to a have an attribute A, if M has an attribute A, or if M has an attribute A' that is a subtype of A.
        /// </para>
        /// <para>
        /// A property P is considered to a have an attribute A, if it overrides a property P' and P' has an attribute A, and <paramref name="inherit"/> is set.
        /// </para>
        /// <para>
        /// If an attribute sets <c>Inherited = false</c> in its <see cref="AttributeUsageAttribute"/>, then the attribute set on a property P
        /// does not cause overriding properties P' to have the attribute, regardless of <paramref name="inherit"/> parameter.
        /// </para>
        /// </summary>
        public static bool HasCustomAttribute<TAttribute>(this MemberInfo mi, bool inherit = true) where TAttribute : Attribute
        {
            return HasCustomAttribute(mi, typeof(TAttribute), inherit);
        }

        /// <summary>
        /// <inheritdoc cref="HasCustomAttribute{TAttribute}(MemberInfo, bool)"/>
        /// </summary>
        public static bool HasCustomAttribute(this MemberInfo mi, Type attributeType, bool inherit = true)
        {
            return Attribute.IsDefined(mi, attributeType, inherit);
        }
    }

    public static partial class EnumerableExtensions
    {
        public static MetaDictionary<TKey, TSource> ToMetaDictionary<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            return new MetaDictionary<TKey, TSource>(
                source.Select(
                    src => new KeyValuePair<TKey, TSource>(
                        keySelector(src),
                        src)));
        }

        public static MetaDictionary<TKey, TSource> ToMetaDictionary<TSource, TKey>(this IEnumerable<KeyValuePair<TKey, TSource>> source)
        {
            return new MetaDictionary<TKey, TSource>(
                source.Select(
                    src => new KeyValuePair<TKey, TSource>(src.Key, src.Value)));
        }

        public static MetaDictionary<TKey, TElement> ToMetaDictionary<TSource, TKey, TElement>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector)
        {
            return new MetaDictionary<TKey, TElement>(
                source.Select(
                    src => new KeyValuePair<TKey, TElement>(
                        keySelector(src),
                        elementSelector(src))));
        }

        #pragma warning disable CS0618 // Type or member is obsolete
        #pragma warning disable MP_OD_01 // OrderedDictionary is deprecated
        [Obsolete("OrderedDictionary is renamed to MetaDictionary to resolve conflict with .NET9 System.Collections.Generic.OrderedDictionary")]
        public static OrderedDictionary<TKey, TSource> ToOrderedDictionary<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            return new OrderedDictionary<TKey, TSource>(
                source.Select(
                    src => new KeyValuePair<TKey, TSource>(
                        keySelector(src),
                        src)));
        }

        [Obsolete("OrderedDictionary is renamed to MetaDictionary to resolve conflict with .NET9 System.Collections.Generic.OrderedDictionary")]
        public static OrderedDictionary<TKey, TSource> ToOrderedDictionary<TSource, TKey>(this IEnumerable<KeyValuePair<TKey, TSource>> source)
        {
            return new OrderedDictionary<TKey, TSource>(
                source.Select(
                    src => new KeyValuePair<TKey, TSource>(src.Key, src.Value)));
        }

        [Obsolete("OrderedDictionary is renamed to MetaDictionary to resolve conflict with .NET9 System.Collections.Generic.OrderedDictionary")]
        public static OrderedDictionary<TKey, TElement> ToOrderedDictionary<TSource, TKey, TElement>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector)
        {
            return new OrderedDictionary<TKey, TElement>(
                source.Select(
                    src => new KeyValuePair<TKey, TElement>(
                        keySelector(src),
                        elementSelector(src))));
        }
        #pragma warning restore MP_OD_01 // OrderedDictionary is deprecated
        #pragma warning restore CS0618   // Type or member is obsolete

        public static OrderedSet<T> ToOrderedSet<T>(this IEnumerable<T> source)
        {
            return new OrderedSet<T>(source);
        }

        public static IEnumerable<(T Value, int Index)> ZipWithIndex<T>(this IEnumerable<T> source) => source.Select((value, index) => (value, index));

        public static bool StartsWith<T>(this IEnumerable<T> sequence, IEnumerable<T> prefix)                                   => sequence.Take(prefix.Count()).SequenceEqual(prefix);
        public static bool StartsWith<T>(this IEnumerable<T> sequence, IEnumerable<T> prefix, IEqualityComparer<T> comparer)    => sequence.Take(prefix.Count()).SequenceEqual(prefix, comparer);

        public static IEnumerable<MetaRef<TInfo>> MetaRefWrap<TInfo>(this IEnumerable<TInfo> source)
            where TInfo : class, IGameConfigData
        {
            return source.Select(item => MetaRef<TInfo>.FromItem(item));
        }

        public static IEnumerable<TInfo> MetaRefUnwrap<TInfo>(this IEnumerable<MetaRef<TInfo>> source)
            where TInfo : class, IGameConfigData
        {
            return source.Select(r => r.Ref);
        }

        public static IEnumerable<MetaRef<TInfoOut>> MetaRefCast<TInfoIn, TInfoOut>(this IEnumerable<MetaRef<TInfoIn>> source)
            where TInfoIn : class, IGameConfigData
            where TInfoOut : class, IGameConfigData
        {
            return source.Select(r => r.CastItem<TInfoOut>());
        }

        public static IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> source)
        {
            return source.SelectMany(sub => sub);
        }
    }

#if UNITY_2018_1_OR_NEWER
    public static class KeyValuePairExtensions
    {
        public static void Deconstruct<T1, T2>(this KeyValuePair<T1, T2> kvp, out T1 key, out T2 value)
        {
            key = kvp.Key;
            value = kvp.Value;
        }
    }
#endif

    public static class DictionaryExtensions
    {
#if UNITY_2018_1_OR_NEWER
        public static bool Remove<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, out TValue value)
        {
            if (dict.TryGetValue(key, out value))
            {
                dict.Remove(key);
                return true;
            }
            else
            {
                value = default(TValue);
                return false;
            }
        }
#endif

        public static TValue GetOrAddDefaultConstructed<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
            where TValue : new()
        {
            if (!dict.TryGetValue(key, out TValue value))
            {
                value = new TValue();
                dict.Add(key, value);
            }

            return value;
        }
    }
}
