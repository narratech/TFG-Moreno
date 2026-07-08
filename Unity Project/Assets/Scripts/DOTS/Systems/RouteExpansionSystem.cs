using DOTSFlowField;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Actualiza la ventana de regiones de cada ruta e inicializa los datos
/// necesarios para el cálculo del integration field.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(RouteSystem))]
public partial class RouteExpansionSystem : SystemBase
{
    protected override void OnCreate() => RequireForUpdate<NavGraphData>();

    protected override void OnUpdate()
    {
        var bridge = FlowFieldBridge.Instance;

        if (bridge == null ||
            !bridge.ActiveRegionsLookup.IsCreated ||
            !bridge.GlobalPortalDistances.IsCreated)
            return;

        var navGraph = SystemAPI.GetSingleton<NavGraphData>();
        var lookup = bridge.ActiveRegionsLookup;
        int windowLevels = bridge.NumRegionLevelsWindow;

        // -----------------------------------------------------------------
        // FASE 1: Obtener las regiones actuales de los agentes.
        // -----------------------------------------------------------------

        var routeInitialRegions = new NativeParallelMultiHashMap<int, int>(256, Allocator.Temp);

        foreach (var (transform, agent) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<AgentComponent>>())
        {
            int nodeGlobal = bridge.gridNavGraph.GetClosestNode(transform.ValueRO.Position);

            if (nodeGlobal >= 0 &&
                nodeGlobal < navGraph.NodeCount &&
                agent.ValueRO.RouteId >= 0)
            {
                routeInitialRegions.Add(agent.ValueRO.RouteId, navGraph.NodeRegionIds[nodeGlobal]);
            }
        }

        // -----------------------------------------------------------------
        // FASE 2: Recalcular la ventana de regiones para cada ruta.
        // -----------------------------------------------------------------

        foreach (var (route, routeEntity) in SystemAPI.Query<RefRW<RouteComponent>>().WithEntityAccess())
        {
            if (!route.ValueRO.IsDirty)
                continue;

            int targetRegion = navGraph.NodeRegionIds[route.ValueRO.TargetNodeGlobal];

            HashSet<int> insideRegions = new HashSet<int>();
            HashSet<int> frontierRegions = new HashSet<int>();

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
                var nextFrontier = new HashSet<int>();

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

                        // --- LÓGICA SELECT EXIT PORTAL (DOTS) ---
                        // Leemos la distancia al destino de nuestro portal actual
                        float currentPortalDist = float.MaxValue;
                        bridge.GlobalPortalDistances.TryGetValue(portalNode, out currentPortalDist);

                        UnityEngine.Debug.Log($"[RouteExpansionSystem] Route {route.ValueRO.RouteIndex} - Portal {portalNode} - CurrentPortalDist: {currentPortalDist}");

                        bool isExitPortal = false;

                        // Si el portal no tiene coste registrado, es que no llega al destino o es inválido. 
                        // Si estamos en la región vecina al target, podría no tener un "siguiente portal", por lo que se asume de salida (como tu if == -1)
                        if (currentPortalDist == float.MaxValue)
                        {
                            isExitPortal = true;
                        }
                        else
                        {
                            // Buscamos si alguno de sus portales vecinos nos acerca más al destino (GetNextPortal)
                            int2 neighborOffset = navGraph.NodeNeighborsOffsets[portalNode];
                            for (int n = 0; n < neighborOffset.y; n++)
                            {
                                int neighborGlobal = navGraph.NeighborsBuffer[neighborOffset.x + n];

                                // Comprobamos si este nodo vecino también es un portal y si está registrado en las distancias globales
                                if (bridge.GlobalPortalDistances.TryGetValue(neighborGlobal, out float neighborPortalDist))
                                {
                                    // Si la distancia del vecino es MENOR que la nuestra, significa que este portal 
                                    // "mira hacia adelante" en la ruta óptima. ¡Es un portal de salida!
                                    if (neighborPortalDist < currentPortalDist)
                                    {
                                        isExitPortal = true;
                                        break; // Con que un vecino nos acerque, este portal ya se valida como salida
                                    }
                                }
                            }
                        }

                        // Si determinamos que NO es un portal de salida (nos lleva hacia atrás o lejos del flujo), lo ignoramos por completo
                        if (!isExitPortal) continue;

                        // --- EXPANDIR SOLO A TRAVÉS DEL PORTAL DE SALIDA ---
                        int2 nodeNeighbors = navGraph.NodeNeighborsOffsets[portalNode];
                        for (int n = 0; n < nodeNeighbors.y; n++)
                        {
                            int neighborGlobal = navGraph.NeighborsBuffer[nodeNeighbors.x + n];
                            int neighborRegion = navGraph.NodeRegionIds[neighborGlobal];

                            if (neighborRegion != rid)
                            {
                                nextFrontier.Add(neighborRegion);
                            }
                        }
                    }

                    insideRegions.Add(rid);
                }

