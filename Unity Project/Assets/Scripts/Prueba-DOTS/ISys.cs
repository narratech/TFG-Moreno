using System.Linq;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct MovementSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {

        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (transform, speed) in
                SystemAPI.Query<RefRW<LocalTransform>, RefRO<MoveSpeed>>())
        {
            float3 dir = new float3(0, 0, 1);

            transform.ValueRW.Position += dir * speed.ValueRO.Value * dt;
        }
    }
}