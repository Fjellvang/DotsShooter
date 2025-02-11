// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using System;
using System.Threading.Tasks;

namespace Metaplay.Cloud.Entity
{
    public abstract partial class EntityActor
    {
        class PeriodicTimerCommand
        {
            public readonly object                                              InnerCommand;
            public readonly long                                                FirstInvocationAtTicks;
            public readonly long                                                IntervalTicks;
            public readonly EntityDispatcherBase<EntityActor>.DispatchCommand   Dispatch;
            public bool                                                         IsFlushingPileup;
            public int                                                          NumPiledUpMessagesIgnored;
            public long                                                         LastCycleNumber;
            public int                                                          NumMessagesOnThisCycle;

            public PeriodicTimerCommand(object innerCommand, long firstInvocationAtTicks, long intervalTicks, EntityDispatcherBase<EntityActor>.DispatchCommand dispatch)
            {
                InnerCommand = innerCommand;
                FirstInvocationAtTicks = firstInvocationAtTicks;
                IntervalTicks = intervalTicks;
                Dispatch = dispatch;
            }
        }

        class ResetPeriodicTimerCommand
        {
            public readonly PeriodicTimerCommand PeriodicTimer;
            public ResetPeriodicTimerCommand(PeriodicTimerCommand periodicTimer)
            {
                PeriodicTimer = periodicTimer;
            }
        }

        bool _allPeriodicTimersCancelled;

        void RegisterPeriodicTimerHandlers()
        {
            ReceiveAsync<PeriodicTimerCommand>(ReceivePeriodicTimerCommand);
            Receive<ResetPeriodicTimerCommand>(ReceiveResetPeriodicTimerCommand);
        }

        void CancelAllPeriodicTimers()
        {
            _allPeriodicTimersCancelled = true;
        }

        /// <summary>
        /// Start a timer which periodically sends a message to the actor itself. The initial delay
        /// is the same as the send interval. Automatically handles canceling the timer when the
        /// actor stops.
        /// </summary>
        /// <param name="interval">Time span between messages after the initial one (also the initial delay)</param>
        /// <param name="message">Message to send. Actor can receive this with <see cref="CommandHandlerAttribute"/></param>
        protected void StartPeriodicTimer(TimeSpan interval, object message)
        {
            StartPeriodicTimer(interval, interval, message);
        }

        /// <summary>
        /// Start a timer which periodically sends a message to the actor itself. The initial delay
        /// and interval can be configured separately. Automatically handles canceling the timer
        /// when the actor stops.
        /// </summary>
        /// <param name="initialDelay">Time span after which the initial message it sent</param>
        /// <param name="interval">Time span between messages after the initial one</param>
        /// <param name="message">Message to send. Actor can receive this with <see cref="CommandHandlerAttribute"/></param>
        protected void StartPeriodicTimer(TimeSpan initialDelay, TimeSpan interval, object message)
        {
            if (!_dispatcher.TryGetCommandDispatchFunc(message.GetType(), out EntityDispatcherBase<EntityActor>.DispatchCommand dispatchFunc))
            {
                throw new InvalidOperationException($"Attempted to add a periodic timer with a command of type {message.GetType().ToGenericTypeString()}, but there is no handler for it. Register a handler with [CommandHandler] attribute.");
            }

            DateTime firstInvocationAt = DateTime.UtcNow + initialDelay;
            PeriodicTimerCommand command = new PeriodicTimerCommand(message, firstInvocationAtTicks: firstInvocationAt.Ticks, intervalTicks: interval.Ticks, dispatchFunc);
            Context.System.Scheduler.ScheduleTellRepeatedly(initialDelay, interval, _self, command, _self, _cancelTimers);
        }

