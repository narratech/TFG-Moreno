using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// --- COMPONENTES DE DATOS ECS ---

// Este es el componente que RouteSystem espera encontrar. ¡Intocable!
public struct AgentComponent : IComponentData
{
    public int RouteId;
    public int NextRouteId;
    public int GraphId;
}

public struct NavAgentComponent : IComponentData
{
    public int CurrentNode;
    public int CurrentRegion;

    public float3 Velocity;

    public float MaxSpeed;
    public float MaxForce;
    public float MaxAngularSpeed;
    public float RotationArrivalThreshold;
    public float BoundaryPadding;
}

public struct FlowFieldSteeringComponent : IComponentData
{
    public float StepSize;
    public float StopRadius;
    public float TimeStamp;
    public float3 FormationOffset;

    public int CurrentSteps;
    public int MaxSteps;
    public float Timer;
    public float3 LastPosition;
}

// --- MONOBEHAVIOURS Y BAKERS ---

public class NavAgentBaker : Baker<NavAgent>
{
    public override void Bake(NavAgent authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);

        // 1. Añadimos el componente principal de Enrutamiento para que RouteSystem funcione
        AddComponent(entity, new AgentComponent
        {
            GraphId = 0,
            RouteId = -1,
            NextRouteId = -1
        });

        // 2. Añadimos el componente de navegación
        AddComponent(entity, new NavAgentComponent
        {
            CurrentNode = -1,
            CurrentRegion = -1,
            Velocity = float3.zero,
            MaxSpeed = authoring.MaxSpeed,
            MaxForce = authoring.MaxForce,
            MaxAngularSpeed = authoring.MaxAngularSpeed,
            RotationArrivalThreshold = authoring.RotationArrivalThreshold,
            BoundaryPadding = authoring.BoundaryPadding
        });
    }
}

public class FlowBaker : Baker<FlowFieldSteering>
{
    public override void Bake(FlowFieldSteering authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new FlowFieldSteeringComponent
        {
            StepSize = authoring.GetStepSize(),
            StopRadius = authoring.GetStopRadius(),
            TimeStamp = authoring.GetTimeStamp(),
            FormationOffset = authoring.GetFormationOffset(),
            CurrentSteps = authoring.CurrentSteps,
            MaxSteps = 0,
            Timer = 0f,
            LastPosition = float3.zero
        });
    }
}