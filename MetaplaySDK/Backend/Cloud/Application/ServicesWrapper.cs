// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Akka.Util.Extensions;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using Metaplay.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Metaplay.Cloud.Application
{
    public class ServiceInitializersWrapper : IServiceInitializers
    {
        IServiceCollection _collection;
        public OrderedSet<Type> RegisteredTypes { get; } = new OrderedSet<Type>();

        public ServiceInitializersWrapper(IServiceCollection collection)
        {
            _collection = collection;
        }

        void IServiceInitializers.Add<TService>(Func<IMetaplayServiceProvider, TService> initializer)
        {
            _collection.AddSingleton(provider => initializer(provider.GetService<IMetaplayServiceProvider>()));
            RegisteredTypes.Add(typeof(TService));
        }

        void IServiceInitializers.Add<TService>(Func<TService> initializer)
        {
            _collection.AddSingleton(_ => initializer());
            RegisteredTypes.Add(typeof(TService));
        }

        void IServiceInitializers.Add<TService>()
        {
            _collection.AddSingleton<TService>();
            RegisteredTypes.Add(typeof(TService));
        }
    }

    /// <summary>
    /// Implementation of IMetaplayServiceProvider that simply wraps a .net IServiceProvider and
    /// registers itself to be the MetaplayServices static service locator for the duration of its
    /// lifetime.
    /// </summary>
    /// Note on interaction with the MetaplayServices service locator: when this provider is constructed
    /// it replaces the existing MetaplayServices singleton with itself and stores the current singleton
    /// as `_oldSingleton`. On disposing, the old singleton is set to be active again. This is to allow
    /// overriding the current services temporarily in a smaller context, for example for unit test purposes.
    public class ServiceProviderWrapper : IMetaplayServiceProvider, IDisposable
    {
        IServiceProvider _provider;
        IMetaplayServiceProvider _oldSingleton;

        public ServiceProviderWrapper(IServiceProvider provider)
        {
            _provider     = provider;
            _oldSingleton = MetaplayServices.SetServiceProvider(this);
        }

        public void Dispose()
        {
            MetaplayServices.SetServiceProvider(_oldSingleton);
        }

        /// <inheritdoc cref="IMetaplayServiceProvider.TryGet{TService}(out TService)"/>
        public bool TryGet<TService>(out TService service)
        {
            service = _provider.GetService<TService>();
            return service != null;
        }

        /// <inheritdoc cref="IMetaplayServiceProvider.Get{TService}()"/>
        public TService Get<TService>()
        {
            TService service = _provider.GetService<TService>();
            if (service == null)
                throw new InvalidOperationException($"Accessing uninitialized Metaplay service {typeof(TService)}!");

            return service;
        }
    }

    public class ForceInitializedServiceList
    {
        // list of services that are force-activated before anything else, in the order configured
        public List<Type> EarlyServices = new List<Type>();
        // list of services that are force-activated
        public List<Type> Services = new List<Type>();
    }

    public class MetaplayForceInit
    {
        public MetaplayForceInit(IServiceProvider provider, ForceInitializedServiceList forceInitializedServices)
        {
            // Make sure that the early init is done
            provider.GetRequiredService<MetaplayForceInitEarly>();

            // Force listed services to be initialized now, in the order provided.
            // This should be eventually removed, when the core services express their
            // dependencies correctly and don't rely on initialization order under the hood.
            foreach (Type type in forceInitializedServices.Services)
                provider.GetRequiredService(type);
        }
    }

    public class MetaplayForceInitEarly
    {
        public MetaplayForceInitEarly(IServiceProvider provider, ForceInitializedServiceList forceInitializedServices)
        {
            provider.GetRequiredService<IMetaplayServiceProvider>();

            // Force listed services to be initialized now, in the order provided.
            // This should be eventually removed, when the core services express their
            // dependencies correctly and don't rely on initialization order under the hood.
            foreach (Type type in forceInitializedServices.EarlyServices)
                provider.GetRequiredService(type);
        }
    }

    public static class MetaplayHostBuilderExtensions
    {
        public static IServiceCollection AddStartupSingleton<TService>(this IServiceCollection collection, bool early = false) where TService : class
        {
            collection.AddSingleton<TService>();
            collection.Configure<ForceInitializedServiceList>(early ? x => x.EarlyServices.Add(typeof(TService)) : x => x.Services.Add(typeof(TService)));
            return collection;
        }

        static bool InitServiceEarly(Type service)
        {
            return service != typeof(MetaSerialization) &&
                service != typeof(TaggedSerializerRoslyn);
        }

        public static IServiceCollection AddMetaplayCore(this IServiceCollection collection, RuntimeEnvironmentInfo envInfo)
        {
            ServiceInitializersWrapper collectionWrapper = new ServiceInitializersWrapper(collection);

            collectionWrapper
                .AddMetaplayCore()
                .ConfigureMetaSerialization(envInfo.ApplicationName);

            // Inject an implementation of IMetaplayServices that forward calls to the DI container and
            // registers the static MetaplayServices singleton for legacy code.
            collection.AddSingleton<IMetaplayServiceProvider>(provider => new ServiceProviderWrapper(provider));
            collection.Configure<ForceInitializedServiceList>(x =>
            {
                // Add MetaplayCore services into the list of services to force initialize. Most services should be
                // initialized early (i.e. as part of host build).
                foreach (Type service in collectionWrapper.RegisteredTypes)
                {
                    if (InitServiceEarly(service))
                        x.EarlyServices.Add(service);
                    else
                        x.Services.Add(service);
                }
            });
            collection.AddTransient(provider => new MetaplayForceInit(provider, provider.GetRequiredService<IOptions<ForceInitializedServiceList>>().Value));
            collection.AddTransient(provider => new MetaplayForceInitEarly(provider, provider.GetRequiredService<IOptions<ForceInitializedServiceList>>().Value));
            return collection;
        }
    }
}
