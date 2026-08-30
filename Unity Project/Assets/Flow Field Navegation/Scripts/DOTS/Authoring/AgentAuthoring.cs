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

    [Header("Smoothing & Steering Settings")]
    [Tooltip("Velocidad de lerp para la transición suave de pasos.")]
    public float offsetSmoothingSpeed = 5.0f;
    [Tooltip("Peso de la fuerza de arrastre/corrección hacia la posición objetivo (0 a 1).")]
    [Range(0f, 1f)]
    public float formationForceWeight = 0.3f;

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
                OffsetSmoothingSpeed = authoring.offsetSmoothingSpeed,
                FormationForceWeight = authoring.formationForceWeight,

                CurrentSteps = 0,
                TargetSteps = 0,
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
    public float OffsetSmoothingSpeed;
    public float FormationForceWeight;

    // Estado interno
    public int CurrentSteps;
    public int TargetSteps;
    public float Timer;
    public float3 LastPosition;
}