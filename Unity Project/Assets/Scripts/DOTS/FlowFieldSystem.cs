using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using BurstCompile = Unity.Burst.BurstCompileAttribute;

namespace DOTSFlowField
{
    /// <summary>
    /// Sistema principal que aplica el flow field a los agentes usando regiones activas.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(IntegrationFieldSystem))]
    public partial struct FlowFieldSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var bridge = FlowFieldBridge.Instance;
            if (bridge == null || bridge.gridNavGraph == null) return;

            var regionQuery = SystemAPI.QueryBuilder()
                .WithAll<RegionRouteConfig, FlowDirectionBuffer>()
                .Build();

            var regionEntities = regionQuery.ToEntityArray(Allocator.TempJob);
            var regionConfigs = regionQuery.ToComponentDataArray<RegionRouteConfig>(Allocator.TempJob);

            var activeRegionsLookup = new NativeParallelHashMap<int2, Entity>(
                regionEntities.Length,
                Allocator.TempJob);

            for (int i = 0; i < regionEntities.Length; i++)
            {
                var config = regionConfigs[i];

                if (config.RouteEntity == Entity.Null)
                    continue;

                if (config.State == RegionState.Generated ||
                    config.State == RegionState.Required)
                {
                    int2 key = new int2(config.RouteEntity.Index, config.RegionId);
                    activeRegionsLookup.TryAdd(key, regionEntities[i]);
                }
            }

            var moveJob = new MoveAgentsJob
            {
                ActiveRegionsLookup = activeRegionsLookup,
                FlowDirectionsLookup = SystemAPI.GetBufferLookup<FlowDirectionBuffer>(true),
                CurrentRouteEntity = bridge.CurrentActiveRouteEntity,
                DeltaTime = SystemAPI.Time.DeltaTime
            };

            state.Dependency = moveJob.ScheduleParallel(state.Dependency);

            regionEntities.Dispose(state.Dependency);
            regionConfigs.Dispose(state.Dependency);
            activeRegionsLookup.Dispose(state.Dependency);
        }
    }

    /// <summary>
    /// Mueve los agentes siguiendo el flow field de la región activa.
    /// </summary>
    [BurstCompile]
    public partial struct MoveAgentsJob : IJobEntity
    {
        [ReadOnly] public NativeParallelHashMap<int2, Entity> ActiveRegionsLookup;
        [ReadOnly] public BufferLookup<FlowDirectionBuffer> FlowDirectionsLookup;
        public Entity CurrentRouteEntity;
        public float DeltaTime;

        public void Execute(ref LocalTransform transform, in AgentMovementData agent)
        {
            if (agent.RouteId == -1 || CurrentRouteEntity == Entity.Null)
                return;

            int currentRegionId = 0; // TODO: calcular desde posición (grid math en Burst)

            int2 key = new int2(CurrentRouteEntity.Index, currentRegionId);

            if (!ActiveRegionsLookup.TryGetValue(key, out Entity regionContainer))
                return;

            if (!FlowDirectionsLookup.HasBuffer(regionContainer))
                return;

            var directions = FlowDirectionsLookup[regionContainer];

            int localNodeIndex = 0; // TODO: calcular índice del nodo local

            if (localNodeIndex < 0 || localNodeIndex >= directions.Length)
                return;

            float3 targetDirection = directions[localNodeIndex].Value;

            if (math.lengthsq(targetDirection) <= 0.001f)
                return;

            float3 velocity = math.normalize(targetDirection) * agent.Speed;
            transform.Position += velocity * DeltaTime;
        }
    }
}