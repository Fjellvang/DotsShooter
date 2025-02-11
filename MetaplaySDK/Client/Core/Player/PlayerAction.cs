// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Model;

namespace Metaplay.Core.Player
{
    [MetaSerializable]
    [MetaImplicitMembersRange(100, 110)]
    [ModelActionExecuteFlags(ModelActionExecuteFlags.LeaderSynchronized)]
    public abstract class PlayerActionBase : ModelAction<IPlayerModelBase>
    {
        /// <summary>
        /// Unique identifier for the player action. Only unique for within a single player during a single session.
        /// </summary>
        public int  Id  { get; set; }
    }

    /// <summary>
    /// Base class for all <see cref="ModelAction"/>s affecting <see cref="IPlayerModelBase"/>.
    ///
    /// The <see cref="Execute"/> method receives the current <c>PlayerModel</c> (typecasted from the base <see cref="IPlayerModelBase"/>) as an argument.
    /// Logging and client/server event listeners can be accessed from it.
    /// </summary>
    [MetaSerializable]
    public abstract class PlayerActionCore<TModel> : PlayerActionBase where TModel : IPlayerModelBase
    {
        public PlayerActionCore() { }

        public override MetaActionResult InvokeExecute(IPlayerModelBase player, bool commit)
        {
            return Execute((TModel)player, commit);
        }

        /// <summary>
        /// Execute the given action against a <typeparamref name="TModel"/> player model object. Typically this is run on the client
        /// upon the player performing an action, and then slightly later on the server where the server's version of
        /// player model is updated. In some cases like detecting Desyncs, Actions can get executed
        /// again, on a previous state.
        ///
        /// The <see cref="Execute"/> should consist of two phases: validation and commit.
        /// All the action validation must happen before modifying the state. This ensures that a hacked
        /// client sending invalid <c>PlayerAction</c>s will not be able to modify the actual game state.
        /// If validation is not successful, i.e. if the action's preconditions are not fulfilled, <see cref="Execute"/>
        /// should return a relevant <see cref="MetaActionResult"/> which is other than <see cref="MetaActionResult.Success"/>.
        /// A non-successful action causes a warning to be logged.
        ///
        /// The action's <see cref="Execute"/> method should only modify the state if
        /// <paramref name="commit"/> is true.
        ///
        /// The execution may also trigger listener callbacks to let the client or the server know of key events.
        /// The listeners are accessible via ctx.ClientListener and ctx.ServerListener, respectively. ClientListener
        /// listener is typically used for updating UI, spawning effects, etc. ServerListener can be used to send
        /// let the server-side PlayerActor messages to other entities, interact with the database, etc.
        /// </summary>
        /// <param name="player">Player model to modify, also provides access to logging and listeners</param>
        /// <param name="commit">Boolean indicating whether the actions should modify the state or just do a dry-run
        /// (ie, only perform validations)</param>
        public abstract MetaActionResult Execute(TModel player, bool commit);
    }

    /// <summary>
    /// Base type for a transaction player-side finalization action.
    /// </summary>
    [MetaImplicitMembersRange(110, 120)]
    public abstract class PlayerTransactionFinalizingActionBase : PlayerSynchronizedServerActionBase
    {
    }

    public abstract class PlayerTransactionFinalizingActionCore<TModel> : PlayerTransactionFinalizingActionBase where TModel : IPlayerModelBase
    {
        public override MetaActionResult InvokeExecute(IPlayerModelBase player, bool commit)
        {
            return Execute((TModel)player, commit);
        }

        public abstract MetaActionResult Execute(TModel player, bool commit);
    }
}
