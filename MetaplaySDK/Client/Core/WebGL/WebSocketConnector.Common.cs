// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System.Threading.Tasks;

namespace Metaplay.Core
{
    /// <summary>
    /// Binding to the WebSocket API in the browser. Only supported on WebGL.
    /// </summary>
    public static partial class WebSocketConnector
    {
        public delegate void SocketError(string errorStr);
        public delegate void SocketClosed(int code, string reason, bool wasClean);
        public delegate void SocketMessage(byte[] msg);

        public static partial void Initialize();
        public static partial Task<int> Connect(string url, SocketError errorCallback, SocketClosed closedCallback, SocketMessage messageCallback);
        public static partial void Close(int connId);

        public static void Send(int connId, byte[] data)
        {
            Send(connId, data, 0, data.Length);
        }
        public static partial void Send(int connId, byte[] data, int dataStart, int dataLength);
    }
}
