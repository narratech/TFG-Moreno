using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using DOTSFlowField;

public class FlowFieldBridge : MonoBehaviour
{
    private static FlowFieldBridge _instance;
    public static FlowFieldBridge Instance =>
        _instance ??= FindFirstObjectByType<FlowFieldBridge>();

    private EntityManager _entityManager;

    public INavGraph gridNavGraph { get; private set; }
    public Entity CurrentActiveRouteEntity { get; private set; }
    public int NumRegionLevelsWindow { get; set; } = 3;

    public NativeParallelHashMap<int, float> GlobalPortalDistances;
    private bool _isMapInitialized;

    /// <summary>
    /// Inicializa el bridge con el grafo de navegación clásico.
    /// </summary>
    public void Init(INavGraph navGraph)
    {
        gridNavGraph = navGraph;
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        NavGraphToDOTS(navGraph);
    }

    /// <summary>
    /// Crea una ruta DOTS y sincroniza el mapa de distancias de portales para Burst.
    /// </summary>
    public Entity RequestDOTSRoute(INavGraph navGraph, int targetNode, List<float3> agentPositions)
    {
        if (gridNavGraph == null)
            Init(navGraph);

        if (!FlowFieldManager.Instance.TryGetRoute(navGraph, targetNode))
        {
            FlowFieldManager.Instance.RegisterRoute(navGraph, targetNode);
        }

        var routeData = FlowFieldManager.Instance.GetRoute(navGraph, targetNode);

        var routeEntity = _entityManager.CreateEntity();

#if UNITY_EDITOR
        _entityManager.SetName(routeEntity, $"Route_Target_{targetNode}");
#endif

        CurrentActiveRouteEntity = routeEntity;

        if (_isMapInitialized && GlobalPortalDistances.IsCreated)
        {
            GlobalPortalDistances.Dispose();
        }

        GlobalPortalDistances = new NativeParallelHashMap<int, float>(
            routeData.DistanceMaps.Count,
            Allocator.Persistent);

        foreach (var kvp in routeData.DistanceMaps)
        {
            GlobalPortalDistances.Add(kvp.Key, kvp.Value);
        }

        _isMapInitialized = true;

        return routeEntity;
    }

    public void NavGraphToDOTS(INavGraph graph)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        // 1. Si ya existía un grafo viejo en DOTS, lo buscamos y liberamos su memoria persistente
        var query = em.CreateEntityQuery(typeof(NavGraphData));
        if (!query.IsEmpty)
        {
            var oldData = query.GetSingleton<NavGraphData>();
            oldData.Dispose(); // Evitamos Memory Leaks
            em.DestroyEntity(query);
        }

        // 2. Reservamos los arrays nativos con el tamaño del grafo de C# clásico
        int nodeCount = graph.NodeCount;
        int regionCount = graph.RegionCount;

        var positions = new NativeArray<float3>(nodeCount, Allocator.Persistent);
        var costs = new NativeArray<float>(nodeCount, Allocator.Persistent);
        var walkables = new NativeArray<bool>(nodeCount, Allocator.Persistent);
        var regionIds = new NativeArray<int>(nodeCount, Allocator.Persistent);
        var globalToLocal = new NativeArray<int>(nodeCount, Allocator.Persistent);
        var localToGlobal = new NativeParallelHashMap<int2, int>(nodeCount, Allocator.Persistent);
        var regionSizes = new NativeArray<int>(regionCount, Allocator.Persistent);

        // Preparación para aplanar los vecinos
        var tempNeighborsList = new List<int>();
        var offsets = new NativeArray<int2>(nodeCount, Allocator.Persistent);

        // 3. Volcado de datos nodo a nodo
        for (int i = 0; i < nodeCount; i++)
        {
            positions[i] = graph.GetNodePosition(i);
            costs[i] = graph.GetNodeCost(i);
            walkables[i] = graph.IsWalkable(i);

            int rId = graph.GetRegionId(i);
            regionIds[i] = rId;

            int localId = graph.GetLocalNode(i);
            globalToLocal[i] = localId;
            localToGlobal.TryAdd(new int2(localId, rId), i);

            // Aplanar vecinos
            int neighborStart = tempNeighborsList.Count;
            var neighbors = graph.GetNeighbors(i);
            int neighborCount = 0;
            foreach (var neighbor in neighbors)
            {
                tempNeighborsList.Add(neighbor);
                neighborCount++;
            }
            offsets[i] = new int2(neighborStart, neighborCount);
        }

        // Volcar lista de vecinos acumulada a un array nativo final
        var neighborsBuffer = new NativeArray<int>(tempNeighborsList.Count, Allocator.Persistent);
        for (int n = 0; n < tempNeighborsList.Count; n++) neighborsBuffer[n] = tempNeighborsList[n];

        // Volcar tamaños de regiones
        for (int r = 0; r < regionCount; r++)
        {
            regionSizes[r] = graph.GetRegionSize(r);
        }

        // 4. Guardamos todo el contenedor en una entidad Singleton de DOTS
        Entity graphEntity = em.CreateEntity();
        em.AddComponentData(graphEntity, new NavGraphData
        {
            NodeCount = nodeCount,
            RegionCount = regionCount,
            NodePositions = positions,
            NodeCosts = costs,
            IsWalkableFlags = walkables,
            NodeRegionIds = regionIds,
            GlobalToLocalMap = globalToLocal,
            LocalToGlobalMap = localToGlobal,
            NeighborsBuffer = neighborsBuffer,
            NodeNeighborsOffsets = offsets,
            RegionSizes = regionSizes
        });

#if UNITY_EDITOR
        em.SetName(graphEntity, "NavGraphData_Singleton");
#endif
    }

    private void OnDestroy()
    {
        if (_isMapInitialized && GlobalPortalDistances.IsCreated)
        {
            GlobalPortalDistances.Dispose();
        }
    }
}