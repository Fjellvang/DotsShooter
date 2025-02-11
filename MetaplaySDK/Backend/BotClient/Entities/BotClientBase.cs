// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Client.Messages;
using Metaplay.Cloud;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.Client;
using Metaplay.Core.Config;
using Metaplay.Core.Localization;
using Metaplay.Core.Message;
using Metaplay.Core.Network;
using Metaplay.Core.Player;
using Metaplay.Core.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Metaplay.Core.MultiplayerEntity.Messages;
using Metaplay.Core.InAppPurchase;
using Metaplay.Core.InGameMail;
using Metaplay.Core.Debugging;
using static Metaplay.Core.Debugging.PlayerIncidentReport;
using static System.FormattableString;
using static Metaplay.BotClient.BotCoordinator;
using Metaplay.Core.Json;
using System.Net.Http;


#if !METAPLAY_DISABLE_GUILDS
using Metaplay.Core.Guild;
using Metaplay.Core.Guild.Messages.Core;
using Metaplay.Core.GuildDiscovery;
#endif

namespace Metaplay.BotClient
{
    public abstract class BotClientConfigBase : EphemeralEntityConfig
    {
        public override EntityKind          EntityKind              => EntityKindBotCore.BotClient;
        public override Type                EntityShardType         => typeof(BotCoordinator);
        public override NodeSetPlacement    NodeSetPlacement        => NodeSetPlacement.All;
        public override IShardingStrategy   ShardingStrategy        => new ManualShardingStrategy();
        public override TimeSpan            ShardShutdownTimeout    => TimeSpan.FromSeconds(60);
    }

    public abstract class BotClientBase : EphemeralEntityActor, IPlayerModelClientListenerCore
    {
        public class SessionEndTimeReached { public static readonly SessionEndTimeReached Instance = new SessionEndTimeReached(); }
        public class InitDeadlineReached { public static readonly InitDeadlineReached Instance = new InitDeadlineReached(); }

        public static readonly double[] LoginDurationBuckets        = new double[] { 0.1, 0.2, 0.5, 1.0, 2.0, 3.0, 4.0, 5.0, 10.0, 20.0, 60.0 };
        static Prometheus.Counter       c_connectAttempts           = Prometheus.Metrics.CreateCounter("botclient_connect_attempts_total", "Cumulative number of connection attempts to the server");
        static Prometheus.Counter       c_connectionsEstablished    = Prometheus.Metrics.CreateCounter("botclient_connections_established_total", "Cumulative number of connections established to server");
        static Prometheus.Histogram     c_connectDuration           = Prometheus.Metrics.CreateHistogram("botclient_connect_duration", "Duration from beginning of connection to protocol handshake, as observed by botclient", new Prometheus.HistogramConfiguration { Buckets = LoginDurationBuckets });
        static Prometheus.Histogram     c_loginDuration             = Prometheus.Metrics.CreateHistogram("botclient_login_duration", "Duration from beginning of connection to login success, as observed by botclient", new Prometheus.HistogramConfiguration { Buckets = LoginDurationBuckets });
        static Prometheus.Counter       c_connectionsFailed         = Prometheus.Metrics.CreateCounter("botclient_connection_failed_total", "Cumulative number of botclient connections to server lost or failed to connect", "reason", "state");
        static Prometheus.Counter       c_sessionTimeouts           = Prometheus.Metrics.CreateCounter("botclient_session_timeout_total", "Cumulative number of botclient sessions ended due to a timeout");

        static HttpClient s_httpClient = new HttpClient();

        /// <summary>
        /// The time interval for Ticks(). This corresponds roughly to frame time on client i.e (1s / interval) FPS.
        /// As on the client, this sets the poll rate for incoming messages.
        /// </summary>
        protected virtual TimeSpan UpdateTickInterval => TimeSpan.FromMilliseconds(100);
        protected override AutoShutdownPolicy ShutdownPolicy => AutoShutdownPolicy.ShutdownNever();
        TimeSpan ConnectTimeout => TimeSpan.FromSeconds(10);

        enum LifecycleState
        {
            NotStarted,
            Ticking,
            Terminated,
        }

        #if !METAPLAY_DISABLE_GUILDS

        public class GuildInviteSample
        {
            public readonly GuildInviteCode InviteCode;

            public GuildInviteSample(GuildInviteCode inviteCode)
            {
                InviteCode = inviteCode;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is GuildInviteSample sample))
                    return false;
                if (InviteCode != sample.InviteCode)
                    return false;
                return true;
            }

