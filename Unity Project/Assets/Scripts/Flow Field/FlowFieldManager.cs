using System.Collections.Generic;
using UnityEngine;

public class FlowFieldManager : MonoBehaviour
{
    // Añade esta línea dentro de tu FlowFieldManager
    public FlowFieldData LastCalculatedData { get; private set; }
    public INavigationGraph LastUsedGraph { get; private set; }

    // singleton para acceso global
    public static FlowFieldManager Instance { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Método principal que "cocina" el FlowField
    public void CalculateFlowField(INavigationGraph graph, FlowFieldData data)
    {
        GenerateIntegrationField(graph, data);
        GenerateFlowConinuosDirection(graph, data);

        LastCalculatedData = data;
        LastUsedGraph = graph;
    }

    // PASO 1: Dijkstra desde el destino hacia atrás
    private void GenerateIntegrationField(INavigationGraph graph, FlowFieldData data)
    {
        Queue<int> nodesToVisit = new Queue<int>();

        // El destino tiene coste 0
        data.IntegrationField[data.DestinationNode] = 0;
        nodesToVisit.Enqueue(data.DestinationNode);

        while (nodesToVisit.Count > 0)
        {
            int currentNode = nodesToVisit.Dequeue();

            foreach (int neighbor in graph.GetNeighbors(currentNode))
            {
                // Cálculo del nuevo coste: coste acumulado + distancia entre ellos * peso del terreno
                float stepDistance = graph.GetDistanceBetweenNeighbors(currentNode, neighbor);
                float newCost = data.IntegrationField[currentNode] + (stepDistance * graph.GetNodeCost(neighbor));

                // Si encontramos un camino más corto, actualizamos
                if (newCost < data.IntegrationField[neighbor])
                {
                    data.IntegrationField[neighbor] = newCost;
                    nodesToVisit.Enqueue(neighbor);
                }
            }
        }
    }

    // PASO 2: Generar vectores apuntando al vecino con menor coste (Gradiente)
    private void GenerateFlowDirections(INavigationGraph graph, FlowFieldData data)
    {
        for (int i = 0; i < graph.NodeCount; i++)
        {
            if (!graph.IsWalkable(i)) continue;

            int bestNeighbor = -1;
            float minCost = data.IntegrationField[i];

            foreach (int neighbor in graph.GetNeighbors(i))
            {
                if (data.IntegrationField[neighbor] < minCost)
                {
                    minCost = data.IntegrationField[neighbor];
                    bestNeighbor = neighbor;
                }
            }

            if (bestNeighbor != -1)
            {
                Vector3 currentPos = graph.GetNodePosition(i);
                Vector3 neighborPos = graph.GetNodePosition(bestNeighbor);
                data.FlowDirections[i] = (neighborPos - currentPos).normalized;
            }
        }
    }

    // Segunda opcion: generar un FlowField con direcciones continuas (en vez de apuntar al vecino más cercano, calcular un vector promedio ponderado)

    private void GenerateFlowConinuosDirection(INavigationGraph graph, FlowFieldData data)
    {
        for (int i = 0; i < graph.NodeCount; i++)
        {
            if (!graph.IsWalkable(i)) continue;
            Vector3 currentPos = graph.GetNodePosition(i);
            Vector3 flowDirection = Vector3.zero;
            float totalWeight = 0f;
            foreach (int neighbor in graph.GetNeighbors(i))
            {
                float costDifference = data.IntegrationField[i] - data.IntegrationField[neighbor];
                if (costDifference > 0)
                {
                    Vector3 neighborPos = graph.GetNodePosition(neighbor);
                    Vector3 directionToNeighbor = (neighborPos - currentPos).normalized;
                    flowDirection += directionToNeighbor * costDifference; // Ponderamos por la diferencia de coste
                    totalWeight += costDifference;
                }
            }
            if (totalWeight > 0)
            {
                data.FlowDirections[i] = flowDirection.normalized; // Normalizamos el vector resultante
            }
        }

    }
}