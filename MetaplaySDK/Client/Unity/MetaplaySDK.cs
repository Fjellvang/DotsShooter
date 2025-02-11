// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

#if UNITY_WEBGL && !UNITY_EDITOR
#   define UNITY_WEBGL_BUILD
#endif

using Metaplay.Client.Messages;
using Metaplay.Core;
using Metaplay.Core.Client;
using Metaplay.Core.IO;
using Metaplay.Core.Localization;
using Metaplay.Core.Memory;
using Metaplay.Core.Message;
using Metaplay.Core.Model;
using Metaplay.Core.MultiplayerEntity.Messages;
using Metaplay.Core.Network;
using Metaplay.Core.Player;
using Metaplay.Core.Serialization;
using Metaplay.Core.Tasks;
using Metaplay.Unity.IncidentReporting;
using Metaplay.Unity.Localization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

#if UNITY_2017_1_OR_NEWER && !UNITY_2021_3_OR_NEWER
#   error Metaplay SDK only supports Unity versions 2021.3 and above!
#elif UNITY_2017_1_OR_NEWER && !UNITY_2022_3_OR_NEWER
#   warning Metaplay SDK support for Unity versions older than 2022.3 is deprecated and planned to be removed in Metaplay release 31 (tentatively December 2024). Consider updating to Unity 2022.3 or newer. Please let Metaplay know if this deprecation timeline is prohibitive for you.
#endif

#if UNITY_WEBGL && !METAPLAY_ENABLE_WEBGL
#   error You must define METAPLAY_ENABLE_WEBGL to enable WebGL support in Metaplay. You should define it for all platforms (not just WebGL) in order to always get warnings about features which are poorly supported in WebGL. You can do this by adding a file called csc.rsp in your Assets folder, with a line saying "-define:METAPLAY_ENABLE_WEBGL" (without the quotes).
#endif

namespace Metaplay.Unity
{
    public struct MetaplaySDKConfig
    {
        public BuildVersion BuildVersion;

        /// <summary>
        /// True, if MetaplaySDK should create and inject MetaplaySDKBehavior GameObject script into the scene.
        /// False, if MetaplaySDKBehavior should be expected in the scene.
        /// </summary>
        public bool AutoCreateMetaplaySDKBehavior;
        public ConnectionConfig ConnectionConfig;
        public MetaplayOfflineOptions OfflineOptions;
        public IMetaplayConnectionDelegate ConnectionDelegate;
        public IMetaplayLocalizationDelegate LocalizationDelegate;
        public IMetaplayClientSocialAuthenticationDelegate SocialAuthenticationDelegate; // Optional

        public ISessionContextProvider SessionContext;
        /// <summary>
        /// Sets the active environment override at init time, if <see cref="DefaultEnvironmentConfigProvider"/>
        /// is being used, and overrides the environment used for this run of the app.
        /// </summary>
        public string ActiveEnvironmentIdOverride;
    }

    public enum ApplicationPauseStatus
    {
        /// <summary>
        /// Application is running, and was running previous frame as well.
        /// </summary>
        Running,

        /// <summary>
        /// Application is about to suspend execution at the end of this of frame, or in a few frames.
        /// OnApplicationPause(pause = true) has been called.
        /// </summary>
        Pausing,

        /// <summary>
        /// Application was unpaused between the last frame and this frame.
        /// OnApplicationPause(pause = false) was called at the start of this frame.
        /// </summary>
        ResumedFromPauseThisFrame,
    }

