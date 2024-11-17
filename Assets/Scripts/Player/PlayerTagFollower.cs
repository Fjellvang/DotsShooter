using System.Collections;
using DotsShooter.Player;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class PlayerTagFollower : MonoBehaviour
{
    private EntityQuery _entityQuery;
    private EntityManager _entityManager;
    IEnumerator Start()
    {
        while (!(World.DefaultGameObjectInjectionWorld?.IsCreated ?? false))
        {
            yield return null;
        }
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _entityQuery = _entityManager.CreateEntityQuery(typeof(PlayerTag), typeof(LocalTransform));
    }

    // Update is called once per frame
    void Update()
    {
        if (!World.DefaultGameObjectInjectionWorld?.IsCreated ?? true) return; 
        if (_entityQuery.CalculateEntityCount() == 0)
        {
            Debug.LogWarning("No player entity found with PlayerTag");
            return;
        }
        var playerTransform = _entityManager.GetComponentData<LocalTransform>(_entityQuery.GetSingletonEntity());
        transform.position = playerTransform.Position;
    }
}
