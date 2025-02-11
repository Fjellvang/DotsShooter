// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Metaplay.Core
{
    /// <summary> Internal. Do not use. See <see cref="IMetaIntegration{T}"/>. </summary>
    public interface IMetaIntegration { }

    /// <summary>
    /// Declares an Integration base type with custom lifecycle semantics. This integration can be be overriden by deriving this class.
    /// </summary>
    /// <typeparam name="T">The curiously recurring integration type (i.e. the name of the type that implements this interface)</typeparam>
    public interface IMetaIntegration<T> : IMetaIntegration where T : class, IMetaIntegration<T> { }

    /// <summary> Internal. Do not use.  </summary>
    public interface IRequireSingleConcreteType { }

    /// <summary> Internal. Do not use. See <see cref="IMetaIntegrationSingleton{T}"/>. </summary>
    public interface IMetaIntegrationSingleton : IRequireSingleConcreteType { }

    /// <summary>
    /// Declares a Singleton Integration base type. This integration can be be overriden by deriving this class.
    /// </summary>
    /// <typeparam name="T">The curiously recurring integration type (i.e. the name of the type that implements this interface)</typeparam>
    public interface IMetaIntegrationSingleton<T> : IMetaIntegration<T>, IMetaIntegrationSingleton where T : class, IMetaIntegrationSingleton<T> { }

    /// <summary> Internal. Do not use. See <see cref="IMetaIntegrationConstructible{T}"/>. </summary>
    public interface IMetaIntegrationConstructible : IRequireSingleConcreteType { }

    /// <summary>
    /// Declares a Constructible Integration base type. This integration can be be overriden by deriving this class.
    /// </summary>
    /// <typeparam name="T">The curiously recurring integration type (i.e. the name of the type that implements this interface)</typeparam>
    public interface IMetaIntegrationConstructible<T> : IMetaIntegration<T>, IMetaIntegrationConstructible where T : class, IMetaIntegrationConstructible<T> { }


    public interface IIntegrationRegistry
    {
        T Get<T>() where T : class, IMetaIntegrationSingleton<T>;
        T Create<T>() where T : class, IMetaIntegrationConstructible<T>;
        IEnumerable<Type> GetIntegrationClasses(Type type);
        IEnumerable<Type> MissingIntegrationTypes();
    }

    public static class IntegrationRegistryExtensions
    {
        public static IEnumerable<T> CreateAll<T>(this IIntegrationRegistry registry) where T : class, IMetaIntegration<T>
        {
            return registry.GetIntegrationClasses(typeof(T)).Select(x => (T)Activator.CreateInstance(x));
        }
    }

    /// <summary>
    /// A registry for game-specific implementations of Metaplay API base classes.
    ///
    /// This system supports three different flavors of API entrypoint classes:
    /// 1. IMetaIntegrationSingleton classes are for integration classes of singleton type. The implementation class is
    ///    constructed once on Metaplay core init and the registry provides access to the singleton via the `Get()` method.
    /// 2. IMetaIntegrationConstructible classes are default constructed on-demand via the `Construct()` method.
    /// 3. Base IMetaIntegration classes that have custom creation/lifetime semantics. The registry provides access to
    ///    all discovered concrete including any non-abstract SDK types without any further utilities for construction.
    ///
    /// User code is never supposed to implement these interfaces directly, but rather implement or inherit the base API
    /// class that is identified by implementing one of the forms of `IMetaIntegration`.
    ///
    /// The integration flavors 1 and 2 require a concrete implementation to exist in the code. Optional integrations can
    /// be declared as concrete API classes that can be overridden by the integration. Mandatory integrations can be declared
    /// either as abstract classes or interfaces. The integration registry will use the most derived class of a given
    /// integration type and check that there aren't multiple conflicting integrations.
    /// </summary>
    public class IntegrationRegistry : IIntegrationRegistry
    {
        HashSet<Type>                                         _mandatoryIntegrations = new HashSet<Type>();
        Dictionary<Type, List<Type>>                          _registry              = new Dictionary<Type, List<Type>>();
        Dictionary<Type, IMetaIntegrationSingleton>           _singletons            = new Dictionary<Type, IMetaIntegrationSingleton>();
        Dictionary<Type, Func<IMetaIntegrationConstructible>> _constructors          = new Dictionary<Type, Func<IMetaIntegrationConstructible>>();
        internal static IIntegrationRegistry                  Instance => MetaplayServices.Get<IIntegrationRegistry>();

        public IntegrationRegistry(Func<Type, bool> integrationTypeFilter)
        {
            foreach (Type type in TypeScanner.GetInterfaceImplementations<IMetaIntegration>())
            {
                // Generic integrations (FooIntegration<T>) are handled as separate by their instantiations (FooIntegration<int>, FooIntegration<ConcreteType>). Hence the
                // generic types are skipped.
                if (type.IsGenericType)
                    continue;

                // The IMetaIntegration type argument is the type (class or interface) that we need a concrete integration implementation for.
                // Gather all such integration types in the registry.
                Type integration = type.GetGenericInterfaceTypeArguments(typeof(IMetaIntegration<>))[0];

                // Check that the API type is accepted by the type filter
                if (!integrationTypeFilter(integration))
                    continue;

                if (integration.ImplementsInterface(typeof(IRequireSingleConcreteType)))
                {
                    _mandatoryIntegrations.Add(integration);
                }

                if (type.IsClass && !type.IsAbstract && !IsTestImplementation(integration, type))
                {
                    if (_registry.TryGetValue(integration, out List<Type> existing))
                    {
                        existing.Add(type);
                    }
                    else
                    {
                        _registry.Add(integration, new List<Type>() {type});
                    }
                }
            }

            // Singleton and Constructible specific handling
            foreach ((Type integrationType, List<Type> concreteTypes) in _registry.Where(x => x.Key.ImplementsInterface(typeof(IRequireSingleConcreteType))))
            {
                Type concreteType = FindSingleConcreteType(concreteTypes, integrationType, integrationTypeFilter);

                // Reset implementations list to contain only the chosen type
                concreteTypes.Clear();
                concreteTypes.Add(concreteType);

                if (integrationType.ImplementsInterface(typeof(IMetaIntegrationSingleton)))
                {
                    ConstructorInfo constructorInfo = MetaSerializationUtil.FindParameterlessConstructorOrThrow(concreteType);
                    _singletons.Add(integrationType, (IMetaIntegrationSingleton)constructorInfo.Invoke(Array.Empty<object>()));
                }

                if (integrationType.ImplementsInterface(typeof(IMetaIntegrationConstructible)))
                {
                    _constructors.Add(integrationType, () =>
                    {
                        ConstructorInfo constructorInfo = MetaSerializationUtil.FindParameterlessConstructorOrThrow(concreteType);
                        return (IMetaIntegrationConstructible)constructorInfo.Invoke(Array.Empty<object>());
                    });
                }
            }
        }

        public IEnumerable<Type> MissingIntegrationTypes()
        {
            foreach (Type integration in _mandatoryIntegrations)
            {
                if (!_registry.ContainsKey(integration))
                    yield return integration;
            }
        }

        static bool IsTestImplementation(Type apiType, Type implType)
        {
            // \todo: maybe tag test classes by attribute instead?
            return apiType.Namespace != implType.Namespace && (implType.Namespace?.StartsWith("Cloud.Tests", StringComparison.Ordinal) ?? false);
        }

        static Type FindSingleConcreteType(IEnumerable<Type> types, Type integrationType, Func<Type, bool> typeFilter)
        {
            // if list of candidates contains non-sdk classes, remove sdk classes
            IEnumerable<Type> candidates = types;
            IEnumerable<Type> userTypes  = types.Where(t => !typeFilter(t));
            if (userTypes.Any())
                candidates = userTypes;

            Type candidate = candidates.First();
            foreach (Type other in candidates.Skip(1))
            {
                if (other.IsSubclassOf(candidate))
                    candidate = other;
                else if (!candidate.IsSubclassOf(other))
                    throw new InvalidOperationException($"A single implementation type is required for {integrationType}. Conflicting types were found: {candidate} and {other}.");
            }

            return candidate;
        }

        IMetaIntegrationSingleton GetSingleton(Type type)
        {
            if (_singletons.TryGetValue(type, out var singleton))
                return singleton;

            throw new InvalidOperationException($"No singleton implementation has been registered for {type}");
        }

        Func<IMetaIntegrationConstructible> GetConstructor(Type type)
        {
            if (_constructors.TryGetValue(type, out var constructor))
                return constructor;

            throw new InvalidOperationException($"No constructible implementation has been registered for {type}");
        }

        IEnumerable<Type> IIntegrationRegistry.GetIntegrationClasses(Type type)
        {
            if (_registry.TryGetValue(type, out List<Type> classes))
                return classes;

            return Enumerable.Empty<Type>();
        }

        T IIntegrationRegistry.Get<T>()
        {
            return (T)GetSingleton(typeof(T));
        }

        T IIntegrationRegistry.Create<T>()
        {
            return (T)GetConstructor(typeof(T)).Invoke();
        }

        /// <summary>
        /// Get the (potentially game-specific) singleton instance for an integration type.
        /// </summary>
        public static T Get<T>() where T : class, IMetaIntegrationSingleton<T> => Instance.Get<T>();

        /// <summary>
        /// Create an instance of an (potentially game-specific) integration type by default constructing.
        /// </summary>
        public static T Create<T>() where T : class, IMetaIntegrationConstructible<T> => Instance.Create<T>();

        /// <summary>
        /// Get the integration type of for IRequireSingleConcreteType APIs
        /// </summary>
        public static Type GetSingleIntegrationType<T>() where T : class, IRequireSingleConcreteType
        {
            return GetSingleIntegrationType(typeof(T));
        }

        /// <summary>
        /// Get the integration type of a constructible
        /// </summary>
        public static Type GetSingleIntegrationType(Type apiType)
        {
            Type ret = TryGetSingleIntegrationType(apiType);
            if (ret == null)
                throw new InvalidOperationException($"Integration not found for API type {apiType}!");

            return ret;
        }

        /// <summary>
        /// Return the existing concrete integration classes for integration type. Note that this includes
        /// all non-abstract parent classes as well.
        /// </summary>
        public static IEnumerable<Type> GetIntegrationClasses(Type apiType)
        {
            if (!IsMetaIntegrationType(apiType))
                throw new ArgumentException($"Given type {apiType} is not an Integration API type. Type T must be a IMetaIntegration<T>.");

            return Instance.GetIntegrationClasses(apiType);
        }

        /// <summary>
        /// Get the integration type of a constructible if one exists, return null otherwise.
        /// </summary>
        public static Type TryGetSingleIntegrationType(Type apiType)
        {
            if (!apiType.ImplementsInterface<IRequireSingleConcreteType>())
                throw new InvalidOperationException($"Integration API type {apiType} may have multiple (or zero) integrations!");

            return Instance.GetIntegrationClasses(apiType).SingleOrDefault();
        }

        /// <summary>
        /// Report the existing concrete integration classes for integration type. Note that this includes
        /// all non-abstract parent classes as well.
        /// </summary>
        public static IEnumerable<Type> IntegrationClasses<T>() where T : class, IMetaIntegration<T>
        {
            return Instance.GetIntegrationClasses(typeof(T));
        }

        /// <summary>
        /// Determines whether a type is a MetaIntegration API type, i.e. whether the type T is declared with a
        /// <c>IMetaIntegration<![CDATA[<T>]]></c>. Such types may have implementations accessible twith.
        /// </summary>
        public static bool IsMetaIntegrationType(Type apiType)
        {
            if (!apiType.ImplementsInterface<IMetaIntegration>())
                return false;
            if (!apiType.ImplementsGenericInterface(typeof(IMetaIntegration<>)))
                return false;

            Type integrationApiType = apiType.GetGenericInterfaceTypeArguments(typeof(IMetaIntegration<>))[0];
            if (integrationApiType != apiType)
                return false;

            return true;
        }
    }
}
