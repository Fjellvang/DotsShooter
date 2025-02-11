// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Model;

namespace Metaplay.Core.Player
{
    /// <summary>
    /// A physical device attached to a PlayerModel.
    /// </summary>
    [MetaSerializableDerived(102)]
    [MetaImplicitMembersRange(200, 300)]
    public class PlayerCompanyAccountAuthEntry : PlayerAuthEntryBase
    {
        PlayerCompanyAccountAuthEntry() { }
        public PlayerCompanyAccountAuthEntry(MetaTime attachedAt) : base(attachedAt)
        {
        }
    }
}
