using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.AI;

public class TestManager : MonoBehaviour
{
    [Header("Target Setup")]
    [SerializeField] private Transform targetTransform;
    [SerializeField] private NavGraphProvider graphProvider;

    [Header("Formation Settings")]
    [SerializeField] private FormationType formationType;
    [SerializeField] private float formationSpacing = 10f;
    [SerializeField] private Texture2D shapeTexture;

    [Header("Automation & Recorder Settings")]
    [SerializeField] private float timerSeconds = 10f;
    [SerializeField] private FormationRecorder formationRecorder;

    private void Start()
    {
        if (!ValidateReferences()) return;

        SendAllAgentsToTarget();
        StartCoroutine(WaitAndQuitRoutine());
    }

    private bool ValidateReferences()
    {
        if (targetTransform == null)
        {
            Debug.LogError("[TestManager] Target Transform no está asignado.", this);
            return false;
        }
        if (graphProvider == null || graphProvider.Graph == null)
        {
            Debug.LogError("[TestManager] NavGraphProvider o su Grafo no están asignados.", this);
            return false;
        }
        if (formationRecorder == null)
        {
            Debug.LogWarning("[TestManager] FormationRecorder no está asignado.", this);
        }
        return true;
    }

    private IEnumerator WaitAndQuitRoutine()
    {
        yield return new WaitForSeconds(timerSeconds);

        if (formationRecorder != null)
        {
            formationRecorder.RecordAndSaveData();
        }

        QuitApplication();
    }

    private void QuitApplication()
    {
        Debug.Log("[TestManager] Tiempo de prueba finalizado. Grabando y cerrando la aplicación...");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SendAllAgentsToTarget()
    {
        if (!TryGetValidDestination(targetTransform.position, out int destinationNode, out Vector3 nodeWorldPos))
            return;

        EnsureRouteRegistered(destinationNode);

        ProcessStandardAgents(destinationNode, nodeWorldPos);
        ProcessECSAgents(destinationNode, nodeWorldPos);
        ProcessNavMeshAgents(targetTransform.position);
    }

    private bool TryGetValidDestination(Vector3 targetPosition, out int nodeIndex, out Vector3 nodeWorldPos)
    {
        nodeIndex = graphProvider.Graph.GetClosestNode(targetPosition);
        nodeWorldPos = Vector3.zero;

        if (nodeIndex == -1 || !graphProvider.Graph.IsWalkable(nodeIndex))
        {
            Debug.LogWarning("[TestManager] La posición objetivo no es un nodo caminable válido.");
            return false;
        }

        nodeWorldPos = graphProvider.Graph.GetNodePosition(nodeIndex);
        return true;
    }

    private void EnsureRouteRegistered(int destinationNode)
    {
        if (!FlowFieldManager.Instance.TryGetRoute(graphProvider.Graph, destinationNode))
        {
            FlowFieldManager.Instance.RegisterRoute(graphProvider.Graph, destinationNode);
        }
    }

    private void ProcessStandardAgents(int destinationNode, Vector3 nodeWorldPosition)
    {
        NavAgent[] activeAgents = Object.FindObjectsByType<NavAgent>(FindObjectsSortMode.None);
        if (activeAgents.Length == 0) return;

        foreach (NavAgent agent in activeAgents)
        {
            if (agent != null) agent.SetDestination(destinationNode);
        }

        FormationGenerator.GenerateAndApply(
            formationType,
            nodeWorldPosition,
            formationSpacing,
            activeAgents,
            shapeTexture,
            graphProvider.Graph
        );
    }

    private void ProcessECSAgents(int destinationNode, Vector3 nodeWorldPosition)
    {
        Debug.Log("[TestManager] Procesando agentes ECS...");
        if (World.DefaultGameObjectInjectionWorld == null) return;

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<AgentComponent>());
        Debug.Log($"[TestManager] Agentes ECS encontrados: {query.CalculateEntityCount()}");

        if (query.IsEmptyIgnoreFilter) return;

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        foreach (Entity entity in entities)
        {
            AgentComponent agent = entityManager.GetComponentData<AgentComponent>(entity);
            agent.NextRouteId = destinationNode;
            entityManager.SetComponentData(entity, agent);
        }

        FormationGenerator.GenerateAndApply(
            formationType,
            nodeWorldPosition,
            formationSpacing,
            query,
            entityManager,
            shapeTexture,
            graphProvider.Graph
        );
    }

    private void ProcessNavMeshAgents(Vector3 destination)
    {
        NavMeshAgent[] navMeshAgents = Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
        if (navMeshAgents.Length == 0) return;

        List<Vector3> offsets = FormationGenerator.Generate(
            formationType, 
            navMeshAgents.Length, 
            formationSpacing, 
            shapeTexture, 
            destination, 
            graphProvider.Graph);

        int index = 0;
        foreach (NavMeshAgent agent in navMeshAgents)
        {
            if (agent != null && agent.isOnNavMesh)
            {
                Vector3 offsetDestination = destination + offsets[index];
                agent.SetDestination(offsetDestination);
                index++;
            }
        }
    }
}