    public readonly struct MaintenanceModeState : IEquatable<MaintenanceModeState>
    {
        public enum ScheduleStatus
        {
            /// <summary>
            /// There is no ongoing maintenance break, nor is there known maintenance break coming in the future.
            /// </summary>
            NotScheduled = 0, // \note: this is 0 to make this struct zero-initialize into a valid, NotScheduled state.

            /// <summary>
            /// The game backend is now down for maintenance.
            /// </summary>
            Ongoing,

            /// <summary>
            /// The game backend will be soon down for maintenance.
            /// </summary>
            Upcoming,
        }

        /// <summary>
        /// Status of the maintenance mode. Maintenance mode may be ongoing, upcoming (scheduled for future), or
        /// not scheduled.
        /// </summary>
        public readonly ScheduleStatus  Status;

        /// <summary>
        /// The start time of the maintenance break.
        ///
        /// <para>
        /// If <see cref="Status"/> is <see cref="ScheduleStatus.Ongoing"/>, this is guaranteed to be in the past or the current time.
        /// </para>
        /// <para>
        /// If <see cref="Status"/> is <see cref="ScheduleStatus.Upcoming"/>, this is NOT guaranteed to be in the future. A scheduled maintenance
        /// may take a few moments to start and notify clients of it actually starting.
        /// </para>
        /// <para>
        /// If <see cref="Status"/> is <see cref="ScheduleStatus.NotScheduled"/>, this value is <c>epoch</c>.
        /// </para>
        /// </summary>
        public readonly MetaTime        MaintenanceStartAt;

        /// <summary>
        /// The estimated time when the maintenance should be complete, if known.
        ///
        /// <para>
        /// If <see cref="Status"/> is <see cref="ScheduleStatus.Ongoing"/> or <see cref="ScheduleStatus.Upcoming"/>, this value is the estimated ending
        /// time as entered by the server operator when the maintenance was started. If no ending time was given, or it could not be fetched, this will be
        /// <c>null</c>. As maintenance breaks may take longer than estimated, this value may be in the past.
        /// </para>
        /// <para>
        /// If <see cref="Status"/> is <see cref="ScheduleStatus.NotScheduled"/>, this value is <c>null</c>.
        /// </para>
        /// </summary>
        public readonly MetaTime?       EstimatedMaintenanceOverAt;

        MaintenanceModeState(ScheduleStatus status, MetaTime maintenanceStartAt, MetaTime? estimatedMaintenanceOverAt)
        {
            Status = status;
            MaintenanceStartAt = maintenanceStartAt;
            EstimatedMaintenanceOverAt = estimatedMaintenanceOverAt;
        }

        public static bool operator ==(MaintenanceModeState lhs, MaintenanceModeState rhs)
        {
            return lhs.Status                       == rhs.Status
                && lhs.MaintenanceStartAt           == rhs.MaintenanceStartAt
                && lhs.EstimatedMaintenanceOverAt   == rhs.EstimatedMaintenanceOverAt;
        }
        public static bool operator !=(MaintenanceModeState lhs, MaintenanceModeState rhs) => !(lhs == rhs);

        public bool Equals(MaintenanceModeState other) => this == other;
        public override bool Equals(object other) => other is MaintenanceModeState mms && this == mms;
        public override int GetHashCode() => Util.CombineHashCode(Status.GetHashCode(), MaintenanceStartAt.GetHashCode(), EstimatedMaintenanceOverAt.GetHashCode());

        public static MaintenanceModeState CreateNotScheduled()
        {
            return new MaintenanceModeState(ScheduleStatus.NotScheduled, MetaTime.Epoch, null);
        }
        public static MaintenanceModeState CreateUpcoming(MetaTime maintenanceStartAt, MetaTime? estimatedMaintenanceOverAt)
        {
            return new MaintenanceModeState(ScheduleStatus.Upcoming, maintenanceStartAt, estimatedMaintenanceOverAt);
        }
        public static MaintenanceModeState CreateOngoing(MetaTime maintenanceStartAt, MetaTime? estimatedMaintenanceOverAt)
        {
            // maintenanceStartAt must be in the past, if set
            MetaTime now = MetaTime.Now;
            if (maintenanceStartAt > now)
                maintenanceStartAt = now;
            return new MaintenanceModeState(ScheduleStatus.Ongoing, maintenanceStartAt, estimatedMaintenanceOverAt);
        }
    }

    public static class MetaplaySDK
    {
#if UNITY_EDITOR
        /// <summary>
        /// Path to the Metaplay specific Temp folder on Editor. Does not have a trailing /.
        /// </summary>
        public const string UnityTempDirectory = "Temp/Metaplay"; // Use Unity's Temp/ directory
#endif

        public static readonly MetaplayLogs Logs = InitializeLogChannels();

        static MetaplayLogs InitializeLogChannels()
        {
            // \note These are initialized without proper log levels, colors etc. being configured.
            // These are configured in MetaplaySDK.InitLogging(). The UnityLogger instance
            // will not be replaced.
            MetaplayLogs logs = new MetaplayLogs();
            return logs;
        }

        public static MessageDispatcher MessageDispatcher { get; private set; }
        public static NetworkDiagnosticsManager NetworkDiagnosticsManager { get; private set; }
        public static ITimelineHistory TimelineHistory { get; private set; }
        public static IncidentRepository IncidentRepository { get; private set; }
        public static IncidentTracker IncidentTracker { get; private set; }
        public static IncidentUploader IncidentUploader { get; private set; }
        public static MetaplayConnection Connection { get; private set; }
        public static MetaplayLocalizationManager LocalizationManager { get; private set; }
        public static LocalizationLanguage ActiveLanguage { get; internal set; }

        /// <summary>
        /// Social authentication utility. This reference is never null. Any social claims before the SDK has been initialized are buffered and
        /// executed when connection is established. Any social claims or results after SDK <see cref="Stop"/> and before the subsequent <see cref="Start"/>
        /// are ignored.
        /// </summary>
        public static SocialAuthManager SocialAuthentication { get; private set; } = SocialAuthManager.CreateBufferingManager();

        /// <summary>
        /// Path where downloadable resources (such as game configs) are downloaded to. Conventional path format: DownloadCachePath/Type/ConfigVersion
        /// </summary>
        public static string DownloadCachePath { get; private set; }

        /// <summary>
        /// Unique identifier for this application launch.
        /// </summary>
        public static Guid AppLaunchId = Guid.NewGuid();

        /// <summary>
        /// Contains the latest information on the upcoming maintenance break. This value is updated
        /// on every successful login and when* <c>UpdateScheduledMaintenanceMode</c> message is received,
        /// or whenever the connection transitions into <see cref="ConnectionStates.TerminalError.InMaintenance"/>
        /// error state. In any case, <see cref="MaintenanceModeChanged"/> event is fired.
        /// <para>
        /// *) This value is guaranteed to update before any game message listener observes the message. Hence accessing
        /// this value from the message handler will observe in the most up-to-date value.
        /// </para>
        /// </summary>
        public static MaintenanceModeState MaintenanceMode { get; private set; }

        /// <summary>
        /// Invoked after <see cref="MaintenanceMode"/> changed.
        /// <para>
        /// Called during <see cref="MetaplaySDK.Update"/> if <see cref="MaintenanceMode"/> updated during the update.
        /// </para>
        /// </summary>
        public static event Action MaintenanceModeChanged;

