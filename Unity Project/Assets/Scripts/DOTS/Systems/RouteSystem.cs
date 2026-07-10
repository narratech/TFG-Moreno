using DOTSFlowField;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Sistema unificado que gestiona las rutas activas, calcula la ventana de expansión 
/// jerárquica por portales una sola vez y crea/gestiona estructuralmente las regiones.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class RouteSystem : SystemBase
{
    protected override void OnCreate() => RequireForUpdate<NavGraphData>();

    protected override void OnUpdate()
    {
        var navGraph = SystemAPI.GetSingleton<NavGraphData>();
        var bridge = FlowFieldBridge.Instance;

        if (bridge == null || !bridge.ActiveRegionsLookup.IsCreated)
            return;

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var lookup = bridge.ActiveRegionsLookup;

        // FASE 1: Registrar rutas activas basándose en la posición de los agentes
        var existingRoutes = GatherExistingRoutes();
        var routeInitialRegions = ProcessAgentsAndCreateRoutes(bridge, navGraph, existingRoutes, ecb);

        // Capturamos el Lookup de configuraciones para actualizar los flags de ventana velozmente
        var routeConfigLookup = GetComponentLookup<RegionRouteConfig>(false);

        // FASE 2 y 3: Expandir la ventana por portales y asegurar la existencia de las entidades
        foreach (var (route, routeEntity) in SystemAPI.Query<RefRW<RouteComponent>>().WithEntityAccess())
        {
            if (!route.ValueRO.IsDirty)
                continue;

            ProcessRouteWindowAndAllocation(
                route.ValueRO.RouteIndex,
                navGraph,
                bridge,
                routeInitialRegions,
                lookup,
                ecb,
                routeConfigLookup
            );
        }

        routeInitialRegions.Dispose();
        existingRoutes.Dispose();

        // Aplicamos los cambios estructurales en los chunks de memoria de ECS
        ecb.Playback(EntityManager);
        ecb.Dispose();

        // FASE 4: Reconstruir lookup final con todas las entidades estables del mundo
        RebuildActiveRegionsLookup(lookup);
    }

    /// <summary>
    /// Registra en un set temporal las rutas que ya existen actualmente en el mundo.
    /// </summary>
    private NativeHashSet<int> GatherExistingRoutes()
    {
        var existingRoutes = new NativeHashSet<int>(32, Allocator.Temp);
        foreach (var rComp in SystemAPI.Query<RefRO<RouteComponent>>())
        {
            existingRoutes.Add(rComp.ValueRO.RouteIndex);
        }
        return existingRoutes;
    }

    /// <summary>
    /// Analiza los agentes, mapea sus regiones de origen y crea la entidad Ruta si es nueva.
    /// </summary>
    private NativeParallelMultiHashMap<int, int> ProcessAgentsAndCreateRoutes(
        FlowFieldBridge bridge,
        NavGraphData navGraph,
        NativeHashSet<int> existingRoutes,
        EntityCommandBuffer ecb)
    {
        var routeInitialRegions = new NativeParallelMultiHashMap<int, int>(256, Allocator.Temp);

        foreach (var (transform, agent) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<AgentComponent>>())
        {
            int routeId = agent.ValueRO.RouteId;
            if (routeId < 0) continue;

            int nodeGlobal = bridge.gridNavGraph.GetClosestNode(transform.ValueRO.Position);

            if (nodeGlobal >= 0 && nodeGlobal < navGraph.NodeCount)
            {
                routeInitialRegions.Add(routeId, navGraph.NodeRegionIds[nodeGlobal]);

                if (!existingRoutes.Contains(routeId))
                {
                    Entity newRoute = ecb.CreateEntity();
                    ecb.AddComponent(newRoute, new RouteComponent
                    {
                        RouteIndex = routeId,
                        IsDirty = true
                    });
                    existingRoutes.Add(routeId);
                }
            }
        }
        return routeInitialRegions;
    }

    /// <summary>
    /// Calcula la ventana geométrica por portales para una ruta y asegura que existan sus entidades/buffers.
    /// </summary>
    private void ProcessRouteWindowAndAllocation(
        int routeIndex,
        NavGraphData navGraph,
        FlowFieldBridge bridge,
        NativeParallelMultiHashMap<int, int> routeInitialRegions,
        NativeParallelHashMap<int2, Entity> lookup,
        EntityCommandBuffer ecb,
        ComponentLookup<RegionRouteConfig> routeConfigLookup)
    {
        int targetRegion = navGraph.NodeRegionIds[routeIndex];

        UnityEngine.Debug.Log($"[RouteExpansionSystem] Route {routeIndex} - Recalculating window regions.");
        UnityEngine.Debug.Log($"[RouteExpansionSystem] Route {routeIndex} - TargetRegion: {targetRegion} - TargetNode: {routeIndex}");

        var insideRegions = new HashSet<int>();
        var frontierRegions = new HashSet<int>();

        if (routeInitialRegions.TryGetFirstValue(routeIndex, out int firstRegion, out var iterator))
        {
            do { frontierRegions.Add(firstRegion); }
            while (routeInitialRegions.TryGetNextValue(out firstRegion, ref iterator));
        }

        // FASE 2: Expansión de la ventana por niveles mediante flujo de portales válidos
        for (int i = 0; i < bridge.NumRegionLevelsWindow; i++)
        {
            var nextFrontier = new HashSet<int>();

            foreach (int rid in frontierRegions)
            {
                if (insideRegions.Contains(rid)) continue;
                if (rid == targetRegion) { insideRegions.Add(rid); continue; }

                int2 portalOffset = navGraph.RegionPortalsOffsets[rid];

                for (int p = 0; p < portalOffset.y; p++)
                {
                    int portalId = navGraph.RegionPortalsBuffer[portalOffset.x + p];
                    int2 portalNodes = navGraph.PortalNodes[portalId];

                    int portalNode = navGraph.NodeRegionIds[portalNodes.x] == rid ? portalNodes.x : portalNodes.y;
                    int neighborPortalNode = navGraph.NodeRegionIds[portalNodes.x] == rid ? portalNodes.y : portalNodes.x;
                    int neighborRegion = navGraph.NodeRegionIds[neighborPortalNode];

                    bridge.GlobalPortalDistances.TryGetValue(portalId, out float currentPortalDist);

                    if (!IsExitPortal(portalId, currentPortalDist, neighborRegion, navGraph, bridge))
                        continue;

                    int2 nodeNeighbors = navGraph.NodeNeighborsOffsets[portalNode];
                    for (int n = 0; n < nodeNeighbors.y; n++)
                    {
                        int neighborGlobal = navGraph.NeighborsBuffer[nodeNeighbors.x + n];
                        int neiRegion = navGraph.NodeRegionIds[neighborGlobal];

                        if (neiRegion != rid && !insideRegions.Contains(neiRegion))
                        {
                            nextFrontier.Add(neiRegion);
                        }
                    }
                }
                insideRegions.Add(rid);
            }
            frontierRegions = nextFrontier;
        }

        UnityEngine.Debug.Log($"[RouteExpansionSystem] Route {routeIndex} - InsideRegions: {string.Join(",", insideRegions)} - FrontierRegions: {string.Join(",", frontierRegions)}");
        UnityEngine.Debug.Log($"[RouteExpansionSystem] Route {routeIndex} - TargetRegion: {targetRegion}");

        // FASE 3: Instanciación estructural de las regiones requeridas
        var allRequiredRegions = new HashSet<int>(insideRegions);
        allRequiredRegions.UnionWith(frontierRegions);

        foreach (int rid in allRequiredRegions)
        {
            int2 key = new(routeIndex, rid);
            bool isInside = insideRegions.Contains(rid);

            if (!lookup.TryGetValue(key, out Entity regEntity))
            {
                // Si la región NO existe, se crea de cero y se dimensiona su buffer (vacio/uninitialized)
                regEntity = ecb.CreateEntity();
                ecb.AddComponent(regEntity, new RegionRouteConfig
                {
                    RegionId = rid,
                    RouteIndex = routeIndex,
                    IsInsideWindow = isInside
                });

                var buffer = ecb.AddBuffer<IntegrationFieldBuffer>(regEntity);
                int regionSize = navGraph.RegionSizes[rid];
                buffer.ResizeUninitialized(regionSize);

#if UNITY_EDITOR
                ecb.SetName(regEntity, $"RegionRouteBuffer_R{routeIndex}_Reg{rid}");
#endif
                // Se añade preventivamente al lookup local del frame
                lookup.TryAdd(key, regEntity);
            }
            else
            {
                // Si ya existe de frames anteriores, actualizamos de forma segura su flag dinámico mediante Lookup directo
                if (routeConfigLookup.HasComponent(regEntity))
                {
                    var config = routeConfigLookup[regEntity];
                    config.IsInsideWindow = isInside;
                    routeConfigLookup[regEntity] = config;
                }
            }
        }
    }

    /// <summary>
    /// Comprueba si un portal es de salida comparando sus costes con la interconexión de la región vecina.
    /// </summary>
    private bool IsExitPortal(int portalId, float currentPortalDist, int neighborRegion, NavGraphData navGraph, FlowFieldBridge bridge)
    {
        if (currentPortalDist == float.MaxValue) return true;

        int2 portalOffsetN = navGraph.RegionPortalsOffsets[neighborRegion];

        for (int pn = 0; pn < portalOffsetN.y; pn++)
        {
            int neighborPortalId = navGraph.RegionPortalsBuffer[portalOffsetN.x + pn];
            if (neighborPortalId == portalId) continue;

            if (navGraph.PortalDistances.TryGetValue(new int2(portalId, neighborPortalId), out float distBetweenPortals))
            {
                if (bridge.GlobalPortalDistances.TryGetValue(neighborPortalId, out float neighborPortalDist))
                {
                    if (neighborPortalDist + distBetweenPortals <= currentPortalDist)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Reconstruye completamente el ActiveRegionsLookup basándose en las entidades reales y consolidadas de los chunks.
    /// </summary>
    private void RebuildActiveRegionsLookup(NativeParallelHashMap<int2, Entity> lookup)
    {
        lookup.Clear();
        foreach (var (config, entity) in SystemAPI.Query<RefRO<RegionRouteConfig>>().WithEntityAccess())
        {
            lookup.TryAdd(new int2(config.ValueRO.RouteIndex, config.ValueRO.RegionId), entity);
        }
    }
}