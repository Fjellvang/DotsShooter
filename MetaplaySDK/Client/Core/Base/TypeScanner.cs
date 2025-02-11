// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Metaplay.Core
{
    /// <summary>
    /// Utility for scanning the runtime application for types.
    /// </summary>
    public static class TypeScanner
    {
        static readonly string[] _ignoredAssemblyPrefixes = new string[]
        {
            "System",
            "Microsoft",
            "Unity",
            "Google",
            "Mono",
            "Akka",
            "Newtonsoft",
            "Prometheus",
            "Serilog",
            "mscorlib",
            "netstandard",
            "nunit",
            "Facebook",
            "Firebase",
            "MySqlConnector",
            "Dapper",
            "JWT",
            "Parquet",
            "Pomelo",
            "AWSSDK",
            "MaxMind",
            "SharpCompress",
            "MySqlConnector",
            "NetEscapades",
            "IronSnappy",
            "MaxMind",
            "YamlDotNet",
            "NetEscapades",
            "MySqlConnector",
            "Anonymously Hosted DynamicMethods Assembly",
            "Hyperion",
            "DotNetty",
            "SQLitePCLRaw",
            "Pomelo",
            "JWT",
            "BouncyCastle",
            "NBitcoin.Secp256k1",
            "Parquet",
            "MaxMind",
            "SharpCompress",
            "IronSnappy",
            "IronCompress",
            "ZstdSharp",
            "Snappier",
        };

        public static bool ShouldIgnoreAssembly(string fullName)
        {
            return _ignoredAssemblyPrefixes.Any(prefix => fullName.StartsWith(prefix, StringComparison.Ordinal));
        }

        /// <summary>
        /// Get a list of assemblies that may contain the game's own types.
        /// Works by filtering out known external assemblies, so returned list likely
        /// contains some external assemblies as well.
        /// </summary>
        /// <returns>List of assemblies which may contain game's own types</returns>
        public static List<Assembly> GetOwnAssemblies()
        {
            List<Assembly> result = new List<Assembly>();
            //foreach (Assembly assembly in Assembly.GetEntryAssembly().GetReferencedAssemblies()) // \todo [petri] this should cover all assemblies?
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (ShouldIgnoreAssembly(assembly.FullName))
                    continue;

                result.Add(assembly);
            }
            return result;
        }

        /// <summary>
        /// Gets all types from game and SDK assemblies. Types from most common third-party SDKs are not included in the results.
        /// </summary>
        public static IEnumerable<Type> GetAllTypes()
        {
            return GetOwnAssemblies()
                .SelectMany(assembly => assembly.GetTypes());
        }

        /// <summary>
        /// Gets all types that derive from <typeparamref name="TBaseType"/>. The type itself is not included in the set.
        /// </summary>
        /// <remarks>
        /// Class implementing an interface does not inherit it, hence calling this for an interface type will
        /// result in an empty list.
        /// </remarks>
        public static IEnumerable<Type> GetDerivedTypes<TBaseType>() where TBaseType : class
        {
            #if UNITY_EDITOR
            // GetTypesDerivedFrom returns also implemented interfaces. Filter them out
            return UnityEditor.TypeCache.GetTypesDerivedFrom<TBaseType>()
                .Where(type => type.IsSubclassOf(typeof(TBaseType)));
            #else
            return GetAllTypes()
                .Where(type => type.IsSubclassOf(typeof(TBaseType)));
            #endif
        }

        /// <summary>
        /// Gets all types that derive from <typeparamref name="TBaseType"/> and the type itself.
        /// </summary>
        /// <remarks>
        /// Class implementing an interface does not inherit it, hence calling this for an interface type will
        /// result in an list containing only self.
        /// </remarks>
        public static IEnumerable<Type> GetDerivedTypesAndSelf<TBaseType>() where TBaseType : class
        {
            #if UNITY_EDITOR
            // GetTypesDerivedFrom returns also implemented interfaces. Filter them out.
            return UnityEditor.TypeCache.GetTypesDerivedFrom<TBaseType>()
                .Where(type => type.IsSubclassOf(typeof(TBaseType)))
                .Prepend(typeof(TBaseType));
            #else
            return GetAllTypes()
                .Where(type => type == typeof(TBaseType) || type.IsSubclassOf(typeof(TBaseType)));
            #endif
        }

        /// <summary>
        /// Gets all non-abstract types that derive from <typeparamref name="TBaseType"/>. The type itself is not included in the set.
        /// The resulting set does not include Generic types without type arguments even if those are not abstract.
        /// </summary>
        /// <remarks>
        /// Class implementing an interface does not inherit it, hence calling this for an interface type will
        /// result in an empty list.
        /// </remarks>
        public static IEnumerable<Type> GetConcreteDerivedTypes<TBaseType>() where TBaseType : class
        {
            return GetDerivedTypes<TBaseType>()
                .Where(type => !type.IsAbstract && !type.IsGenericTypeDefinition);
        }

        /// <summary>
        /// Gets all non-abstract types that derive from <paramref name="baseType"/>. The type itself is not included in the set.
        /// The resulting set does not include Generic types without type arguments even if those are not abstract.
        /// </summary>
        /// <remarks>
        /// Class implementing an interface does not inherit it, hence calling this for an interface type will
        /// result in an empty list.
        /// </remarks>
        public static IEnumerable<Type> GetConcreteDerivedTypes(Type baseType)
        {
            if (baseType.IsGenericTypeDefinition)
                return GetAllTypes().Where(type => type.HasGenericAncestor(baseType) && !type.IsAbstract && !type.IsGenericTypeDefinition);
            else
                return GetAllTypes().Where(type => type.IsSubclassOf(baseType) && !type.IsAbstract && !type.IsGenericTypeDefinition);
        }


        /// <summary>
        /// Gets all types that implement the <typeparamref name="TInterface"/>.
        /// </summary>
        /// <remarks>
        /// An interface inheriting the another interface does not implement it, hence return value does not contain any intefaces.
        /// </remarks>
        public static ICollection<Type> GetInterfaceImplementations<TInterface>() where TInterface : class
        {
            // Resolve the types eagerly. In all happy paths, all elements are consumed. Lazy linq
            // only makes callstacks hard to follow.
            #if UNITY_EDITOR
            UnityEditor.TypeCache.TypeCollection coarseTypes = UnityEditor.TypeCache.GetTypesDerivedFrom<TInterface>();
            List<Type> returnTypes = new List<Type>(capacity: coarseTypes.Count);

            foreach (Type type in coarseTypes)
            {
                if (type.IsInterface)
                    continue;
                returnTypes.Add(type);
            }
            return returnTypes;
            #else
            List<Type> types = new List<Type>();
            foreach (Type type in GetAllTypes())
            {
                if (type.IsInterface)
                    continue;
                if (!type.ImplementsInterface<TInterface>())
                    continue;
                types.Add(type);
            }
            return types;
            #endif
        }

        /// <summary>
        /// Gets all types that have the <typeparamref name="TAttrib"/> attribute (may also have multiple copies of the attribute).
        /// </summary>
        /// <remarks>
        /// If attribute is marked as Inherited, all subclasses of a baseclass with the attribute will be included in the results.
        /// </remarks>
        public static IEnumerable<Type> GetClassesWithAttribute<TAttrib>() where TAttrib : Attribute
        {
            // Resolve the types eagerly. In all happy paths, all elements are consumed. Lazy linq
            // only makes callstacks hard to follow.
            #if UNITY_EDITOR
            bool inherit = typeof(TAttrib).GetCustomAttribute<AttributeUsageAttribute>(inherit: true).Inherited;
            if (!inherit)
            {
                UnityEditor.TypeCache.TypeCollection attributedTypes = UnityEditor.TypeCache.GetTypesWithAttribute<TAttrib>();
                List<Type> classTypes = new List<Type>(capacity: attributedTypes.Count);

                foreach (Type type in attributedTypes)
                {
                    if (!type.IsClass)
                        continue;
                    classTypes.Add(type);
                }
                return classTypes;
            }
            else
            {
                UnityEditor.TypeCache.TypeCollection attributedTypes = UnityEditor.TypeCache.GetTypesWithAttribute<TAttrib>();
                OrderedSet<Type> classTypes = new OrderedSet<Type>(capacity: attributedTypes.Count);

                foreach (Type type in attributedTypes)
                {
                    if (!type.IsClass)
                        continue;
                    classTypes.Add(type);
                    foreach (Type subtype in UnityEditor.TypeCache.GetTypesDerivedFrom(type))
                        classTypes.Add(subtype);
                }

                return classTypes;
            }
            #else
            List<Type> types = new List<Type>();
            foreach (Type type in GetAllTypes())
            {
                if (!type.IsClass)
                    continue;
                // \note HasCustomAttribute with `inherit: true` does still respect the Inherited property of the attribute type,
                //       so we do not need to check that manually here.
                if (!type.HasCustomAttribute(typeof(TAttrib), inherit: true))
                    continue;
                types.Add(type);
            }
            return types;
            #endif
        }

        /// <summary>
        /// Gets all concerte types that have the <typeparamref name="TAttrib"/> attribute.
        /// </summary>
        /// <remarks>
        /// If attribute is marked as Inherited, all subclasses of a baseclass with the attribute will be included in the results.
        /// </remarks>
        public static IEnumerable<Type> GetConcreteClassesWithAttribute<TAttrib>() where TAttrib : Attribute
        {
            return GetClassesWithAttribute<TAttrib>()
                .Where(type => !type.IsAbstract);
        }

        /// <summary>
        /// Finds a type with the given <see cref="Type.FullName" /> from all game and SDK assemblies, or returns null if no such type exists.
        /// The supplied name should not have the assembly specifier.
        /// </summary>
        public static Type TryGetTypeByName(string name)
        {
            foreach (Assembly assembly in GetOwnAssemblies())
            {
                Type type = assembly.GetType(name);
                if (type != null)
                    return type;
            }
            return null;
        }
    }
}
