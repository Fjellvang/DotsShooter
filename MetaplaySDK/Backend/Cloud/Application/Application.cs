// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Akka.Actor;
using Metaplay.Cloud.Analytics;
using Metaplay.Cloud.Cluster;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Options;
using Metaplay.Cloud.Persistence;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Metaplay.Cloud.Application
{
    /// <summary>
    /// Defines an "Quick Link", which is a decorated link that appears in the sidebar of the LiveOps Dashboard.
    /// </summary>
    public class QuickLink
    {
        public string Icon  { get; private set; } = "";
        public string Title { get; private set; } = "";
        public string Uri   { get; private set; } = "";
        public string Color { get; private set; } = "";

        public QuickLink() { } // need public ctor for .NET Configuration
        public QuickLink(string icon, string title, string uri, string color)
        {
            Icon    = icon;
            Title   = title;
            Uri     = uri;
            Color   = color;
        }

        public void Validate()
        {
            if (string.IsNullOrEmpty(Title) || string.IsNullOrWhiteSpace(Uri))
                throw new InvalidOperationException($"Environment.QuickLink must specify Title and URI, got title='{Title}', Uri='{Uri}'.");
        }
    }

    [RuntimeOptions("Environment", isStatic: true, "Configuration options for the application runtime environment.")]
    public class EnvironmentOptions : RuntimeOptionsBase
    {
        // Generic
        [MetaDescription("The human readable environment name.")]
        public string                   Environment                 { get; private set; } =
            IsProductionEnvironment ? "prod" :
            IsStagingEnvironment ? "stage" :
            IsDevelopmentEnvironment ? "dev" :
            IsLocalEnvironment ? "local" :
            null;
        [MetaDescription("Enables the development features (such as cheats, in-app dev purchases, etc.) for all players.")]
        public bool                     EnableDevelopmentFeatures   { get; private set; } = IsLocalEnvironment || IsDevelopmentEnvironment;
        [MetaDescription("When persisting entities, perform a sanity check to see that they deserialize and decompress properly before writing to the database.")]
        public bool                     ExtraPersistenceChecks      { get; private set; } = IsLocalEnvironment || IsDevelopmentEnvironment;
        [MetaDescription("Enable keyboard input (only enabled locally).")]
        public bool                     EnableKeyboardInput         { get; private set; } = IsLocalEnvironment;
        [MetaDescription("List of links to relevant websites, viewable in the LiveOps Dashboard")]
        public QuickLink[]              QuickLinks                  { get; private set; }

        // Metrics
        [CommandLineAlias("-EnableMetrics")]
        [MetaDescription("Enables HTTP server which Prometheus uses to scrape the application metrics.")]
        public bool         EnableMetrics               { get; private set; } = IsServerApplication || IsCloudEnvironment; // When running locally, only enable metrics for server
        [MetaDescription("If `EnableMetrics` is enabled: The port on which to serve Prometheus metrics.")]
        public int          MetricPort                  { get; private set; } = (IsBotClientApplication && IsLocalEnvironment) ? 9091 : 9090; // Local BotClient defaults to 9091 (to avoid conflicts)
        [MetaDescription("Enables system HTTP endpoints at `/gracefulShutdown` and `/healthz`.")]
        public bool         EnableSystemHttpServer      { get; private set; } = IsCloudEnvironment;
        [MetaDescription("If `EnableSystemHttpServer` is enabled: The host on which to serve the system HTTP endpoints.")]
        public string       SystemHttpListenHost        { get; private set; } = IsCloudEnvironment ? "0.0.0.0" : "127.0.0.1";
        [MetaDescription("If `EnableSystemHttpServer` is enabled: The port on which to serve the system HTTP endpoints.")]
        public int          SystemHttpPort              { get; private set; } = 8888;

        // Lifecycle
        [CommandLineAlias("-ExitAfter")]
        [MetaDescription("Optional: The amount of time the application is allowed to run after initialization before automatically exiting. _Intended for automated testing only._")]
        public TimeSpan?    ExitAfter                   { get; private set; } = null;
        [MetaDescription("When enabled, exit the application immediately when an error event is logged. _Intended for automated testing only._")]
        public bool         ExitOnLogError              { get; private set; } = false;
        [MetaDescription("When enabled, exit the application if unknown sections or keys are specified in the runtime options `.yaml` files. _Intended for local development only._")]
        public bool         ExitOnUnknownOptions        { get; private set; } = IsLocalEnvironment;

        public override Task OnLoadedAsync()
        {
            // Default value for QuickLinks if not defined by user
            if (QuickLinks == null)
            {
                QuickLinks = new QuickLink[]
                {
                    new QuickLink(icon: null, title: "Metaplay Documentation", uri: "https://docs.metaplay.io/", color: "rgb(134, 199, 51)"),
                    new QuickLink(icon: null, title: "Metaplay Homepage", uri: "https://metaplay.io/", color: "rgb(134, 199, 51)"),
                };
            }

            foreach (QuickLink quickLink in QuickLinks)
                quickLink.Validate();

            return Task.CompletedTask;
        }
    }

    public enum LogColorTheme
    {
        None,               // No color theme
        SystemColored,      // SystemConsoleTheme.Colored
        SystemLiterate,     // SystemConsoleTheme.Literate
        AnsiCode,           // AnsiConsoleTheme.Code
        AnsiLiterate,       // AnsiConsoleTheme.Literate
    }

    public enum LogFormat
    {
        Text,   // Human-readable text format with colors, default for all builds now
        Json,   // Compact JSON format, intended for cloud use
    }

    [RuntimeOptions("Logging", isStatic: true, "Configuration options for logging.")]
    public class LoggingOptions : RuntimeOptionsBase
    {
        // Console logger
        [MetaDescription("Format to output logs in (`Text` or `Json`).")]
        public LogFormat        Format              { get; private set; } = LogFormat.Text;
        [MetaDescription("The format template for writing logs. See [Serilog](https://serilog.net/) documentation for more details.")]
        public string           FormatTemplate      { get; private set; } = "[{Timestamp:HH:mm:ss.fff} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}";
        [MetaDescription("The color theme for writing logs (`None` or `AnsiLiterate`).")]
        public LogColorTheme    ColorTheme          { get; private set; } = LogColorTheme.AnsiLiterate;
        [CommandLineAlias("-LogLevel")]
        [MetaDescription("The event logging level (`Verbose`, `Debug`, `Information`, `Warning`, or `Error`). Events below this level are not recorded.")]
        public LogLevel         Level               { get; private set; } = LogLevel.Debug;
    }

    public class ApplicationService : IHostedService
    {
        IServiceProvider             _serviceProvider;
        IApplicationServiceCallbacks _app;
        IHostApplicationLifetime     _lifetime;
        RuntimeOptionsRegistry       _runtimeOpts;
        IMetaLogger                  _logger = MetaLogger.ForContext<ApplicationService>();
        ExtendedActorSystem          _actorSystem;
        EntitySharding               _entitySharding;
        IActorRef                    _clusterCoordinator;
        IActorRef                    _clusterConnectionManager;
        bool                         _clusterDidStart;

        public ApplicationService(IServiceProvider serviceProvider, IApplicationServiceCallbacks app, IHostApplicationLifetime lifetime, RuntimeOptionsRegistry runtimeOpts)
        {
            _serviceProvider = serviceProvider;
            _app             = app;
            _lifetime        = lifetime;
            _runtimeOpts     = runtimeOpts;
        }

        public async Task StartAsync(CancellationToken ct)
        {
            // Force Metaplay services activation
            _serviceProvider.GetRequiredService<MetaplayForceInit>();

            _logger.Information("Initializing Akka.NET");
            string akkaConfig = GenerateAkkaConfig();
            _actorSystem = (ExtendedActorSystem)ActorSystem.Create(MetaplayCore.Options.ProjectName, akkaConfig);

            _serviceProvider.GetRequiredService<MetaSerialization>().RegisterActorSystem(_actorSystem);
            _app.SetActorSystem(_actorSystem);

            // Start SDK and user services
            await _app.PreClusteringStartAsync(ct).ConfigureAwait(false);

            // Start clustering & all cluster services
            // \note: Cluster may be already shutting down. In that case, we skip directly to shutdown sequence.
            try
            {
                _clusterDidStart = await TryStartClusteringAsync();
                if (!_clusterDidStart)
                {
                    _lifetime.StopApplication();
                    return;
                }
            }
            catch (TimeoutException)
            {
                await _app.PreClusteringStopAsync(ct).ConfigureAwait(false);
                throw;
            }

            // Let SDK and user know that all systems are up and running
            await _app.PostClusteringStartAsync(ct).ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken ct)
        {
            _logger.Information("Shutting down application..");

            // Cluster shutdown sequence. The coordinated shutdown may have already been initiated if the app shutdown
            // was caused by it, but the duplicate request is ok.
            ClusterCoordinatorActor.RequestNodeShutdown();

            // Wait for cluster termination. This is also done if cluster failed to start, as we may already
            // have started some entity shards.
            try
            {
                await ClusterCoordinatorActor.WaitForClusterTerminatedAsync(_clusterCoordinator, _logger, TimeSpan.FromMinutes(1));
            }
            catch (TimeoutException ex)
            {
                _logger.Error("Cluster shutdown failed: {Error}", ex);
            }

            // Stop user and SDK parts of application (in that order)
            if (_clusterDidStart)
                await _app.PreClusteringStopAsync(ct).ConfigureAwait(false);

            // Stop EntitySharding
            await StopClusteringAsync().ConfigureAwait(false);

            // Stop user and SDK services (in that order)
            await _app.PostClusteringStopAsync(ct).ConfigureAwait(false);

            // Stop Akka.NET
            await StopAkka();
        }

        /// <summary>
        /// Starts clustering, connects to other cluster nodes and waits until entity shards are running. Returns true on success.
        /// If cluster is already shutting down, returns false. If clustering cannot be started within the timeout, throws <see cref="TimeoutException"/>.
        /// </summary>
        async Task<bool> TryStartClusteringAsync()
        {
            // Start cluster coordinator
            _clusterConnectionManager = ClusterConnectionManager.CreateActor(_actorSystem);
            _entitySharding           = EntitySharding.Get(_actorSystem);
            _clusterCoordinator       = _actorSystem.ActorOf(Props.Create<ClusterCoordinatorActor>(), "cluster");

            // Wait for cluster to become ready (all nodes connected & entities initialized)
            try
            {
                await ClusterCoordinatorActor.WaitForClusterReadyAsync(_clusterCoordinator, _logger, timeout: TimeSpan.FromMinutes(5)).ConfigureAwait(false);
                _logger.Information("Clustering is now initialized");
                return true;
            }
            catch (ClusterAlreadyShuttingDownException)
            {
                // Failed to start because we are supposed to shut down.
                _logger.Warning("Cluster is shutting down while trying to join it. Shutting down.");
                return false;
            }
            catch (TimeoutException)
            {
                await StopClusteringAsync();
                throw;
            }
        }
        async Task StopClusteringAsync()
        {
            _logger.Information("Shutting down clustering services..");
            await _entitySharding.ShutdownAsync(TimeSpan.FromSeconds(5));
            await _clusterCoordinator.GracefulStop(TimeSpan.FromSeconds(5));
            await _clusterConnectionManager.GracefulStop(TimeSpan.FromSeconds(5));
        }

        string GenerateAkkaConfig()
        {
            // Clustering config
            ClusteringOptions clusterOpts = _runtimeOpts.GetCurrent<ClusteringOptions>();
            if (clusterOpts.Mode != ClusteringMode.Disabled)
                _logger.Information("Remoting local address: {SelfAddress}", clusterOpts.SelfAddress);
            else
                _logger.Information("Remoting disabled");

            StringBuilder sb = new StringBuilder(1024);
            sb.AppendLine("akka {");
            sb.AppendLine("    loglevel = DEBUG");
            sb.AppendLine("    actor.debug.unhandled = on");
            sb.AppendLine("");
            sb.AppendLine($"    loggers = [\"{typeof(MetaAkkaLogger).AssemblyQualifiedName}\"]");
            sb.AppendLine("");
            sb.AppendLine("    actor {");
            if (clusterOpts.Mode != ClusteringMode.Disabled)
            {
                sb.AppendLine("        provider = remote");
                sb.AppendLine("");
            }
            sb.AppendLine("        serializers {");
            sb.AppendLine($"            tagged = \"{typeof(AkkaTaggedSerializer).AssemblyQualifiedName}\"");
            sb.AppendLine($"            compact = \"{typeof(AkkaCompactSerializer).AssemblyQualifiedName}\"");
            sb.AppendLine($"            hyperion = \"{typeof(Akka.Serialization.HyperionSerializer).AssemblyQualifiedName}\"");
            sb.AppendLine("        }");
            sb.AppendLine("");
            sb.AppendLine("        serialization-bindings {");
            sb.AppendLine("            \"System.Object\" = hyperion");
            sb.AppendLine($"            \"{typeof(EntityShard.IRoutedMessage).AssemblyQualifiedName}\" = compact");
            sb.AppendLine($"            \"{typeof(MetaMessage).AssemblyQualifiedName}\" = compact");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    io.tcp {");
            sb.AppendLine("        batch-accept-limit = 100"); // accepted connections in parallel (to minimize wait times of socket accepts under load)
            sb.AppendLine("    }");
            sb.AppendLine("");

            if (clusterOpts.Mode != ClusteringMode.Disabled)
            {
                string remotingHost = clusterOpts.SelfAddress.HostName;
                string remotingPort = clusterOpts.SelfAddress.Port.ToString(CultureInfo.InvariantCulture);

                // \note If you change maximum-frame-size for the purpose of supporting larger PlayerModels,
                //       please consider also WireProtocol.MaxPacketUncompressedPayloadSize.
                const int remotingMaximumFrameSize = 6 * 1024 * 1024 + 1024; // 6MB, plus some extra for overheads, so that a nice round 6MB payload should still be accepted.
                const int remotingSendBufferSize = 2 * remotingMaximumFrameSize;
                const int remotingReceiveBufferSize = 2 * remotingMaximumFrameSize;

                sb.AppendLine("    remote {");
                sb.AppendLine("        log-remote-lifecycle-events = info");
                sb.AppendLine("        log-received-messages = off");
                sb.AppendLine("        log-sent-messages = off");
                sb.AppendLine("");

                // Use very short quarantine to allow nodes to recover connectivity
                // \note We must not use remote watches or remote actor deploys as they won't work with this!
                sb.AppendLine("        retry-gate-closed-for = 5s");
                sb.AppendLine("        prune-quarantine-marker-after = 1m");
                sb.AppendLine("");

                sb.AppendLine("        dot-netty.tcp {");
                sb.AppendLine("            enforce-ip-family = true");
                sb.AppendLine("            dns-use-ipv6 = false");
                sb.AppendLine("            hostname = 0.0.0.0");
                sb.AppendLine($"            public-hostname = {remotingHost}");
                sb.AppendLine($"            port = {remotingPort}");
                sb.AppendLine("");
                sb.AppendLine($"            maximum-frame-size  = {remotingMaximumFrameSize.ToString(CultureInfo.InvariantCulture)}b");
                sb.AppendLine($"            send-buffer-size    = {remotingSendBufferSize.ToString(CultureInfo.InvariantCulture)}b");
                sb.AppendLine($"            receive-buffer-size = {remotingReceiveBufferSize.ToString(CultureInfo.InvariantCulture)}b");
                sb.AppendLine("");
                sb.AppendLine("            batching {");
                sb.AppendLine("                enabled = true"); // \note use batching with default settings, Akka 1.4.14 should no longer suffer latency hits with low volumes
                //sb.AppendLine("                enabled = true");
                //sb.AppendLine("                max-pending-writes = 50");
                //sb.AppendLine("                max-pending-bytes = 16k");
                //sb.AppendLine("                flush-interval = 10ms");
                sb.AppendLine("            }");
                sb.AppendLine("            use-dispatcher-for-io = \"akka.actor.default-dispatcher\"");
                sb.AppendLine("        }");
                sb.AppendLine("");
                sb.AppendLine("        #transport-failure-detector {");
                sb.AppendLine("        #    heartbeat-interval = 4s");
                sb.AppendLine("        #    acceptable-heartbeat-pause = 20s");
                sb.AppendLine("        #}");
                sb.AppendLine("");
                sb.AppendLine("        watch-failure-detector {");
                sb.AppendLine("            heartbeat-interval = 3s");
                sb.AppendLine("        }");
                sb.AppendLine("        use-dispatcher = \"akka.actor.default-dispatcher\"");
                sb.AppendLine("    }");
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        async Task StopAkka()
        {
            // Wait a bit to allow Akka logger to process its messages
            await Task.Delay(200);

            // We don't shut down the actor system with _actorSystem.Terminate().
            //
            // That would shut down all actors in unspecified order. If the MetaLogger actor is shut down
            // before the remote transport actors have logged their exit debug stuff, we might get badly
            // formatted text in stdout.
            //
            // Instead, we just leave the akka running, and close the process. All our entities have been
            // shut down already.
            //
            // Unclean shutdown of Akka.Remote is visible to other connected nodes. However, the Terminate()
            // is unreliable with shutting down these remotes so shutting or not doesn't really change anything
            // except the likelihood of other nodes seeing unclean shutdowns. So why bother.
        }
    }

    /// <summary>
    /// Exposes the services start and stop endpoints from the Application instance to the hosted ApplicationService.
    /// </summary>
    public interface IApplicationServiceCallbacks
    {
        // TODO: get rid of this by introducing the actor system into the container
        void SetActorSystem(ExtendedActorSystem actorSystem);
        Task PreClusteringStartAsync(CancellationToken ct);
        Task PostClusteringStartAsync(CancellationToken ct);
        Task PreClusteringStopAsync(CancellationToken ct);
        Task PostClusteringStopAsync(CancellationToken ct);
    }

    /// <summary>
    /// Base class for a Metaplay server-side application. Sets up basic services like logging, Akka, Akka.Remote clustering,
    /// database access layer, Prometheus metrics, etc.
    /// </summary>
    public abstract class Application : IDisposable
    {
        /// <summary>
        /// Symbolic name of the application.
        /// <para>
        /// For game server, this should be "Server". For botclient, this should be "BotClient".
        /// </para>
        /// </summary>
        public abstract string              ApplicationSymbolicName { get; }

        public static Application Instance { get; private set; }

        static readonly Prometheus.Gauge c_buildInfoGauge = Prometheus.Metrics.CreateGauge("metaplay_build_info", "Describes the launching Metaplay Application version. Value is the launch timestamp in milliseconds.", "build_number", "commit_id", "release");
        public static readonly MetaTime ProcessStartTimestamp = MetaTime.Now;

        protected IMetaLogger _logger;

        // Set by ApplicationService on start, for backwards compatibility.
        protected ExtendedActorSystem _actorSystem;

        /// <summary>
        /// Instantly terminate program with given exit code.
        /// </summary>
        /// <param name="exitCode"></param>
        public static void ForceTerminate(int exitCode, string reason)
        {
            Console.WriteLine("Force-terminating application with exitCode {0}: {1}", exitCode, reason);

            ForceTerminateCalled = true;

            Application instance = Instance;
            if (instance != null)
                instance.ForceTerminateImpl(exitCode);
            else
                ForceTerminateDefaultImpl(exitCode);
        }

        /// <summary>
        /// Used to mark that <see cref="ForceTerminate"/> has been called.
        /// Normally this would not be needed as the process will get actually force-terminated,
        /// but in test code <see cref="ForceTerminateImpl"/> may be overridden and does not actually
        /// force-terminate the process. This flag is used in some places to stop some functionality
        /// that would otherwise keep running.
        /// </summary>
        // \todo See todo comment on ForceTerminate.
        public static bool ForceTerminateCalled { get; protected set; } = false;

        protected virtual void ForceTerminateImpl(int exitCode)
        {
            ForceTerminateDefaultImpl(exitCode);
        }

        static void ForceTerminateDefaultImpl(int exitCode)
        {
            // Sleep a bit before exiting to give logger some time to flush all messages
            Thread.Sleep(500);

            Environment.Exit(exitCode);
        }

        static bool _staticInitDone = false;

        // Initialization that is done only once per lifetime of the process. Invoked from constructor only once.
        static void StaticInit()
        {
            if (_staticInitDone)
                return;

            // Print launch info. This is the first thing application prints.
            Console.WriteLine("Launching application, build: {0}, commit id: {1}, Metaplay release {2}", CloudCoreVersion.BuildNumber, CloudCoreVersion.CommitId ?? "<not set>", DeploymentOptions.CurrentMetaplayVersion.ToString());

            // Warn if GC is not in server mode.
            if (!System.Runtime.GCSettings.IsServerGC)
                Console.WriteLine("WARNING: The garbage collector is not in server mode! You should add the following to your relevant .csproj: <ServerGarbageCollection>true</ServerGarbageCollection> AND <ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>");

            // Use InvariantCulture as default current culture.
            // We try to use explicit CultureInfo/IFormatProvider arguments instead of relying on current culture.
            // However, some usage of current culture may accidentally remain, so this is used as a best-effort failsafe.
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

            // Core static init
            MetaplayCore.StaticInit();

            // Register pretty printers
            PrettyPrinter.RegisterFormatter<IActorRef>((actor, isCompact) => actor.ToString());

            // Force-load local assemblies so they are available through reflection
            _ = LoadLocalAssemblies();

            // Sanity-check the application is running on a supported platform.
            // (Some libraries such as ironcompress ship arch-specific binary blobs)
            HashSet<Architecture> supportedArchs = new HashSet<Architecture>
            {
                Architecture.X64,   // amd64
                Architecture.Arm64,
            };
            if (!supportedArchs.Contains(RuntimeInformation.ProcessArchitecture))
                throw new Exception($"The current architecture {RuntimeInformation.ProcessArchitecture} is not supported. Only AMD64 or ARM64 are supported. Make sure you are using a 64-bit runtime.");

            PreInitLogger();

            _staticInitDone = true;
        }

        protected Application()
        {
            // Register instance
            if (Instance != null)
                throw new InvalidOperationException($"Duplicate Application initialization");
            StaticInit();
            Instance = this;
            _logger = MetaLogger.ForContext<Application>();
        }

        public static Assembly[] LoadLocalAssemblies()
        {
            HashSet<string> processedSet = new HashSet<string>();
            List<Assembly> localAssemblies = new List<Assembly>() { Assembly.GetEntryAssembly() };

            Queue<Assembly> queue = new Queue<Assembly>();
            queue.Enqueue(Assembly.GetEntryAssembly());

            while (queue.TryDequeue(out Assembly assembly))
            {
                foreach (AssemblyName referencedAssembly in assembly.GetReferencedAssemblies())
                {
                    // Skip ignored assemblies
                    if (TypeScanner.ShouldIgnoreAssembly(referencedAssembly.FullName))
                        continue;

                    // Skip already processed assemblies
                    if (processedSet.Contains(referencedAssembly.FullName))
                        continue;
                    processedSet.Add(referencedAssembly.FullName);

                    Assembly loaded = Assembly.Load(referencedAssembly);
                    queue.Enqueue(loaded);
                    localAssemblies.Add(loaded);
                }
            }

            return localAssemblies.ToArray();
        }

        static ConsoleTheme MapLogColorTheme(LogColorTheme theme)
        {
            switch (theme)
            {
                case LogColorTheme.None:            return ConsoleTheme.None;
                case LogColorTheme.SystemColored:   return SystemConsoleTheme.Colored;
                case LogColorTheme.SystemLiterate:  return SystemConsoleTheme.Literate;
                case LogColorTheme.AnsiCode:        return AnsiConsoleTheme.Code;
                case LogColorTheme.AnsiLiterate:    return AnsiConsoleTheme.Literate;

                default:
                    throw new ArgumentException($"Invalid LogColorTheme: {theme}");
            }
        }

        /// <summary>
        /// Pre-initialize Serilog with a simple console logger. Used during app init before the
        /// proper logger configuration is known.
        /// </summary>
        static void PreInitLogger()
        {
            LoggerConfiguration config = new LoggerConfiguration();
            config.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture);
            // Note: no access to runtime options at this stage, so OnErrorTerminate can't be utilized
            //config.WriteTo.Sink(new MetaplayOnErrorTerminateSink(), restrictedToMinimumLevel: LogEventLevel.Error);
            config.WriteTo.Sink(new MetaplayOnLogEventTrackSink(), restrictedToMinimumLevel: LogEventLevel.Error);
            Serilog.Log.Logger = config.CreateLogger();
        }

        static void ConfigureLogger(IServiceProvider provider, LoggerConfiguration config)
        {
            LoggingOptions logOpts = provider.GetRequiredService<LoggingOptions>();

            // Initialize logging level switch ??
            MetaLogger.SetLoggingLevel(logOpts.Level);

            // Initialize Serilog logger
            config
                .MinimumLevel.ControlledBy(MetaLogger.SerilogLogLevelSwitch)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)                                                   // make ASP.NET a bit less noisy
                .MinimumLevel.Override("Microsoft.AspNetCore.DataProtection.Repositories.FileSystemXmlRepository", LogEventLevel.Error) // silence DataProtection warning -- we don't use it
                .MinimumLevel.Override("Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager", LogEventLevel.Error)          // silence DataProtection warning -- we don't use it
                .MinimumLevel.Override("Metaplay.Server.AdminApi.AnonymousAuthenticationHandler", LogEventLevel.Information)            // silence noisy auth success events
                .MinimumLevel.Override("Metaplay.Cloud.Application.MetaplaySystemHttpServer", LogEventLevel.Information);               // silence noisy health check ok events

            // Configure log format: text or json
            switch (logOpts.Format)
            {
                case LogFormat.Text:
                    config.WriteTo.Console(
                        outputTemplate: logOpts.FormatTemplate,
                        formatProvider: CultureInfo.InvariantCulture,
                        theme:          MapLogColorTheme(logOpts.ColorTheme));
                    break;

                case LogFormat.Json:
                    config.WriteTo.Console(new CompactJsonFormatter());
                    break;

                default:
                    throw new InvalidOperationException($"Invalid LogFormat {logOpts.Format}, expecting 'Text' or 'Json'");
            }

            config.WriteTo.Sink(new MetaplayOnErrorTerminateSink(provider.GetRequiredService<RuntimeOptionsRegistry>()), restrictedToMinimumLevel: LogEventLevel.Error);
            config.WriteTo.Sink(new MetaplayOnLogEventTrackSink(), restrictedToMinimumLevel: LogEventLevel.Error);
        }


        #region Lifecycle hooks for user code (e.g. ServerMain)

        /// <summary>
        /// Optional user hook to start custom pre-clustering services.
        /// The services should be stopped in <see cref="StopServicesAsync"/>, if needed.
        /// <para>
        /// This is called before clustering is started.
        /// </para>
        /// </summary>
        /// <remarks>
        /// The various user hooks are called in the following sequence. <br/>
        /// 1. <see cref="StartServicesAsync"/> is called.<br/>
        /// 2. Clustering and entity sharding are started.<br/>
        /// 3. <see cref="StartApplicationAsync"/> is called.<br/>
        /// 4. Server is now running, and continues to run until it decides to shut down for whatever reason (e.g. being explicitly requested to shut down).<br/>
        /// 5. <see cref="StopApplicationAsync"/> is called.<br/>
        /// 6. Clustering and entity sharding are stopped.<br/>
        /// 7. <see cref="StopServicesAsync"/> is called.<br/>
        /// </remarks>
        protected virtual Task StartServicesAsync() => Task.CompletedTask;
        /// <summary>
        /// Optional user hook to stop custom pre-clustering services.
        /// Should stop services that were started in <see cref="StartServicesAsync"/>, if needed.
        /// <para>
        /// This is called after clustering has been stopped.
        /// </para>
        /// </summary>
        /// <remarks>
        /// <inheritdoc cref="StartServicesAsync" />
        /// </remarks>
        protected virtual Task StopServicesAsync() => Task.CompletedTask;

        /// <summary>
        /// Optional user hook to start custom post-clustering services.
        /// The services should be stopped in <see cref="StopApplicationAsync"/>, if needed.
        /// <para>
        /// This is called after clustering has been started.
        /// </para>
        /// </summary>
        /// <remarks>
        /// <inheritdoc cref="StartServicesAsync" />
        /// </remarks>
        protected virtual Task StartApplicationAsync() => Task.CompletedTask;
        /// <summary>
        /// Optional user hook to stop custom post-clustering services.
        /// Should stop services that were started in <see cref="StartApplicationAsync"/>, if needed.
        /// <para>
        /// This is called before clustering is stopped.
        /// </para>
        /// </summary>
        /// <remarks>
        /// <inheritdoc cref="StartServicesAsync" />
        /// </remarks>
        protected virtual Task StopApplicationAsync() => Task.CompletedTask;

        #endregion

        #region Lifecycle hooks for SDK (e.g. ServerMainBase)

        protected abstract Task StartCoreServicesAsync();
        protected abstract Task StopCoreServicesAsync();
        protected abstract Task StartCoreApplicationAsync();
        protected abstract Task StopCoreApplicationAsync();

        #endregion

        class ApplicationServiceCallbacks : IApplicationServiceCallbacks
        {
            readonly Application _app;

            public ApplicationServiceCallbacks(Application app)
            {
                _app = app;
            }

            public void SetActorSystem(ExtendedActorSystem actorSystem)
            {
                _app._actorSystem = actorSystem;
            }

            public async Task PreClusteringStartAsync(CancellationToken ct)
            {
                // replace logger with a properly configured one (should get this from the DI container)
                _app._logger = MetaLogger.ForContext<Application>();

                await _app.StartCoreServicesAsync();
                await _app.StartServicesAsync();
            }

            public async Task PostClusteringStartAsync(CancellationToken ct)
            {
                await _app.StartCoreApplicationAsync();
                await _app.StartApplicationAsync();
            }

            public async Task PreClusteringStopAsync(CancellationToken ct)
            {
                await _app.StopApplicationAsync();
                await _app.StopCoreApplicationAsync();
            }

            public async Task PostClusteringStopAsync(CancellationToken ct)
            {
                await _app.StopServicesAsync();
                await _app.StopCoreServicesAsync();
            }
        }

        protected virtual void HandleKeyPress(ConsoleKeyInfo key)
        {
            // nada
        }

        protected virtual void AddServices(IServiceCollection services) { }

        // Note: this is planned to be externalized from Application later
        static IHost BuildApplicationHost(Application app, string[] cmdLineArgs)
        {
            IHostBuilder builder = Host.CreateDefaultBuilder();

            RuntimeEnvironmentInfo envInfo = new RuntimeEnvironmentInfo(app.ApplicationSymbolicName);
            RuntimeOptionsRegistry runtimeOptionsRegistry = new RuntimeOptionsRegistry(envInfo, cmdLineArgs);

            // Read startup options
            List<RuntimeOptionsBase> startupOptions = runtimeOptionsRegistry.ReadStartupOptions(
            [
                typeof(EnvironmentOptions),
                typeof(LoggingOptions),
            ]).ToList();

            EnvironmentOptions envOptions = startupOptions.OfType<EnvironmentOptions>().Single();
            LoggingOptions loggingOptions = startupOptions.OfType<LoggingOptions>().Single();

            builder.ConfigureServices(
                (context, services) =>
                {
                    // Logging
                    services.AddSerilog(ConfigureLogger);

                    // MetaplayCore
                    services.AddMetaplayCore(envInfo);

                    // Add pre-created instances
                    services.AddSingleton(envInfo);
                    services.AddSingleton(envOptions);
                    services.AddSingleton(loggingOptions);
                    services.AddSingleton(runtimeOptionsRegistry);

                    // System http server (start this early, so can start responding to k8s health checks).
                    if (envOptions.EnableSystemHttpServer)
                        services.AddHostedService<MetaplaySystemHttpServer>();
                    // Metrics server
                    if (envOptions.EnableMetrics)
                        services.AddHostedService<MetaplayMetricsHttpServer>();

                    // Metaplay cloud services
                    services.AddHostedService(provider => provider.GetRequiredService<RuntimeOptionsRegistry>());
                    services.AddStartupSingleton<BigQueryFormatter>();
                    services.AddStartupSingleton<BigQueryFormatterV2>();
                    services.AddStartupSingleton<EntityConfigRegistry>(early: true); // required by runtime options
                    services.AddStartupSingleton<EntityConfiguration>();
                    services.AddStartupSingleton<Metrics.RuntimeCollector>();

                    // Add ApplicationService as a hosted service and expose the service start&stop functions of this
                    // application instance to it via IApplicationServiceCallbacks.
                    services.AddSingleton<IApplicationServiceCallbacks>(new ApplicationServiceCallbacks(app));
                    services.AddHostedService<ApplicationService>();

                    // Using manual host lifetime. Add MetaplayForceInitEarly as a dependency to host lifetime to
                    // force the early initialization happen already during the host build process.
                    services.AddSingleton<IHostLifetime>(
                        provider =>
                        {
                            provider.GetRequiredService<MetaplayForceInitEarly>();
                            return new SimpleHostLifetime();
                        });

                    // Add custom services from derived classes
                    app.AddServices(services);
            });

            return builder.Build();
        }

        /// <summary>
        /// Prepares the application to be run from a container image. This is run when container is built (in dockerfile),
        /// and any saved files will then be available in the final container image. Failure here will abort the container build.
        /// The main use is to pre-generate the serializer DLL and store it in the container image.
        /// </summary>
        int BakeContainerImage()
        {
            // Validate that the serializer DLL was built by initialization. It gets cached onto the filesystem automatically.
            MetaplayServices.Get<TaggedSerializerRoslyn>();

            // Application-specific validations
            OnBakeContainerImage();
            return 0;
        }

        /// <summary>
        /// Prepares the application to be run from a container image. This is run when container is built (in dockerfile),
        /// and any saved files will then be available in the final container image. Failure here will abort the container build.
        /// Can be used to validate static properties of the application, and fail build if a violation is found.
        /// </summary>
        protected virtual void OnBakeContainerImage() { }

        static int ValidateRuntimeOptionsAsync(IEnumerable<string> filePatterns)
        {
            Matcher matcher = new();
            matcher.AddIncludePatterns(filePatterns);
            PatternMatchingResult result = matcher.Execute(
                new DirectoryInfoWrapper(new DirectoryInfo(".")));
            string[] paths = result.Files.Select(x => x.Path).ToArray();
            return RuntimeOptionsRegistry.TryValidateOptionsFiles(paths) ? 0 : 1;
        }

        async Task<int> RunApplicationAsync(IHost host)
        {
            EnvironmentOptions envOptions = host.Services.GetRequiredService<EnvironmentOptions>();

            // Push version info into metrics. The value of the gauge is the timestamp to allow detecting both same version launching again and version changes.
            c_buildInfoGauge.WithLabels(CloudCoreVersion.BuildNumber, CloudCoreVersion.CommitId ?? "null", DeploymentOptions.CurrentMetaplayVersion.ToString()).Set((double)ProcessStartTimestamp.MillisecondsSinceEpoch);

            // Create termination handler early on to allow termination during startup.
            IHostApplicationLifetime applicationLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            using ApplicationTerminationHandler terminationHandler = new ApplicationTerminationHandler(applicationLifetime, _logger, envOptions.EnableKeyboardInput);

            // Start hosted services

            await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
            ClusteringOptions clusterOpts = host.Services.GetRequiredService<RuntimeOptionsRegistry>().GetCurrent<ClusteringOptions>();

            if (!applicationLifetime.ApplicationStopping.IsCancellationRequested)
            {
                // Force GC before going into event loop
                GC.Collect(2, GCCollectionMode.Forced);

                // Print server start time
                long serverStartElapsedMilliseconds = (MetaTime.Now - ProcessStartTimestamp).Milliseconds;
                _logger.Information("Server is now ready to serve, {StartElapsed}ms elapsed since process start!", serverStartElapsedMilliseconds);
                _logger.Information("Cluster self address: {NodeAddress}", clusterOpts.SelfAddress);

                // Check if application should exit after specified time
                DateTime? exitAt = null;
                if (envOptions.ExitAfter.HasValue)
                    exitAt = DateTime.UtcNow + envOptions.ExitAfter.Value;

                // Main interactive application event loop.
                for (;;)
                {
                    try
                    {
                        // Wait a bit to not starve CPU
                        await Task.Delay(100, applicationLifetime.ApplicationStopping).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        // Application stopped, break out of event loop.
                        break;
                    }

                    // Exit based on ExitAfter duration
                    if (exitAt != null && DateTime.UtcNow >= exitAt.Value)
                    {
                        Console.WriteLine("Exiting automatically after running for the duration specified with ExitAfter..");
                        exitAt = null; // prevent invoking multiple times
                        applicationLifetime.StopApplication();
                    }

                    // Poll key presses (if enabled)
                    if (envOptions.EnableKeyboardInput)
                        PollKeyboardInput(applicationLifetime);

                    // Cluster shutdown
                    if (ClusterCoordinatorActor.Phase == ClusterPhase.Terminated)
                        applicationLifetime.StopApplication();
                }
            }

            await host.StopAsync().ConfigureAwait(false);

            // Check if we're allowed to terminate:
            //
            // In Kubernetes, exiting the process before receiving SIGTERM would cause the pod to get restarted.
            // If this is a coordinated shutdown (where any node in the deployment has received the coordinated-shutdown hook),
            // it signals that the whole deployment is going down. We don't want to restart a node if the deployment is being
            // shutdown. We wait here until a SIGTERM is received.
            //
            // If running outside Kubernetes (locally or if clustering is disabled), we don't need to wait for SIGTERM and can
            // exit immediately.
            if (ClusterCoordinatorActor.IsCoordinatedShutdown && clusterOpts.Mode == ClusteringMode.Kubernetes)
            {
                _logger.Information("Waiting for allowProcessTerminate (SIGTERM)..");
                while (!terminationHandler.Terminated.Wait(1000))
                    _logger.Information("Still waiting for allowProcessTerminate (SIGTERM)..");
            }

            _logger.Information("Application shutdown complete, exiting");
            return 0;
        }

        /// <summary>
        /// Initializes and runs the application. Returns Exit code.
        /// </summary>
        protected async Task<int> RunApplicationMainAsync(string[] cmdLineArgs)
        {
            if (cmdLineArgs.Length == 1 && cmdLineArgs[0] == "--MetaplayBakeForContainer")
            {
                Console.WriteLine("Baking serializer for container image");
                return RunSingleCommand(BakeContainerImage);
            }

            if (cmdLineArgs.Length > 0 && cmdLineArgs[0] == "--MetaplayValidateRuntimeOptions")
            {
                if (cmdLineArgs.Length == 1)
                    throw new InvalidOperationException("--MetaplayValidateRuntimeOptions called without path arguments!");

                return RunSingleCommand(() => ValidateRuntimeOptionsAsync(cmdLineArgs[1..]));
            }

            using IHost host = BuildApplicationHost(this, cmdLineArgs);
            return await RunApplicationAsync(host);
        }

        protected int RunSingleCommand(Func<int> command)
        {
            #pragma warning disable VSTHRD002
            return RunSingleCommandAsync(() => Task.FromResult(command())).Result;
            #pragma warning restore VSTHRD002
        }

        protected async Task<int> RunSingleCommandAsync(Func<Task<int>> command)
        {
            // Silence logging for single-shot commands
            MetaLogger.SetLoggingLevel(LogLevel.Warning);

            using IHost host = BuildApplicationHost(this, ["-LogLevel", "Warning"]);
            // Note: hosted services are not started here, access to host services is provided only through the
            // MetaplayServices locator.
            return await command();
        }

        void PollKeyboardInput(IHostApplicationLifetime lifetime)
        {
            // \todo [petri] If input is redirected (running under test suite), this operation blocks!
            if (!TryGetConsoleKey(out ConsoleKeyInfo key))
                return;

            // Key shortcuts:
            // Q/ESC -- shut cluster down gracefully
            // E,W,I,D -- set log level to error/warning/info/debug
            // O -- print runtime options
            switch (key.Key)
            {
                case ConsoleKey.Q:
                case ConsoleKey.Escape:
                    Console.WriteLine("Received keypress requesting to gracefully shutdown..");
                    // Request whole cluster shutdown here. Not sure if only stopping this node would make more sense.
                    ClusterCoordinatorActor.RequestClusterShutdown();
                    lifetime.StopApplication();
                    break;

                case ConsoleKey.G:
                    Console.WriteLine("Forcing garbage collect (all generations)");
                    GC.Collect();
                    break;

                case ConsoleKey.E:
                    Console.WriteLine("Log level set to: ERROR");
                    MetaLogger.SetLoggingLevel(LogLevel.Error);
                    break;

                case ConsoleKey.W:
                    Console.WriteLine("Log level set to: WARNING");
                    MetaLogger.SetLoggingLevel(LogLevel.Warning);
                    break;

                case ConsoleKey.I:
                    Console.WriteLine("Log level set to: INFO");
                    MetaLogger.SetLoggingLevel(LogLevel.Information);
                    break;

                case ConsoleKey.D:
                    Console.WriteLine("Log level set to: DEBUG");
                    MetaLogger.SetLoggingLevel(LogLevel.Debug);
                    break;

                case ConsoleKey.V:
                    Console.WriteLine("Log level set to: VERBOSE");
                    MetaLogger.SetLoggingLevel(LogLevel.Verbose);
                    break;

                case ConsoleKey.O:
                    RuntimeOptionsRegistry.Instance.Print();
                    break;

                default:
                    HandleKeyPress(key);
                    break;
            }
        }

        static bool TryGetConsoleKey(out ConsoleKeyInfo key)
        {
            // Handle redirected input (used from nested process run under Unity or tests)
            if (Console.IsInputRedirected)
            {
                try
                {
                    // Check for EOF
                    // \note Blocking operation!
                    if (Console.In.Peek() == -1)
                    {
                        key = default;
                        return false;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // On Linux or Posix if there is no input stream, we get "Bad file descriptor" IOException that gets wrapped
                    // as UnauthorizedAccessException. Ignore.
                    key = default;
                    return false;
                }

                int keyChar = Console.In.Read();
                ConsoleKey consoleKey = ConsoleKey.Escape;

                if (Enum.TryParse<ConsoleKey>(keyChar.ToString(CultureInfo.InvariantCulture).ToUpperInvariant(), out ConsoleKey ck))
                    consoleKey = ck;

                key = new ConsoleKeyInfo((char)keyChar, consoleKey, shift: false, alt: false, control: false);
                return true;
            }

            // Regular console input
            if (!Console.KeyAvailable)
            {
                key = default;
                return false;
            }

            key = Console.ReadKey();
            return true;
        }

        public void Dispose()
        {
            Instance = null;
        }
    }
}
