// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Message;
using Metaplay.Core.Model;

namespace Metaplay.Core.Player
{
    /// <summary>
    /// Login event for a player. Can be used to retain the latest player logins for
    /// security purposes, eg, when trying to figure out whether an account has been
    /// legitimately lost vs. someone trying to steal it.
    /// </summary>
    [MetaSerializable]
    [MetaBlockedMembers(7)]
    public class PlayerLoginEvent
    {
        [MetaMember(1)] public MetaTime          Timestamp         { get; private set; } // Timestamp of login event
        [MetaMember(2)] public string            DeviceId          { get; private set; } // Unique identifier of device
        [MetaMember(3)] public string            DeviceModel       { get; private set; } // Human-readable device model (SystemInfo.deviceModel in Unity, eg, iPhone6,1)
        [MetaMember(4)] public string            ClientVersion     { get; private set; } // Game client version (Application.version in Unity)
        [MetaMember(5)] public PlayerLocation?   Location          { get; private set; } // Location of login
        [MetaMember(6)] public AuthenticationKey AuthenticationKey { get; private set; } // Authentication key used for login
        [MetaMember(8)] public ClientPlatform    ClientPlatform    { get; private set; } // Platform reported by client
        [MetaMember(9)] public string            OperatingSystem   { get; private set; } // Operating system with version info (SystemInfo.GetOperatingSystem() in Unity)
        [MetaMember(10)] public MetaDuration?    SessionLengthApprox  { get; set; }      // Approximate length of the session. Will be `null` initially when session is started, actual value is set when session terminates.

        public PlayerLoginEvent() { }
        public PlayerLoginEvent(MetaTime timestamp, string deviceId, SessionProtocol.ClientDeviceInfo device, string clientVersion, PlayerLocation? location, AuthenticationKey authKey)
        {
            Timestamp         = timestamp;
            DeviceId          = deviceId;
            DeviceModel       = device.DeviceModel;
            ClientVersion     = clientVersion;
            Location          = location;
            AuthenticationKey = authKey;
            ClientPlatform    = device.ClientPlatform;
            OperatingSystem   = device.OperatingSystem;
        }
    }
}
