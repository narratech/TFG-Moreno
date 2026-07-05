using DOTSFlowField;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Gestiona las rutas activas y crea las regiones necesarias para cada una.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class RouteSystem : SystemBase
{
    protected override void OnCreate() => RequireForUpdate<NavGraphData>();

    protected override void OnUpdate()
    {
        var navGraph = SystemAPI.GetSingleton<NavGraphData>();
        var bridge = FlowFieldBridge.Instance;
        if (bridge == null) return;

        if (!bridge.ActiveRegionsLookup.IsCreated)
            bridge.ActiveRegionsLookup = new NativeParallelHashMap<int2, Entity>(512, Allocator.Persistent);

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var lookup = bridge.ActiveRegionsLookup;
        int windowLevels = bridge.NumRegionLevelsWindow;

        // -----------------------------------------------------------------
        // FASE 1: Registrar las rutas activas y las regiones iniciales.
        // -----------------------------------------------------------------

        var existingRoutes = new NativeHashSet<int>(32, Allocator.Temp);

        foreach (var rComp in SystemAPI.Query<RefRO<RouteComponent>>())
        {
            existingRoutes.Add(rComp.ValueRO.RouteIndex);
        }

        var routeInitialRegions = new NativeParallelMultiHashMap<int, int>(256, Allocator.Temp);

        foreach (var (transform, agent) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<AgentComponent>>())
        {
            int routeId = agent.ValueRO.RouteId;
            if (routeId < 0)
                continue;

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
                        InitialNodeGlobal = nodeGlobal,
                        TargetNodeGlobal = nodeGlobal,
                        IsDirty = true
                    });

                    existingRoutes.Add(routeId);
                }
            }
        }

        // -----------------------------------------------------------------
        // FASE 2: Calcular la ventana de regiones de cada ruta.
        // -----------------------------------------------------------------

        foreach (var (route, routeEntity) in SystemAPI.Query<RefRW<RouteComponent>>().WithEntityAccess())
        {
            if (!route.ValueRO.IsDirty)
                continue;

            int targetRegion = navGraph.NodeRegionIds[route.ValueRO.TargetNodeGlobal];

            var insideRegions = new NativeHashSet<int>(64, Allocator.Temp);
            var frontierRegions = new NativeHashSet<int>(64, Allocator.Temp);

            if (routeInitialRegions.TryGetFirstValue(route.ValueRO.RouteIndex, out int firstRegion, out var iterator))
            {
                do
                {
                    frontierRegions.Add(firstRegion);
                }
                while (routeInitialRegions.TryGetNextValue(out firstRegion, ref iterator));
            }

            if (frontierRegions.Count == 0)
                frontierRegions.Add(navGraph.NodeRegionIds[route.ValueRO.InitialNodeGlobal]);

            for (int i = 0; i < windowLevels; i++)
            {
                var nextFrontier = new NativeHashSet<int>(64, Allocator.Temp);

                foreach (int rid in frontierRegions)
                {
                    if (insideRegions.Contains(rid))
                        continue;

                    if (rid == targetRegion)
                    {
                        insideRegions.Add(rid);
                        continue;
                    }

                    int2 portalOffset = navGraph.RegionPortalsOffsets[rid];

                    for (int p = 0; p < portalOffset.y; p++)
                    {
                        int portalNode = navGraph.RegionPortalsBuffer[portalOffset.x + p];
                        int2 neighborOffset = navGraph.NodeNeighborsOffsets[portalNode];

                        for (int n = 0; n < neighborOffset.y; n++)
                        {
                            int neighbor = navGraph.NeighborsBuffer[neighborOffset.x + n];
                            int neighborRegion = navGraph.NodeRegionIds[neighbor];

                            if (neighborRegion != rid)
                                nextFrontier.Add(neighborRegion);
                        }
                    }

                    insideRegions.Add(rid);
                }

                frontierRegions.Dispose();
                frontierRegions = nextFrontier;
            }

            NativeHashSet<int> allRequiredRegions = new(insideRegions.Count, Allocator.Temp);
            foreach (int rid in insideRegions)
                allRequiredRegions.Add(rid);

            foreach (int rid in frontierRegions)
                allRequiredRegions.Add(rid);

            // -------------------------------------------------------------
            // FASE 3: Crear las regiones necesarias para la ruta.
            // -------------------------------------------------------------

            foreach (int rid in allRequiredRegions)
            {
                int2 key = new(route.ValueRO.RouteIndex, rid);

                Entity newRegionRoute = ecb.CreateEntity();

                ecb.AddComponent(newRegionRoute, new RegionRouteConfig
                {
                    RegionId = rid,
                    RouteIndex = route.ValueRO.RouteIndex
                });

                var buffer = ecb.AddBuffer<IntegrationFieldBuffer>(newRegionRoute);

                int regionSize = navGraph.RegionSizes[rid];
                buffer.ResizeUninitialized(regionSize);

                for (int i = 0; i < regionSize; i++)
                {
                    buffer.Add(new IntegrationFieldBuffer
                    {
                        Value = float.MaxValue
                    });
                }

#if UNITY_EDITOR
                ecb.SetName(newRegionRoute, $"RegionRouteBuffer_R{route.ValueRO.RouteIndex}_Reg{rid}");
#endif
            }

            insideRegions.Dispose();
            frontierRegions.Dispose();
            allRequiredRegions.Dispose();
        }

        routeInitialRegions.Dispose();
        existingRoutes.Dispose();

        ecb.Playback(EntityManager);
        ecb.Dispose();

        // Reconstruimos el lookup con las entidades reales
        lookup.Clear();

        foreach (var (config, entity) in SystemAPI.Query<RefRO<RegionRouteConfig>>().WithEntityAccess())
        {
            lookup.TryAdd(
                new int2(config.ValueRO.RouteIndex, config.ValueRO.RegionId),
                entity);
        }
    }
}