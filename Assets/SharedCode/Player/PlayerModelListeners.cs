// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

namespace Game.Logic
{
    public interface IPlayerModelServerListener
    {
        void OnCollectLeaderboardStats(int kills, int goldCollected, int roundsCompleted);
    }
    
    public interface IPlayerModelClientListener
    {
        void OnGoldAdded();
        void OnStatUpdated();
    }

    public class EmptyPlayerModelServerListener : IPlayerModelServerListener
    {
        public static readonly EmptyPlayerModelServerListener Instance = new EmptyPlayerModelServerListener();
        public void OnCollectLeaderboardStats(int kills, int goldCollected, int roundsCompleted)
        {
        }
    }

    public class EmptyPlayerModelClientListener : IPlayerModelClientListener
    {
        public static readonly EmptyPlayerModelClientListener Instance = new EmptyPlayerModelClientListener();
        public void OnGoldAdded()
        {
        }

        public void OnStatUpdated()
        {
        }
    }
}
