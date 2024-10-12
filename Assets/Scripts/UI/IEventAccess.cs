using DotsShooter.Events;
using Unity.Entities;

public interface IEventAccess
{
    EventSystem EventSystem => World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<EventSystem>();
    
    void Test(){}

    public void Test3()
    {
    }
}