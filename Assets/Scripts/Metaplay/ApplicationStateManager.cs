// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

#if UNITY_WEBGL && !UNITY_EDITOR
#define UNITY_WEBGL_BUILD
#endif

using Game.Logic;
using Metaplay.Core;
using Metaplay.Core.Message;
using Metaplay.Unity;
using Metaplay.Unity.DefaultIntegration;
using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DotsShooter.Metaplay
{
    /// <summary>
    /// Manages the application's lifecycle, including mock loading state, Metaplay server connectivity, and failure states.
    /// This class is a simplified version of a state manager that a real game would have, but in such a manner that the
    /// integration of Metaplay into such a state manager is exemplified.
    ///
    /// Also implements <see cref="IMetaplayLifecycleDelegate"/> to get callbacks from Metaplay on connectivity events and
    /// error states.
    /// </summary>
    public class ApplicationStateManager : MonoBehaviour, IMetaplayLifecycleDelegate, IPlayerModelClientListener
    {
        /// <summary>
        /// Represents the state of the application.
        /// </summary>
        public enum ApplicationState
        {
            /// <summary>
            /// Application is being started.
            /// </summary>
            AppStart,

            /// <summary>
            /// Connecting to the server. A real application would also load its assets here.
            /// </summary>
            Initializing,

            /// <summary>
            /// Session with server has been established and we're playing the game.
            /// </summary>
            Game,
        }

        // When connection to server is not established, display connection status.
        public SceneAsset MenuScene;

        // Runtime state
        ApplicationState        _applicationState = ApplicationState.AppStart;  // Begin in the AppStart state.

        void Awake()
        {
            // When the app starts, make sure the connection error info is hidden.
            // ConnectionErrorPopup.SetActive(false);
            DontDestroyOnLoad(this.gameObject); // Todo: I think we need the statemanager throughout the game, but look into it.
        }

        void Start()
        {
            // Initialize Metaplay SDK.
            MetaplayClient.Initialize(new MetaplayClientOptions
            {
                // Hook all the lifecycle and connectivity callbacks back to this class.
                LifecycleDelegate = this,
            });

            // Switch to initializing state, to start connecting to the server.
            SwitchToState(ApplicationState.Initializing);
        }

        void Update()
        {
            // Update Metaplay connections and game logic
            MetaplayClient.Update();
        }

        /// <summary>
        /// Switch the application's state and perform actions relevant to the state transition.
        /// </summary>
        /// <param name="newState"></param>
        void SwitchToState(ApplicationState newState)
        {
            Debug.Log($"Switching to state {newState} (from {_applicationState})");

            switch (newState)
            {
                case ApplicationState.AppStart:
                    // Cannot enter, app starts in this state.
                    break;

                case ApplicationState.Initializing:
                    
                    // // Simulate the transition away from the Game scene by destroying the GameManager instance.
                    // // In addition to the Game scene, it's possible to arrive here from Initializing state itself,
                    // // in case the connection fails before a session was started. In that case there is no
                    // // GameManager instance.
                    // if (_gameManager != null)
                    // {
                    //     Destroy(_gameManager.gameObject);
                    //     _gameManager = null;
                    // }



                    // Start connecting to the server.
                    MetaplayClient.Connect();
                    break;

                case ApplicationState.Game:
                    SceneManager.LoadScene(MenuScene.name);
                    break;
            }

            // Store the new state.
            _applicationState = newState;
        }


        #region IMetaplayLifecycleDelegate

        /// <summary>
        /// A session has been successfully negotiated with the server. At this point, we also have the
        /// relevant state initialized on the client, so we can move on to the game state.
        /// </summary>
        void IMetaplayLifecycleDelegate.OnSessionStarted()
        {
            // Hook up to updates in PlayerModel.
            MetaplayClient.PlayerModel.ClientListener = this;

            // Switch to the in-game state.
            SwitchToState(ApplicationState.Game);

            // At this point, the player state is available. For example, the following are now valid:
            // Access player state members: MetaplayClient.PlayerModel.CurrentTime
            // Execute player actions: MetaplayClient.PlayerContext.ExecuteAction(..);
        }

        /// <summary>
        /// The current logical session has been lost and can no longer be resumed. This can happen for multiple
        /// reasons, for example, if the network connection is dropped for a sufficient long time, or if the
        /// application has been in the background for a long time, or if the server is in a maintenance mode.
        ///
        /// The application should react to this by showing a 'Connection Lost' dialog and present the player
        /// with a 'Reconnect' button.
        /// For some types of errors, it may be appropriate to omit the error popup, and auto-reconnect instead.
        /// </summary>
        /// <param name="connectionLost">Information about why the session loss happened.</param>
        void IMetaplayLifecycleDelegate.OnSessionLost(ConnectionLostEvent connectionLost)
        {
            if (connectionLost.AutoReconnectRecommended)
            {
                // For certain errors, we auto-reconnect straight away without
                // prompting the player. Note that AutoReconnectRecommended is
                // just a suggestion by the SDK and is based on the type of the
                // error. The game does not have to obey the suggestion.
                SwitchToState(ApplicationState.Initializing);
            }
            else
            {
                // Otherwise, show the connection error popup, with info text
                // and a reconnect button.
                // Despite losing the session, the game scene will linger until
                // the player clicks on the reconnect button.
                // MetaplayClient.PlayerModel is still available so that the
                // game scene can continue to access it. It will remain available
                // until the reconnection starts.
                ShowConnectionErrorPopup(connectionLost);
            }
        }

        /// <summary>
        /// Metaplay failed to establish a session with the server. Show the connection error and 'Reconnect'
        /// button so the player can try again.
        /// </summary>
        /// <param name="connectionLost">Information about why the failure happened.</param>
        void IMetaplayLifecycleDelegate.OnFailedToStartSession(ConnectionLostEvent connectionLost)
        {
            // Show the connection error popup, with info text and a reconnect button.
            // Note that we're not in the game scene since the error occurred before
            // the session was started. Furthermore, MetaplayClient.PlayerModel is
            // unavailable.
            ShowConnectionErrorPopup(connectionLost);
        }

        /// <summary>
        /// Show a popup with the details of a connection/session error,
        /// and a reconnect button.
        /// </summary>
        /// <param name="connectionLost"></param>
        void ShowConnectionErrorPopup(ConnectionLostEvent connectionLost)
        {
            //TODO: Implement this
            Debug.Log($"Connection lost: {CreateConnectionLostInfoText(connectionLost)}");
        }

        /// <summary>
        /// Convert a <see cref="ConnectionLostEvent"/> into a human-readable string, for displaying in the UI.
        /// This implementation shows quite a lot of technical detail which is useful for developers, but for
        /// real players, you'd want to show something more compact.
        /// </summary>
        /// <param name="connectionLost"></param>
        /// <returns>Technical description of the connection loss event, mainly intended for developers.</returns>
        static string CreateConnectionLostInfoText(ConnectionLostEvent connectionLost)
        {
            StringBuilder info = new StringBuilder();

            // EnglishLocalizedReason and TechnicalErrorCode should typically be shown to players.
            info.AppendLine($"* Reason: {connectionLost.EnglishLocalizedReason}");
            info.AppendLine($"* Technical code: {connectionLost.TechnicalErrorCode}");

            // TechnicalErrorString and ExtraTechnicalInfo are intended for analytics.
            info.AppendLine();
            info.AppendLine($"* Technical error string: {connectionLost.TechnicalErrorString}");
            if (!string.IsNullOrEmpty(connectionLost.ExtraTechnicalInfo))
                info.AppendLine($"* Additional technical info: {connectionLost.ExtraTechnicalInfo}");

            // More detailed technical info that's mainly useful for developers.
            info.AppendLine();
            info.AppendLine($"* Technical error: {PrettyPrint.Compact(connectionLost.TechnicalError)}");

            return info.ToString();
        }

        #endregion // IMetaplayLifecycleDelegate
    }
}
