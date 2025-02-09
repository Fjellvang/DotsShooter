namespace DotsShooter
{
    public class ShopState : GameBaseState
    {
        public ShopState(GameStateMachine stateMachine) : base(stateMachine)
        {
        }

        public override void OnEnter()
        {
            // Subscribe to exit shop event
        }
        
        public override void OnExit()
        {
            // Unsubscribe from exit shop event
        }
        
        public void ExitShop()
        {
            SendMessageToParent(new ShopCompletedMessage());
        }
    }
}