        /// <summary>
        /// Time of previous EndOfFrame event. On the first frame, set to the time of initialization.
        /// </summary>
        public static DateTime ApplicationPreviousEndOfTheFrameAt { get; private set; }

        /// <summary>
        /// Duration of the previous application pause. Value is updated when OnApplicationPause(false) is received.
        /// If no pauses have occurred, this is <see cref="TimeSpan.Zero"/>.
        /// </summary>
        public static TimeSpan ApplicationLastPauseDuration { get; private set; } = TimeSpan.Zero;

        /// <summary>
        /// Updated when OnApplicationPause(true) is received.
        /// If no pauses have occurred, this is <see cref="DateTime.UnixEpoch"/>.
        /// </summary>
        public static DateTime ApplicationLastPauseBeganAt { get; private set; } = DateTime.UnixEpoch;

        /// <summary>
        /// Updated when application pauses, unpauses, and at the end of the frame.
        /// </summary>
        public static ApplicationPauseStatus ApplicationPauseStatus { get; private set; } = ApplicationPauseStatus.Running;

        /// <summary>
        /// The duration the application should tolerate being paused during the current pause event. Exceeding this time
        /// will result in Session closing with <see cref="ConnectionStates.TransientError.SessionLostInBackground"/> error.
        /// This field only meaningful when <see cref="ApplicationPauseStatus"/> is <see cref="ApplicationPauseStatus.Pausing"/>
        /// or <see cref="ApplicationPauseStatus.ResumedFromPauseThisFrame"/>.
        /// <para>
        /// The value is by default <see cref="MetaplaySDKConfig.ConnectionConfig"/>.<see cref="ConnectionConfig.MaxSessionRetainingPauseDuration"/>
        /// but may be overriden by <see cref="OnApplicationAboutToBePaused(string, TimeSpan)"/>.
        /// </para>
        /// </summary>
        public static TimeSpan ApplicationPauseMaxDuration { get; private set; } = TimeSpan.Zero;

        /// <summary>
        /// The maximum duration the application will paused as declared in recent <see cref="OnApplicationAboutToBePaused(string, TimeSpan)"/>,
        /// or <c>null</c> otherwise. This field only meaningful when <see cref="ApplicationPauseStatus"/> is <see cref="ApplicationPauseStatus.Pausing"/>
        /// or <see cref="ApplicationPauseStatus.ResumedFromPauseThisFrame"/>.
        /// </summary>
        public static TimeSpan? ApplicationPauseDeclaredMaxDuration { get; private set; } = TimeSpan.Zero;

        /// <summary>
        /// The reason string application provided for the pause with <see cref="OnApplicationAboutToBePaused(string, TimeSpan)"/>
        /// if <see cref="ApplicationPauseStatus"/> is <see cref="ApplicationPauseStatus.Pausing"/> or <see cref="ApplicationPauseStatus.ResumedFromPauseThisFrame"/>.
        /// Null otherwise.
        /// </summary>
        public static string ApplicationPauseReason { get; private set; } = null;

        /// <summary>
        /// The base address of the currently active CDN (Content Delivery Network) endpoint. The value may vary at runtime if the
        /// client is redirected to another server or if client changes whether it prefers IPv4 or IPv6 connection. Empty in offline
        /// mode.
        /// </summary>
        public static MetaplayCdnAddress CdnAddress { get; private set; }

        /// <summary>
        /// The version of this client build.
        /// </summary>
        public static BuildVersion BuildVersion { get; private set; }

        /// <summary>
        /// Unique identifier for the device.
        /// </summary>
        internal static string DeviceGuid { get; private set; }

        /// <summary>
        /// Get data baked into the client during build. See <see cref="ClientBuildData"/> documentation for further info.
        /// </summary>
        public static T GetClientBuildData<T>() where T : ClientBuildData
        {
            return (T)IntegrationRegistry.Get<ClientBuildData>();
        }

        /// <summary>
        /// The last known PlayerId of the this client. This will be <c>None</c> until asynchronouly loaded during SDK startup
        /// (when game has been launched before) or when server generates a new account for application first launches.
        /// </summary>
        public static EntityId PlayerId { get; set; }

        /// <summary>
        /// True between <see cref="Start"/> and <see cref="Stop"/>. Otherwise false.
        /// </summary>
        public static bool IsRunning => _runState != null;

        public static ISessionContextProvider SessionContext { get; private set; }

        /// <summary>
        /// The set of experiments the current player is a member of. Values are set when the session
        /// is established, i.e. <c>Connection.State.Status == Connected</c>.
        /// </summary>
        public static MetaDictionary<PlayerExperimentId, ExperimentMembershipStatus> ActiveExperiments { get; private set; }

        static EnvironmentConfig _currentEnvironmentConfig = null;

        /// <summary>
        /// Currently active environment config, cached in <see cref="MetaplaySDK.Start"/>.
        /// </summary>
        public static EnvironmentConfig CurrentEnvironmentConfig {
            get { return _currentEnvironmentConfig ?? throw new InvalidOperationException("Cannot access MetaplaySDK.CurrentEnvironmentConfig before calling MetaplaySDK.Start()"); }
            private set { _currentEnvironmentConfig = value; }
        }

        static string _persistentDataPath = null;

        /// <summary>
        /// Wrapper for Unity's <see cref="Application.persistentDataPath" /> that returns simpler paths
        /// in WebGL builds where persistence is implemented using localStorage and IndexedDB.
        /// </summary>
        public static string PersistentDataPath
        {
            get { return _persistentDataPath ?? throw new InvalidOperationException("Cannot access MetaplaySDK.PersistentDataPath before calling MetaplaySDK.Initialize()"); }
            private set { _persistentDataPath = value; }
        }

