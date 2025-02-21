namespace Game.Logic.PlayerActions
{
    /// <summary>
    /// Registry for game-specific ActionCodes, used by the individual PlayerAction classes.
    /// </summary>
    public static class ActionCodes
    {
        public const int PlayerAddGold = 5000;
        public const int PlayerBuyStat = 5001;
        public const int PlayerStartGame = 5002;
    }
}