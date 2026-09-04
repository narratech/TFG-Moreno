using System.Collections.Generic;


public static class PathFinder
{
    // --- DIJKSTRA / FLOOD FILL LOCAL ---
    public static float[] RunFloodFill(INavGraph navGraph, int startNode, int regionId)
    {
        // Pre-asignamos el array con el tamaño total de nodos
        float[] distances = new float[navGraph.NodeCount];
        for (int i = 0; i < distances.Length; i++) distances[i] = float.MaxValue;

        PriorityQueue<int, float> pq = new PriorityQueue<int, float>();

        distances[startNode] = 0f;
        pq.Enqueue(startNode, 0f);

        while (pq.Count > 0)
        {
            int current = pq.Dequeue();
            float d = distances[current];

            foreach (int neighbor in navGraph.GetNeighbors(current))
            {
                if (!navGraph.IsWalkable(neighbor) || navGraph.GetRegionId(neighbor) != regionId)
                    continue;

                float newDist = d + navGraph.GetDistanceBetweenNeighbors(current, neighbor);

                if (newDist < distances[neighbor])
                {
                    distances[neighbor] = newDist;
                    pq.Enqueue(neighbor, newDist);
                }
            }
        }
        return distances;
    }

    // --- A* ESTÁNDAR ---
    public static List<int> FindPath(INavGraph grid, int startNode, int endNode)
    {
        // Implementación clásica de A* usando grid.GetNodePosition para la heurística
        return new List<int>();
    }
}