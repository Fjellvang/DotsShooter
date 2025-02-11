// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Activables;
using Metaplay.Core.Analytics;
using Metaplay.Core.Client;
using Metaplay.Core.Config;
using Metaplay.Core.LiveOpsEvent;
using Metaplay.Core.Localization;
using Metaplay.Core.Math;
using Metaplay.Core.Message;
using Metaplay.Core.Model;
using Metaplay.Core.Network;
using Metaplay.Core.Profiling;
using Metaplay.Core.Serialization;
using Metaplay.Core.Tasks;
using Metaplay.Core.Web3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using static System.FormattableString;

[assembly: InternalsVisibleTo("Metaplay.Cloud.Tests")]
[assembly: InternalsVisibleTo("Metaplay.Cloud.Serialization.Compilation.Tests")]
[assembly: InternalsVisibleTo("Cloud.Tests")]
[assembly: InternalsVisibleTo("Metaplay.Core.Tests")]

[assembly: InternalsVisibleTo("Metaplay.Server")]
[assembly: InternalsVisibleTo("Metaplay.Server.Tests")]

namespace Metaplay.Core
{
    /// <summary>
    /// Flags to specify which optional Metaplay SDK features should be enabled in your project.
    /// </summary>
    public class MetaplayFeatureFlags
    {
        /// <summary>
        /// Enable Metaplay-powered localizations in the game.
        /// </summary>
        public bool EnableLocalizations { get; set; }

#if !METAPLAY_DISABLE_GUILDS
        /// <summary>
        /// Enable Metaplay-powered guilds in the game.
        /// </summary>
        public bool EnableGuilds { get; set; }
#endif

        /// <summary>
        /// Enable Metaplay's Web3 features in the game.
        /// </summary>
        public bool EnableWeb3 { get; set; }

        /// <summary>
        /// Enable building ImmutableX Link library on Client. Only applies to WebGL builds.
        /// </summary>
        public bool EnableImmutableXLinkClientLibrary { get; set; }

        /// <summary>
        /// Enable player leagues system in the game.
        /// </summary>
        public bool EnablePlayerLeagues { get; set; }

        /// <summary>
        /// Enable Company Account support on Client.
        /// <para>
        /// Logging in into a Company Account requires the use of a platform native browser.
        /// </para>
        /// <para>
        /// On iOS, enabling this injects <c>AuthenticationServices.framework</c> dependency automatically into the app. This dependency brings in ASWebAuthenticationSession
        /// which allows opening a in-app browser for authentication purposes. Company Account client integration then uses it automatically.
        /// </para>
        /// <para>
        /// On Android, we want to inject <c>androidx.browser:browser</c> dependency for an in-app browser but we cannot do it directly. Instead, in addition to this flag, you
        /// need to add io.metaplay.unitysdk.androidcustomtabs (MetaplaySDK/Plugins/AndroidCustomTabs/Package) package into the Unity project using Unity's Package Manager. See
        /// MetaplaySDK/Plugins/AndroidCustomTabs/README.md for details.
        /// </para>
        /// </summary>
        public bool EnableCompanyAccountClientIntegrations { get; set; }
    }

    public class MissingIntegrationException : Exception
    {
        public MissingIntegrationException(string msg): base(msg) { }
    }

    /// <summary>
    /// Game-specific constant options for core Metaplay SDK.
    /// </summary>
    public class MetaplayCoreOptions
    {
        public static readonly Regex ProjectNameRegex = new Regex(@"^[a-zA-Z0-9_]+$"); // Allow characters, numbers, and underscores.

        /// <summary>
        /// Technical name of the project. Must only contain letters, numbers, and underscores.
        /// </summary>
        public readonly string ProjectName;