            public override int GetHashCode() => HashCode.Combine(InviteCode);
        }
        #endif

        // \note Caching the options for the lifecycle of each bot
        protected BotOptions            _botOpts    = RuntimeOptionsRegistry.Instance.GetCurrent<BotOptions>();

        LifecycleState                  _lifecycleState;
        SessionNonceService             _sessionNonceService;
        BotClientDeviceGuidService      _sessionGuidService;
        BotClientCredentialsService     _sessionCredentialsService;
        ServerConnection                _serverConnection;
        BotMessageDispatcher            _messageDispatcher;
        FaultInjectingMessageTransport  _latestTransport;
        DateTime?                       _appOnBackgroundBackgroundEndsAt;       // if null, not on simulated background. Otherwise the point in time on which we resume
        bool                            _appOnBackgroundDropConnectionOnResume; // if set, the connection will be observed lost the moment we return from simulated background
        bool                            _appOnBackgroundAcceptErrorOnResume;    // if set, connection error is an accepted result when waking from simulated pause
        bool                            _droppingConnectionOnWrite;             // if set, the connection is pending death and is killed when data is written to it
        DateTime                        _dropConnectionDeadline;                // if connection is pending death, the final deadline unto which we wait for the triggering write
        TransportQosMonitor             _qosMonitor;

        List<MetaMessage>               _messageBuffer  = new List<MetaMessage>(); // Temporary message buffer (to avoid creating new lists)

        protected EntityId              _actualPlayerId;        // Actual PlayerId, as specified by the server (can happen if account is transferred)
        protected LogChannel            _logChannel;
        protected int                   _logicVersion;          // LogicVersion to use for session

        protected abstract IPlayerClientContext PlayerContext { get; }

        #if !METAPLAY_DISABLE_GUILDS
        protected GuildClient GuildClient => ClientStore.TryGetClient<GuildClient>(ClientSlotCore.Guild);
        protected IGuildClientContext GuildContext => GuildClient?.GuildContext;

        /// <summary>
        /// A sample of all guild invites.
        /// </summary>
        public static readonly ConcurrentBag<GuildInviteSample> GuildInviteSamples = new ConcurrentBag<GuildInviteSample>();
        #endif

        protected LocalizationLanguage ActiveLanguage { get; private set; }
        protected IMessageDispatcher MessageDispatcher => _messageDispatcher;

        protected ClientSessionNegotiationResources                     _sessionNegotiationResources = new ClientSessionNegotiationResources();
        protected ClientSessionStartResources                           _sessionStartResources;

        /// <summary>
        /// The set of active experiments the current player is a member of. An active experiment is an experiment that is
        /// neither disabled or in an invalid state, i.e. the experiment's config changes have been applied.
        /// </summary>
        protected MetaDictionary<PlayerExperimentId, ExperimentMembershipStatus> ActiveExperiments { get; private set; }

        protected virtual IMetaplaySubClient[] AdditionalSubClients => Array.Empty<IMetaplaySubClient>();
        protected MetaplayClientStore ClientStore { get; }

        public BotClientBase(EntityId entityId) : base(entityId)
        {
            // Log channel for logic
            _logChannel = new LogChannel("bot", _log, MetaLogger.MetaLogLevelSwitch);

            _messageDispatcher = new BotMessageDispatcher(_logChannel);
            _qosMonitor = new TransportQosMonitor(_logChannel);

            ActiveLanguage = new LocalizationLanguage(LanguageId.FromString("none"), ContentHash.None, new MetaDictionary<TranslationId, string>());
            _sessionNegotiationResources.ActiveLanguage = ActiveLanguage;

            ClientStore = new MetaplayClientStore(() => PlayerContext);

#if !METAPLAY_DISABLE_GUILDS
            // \todo: Use IntegrationRegistry for overriding guild client
            bool shouldAddDefaultGuildClient = AdditionalSubClients?.All(client => client.ClientSlot != ClientSlotCore.Guild) ?? true;
            if (shouldAddDefaultGuildClient)
                ClientStore.AddClient(new GuildClient());
#endif

            foreach (IMetaplaySubClient client in AdditionalSubClients)
                ClientStore.AddClient(client);
            IMetaplaySubClientServices clientServices = new BotSubClientServices(_messageDispatcher, ClientStore, _qosMonitor, _log);
            ClientStore.Initialize(clientServices);

#if !METAPLAY_DISABLE_GUILDS
            GuildClient.PhaseChanged += HarvestGuildInvitationSamples;
#endif
        }

        protected override void PreStart()
        {
            base.PreStart();

            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(5), _self, InitDeadlineReached.Instance, _self, _cancelTimers);
        }

        protected override void PostStop()
        {
            // Dispose server connection
            if (_serverConnection != null)
            {
                _serverConnection.Dispose();
                _serverConnection = null;
            }

            ClientStore.Dispose();

            base.PostStop();
        }

        protected override Task OnShutdown()
        {
            _sessionCredentialsService.OnConnectionClosed();
            return base.OnShutdown();
        }

        protected bool SendToServer(MetaMessage message)
        {
            return _serverConnection.EnqueueSendMessage(message);
        }

        protected override async Task Initialize()
        {
            _lifecycleState = LifecycleState.Ticking;

            // Tick timer
            StartPeriodicTimer(UpdateTickInterval, UpdateTickInterval, ActorTick.Instance);

            // Schedule session stop in 0.5..1.5 * expectedSessionDuration
            TimeSpan sessionDuration = _botOpts.ExpectedSessionDuration * (0.5 + new Random().NextDouble());
            _log.Verbose("Attempting to run game session for {SessionDuration}", sessionDuration);
            Context.System.Scheduler.ScheduleTellOnce(sessionDuration, _self, SessionEndTimeReached.Instance, _self, _cancelTimers);

            ServerEndpoint endpoint = new ServerEndpoint(
                _botOpts.ServerHost,
                _botOpts.ServerPort,
                _botOpts.EnableTls,
                _botOpts.CdnBaseUrl,
                _botOpts.ServerBackupGateways.Select(opt => opt.ToServerGateway()).ToList());

            // Establish connection to server

            _sessionNonceService = new SessionNonceService(appLaunchId: Guid.NewGuid());
            _sessionNonceService.NewSession();
            _sessionGuidService = new BotClientDeviceGuidService();

            // Use botId as playerId (or force all bots to use EntityId Player:000000000, if conflicts enabled)
            EntityId expectedPlayerId = EntityId.Create(EntityKindCore.Player, _botOpts.ForceConflictingPlayerId ? 0 : _entityId.Value);
            _sessionCredentialsService = new BotClientCredentialsService(expectedPlayerId);
            await _sessionCredentialsService.InitializeAsync();

            _serverConnection = new ServerConnection(
                _logChannel,
                new ServerConnection.Config()
                {
                    ConnectTimeout          = ConnectTimeout,
                    CommitIdCheckRule       = ClientServerCommitIdCheckRule.Disabled,
                    DeviceInfo              = new SessionProtocol.ClientDeviceInfo()
                    {
                        ClientPlatform = ClientPlatform.Unknown,
                        DeviceModel = "bot",
                        OperatingSystem = "bot",
                    },
                    LoginGamePayload        = CreateLoginRequestGamePayload(),
                    SessionStartGamePayload = CreateSessionStartRequestGamePayload(),
                    OverrideLocation        = null
                },
                endpoint:                       endpoint,
                nonceService:                   _sessionNonceService,
                guidService:                    _sessionGuidService,
                loginMethod:                    await _sessionCredentialsService.GetCurrentLoginMethodAsync(),
                createTransport:                CreateTransport,
                buildVersion:                   GetBuildVersion(),
                initialResourceProposal:        _sessionNegotiationResources.ToResourceProposal(),
                initialClientAppPauseStatus:    ClientAppPauseStatus.Running,
                getDebugDiagnostics:            (bool isSessionResumption) => null,
                earlyMessageFilterSync:         null,
                enableWatchdog:                 true,
                reportWatchdogViolation:        () => { },
                numFailedConnectionAttempts:    0);

            c_connectAttempts.Inc();
        }

        BuildVersion GetBuildVersion() => new BuildVersion(version: "0.0.1-bot", buildNumber: CloudCoreVersion.BuildNumber ?? "undefined", commitId: CloudCoreVersion.CommitId ?? "undefined");

        protected virtual Handshake.ILoginRequestGamePayload                CreateLoginRequestGamePayload()         => null;
        protected virtual ISessionStartRequestGamePayload                   CreateSessionStartRequestGamePayload()  => null;

        IMessageTransport CreateTransport(ServerGateway gateway)
        {
            TcpMessageTransport transport;
            if (gateway.EnableTls)
                transport = new TlsMessageTransport(_logChannel);
            else
                transport = new TcpMessageTransport(_logChannel);

            TcpMessageTransport.ConfigArgs transportConfig = transport.Config();

            transportConfig.GameMagic                  = MetaplayCore.Options.GameMagic;
            transportConfig.BuildVersion               = GetBuildVersion().Version;
            transportConfig.BuildNumber                = GetBuildVersion().BuildNumber;
            transportConfig.ClientVersion              = MetaplayCore.Options.ClientVersion;
            transportConfig.FullProtocolHash           = MetaSerializerTypeRegistry.Instance.FullProtocolHash;
            transportConfig.CommitId                   = GetBuildVersion().CommitId;
            transportConfig.ClientSessionConnectionNdx = _sessionNonceService.GetSessionConnectionIndex();
            transportConfig.ClientSessionNonce         = _sessionNonceService.GetSessionNonce();
            transportConfig.AppLaunchId                = _sessionNonceService.GetTransportAppLaunchId();
            transportConfig.LoginProtocolVersion       = MetaplayCore.LoginProtocolVersion;
            transportConfig.TargetHostname             = gateway.ServerHost;
            transportConfig.ConnectTimeout             = ConnectTimeout;

            transportConfig.ServerHostIPv4              = MetaplayHostnameUtil.GetV4V6SpecificHost(gateway.ServerHost, isIPv4: true);
            // IPv6 disabled for now
            //transportConfig.ServerHostIPv6            = MetaplayHostnameUtil.GetV4V6SpecificHost(gateway.ServerHost, isIPv4: false);
            transportConfig.ServerPort                  = gateway.ServerPort;

            // Some random to avoid all queries expiring at the same time
            transportConfig.DnsCacheMaxTTL              = TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(2.0 * (new Random()).NextDouble());

            _latestTransport = new FaultInjectingMessageTransport(transport);

            return _latestTransport;
        }

        protected override void RegisterHandlers()
        {
            Receive<InitDeadlineReached>(ReceiveInitDeadlineReached);
            Receive<SessionEndTimeReached>(ReceiveSessionEndTimeReached);
            base.RegisterHandlers();
        }

        void ReceiveSessionEndTimeReached(SessionEndTimeReached request)
        {
            c_sessionTimeouts.Inc();
            _log.Debug("Session timeout, requesting shutdown");
            RequestShutdown();
        }

        void ReceiveInitDeadlineReached(InitDeadlineReached request)
        {
            if (_lifecycleState == LifecycleState.NotStarted)
            {
                _log.Warning("BotClient did not start a session after waking up. Shutting down.");
                RequestShutdown();
            }
        }

        [CommandHandler]
        async Task HandleActorTick(ActorTick tick)
        {
            if (_lifecycleState != LifecycleState.Ticking)
                return;

            DateTime        now                     = DateTime.UtcNow;
            RandomPCG       rnd                     = RandomPCG.CreateNew();
            bool            acceptConnectionError   = false;
            bool            resumedFromPause        = false;

            // Simulate app put to background
            if (_appOnBackgroundBackgroundEndsAt.HasValue)
            {
                if (now < _appOnBackgroundBackgroundEndsAt.Value)
                {
                    // app is on background, we are paused.
                    return;
                }
                else
                {
                    // app just comes on focus after being on background.
                    // ~ OnApplicationPause(false)
                    _log.Debug("Resuming from pause");
                    _appOnBackgroundBackgroundEndsAt = null;

                    SendToServer(new ClientLifecycleHintUnpausing(clientTimestamp: DateTimeOffset.Now));
                    resumedFromPause = true;

                    // Simulate waking up the connection thread ...
                    if (_appOnBackgroundDropConnectionOnResume)
                    {
                        // ... and noticing the connection gone.
                        _latestTransport?.InjectError(new StreamMessageTransport.StreamClosedError());

                        // tolerate if can be tolerated
                        acceptConnectionError = _appOnBackgroundAcceptErrorOnResume;
                    }
                    else
                    {
                        // .. and everthing is fine
                        _latestTransport.Resume();
                    }
                }
            }
            else if (rnd.NextDouble() < _botOpts.PutOnBackgroundProbabilityPerTick)
            {
                // We model pausing an application on two axis: time and impact.
                // Time:
                // * is the pause long or short.
                // Impact of the pause:
                // * low impact, app remains active: low-level connection remains alive, and session remains alive.
                // * medium impact, app is suspended: low-level connection gets suspended, but wakes on resume
                // * high impact, app is suspended and connection is lost: low-level connection gets suspended, dies, and is observed dead on resume.
                //
                // We accept loss of an session only with long pauses when app is suspended and connection is lost, as that case cannot be discriminated from
                // app being closed.

                bool isLongPause                = false;//rnd.NextBool();
                int impactLevel                 = rnd.NextInt(3);
                TimeSpan pauseBaseDuration      = isLongPause ? (_botOpts.PutOnBackgroundLongPause) : (_botOpts.PutOnBackgroundShortPause);

                SendToServer(new ClientLifecycleHintPausing(pauseBaseDuration, "bot-pause", clientTimestamp: DateTimeOffset.Now));

                if (impactLevel == 0)
                {
                    // keep 'em active
                    _appOnBackgroundDropConnectionOnResume = false;
                    _appOnBackgroundAcceptErrorOnResume = false;
                }
                else if (impactLevel == 1)
                {
                    // suspend
                    _latestTransport.Halt();
                    _appOnBackgroundDropConnectionOnResume = false;
                    _appOnBackgroundAcceptErrorOnResume = false;
                }
                else
                {
                    // drop connection silently.
                    _latestTransport.Halt();
                    _latestTransport.Dispose();

                    _appOnBackgroundDropConnectionOnResume = true;
                    _appOnBackgroundAcceptErrorOnResume = isLongPause; // only if also long pause
                }

                // was put on background
                // ~ OnApplicationPause(true)
                _log.Debug("Starting simulated application pause. ImpactLevel={ImpactLevel}. IsLongPause={IsLongPause}", impactLevel, isLongPause);
                _appOnBackgroundBackgroundEndsAt = now + pauseBaseDuration;
                return;
            }

            // simulate random connection losses
            else if (_droppingConnectionOnWrite && (_latestTransport.NumHaltedSendMessages > 0 || now > _dropConnectionDeadline))
            {
                _log.Debug("Write pending, triggering connection drop now.");
                _droppingConnectionOnWrite = false;
                _latestTransport?.InjectError(new StreamMessageTransport.StreamClosedError());
            }
            else if (!_droppingConnectionOnWrite && rnd.NextDouble() < _botOpts.DropConnectionProbabilityPerTick)
            {
                // Occasionally, close message transport in order to exercise session resumption code. We have a session, so
                // this should always be tolerated.
                // In realistic cases, connection drop is only observed when we try to use the transport. So let's halt here and
                // drop the connection when we notice any sent messages in the queue. Add a relative large deadline to simulate
                // rare pings.
                _log.Debug("Simulated conncection drop primed. Dropping on next write");

                _droppingConnectionOnWrite = true;
                _dropConnectionDeadline = now + TimeSpan.FromSeconds(5);
                _latestTransport.Halt();
            }

            // Handle any incoming messages
            MessageTransport.Error connectionError = _serverConnection.ReceiveMessages(_messageBuffer);
            _ = _qosMonitor.ProcessMessages(_messageBuffer, _serverConnection);
            foreach (MetaMessage msg in _messageBuffer)
            {
                if (await FilterNetworkMessageAsync(msg))
                {
                    await OnNetworkMessage(msg);
                }
                _messageDispatcher.OnReceiveMessage(msg);
            }
            _messageBuffer.Clear();

            // Check for connection lost
            if (connectionError != null)
            {
                if (acceptConnectionError)
                {
                    // connection loss was expected, no need do anything
                }
                else if (connectionError is ServerConnection.SessionForceTerminatedError sessionForceTerminate && sessionForceTerminate.Reason is SessionForceTerminateReason.ReceivedAnotherConnection)
                {
                    _log.Info("Session force terminated due to another client logging in with same credentials");
                }
                else
                {
                    string reason = connectionError.GetType().Name;
                    c_connectionsFailed.WithLabels(reason, GetCurrentStateLabel()).Inc();

                    // Print error-specific messages for some well-known errors.
                    string errorDescription;
                    switch (connectionError)
                    {
                        case WireMessageTransport.WireFormatError err:
                        {
                            errorDescription = $"WireFormatError: {NetworkErrorLoggingUtil.GetMinimalDescription(err.DecodeException)}";
                            break;
                        }
                        case StreamMessageTransport.StreamIOFailedError err:
                        {
                            errorDescription = $"StreamIOFailedError: {NetworkErrorLoggingUtil.GetMinimalDescription(err.Exception)}";
                            break;
                        }
                        case StreamMessageTransport.StreamExecutorError err:
                        {
                            errorDescription = $"StreamExecutorError: {NetworkErrorLoggingUtil.GetMinimalDescription(err.Exception)}";
                            break;
                        }
                        case ServerConnection.SessionStartFailed err:
                        {
                            errorDescription = $"SessionStartFailed: {err.Message}";
                            break;
                        }
                        case ServerConnection.SessionError err:
                        {
                            errorDescription = $"SessionError: {err.Reason}";
                            break;
                        }
                        case ServerConnection.ServiceFailureError err:
                        {
                            errorDescription = $"ServiceFailureError: {NetworkErrorLoggingUtil.GetMinimalDescription(err.Error)}";
                            break;
                        }
                        default:
                        {
                            errorDescription = reason;
                            break;
                        }
                    }
                    _log.Error("Connection to server failed: {Description}", errorDescription);

                    // If the Connection was refused with TCP ConnectionRefused, it means there is no server process
                    // right now. Throttle by waiting a bit before dying. Otherwise, a new replacement bot would be
                    // spawned quickly and would futilely get TCP rejected again, spamming logs.
                    if (connectionError is TcpMessageTransport.ConnectionRefused)
                        await Task.Delay(TimeSpan.FromSeconds(5));
                }

                _messageDispatcher.SetConnection(null);
                ClientStore.OnDisconnected();
                ClientStore.OnSessionStop();

                _lifecycleState = LifecycleState.Terminated;
                RequestShutdown();
            }

            bool isOnPause = _appOnBackgroundBackgroundEndsAt.HasValue;
            if (!isOnPause && _lifecycleState == LifecycleState.Ticking)
            {
                if (rnd.NextDouble() < _botOpts.DummyIncidentReportProbabilityPerTick
                    && _incidentReportThrottle.TryTrigger(DateTime.UtcNow))
                {
                    _log.Debug("Sending dummy incident report");
                    StartUploadIncidentReport(CreateDummyIncidentReport());
                }

                // \todo: should we check last update was not too far away, like MetaplaySDK does on client.
                ClientStore.EarlyUpdate();

                MetaTime gameNow = MetaTime.Now;

                ClientStore.UpdateLogic(gameNow);

                UpdateInAppPurchases();

                await OnUpdate();
            }

            if (resumedFromPause)
                SendToServer(new ClientLifecycleHintUnpaused(clientTimestamp: DateTimeOffset.Now));
        }

        /// <summary>
        /// Returns true if message should be delivered to <see cref="OnNetworkMessage(MetaMessage)"/>.
        /// </summary>
        protected virtual async ValueTask<bool> FilterNetworkMessageAsync(MetaMessage message)
        {
            switch (message)
            {
                case ConnectedToServer connected:
                    _log.Verbose("Connected to server");
                    c_connectionsEstablished.Inc();
                    c_connectDuration.Observe(_serverConnection.Statistics.DurationToConnected.TotalSeconds);
                    return false;

                case DisconnectedFromServer disconnected:
                    _log.Verbose("Received DisconnectedFromServer, shutting down..");
                    RequestShutdown();
                    return false;

                case ConnectionHandshakeFailure handshakeFailure:
                    _log.Error("Received: {Message}", PrettyPrint.Compact(handshakeFailure));
                    RequestShutdown();
                    return false;

                case SessionProtocol.SessionStartSuccess success:
                    Tell(_shard, BotStartedSession.Instance);
                    _log.Debug("Login success with new session");
                    c_loginDuration.Observe(_serverConnection.Statistics.DurationToLoginSuccess.TotalSeconds);

                    // Store actual PlayerId (it should almost always be same as _expectedPlayerId, but transferring accounts can change it)
                    _actualPlayerId = success.PlayerId;
                    if (!_actualPlayerId.IsOfKind(EntityKindCore.Player))
                        throw new InvalidDataException($"Got invalid PlayerId '{_actualPlayerId}' for bot");
                    else if (_actualPlayerId != _sessionCredentialsService.ExpectedPlayerId)
                        _log.Warning("Actual server-provided {ActualPlayerId} differs from expected {ExpectedPlayerId}", _actualPlayerId, _sessionCredentialsService.ExpectedPlayerId);

                    // \todo [petri] duplicate in Authenticator.cs, refactor to common place
                    const ulong NumReservedBotIds = 1UL << 32; // reserve 4 billion lowest ids for bots to use
                    if (_actualPlayerId.Value >= NumReservedBotIds)
                        _log.Warning("Non-bot id {PlayerId} given to a bot", _actualPlayerId);

                    // Activate language
                    ActiveLanguage = _sessionNegotiationResources.ActiveLanguage;

                    // Activate game configs
                    Func<ConfigArchive, (GameConfigSpecializationPatches, GameConfigSpecializationKey)?, ISharedGameConfig> gameConfigImporter = (ConfigArchive archive, (GameConfigSpecializationPatches, GameConfigSpecializationKey)? specialization) =>
                    {
                        if (specialization is (GameConfigSpecializationPatches patches, GameConfigSpecializationKey specializationKey))
                        {
                            return BotGameConfigProvider.Instance.GetSpecializedGameConfig(archive, patches, specializationKey);
                        }
                        else
                        {
                            return BotGameConfigProvider.Instance.GetSpecializedGameConfig(archive, null, default);
                        }
                    };
                    _sessionStartResources = ClientSessionStartResources.SpecializeResources(success, _sessionNegotiationResources, gameConfigImporter);
                    _logicVersion = _sessionStartResources.LogicVersion;

                    ActiveExperiments = success.PlayerState.ActiveExperiments.ToMetaDictionary(experiment => experiment.ExperimentId, experiment => ExperimentMembershipStatus.FromSessionInfo(experiment));

                    ISharedGameConfig playerGameConfig = _sessionStartResources.GetConfigForPlayer(success.PlayerState);
                    IPlayerModelBase playerModel = success.PlayerState.PlayerModel.Deserialize(playerGameConfig, _logicVersion);
                    if (success.PlayerId != playerModel.PlayerId)
                        throw new InvalidOperationException("Player id in state and protocol do not match");

                    _messageDispatcher.SetConnection(_serverConnection);

                    // Initialize player state services
                    playerModel.LogicVersion        = _logicVersion;
                    playerModel.GameConfig          = playerGameConfig;
                    playerModel.Log                 = _logChannel;
                    playerModel.ClientListenerCore  = this;
                    HandleStartSession(success, playerModel, playerGameConfig);

                    ClientStore.OnSessionStart(success, _sessionStartResources);

                    // Set name for the bot
                    if (playerModel.Stats.TotalLogins == 1)
                        SendToServer(new PlayerChangeOwnNameRequest(GenerateRandomPlayerName(new Random())));
                    return true;

                case SessionProtocol.SessionStartFailure failed:
                {
                    switch (failed.Reason)
                    {
                        case SessionProtocol.SessionStartFailure.ReasonCode.InternalError:
                        {
                            _log.Error("Session start failed: {Reason}", failed.Reason);
                            break;
                        }
                    }
                    return false;
                }

                case SessionProtocol.SessionStartResourceCorrection resourceCorrection:
                {
                    _log.Debug("Session resources are out of date, applying correction and retrying");
                    await ApplyResourceCorrectionAsync(resourceCorrection.ResourceCorrection);
                    _serverConnection.RetrySessionStart(_sessionNegotiationResources.ToResourceProposal(), ClientAppPauseStatus.Running);
                    return false;
                }

                case SessionProtocol.SessionResumeSuccess success:
                    _log.Debug("Session resumed");
                    return false;

                case SessionProtocol.SessionResumeFailure failed:
                    _log.Warning("Session resume failed.");
                    return false;

                case Handshake.ClientHelloAccepted helloAccepted:
                {
                    if (helloAccepted.ServerOptions.GameTimeOffset != MetaDuration.Zero)
                        _log.Info("Using game time offset {Offset}", helloAccepted.ServerOptions.GameTimeOffset);

                    MetaTime.DebugTimeOffset = helloAccepted.ServerOptions.GameTimeOffset;
                    return false;
                }

                case Handshake.LoginSuccessResponse _:
                    return false;

                case SessionAcknowledgementMessage _:
                    // Explicitly do nothing so we don't print a warning
                    return false;

                case PlayerEnqueueSynchronizedServerAction enqueueSynchronizedServerAction:
                    PlayerContext.ExecuteServerAction(enqueueSynchronizedServerAction);
                    return false;

                case EntityServerToClientEnvelope envelope:
                {
                    // Entity channel messages are handled via dedicated message dispatcher
                    return false;
                }

                case EntitySwitchedMessage switched:
                {
                    // Affected subclients handle this automatically
                    return false;
                }

                case MessageTransportLatencySampleMessage sample:
                {
                    // Affected subclient handles this automatically.
                    return false;
                }

                case MessageTransportInfoWrapperMessage infoWrapper:
                {
                    switch (infoWrapper.Info)
                    {
                        case ServerConnection.SessionConnectionErrorLostInfo connectionLost:
                        {
                            TimeSpan maxSessionResumptionDuration = TimeSpan.FromSeconds(5);
                            DateTime deadlineAt = connectionLost.Attempt.StartTime + maxSessionResumptionDuration;
                            if (ReconnectScheduler.TryGetDurationToSessionResumeAttempt(connectionLost.Attempt, deadlineAt, out TimeSpan durationTillReconnect))
                            {
                                ScheduleExecuteOnActorContext(durationTillReconnect, () =>
                                {
                                    _serverConnection.ResumeSessionAfterConnectionDrop();
                                });
                            }
                            else
                            {
                                _serverConnection.AbortSessionAfterConnectionDrop();
                            }
                            break;
                        }
                    }
                    // filter out to avoid spammy logging
                    return false;
                }

                case PlayerIncidentUploadUrlResponse uploadUrlResponse:
                    if (_pendingIncidentUploadUrlResponse.Remove(uploadUrlResponse.QueryId, out TaskCompletionSource<PlayerIncidentUploadUrlResponse> tcs))
                        tcs.SetResult(uploadUrlResponse);
                    else
                        _log.Error($"Got {nameof(PlayerIncidentUploadUrlResponse)} for unknown query {{QueryId}}", uploadUrlResponse.QueryId);

                    return false;

#if !METAPLAY_DISABLE_GUILDS
                case GuildTimelineUpdateMessage:
                case GuildCreateResponse:
                case GuildJoinResponse:
                case GuildDiscoveryResponse:
                case GuildTransactionResponse:
                case GuildSearchResponse:
                case GuildViewResponse:
                case GuildViewEnded:
                case GuildSwitchedMessage:
                case GuildCreateInvitationResponse:
                case GuildInspectInvitationResponse:
                    // Handled by GuildClient, so ignore here so we don't print a warning.
                    // \todo Ignore automatically if handled by a sub-client.
                    return false;
#endif

                case Handshake.OperationStillOngoing:
                    return false;

                case PlayerAckIncidentReportUpload:
                    return false;
            }

            return true;
        }

        protected abstract void HandleStartSession(SessionProtocol.SessionStartSuccess success, IPlayerModelBase playerModelBase, ISharedGameConfig gameConfig);

        #if !METAPLAY_DISABLE_GUILDS

        /// <summary>
        /// Handles response to GuildDiscoveryRequest by attempting to join a random guild.
        /// </summary>
        protected void DefaultHandleGuildDiscoveryResult(GuildDiscoveryResponse response)
        {
            if (response.GuildInfos.Count > 0 && GuildClient.Phase == GuildClientPhase.NoGuild)
            {
                int ndx = (new Random()).Next(response.GuildInfos.Count);
                GuildClient.BeginJoinGuild(response.GuildInfos[ndx].GuildId, onCompletion: null);
            }
        }

        /// <summary>
        /// Handles response to GuildDiscoveryRequestby attempting to join a random guild.
        /// </summary>
        protected void DefaultHandleGuildSearchResult(GuildSearchResponse response)
        {
            if (response.IsError)
            {
                _log.Warning("Guild search failed due to a server error.");
                return;
            }

            if (response.GuildInfos.Count > 0 && GuildClient.Phase == GuildClientPhase.NoGuild)
            {
                int ndx = (new Random()).Next(response.GuildInfos.Count);
                GuildClient.BeginJoinGuild(response.GuildInfos[ndx].GuildId, onCompletion: null);
            }
        }

        /// <summary>
        /// Handles response to GuildInspectInvitationRequest. Defaults to attempting to join a random guild.
        /// </summary>
        protected void DefaultHandleGuildInspectInvitationResult(GuildInviteInfo inviteInfo)
        {
            if (inviteInfo == null)
                return;

            if (GuildClient.Phase != GuildClientPhase.NoGuild)
                return;

            GuildDiscoveryInfoBase guildDiscoveryInfo = inviteInfo.GuildInfo;
            GuildClient.BeginJoinGuildWithInviteCode(guildDiscoveryInfo.GuildId, inviteInfo.InviteId, inviteInfo.InviteCode, onCompletion: null);
        }

        /// <summary>
        /// Handles response to BeginCreateGuildInviteCode. Defaults to adding the invite into <see cref="GuildInviteSamples"/>.
        /// </summary>
        protected void DefaultHandleGuildCreateInvitationResult(GuildCreateInvitationResult result)
        {
            if (result.Status != GuildCreateInvitationResponse.StatusCode.Success)
            {
                _log.Debug("GuildCreateInvitation request failed: {Reason}", result.Status);
            }
            else
            {
                GuildInviteSamples.Add(new GuildInviteSample(result.InviteState.InviteCode));
            }
        }

        void HarvestGuildInvitationSamples()
        {
            if (GuildContext == null)
                return;
            foreach (EntityId playerId in GuildContext.CommittedModel.EnumerateMembers())
            {
                if (!GuildContext.CommittedModel.TryGetMember(playerId, out GuildMemberBase member))
                    continue;
                foreach (GuildInviteState invite in member.Invites.Values)
                    GuildInviteSamples.Add(new GuildInviteSample(invite.InviteCode));
            }
        }

        #endif

        protected async Task ApplyResourceCorrectionAsync(SessionProtocol.SessionResourceCorrection correction)
        {
            if (correction.LanguageUpdate.HasValue)
            {
                SessionProtocol.SessionResourceCorrection.LanguageUpdateInfo languageUpdate = correction.LanguageUpdate.Value;
                _sessionNegotiationResources.ActiveLanguage = await BotLocalizationLanguageProvider.Instance.GetAsync(languageUpdate.ActiveLanguage, languageUpdate.LocalizationVersion);
            }

            foreach (SessionProtocol.SessionResourceCorrection.ConfigArchiveUpdateInfo configUpdate in correction.ConfigUpdates)
            {
                ConfigArchive archive = await BotGameConfigProvider.Instance.GetBaselineConfigArchiveAsync(configUpdate);
                _sessionNegotiationResources.ConfigArchives[archive.Version] = archive;
            }

            foreach (SessionProtocol.SessionResourceCorrection.ConfigPatchesUpdateInfo configPatches in correction.PatchUpdates)
            {
                GameConfigSpecializationPatches patch = await BotGameConfigProvider.Instance.GetSpecializationPatchesAsync(configPatches.PatchesVersion);
                _sessionNegotiationResources.PatchArchives[patch.Version] = patch;
            }
        }

        public static string GetRandomSubstring(Random random, string sourceString)
        {
            int startNdx = random.Next(sourceString.Length);
            int len = random.Next(sourceString.Length - startNdx) + 1;
            return sourceString.Substring(startNdx, len);
        }

        static readonly string[] _botNameFormats    = new string[] { "[Wrapper] [Adjective] [Wrapper] [Creature] [Wrapper]", "[Wrapper][Adjective][Creature][Extra][Wrapper]", "[Adjective][Creature]", "[Adjective][Creature][Number]", "[Adjective] [Creature] [Extra]", "[ADJECTIVE] [Extra] [CREATURE]", "[Adjective] [Wrapper] [Creature] [Wrapper] [Number]" };
        static readonly string[] _botNameAdjectives = new string[] { "abashed", "abrasive", "adamant", "admiral", "aloof", "autoritarian", "axiomatic", "barbarous", "beginner", "brittle", "casual", "cranky", "crude", "dapper", "dark", "deceptive", "digital", "draconian", "eager", "easy", "erratic", "fake", "friendly", "genuine", "heady", "helpful", "idle", "insolent", "invisible", "irksome", "irresponsible", "magical", "mannered", "master", "native", "naïve", "nebulous", "obtuse", "professional", "provocative", "rare", "shoddy", "slow", "smart", "sparkling", "special", "spontaneous", "swollen", "talkative", "tenacious", "viral", "weak", "wild", "wise", "witty" };
        static readonly string[] _botNameCreatures  = new string[] { "angel", "ant", "armadillo", "bat", "bear", "bee", "beetle", "blobfish", "boar", "bull", "camel", "chinchilla", "cobra", "cow", "crab", "deer", "dolphin", "dragon", "eagle", "elephant", "flamingo", "fox", "gecko", "giraffe", "goat", "goblin", "grasshopper", "hobbit", "hyena", "kong", "liger", "lizard", "manatee", "mustang", "narwhal", "octopus", "owl", "panda", "pangolin", "parrot", "pegasus", "penguin", "pig", "pigeon", "rabbit", "rat", "raven", "salamander", "salmon", "shark", "sloth", "spider", "squid", "squirrel", "turtle", "unicorn", "wasp", "whale", "wolf", "wolverine", "wombat", "zebra" };
        static readonly string[] _botNameWrappers   = new string[] { "-", "==", "^", "_", "+", "‼️", "❤️", "☠️", "⭐️⭐️", "✨", "⚡️" };
        static readonly string[] _botNameExtras     = new string[] { "🔥", "💯", "🙌", "🚀", "🎩" };

        public static string GenerateRandomPlayerName(Random random)
        {
            string name;
            do
            {
                string selectedFormat = _botNameFormats[random.Next(_botNameFormats.Length)];
                string selectedAdjectiveName = _botNameAdjectives[random.Next(_botNameAdjectives.Length)];
                string selectedCreatureName = _botNameCreatures[random.Next(_botNameCreatures.Length)];
                string selectedWrapper = _botNameWrappers[random.Next(_botNameWrappers.Length)];
                string selectedExtra = _botNameExtras[random.Next(_botNameExtras.Length)];
                string selectedNumber = random.Next(99).ToString(CultureInfo.InvariantCulture.NumberFormat);
                name = "🤖 " + selectedFormat
                    .Replace("[Adjective]", CultureInfo.CurrentCulture.TextInfo.ToTitleCase(selectedAdjectiveName))
                    .Replace("[ADJECTIVE]", selectedAdjectiveName.ToUpper(CultureInfo.CurrentCulture))
                    .Replace("[Creature]", CultureInfo.CurrentCulture.TextInfo.ToTitleCase(selectedCreatureName))
                    .Replace("[CREATURE]", selectedCreatureName.ToUpper(CultureInfo.CurrentCulture))
                    .Replace("[Extra]", selectedExtra)
                    .Replace("[Wrapper]", selectedWrapper)
                    .Replace("[Number]", selectedNumber);
            }
            while (name.Length < 5 || name.Length > 20);
            return name;
        }

        static readonly string[] _guildNameFormats  = new string[] { "[PREFIX] [TYPE]s", "The [PREFIX] [TYPE]s", "Society of [PREFIX] [TYPE]s", "[PREFIX] [TYPE] Guild", "[PREFIX] [TYPE] Killers", "[PREFIX] [TYPE]s Gang" };
        static readonly string[] _guildNamePrefixes = new string[] { "Dark", "Angry", "Clever", "Happy", "Wild", "Powerful", "Incredible", "Careless", "Bad", "Mean", "Brutal", "Epic", "Classy", "Optimal", "Casual", "Serious", "Stealthy", "Junior", "AI", "Super", "Mighty" };
        static readonly string[] _guildNameTypes    = new string[] { "Angel", "Zombie", "Titan", "Duck", "Coder", "Leopard", "Quagga", "Wasp", "Giant", "Droid", "Grinder", "Killer", "Farmer", "Cleaner", "DJ", "Gangster", "PK", "Adventurer", "Trader", "Miner", "Soldier", "Professional", "Gamer" };

        public static string GenerateRandomGuildName(Random random)
        {
            string selectedFormat = _guildNameFormats[random.Next(_guildNameFormats.Length)];
            string selectedPrefix = _guildNamePrefixes[random.Next(_guildNamePrefixes.Length)];
            string selectedType = _guildNameTypes[random.Next(_guildNameTypes.Length)];
            return selectedFormat
                .Replace("[PREFIX]", selectedPrefix)
                .Replace("[TYPE]", selectedType);
        }

        static readonly string[] _guildLoremTexts = new string[] { "Lorem ipsum dolor sit amet.", "Consectetur adipiscing elit.", "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.", "Ut enim ad minim veniam.", "Quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.", "Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur.", "Excepteur sint occaecat cupidatat non proident.", "Sunt in culpa qui officia deserunt mollit anim id est laborum." };

        public static string GenerateRandomGuildDescription(Random random)
        {
            return string.Join(" ", Enumerable.Range(0, 2).Select(_ => _guildLoremTexts[random.Next(_guildLoremTexts.Length)]));
        }

        #region Dummy incident reports

        PeriodThrottle _incidentReportThrottle = new PeriodThrottle(TimeSpan.FromSeconds(60), maxEventsPerDuration: 5, DateTime.UtcNow);
        int _nextIncidentUploadUrlRequestId = 0;
        Dictionary<int, TaskCompletionSource<PlayerIncidentUploadUrlResponse>> _pendingIncidentUploadUrlResponse = new();

        PlayerIncidentReport CreateDummyIncidentReport()
        {
            MetaTime now = MetaTime.Now;

            List<ClientLogEntry> logEntries = new List<ClientLogEntry>();
            for (int i = 0; i < 10; i++)
            {
                logEntries.Add(new ClientLogEntry(
                    timestamp: now - MetaDuration.FromSeconds(1),
                    level: ClientLogEntryType.Log,
                    message: Invariant($"botclient dummy message {i}"),
                    stackTrace: Invariant($"botclient dummy stack trace {i}")));
            }

            RandomPCG rnd = RandomPCG.CreateNew();

            string randomTag = rnd.NextInt(2).ToString(CultureInfo.InvariantCulture);

            PlayerIncidentReport report = new UnhandledExceptionError(
                id: PlayerIncidentUtil.EncodeIncidentId(now, rnd.NextULong()),
                occurredAt: now,
                logEntries: logEntries,
                systemInfo: new UnitySystemInfo(),
                platformInfo: new UnityPlatformInfo(),
                applicationInfo: new IncidentApplicationInfo(
                    buildVersion:                   GetBuildVersion(),
                    deviceGuid:                     _sessionGuidService.TryGetDeviceGuid(),
                    activeLanguage:                 ActiveLanguage.LanguageId.Value,
                    highestSupportedLogicVersion:   MetaplayCore.Options.ClientLogicVersion,
                    environmentId:                  "BotDummyEnvironmentId" /* IEnvironmentConfigProvider.Get().Id */), // \todo Enable IEnvironmentConfigProvider here when it's supported in bot client.
                gameConfigInfo: new IncidentGameConfigInfo(ContentHash.None, ContentHash.None, new List<ExperimentVariantPair>()),
                exceptionName: "BotclientDummyExceptionName",
                exceptionMessage: $"Botclient dummy exception message {randomTag}",
                stackTrace: $"Botclient dummy exception stack trace {randomTag}");

            return report;
        }

        /// <summary>
        /// Start uploading the incident to the server.
        /// The incident may be uploaded either with a HTTP (S3) upload or a simple <see cref="PlayerUploadIncidentReport"/> session message.
        /// </summary>
        void StartUploadIncidentReport(PlayerIncidentReport report)
        {
            // \note Not awaiting. See comment on the called method.
            _ = InternalStartUploadIncidentReport(report);
        }

        /// <summary>
        /// Implementation for <see cref="StartUploadIncidentReport"/>, separated for clarity to hide the returned task:
        /// The returned task completes when the incident report has been uploaded;
        /// however, you should not block the bot actor's mailbox processing to await this task.
        /// Doing so will deadlock because an internal task (via _pendingIncidentUploadUrlResponse) is
        /// fulfilled in the bot's mailbox processing.
        /// </summary>
        async Task InternalStartUploadIncidentReport(PlayerIncidentReport report)
        {
            RandomPCG rnd = RandomPCG.CreateNew();

            byte[] payloadToUpload = PlayerIncidentUtil.CompressIncidentForNetworkDelivery(report);

            // 50% chance to try to upload via HTTP, and otherwise (or as fallback if HTTP fails) upload with PlayerUploadIncidentReport session message.

            string uploadUrl;

            if (rnd.NextBool())
            {
                // Try to request HTTP upload url.
                // Note that server can still refuse and uploadUrl can be set to null.

                int responseId = _nextIncidentUploadUrlRequestId++;
                _pendingIncidentUploadUrlResponse.Add(responseId, new TaskCompletionSource<PlayerIncidentUploadUrlResponse>());
                SendToServer(new PlayerIncidentUploadUrlRequest(responseId, report.IncidentId, payloadToUpload.Length));

                PlayerIncidentUploadUrlResponse urlResponse = await _pendingIncidentUploadUrlResponse[responseId].Task;

                if (urlResponse.AlreadyUploaded)
                {
                    _log.Error($"Got {nameof(urlResponse.AlreadyUploaded)}=true in {nameof(PlayerIncidentUploadUrlResponse)}, which is not expected as the bot does not retry uploads.");
                    return;
                }

                uploadUrl = urlResponse.Url;
            }
            else
            {
                // Don't even try HTTP upload, just do PlayerUploadIncidentReport session message.
                uploadUrl = null;
            }

            if (uploadUrl != null)
            {
                try
                {
                    HttpResponseMessage putResponse = await s_httpClient.PutAsync(uploadUrl, new ByteArrayContent(payloadToUpload));
                    if (putResponse.IsSuccessStatusCode)
                    {
                        _messageDispatcher.SendMessage(new PlayerUploadedIncidentReportToUrl());
                        return;
                    }

                    _log.Warning("Failed to upload incident to S3: {Error}", putResponse.StatusCode);
                }
                catch (Exception ex)
                {
                    _log.Warning("Failed to upload incident to S3: {Error}", ex);
                }
            }

            // Upload via normal path, either if dice roll said so or the HTTP path was attempted but failed.
            SendToServer(new PlayerUploadIncidentReport(report.IncidentId, payloadToUpload));
        }

        #endregion

        #region IAP

        HashSet<InAppProductId> _pendingDynamicPurchases = new();

        protected void StartFakeInAppPurchase(InAppProductId productId)
        {
            if (PlayerContext == null)
            {
                _log.Error("Can't start fake IAP purchase until session has started");
                return;
            }

            IPlayerModelBase player = (IPlayerModelBase)PlayerContext.Model;
            InAppProductInfoBase productInfo = player.GameConfig.InAppProducts.GetValueOrDefault(productId) ?? throw new ArgumentException($"No such product in InAppProducts game config: {productId}", nameof(productId));
            RandomPCG rnd = RandomPCG.CreateNew();

            // \todo Some duplicate implementation in client-side utilities (e.g. FakeStore.cs), could deduplicate

            string transactionId = $"botFakeTxn_{rnd.NextUInt()}";
            string platformProductId = productInfo.DevelopmentId;
            string receiptJson = JsonSerialization.SerializeToString(
                new
                {
                    productId               = platformProductId,
                    transactionId           = transactionId,
                    originalTransactionId   = transactionId,
                    paymentType             = InAppPurchasePaymentType.Normal,
                });
            string signature = Util.ComputeSHA1(receiptJson);
            string receiptBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(receiptJson));

            PlayerContext.ExecuteAction(new PlayerInAppPurchased(InAppPurchaseEvent.ForDevelopment(transactionId: transactionId, productId, platformProductId: platformProductId, receipt: receiptBase64, signature: signature)));
        }

        void UpdateInAppPurchases()
        {
            if (PlayerContext == null)
                return;

            UpdatePurchasePreparationTracking();
            UpdatePendingInAppPurchases();
        }

        void UpdatePurchasePreparationTracking()
        {
            IPlayerModelBase player = (IPlayerModelBase)PlayerContext.Model;

            // \todo This is modified copypaste from client-side IAPManager.cs, deduplicate?

            List<InAppProductId> pendingDynamicPurchasesToDelete = null;
            foreach (InAppProductId productId in _pendingDynamicPurchases)
            {
                if (player.PendingDynamicPurchaseContents.TryGetValue(productId, out PendingDynamicPurchaseContent pendingContent)
                    && pendingContent.Status == PendingDynamicPurchaseContentStatus.ConfirmedByServer)
                {
                    StartFakeInAppPurchase(productId);
                    if (pendingDynamicPurchasesToDelete == null)
                        pendingDynamicPurchasesToDelete = new List<InAppProductId>();
                    pendingDynamicPurchasesToDelete.Add(productId);
                }
            }
            if (pendingDynamicPurchasesToDelete != null)
            {
                foreach (InAppProductId productId in pendingDynamicPurchasesToDelete)
                    _pendingDynamicPurchases.Remove(productId);
            }
        }

        void UpdatePendingInAppPurchases()
        {
            IPlayerModelBase player = (IPlayerModelBase)PlayerContext.Model;

            // \todo This is modified copypaste from client-side IAPManager.cs, deduplicate?

            // Poll pending IAP purchases, and handle those that need handling

            // \note Postponing reactions to avoid mutating PendingInAppPurchases while traversing it:
            List<Action> completionActions = null;
            foreach (InAppPurchaseEvent purchaseEvent in player.PendingInAppPurchases.Values)
            {
                switch (purchaseEvent.Status)
                {
                    case InAppPurchaseStatus.PendingValidation:
                        // Do nothing, still waiting for server validation
                        break;

                    case InAppPurchaseStatus.ValidReceipt:
                        // Has been validated by server, now we can claim and confirm it
                        if (completionActions == null)
                            completionActions = new List<Action>();
                        completionActions.Add(() =>
                        {
                            _log.Info("Claiming purchase {TransactionId} of product {ProductId}", purchaseEvent.TransactionId, purchaseEvent.ProductId);
                            PlayerContext.ExecuteAction(new PlayerClaimPendingInAppPurchase(purchaseEvent.TransactionId));
                        });
                        break;

                    case InAppPurchaseStatus.InvalidReceipt:
                        // Invalid receipt (removal is handled in PlayerInAppPurchaseValidated action, so we shouldn't be here)
                        _log.Error("Purchase {TransactionId} of product {ProductId} has invalid receipt but exists in pending purchases", purchaseEvent.TransactionId, purchaseEvent.ProductId);
                        break;

                    case InAppPurchaseStatus.ReceiptAlreadyUsed:
                        // Can happen (besides just cheaters) when a past purchase has been already claimed but its confirmation has not reached the
                        // IAP store, and the IAP store asks the game to re-process the purchase. The client cannot reject such
                        // re-processing cases on its own, as only the server knows about past purchases that have already been claimed.
                        // We clear and confirm it.
                        if (completionActions == null)
                            completionActions = new List<Action>();
                        completionActions.Add(() =>
                        {
                            _log.Info("Confirming {TransactionId} of product {ProductId}, where receipt had already been used", purchaseEvent.TransactionId, purchaseEvent.ProductId);
                            PlayerContext.ExecuteAction(new PlayerClearPendingDuplicateInAppPurchase(purchaseEvent.TransactionId));
                        });
                        break;
                }
            }
            if (completionActions != null)
            {
                foreach (Action completion in completionActions)
                    completion.Invoke();
            }
        }

        #endregion

        protected abstract string GetCurrentStateLabel();
        protected abstract Task OnNetworkMessage(MetaMessage message);
        protected abstract Task OnUpdate();

        #region IPlayerModelClientListenerCore

        public virtual void OnPlayerNameChanged(string newName) { }

        public virtual void PendingDynamicPurchaseContentAssigned(InAppProductId productId)
        {
            _pendingDynamicPurchases.Add(productId);
        }

        public virtual void PendingStaticInAppPurchaseContextAssigned(InAppProductId productId) { }
        public virtual void InAppPurchaseValidationFailed(InAppPurchaseEvent ev) { }
        public virtual void InAppPurchaseValidated(InAppPurchaseEvent ev) { }
        public virtual void InAppPurchaseClaimed(InAppPurchaseEvent ev) { }
        public virtual void DuplicateInAppPurchaseCleared(InAppPurchaseEvent ev) { }
        public virtual void OnPlayerScheduledForDeletionChanged() { }
        public virtual void OnNewInGameMail(PlayerMailItem mail) { }

        #endregion
    }
}
