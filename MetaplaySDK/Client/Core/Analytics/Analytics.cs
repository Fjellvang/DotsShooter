// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Model;
using System;
using System.Linq;

namespace Metaplay.Core.Analytics
{
    /// <summary>
    /// Analytics event handler with configurable handler callback.
    ///
    /// Each event payload is accompanied by a context object
    /// whose meaning is use-case-specific.
    /// In practice, the context object may be used to differentiate
    /// between separate sources the events are associated with,
    /// in case the same event handler instance is used with multiple
    /// different sources.
    /// </summary>
    public class AnalyticsEventHandler<TContext, TEvent> where TEvent : AnalyticsEventBase
    {
        public static readonly AnalyticsEventHandler<TContext, TEvent> NopHandler = new AnalyticsEventHandler<TContext, TEvent>(eventHandler: null);

        Action<TContext, TEvent> _eventHandler;

        public AnalyticsEventHandler(Action<TContext, TEvent> eventHandler)
        {
            _eventHandler = eventHandler;
        }

        public void Event(TContext context, TEvent payload)
        {
            // Invoke handler immediately
            _eventHandler?.Invoke(context, payload);
        }
    }

    /// <summary>
    /// A helper for <see cref="AnalyticsEventHandler{TContext, TEvent}"/>,
    /// wrapping the context object in order to provide an implicit-context
    /// <see cref="Event(TEvent)"/> method.
    /// </summary>
    public readonly ref struct ContextWrappingAnalyticsEventHandler<TContext, TEvent> where TEvent : AnalyticsEventBase
    {
        readonly TContext                                _context;
        readonly AnalyticsEventHandler<TContext, TEvent> _handler;

        public ContextWrappingAnalyticsEventHandler(TContext context, AnalyticsEventHandler<TContext, TEvent> handler)
        {
            _context = context;
            _handler = handler;
        }

        public void Event(TEvent payload)
        {
            _handler?.Event(_context, payload);
        }
    }
}