        /// <summary>
        /// Location of DeviceGuid blob.
        /// </summary>
        static string GetDeviceGuidPath() => Path.Combine(PersistentDataPath, "MetaplayDeviceGuid.dat");
        static int DeviceGuidFileVersion = 1;

#if UNITY_EDITOR
        // Make sure PersistentDataPath is always available in Editor.
        [InitializeOnLoadMethod]
        private static void InitializeForEditor()
        {
            PersistentDataPath = Application.persistentDataPath;
        }
#endif

#if UNITY_IOS
        /// <summary>
        /// The BundleID of this iOS app. This field is only available on iOS platforms.
        /// </summary>
        public static string AppleBundleId { get; private set; }
#endif

        // Internal state that only exists between Start() and Stop()
        class RunState
        {
            public Stack<Action> CleanupTasks = new Stack<Action>();
        }

        static DateTime? _currentPauseStartTime;
        static bool _isFirstFrame;
        static bool _onlyOnceInitCompleted;
        static DateTime _expectedPauseHintedAt = DateTime.UnixEpoch;
        static string _expectedPauseReason;
        static TimeSpan _expectedPauseDuration;
        static bool _maintenanceModeChanged;
        static Task _pendingFlushBeforeAppSuspend;
        static DateTime _pendingFlushBeforeAppSuspendDeadline = DateTime.UnixEpoch;
        static RunState _runState;

