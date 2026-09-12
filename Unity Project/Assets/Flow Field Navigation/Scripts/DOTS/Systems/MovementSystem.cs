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
    public bool UseJobs;

    public void OnCreate(ref SystemState state)
    {
        UseJobs = true; // Alternar a false para ejecutar en Main Thread
    }

    public void OnDestroy(ref SystemState state)
    {
        FlowFieldStorage.DisposeInstance();
    }

    public void OnUpdate(ref SystemState state)
    {
        var storage = FlowFieldStorage.Instance;
        if (storage == null || !storage.NavGraphs.IsCreated) return;

        var movementJob = new ProcessMovementJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            FieldMap = storage.FieldMap,
            Directions = storage.Directions.AsArray(),
            NavGraphs = storage.NavGraphs.AsArray(),
            Walkability = storage.Walkability.AsArray()
        };

        if (UseJobs)
        {
            state.Dependency = movementJob.ScheduleParallel(state.Dependency);
        }
        else
        {
            state.Dependency.Complete();
            movementJob.Run();
        }
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

    // Fíjate que pedimos los tres componentes. RouteSystem no se entera del cambio, 
    // porque AgentComponent sigue existiendo y guardando GraphId y RouteId.
    public void Execute(ref AgentComponent agent, ref NavAgentComponent navAgent, ref FlowFieldSteeringComponent steering, ref LocalTransform transform)
    {
        int graphId = agent.GraphId;
        int routeId = agent.RouteId;

        if (routeId < 0 || graphId < 0 || graphId >= NavGraphs.Length)
            return;

        NavGraphData graph = NavGraphs[graphId];
        float3 currentPos = transform.Position;

        // --------------------------------------------------
        // COMPROBACIÓN BÁSICA DE NODO ACTUAL
        // --------------------------------------------------
        int currentNode = NavGraphAPI.GetClosestNode(in graph, currentPos);
        navAgent.CurrentNode = currentNode;
        navAgent.CurrentRegion = NavGraphAPI.GetRegionId(in graph, currentNode);

        bool isCurrentNodeBlocked = currentNode >= 0 && !NavGraphAPI.IsWalkable(in graph, in Walkability, currentNode);

        float3 activeFormationOffset = steering.FormationOffset;
        if (isCurrentNodeBlocked)
        {
            steering.Timer = 0f;
            activeFormationOffset = float3.zero;
        }

        // --------------------------------------------------
        // 1. CONDICIÓN: PASA EL TIEMPO O RECORRE STEPSIZE
        // --------------------------------------------------
        steering.Timer += DeltaTime;
        float stepSize = steering.StepSize > 0f ? steering.StepSize : 1f;

        if (steering.Timer >= steering.TimeStamp || math.distancesq(currentPos, steering.LastPosition) >= (stepSize * stepSize))
        {
            UpdateStepSize(ref steering, in graph, currentPos, activeFormationOffset, graphId, routeId, FieldMap, Directions, Walkability);
            steering.LastPosition = currentPos;
            steering.Timer = 0f;
        }

        // --------------------------------------------------
        // 2. POSICIÓN DE MUESTREO REAL
        // --------------------------------------------------
        float3 desiredOffset = CalculateDesiredOffset(in graph, currentPos, activeFormationOffset);
        bool hasFormationOffset = math.lengthsq(desiredOffset) > 0.0001f;
        float3 samplePosition = currentPos;

        if (hasFormationOffset && steering.CurrentSteps > 0)
        {
            float3 offsetDir = math.normalize(desiredOffset);
            samplePosition = currentPos + offsetDir * (steering.CurrentSteps * stepSize);
        }

        // --------------------------------------------------
        // 3. DIRECCIÓN DEL FLOWFIELD EN SAMPLEPOSITION
        // --------------------------------------------------
        float3 flowVector = SampleDirectionAtPosition(in graph, graphId, routeId, samplePosition, FieldMap, Directions, Walkability);

        // --------------------------------------------------
        // 4. ARRIVAL STEERING + WALL AVOIDANCE (Físicas del Managed)
        // --------------------------------------------------
        float maxSpeed = navAgent.MaxSpeed;
        float maxForce = navAgent.MaxForce;
        float3 currentVelocity = navAgent.Velocity;

        float minSpeed = 0.15f;
        float arrivalOffset = steering.StopRadius > 0f ? steering.StopRadius : 0.3f;
        float distToTarget = math.distance(currentPos, samplePosition);

        float flowLenSq = math.lengthsq(flowVector);
        bool isFlowZero = flowLenSq <= 0.0025f;
        bool isAtTarget = hasFormationOffset && (steering.CurrentSteps == 0 && distToTarget <= arrivalOffset);

        float3 desiredVelocity = float3.zero;

        NavGraphAPI.GetNodeNormal(in graph, currentNode, out float3 surfaceNormal);
        bool isVolumetric = math.lengthsq(surfaceNormal) < 0.0001f;

        if (!isFlowZero && !isAtTarget)
        {
            float flowLen = math.sqrt(flowLenSq);
            float3 desiredDirection = flowVector / flowLen;

            // Proyección inicial sobre la superficie
            if (!isVolumetric)
            {
                surfaceNormal = math.normalize(surfaceNormal);
                desiredDirection = ProjectOnPlane(desiredDirection, surfaceNormal);
            }

            // Repulsión fuerte contra muros colindantes (Físicas managed)
            if (EvaluateUnwalkableNodesNormal(currentPos, surfaceNormal, transform.Forward(), in graph, navAgent.BoundaryPadding, isVolumetric, Walkability, out float3 wallNormal, out float penetrationDepth))
            {
                if (math.dot(desiredDirection, wallNormal) < 0f)
                {
                    desiredDirection = ProjectOnPlane(desiredDirection, wallNormal);
                }
            }

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

        if (isFlowZero || isAtTarget || math.lengthsq(currentVelocity) < (minSpeed * minSpeed))
        {
            currentVelocity = float3.zero;
        }

        // --------------------------------------------------
        // 5. LOOK-AHEAD COLLISION (Predicción de choque del Managed)
        // --------------------------------------------------
        if (EvaluateUnwalkableNodesNormal(currentPos, surfaceNormal, transform.Forward(), in graph, navAgent.BoundaryPadding, isVolumetric, Walkability, out float3 wallNormalRepel, out float penDepth))
        {
            float velDot = math.dot(currentVelocity, wallNormalRepel);
            if (velDot < 0f) currentVelocity -= wallNormalRepel * velDot;
            if (penDepth > 0f) currentPos += wallNormalRepel * penDepth;
        }

        if (math.lengthsq(currentVelocity) > 0.0001f)
        {
            float3 nextPos = currentPos + currentVelocity * DeltaTime;
            int nextNode = NavGraphAPI.GetClosestNode(in graph, nextPos);
            if (nextNode >= 0 && !NavGraphAPI.IsWalkable(in graph, in Walkability, nextNode))
            {
                NavGraphAPI.GetNodePosition(in graph, nextNode, out float3 wallPos);
                float3 dirFromWall = currentPos - wallPos;

                NavGraphAPI.GetNodeNormal(in graph, nextNode, out float3 nextSurfaceNormal);
                bool nextIsVolumetric = math.lengthsq(nextSurfaceNormal) < 0.0001f;

                float3 emergencyWallNorm = nextIsVolumetric ? math.normalize(dirFromWall) : math.normalize(ProjectOnPlane(dirFromWall, nextSurfaceNormal));
                float velDot = math.dot(currentVelocity, emergencyWallNorm);
                if (velDot < 0f) currentVelocity -= emergencyWallNorm * velDot;
            }
        }

        float3 newPosition = currentPos + currentVelocity * DeltaTime;

        // --------------------------------------------------
        // 6. ROTACIÓN SUAVE Y PRECISA
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
                targetRotation = math.lengthsq(forward) > 0.0001f ? quaternion.LookRotationSafe(math.normalize(forward), normal) : transform.Rotation;
            }
            else
            {
                targetRotation = quaternion.LookRotationSafe(moveDir, math.up());
            }

            // Aplicamos la lógica de Arrival Rotation Threshold del script managed
            float angleDiff = math.degrees(math.acos(math.clamp(math.abs(math.dot(transform.Rotation.value, targetRotation.value)), 0f, 1f)) * 2f);
            float maxStepThisFrame = navAgent.MaxAngularSpeed * DeltaTime;
            float rotationStep = maxStepThisFrame;

            if (angleDiff < navAgent.RotationArrivalThreshold && navAgent.RotationArrivalThreshold > 0f)
            {
                rotationStep = maxStepThisFrame * (angleDiff / navAgent.RotationArrivalThreshold);
            }

            if (angleDiff <= rotationStep || angleDiff < 0.1f)
            {
                newRotation = targetRotation;
            }
            else
            {
                float slerpT = rotationStep / angleDiff;
                newRotation = math.slerp(transform.Rotation, targetRotation, slerpT);
            }
        }

        // --------------------------------------------------
        // 7. RESTRICCIONES FINALES DEL GRAFO
        // --------------------------------------------------
        NavGraphAPI.ConstrainPositionAndRotation(in graph, Walkability, ref newPosition, ref currentVelocity, ref newRotation);

        navAgent.Velocity = currentVelocity;
        transform.Position = newPosition;
        transform.Rotation = newRotation;
    }

    // --- MÉTODOS DE SOPORTE INTERNO Y FÍSICA ---

    private static float3 ProjectOnPlane(float3 vector, float3 planeNormal)
        => vector - planeNormal * math.dot(vector, planeNormal);

    private static bool EvaluateUnwalkableNodesNormal(float3 position, float3 surfaceNormal, float3 forward, in NavGraphData graph, float padding, bool isVolumetric, in NativeArray<bool> walkability, out float3 wallNormal, out float penetrationDepth)
    {
        wallNormal = float3.zero;
        penetrationDepth = 0f;

        FixedList64Bytes<int> nodes = new FixedList64Bytes<int>();
        NavGraphAPI.GetInterpolationNodes(in graph, position, ref nodes);
        if (nodes.Length <= 0) return false;

        float3 accumNormal = float3.zero;
        float totalWeight = 0f;
        float maxPenetration = 0f;

        for (int i = 0; i < nodes.Length; i++)
        {
            int node = nodes[i];
            if (NavGraphAPI.IsWalkable(in graph, in walkability, node)) continue;

            NavGraphAPI.GetNodePosition(in graph, node, out float3 nodePos);
            float3 diff = position - nodePos;
            float3 collisionDiff = isVolumetric ? diff : ProjectOnPlane(diff, surfaceNormal);
            float dist = math.length(collisionDiff);

            if (dist < 0.0001f)
            {
                collisionDiff = isVolumetric ? -forward : ProjectOnPlane(-forward, surfaceNormal);
                if (math.lengthsq(collisionDiff) < 0.0001f) collisionDiff = new float3(0, 0.01f, 0);
                dist = 0.01f;
            }

            float3 dirFromObstacle = collisionDiff / dist;
            float weight = 1f / (dist * dist);
            accumNormal += dirFromObstacle * weight;
            totalWeight += weight;

            float overlap = padding - dist;
            if (overlap > maxPenetration) maxPenetration = overlap;
        }

        if (totalWeight > 0f && math.lengthsq(accumNormal) > 0.0001f)
        {
            wallNormal = math.normalize(accumNormal);
            penetrationDepth = math.max(0f, maxPenetration);
            return true;
        }
        return false;
    }

    private static void UpdateStepSize(
        ref FlowFieldSteeringComponent steering,
        in NavGraphData graph,
        float3 currentPos,
        float3 activeFormationOffset,
        int graphId,
        int routeId,
        in NativeParallelHashMap<FlowFieldKey, NativeFlowFieldInfo>.ReadOnly fieldMap,
        in NativeArray<float3> directions,
        in NativeArray<bool> walkability)
    {
        float3 desiredOffset = CalculateDesiredOffset(in graph, currentPos, activeFormationOffset);
        float offsetLen = math.length(desiredOffset);
        float stepSize = steering.StepSize > 0f ? steering.StepSize : 1f;

        if (offsetLen < 0.001f)
        {
            steering.CurrentSteps = 0;
            steering.MaxSteps = 0;
            return;
        }

        int absoluteMaxSteps = (int)math.ceil(offsetLen / stepSize);
        steering.MaxSteps = absoluteMaxSteps;

        if (absoluteMaxSteps <= 0)
        {
            steering.CurrentSteps = 0;
            return;
        }

        float3 offsetDir = desiredOffset / offsetLen;
        int targetCheckStep = math.min(steering.CurrentSteps + 1, absoluteMaxSteps);
        int maxWalkableStep = 0;

        for (int step = 1; step <= targetCheckStep; step++)
        {
            float3 checkPos = currentPos + offsetDir * (step * stepSize);
            int node = NavGraphAPI.GetClosestNode(in graph, checkPos);

            if (node >= 0 && NavGraphAPI.IsWalkable(in graph, walkability, node) && NavGraphAPI.IsInBounds(in graph, checkPos))
                maxWalkableStep = step;
            else
                break;
        }

        if (maxWalkableStep == 0)
        {
            steering.CurrentSteps = 0;
            return;
        }

        for (int step = maxWalkableStep; step >= 1; step--)
        {
            float3 samplePos = currentPos + offsetDir * (step * stepSize);
            float3 sampleFlow = SampleDirectionAtPosition(in graph, graphId, routeId, samplePos, fieldMap, directions, walkability);

            if (math.lengthsq(sampleFlow) < 0.0001f)
            {
                steering.CurrentSteps = step;
                return;
            }

            float3 flowDir = math.normalize(sampleFlow);
            bool pathBlocked = false;

            for (int flowStep = 1; flowStep <= step; flowStep++)
            {
                float3 agentProjectionPos = currentPos + flowDir * (flowStep * stepSize * 0.5f);
                int projNode = NavGraphAPI.GetClosestNode(in graph, agentProjectionPos);

                if (projNode < 0 || !NavGraphAPI.IsWalkable(in graph, walkability, projNode) || !NavGraphAPI.IsInBounds(in graph, agentProjectionPos))
                {
                    pathBlocked = true;
                    break;
                }
            }

            if (!pathBlocked)
            {
                steering.CurrentSteps = step;
                return;
            }
        }
        steering.CurrentSteps = 0;
    }

    private static float3 CalculateDesiredOffset(in NavGraphData graph, float3 currentPos, float3 formationOffset)
    {
        if (math.lengthsq(formationOffset) < 0.0001f) return float3.zero;

        int currentNode = NavGraphAPI.GetClosestNode(in graph, currentPos);
        NavGraphAPI.GetNodeNormal(in graph, currentNode, out float3 normal);
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
        NavGraphAPI.GetInterpolationNodes(in graph, position, ref nodes);
        if (nodes.Length == 0) return float3.zero;

        float3 accumulatedDirection = float3.zero;
        float totalWeight = 0f;

        for (int i = 0; i < nodes.Length; i++)
        {
            int node = nodes[i];
            if (node < 0) continue;

            NavGraphAPI.GetNodePosition(in graph, node, out float3 nodePos);
            float distSq = math.distancesq(position, nodePos);
            float weight = 1.0f / math.max(distSq, 0.0001f);

            // Suave repulsión intrínseca del FlowField (tu código original)
            if (!NavGraphAPI.IsWalkable(in graph, walkability, node))
            {
                float3 repulsionVector = math.normalize(position - nodePos);
                accumulatedDirection += repulsionVector * weight * 2.0f;
                totalWeight += weight;
                continue;
            }

            int regionId = NavGraphAPI.GetRegionId(in graph, node);
            var key = new FlowFieldKey(graphId, routeId, regionId);

            if (!fieldMap.TryGetValue(key, out NativeFlowFieldInfo field)) continue;

            int localNode = NavGraphAPI.GetLocalNode(in graph, node);
            if (localNode < 0 || localNode >= field.Length) continue;

            float3 flowDir = directions[field.StartIndex + localNode];

            if (math.lengthsq(flowDir) > 0.0001f)
            {
                accumulatedDirection += flowDir * weight;
                totalWeight += weight;
            }
        }

        return totalWeight < 0.0001f ? float3.zero : accumulatedDirection / totalWeight;
    }
}