using DOTSFlowField;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(RouteSystem))]
public partial struct IntegrationFieldSystem : ISystem
{
    private EntityQuery _query;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NavGraphData>();

        _query = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<RegionRouteConfig>()
            .WithAll<IntegrationFieldBuffer>()
            .Build(ref state);
    }

    public void OnUpdate(ref SystemState state)
    {
        var bridge = FlowFieldBridge.Instance;
        if (bridge == null) return;

        var navGraph = SystemAPI.GetSingleton<NavGraphData>();
        int maxFases = 20;

        JobHandle dependencyChain = state.Dependency;

        for (int fase = 0; fase <= maxFases; fase++)
        {
            var integrationJob = new RegionEikonalIntegrationChunkJob
            {
                TargetPhase = fase,
                NavGraph = navGraph,
                ActiveRegionsLookup = bridge.ActiveRegionsLookup,

                AllIntegrationBuffers = state.GetBufferLookup<IntegrationFieldBuffer>(true),
                AllRouteConfigs = state.GetComponentLookup<RegionRouteConfig>(true),

                BufferHandle = state.GetBufferTypeHandle<IntegrationFieldBuffer>(false), // Escritura
                ConfigHandle = state.GetComponentTypeHandle<RegionRouteConfig>(false)   // Escritura directa en chunk
            };

            dependencyChain = integrationJob.ScheduleParallel(_query, dependencyChain);
        }

        // Devolvemos la cadena limpia a Unity. Sin ECB, no hay excepciones de sincronización.
        state.Dependency = dependencyChain;
    }
}

[BurstCompile]
public struct RegionEikonalIntegrationChunkJob : IJobChunk
{
    public int TargetPhase;
    [ReadOnly] public NavGraphData NavGraph;
    [ReadOnly] public NativeParallelHashMap<int2, Entity> ActiveRegionsLookup;

    [NativeDisableContainerSafetyRestriction] public BufferLookup<IntegrationFieldBuffer> AllIntegrationBuffers;
    [NativeDisableContainerSafetyRestriction] public ComponentLookup<RegionRouteConfig> AllRouteConfigs;

    public BufferTypeHandle<IntegrationFieldBuffer> BufferHandle;
    // 💡 Quitamos el [ReadOnly] porque vamos a apagar el flag IsDirty directamente en el chunk
    public ComponentTypeHandle<RegionRouteConfig> ConfigHandle;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        var buffersAccessor = chunk.GetBufferAccessor(ref BufferHandle);
        var configs = chunk.GetNativeArray(ref ConfigHandle); // Array directo del chunk

        UnityEngine.Debug.Log($"holi: {chunk.Count}");

        for (int i = 0; i < chunk.Count; i++)
        {
            var config = configs[i];

            // FILTRO DE RÁFAGA
            if (config.ExecutionPhase != TargetPhase || !config.IsDirty)
                continue;

            var localBuffer = buffersAccessor[i];
            int localRegionId = config.RegionId;
            int routeIdx = config.RouteIndex;
            int regionSize = NavGraph.RegionSizes[localRegionId];

            var queue = new NativeQueue<int>(Allocator.Temp);

            // -----------------------------------------------------------------
            // PASO 1: SEMBRADO INTER-REGIÓN
            // -----------------------------------------------------------------
            for (int localIdx = 0; localIdx < regionSize; localIdx++)
            {
                int globalNode = NavGraph.LocalToGlobalMap[new int2(localIdx, localRegionId)];

                if (localBuffer[localIdx].Value < float.MaxValue)
                {
                    queue.Enqueue(localIdx);
                    continue;
                }

                int2 neighborOffset = NavGraph.NodeNeighborsOffsets[globalNode];
                float bestExternalCost = float.MaxValue;

                for (int n = 0; n < neighborOffset.y; n++)
                {
                    int neighborGlobal = NavGraph.NeighborsBuffer[neighborOffset.x + n];
                    int neighborRegionId = NavGraph.NodeRegionIds[neighborGlobal];

                    if (neighborRegionId != localRegionId)
                    {
                        int2 neighborKey = new int2(routeIdx, neighborRegionId);

                        if (ActiveRegionsLookup.TryGetValue(neighborKey, out Entity neighborEntity))
                        {
                            var neighborConfig = AllRouteConfigs[neighborEntity];

                            if (neighborConfig.ExecutionPhase < config.ExecutionPhase)
                            {
                                var neighborBuffer = AllIntegrationBuffers[neighborEntity];
                                int neighborLocalIdx = NavGraph.GlobalToLocalMap[neighborGlobal];
                                float externalCost = neighborBuffer[neighborLocalIdx].Value;

                                if (externalCost < float.MaxValue)
                                {
                                    float transitionCost = externalCost + (1.0f * NavGraph.NodeCosts[globalNode]);
                                    if (transitionCost < bestExternalCost)
                                    {
                                        bestExternalCost = transitionCost;
                                    }
                                }
                            }
                        }
                    }
                }

                if (bestExternalCost < float.MaxValue)
                {
                    localBuffer[localIdx] = new IntegrationFieldBuffer { Value = bestExternalCost };
                    queue.Enqueue(localIdx);
                }
            }

            // -----------------------------------------------------------------
            // PASO 2: DIJKSTRA LOCAL INTERNO
            // -----------------------------------------------------------------
            while (queue.TryDequeue(out int currentLocalNode))
            {
                int currentGlobalNode = NavGraph.LocalToGlobalMap[new int2(currentLocalNode, localRegionId)];
                float currentCost = localBuffer[currentLocalNode].Value;

                int2 neighborOffset = NavGraph.NodeNeighborsOffsets[currentGlobalNode];

                for (int n = 0; n < neighborOffset.y; n++)
                {
                    int neighborGlobal = NavGraph.NeighborsBuffer[neighborOffset.x + n];

                    if (NavGraph.NodeRegionIds[neighborGlobal] == localRegionId)
                    {
                        if (!NavGraph.IsWalkableFlags[neighborGlobal]) continue;

                        int neighborLocal = NavGraph.GlobalToLocalMap[neighborGlobal];
                        float stepCost = 1.0f * NavGraph.NodeCosts[neighborGlobal];
                        float newCost = currentCost + stepCost;

                        if (newCost < localBuffer[neighborLocal].Value)
                        {
                            localBuffer[neighborLocal] = new IntegrationFieldBuffer { Value = newCost };
                            queue.Enqueue(neighborLocal);
                        }
                    }
                }
            }

            queue.Dispose();

            // 💡 MODIFICACIÓN DIRECTA EN EL CHUNK: Rápido, seguro y sin ECB
            config.IsDirty = false;
            configs[i] = config;
        }
    }
}