        public static void Start(MetaplaySDKConfig config)
        {
            if (IsRunning)
                throw new InvalidOperationException("Duplicate MetaplaySDK.Start(). Start has already been called without calling Stop() in between.");

            try
            {
                _runState = new RunState();

#if UNITY_WEBGL && !UNITY_EDITOR
                PersistentDataPath = "/persistent";
#else
                PersistentDataPath = Application.persistentDataPath;
#endif

#if UNITY_IOS
                AppleBundleId = Application.identifier;
#endif

                if (config.ConnectionDelegate == null)
                    throw new ArgumentNullException(nameof(config.ConnectionDelegate));
                if (config.LocalizationDelegate == null)
                    throw new ArgumentNullException(nameof(config.LocalizationDelegate));

                MetaplaySDKBehavior.EnsureExists(config.AutoCreateMetaplaySDKBehavior);

                // Initialize logic systems
                Stopwatch sw = Stopwatch.StartNew();
                MetaplayCore.InitializeForClient(ClientPlatformUnityUtil.GetBuildTargetPlatform());

                // Apply possible active environment ID override
                if (!string.IsNullOrEmpty(config.ActiveEnvironmentIdOverride))
                {
                    try
                    {
                        DefaultEnvironmentConfigProvider.Instance.SetActiveEnvironmentIdRuntimeOverride(config.ActiveEnvironmentIdOverride);
                    }
                    catch (DefaultEnvironmentConfigProvider.NotDefaultEnvironmentConfigProviderIntegrationException)
                    {
                        Logs.Metaplay.Warning("Active environment ID override was set in MetaplaySDKConfig, but DefaultEnvironmentConfigProvider is not the active integration in use, so the override is not used.");
                    }
                }

                // Get the currently active environment config
                CurrentEnvironmentConfig = IEnvironmentConfigProvider.Get();

                LogLevel                     defaultLogLevel   = CurrentEnvironmentConfig.ClientLoggingConfig.LogLevel;
                Dictionary<string, LogLevel> logLevelOverrides = CurrentEnvironmentConfig.ClientLoggingConfig.LogLevelOverrides.ToDictionary(spec => spec.ChannelName, spec => spec.LogLevel);
                InitLogging(defaultLogLevel, logLevelOverrides, _runState.CleanupTasks);

                Logs.Metaplay.Info("MetaplayCore.Initialize() {ms}ms", sw.ElapsedMilliseconds);

                // Initialize main thread debug checker
                MainThreadDebugCheck.InitializeFromMainThread(Logs.Metaplay);

                // Initialize WebGL runtime systems
#if UNITY_WEBGL && !UNITY_EDITOR
                MetaplayWebGL.Initialize(OnApplicationQuit);
#endif

                MessageDispatcher = new MessageDispatcher(Logs.Message);
                MessageDispatcher.AddListener<DisconnectedFromServer>(OnDisconnectedFromServer);
                // Silence warnings from unhandled messages
                MessageDispatcher.AddListener<SessionPong>(OnIgnoredMessage);
                MessageDispatcher.AddListener<UpdateScheduledMaintenanceMode>(OnIgnoredMessage);
                MessageDispatcher.AddListener<SessionAcknowledgementMessage>(OnIgnoredMessage);
                MessageDispatcher.AddListener<ConnectedToServer>(OnIgnoredMessage);
                MessageDispatcher.AddListener<Handshake.ClientHelloAccepted>(OnIgnoredMessage);
                MessageDispatcher.AddListener<Handshake.LoginSuccessResponse>(OnIgnoredMessage);
#if !METAPLAY_DISALLOW_DEVICE_ID_LOGIN
                MessageDispatcher.AddListener<Handshake.CreateGuestAccountResponse>(OnIgnoredMessage);
#endif
                MessageDispatcher.AddListener<SessionProtocol.SessionResumeSuccess>(OnIgnoredMessage);
                MessageDispatcher.AddListener<SessionProtocol.SessionStartFailure>(OnIgnoredMessage);
                MessageDispatcher.AddListener<SessionProtocol.SessionStartResourceCorrection>(OnIgnoredMessage);
                MessageDispatcher.AddListener<ConnectionHandshakeFailure>(OnIgnoredMessage);
                MessageDispatcher.AddListener<MessageTransportInfoWrapperMessage>(OnIgnoredMessage);
                MessageDispatcher.AddListener<Handshake.OperationStillOngoing>(OnIgnoredMessage);

                NetworkDiagnosticsManager = new NetworkDiagnosticsManager();

#if UNITY_EDITOR
                TimelineHistory = new TimelineHistory();
                _runState.CleanupTasks.Push(() => { TimelineHistory.Dispose(); TimelineHistory = null; });
#else
                TimelineHistory = null;
#endif

                ServerEndpoint initialEndpoint = CurrentEnvironmentConfig.ConnectionEndpointConfig.GetServerEndpoint();

                CdnAddress = CreateInitialCdnAddress(initialEndpoint);
                DeviceGuid = ReadDeviceGuid();

                ClientBuildData buildData = GetClientBuildData<ClientBuildData>();

                BuildVersion = new BuildVersion(
                    version: config.BuildVersion.Version ?? "undefined",
                    buildNumber: config.BuildVersion.BuildNumber ?? buildData.BuildNumber,
                    commitId: config.BuildVersion.CommitId ?? buildData.CommitId);

                Connection = new MetaplayConnection(initialEndpoint, config.ConnectionConfig, config.ConnectionDelegate, new MetaplayConnectionSDKHook(), config.OfflineOptions);
                _runState.CleanupTasks.Push(() => Connection.Close(flushEnqueuedMessages: false));

                // Initialize incident reporting. Note that the repository is only set-up once
                if (_onlyOnceInitCompleted == false)
                    IncidentRepository = new IncidentRepository();

                IncidentTracker = new IncidentTracker();
                _runState.CleanupTasks.Push(() => IncidentTracker.Dispose());
                IncidentUploader = new IncidentUploader();

                // Initialize localization systems
                if (_onlyOnceInitCompleted == false)
                {
                    BuiltinLanguageRepository.Initialize();
                    LocalizationManager = new MetaplayLocalizationManager();
                }

                LocalizationManager.Start(config.LocalizationDelegate);
                _runState.CleanupTasks.Push(() => LocalizationManager.Stop());

                DownloadCachePath = Path.Combine(PersistentDataPath, "GameConfigCache");

                SetMaintenanceMode(MaintenanceModeState.CreateNotScheduled());

                if (_onlyOnceInitCompleted == false)
                {
                    ApplicationPreviousEndOfTheFrameAt = DateTime.UtcNow;
                    ActiveExperiments = new MetaDictionary<PlayerExperimentId, ExperimentMembershipStatus>();
                    _onlyOnceInitCompleted = true;
                    _isFirstFrame = true;
                }

                MetaplaySDKBehavior.AfterSDKInit();
                LocalizationManager.AfterSDKInit();

                SessionContext = config.SessionContext;

                // Create a new authentication manager and copy buffered claims to it. If existing manager was not a buffering
                // manager, i.e. this SDK was Closed and Started, the buffer will be null.
                SocialAuthentication = SocialAuthManager.CreateRealManager(config.SocialAuthenticationDelegate, SocialAuthentication.BufferedClaims);
                _runState.CleanupTasks.Push(() =>
                {
                    // Stop ongoing authentication operations
                    SocialAuthentication.Stop();
                    // Create a dummy manager for the duration from Stop() until next Start.
                    SocialAuthentication = SocialAuthManager.CreateStoppedManager();
                });
            }
            catch (Exception ex)
            {
                Logs.Metaplay.Error("MetaplaySDK.Start() failed and will not be operational: {ex}", ex);
                Stop();
                throw ex;
            }
        }

        public static void Stop()
        {
            if (!IsRunning)
            {
                Logs.Metaplay.Error("MetaplaySDK.Stop() was called without matching Start()");
                return;
            }

            MainThreadDebugCheck.Deinitialize();

            // \note: MetaplaySDKBehavior is kept intact. It will do lifecycle measurement.

            foreach (Action action in _runState.CleanupTasks)
                action.Invoke();

            IncidentTracker = null;
            _maintenanceModeChanged = false;

            // \note: SocialAuthentication remains non-null after stopping to keep old behavior.

            _runState = null;
        }

        /// <summary>
        /// Change the server endpoint to connect to.
        /// This closes the current connection (if any). Starting from the next connection,
        /// the new endpoint will be used.
        /// </summary>
        /// <remarks>
        /// This is intended as a development utility.
        /// </remarks>
        public static void ChangeServerEndpoint(ServerEndpoint endpoint)
        {
            if (Connection.State is ConnectionStates.Connecting || Connection.State is ConnectionStates.Connected)
                Connection.CloseWithError(flushEnqueuedMessages: true, new ClientTerminatedConnectionConnectionError());

            CdnAddress = CreateInitialCdnAddress(endpoint);
            Connection.ChangeServerEndpoint(endpoint);
        }

        static MetaplayCdnAddress CreateInitialCdnAddress(ServerEndpoint endpoint)
        {
            // Initial CDN address. Prefer initially IPv4 but this will get updated when the connection to server is established.
            if (!endpoint.IsOfflineMode)
                return MetaplayCdnAddress.Create(endpoint.CdnBaseUrl, prioritizeIPv4: true);
            else
                return MetaplayCdnAddress.Empty;
        }

