using DotsShooter.State.Hierarchical;

namespace DotsShooter
{
    public readonly struct ShopCompletedMessage : IStateMessage<StateMessageType>
    {
        public StateMessageType Type => StateMessageType.ShopCompleted;
    }

    public readonly struct RoundCompletedMessage :  IStateMessage<StateMessageType>
    {
        public StateMessageType Type => StateMessageType.RoundCompleted;
    }

    public readonly struct PlayerDeathMessage : IStateMessage<StateMessageType> 
    {
        public StateMessageType Type => StateMessageType.PlayerDeath;
    }

    public enum StateMessageType
    {
        RoundCompleted,
        ShopCompleted,
        PlayerDeath
    }
}