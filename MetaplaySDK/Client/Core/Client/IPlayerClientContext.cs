// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Model;
using Metaplay.Core.Player;
using System;

namespace Metaplay.Core.Client
{
    public interface IPlayerClientContext : IEntityClientContext
    {
        [Obsolete("This should not be called manually. ClientStore.EarlyUpdate handles this")]
        new void EarlyUpdate();
        [Obsolete("This should not be called manually. ClientStore.UpdateLogic handles this")]
        new void Update(MetaTime time);

        IClientPlayerModelJournal Journal { get; }
        MetaDuration GetAccumulatedTimeSinceLastTick();
        DateTime LastUpdateTimeDebug { get; }

        MetaActionResult ExecuteAction(PlayerActionBase action);
        MetaActionResult ExecuteAction(PlayerActionBase action, out int actionId);
        MetaActionResult DryExecuteAction(PlayerActionBase action);

        /// <summary>
        /// The server requests us to execute an action. Execute it immediately.
        /// Note that the UnsynchronizedServerActions have already been executed on the server
        /// (on some previous tick), so they may only modify state that is marked with the [NoChecksum] attribute.
        /// </summary>
        /// <param name="execute">Message containing the UnsynchronizedServerAction to execute</param>
        /// <returns>The executed action</returns>
        PlayerActionBase ExecuteServerAction(PlayerExecuteUnsynchronizedServerAction execute);

        /// <summary>
        /// The server requests us to enqueue an action. Execute it immediately.
        /// </summary>
        /// <param name="enqueued">Message containing the SynchronizedServerAction to execute</param>
        /// <returns>The executed action</returns>
        PlayerActionBase ExecuteServerAction(PlayerEnqueueSynchronizedServerAction enqueued);

        void HandleGuildTransactionResponse(PlayerTransactionFinalizingActionBase action, int trackingId);
        void MarkPendingActionsAsFlushed();
        PlayerChecksumMismatchDetails ResolveChecksumMismatch(PlayerChecksumMismatch mismatch);
        void PurgeSnapshotsUntil(JournalPosition untilPosition);
        void OnDisconnected(); // \todo [jarkko]: allow PlayerClientContext to listen to messages automatically.
    }
}