        static void UnobservedTaskExceptionHandler(object sender, UnobservedTaskExceptionEventArgs args)
        {
            // Unwrap outermost layer when there is only one Aggegate([Inner])
            AggregateException ae = args.Exception.Flatten();
            Logs.Metaplay.Warning("Unobserved {Exception}", ae.InnerExceptions.Count == 1 ? ae.InnerException : ae);
        }

        static void InitLogging(LogLevel defaultLogLevel, Dictionary<string, LogLevel> logLevelOverrides, Stack<Action> cleanupTasks)
        {
            Logs.Initialize(defaultLogLevel, logLevelOverrides);
            cleanupTasks.Push(() => Logs.Reset());

            Logs.Metaplay.Info("Logger config: level={DefaultLogLevel} overrides={Overrides}", defaultLogLevel, PrettyPrint.Compact(logLevelOverrides));

            // Register additional log sources
            TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler;
            cleanupTasks.Push(() => TaskScheduler.UnobservedTaskException -= UnobservedTaskExceptionHandler);
        }

        /// <summary>
        /// Update logic for the Metaplay SDK. Should be called on every frame from your game's main loop.
        /// </summary>
        public static void Update()
        {
            if (!IsRunning)
                return;

            // Update incident tracker cached values
            IncidentTracker?.UpdateEarly();

            Connection.InternalUpdate();

            // Update incident tracker cached values
            IncidentTracker?.UpdateAfterConnection();

            if (_maintenanceModeChanged)
            {
                _maintenanceModeChanged = false;
                try
                {
                    MaintenanceModeChanged?.Invoke();
                }
                catch(Exception ex)
                {
                    Logs.Metaplay.Error("MaintenanceModeChanged handler failed with {0}", ex);
                }
            }

            // Wait for flush to complete until we allow app to go to background
            if (_pendingFlushBeforeAppSuspend != null)
            {
                int millisecondsToWait = (int)(_pendingFlushBeforeAppSuspendDeadline - DateTime.UtcNow).TotalMilliseconds;
                if (millisecondsToWait > 0)
                {
                    if (!_pendingFlushBeforeAppSuspend.Wait(millisecondsToWait))
                        Logs.Metaplay.Warning("Network flush on before app suspend is taking too much time. Will not block suspend any further.");
                }
                _pendingFlushBeforeAppSuspend = null;
            }
        }

        /// <summary>
        /// Start generating a new <see cref="NetworkDiagnosticReport"/> with default settings. Generating the report takes
        /// some time (up to 5 sec for now, at which point any remaining attempts are given up on). After the report is
        /// completed or the timeout happens, a callback method is called (on the calling thread).
        /// </summary>
        /// <param name="callback">Method to call when the report generation is finished (or timeout occurs).</param>
        // \todo [petri] support cancellation?
        public static void StartNewNetworkDiagnosticsReport(Action<NetworkDiagnosticReport> callback)
        {
            // \todo [petri] network diagnostics tool makes difference assumptions about how gateways are handled, fix them
            ServerEndpoint endpoint = Connection.Endpoint;
            List<int> gameServerPorts = new List<int> { endpoint.PrimaryGateway.ServerPort }.Concat(endpoint.BackupGateways.Select(gw => gw.ServerPort)).ToList();

            // In offline mode there is no need to run network diagnostics
            if (endpoint.IsOfflineMode)
            {
                callback(new NetworkDiagnosticReport());
                return;
            }

            NetworkDiagnosticsManager.StartNewReport(
                gameServerHost4:    MetaplayHostnameUtil.GetV4V6SpecificHost(endpoint.PrimaryGateway.ServerHost, isIPv4: true),
                gameServerHost6:    MetaplayHostnameUtil.GetV4V6SpecificHost(endpoint.PrimaryGateway.ServerHost, isIPv4: false),
                gameServerPorts:    gameServerPorts,
                gameServerUseTls:   endpoint.PrimaryGateway.EnableTls,
                cdnHostname4:       GetCDNHostnameFromBaseUrl(MetaplaySDK.CdnAddress.IPv4BaseUrl),
                cdnHostname6:       GetCDNHostnameFromBaseUrl(MetaplaySDK.CdnAddress.IPv6BaseUrl),
                timeout:            TimeSpan.FromSeconds(5),
                callback:           callback);
        }

        static string GetCDNHostnameFromBaseUrl(string cdnBaseUrl)
        {
            if (string.IsNullOrEmpty(cdnBaseUrl))
                return "";
            return new Uri(cdnBaseUrl).Host;
        }

        /// <summary>
        /// Enqueues operation to be completed on the Unity Thread. If called from Unity Thread,
        /// the operation is also enqueued for later execution and it is not executed synchronously.
        /// </summary>
        public static Task<TResult> RunOnUnityThreadAsync<TResult>(Func<TResult> op)
        {
            return MetaTask.Run(op, MetaTask.UnityMainScheduler);
        }

        /// <inheritdoc cref="RunOnUnityThreadAsync{TResult}(Func{TResult})"/>
        public static Task RunOnUnityThreadAsync(Action op)
        {
            return MetaTask.Run(op, MetaTask.UnityMainScheduler);
        }

        static void OnIgnoredMessage(MetaMessage message)
        {
        }

