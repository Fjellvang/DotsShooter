// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Model;
using System;

namespace Metaplay.Core.Client
{
    [MetaSerializable]
    public class ClientSlotCore : ClientSlot
    {
        public static readonly ClientSlot Player            = new ClientSlotCore(1, nameof(Player));
        public static readonly ClientSlot Guild             = new ClientSlotCore(2, nameof(Guild));
        public static readonly ClientSlot Nft               = new ClientSlotCore(3, nameof(Nft));

        [Obsolete("Create a game-specific client slot instead.")]
        public static readonly ClientSlot PlayerDivisionLegacy = new ClientSlotCore(4, nameof(PlayerDivisionLegacy));

        public ClientSlotCore(int id, string name) : base(id, name) { }
    }
}
