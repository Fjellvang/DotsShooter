// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Model.JournalCheckers;
using System;
using static System.FormattableString;

namespace Metaplay.Core.Model
{
    public interface ITimelineHistory : IDisposable
    {
        /// <summary>
        /// Is the timeline history enabled? Entries should only be added to the timeline if it is enabled (to avoid any
        /// allocations when it's not enabled).
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Add an entry to the timeline from some operation. The operation is usually a Tick or an Action.
        /// </summary>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="name">Name of the operation.</param>
        /// <param name="tick">Tick index when the operation happened.</param>
        /// <param name="action">Optional action.</param>
        /// <param name="afterState">The model state after the action.</param>
        void AddEntry<TModel>(string name, long tick, ModelAction action, TModel afterState) where TModel : class, IModel;
    }

    /// <summary>
    /// Adapter for forwarding events from a <see cref="ModelJournal{TModel}"/> into a <see cref="ITimelineHistory"/>.
    /// Only active when running in Unity Editor, just a no-operation in builds.
    /// </summary>
    public class TimelineHistoryListener<TModel> : ModelJournalListenerBase<TModel>
        where TModel : class, IModel<TModel>
    {
#if UNITY_EDITOR
        ITimelineHistory _timelineHistory; // Timeline where to register events (can be null)
#endif

        public TimelineHistoryListener(LogChannel log, ITimelineHistory timelineHistory) : base(log)
        {
#if UNITY_EDITOR
            _timelineHistory = timelineHistory;
#endif
        }

        protected override void AfterSetup()
        {
#if UNITY_EDITOR
            // Add initial state to timeline history (if enabled)
            if (_timelineHistory != null && _timelineHistory.IsEnabled)
                _timelineHistory.AddEntry($"{StagedModel.GetType().Name} init", tick: StagedPosition.Tick, action: null, (TModel)StagedModel);
#endif
        }

        protected override void AfterTick(long tick, MetaActionResult result)
        {
#if UNITY_EDITOR
            // Add tick to timeline history (if enabled)
            if (_timelineHistory != null && _timelineHistory.IsEnabled)
                _timelineHistory.AddEntry(Invariant($"{StagedModel.GetType().Name} tick {tick}"), StagedPosition.Tick, null, (TModel)StagedModel);
#endif
        }

        protected override void AfterAction(ModelAction action, MetaActionResult result)
        {
#if UNITY_EDITOR
            // Add action to timeline history (if enabled)
            if (_timelineHistory != null && _timelineHistory.IsEnabled)
                _timelineHistory.AddEntry(action.GetType().ToGenericTypeString(), StagedPosition.Tick, action, (TModel)StagedModel);
#endif
        }
    }
}
