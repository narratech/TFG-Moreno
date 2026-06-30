using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;
using DOTSFlowField;

public class FlowFieldTestController : MonoBehaviour
{
    [Header("Configuración de Prueba")]
    public Transform targetTransform;
    public Grid2DProvider grid2DProvider;

    private EntityManager _entityManager;
    private EntityQuery _agentQuery;

    /// <summary>
    /// Inicializa el puente con el grafo clásico y prepara la consulta de agentes.
    /// </summary>
    void Start()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        if (grid2DProvider != null && grid2DProvider.Graph != null)
        {
            FlowFieldBridge.Instance.Init(grid2DProvider.Graph);
        }
        else
        {
            Debug.LogError("[Test] Falta el Grid2DProvider o el Graph no está inicializado.");
        }

        _agentQuery = _entityManager.CreateEntityQuery(typeof(AgentMovementData));
    }

    /// <summary>
    /// Solicita una nueva ruta y la asigna a todos los agentes activos.
    /// </summary>
    void Update()
    {
        if (!InputManager.Instance.IsSelecting)
            return;

        if (targetTransform == null)
        {
            Debug.LogWarning("[Test] Por favor, asigna un Target Transform en el inspector.");
            return;
        }

        var navGraph = FlowFieldBridge.Instance.gridNavGraph;
        if (navGraph == null)
            return;

        Debug.Log("<color=cyan><b>[Test] ¡Petición de ruta enviada a DOTS!</b></color>");

        int targetNodeGlobal = navGraph.GetClosestNode(targetTransform.position);

        Entity routeEntity = FlowFieldBridge.Instance.RequestDOTSRoute(
            navGraph,
            targetNodeGlobal,
            new List<float3>());

        using var agentEntities = _agentQuery.ToEntityArray(Allocator.Temp);

        if (agentEntities.Length == 0)
        {
            Debug.LogWarning("[Test] No se han encontrado entidades Agentes en el mundo de DOTS.");
            return;
        }

        int activeRouteId = routeEntity.Index;

        for (int i = 0; i < agentEntities.Length; i++)
        {
            var agentData = _entityManager.GetComponentData<AgentMovementData>(agentEntities[i]);
            agentData.RouteId = activeRouteId;
            _entityManager.SetComponentData(agentEntities[i], agentData);
        }

        Debug.Log($"<color=green><b>[Test] Ruta activada (ID: {activeRouteId}). Sincronizados {agentEntities.Length} agentes con éxito.</b></color>");
    }
}