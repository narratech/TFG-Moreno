using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(RouteSystem))]
[BurstCompile]
public partial struct MovementSystem : ISystem
{
    public void OnCreate(ref SystemState state) { }
    public void OnDestroy(ref SystemState state) { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var storage = FlowFieldStorage.Instance;

        var fieldMap = storage.FieldMap;
        var directions = storage.Directions;
        var navGraphs = storage.NavGraphs;
        var walkability = storage.Walkability;

        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (agent, transform) in
                 SystemAPI.Query<
                     RefRW<AgentComponent>,
                     RefRW<LocalTransform>>())
        {
            int graphId = agent.ValueRO.GraphId;
            int routeId = agent.ValueRO.RouteId;

            if (routeId < 0 || graphId < 0 || graphId >= navGraphs.Length)
                continue;

            NavGraphData graph = navGraphs[graphId];
            float3 currentPos = transform.ValueRO.Position;

            // --------------------------------------------------
            // 1. Condición: Pasa el tiempo O recorre StepSize
            // --------------------------------------------------
            agent.ValueRW.Timer += deltaTime;
            float stepSize = agent.ValueRO.StepSize > 0f ? agent.ValueRO.StepSize : 1f;

            if (agent.ValueRO.Timer >= agent.ValueRO.TimeStamp ||
                math.distancesq(currentPos, agent.ValueRO.LastPosition) >= (stepSize * stepSize))
            {
                UpdateStepSize(ref agent.ValueRW, graph, currentPos, fieldMap, directions, walkability);
                agent.ValueRW.LastPosition = currentPos;
                agent.ValueRW.Timer = 0f;
            }

            // --------------------------------------------------
            // 2. Posición de muestreo real
            // --------------------------------------------------
            float3 desiredOffset = CalculateDesiredOffset(graph, currentPos, agent.ValueRO.FormationOffset);
            float3 samplePosition = currentPos;

            if (agent.ValueRO.CurrentSteps > 0 && math.lengthsq(desiredOffset) > 0.0001f)
            {
                float3 offsetDir = math.normalize(desiredOffset);
                samplePosition = currentPos + offsetDir * (agent.ValueRO.CurrentSteps * stepSize);
            }

            // --------------------------------------------------
            // 3. Dirección del FlowField en SamplePosition
            // --------------------------------------------------
            float3 flowDirection = SampleDirectionAtPosition(
                graph,
                graphId,
                routeId,
                samplePosition,
                fieldMap,
                directions);

            if (math.lengthsq(flowDirection) < 0.0001f)
                continue;

            flowDirection = math.normalize(flowDirection);

            // --------------------------------------------------
            // 4. Integrar Movimiento y Orientación
            // --------------------------------------------------
            float speed = agent.ValueRO.Speed;
            float3 velocity = flowDirection * speed;

            float3 newPosition = currentPos + velocity * deltaTime;
            quaternion newRotation = transform.ValueRO.Rotation;

            if (graph.Type == NavGraphType.QuadSphere)
            {
                float3 normal = math.normalize(newPosition - graph.Origin);
                float3 forward = flowDirection - normal * math.dot(flowDirection, normal);

                if (math.lengthsq(forward) > 0.0001f)
                {
                    newRotation = quaternion.LookRotationSafe(math.normalize(forward), normal);
                }
            }
            else
            {
                newRotation = quaternion.LookRotationSafe(flowDirection, math.up());
            }

            // --------------------------------------------------
            // 5. Restricciones del Grafo
            // --------------------------------------------------
            NavGraphAPI.ConstrainPositionAndRotation(
                graph,
                walkability,
                ref newPosition,
                ref velocity,
                ref newRotation);

            transform.ValueRW.Position = newPosition;
            transform.ValueRW.Rotation = newRotation;
        }
    }

    private static void UpdateStepSize(
    ref AgentComponent agent,
    in NavGraphData graph,
    float3 currentPos,
    in NativeParallelHashMap<FlowFieldKey, NativeFlowFieldInfo>.ReadOnly fieldMap,
    in NativeArray<float3> directions,
    in NativeArray<bool> walkability)
    {
        float3 desiredOffset = CalculateDesiredOffset(graph, currentPos, agent.FormationOffset);
        float offsetLen = math.length(desiredOffset);
        float stepSize = agent.StepSize > 0f ? agent.StepSize : 1f;


        UnityEngine.Debug.Log(offsetLen);

        if (offsetLen < 0.001f)
        {
            agent.CurrentSteps = 0;
            return;
        }

        // Cantidad máxima de pasos absoluta según el DesiredOffset
        int absoluteMaxSteps = (int)math.ceil(offsetLen / stepSize);

        if (absoluteMaxSteps <= 0)
        {
            agent.CurrentSteps = 0;
            return;
        }

        float3 offsetDir = desiredOffset / offsetLen;

        // --------------------------------------------------
        // FASE 1: Explorar transitabilidad (Probar hasta CurrentSteps + 1 o el máximo absoluto)
        // --------------------------------------------------
        // Intentamos incrementar 1 paso respecto al actual, sin exceder absoluteMaxSteps
        int targetCheckStep = math.min(agent.CurrentSteps + 1, absoluteMaxSteps);
        int maxWalkableStep = 0;

        for (int step = 1; step <= targetCheckStep; step++)
        {
            float3 checkPos = currentPos + offsetDir * (step * stepSize);
            int node = NavGraphAPI.GetClosestNode(graph, checkPos);

            if (node >= 0 && NavGraphAPI.IsWalkable(graph, walkability, node))
            {
                maxWalkableStep = step;
            }
            else
            {
                // Encontramos pared -> rompemos la exploración
                break;
            }
        }

        // Si ni el paso 1 es transitable, offset = 0
        if (maxWalkableStep == 0)
        {
            agent.CurrentSteps = 0;
            return;
        }

        // --------------------------------------------------
        // FASE 2: Validar el flujo de retroceso desde maxWalkableStep hasta 1
        // --------------------------------------------------
        for (int step = maxWalkableStep; step >= 1; step--)
        {
            float3 samplePos = currentPos + offsetDir * (step * stepSize);

            // 1. Obtener dirección del flujo en la posición hipotética
            float3 sampleFlow = SampleDirectionAtPosition(
                graph,
                agent.GraphId,
                agent.RouteId,
                samplePos,
                fieldMap,
                directions);

            if (math.lengthsq(sampleFlow) < 0.0001f)
            {
                agent.CurrentSteps = step;
                return;
            }

            float3 flowDir = math.normalize(sampleFlow);
            bool pathBlocked = false;

            // 2. Proyectar la misma cantidad de pasos desde la posición actual del agente
            for (int flowStep = 1; flowStep <= step; flowStep++)
            {
                float3 agentProjectionPos = currentPos + flowDir * (flowStep * stepSize);
                int projNode = NavGraphAPI.GetClosestNode(graph, agentProjectionPos);

                if (projNode < 0 || !NavGraphAPI.IsWalkable(graph, walkability, projNode))
                {
                    pathBlocked = true;
                    break;
                }
            }

            // Si la trayectoria proyectada está libre de muros, aceptamos este paso
            if (!pathBlocked)
            {
                agent.CurrentSteps = step;
                return;
            }
        }

        // Si todas las comprobaciones de flujo fallaron, volvemos a 0
        agent.CurrentSteps = 0;
    }

    private static float3 CalculateDesiredOffset(in NavGraphData graph, float3 currentPos, float3 formationOffset)
    {
        if (math.lengthsq(formationOffset) < 0.0001f)
            return float3.zero;

        int currentNode = NavGraphAPI.GetClosestNode(graph, currentPos);
        float3 normal = NavGraphAPI.GetNodeNormal(graph, currentNode);
        float3 desiredOffset = formationOffset;

        if (math.lengthsq(normal) > 0.0001f)
        {
            float3 up = new float3(0, 1, 0);
            float dot = math.dot(up, normal);

            if (dot < 0.9999f)
            {
                float3 axis = math.cross(up, normal);
                float axisLen = math.length(axis);

                if (axisLen > 0.0001f)
                {
                    float angle = math.acos(math.clamp(dot, -1f, 1f));
                    quaternion rot = quaternion.AxisAngle(axis / axisLen, angle);
                    desiredOffset = math.rotate(rot, formationOffset);
                }
                else if (dot < -0.9999f)
                {
                    quaternion rot = quaternion.AxisAngle(new float3(1, 0, 0), math.PI);
                    desiredOffset = math.rotate(rot, formationOffset);
                }
            }
        }

        return desiredOffset;
    }

    private static float3 SampleDirectionAtPosition(
        in NavGraphData graph,
        int graphId,
        int routeId,
        float3 position,
        in NativeParallelHashMap<FlowFieldKey, NativeFlowFieldInfo>.ReadOnly fieldMap,
        in NativeArray<float3> directions)
    {
        int node = NavGraphAPI.GetClosestNode(graph, position);
        if (node < 0)
            return float3.zero;

        int regionId = NavGraphAPI.GetRegionId(graph, node);
        var key = new FlowFieldKey(graphId, routeId, regionId);

        if (!fieldMap.TryGetValue(key, out NativeFlowFieldInfo field))
            return float3.zero;

        int localNode = NavGraphAPI.GetLocalNode(graph, node);
        if (localNode < 0 || localNode >= field.Length)
            return float3.zero;

        int directionIndex = field.StartIndex + localNode;
        return directions[directionIndex];
    }
}