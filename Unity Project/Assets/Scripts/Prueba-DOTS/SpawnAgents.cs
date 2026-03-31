using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class SpawnAgents : MonoBehaviour
{
    public int count = 10000;

    void Start()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        for (int i = 0; i < count; i++)
        {
            Entity e = em.CreateEntity();

            em.AddComponentData(e, new Position
            {
                Value = new float3(UnityEngine.Random.Range(-50, 50), 0, UnityEngine.Random.Range(-50, 50))
            });

            em.AddComponentData(e, new Velocity
            {
                Value = new float3(0, 0, 1)
            });
        }
    }
}