        static void OnDisconnectedFromServer(MetaMessage message)
        {
            // \todo [jarkko]: uses SDK hook for start, and game-hook for Stop. Tiny race possibility where
            //                 stop could be lost.
            SocialAuthentication.OnSessionStopped();
        }

        internal static void UpdateAtEndOfTheFrame()
        {
            DateTime timeNow = DateTime.UtcNow;

            ApplicationPreviousEndOfTheFrameAt = timeNow;
            if (ApplicationPauseStatus == ApplicationPauseStatus.ResumedFromPauseThisFrame)
            {
                ApplicationPauseStatus = ApplicationPauseStatus.Running;
                ApplicationPauseMaxDuration = Connection.Config.MaxSessionRetainingPauseDuration;
                ApplicationPauseDeclaredMaxDuration = null;
                ApplicationPauseReason = null;
            }

            _isFirstFrame = false;

            if (IsRunning)
            {
                // \todo: update integrations

                Connection.InternalUpdateAtEndOfTheFrame();
                IncidentUploader.LateUpdate(DateTime.UtcNow);
            }
        }

        internal static void OnApplicationQuit()
        {
            Logs.Metaplay.Info("MetaplaySDK.OnApplicationQuit()");

            if (Connection != null)
            {
                // Close existing server connection. This is done here for the purposes of flushing any pending log
                // messages from the network thread and to prevent further server communication during the application
                // shutdown sequence.
                // \note In offline mode, this also ends up calling OfflineServer.TryPersistState().
                //       We rely on that here, and don't call OfflineServer.TryPersistState() here explicitly.
                Connection.Close(false);
            }
        }