        /// <summary>
        /// Magic identifier to ensure that only client and server of the same game talk to each other.
        /// Must be exactly 4 characters long.
        /// </summary>
        public readonly string GameMagic;

#if NETCOREAPP
        /// <summary>
        /// Supported range of LogicVersions (inclusive min and max) for this build. Applies only on the server.
        /// This value is hidden in Unity builds to prevent accidental usage.
        /// </summary>
        public readonly MetaVersionRange SupportedLogicVersions;
        /// <summary>
        /// Available options for setting active min and max version within the supported logic version range.
        /// This is to allow non-continuous versioning schemes. SupportedLogicVersions.MinVersion and
        /// SupportedLogicVersions.MaxVersion must be among the list.
        /// </summary>
        /// This option is optional, when not given the options will be deduced from the SupportedLogicVersions range.
        public readonly List<int> SupportedLogicVersionOptions;
#endif
        /// <summary>
        /// The client LogicVersion used in the current build. Applies only on the client.
        /// The server should not be using this value, except for the bot client.
        /// </summary>
        public readonly ClientVersion ClientVersion;
        public int ClientLogicVersion => ClientVersion.LogicVersion;

        /// <summary>
        /// Public salt used for guild invite code. Game-specific. Not a secret. Magic value to early detect
        /// and reject invalid invite codes.
        /// </summary>
        public readonly byte GuildInviteCodeSalt;

        /// <summary>
        /// List of all namespaces which are shared between the client and the server (and therefore part
        /// of the protocol between client and server).
        /// </summary>
        public readonly string[] SharedNamespaces;

        /// <summary>
        /// Default language of the application.
        /// </summary>
        public readonly LanguageId DefaultLanguage;

        /// <summary>
        /// Flags for enabling/disabling optional Metaplay SDK features.
        /// </summary>
        public readonly MetaplayFeatureFlags FeatureFlags;

        public MetaplayCoreOptions(
            string projectName,
            MetaVersionRange supportedLogicVersions,
            int clientLogicVersion,
            byte guildInviteCodeSalt,
            string[] sharedNamespaces,
            LanguageId defaultLanguage,
            MetaplayFeatureFlags featureFlags,
            string gameMagic = WireProtocol.CommonGameMagic,
            List<int> supportedLogicVersionOptions = null,
            int clientPatchVersion = 0)
        {
            if (projectName == null)
                throw new ArgumentNullException(nameof(projectName));
            if (projectName == "")
                throw new ArgumentException("ProjectName must be non-empty!", nameof(projectName));
            if (!ProjectNameRegex.IsMatch(projectName))
                throw new ArgumentException("ProjectName must only contain letters, numbers, and underscores!", nameof(projectName));

            if (gameMagic == null)
                throw new ArgumentNullException(nameof(gameMagic));
            if (gameMagic.Length != 4)
                throw new ArgumentException("GameMagic must be exactly 4 characters long", nameof(gameMagic));

            if (defaultLanguage == null)
            {
                if (featureFlags.EnableLocalizations)
                    throw new ArgumentException("Default language must be set when Localizations are enabled.", nameof(defaultLanguage));
                else
                    throw new ArgumentException("Default language must be set. For example LanguageId.FromString(\"en\").", nameof(defaultLanguage));
            }

            if (supportedLogicVersions == null)
                throw new ArgumentNullException(nameof(supportedLogicVersions));

            if (supportedLogicVersions.MinVersion <= 0)
                throw new ArgumentException("Logic version less or equal to 0 is not allowed", nameof(supportedLogicVersions));
            if (supportedLogicVersions.MinVersion > supportedLogicVersions.MaxVersion)
                throw new ArgumentException("Logic version max must be greater or equal to min", nameof(supportedLogicVersions));

            if (supportedLogicVersionOptions == null)
            {
                supportedLogicVersionOptions = supportedLogicVersions.Enumerate.ToList();
            }
            else
            {
                supportedLogicVersionOptions.Sort();
                if (supportedLogicVersionOptions.First() != supportedLogicVersions.MinVersion)
                    throw new ArgumentException("Minimum supported logic version option must equal to supportedLogicVersions.MinVersion", nameof(supportedLogicVersionOptions));
                if (supportedLogicVersionOptions.Last() != supportedLogicVersions.MaxVersion)
                    throw new ArgumentException("Maximum supported logic version option must equal to supportedLogicVersions.MaxVersion", nameof(supportedLogicVersionOptions));
            }

            if (clientLogicVersion < supportedLogicVersions.MinVersion || clientLogicVersion > supportedLogicVersions.MaxVersion)
                throw new ArgumentException(Invariant($"Client LogicVersion ({clientLogicVersion}) must be within supported LogicVersion range ({supportedLogicVersions})."), nameof(clientLogicVersion));
            if (!supportedLogicVersionOptions.Contains(clientLogicVersion))
                throw new ArgumentException(Invariant($"Client LogicVersion ({clientLogicVersion}) must be found within the supported logic version options."), nameof(clientLogicVersion));

            ProjectName = projectName;
            GameMagic   = gameMagic;
#if NETCOREAPP
            SupportedLogicVersions       = supportedLogicVersions;
            SupportedLogicVersionOptions = supportedLogicVersionOptions;
#endif
            ClientVersion          = new ClientVersion(clientLogicVersion, clientPatchVersion);
            GuildInviteCodeSalt    = guildInviteCodeSalt;
            SharedNamespaces       = sharedNamespaces ?? throw new ArgumentNullException(nameof(sharedNamespaces));
            DefaultLanguage        = defaultLanguage;
            FeatureFlags           = featureFlags;
        }
    }

