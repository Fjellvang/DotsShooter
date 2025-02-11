// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Entity;
using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.MultiplayerEntity;
using System.Threading.Tasks;

namespace Metaplay.Server.MultiplayerEntity
{
    /// <summary>
    /// Base class for actors for an ephemeral a Multiplayer Entity. Ephemeral entities are not stored into database and are always created anew.
    /// <para>
    /// Multiplayer Entity is an basic implementation for server-driven Entity. Multiple Clients may subscribe to an Multiplayer Entity and propose
    /// actions. Actions and Ticks executed by server are delivered back to client, allowing clients to see the changes in their copy of the Model.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The lifecycle of an entity is:
    /// <code>
    /// EphemeralMultiplayerEntityActorBase
    ///      |
    /// .---&gt;|
    /// |    | &lt;- (setup)
    /// |    |      Entity is woken up with InternalEntitySetupRequest message
    /// |    |
    /// |    |- Actor.Constructor()
    /// |    |       You should initialize readonly variables here
    /// |    |
    /// |    |- EntityActor.PreStart()
    /// |    |       Optional. Akka.Net Actor start hook. You should not do anything here.
    /// |    |       If overriden, You MUST call base.PreStart()
    /// |    |
    /// |    |- EntityActor.Initialize()
    /// |    |       Optional. Initialization before the first message is handled. You should not do anything here.
    /// |    |       If overriden, You MUST call base.Initialize()
    /// |    |
    /// |    |- MultiplayerEntityActorBase.OnSwitchedToModel()
    /// |    |       Optional. New model becomes active. You may attach Server-Listeners to the model.
    /// |    |
    /// |    |- MultiplayerEntityActorBase.SetUpModelAsync()
    /// |    |       Setups the initial Model state from InternalEntitySetupRequest parameters. A battle
    /// |    |       would set up its players.
    /// |    |
    /// |    |- MultiplayerEntityActorBase.OnEntityInitialized()
    /// |    |       The Main initialization method. The model has been set up.
    /// |    |
    /// |    |- [Entity is now running]
    /// |    |- [Message handlers]
    /// |    |
    /// |    |- EntityActor.OnShutdown()
    /// |    |       Optional. Cleanup method when just before actor is shut down.
    /// |    |
    /// |    |- EntityActor.PostStop()
    /// |    |       Optional. Akka.Net actor stop hook. You should not do anything here.
    /// |    |       If overriden, You MUST call base.PostStop()
    /// |    |
    /// |    |- Actor shuts down
    /// '----'
    /// </code>
    /// </remarks>
    public abstract partial class EphemeralMultiplayerEntityActorBase<TModel, TAction>
        : MultiplayerEntityActorBase<TModel, TAction>
        , IEphemeralEntityActor
        where TModel : class, IMultiplayerModel<TModel>, new()
        where TAction : ModelAction
    {
        protected EphemeralMultiplayerEntityActorBase(EntityId entityId, string logChannelName = null) : base(entityId, logChannelName)
        {
        }

        public sealed override Task PersistSnapshot() => Task.CompletedTask;
    }
}