        internal static void OnApplicationPause(bool pause)
        {
            DateTime now = DateTime.UtcNow;

            if (pause)
            {
                if (!_currentPauseStartTime.HasValue)
                {
                    TimeSpan maxDuration;
                    string pauseReason;
                    TimeSpan? declaredDurationMaybe;
                    if (!IsRunning)
                    {
                        Logs.Metaplay.Info("Application paused while MetaplaySDK wasn't running");
                        maxDuration = new ConnectionConfig().MaxSessionRetainingPauseDuration; // \note No game configuration available yet - use default.
                        pauseReason = null;
                        declaredDurationMaybe = null;
                    }
                    else if (_expectedPauseHintedAt == DateTime.UnixEpoch)
                    {
                        Logs.Metaplay.Info("Application paused");
                        maxDuration = Connection.Config.MaxSessionRetainingPauseDuration;
                        pauseReason = null;
                        declaredDurationMaybe = null;
                    }
                    else if (now - _expectedPauseHintedAt < TimeSpan.FromSeconds(1))
                    {
                        Logs.Metaplay.Info("Application paused with a declared reason: {Reason}", _expectedPauseReason);
                        maxDuration = Util.Max(Connection.Config.MaxSessionRetainingPauseDuration, _expectedPauseDuration);
                        pauseReason = _expectedPauseReason;
                        declaredDurationMaybe = maxDuration;
                    }
                    else
                    {
                        Logs.Metaplay.Info("Application paused (declared reason missed due to timeout: {Reason})", _expectedPauseReason);
                        maxDuration = Connection.Config.MaxSessionRetainingPauseDuration;
                        pauseReason = null;
                        declaredDurationMaybe = null;
                    }

                    _currentPauseStartTime = now;
                    ApplicationPauseStatus = ApplicationPauseStatus.Pausing;
                    ApplicationPauseMaxDuration = maxDuration;
                    ApplicationLastPauseBeganAt = now;
                    ApplicationPauseDeclaredMaxDuration = declaredDurationMaybe;
                    ApplicationPauseReason = pauseReason;

                    // Flush any pending messages to the server.
                    TryFlushNetworkMessages();

                    // Send the pause start information to the server
                    if (IsRunning)
                    {
                        Connection.InternalOnApplicationPause(declaredDurationMaybe, pauseReason);
                    }

                    // If in offline mode, persist state
                    if (Connection != null && Connection.Endpoint.IsOfflineMode)
                        Connection.OfflineServer?.TryPersistState();

                    // We have flushed the messages to the session layer. Wait until the data
                    // has been flushed to the socket layer too. Set a timeout of 100 milliseconds
                    // to avoid blocking too long.
                    // Note that we don't wait here. Unity issues one extra frame update after this
                    // notification, so we will wait at the next Update().
                    MessageTransportWriteFence writeFence = Connection?.TryEnqueueTransportWriteFence();
                    if (writeFence != null)
                    {
                        _pendingFlushBeforeAppSuspend = writeFence.WhenComplete;
                        _pendingFlushBeforeAppSuspendDeadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(100);
                    }

                    if (IsRunning)
                    {
                        Connection.SchedulePauseTerminationTimer(ApplicationPauseMaxDuration);
                    }
                }
                else
                {
                    Logs.Metaplay.Debug("Received application pause while application was already paused. Ignored.");
                }
            }
            else
            {
                if (_currentPauseStartTime.HasValue)
                {
                    ApplicationLastPauseDuration = now - _currentPauseStartTime.Value;
                    ApplicationPauseStatus = ApplicationPauseStatus.ResumedFromPauseThisFrame;
                    _currentPauseStartTime = null;
                    _expectedPauseHintedAt = DateTime.UnixEpoch;

                    Logs.Metaplay.Info("Application resumed from pause, duration was {PauseDuration}", ApplicationLastPauseDuration);

                    if (IsRunning)
                    {
                        Connection.InternalOnApplicationResume();
                    }
                }
                else if (_isFirstFrame)
                {
                    // Resume on first frame is ignored.
                }
                else
                {
                    Logs.Metaplay.Debug("Received application unpause while application was already running. Ignored.");
                }
            }
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Static (re-)initialization to clear per app start state also in editor playmode with domain reload disabled.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void StaticInit()
        {
            _onlyOnceInitCompleted = false;
            AppLaunchId = Guid.NewGuid();
        }
        #endif

        /// <summary>
        /// <para>
        /// Hints the MetaplaySDK that Application is expected to Pause for a moment, and then continue execution. MetaplaySDK
        /// uses this to extend the session lifetime over the duration of the expected pause. If the application is not paused
        /// soon after this call, this call has no effect.
        /// </para>
        /// <para>
        /// This is useful in cases such as showing an Ad or opening an external app from which user is expected to return after
        /// a short while without losing the session state.
        /// </para>
        /// </summary>
        /// <param name="reason">Developer name for the reason of the pause. Only used in logging.</param>
        /// <param name="keepSessionAliveInBackgroundDuration">Duration the connection is kept alive in the background.</param>
        public static void OnApplicationAboutToBePaused(string reason, TimeSpan keepSessionAliveInBackgroundDuration)
        {
            _expectedPauseHintedAt = DateTime.UnixEpoch;
            _expectedPauseReason = reason;
            _expectedPauseDuration = keepSessionAliveInBackgroundDuration;

            TryFlushNetworkMessages();
        }

        static void TryFlushNetworkMessages()
        {
            try
            {
                Connection?.Delegate?.FlushPendingMessages();
            }
            catch(Exception ex)
            {
                Logs.Metaplay.Warning("Error while flushing messages: {ex}", ex);
            }
        }

        static void SetMaintenanceMode(MaintenanceModeState maintenanceMode)
        {
            if (MetaplaySDK.MaintenanceMode.Equals(maintenanceMode))
                return;

            MetaplaySDK.MaintenanceMode = maintenanceMode;
            MetaplaySDK._maintenanceModeChanged = true;
        }

        static string ReadDeviceGuid()
        {
            byte[] data = AtomicBlobStore.TryReadBlob(GetDeviceGuidPath());
            if (data == null)
                return null;

            using (IOReader reader = new IOReader(data))
            {
                int ver = reader.ReadInt32();
                if (ver != DeviceGuidFileVersion)
                {
                    Logs.Metaplay.Warning("Found incompatible DeviceGuid file version {foundVersion}, expected {expectedVersion}", ver, DeviceGuidFileVersion);
                    return null;
                }
                return reader.ReadString(128);
            }
        }

        static void SetDeviceGuid(string deviceGuid)
        {
            DeviceGuid = deviceGuid;
            using (SegmentedIOBuffer buffer = new SegmentedIOBuffer())
            {
                using (IOWriter writer = new IOWriter(buffer))
                {
                    writer.WriteInt32(DeviceGuidFileVersion);
                    writer.WriteString(deviceGuid);
                }

                if (!AtomicBlobStore.TryWriteBlob(GetDeviceGuidPath(), buffer.ToArray()))
                    Logs.Metaplay.Error("Storing DeviceGuid as blob {path} failed", GetDeviceGuidPath());
            }
        }

        #region MetaplayConnectionSDKHook

        private class MetaplayConnectionSDKHook : IMetaplayConnectionSDKHook
        {
            public MetaplayConnectionSDKHook()
            {
            }

            void IMetaplayConnectionSDKHook.OnCurrentCdnAddressUpdated(MetaplayCdnAddress currentAddress)
            {
                MetaplaySDK.CdnAddress = currentAddress;
            }
            void IMetaplayConnectionSDKHook.OnScheduledMaintenanceModeUpdated(MaintenanceModeState maintenanceMode)
            {
                SetMaintenanceMode(maintenanceMode);
            }
            void IMetaplayConnectionSDKHook.OnSessionStarted(SessionProtocol.SessionStartSuccess sessionStart)
            {
                MetaplaySDK.ActiveExperiments.Clear();
                if (sessionStart.PlayerState.ActiveExperiments != null)
                {
                    foreach (EntityActiveExperiment sessionActiveExperiment in sessionStart.PlayerState.ActiveExperiments)
                        MetaplaySDK.ActiveExperiments.Add(sessionActiveExperiment.ExperimentId, ExperimentMembershipStatus.FromSessionInfo(sessionActiveExperiment));
                }

#if UNITY_WEBGL_BUILD
                // Setup IMX login
                if (MetaplayCore.Options.FeatureFlags.EnableImmutableXLinkClientLibrary)
                    ImmutableXApiBridge.SetApiUrl(new ImmutableXApiBridge.SetApiUrlAsyncRequest() { ApiUrl = Connection.ServerOptions.ImmutableXLinkApiUrl });
#endif

                MetaplayConfigManager.OnSessionStarted();
                SocialAuthentication.OnSessionStarted();
            }

            string IMetaplayConnectionSDKHook.GetDeviceGuid()
            {
                return DeviceGuid;
            }

            void IMetaplayConnectionSDKHook.SetDeviceGuid(string deviceGuid)
            {
                SetDeviceGuid(deviceGuid);
            }
        }

        #endregion
    }

    public class ClientTerminatedConnectionConnectionError : ConnectionStates.TransientError
    {
    }
}
