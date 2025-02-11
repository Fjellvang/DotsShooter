// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Application;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Sharding;
using Metaplay.Cloud.Utility;
using Metaplay.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Server.AdminApi
{
    // AdminApiActor

    [EntityConfig]
    public class AdminApiConfig : EphemeralEntityConfig
    {
        public override EntityKind          EntityKind              => EntityKindCloudCore.AdminApi;
        public override Type                EntityActorType         => typeof(AdminApiActor);
        public override NodeSetPlacement    NodeSetPlacement        => NodeSetPlacement.Service;
        public override IShardingStrategy   ShardingStrategy        => ShardingStrategies.CreateSingletonService();
        public override TimeSpan            ShardShutdownTimeout    => TimeSpan.FromSeconds(30);
    }

    class MetaplayControllerActivator : IControllerActivator
    {
        IServiceProvider _serviceProvider;
        ConcurrentDictionary<Type, Func<object>> _factories = new ConcurrentDictionary<Type, Func<object>>();

        public MetaplayControllerActivator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public object Create(ControllerContext context)
        {
            Func<object> factory = _factories.GetOrAdd(context.ActionDescriptor.ControllerTypeInfo.AsType(), CreateFactory);
            return factory();
        }

        Func<object> CreateFactory(Type type)
        {
            ConstructorInfo ci = type.GetConstructors()[0];

            ParameterInfo[] parameters = ci.GetParameters();
            object[] arguments = parameters.Length == 0 ? Array.Empty<object>() : new object[parameters.Length];

            for (int ndx = 0; ndx < parameters.Length; ++ndx)
                arguments[ndx] = _serviceProvider.GetRequiredService(parameters[ndx].ParameterType);

            bool isMetaplayController = type.Implements<MetaplayController>(); // GetInterfaces().Contains(typeof(IMetaplayControllerInitHook));
            Microsoft.Extensions.Logging.ILogger log = null;
            SharedApiBridge apiBridge = null;

            if (isMetaplayController)
            {
                log = (Microsoft.Extensions.Logging.ILogger)_serviceProvider.GetRequiredService(typeof(ILogger<>).MakeGenericType(type));
                apiBridge = _serviceProvider.GetRequiredService<SharedApiBridge>();
            }

            return () =>
            {
                // If MetaplayController, provide it the init context via a ThreadLocal variable.
                if (isMetaplayController)
                    MetaplayControllerInitContext.Instance.Value = new MetaplayControllerInitContext(log, apiBridge);

                // Create the controller.
                object controller = ci.Invoke(arguments);

                // Cleanup the init context.
                if (isMetaplayController)
                    MetaplayControllerInitContext.Instance.Value = default;
                return controller;
            };
        }

        public void Release(ControllerContext context, object controller)
        {
            if (controller is IDisposable disposable)
                disposable.Dispose();
        }
    }

    public class AdminApiActor : EphemeralEntityActor
    {
        WebApplication _webApp;
        IHost _cdnEmulatorHost;

        protected override AutoShutdownPolicy ShutdownPolicy => AutoShutdownPolicy.ShutdownNever();

        public AdminApiActor(EntityId entityId) : base(entityId)
        {
        }

        protected override async Task Initialize()
        {
            // Start the primary web server
            _webApp = CreateWebServerApp();
            await _webApp.StartAsync();

            // Start CDN emulator server (only when running locally)
            // Note: Requires BlobStorageBackend == Disk (otherwise, S3/CloudFront should be used)
            CdnEmulatorOptions cdnOpts = RuntimeOptionsRegistry.Instance.GetCurrent<CdnEmulatorOptions>();
            if (cdnOpts.Enable)
            {
                _cdnEmulatorHost = CreateCdnEmulatorHostBuilder(cdnOpts).Build();
                await _cdnEmulatorHost.StartAsync();
            }
        }

        protected override async Task OnShutdown()
        {
            _log.Info("Stopping AdminApi HTTP server");

            // Stop the primary web server
            TimeSpan timeoutUntilGracefulShutdownBecomesForceShutdown = TimeSpan.FromSeconds(2);
            await _webApp.StopAsync(timeoutUntilGracefulShutdownBecomesForceShutdown);
            await _webApp.DisposeAsync();
            _webApp = null;

            // Stop CDN emulator
            if (_cdnEmulatorHost != null)
            {
                await _cdnEmulatorHost.StopAsync(timeout: TimeSpan.FromSeconds(2));
                _cdnEmulatorHost.Dispose();
                _cdnEmulatorHost = null;
            }
        }

        protected override void PostStop()
        {
            // Abnormal termination. Stop the host synchronously
            if (_webApp != null)
            {
                _log.Info("Stopping AdminApi HTTP server immediately");
                _webApp.StopAsync(timeout: TimeSpan.Zero).ConfigureAwait(false).GetAwaiter().GetResult();
                _webApp.DisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                _webApp = null;
            }

            // Stop CdnEmulator synchronously.
            if (_cdnEmulatorHost != null)
            {
                _log.Info("Stopping CDN emulator HTTP server immediately");
                _cdnEmulatorHost.StopAsync(timeout: TimeSpan.Zero).ConfigureAwait(false).GetAwaiter().GetResult();
                _cdnEmulatorHost.Dispose();
                _cdnEmulatorHost = null;
            }

            base.PostStop();
        }

        protected override void RegisterHandlers()
        {
            Receive<SharedApiBridge.ForwardAskToEntity>(ReceiveForwardAskToEntity);

            base.RegisterHandlers();
        }

        void ReceiveForwardAskToEntity(SharedApiBridge.ForwardAskToEntity request)
        {
            request.HandleReceive(Sender, _log, this);
        }

        void RegisterApiBridgeTypes(WebApplicationBuilder builder)
        {
            // Get all ApiBridge implementations
            HashSet<Type> candidateTypes = IntegrationRegistry.GetIntegrationClasses(typeof(SharedApiBridge)).ToHashSet();

            // Remove all dominated types from candidates, i.e., such types where another
            // candidate inherits the other type. We only want the leaf types that no other
            // type inherits from.
            foreach (Type candidate in candidateTypes.ToArray())
            {
                // Remove all base types of candidate from the candidates
                Type baseType = candidate.BaseType;
                while (baseType.IsAssignableTo(typeof(SharedApiBridge)))
                {
                    candidateTypes.Remove(baseType);
                    baseType = baseType.BaseType;
                }
            }

            // Must only have one non-dominated candidate left!
            if (candidateTypes.Count > 1)
            {
                string candidateNames = string.Join(", ", candidateTypes.Select(t => t.ToGenericTypeString()));
                throw new InvalidOperationException($"Multiple non-leaf types inheriting from SharedApiBridge: {candidateNames}");
            }

            // Instantiate the ApiBridge type
            Type apiBridgeType = candidateTypes.Single();
            _log.Information("Using ApiBridge type {ApiBridgeType}", apiBridgeType.ToGenericTypeString());
            SharedApiBridge apiBridge = (SharedApiBridge)Activator.CreateInstance(apiBridgeType, [_self]);

            // Register as all types in its hierarchy that implement SharedApiBridge (so can use any of the types in the controllers)
            Type type = apiBridgeType;
            while (type.IsAssignableTo(typeof(SharedApiBridge)))
            {
                _log.Information("Registering as {Type}", type.ToGenericTypeString());
                builder.Services.AddSingleton(type, apiBridge);
                type = type.BaseType;
            }
        }

        public WebApplication CreateWebServerApp()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();

            builder.AddAndConfigureResponseCompression();

            builder.Host.UseSerilog();

            // Use simple lifetime to not capture Ctrl-C
            builder.Services.AddSingleton<IHostLifetime>(new SimpleHostLifetime());

            // Register as all types in the ApiBridge's hierarchy so can use any of the types in the controllers
            RegisterApiBridgeTypes(builder);

            // Register custom controller activator that injects MetaplayControllers with an initialization context
            builder.Services.AddSingleton<IControllerActivator, MetaplayControllerActivator>();

            // Figure out all registered authentication domains
            AuthenticationDomainConfig[] authDomains = MetaplayServices.Get<IIntegrationRegistry>().CreateAll<AuthenticationDomainConfig>().ToArray();
            AdminApiStartup.ConfigureServices(builder.Services, authDomains);

            IAdminApiServices[] additionalServices = MetaplayServices.Get<IIntegrationRegistry>().CreateAll<IAdminApiServices>().ToArray();

            foreach (IAdminApiServices services in additionalServices)
                services.ConfigureServices(builder.Services, authDomains);

            // Build
            WebApplication webApp = builder.Build();
            webApp.UseResponseCompression();

            // Configure listen host/port
            // \note only using HTTP as HTTPS is terminated in the load balancer
            AdminApiOptions adminApiOptions = RuntimeOptionsRegistry.Instance.GetCurrent<AdminApiOptions>();
            string urls = Invariant($"http://{adminApiOptions.ListenHost}:{adminApiOptions.ListenPort}");
            _log.Info($"Binding AdminApi to listen on {urls}");
            webApp.Urls.Add(urls);

            // Configure app
            AdminApiStartup.Configure(webApp, _log, authDomains);

            return webApp;
        }

        public IHostBuilder CreateCdnEmulatorHostBuilder(CdnEmulatorOptions opts)
        {
            return Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<CdnEmulatorStartup>();

                    // Configure listen host/port
                    string urls = Invariant($"http://{opts.ListenHost}:{opts.ListenPort}");
                    _log.Info($"Binding CDN emulator to listen on {urls}");
                    webBuilder.UseUrls(urls);
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IHostLifetime>(new SimpleHostLifetime());
                });
        }
    }
}