    /// <summary>
    /// The game integration API for providing Metaplay core options. An implementation of this interface
    /// is expected to be found in the shared game code for providing mandatory Metaplay SDK options.
    /// </summary>
    public interface IMetaplayCoreOptionsProvider : IMetaIntegrationSingleton<IMetaplayCoreOptionsProvider>
    {
        MetaplayCoreOptions Options { get; }
    }

    public static class MetaplayCore
    {
        public static bool IsInitialized => MetaplayServices.IsInitialized;
        static Exception _initializeFailedException = null;
        public static MetaplayCoreOptions Options => MetaplayServices.TryGet(out MetaplayCoreOptions opts) ? opts : throw new InvalidOperationException($"{nameof(MetaplayCore)}.{nameof(Initialize)} must be called before accessing {nameof(MetaplayCore)}.{nameof(Options)}");

        /// <summary>
        /// The current login protocol version for Metaplay SDK.
        /// Applies to both client and server.
        /// </summary>
        public const int LoginProtocolVersion = 2;

        /// <summary>
        /// Adds Metaplay core services into the `initializers` service collection. This is internally used by the
        /// MetaplayCore initialization functions, but can also be used directly to use Metaplay core within an
        /// external container. For using with the standard .net service collection, see the wrapper implementation
        /// in ServiceWrapper.cs.
        /// </summary>
        internal static IServiceInitializers AddMetaplayCore(this IServiceInitializers initializers)
        {
            // Initialize integrations, accept only Metaplay API
            initializers.Add<IIntegrationRegistry>(() => new IntegrationRegistry(type => type.Namespace?.StartsWith("Metaplay.", StringComparison.Ordinal) ?? false));
            // Cache MetaplayCoreOptions as a separate "service" and provide a more helpful error when
            // integration is missing.
            initializers.Add(services =>
            {
                try
                {
                    return services.Get<IIntegrationRegistry>().Get<IMetaplayCoreOptionsProvider>().Options;
                }
                catch (InvalidOperationException)
                {
                    throw new MissingIntegrationException(
                        $"No implementation of {nameof(IMetaplayCoreOptionsProvider)} found. " +
                        $"This usually means that the Metaplay integration to the game is missing or undiscoverable.");
                }
            });
            // Various registry services
            initializers.Add<EntityKindRegistry>();
            initializers.Add<SchemaMigrationRegistry>();
            initializers.Add<GameConfigRepository>();
            initializers.Add(services => new ConfigParser(services.Get<IIntegrationRegistry>(), services.Get<GameConfigRepository>()));
            initializers.Add(services => new MetaSerializerTypeRegistry(services.Get<MetaSerializerTypeInfo>()));
            initializers.Add(services => new MetaSerialization(services.Get<MetaSerializerTypeRegistry>(), services.Get<TaggedSerializerRoslyn>()));
            initializers.Add(services => NftTypeRegistry.Create(services.Get<MetaplayCoreOptions>()));
            initializers.Add<ModelActionRepository>();
            initializers.Add<MetaMessageRepository>();
            initializers.Add(services => new MetaActivableRepository(services.Get<GameConfigRepository>()));
            initializers.Add<LiveOpsEventTypeRegistry>();
            initializers.Add<AnalyticsEventRegistry>();
            initializers.Add(services => new FirebaseAnalyticsFormatter(services.Get<AnalyticsEventRegistry>()));
            initializers.Add(
                services =>
                {
                    IEnvironmentConfigProvider provider = services.Get<IIntegrationRegistry>().Get<IEnvironmentConfigProvider>();
                    provider.InitializeSingleton();
                    return provider;
                });
            // Note: services TaggedSerializerRoslyn and MetaSerializationTypeInfo not configured here.
            return initializers;
        }

