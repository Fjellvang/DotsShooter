// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

namespace Game.Logic
{
    public interface IPlayerModelServerListener
    {
    }

    public interface IPlayerModelClientListener
    {
        void OnGoldAdded();
    }

    public class EmptyPlayerModelServerListener : IPlayerModelServerListener
    {
        public static readonly EmptyPlayerModelServerListener Instance = new EmptyPlayerModelServerListener();
    }

    public class EmptyPlayerModelClientListener : IPlayerModelClientListener
    {
        public static readonly EmptyPlayerModelClientListener Instance = new EmptyPlayerModelClientListener();
        public void OnGoldAdded()
        {
        }
    }
}
