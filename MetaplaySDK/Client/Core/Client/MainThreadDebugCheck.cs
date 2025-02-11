// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Diagnostics;
using static System.FormattableString;

namespace Metaplay.Core.Client
{
    public static class MainThreadDebugCheck
    {
        static int         _mainThreadId = -1;
        static IMetaLogger _log;

        [Conditional("DEBUG")]
        public static void InitializeFromMainThread(IMetaLogger log)
        {
            _mainThreadId = Environment.CurrentManagedThreadId;
            _log          = log;
        }

        [Conditional("DEBUG")]
        public static void Deinitialize()
        {
            _mainThreadId = -1;
            _log          = null;
        }

        /// <summary>
        /// Logs and throws an exception if the calling thread is not the Unity main thread.
        /// </summary>
        [Conditional("DEBUG")]
        public static void Invoke([System.Runtime.CompilerServices.CallerMemberName] string operation = "")
        {
            if (_mainThreadId != -1 && Environment.CurrentManagedThreadId != _mainThreadId)
            {
                string err = Invariant($"Tried to execute {operation} on thread {Environment.CurrentManagedThreadId} which is not the main thread!");
                _log.Error(err);
                throw new InvalidOperationException(err);
            }
        }
    }
}
