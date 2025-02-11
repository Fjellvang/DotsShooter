// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

#if !UNITY_WEBGL || UNITY_EDITOR

using System;
using System.Threading.Tasks;

namespace Metaplay.Core
{
    public static partial class WebSocketConnector
    {
        public static partial void Initialize() => throw new NotImplementedException();
        public static partial Task<int> Connect(string url, SocketError errorCallback, SocketClosed closedCallback, SocketMessage messageCallback)
        {
            return Task.FromException<int>(new NotImplementedException());
        }
        public static partial void Close(int connId) => throw new NotImplementedException();
        public static partial void Send(int connId, byte[] data, int dataStart, int dataLength) => throw new NotImplementedException();
    }
}

#endif