        /// <summary>
        /// Start a timer which periodically sends a message to the actor itself. The initial delay
        /// is the interval multiplied by a random factor in range [0.5; 1.5] which helps even out the
        /// events in case of multiple timers started in close proximity (eg, by lots of logins within
        /// a short period of time). Automatically handles canceling the timer when the actor stops.
        /// </summary>
        /// <param name="interval">Time span between messages after the initial delay</param>
        /// <param name="message">Message to send. Actor can receive this with <see cref="CommandHandlerAttribute"/></param>
        protected void StartRandomizedPeriodicTimer(TimeSpan interval, object message)
        {
            Random rnd = new Random();
            double multiplier = rnd.NextDouble() + 0.5;
            TimeSpan initialDelay = TimeSpan.FromSeconds(multiplier * interval.TotalSeconds);
            StartPeriodicTimer(initialDelay, interval, message);
        }

        async Task ReceivePeriodicTimerCommand(PeriodicTimerCommand cmd)
        {
            // All timers cancelled and entity is shutting down?
            if (_allPeriodicTimersCancelled)
                return;

            // If flush in progress, ignore.
            if (cmd.IsFlushingPileup)
            {
                cmd.NumPiledUpMessagesIgnored++;
                return;
            }

            // If flush just completed, print the warning.
            if (cmd.NumPiledUpMessagesIgnored > 0)
            {
                _log.Warning("Handler for periodic timer {CommandName} took more time than the interval of the timer is. To avoid piling up work, {NumDropped} previous timer invocation(s) were dropped.",
                    cmd.InnerCommand.GetType().ToGenericTypeString(),
                    cmd.NumPiledUpMessagesIgnored);
                cmd.NumPiledUpMessagesIgnored = 0;
            }

            long cycleNumberBefore = (DateTime.UtcNow.Ticks - cmd.FirstInvocationAtTicks) / cmd.IntervalTicks;

            // Handle timer scheduler catch-up. If the process is paused and resumed, the time jumps abruptly
            // forward. This causes many timer messages in a short amount of time. We don't care for ensuring
            // the expected number of timer invocations are delivered -- instead we care that the invocations
            // are roughly equal time interval apart. Process pausing happens during debug operations such as
            // when taking memory dumps. We don't want a sudden surge of work when the process is unpaused so
            // we start dropping invocations if we get too many too frequently (i.e. within a timer interval).
            // \note: Since FirstInvocationAtTicks and scheduler's time are not in sync, we can expect 0-2 in
            //        a normal usage. We only want to prevent surges, so the dropping starts at 2x the normal.
            const int maxNumInvocationsPerCycle = 4;
            if (cmd.LastCycleNumber == cycleNumberBefore)
            {
                cmd.NumMessagesOnThisCycle++;
                if (cmd.NumMessagesOnThisCycle > maxNumInvocationsPerCycle)
                {
                    // Drop
                    return;
                }
            }
            else
            {
                if (cmd.NumMessagesOnThisCycle > maxNumInvocationsPerCycle)
                {
                    int numDropped = cmd.NumMessagesOnThisCycle - maxNumInvocationsPerCycle;
                    _log.Warning("Handler for periodic timer {CommandName} has too many pending invocations. This can happen if the process has been paused or the process is resource starved. {NumDropped} previous timer invocation(s) were dropped.",
                        cmd.InnerCommand.GetType().ToGenericTypeString(),
                        numDropped);
                }

                cmd.LastCycleNumber = cycleNumberBefore;
                cmd.NumMessagesOnThisCycle = 1;
            }

            // Dispatch timed operation
            await cmd.Dispatch(this, cmd.InnerCommand);

            // If operation took more time than next timer instance should be, we skip the next invocations. Since
            // the command should already be in our mailbox, we flush and ignore all/any messages from mailbox and then continue.
            long cycleNumberAfter = (DateTime.UtcNow.Ticks - cmd.FirstInvocationAtTicks) / cmd.IntervalTicks;
            if (cycleNumberBefore != cycleNumberAfter)
            {
                // Start flushing
                cmd.IsFlushingPileup = true;
                // Enqueue end flush
                Tell(_self, new ResetPeriodicTimerCommand(cmd));
            }
        }

        void ReceiveResetPeriodicTimerCommand(ResetPeriodicTimerCommand cmd)
        {
            cmd.PeriodicTimer.IsFlushingPileup = false;
        }
    }
}
