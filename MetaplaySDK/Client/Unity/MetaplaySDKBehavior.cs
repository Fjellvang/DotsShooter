// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using UnityEngine;
using System.Collections;
using System;
using Metaplay.Core;
using Metaplay.Core.Network;
using Metaplay.Core.Message;

namespace Metaplay.Unity
{
    class MetaplayInitializationError : Exception
    {
        public MetaplayInitializationError(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Hooks MetaplaySDK into Unity.
    /// </summary>
    public class MetaplaySDKBehavior : MonoBehaviour
    {
        internal static MetaplaySDKBehavior Instance = null;
        private bool _wasAutoCreated;
        private bool _isDuplicate;

        LatencySimulationMessageTransport _latencySimulationTransport;

        public bool EnableLatencySimulation = false;

        /// <summary>
        /// The amount of latency to add to sent and received messages in milliseconds.
        /// </summary>
        public int  ArtificialAddedLatency  = 0;
#if UNITY_EDITOR

        // Link quality simulator
        public enum LinkQualitySetting
        {
            Perfect = 0,
            Spotty = 1,
            NoReplies = 2,
            AllRejected = 3,
        }
        public LinkQualitySetting _simulatedLinkQuality;
        FaultInjectingMessageTransport _faultInjectingTransport;
        DateTime _nextSpottyLinkUpdate;
        bool _spottyLinkIsHalted;

        // Spoofing of client platform as reported to server
        public ClientPlatform _appearAsPlatform = ClientPlatform.UnityEditor;

        // Spoofed client location
        // \note: stored as string to make Unity-serialiable (to survive domain reloads).
        public string _overrideLocationString;
#endif

        private void Awake()
        {
            if (Instance != null)
            {
                MetaplaySDK.Logs.Metaplay.Info("MetaplaySDKBehavior already exists in the scene. Removing the new one (this one).");
                Destroy(this);
                _isDuplicate = true;
                return;
            }

            GameObject.DontDestroyOnLoad(this.gameObject);
            Instance = this;
        }

        private void OnDestroy()
        {
            if (_isDuplicate)
                return;

            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            if (_isDuplicate)
                return;

            StartCoroutine(EndOfTheFrameUpdater());
        }

        private void OnApplicationQuit()
        {
            if (_isDuplicate)
                return;

            MetaplaySDK.OnApplicationQuit();
        }

        private void OnApplicationPause(bool pause)
        {
            if (_isDuplicate)
                return;

            MetaplaySDK.OnApplicationPause(pause);
        }

        internal static void EnsureExists(bool autoCreateMetaplaySDKBehavior)
        {
            if (autoCreateMetaplaySDKBehavior)
            {
                if (Instance == null)
                {
                    GameObject go = new GameObject("MetaplaySDKBehavior");
                    MetaplaySDKBehavior sdk = go.AddComponent<MetaplaySDKBehavior>();

                    sdk._wasAutoCreated = true;

                    Instance = sdk;
                }
                else
                {
                    if (!Instance._wasAutoCreated)
                        throw new MetaplayInitializationError("Manually placed MetaplaySDKBehavior already existed in the scene even though MetaplaySDKConfig.AutoCreateMetaplaySDKBehavior was set. Only one behavior is allowed.");
                }
            }
            else
            {
                if (Instance == null)
                    throw new MetaplayInitializationError("MetaplaySDKBehavior is missing. You should add MetaplaySDKBehavior to the scene and make sure it is not destroyed as long as MetaplaySDK is running, or set MetaplaySDKConfig.AutoCreateMetaplaySDKBehavior");
            }
        }

        internal static void AfterSDKInit()
        {
            if (Instance.EnableLatencySimulation)
                MetaplaySDK.Connection.CreateTransportHooks.Add(Instance.CreateLatencySimulationTransport);

#if UNITY_EDITOR
            MetaplaySDK.Connection.CreateTransportHooks.Add(Instance.EditorHookCreateFaultInjectingTransport);
            ClientPlatformUnityUtil.EditorHookGetClientPlatform = () => Instance._appearAsPlatform;
            MetaplaySDK.LocalizationManager.EditorHookLocalizationUpdatedEvent = Instance.EditorHookLocalizationChangedEvent;
            ClientLocationOverrideProvider.EditorHookGetOverrideLocation = () =>
            {
                if (string.IsNullOrEmpty(Instance._overrideLocationString))
                    return null;
                string[] parts = Instance._overrideLocationString.Split("-");
                return new Metaplay.Core.Player.PlayerLocation(new Metaplay.Core.Player.CountryId(parts[1]), parts[0]);
            };
#endif
        }

        public void Update()
        {
#if UNITY_EDITOR
            UpdateTransportState();
#endif
            if (EnableLatencySimulation && _latencySimulationTransport != null && _latencySimulationTransport.ArtificialLatency != ArtificialAddedLatency)
            {
                _latencySimulationTransport.UpdateLatency(ArtificialAddedLatency);
            }
        }

        static IEnumerator EndOfTheFrameUpdater()
        {
            WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();
            for (;;)
            {
                yield return waitForEndOfFrame;
                try
                {
                    MetaplaySDK.UpdateAtEndOfTheFrame();
                }
                catch
                {
                }
            }
        }

        ServerConnection.CreateTransportFn CreateLatencySimulationTransport(ServerConnection.CreateTransportFn inner)
        {
            return (ServerGateway gateway) =>
            {
                IMessageTransport innerTransport = inner(gateway);
                _latencySimulationTransport = new LatencySimulationMessageTransport(innerTransport, MetaplaySDK.Logs.Metaplay);
                _latencySimulationTransport.UpdateLatency(ArtificialAddedLatency);
                return _latencySimulationTransport;
            };
        }

#if UNITY_EDITOR
        void UpdateTransportState()
        {
            if (_faultInjectingTransport == null)
                return;

            // Simulated Connection quality
            switch (_simulatedLinkQuality)
            {
                case LinkQualitySetting.Perfect:
                    _faultInjectingTransport.Resume();
                    _spottyLinkIsHalted = false;
                    break;

                case LinkQualitySetting.Spotty:
                    if (DateTime.UtcNow >= _nextSpottyLinkUpdate)
                    {
                        var rnd = new System.Random();

                        if (rnd.Next(100) < 10)
                        {
                            MetaplaySDK.Logs.Metaplay.Warning("Spotty network updated: simulating loss of stream");
                            _faultInjectingTransport.InjectError(new StreamMessageTransport.StreamClosedError());
                            _spottyLinkIsHalted = false;
                        }
                        else if (rnd.Next(100) < 50)
                        {
                            if (!_spottyLinkIsHalted)
                                MetaplaySDK.Logs.Metaplay.Warning("Spotty network updated: pausing incoming traffic, simulating silent network loss");
                            _spottyLinkIsHalted = true;
                            _faultInjectingTransport.Halt();
                        }
                        else
                        {
                            if (_spottyLinkIsHalted)
                                MetaplaySDK.Logs.Metaplay.Warning("Spotty network updated: normal");
                            _spottyLinkIsHalted = false;
                            _faultInjectingTransport.Resume();
                        }
                        _nextSpottyLinkUpdate = DateTime.UtcNow + TimeSpan.FromMilliseconds(3000 + rnd.Next(2000));
                    }
                    break;

                case LinkQualitySetting.AllRejected:
                {
                    _faultInjectingTransport.Halt();
                    _faultInjectingTransport.InjectError(new TcpMessageTransport.ConnectionRefused());
                    _spottyLinkIsHalted = false;
                    break;
                }

                case LinkQualitySetting.NoReplies:
                default:
                    _faultInjectingTransport.Halt();
                    _spottyLinkIsHalted = false;
                    break;
            }
        }

        ServerConnection.CreateTransportFn EditorHookCreateFaultInjectingTransport(ServerConnection.CreateTransportFn inner)
        {
            return (ServerGateway gateway) =>
            {
                IMessageTransport innerTransport = inner(gateway);
                _faultInjectingTransport     = new FaultInjectingMessageTransport(innerTransport);
                _nextSpottyLinkUpdate = DateTime.UnixEpoch;
                UpdateTransportState();
                return _faultInjectingTransport;
            };
        }

        /// <summary>
        /// <inheritdoc cref="MetaplayLocalizationManager.EditorHookLocalizationUpdatedEvent"/>
        /// </summary>
        // \note: this event trampoline is to keep initial event handlers and editor handler statically separated
        public event Action EditorHookOnLocalizationUpdatedEvent;
        void EditorHookLocalizationChangedEvent()
        {
            try
            {
                EditorHookOnLocalizationUpdatedEvent?.Invoke();
            }
            catch
            {
            }
        }
#endif
    }
}