        static bool _staticInitDone = false;
        public static void StaticInit()
        {
            if (_staticInitDone)
                return;

            // Initialize MetaTask
            MetaTask.Initialize();

            // Register PrettyPrinter for various types
            PrettyPrinter.RegisterFormatter<IntVector2>((v, isCompact) => Invariant($"({v.X}, {v.Y})"));
            PrettyPrinter.RegisterFormatter<IntVector3>((v, isCompact) => Invariant($"({v.X}, {v.Y}, {v.Z})"));

            _staticInitDone = true;
        }

        public static void InitializeForClient(ClientPlatform clientPlatform)
        {
            string generatedAssemblyName = $"Metaplay.Generated.{clientPlatform}";
            Initialize(
                initializers =>
                {
                    initializers.Add(_ => TaggedSerializerRoslyn.CreateFromAssemblyByName(generatedAssemblyName, false));
                    initializers.Add(services => new RuntimeTypeInfoProvider(services.Get<TaggedSerializerRoslyn>()).GetTypeInfo());
                });
        }

        public static IMetaplayServiceProvider Reinitialize(Action<IServiceInitializers> configureServices)
        {
            using (ProfilerScope.Create("MetaplayCore.Initialize()"))
            {
                StaticInit();

                // Configure Metaplay core services. The provided callback can be used to add additional services as well
                // as replace default services with different versions or different configuration.
                SimpleServiceInitializers initializers = new SimpleServiceInitializers();
                initializers.AddMetaplayCore();
                configureServices?.Invoke(initializers);

                // Create the service provider (i.e. the simple dependency injection container used by non-server code)
                SimpleMetaplayServiceProvider provider = new SimpleMetaplayServiceProvider(initializers);
                // Set current MetaplayServices provider early so that legacy init code can access dependencies.
                IMetaplayServiceProvider oldProvider = MetaplayServices.SetServiceProvider(provider);

                try
                {
                    // Force activation of all core services now, in the order they were introduced.
                    foreach (Type service in initializers.RegisteredTypes)
                        provider.GetOrCreate(service);

                    // Check missing integration types
                    IEnumerable<Type> missingIntegrationTypes = ((IMetaplayServiceProvider)provider).Get<IIntegrationRegistry>().MissingIntegrationTypes();
                    if (missingIntegrationTypes.Any())
                    {
                        throw new InvalidOperationException(
                            "Game must provide concrete implementations of integration types: " +
                            $"{string.Join(", ", missingIntegrationTypes.Select(x => x.Name))}");
                    }
                }
                catch (Exception)
                {
                    MetaplayServices.SetServiceProvider(oldProvider);
                    throw;
                }

                return oldProvider;
            }
        }

        public static void Initialize(Action<IServiceInitializers> configureServices)
        {
            // Avoid duplicate initialization
            if (IsInitialized)
                return;

            if (_initializeFailedException != null)
            {
                // Don't rewrite stack trace
                ExceptionDispatchInfo.Throw(_initializeFailedException);
                return;
            }

            try
            {
                Reinitialize(configureServices);
            }
            catch (Exception ex)
            {
                _initializeFailedException = ex;
                throw;
            }
        }
    }
}
