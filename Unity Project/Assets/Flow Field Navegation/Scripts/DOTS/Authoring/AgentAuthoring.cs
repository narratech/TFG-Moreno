using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class AgentAuthoring : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 4.0f;

    [Header("Smart Offset Settings")]
    public Vector3 formationOffset = Vector3.zero;
    public float stepSize = 1.0f;
    public float timeStamp = 0.1f;

    public class AgentBaker : Baker<AgentAuthoring>
    {
        public override void Bake(AgentAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new AgentComponent
            {
                GraphId = 0,
                NextRouteId = -1,
                RouteId = -1,

                Speed = authoring.speed,
                Velocity = float3.zero,

                FormationOffset = authoring.formationOffset,
                StepSize = authoring.stepSize,
                TimeStamp = authoring.timeStamp,

                CurrentSteps = 0,
                Timer = 0f,
                LastPosition = float3.zero
            });
        }
    }
}

public struct AgentComponent : IComponentData
{
    public int GraphId;
    public int NextRouteId;
    public int RouteId;

    public float Speed;
    public float3 Velocity;

    // Configuración de Offset
    public float3 FormationOffset;
    public float StepSize;
    public float TimeStamp;

    // Estado interno
    public int CurrentSteps;
    public float Timer;
    public float3 LastPosition;
}