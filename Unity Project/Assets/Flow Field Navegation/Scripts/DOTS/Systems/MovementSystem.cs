using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.VisualScripting;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(RouteSystem))]
[BurstCompile]
public partial struct MovementSystem : ISystem
{
    public void OnCreate(ref SystemState state) { }
    public void OnDestroy(ref SystemState state) {
        FlowFieldStorage.DisposeInstance();
    }
    public void OnUpdate(ref SystemState state)
    {
        var storage = FlowFieldStorage.Instance;

        // Crear e invocar el Job Paralelo
        var movementJob = new ProcessMovementJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            FieldMap = storage.FieldMap,
            Directions = storage.Directions.AsArray(),
            NavGraphs = storage.NavGraphs.AsArray(),
            Walkability = storage.Walkability.AsArray()
        };

        // Asignación paralela eficiente a través de los Workers de Unity
        state.Dependency = movementJob.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
public partial struct ProcessMovementJob : IJobEntity
{
    public float DeltaTime;

    [ReadOnly] public NativeParallelHashMap<FlowFieldKey, NativeFlowFieldInfo>.ReadOnly FieldMap;
    [ReadOnly] public NativeArray<float3> Directions;
    [ReadOnly] public NativeArray<NavGraphData> NavGraphs;
    [ReadOnly] public NativeArray<bool> Walkability;

    // Ejecución paralela por cada entidad que contenga AgentComponent y LocalTransform
    public void Execute(ref AgentComponent agent, ref LocalTransform transform)
    {
        int graphId = agent.GraphId;
        int routeId = agent.RouteId;

        if (routeId < 0 || graphId < 0 || graphId >= NavGraphs.Length)
            return;

        NavGraphData graph = NavGraphs[graphId];
        float3 currentPos = transform.Position;

        // --------------------------------------------------
        // 1. Condición: Pasa el tiempo O recorre StepSize
        // --------------------------------------------------
        agent.Timer += DeltaTime;
        float stepSize = agent.StepSize > 0f ? agent.StepSize : 1f;

        if (agent.Timer >= agent.TimeStamp ||
            math.distancesq(currentPos, agent.LastPosition) >= (stepSize * stepSize))
        {
            UpdateStepSize(ref agent, graph, currentPos, FieldMap, Directions, Walkability);
            agent.LastPosition = currentPos;
            agent.Timer = 0f;
        }

        // --------------------------------------------------
        // 2. Posición de muestreo real
        // --------------------------------------------------
        float3 desiredOffset = CalculateDesiredOffset(graph, currentPos, agent.FormationOffset);
        bool hasFormationOffset = math.lengthsq(desiredOffset) > 0.0001f;
        float3 samplePosition = currentPos;

        if (hasFormationOffset && agent.CurrentSteps > 0)
        {
            float3 offsetDir = math.normalize(desiredOffset);
            samplePosition = currentPos + offsetDir * (agent.CurrentSteps * stepSize);
        }

        // --------------------------------------------------
        // 3. Dirección del FlowField en SamplePosition
        // --------------------------------------------------
        float3 flowVector = SampleDirectionAtPosition(
            graph,
            graphId,
            routeId,
            samplePosition,
            FieldMap,
            Directions,
            Walkability);

        // --------------------------------------------------
        // 4. Arrival Steering + Parada Seca
        // --------------------------------------------------
        float maxSpeed = agent.Speed;
        float maxForce = maxSpeed * 10.0f;
        float3 currentVelocity = agent.Velocity;

        float minSpeed = 0.15f;
        float arrivalOffset = 0.3f;
        float distToTarget = math.distance(currentPos, samplePosition);

        float flowLenSq = math.lengthsq(flowVector);
        bool isFlowZero = flowLenSq <= 0.0025f;

        bool isAtTarget = hasFormationOffset && (agent.CurrentSteps == 0 && distToTarget <= arrivalOffset);

        float3 desiredVelocity = float3.zero;

        if (!isFlowZero && !isAtTarget)
        {
            float flowLen = math.sqrt(flowLenSq);
            float3 desiredDirection = flowVector / flowLen;

            float slowingRadius = 1.2f;
            float deceleration = math.clamp(flowLen / slowingRadius, 0.0f, 1.0f);

            desiredVelocity = desiredDirection * (maxSpeed * deceleration);

            float3 arrivalForce = desiredVelocity - currentVelocity;

            float forceLenSq = math.lengthsq(arrivalForce);
            if (forceLenSq > maxForce * maxForce)
            {
                arrivalForce = (arrivalForce / math.sqrt(forceLenSq)) * maxForce;
            }

            currentVelocity += arrivalForce * DeltaTime;
        }
        else
        {
            currentVelocity = math.lerp(currentVelocity, float3.zero, 30.0f * DeltaTime);
        }

        // Cierre de minSpeed
        if (isFlowZero || isAtTarget || math.lengthsq(currentVelocity) < (minSpeed * minSpeed))
        {
            currentVelocity = float3.zero;
        }

        float3 newPosition = currentPos + currentVelocity * DeltaTime;

        // --------------------------------------------------
        // 5. Rotación Suave
        // --------------------------------------------------
        quaternion newRotation = transform.Rotation;
        float speedSq = math.lengthsq(currentVelocity);

        if (speedSq >= (minSpeed * minSpeed) && math.lengthsq(desiredVelocity) > 0.01f)
        {
            float3 moveDir = currentVelocity / math.sqrt(speedSq);
            quaternion targetRotation;

            if (graph.Type == NavGraphType.QuadSphere)
            {
                float3 normal = math.normalize(newPosition - graph.Origin);
                float3 forward = moveDir - normal * math.dot(moveDir, normal);

                if (math.lengthsq(forward) > 0.0001f)
                {
                    targetRotation = quaternion.LookRotationSafe(math.normalize(forward), normal);
                }
                else
                {
                    targetRotation = transform.Rotation;
                }
            }
            else
            {
                targetRotation = quaternion.LookRotationSafe(moveDir, math.up());
            }

            float rotationLerpSpeed = 12.0f;
            newRotation = math.slerp(transform.Rotation, targetRotation, rotationLerpSpeed * DeltaTime);
        }

        // --------------------------------------------------
        // 6. Restricciones del Grafo
        // --------------------------------------------------
        NavGraphAPI.ConstrainPositionAndRotation(
            graph,
            Walkability,
            ref newPosition,
            ref currentVelocity,
            ref newRotation);

        agent.Velocity = currentVelocity;
        transform.Position = newPosition;
        transform.Rotation = newRotation;
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

        if (offsetLen < 0.001f)
        {
            agent.CurrentSteps = 0;
            return;
        }

        int absoluteMaxSteps = (int)math.ceil(offsetLen / stepSize);

        if (absoluteMaxSteps <= 0)
        {
            agent.CurrentSteps = 0;
            return;
        }

        float3 offsetDir = desiredOffset / offsetLen;

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
                break;
            }
        }

