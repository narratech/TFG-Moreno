using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public enum FormationType
{
    Square,
    Triangle,
    Circle,
    Cube,
    Line,
    Texture
}

public static class FormationGenerator
{
    public static void GenerateAndApply(
        FormationType type,
        Vector3 centerPosition,
        float spacing,
        IReadOnlyList<NavAgent> agents,
        Texture2D shapeTexture = null,
        INavGraph graph = null)
    {
        if (agents == null || agents.Count == 0) return;

        if (centerPosition == default && agents[0] != null)
            centerPosition = agents[0].transform.position;

        List<Vector3> offsets = Generate(type, agents.Count, spacing, shapeTexture, centerPosition, graph);
        ApplyOffsets(agents, offsets);
    }

    public static List<Vector3> Generate(
        FormationType type,
        int count,
        float spacing,
        Texture2D shapeTexture = null,
        Vector3 centerPosition = default,
        INavGraph graph = null)
    {
        if (type == FormationType.Texture && shapeTexture == null)
            throw new System.ArgumentNullException(nameof(shapeTexture),
                "A texture must be provided for the Texture formation type.");

        if (graph == null)
        {
            Debug.LogWarning("No navigation graph provided. Formation generation " +
                "may not respect walkability constraints.");
        }

        return type switch
        {
            FormationType.Square => GenerateSquare(count, spacing, centerPosition, graph),
            FormationType.Triangle => GenerateTriangle(count, spacing, centerPosition, graph),
            FormationType.Circle => GenerateCircle(count, spacing, centerPosition, graph),
            FormationType.Cube => GenerateCube(count, spacing, centerPosition, graph),
            FormationType.Line => GenerateLine(count, spacing, centerPosition, graph),
            FormationType.Texture => GenerateTexture(count, spacing, shapeTexture, centerPosition, graph),
            _ => new List<Vector3>()
        };
    }

    public static void ApplyOffsets(IReadOnlyList<NavAgent> agents, IReadOnlyList<Vector3> offsets)
    {
        int count = Mathf.Min(agents.Count, offsets.Count);

        for (int i = 0; i < count; i++)
        {
            FlowFieldSteering steering = agents[i].GetComponent<FlowFieldSteering>();

            if (steering != null)
                steering.SetFormationOffset(-offsets[i]); // hay que invertir el offset
        }
    }

    /// <summary>
    /// Genera y aplica los offsets directamente desde una EntityQuery usando EntityManager.
    /// </summary>
    public static void GenerateAndApply(
        FormationType type,
        Vector3 centerPosition,
        float spacing,
        EntityQuery query,
        EntityManager entityManager,
        Texture2D shapeTexture = null,
        INavGraph graph = null)
    {
        if (type == FormationType.Texture && shapeTexture == null)
            throw new System.ArgumentNullException(nameof(shapeTexture),
                "A texture must be provided for the Texture formation type.");

        using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        if (entities.Length == 0) return;

        if (centerPosition == default && entityManager.HasComponent<LocalTransform>(entities[0]))
        {
            centerPosition = entityManager.GetComponentData<LocalTransform>(entities[0]).Position;
        }

        List<Vector3> offsets = Generate(type, entities.Length, spacing, shapeTexture, centerPosition, graph);

        int count = Mathf.Min(entities.Length, offsets.Count);

        for (int i = 0; i < count; i++)
        {
            Entity entity = entities[i];
            AgentComponent agent = entityManager.GetComponentData<AgentComponent>(entity);
            agent.FormationOffset = (float3)(-offsets[i]); // hay que invertir el offset
            entityManager.SetComponentData(entity, agent);
        }
    }

    // =========================================================================
    // VALIDACIÓN DE WALKABILITY DIRECTA SOBRE EL GRAFO
    // =========================================================================

