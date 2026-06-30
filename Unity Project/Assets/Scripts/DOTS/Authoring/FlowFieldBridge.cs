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

    private void OnDestroy()
    {
        if (_isMapInitialized && GlobalPortalDistances.IsCreated)
        {
            GlobalPortalDistances.Dispose();
        }
    }
}