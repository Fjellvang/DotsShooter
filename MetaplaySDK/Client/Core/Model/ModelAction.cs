// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;

namespace Metaplay.Core.Model
{
    /// <summary>
    /// Base class for all Actions for various entities. Actions are the main way how
    /// the user can interact with an entity (eg, User, Alliance, etc.) in the game.
    ///
    /// By default, the fields of Actions are implicitly marked for serialization (for when actions are sent
    /// from client to server).
    ///
    /// Only entity-level actions (eg, PlayerAction or AllianceAction) should directly derive from this, and
    /// any concrete actions should derive from the domain-level action class.
    /// </summary>
    [MetaSerializable]
    [MetaImplicitMembersDefaultRangeForMostDerivedClass(1, 100)]
    public abstract class ModelAction
    {
        public abstract MetaActionResult InvokeExecute(IModel model, bool commit);
    }

    public abstract class ModelAction<TModel> : ModelAction
        where TModel : IModel
    {
        public sealed override MetaActionResult InvokeExecute(IModel model, bool commit)
        {
            return InvokeExecute((TModel)model, commit);
        }

        public abstract MetaActionResult InvokeExecute(TModel model, bool commit);
    }
}