    private static bool IsOffsetWalkable(Vector3 offset, Vector3 centerPosition, INavGraph grafo)
    {
        if (grafo == null)
            return true; // Si no se pasa grafo, se permite por defecto

        // Si el offset es cero, es la posición central exacta
        if (math.lengthsq((float3)offset) < 0.0001f)
        {
            int centerNode = grafo.GetClosestNode((float3)centerPosition);
            if (centerNode < 0 || !grafo.IsInBounds((float3)centerPosition)) return false;
            return grafo.IsWalkable(centerNode);
        }

        // 1. Obtenemos el nodo central para consultar su normal en el grafo
        int currentNode = grafo.GetClosestNode((float3)centerPosition);
        Vector3 realOffset = offset;

        if (currentNode >= 0)
        {
            // Intentamos obtener la normal del nodo en la superficie
            // (Ajusta esto según si tu interfaz INavGraph tiene GetNodeNormal o si usas NavGraphAPI)
            Vector3 normal = Vector3.up;
            bool hasNormal = false;

            // Aplicamos la rotación del offset según la normal de la superficie (idéntico a GetRealOffset)
            if (hasNormal && normal != Vector3.zero)
            {
                float dot = Vector3.Dot(Vector3.up, normal);

                if (dot < 0.9999f)
                {
                    Vector3 axis = Vector3.Cross(Vector3.up, normal);
                    float axisLen = axis.magnitude;

                    if (axisLen > 0.0001f)
                    {
                        float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f));
                        Quaternion rot = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, axis / axisLen);
                        realOffset = rot * offset;
                    }
                    else if (dot < -0.9999f)
                    {
                        Quaternion rot = Quaternion.AngleAxis(180f, Vector3.right);
                        realOffset = rot * offset;
                    }
                }
            }
        }

        // 2. Calculamos la posición mundial real con el offset ya rotado por la pendiente
        Vector3 worldPos = centerPosition + realOffset;
        int targetNode = grafo.GetClosestNode((float3)worldPos);

        if (targetNode < 0) return false;
        if (!grafo.IsInBounds((float3)worldPos)) return false;

        return grafo.IsWalkable(targetNode);
    }

    // =========================================================================
    // GENERADORES FILTRADOS
    // =========================================================================

    public static List<Vector3> GenerateSquare(int count, float spacing, Vector3 centerPosition = default, INavGraph grafo = null)
    {
        List<Vector3> offsets = new(count);

        Vector3 centerOffset = Vector3.zero;
        if (IsOffsetWalkable(centerOffset, centerPosition, grafo))
        {
            offsets.Add(centerOffset);
        }

        int r = 1;
        while (offsets.Count < count && r < 100)
        {
            for (int z = -r; z <= r && offsets.Count < count; z++)
            {
                for (int x = -r; x <= r && offsets.Count < count; x++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != r)
                        continue;

                    Vector3 candidate = new Vector3(x * spacing, 0f, z * spacing);
                    if (IsOffsetWalkable(candidate, centerPosition, grafo))
                    {
                        offsets.Add(candidate);
                        if (offsets.Count >= count) break;
                    }
                }
            }
            r++;
        }

        return offsets;
    }

    public static List<Vector3> GenerateTriangle(int count, float spacing, Vector3 centerPosition = default, INavGraph grafo = null)
    {
        List<Vector3> offsets = new(count);

        Vector3 centerOffset = Vector3.zero;
        if (IsOffsetWalkable(centerOffset, centerPosition, grafo))
        {
            offsets.Add(centerOffset);
        }

        int row = 1;
        while (offsets.Count < count && row < 200)
        {
            int cells = row + 1;
            float start = -(cells - 1) * 0.5f * spacing;

            for (int i = 0; i < cells && offsets.Count < count; i++)
            {
                Vector3 candidate = new Vector3(start + i * spacing, 0f, row * spacing);
                if (IsOffsetWalkable(candidate, centerPosition, grafo))
                {
                    offsets.Add(candidate);
                    if (offsets.Count >= count) break;
                }
            }
            row++;
        }

        return offsets;
    }

    public static List<Vector3> GenerateCircle(int count, float spacing, Vector3 centerPosition = default, INavGraph grafo = null)
    {
        List<Vector3> offsets = new(count);

        Vector3 centerOffset = Vector3.zero;
        if (IsOffsetWalkable(centerOffset, centerPosition, grafo))
        {
            offsets.Add(centerOffset);
        }

        int ring = 1;
        while (offsets.Count < count && ring < 100)
        {
            float radius = ring * spacing;
            int elements = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * ring));

            for (int i = 0; i < elements && offsets.Count < count; i++)
            {
                float angle = i * Mathf.PI * 2f / elements;
                Vector3 candidate = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius);

                if (IsOffsetWalkable(candidate, centerPosition, grafo))
                {
                    offsets.Add(candidate);
                    if (offsets.Count >= count) break;
                }
            }
            ring++;
        }

        return offsets;
    }

    public static List<Vector3> GenerateCube(int count, float spacing, Vector3 centerPosition = default, INavGraph grafo = null)
    {
        List<Vector3> offsets = new(count);

        Vector3 centerOffset = Vector3.zero;
        if (IsOffsetWalkable(centerOffset, centerPosition, grafo))
        {
            offsets.Add(centerOffset);
        }

        int r = 1;
        while (offsets.Count < count && r < 50)
        {
            for (int y = -r; y <= r && offsets.Count < count; y++)
            {
                for (int z = -r; z <= r && offsets.Count < count; z++)
                {
                    for (int x = -r; x <= r && offsets.Count < count; x++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y), Mathf.Abs(z)) != r)
                            continue;

                        Vector3 candidate = new Vector3(x * spacing, y * spacing, z * spacing);
                        if (IsOffsetWalkable(candidate, centerPosition, grafo))
                        {
                            offsets.Add(candidate);
                            if (offsets.Count >= count) break;
                        }
                    }
                }
            }
            r++;
        }

        return offsets;
    }

    public static List<Vector3> GenerateLine(int count, float spacing, Vector3 centerPosition = default, INavGraph grafo = null)
    {
        List<Vector3> offsets = new(count);

        Vector3 centerOffset = Vector3.zero;
        if (IsOffsetWalkable(centerOffset, centerPosition, grafo))
        {
            offsets.Add(centerOffset);
        }

        int i = 1;
        while (offsets.Count < count && i < 200)
        {
            Vector3 candidate1 = new Vector3(0f, 0f, i * spacing);
            if (IsOffsetWalkable(candidate1, centerPosition, grafo))
            {
                offsets.Add(candidate1);
                if (offsets.Count >= count) break;
            }

            Vector3 candidate2 = new Vector3(0f, 0f, -i * spacing);
            if (IsOffsetWalkable(candidate2, centerPosition, grafo))
            {
                offsets.Add(candidate2);
                if (offsets.Count >= count) break;
            }

            i++;
        }

        return offsets;
    }

    public static List<Vector3> GenerateTexture(int count, float spacing, Texture2D shapeTexture, Vector3 centerPosition = default, INavGraph grafo = null)
    {
        List<Vector3> validOffsets = new(count);
        int maxAttempts = 10;
        int attempt = 0;

        while (validOffsets.Count < count && attempt < maxAttempts)
        {
            int requestedCount = (count - validOffsets.Count) * 2 + 5;
            var rawSamples = FormationShapeSampler.GenerateSample(requestedCount, shapeTexture) as List<Vector3>;

            if (rawSamples == null || rawSamples.Count == 0) break;

            foreach (var sample in rawSamples)
            {
                Vector3 candidate = sample * spacing;
                if (IsOffsetWalkable(candidate, centerPosition, grafo))
                {
                    validOffsets.Add(candidate);
                    if (validOffsets.Count >= count) break;
                }
            }
            attempt++;
        }

        return validOffsets;
    }
}