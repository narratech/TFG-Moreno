using DOTSFlowField;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(RouteSystem))]
public partial struct IntegrationFieldSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NavGraphData>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var bridge = FlowFieldBridge.Instance;
        if (bridge == null) return;

        var navGraph = SystemAPI.GetSingleton<NavGraphData>();
        int maxFases = bridge.NumRegionLevelsWindow;

        // Conseguimos los Lookups necesarios para resolver las fronteras inter-región
        var integrationBufferLookup = state.GetBufferLookup<IntegrationFieldBuffer>(true); // ReadOnly
        var routeConfigLookup = state.GetComponentLookup<RegionRouteConfig>(true); // ReadOnly

        JobHandle dependencyChain = state.Dependency;

        // Ejecución en ráfaga por fases
        for (int fase = 0; fase <= maxFases; fase++)
        {
            var eikonalJob = new RegionEikonalIntegrationJob
            {
                TargetPhase = fase,
                NavGraph = navGraph,
                ActiveRegionsLookup = bridge.ActiveRegionsLookup,
                AllIntegrationBuffers = integrationBufferLookup,
                AllRouteConfigs = routeConfigLookup
            };

            dependencyChain = eikonalJob.ScheduleParallel(dependencyChain);
        }

        state.Dependency = dependencyChain;
    }
}

[BurstCompile]
public partial struct RegionEikonalIntegrationJob : IJobEntity
{
    public int TargetPhase;
    [ReadOnly] public NavGraphData NavGraph;

    // Mapeo global de (RouteIndex, RegionId) -> Entity para buscar vecinos
    [ReadOnly] public NativeParallelHashMap<int2, Entity> ActiveRegionsLookup;

    // Lookups para inspeccionar el mundo exterior de forma segura
    [ReadOnly] public BufferLookup<IntegrationFieldBuffer> AllIntegrationBuffers;
    [ReadOnly] public ComponentLookup<RegionRouteConfig> AllRouteConfigs;

    void Execute(ref DynamicBuffer<IntegrationFieldBuffer> localBuffer, ref RegionRouteConfig config)
    {
        // Solo trabajamos si toca esta fase y está marcado como sucio
        if (config.ExecutionPhase != TargetPhase || !config.IsDirty)
            return;

        int localRegionId = config.RegionId;
        int routeIdx = config.RouteIndex;
        int regionSize = NavGraph.RegionSizes[localRegionId];

        var queue = new NativeQueue<int>(Allocator.Temp);

        // -----------------------------------------------------------------
        // FASE 1: Sembrado Eikonal e Inter-Región
        // -----------------------------------------------------------------
        for (int localIdx = 0; localIdx < regionSize; localIdx++)
        {
            int globalNode = NavGraph.LocalToGlobalMap[new int2(localIdx, localRegionId)];

            // Si ya tiene coste inicial inyectado de antemano (Target o Portales de entrada globales)
            if (localBuffer[localIdx].Value < float.MaxValue)
            {
                queue.Enqueue(localIdx);
                continue;
            }

            // Lógica Eikonal: Revisamos si este nodo tiene vecinos físicos en OTRAS regiones
            int2 neighborOffset = NavGraph.NodeNeighborsOffsets[globalNode];
            float bestExternalCost = float.MaxValue;

            for (int n = 0; n < neighborOffset.y; n++)
            {
                int neighborGlobal = NavGraph.NeighborsBuffer[neighborOffset.x + n];
                int neighborRegionId = NavGraph.NodeRegionIds[neighborGlobal];

                // Si el vecino pertenece a otra región...
                if (neighborRegionId != localRegionId)
                {
                    int2 neighborKey = new int2(routeIdx, neighborRegionId);

                    // Intentamos obtener la entidad de esa región vecina
                    if (ActiveRegionsLookup.TryGetValue(neighborKey, out Entity neighborEntity))
                    {
                        var neighborConfig = AllRouteConfigs[neighborEntity];

                        // Eikonal condicional: Solo leemos si el vecino es de una fase INFERIOR o IGUAL (datos estables)
                        if (neighborConfig.ExecutionPhase <= config.ExecutionPhase)
                        {
                            var neighborBuffer = AllIntegrationBuffers[neighborEntity];
                            int neighborLocalIdx = NavGraph.GlobalToLocalMap[neighborGlobal];
                            float externalCost = neighborBuffer[neighborLocalIdx].Value;

                            if (externalCost < float.MaxValue)
                            {
                                // Coste de transición eikonal entre regiones
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

            // Si encontramos una región vecina que ya tiene datos para este nodo frontera, nos acoplamos
            if (bestExternalCost < float.MaxValue)
            {
                localBuffer[localIdx] = new IntegrationFieldBuffer { Value = bestExternalCost };
                queue.Enqueue(localIdx);
            }
        }

        // -----------------------------------------------------------------
        // FASE 2: Inundación Dijkstra / Eikonal Estándar Interna
        // -----------------------------------------------------------------
        while (queue.TryDequeue(out int currentLocalNode))
        {
            int currentGlobalNode = NavGraph.LocalToGlobalMap[new int2(currentLocalNode, localRegionId)];
            float currentCost = localBuffer[currentLocalNode].Value;

            int2 neighborOffset = NavGraph.NodeNeighborsOffsets[currentGlobalNode];

            for (int n = 0; n < neighborOffset.y; n++)
            {
                int neighborGlobal = NavGraph.NeighborsBuffer[neighborOffset.x + n];

                // Nos limitamos estrictamente al cálculo dentro de los límites de nuestra propia región
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

        // Al terminar el cálculo quitamos el Dirty para evitar trabajo en el siguiente frame
        config.IsDirty = false;
    }
}