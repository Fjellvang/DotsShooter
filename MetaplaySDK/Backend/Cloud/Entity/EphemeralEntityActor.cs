// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;

namespace Metaplay.Cloud.Entity
{
    /// <summary>
    /// A marker that annotates the actor is an ephemeral persisted entity actor.
    /// </summary>
    internal interface IEphemeralEntityActor
    {
    }

    /// <summary>
    /// Entity actor, which has no persisted state (ie, the state is forgotten whenever
    /// the entity shuts down).
    /// </summary>
    /// <remarks>
    /// The lifecycle of an entity is:
    /// <code>
    /// EphemeralEntityActor
    ///      |
    /// .---&gt;|
    /// |    | &lt;- (wakeup)
    /// |    |      Entity is woken up to respond to a message, or is a service.
    /// |    |
    /// |    |- Constructor()
    /// |    |       You should initialize readonly variables here
    /// |    |
    /// |    |- EntityActor.PreStart()
    /// |    |       Optional. Akka.Net Actor start hook. You should not do anything here.
    /// |    |       If overriden, You MUST call base.PreStart()
    /// |    |
    /// |    |- EntityActor.Initialize()
    /// |    |       Optional. Initialization before the first message is handled. This is
    /// |    |       the main init method.
    /// |    |
    /// |    |- [Actor is now running]
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
    public abstract class EphemeralEntityActor
        : EntityActor
        , IEphemeralEntityActor
    {
        protected EphemeralEntityActor(EntityId entityId) : base(entityId)
        {
        }

        protected override void PreStart()
        {
            base.PreStart();
        }
    }
}
