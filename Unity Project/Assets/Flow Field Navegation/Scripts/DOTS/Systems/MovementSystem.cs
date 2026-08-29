using DOTSFlowField;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(RouteSystem))]
[BurstCompile]
public partial struct MovementSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
    }

    public void OnDestroy(ref SystemState state)
    {
    }

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
                     RefRO<AgentComponent>,
                     RefRW<LocalTransform>>())
        {
            int graphId = agent.ValueRO.GraphId;
            int routeId = agent.ValueRO.RouteId;

            if (routeId < 0)
                continue;

            if (graphId < 0 || graphId >= navGraphs.Length)
                continue;

            NavGraphData graph = navGraphs[graphId];

            // --------------------------------------------------
            // 1. Nodo actual
            // --------------------------------------------------

            int currentNode = NavGraphAPI.GetClosestNode(
                graph,
                transform.ValueRO.Position);

            if (currentNode < 0)
                continue;

            // --------------------------------------------------
            // 2. Región actual
            // --------------------------------------------------

            int regionId = NavGraphAPI.GetRegionId(
                graph,
                currentNode);

            var key = new FlowFieldKey(
                graphId,
                routeId,
                regionId);

            // --------------------------------------------------
            // 3. Buscar Flow Field
            // --------------------------------------------------

            if (!fieldMap.TryGetValue(key, out NativeFlowFieldInfo field))
                continue;

            // --------------------------------------------------
            // 4. Obtener dirección del nodo
            // --------------------------------------------------

            int localNode = NavGraphAPI.GetLocalNode(
                graph,
                currentNode);

            if (localNode < 0 || localNode >= field.Length)
                continue;

            int directionIndex =
                field.StartIndex + localNode;

            float3 direction = directions[directionIndex];

            if (math.lengthsq(direction) < 0.0001f)
                continue;

            direction = math.normalize(direction);

            // --------------------------------------------------
            // 5. Movimiento
            // --------------------------------------------------

            float speed = agent.ValueRO.Speed;

            transform.ValueRW.Position +=
                direction * speed * deltaTime;

            // --------------------------------------------------
            // 6. Orientación
            // --------------------------------------------------

            if (graph.Type == NavGraphType.QuadSphere)
            {
                float3 normal = math.normalize(
                    transform.ValueRO.Position - graph.Origin);

                float3 forward =
                    direction -
                    normal * math.dot(direction, normal);

                if (math.lengthsq(forward) > 0.0001f)
                {
                    transform.ValueRW.Rotation =
                        quaternion.LookRotationSafe(
                            math.normalize(forward),
                            normal);
                }
            }
            else
            {
                if (math.lengthsq(direction) > 0.0001f)
                {
                    transform.ValueRW.Rotation =
                        quaternion.LookRotationSafe(
                            direction,
                            math.up());
                }
            }
        }
    }
}