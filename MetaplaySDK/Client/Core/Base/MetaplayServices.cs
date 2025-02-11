// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Metaplay.Core
{
    public interface IServiceInitializers
    {
        /// <summary>
        /// Add service that is created using a function that takes the MetaplayServices provider as argument
        /// for retrieving dependencies.
        /// </summary>
        /// <param name="initializer"></param>
        /// <typeparam name="TService"></typeparam>
        void Add<TService>(Func<IMetaplayServiceProvider, TService> initializer) where TService : class;
        /// <summary>
        /// Add service that is created using a parameterless function.
        /// </summary>
        /// <param name="initializer"></param>
        /// <typeparam name="TService"></typeparam>
        void Add<TService>(Func<TService> initializer) where TService : class;
        /// <summary>
        /// Add service that is created by invoking the default constructor of the type.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        void Add<TService>() where TService : class, new();
    }

    /// <summary>
    /// Main Metaplay services lookup. In environments where .net dependency injection is available (server) this is
    /// a thin wrapper around IServiceProvider.
    /// </summary>
    public interface IMetaplayServiceProvider
    {
        /// <summary>
        /// Try to get service of type TService, potentially instantiating it. Returns false if service does not exist.
        /// </summary>
        /// <param name="service"></param>
        /// <typeparam name="TService"></typeparam>
        /// <returns></returns>
        bool TryGet<TService>(out TService service);
        /// <summary>
        /// Get service of type TService, potentially instantiating it. Throws if service does not exist.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <returns></returns>
        TService Get<TService>();
    }

    // Static service locator for backwards compatibility with non-DI code.
    public static class MetaplayServices
    {
        // Statically initialize with an empty instance to provide better error messages.
        static readonly IMetaplayServiceProvider _emptyInstance = new SimpleMetaplayServiceProvider();
        static          IMetaplayServiceProvider _singleton     = _emptyInstance;
        public static   bool                     IsInitialized => _singleton != _emptyInstance;

        public static IMetaplayServiceProvider SetServiceProvider(IMetaplayServiceProvider services)
        {
            IMetaplayServiceProvider current = _singleton;
            _singleton = services;
            return current;
        }

        public static TService Get<TService>() => _singleton.Get<TService>();
        public static bool TryGet<TService>(out TService service) => _singleton.TryGet(out service);
    }

    public class SimpleServiceInitializers : IServiceInitializers
    {
        MetaDictionary<Type, Func<IMetaplayServiceProvider, object>> _initializers = new MetaDictionary<Type, Func<IMetaplayServiceProvider, object>>();

        public void Add<TService>(Func<IMetaplayServiceProvider, TService> initializer) where TService : class
        {
            _initializers.AddOrReplace(typeof(TService), initializer);
        }

        public void Add<TService>(Func<TService> initializer) where TService : class
        {
            _initializers.AddOrReplace(typeof(TService), _ => initializer());
        }

        public void Add<TService>() where TService : class, new()
        {
            _initializers.AddOrReplace(typeof(TService), _ => new TService());
        }

        public IEnumerable<Type> RegisteredTypes => _initializers.Keys;
        public IEnumerable<KeyValuePair<Type, Func<IMetaplayServiceProvider, object>>> Initializers => _initializers;
    }


    public class SimpleMetaplayServiceProvider : IMetaplayServiceProvider
    {
        class ServiceDescriptor
        {
            public ServiceDescriptor(Func<IMetaplayServiceProvider, object> initializer)
            {
                Instance = null;
                Initializer = initializer;
            }

            public object Instance;
            public Func<IMetaplayServiceProvider, object> Initializer;
            public bool IsBeingCreated = false;
        }


        Dictionary<Type, ServiceDescriptor> _services;

        public SimpleMetaplayServiceProvider()
        {
            _services = new Dictionary<Type, ServiceDescriptor>();
        }

        public SimpleMetaplayServiceProvider(SimpleServiceInitializers initializers)
        {
            _services = initializers.Initializers.ToDictionary(x => x.Key, x => new ServiceDescriptor(x.Value));
        }

        // Returns null if type wasn't registered, throws if initialization fails. Not thread safe!
        internal object GetOrCreate(Type serviceType)
        {
            if (!_services.TryGetValue(serviceType, out ServiceDescriptor service))
                return null;
            if (service.Instance == null)
            {
                if (service.IsBeingCreated)
                    throw new InvalidOperationException("Cyclic service dependency detected!");
                try
                {
                    service.IsBeingCreated = true;
                    service.Instance = service.Initializer(this);
                }
                finally
                {
                    service.IsBeingCreated = false;
                }
            }
            return service.Instance;
        }

        /// <inheritdoc cref="IMetaplayServiceProvider.TryGet{TService}(out TService)"/>
        bool IMetaplayServiceProvider.TryGet<TService>(out TService service)
        {
            object s = GetOrCreate(typeof(TService));
            if (s == null)
            {
                service = default;
                return false;
            }
            service = (TService)s;
            return true;
        }

        /// <inheritdoc cref="IMetaplayServiceProvider.Get{TService}()"/>
        TService IMetaplayServiceProvider.Get<TService>()
        {
            object s = GetOrCreate(typeof(TService));
            if (s == null)
                throw new InvalidOperationException($"Accessing uninitialized Metaplay service {typeof(TService)}!");
            return (TService)s;
        }
    }
}