        if (maxWalkableStep == 0)
        {
            agent.CurrentSteps = 0;
            return;
        }

        for (int step = maxWalkableStep; step >= 1; step--)
        {
            float3 samplePos = currentPos + offsetDir * (step * stepSize);

            float3 sampleFlow = SampleDirectionAtPosition(
                graph,
                agent.GraphId,
                agent.RouteId,
                samplePos,
                fieldMap,
                directions,
                walkability);

            if (math.lengthsq(sampleFlow) < 0.0001f)
            {
                agent.CurrentSteps = step;
                return;
            }

            float3 flowDir = math.normalize(sampleFlow);
            bool pathBlocked = false;

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

            if (!pathBlocked)
            {
                agent.CurrentSteps = step;
                return;
            }
        }

        agent.CurrentSteps = 0;
    }

    private static float3 CalculateDesiredOffset(in NavGraphData graph, float3 currentPos, float3 formationOffset)
    {
        if (math.lengthsq(formationOffset) < 0.0001f)
            return float3.zero;

        int currentNode = NavGraphAPI.GetClosestNode(graph, currentPos);
        NavGraphAPI.GetNodeNormal(graph, currentNode, out float3 normal);
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
        in NativeArray<float3> directions,
        in NativeArray<bool> walkability)
    {
        FixedList64Bytes<int> nodes = new FixedList64Bytes<int>();
        NavGraphAPI.GetInterpolationNodes(graph, walkability, position, ref nodes);

        if (nodes.Length == 0)
            return float3.zero;

        float3 accumulatedDirection = float3.zero;
        float totalWeight = 0f;

        for (int i = 0; i < nodes.Length; i++)
        {
            int node = nodes[i];
            if (node < 0)
                continue;

            NavGraphAPI.GetNodePosition(graph, node, out float3 nodePos);
            float distSq = math.distancesq(position, nodePos);

            float weight = 1.0f / math.max(distSq, 0.0001f);

            if (!NavGraphAPI.IsWalkable(graph, walkability, node))
            {
                float3 repulsionVector = math.normalize(position - nodePos);
                accumulatedDirection += repulsionVector * weight * 2.0f;
                totalWeight += weight;
                continue;
            }

            int regionId = NavGraphAPI.GetRegionId(graph, node);
            var key = new FlowFieldKey(graphId, routeId, regionId);

            if (!fieldMap.TryGetValue(key, out NativeFlowFieldInfo field))
                continue;

            int localNode = NavGraphAPI.GetLocalNode(graph, node);
            if (localNode < 0 || localNode >= field.Length)
                continue;

            int directionIndex = field.StartIndex + localNode;
            float3 flowDir = directions[directionIndex];

            if (math.lengthsq(flowDir) > 0.0001f)
            {
                accumulatedDirection += flowDir * weight;
                totalWeight += weight;
            }
        }

        if (totalWeight < 0.0001f)
            return float3.zero;

        return accumulatedDirection / totalWeight;
    }
}