using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
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
        float spacing, 
        IReadOnlyList<NavAgent> agents, 
        Texture2D shapeTexture = null)
    {
        List<Vector3> offsets = Generate(type, agents.Count, spacing, shapeTexture);
        ApplyOffsets(agents, offsets);
    }

    public static List<Vector3> Generate(FormationType type, int count, float spacing, Texture2D shapeTexture = null)
    {
        if (type == FormationType.Texture && shapeTexture == null)
            throw new System.ArgumentNullException(nameof(shapeTexture), 
                "A texture must be provided for the Texture formation type.");

        return type switch
        {
            FormationType.Square => GenerateSquare(count, spacing),
            FormationType.Triangle => GenerateTriangle(count, spacing),
            FormationType.Circle => GenerateCircle(count, spacing),
            FormationType.Cube => GenerateCube(count, spacing),
            FormationType.Line => GenerateLine(count, spacing),
            FormationType.Texture => GenerateTexture(count, spacing, shapeTexture),
            _ => new List<Vector3>()
        };
    }

    public static void ApplyOffsets(IReadOnlyList<NavAgent> agents, IReadOnlyList<Vector3> offsets)
    {
        int count = Mathf.Min(
            agents.Count,
            offsets.Count);

        for (int i = 0; i < count; i++)
        {
            FlowFieldSteering steering = agents[i].GetComponent<FlowFieldSteering>();

            if (steering != null)
                steering.SetDesiredOffset(offsets[i]);
        }
    }

    /// <summary>
    /// Genera y aplica los offsets directamente desde una EntityQuery usando EntityManager.
    /// </summary>
    public static void GenerateAndApply(
        FormationType type, 
        float spacing, 
        EntityQuery query, 
        EntityManager entityManager, 
        Texture2D shapeTexture = null)
    {
        if (type == FormationType.Texture && shapeTexture == null)
            throw new System.ArgumentNullException(nameof(shapeTexture),
                "A texture must be provided for the Texture formation type.");

        using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        List<Vector3> offsets = Generate(type, entities.Length, spacing, shapeTexture);

        int count = Mathf.Min(entities.Length, offsets.Count);

        for (int i = 0; i < count; i++)
        {
            Entity entity = entities[i];
            AgentComponent agent = entityManager.GetComponentData<AgentComponent>(entity);
            agent.FormationOffset = (float3)offsets[i];
            entityManager.SetComponentData(entity, agent);
        }
    }

    public static List<Vector3> GenerateTexture(int count, float spacing, Texture2D shapeTexture)
    {
        return FormationShapeSampler.GenerateSample(count, shapeTexture) as List<Vector3>;
    }

    public static List<Vector3> GenerateSquare(int count, float spacing)
    {
        List<Vector3> offsets = new(count);

        offsets.Add(Vector3.zero);

        int side = Mathf.CeilToInt(Mathf.Sqrt(count));

        for (int r = 1; offsets.Count < count; r++)
        {
            for (int z = -r; z <= r && offsets.Count < count; z++)
            {
                for (int x = -r; x <= r && offsets.Count < count; x++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != r)
                        continue;

                    offsets.Add(new Vector3(
                        x * spacing,
                        0f,
                        z * spacing));
                }
            }
        }

        return offsets;
    }

    public static List<Vector3> GenerateTriangle(int count, float spacing)
    {
        List<Vector3> offsets = new(count);

        offsets.Add(Vector3.zero);

        int row = 1;

        while (offsets.Count < count)
        {
            int cells = row + 1;

            float start =
                -(cells - 1) * 0.5f * spacing;

            for (int i = 0; i < cells && offsets.Count < count; i++)
            {
                offsets.Add(new Vector3(
                    start + i * spacing,
                    0f,
                    row * spacing));
            }

            row++;
        }

        return offsets;
    }

    public static List<Vector3> GenerateCircle(int count, float spacing)
    {
        List<Vector3> offsets = new(count);

        offsets.Add(Vector3.zero);

        int ring = 1;

        while (offsets.Count < count)
        {
            float radius = ring * spacing;

            int elements =  Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * ring));

            for (int i = 0; i < elements && offsets.Count < count; i++)
            {
                float angle = i * Mathf.PI * 2f / elements;

                offsets.Add(new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius));
            }

            ring++;
        }

        return offsets;
    }

    public static List<Vector3> GenerateCube(int count, float spacing)
    {
        List<Vector3> offsets = new(count);

        offsets.Add(Vector3.zero);

        for (int r = 1; offsets.Count < count; r++)
        {
            for (int y = -r; y <= r && offsets.Count < count; y++)
            {
                for (int z = -r; z <= r && offsets.Count < count; z++)
                {
                    for (int x = -r; x <= r && offsets.Count < count; x++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y),Mathf.Abs(z)) != r)
                            continue;

                        offsets.Add(new Vector3(
                            x * spacing,
                            y * spacing,
                            z * spacing));
                    }
                }
            }
        }

        return offsets;
    }

    public static List<Vector3> GenerateLine(int count, float spacing)
    {
        List<Vector3> offsets = new(count);

        offsets.Add(Vector3.zero);

        int i = 1;

        while (offsets.Count < count)
        {
            offsets.Add(new Vector3(0f, 0f, i * spacing));

            if (offsets.Count >= count)
                break;

            offsets.Add(new Vector3(0f, 0f, -i * spacing));

            i++;
        }

        return offsets;
    }
}