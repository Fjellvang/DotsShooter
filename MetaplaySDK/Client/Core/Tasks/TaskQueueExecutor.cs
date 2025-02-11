// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Metaplay.Core.Tasks
{
    /// <inheritdoc cref="TaskQueueExecutor{TArg1,TArg2,TArg3,TArg4}"/>
    public class TaskQueueExecutor : TaskQueueExecutor<object, object, object, object>
    {
        public TaskQueueExecutor(TaskScheduler scheduler) : base(scheduler)
        {
        }
    }

    /// <inheritdoc cref="TaskQueueExecutor{TArg1,TArg2,TArg3,TArg4}"/>
    public class TaskQueueExecutor<TArg1> : TaskQueueExecutor<TArg1, object, object, object>
    {
        public TaskQueueExecutor(TaskScheduler scheduler) : base(scheduler)
        {
        }
    }

    /// <inheritdoc cref="TaskQueueExecutor{TArg1,TArg2,TArg3,TArg4}"/>
    public class TaskQueueExecutor<TArg1, TArg2> : TaskQueueExecutor<TArg1, TArg2, object, object>
    {
        public TaskQueueExecutor(TaskScheduler scheduler) : base(scheduler)
        {
        }
    }

    /// <inheritdoc cref="TaskQueueExecutor{TArg1,TArg2,TArg3,TArg4}"/>
    public class TaskQueueExecutor<TArg1, TArg2, TArg3> : TaskQueueExecutor<TArg1, TArg2, TArg3, object>
    {
        public TaskQueueExecutor(TaskScheduler scheduler) : base(scheduler)
        {
        }
    }

    /// <summary>
    /// Executes Tasks in order on the desired TaskScheduler. A Task must be completed before
    /// any later tasks are executed. This means, that unlike with a task scheduler, enqueuing
    /// <c>Task.Delay</c> will block the whole queue until delay completion, after which the next task
    /// can proceed.
    ///
    /// <para>
    /// This is useful for throttling and for keeping the execution of multiple fire-and-forget tasks in order.
    /// </para>
    /// </summary>
    public class TaskQueueExecutor<TArg1, TArg2, TArg3, TArg4>
    {
        struct EnqueuedOp
        {
            public TArg1                                          Arg1;
            public TArg2                                          Arg2;
            public TArg3                                          Arg3;
            public TArg4                                          Arg4;
            public object                                         InnerActionArg;
            public Action<object, TArg1, TArg2, TArg3, TArg4>     Action;
            public Func<object, TArg1, TArg2, TArg3, TArg4, Task> AsyncAction;
        }

        readonly TaskScheduler _scheduler;
        readonly LogChannel    _log;

        readonly object        _lock;
        Queue<EnqueuedOp>      _queue;
        Task                   _worker;
        WorkerWaitNextWaitable _waitable;

        public TaskQueueExecutor(TaskScheduler scheduler, LogChannel log = null)
        {
            _scheduler = scheduler;
            _log       = log;
            _lock      = new object();
            _queue     = new Queue<EnqueuedOp>();
            _waitable  = new WorkerWaitNextWaitable(this);
        }

        /// <summary>
        /// Enqueues action to be run on the scheduler. The supplied arguments are passed into the Action. This enables
        /// the use of static, non-allocating actions where the creation of the delegate does not capture anything and is
        /// cached.
        /// </summary>
        public void EnqueueAsync(Action action)
        {
            EnqueueAsync(action, default, default, default, default, (innerAction, _, _, _, _) => ((Action)innerAction)());
        }

        /// <inheritdoc cref="EnqueueAsync(Action)"/>
        public void EnqueueAsync(TArg1 arg1, Action<TArg1> action)
        {
            EnqueueAsync(action, arg1, default, default, default, (innerAction, arg1, _, _, _) => ((Action<TArg1>)innerAction)(arg1));
        }

        /// <inheritdoc cref="EnqueueAsync(Action)"/>
        public void EnqueueAsync(TArg1 arg1, TArg2 arg2, Action<TArg1, TArg2> action)
        {
            EnqueueAsync(action, arg1, arg2, default, default, (innerAction, arg1, arg2, _, _) => ((Action<TArg1, TArg2>)innerAction)(arg1, arg2));
        }

        /// <inheritdoc cref="EnqueueAsync(Action)"/>
        public void EnqueueAsync(TArg1 arg1, TArg2 arg2, TArg3 arg3, Action<TArg1, TArg2, TArg3> action)
        {
            EnqueueAsync(action, arg1, arg2, arg3, default, (innerAction, arg1, arg2, arg3, _) => ((Action<TArg1, TArg2, TArg3>)innerAction)(arg1, arg2, arg3));
        }

        /// <inheritdoc cref="EnqueueAsync(Action)"/>
        public void EnqueueAsync(TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, Action<TArg1, TArg2, TArg3, TArg4> action)
        {
            EnqueueAsync(action, arg1, arg2, arg3, arg4, (innerAction, arg1, arg2, arg3, arg4) => ((Action<TArg1, TArg2, TArg3, TArg4>)innerAction)(arg1, arg2, arg3, arg4));
        }

        /// <inheritdoc cref="EnqueueAsync(Action)"/>
        public void EnqueueAsync(object innerActionArg, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, Action<object, TArg1, TArg2, TArg3, TArg4> action)
        {
            lock (_lock)
            {
                _queue.Enqueue(new EnqueuedOp()
                {
                    InnerActionArg = innerActionArg,
                    Arg1   = arg1,
                    Arg2   = arg2,
                    Arg3   = arg3,
                    Arg4   = arg4,
                    Action = action,
                });
                EnsureWorker();
            }
        }

        /// <summary>
        /// Enqueues async function to be executed on the scheduler.
        /// </summary>
        public void EnqueueAsync(Func<Task> asyncAction)
        {
            EnqueueAsync(asyncAction, default, default, default, default, (innerAction, _, _, _, _) => ((Func<Task>)innerAction)());
        }

        /// <inheritdoc cref="EnqueueAsync(Func{Task})"/>
        public void EnqueueAsync(TArg1 arg1, Func<TArg1, Task> asyncAction)
        {
            EnqueueAsync(asyncAction, arg1, default, default, default, (innerAction, arg1, _, _, _) => ((Func<TArg1, Task>)innerAction)(arg1));
        }

        /// <inheritdoc cref="EnqueueAsync(Func{Task})"/>
        public void EnqueueAsync(TArg1 arg1, TArg2 arg2, Func<TArg1, TArg2, Task> asyncAction)
        {
            EnqueueAsync(asyncAction, arg1, arg2, default, default, (innerAction, arg1, arg2, _, _) => ((Func<TArg1, TArg2, Task>)innerAction)(arg1, arg2));
        }

        /// <inheritdoc cref="EnqueueAsync(Func{Task})"/>
        public void EnqueueAsync(TArg1 arg1, TArg2 arg2, TArg3 arg3, Func<TArg1, TArg2, TArg3, Task> asyncAction)
        {
            EnqueueAsync(asyncAction, arg1, arg2, arg3, default, (innerAction, arg1, arg2, arg3, _) => ((Func<TArg1, TArg2, TArg3, Task>)innerAction)(arg1, arg2, arg3));
        }

        /// <inheritdoc cref="EnqueueAsync(Func{Task})"/>
        public void EnqueueAsync(TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, Func<TArg1, TArg2, TArg3, TArg4, Task> asyncAction)
        {
            EnqueueAsync(asyncAction, arg1, arg2, arg3, arg4, (innerAction, arg1, arg2, arg3, arg4) => ((Func<TArg1, TArg2, TArg3, TArg4, Task>)innerAction)(arg1, arg2, arg3, arg4));
        }

        /// <inheritdoc cref="EnqueueAsync(Func{Task})"/>
        public void EnqueueAsync(object innerActionArg, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, Func<object, TArg1, TArg2, TArg3, TArg4, Task> asyncAction)
        {
            lock (_lock)
            {
                _queue.Enqueue(new EnqueuedOp()
                {
                    InnerActionArg = innerActionArg,
                    Arg1           = arg1,
                    Arg2           = arg2,
                    Arg3           = arg3,
                    Arg4           = arg4,
                    AsyncAction    = asyncAction,
                });
                EnsureWorker();
            }
        }

        void EnsureWorker()
        {
            if (_worker == null)
                _worker = MetaTask.Run(Worker, _scheduler);
            _waitable.NotifyIfAwaitingLocked();
        }

        class WorkerWaitNextWaitable
            : INotifyCompletion
            #if NETCOREAPP3_0_OR_GREATER
            , IThreadPoolWorkItem
            #endif
        {
            TaskQueueExecutor<TArg1, TArg2, TArg3, TArg4> _executor;
            EnqueuedOp                                    _result;
            Action                                        _continuation;
            Action                                        _continuationForThreadPool;
            SynchronizationContext                        _continuationSynchronizationContext;

            public WorkerWaitNextWaitable(TaskQueueExecutor<TArg1, TArg2, TArg3, TArg4> executor)
            {
                _executor = executor;
            }

            public WorkerWaitNextWaitable GetAwaiter()
            {
                // Await next. Complete immediately if has next available.
                lock (_executor._lock)
                {
                    if (_executor._queue.TryDequeue(out var result))
                    {
                        _result     = result;
                        IsCompleted = true;
                    }
                    else
                        IsCompleted = false;
                }

                return this;
            }

            public void OnCompleted(Action continuation)
            {
                SynchronizationContext synchronizationContext = SynchronizationContext.Current;

                // Was not ready. Last check and if still not available, suspend and wait for wakeup.
                lock (_executor._lock)
                {
                    if (!_executor._queue.TryDequeue(out _result))
                    {
                        _continuation = continuation;
                        _continuationSynchronizationContext = synchronizationContext;
                        return;
                    }
                }

                // Completed while being enqueued
                continuation();
            }

            // Caller must have Lock
            public void NotifyIfAwaitingLocked()
            {
                if (_continuation != null)
                {
                    _result = _executor._queue.Dequeue();
                    if (_continuationSynchronizationContext != null)
                    {
                        // Enqueue on Synchronization context
    #pragma warning disable VSTHRD001 // Avoid legacy thread switching APIs
                        _continuationSynchronizationContext.Post((object action) =>
                        {
                            ((Action)action).Invoke();
                        }, _continuation);
    #pragma warning restore VSTHRD001 // Avoid legacy thread switching APIs
                    }
                    else
                    {
                        // Enqueue on thread pool
                        _continuationForThreadPool = _continuation;
                        #if NETCOREAPP3_0_OR_GREATER
                        ThreadPool.UnsafeQueueUserWorkItem(this, preferLocal: false);
                        #else
                        ThreadPool.UnsafeQueueUserWorkItem((self) =>
                        {
                            ((WorkerWaitNextWaitable)self).ExecuteOnThreadPool();
                        }, this);
                        #endif
                    }

                    _continuationSynchronizationContext = null;
                    _continuation = null;
                }
            }

            void ExecuteOnThreadPool()
            {
                Action continuation = _continuationForThreadPool;
                _continuationForThreadPool = null;
                continuation();
            }

            public bool IsCompleted { get; set; }
            public TaskQueueExecutor<TArg1, TArg2, TArg3, TArg4>.EnqueuedOp GetResult() => _result;

            #if NETCOREAPP3_0_OR_GREATER
            void IThreadPoolWorkItem.Execute() => ExecuteOnThreadPool();
            #endif
        }

        async Task Worker()
        {
            for (;;)
            {
                EnqueuedOp op = await _waitable;
                try
                {
                    if (op.Action != null)
                    {
                        op.Action(op.InnerActionArg, op.Arg1, op.Arg2, op.Arg3, op.Arg4);
                    }
                    if (op.AsyncAction != null)
                    {
                        await op.AsyncAction(op.InnerActionArg, op.Arg1, op.Arg2, op.Arg3, op.Arg4);
                    }
                }
                catch (Exception ex)
                {
                    _log?.Error(ex, "TaskQueueExecutor ran into an exception while executing");
                }
            }
        }
    }
}
