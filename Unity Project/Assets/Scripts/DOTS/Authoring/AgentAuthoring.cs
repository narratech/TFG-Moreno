using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using DOTSFlowField;

public class AgentAuthoring : MonoBehaviour
{
    public float speed = 4.0f;
    public class AgentBaker : Baker<AgentAuthoring>
    {
        public override void Bake(AgentAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new AgentComponent
            {
                Speed = authoring.speed,
                Velocity = float3.zero,
                RouteId = -1
            });
        }
    }
}