                frontierRegions = nextFrontier;
                //nextFrontier.Dispose();
            }

            UnityEngine.Debug.Log($"[RouteExpansionSystem] Route {route.ValueRO.RouteIndex} - InsideRegions: {string.Join(",", insideRegions)} - FrontierRegions: {string.Join(",", frontierRegions)}");
            UnityEngine.Debug.Log($"[RouteExpansionSystem] Route {route.ValueRO.RouteIndex} - TargetRegion: {targetRegion}");

            // -----------------------------------------------------------------
            // FASE 3A: Inicializar las regiones interiores.
            // -----------------------------------------------------------------

            foreach (int rid in insideRegions)
            {
                int2 key = new(route.ValueRO.RouteIndex, rid);

                if (!lookup.TryGetValue(key, out Entity regEntity))
                    continue;

                var config = EntityManager.GetComponentData<RegionRouteConfig>(regEntity);
                config.IsInsideWindow = true;
                EntityManager.SetComponentData(regEntity, config);

                var buffer = EntityManager.GetBuffer<IntegrationFieldBuffer>(regEntity);

                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] = float.MaxValue;
            }

            //insideRegions.Dispose();

            // -----------------------------------------------------------------
            // FASE 3B: Inicializar las regiones frontera.
            // -----------------------------------------------------------------

            foreach (int rid in frontierRegions)
            {
                int2 key = new(route.ValueRO.RouteIndex, rid);

                if (!lookup.TryGetValue(key, out Entity regEntity))
                    continue;

                var config = EntityManager.GetComponentData<RegionRouteConfig>(regEntity);
                config.IsInsideWindow = false;
                EntityManager.SetComponentData(regEntity, config);

                var buffer = EntityManager.GetBuffer<IntegrationFieldBuffer>(regEntity);

                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] = float.MaxValue;

                int2 portalOffset = navGraph.RegionPortalsOffsets[rid];

                for (int p = 0; p < portalOffset.y; p++)
                {
                    int portalNode = navGraph.RegionPortalsBuffer[portalOffset.x + p];

                    if (bridge.GlobalPortalDistances.TryGetValue(portalNode, out float cost))
                    {
                        int localIdx = navGraph.GlobalToLocalMap[portalNode];
                        buffer[localIdx] = cost;
                    }
                }
            }

            //frontierRegions.Dispose();

            // -----------------------------------------------------------------
            // FASE 3C: Fijar el nodo destino con coste cero.
            // -----------------------------------------------------------------

            int2 targetKey = new(route.ValueRO.RouteIndex, targetRegion);

            //UnityEngine.Debug.Log($"LOOKUP: {lookup.Count()}");
            if (lookup.TryGetValue(targetKey, out Entity targetRegEntity))
            {
                var buffer = EntityManager.GetBuffer<IntegrationFieldBuffer>(targetRegEntity);
                int localIdx = navGraph.GlobalToLocalMap[route.ValueRO.TargetNodeGlobal];
                buffer[localIdx] = 0f;

                //UnityEngine.Debug.Log($"------> {buffer.ToString()}");
            }
        }

        routeInitialRegions.Dispose();
    }
}