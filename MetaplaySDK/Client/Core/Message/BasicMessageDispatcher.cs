// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

#if UNITY_WEBGL && !UNITY_EDITOR
#   define UNITY_WEBGL_BUILD
#endif

using Metaplay.Core.Network;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Metaplay.Core.Message
{
    /// <summary>
    /// Implements the common state management of a message dispatcher.
    /// </summary>
    public abstract class BasicMessageDispatcher : IMessageDispatcher
    {
        delegate void RequestCompleteFn(MetaResponseMessage msg, bool forceCancel);

        abstract class Handler
        {
            public readonly object Label;

            protected Handler(object label)
            {
                Label = label;
            }
            public abstract void DispatchToHandler(MetaMessage msg);
            public abstract bool IsHandlerForDelegate(MulticastDelegate @delegate);
        }
        class Handler<T> : Handler where T : MetaMessage
        {
            MessageHandler<T> _handlerFunc;

            public Handler(object label, MessageHandler<T> handlerFunc) : base(label)
            {
                _handlerFunc = handlerFunc;
            }
            public sealed override void DispatchToHandler(MetaMessage msg)
            {
                _handlerFunc.Invoke((T)msg);
            }
            public sealed override bool IsHandlerForDelegate(MulticastDelegate @delegate)
            {
                return _handlerFunc.Equals(@delegate);
            }
        }

        readonly LogChannel                             _log;
        Dictionary<Type, List<Handler>>                 _handlers           = new Dictionary<Type, List<Handler>>();
        Dictionary<Type, List<Handler>>                 _updatedHandlers;
#if UNITY_WEBGL_BUILD
        WebConcurrentDictionary<int, RequestCompleteFn> _pendingRequests    = new WebConcurrentDictionary<int, RequestCompleteFn>();
#else
#pragma warning disable MP_WGL_00 // "Feature is poorly supported in WebGL". False positive, this is non-WebGL.
        ConcurrentDictionary<int, RequestCompleteFn>    _pendingRequests    = new ConcurrentDictionary<int, RequestCompleteFn>();
#pragma warning restore MP_WGL_00
#endif
        int                                             _runningRequestId = 0;
        bool                                            _isDispatching;

        protected LogChannel                            Log => _log;

        public abstract ServerConnection                ServerConnection { get; }

        protected BasicMessageDispatcher(LogChannel log)
        {
            _log = log;
        }

        public void AddListener<T>(MessageHandler<T> handlerFunc) where T : MetaMessage
        {
            AddListener(handlerFunc, label: null);
        }

        public void AddListener<T>(MessageHandler<T> handlerFunc, object label) where T : MetaMessage
        {
            Dictionary<Type, List<Handler>> dst;

            if (_isDispatching)
            {
                // Walking the structure. Delay changes.
                if (_updatedHandlers == null)
                    _updatedHandlers = CopyHandlers();
                dst = _updatedHandlers;
            }
            else
            {
                // We are not walking. Add immediately.
                dst = _handlers;
            }

            Handler handler = new Handler<T>(label, handlerFunc);

            // Add
            if (!dst.TryGetValue(typeof(T), out List<Handler> msgHandlers))
                dst.Add(typeof(T), new List<Handler>(1) { handler });
            else
                msgHandlers.Add(handler);
        }

        public void RemoveListener<T>(MessageHandler<T> handlerFunc) where T : MetaMessage
        {
            Dictionary<Type, List<Handler>> dst;

            if (_isDispatching)
            {
                // Walking the structure. Delay changes.
                if (_updatedHandlers == null)
                    _updatedHandlers = CopyHandlers();
                dst = _updatedHandlers;
            }
            else
            {
                // We are not walking. Remove immediately.
                dst = _handlers;
            }

            // Remove.
            if (dst.TryGetValue(typeof(T), out List<Handler> msgHandlers))
            {
                bool found = false;
                for (int ndx = 0; ndx < msgHandlers.Count; ++ndx)
                {
                    if (msgHandlers[ndx].IsHandlerForDelegate(handlerFunc))
                    {
                        msgHandlers.RemoveAt(ndx);
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    _log.Warning("Attempted to RemoveListener<{Type}> for a listener that wasn't Added.", typeof(T).ToGenericTypeString());
                }
                if (msgHandlers.Count == 0)
                    dst.Remove(typeof(T));
            }
        }

        public void RemoveListenersWithLabel(object label)
        {
            // No label, nothing to do.
            if (label == null)
                return;

            Dictionary<Type, List<Handler>> dst;

            if (_isDispatching)
            {
                // Walking the structure. Delay changes.
                if (_updatedHandlers == null)
                    _updatedHandlers = CopyHandlers();
                dst = _updatedHandlers;
            }
            else
            {
                // We are not walking. Remove immediately.
                dst = _handlers;
            }

            List<Type> typesToDelete = new List<Type>();
            Predicate<Handler> predicate = (handler) => label.Equals(handler.Label);
            foreach ((Type type, List<Handler> list) in dst)
            {
                list.RemoveAll(predicate);
                if (list.Count == 0)
                    typesToDelete.Add(type);
            }
            foreach (Type type in typesToDelete)
                dst.Remove(type);
        }

        public void RemoveAllListeners()
        {
            if (_isDispatching)
            {
                // Walking the structure. Delay changes.
                if (_updatedHandlers == null)
                    _updatedHandlers = new Dictionary<Type, List<Handler>>();
                _updatedHandlers.Clear();
            }
            else
            {
                // We are not walking. Remove immediately.
                _handlers.Clear();
            }
        }

        protected abstract bool SendMessageInternal(MetaMessage message);

        public bool SendMessage(MetaMessage message)
        {
            if (message is MetaRequestMessage || message is MetaResponseMessage)
                throw new InvalidOperationException($"SendMessage for MetaMessage of type {message.GetType()} not allowed");

            bool didSend = SendMessageInternal(message);
            if (didSend)
            {
                if (_log.IsDebugEnabled)
                    _log.Debug("Sending: {Message}", PrettyPrint.Compact(message));
            }
            else
            {
                _log.Warning("Cannot send, no transport. Dropping: {Message}", PrettyPrint.Compact(message));
            }

            return didSend;
        }

        public Task<T> SendRequestAsync<T>(MetaRequest request) where T : MetaResponse
        {
            return SendRequestAsync<T>(request, CancellationToken.None);
        }

        void CompleteResponsePromise<T>(TaskCompletionSource<T> promise, MetaResponseMessage message, CancellationToken ct, bool forceCancel) where T : MetaResponse
        {
            if (forceCancel || ct.IsCancellationRequested)
            {
                promise.TrySetCanceled();
            }
            else if (message.Payload is T response)
            {
                promise.TrySetResult(response);
            }
            else
            {
                string err = $"Invalid response type received, expected {typeof(T)} but got {message.GetType()}";
                _log.Error(err);
                promise.TrySetException(new Exception(err));
            }
        }

        public Task<T> SendRequestAsync<T>(MetaRequest request, CancellationToken ct) where T : MetaResponse
        {
            MetaRequestMessage message = new SessionMetaRequestMessage()
            {
                Id = Interlocked.Increment(ref _runningRequestId),
                Payload = request
            };

            TaskCompletionSource<T> promise = new TaskCompletionSource<T>();
            if (ct != CancellationToken.None)
                ct.Register(() => promise.TrySetCanceled());
            _pendingRequests[message.Id] = (response, forceCancel) => CompleteResponsePromise(promise, response, ct, forceCancel);

            if (SendMessageInternal(message))
            {
                return promise.Task;
            }
            else
            {
                _pendingRequests.TryRemove(message.Id, out RequestCompleteFn requestCompleteFn);
                return Task.FromException<T>(new Exception("Could not send request"));
            }
        }

        public void ClearPendingRequests()
        {
            // \note: copy Keys as we are modifying them while we iterate
            foreach (int requestId in _pendingRequests.Keys.ToArray())
            {
                if (_pendingRequests.TryRemove(requestId, out RequestCompleteFn handler))
                {
                    handler.Invoke(null, true);
                }
            }
        }

        bool DispatchDefault(MetaMessage msg)
        {
            if (!_handlers.TryGetValue(msg.GetType(), out List<Handler> handlers))
            {
                return false;
            }

            _isDispatching = true;

            foreach (Handler handler in handlers)
            {
                try
                {
                    handler.DispatchToHandler(msg);
                }
                catch (Exception ex)
                {
                    // Invoke wraps real callee's exceptions to TargetInvocationException. Unwrap here.
                    if (ex is TargetInvocationException targetInvocationException)
                        ex = targetInvocationException.InnerException;

                    _log.Error("Message handler for {Type} failed: {Ex}", msg.GetType().Name, ex);
                    OnListenerInvocationException(ex);
                }
            }

            _isDispatching = false;
            if (_updatedHandlers != null)
            {
                _handlers = _updatedHandlers;
                _updatedHandlers = null;
            }

            return true;
        }

        bool DispatchResponse(MetaResponseMessage msg)
        {
            if (_pendingRequests.TryRemove(msg.RequestId, out RequestCompleteFn handler))
            {
                handler.Invoke(msg, false);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Dispatches the message to the listeners. Returns true, if message was delivered to
        /// any handler.
        /// </summary>
        protected bool DispatchMessage(MetaMessage msg)
        {
            if (msg is MetaResponseMessage response)
                return DispatchResponse(response);
            else
                return DispatchDefault(msg);
        }

        protected virtual void OnListenerInvocationException(Exception ex) { }

        Dictionary<Type, List<Handler>> CopyHandlers()
        {
            Dictionary<Type, List<Handler>> result = new Dictionary<Type, List<Handler>>(_handlers.Count);
            foreach ((Type type, List<Handler> msgHandlers) in _handlers)
                result.Add(type, new List<Handler>(msgHandlers));
            return result;
        }
    }
}
