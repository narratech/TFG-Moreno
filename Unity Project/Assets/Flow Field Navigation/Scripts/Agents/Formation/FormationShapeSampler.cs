using System.Collections.Generic;
using UnityEngine;

public static class FormationShapeSampler
{
    private static float formationWidth = 10f;
    private static float formationDepth = 10f;
    private static float rotationAngle = -90f;

    public static IReadOnlyList<Vector3> GenerateSample(
        int unitCount,
        Texture2D shapeTexture)
    {
        List<Vector3> offsets = new List<Vector3>();

        if (shapeTexture == null || unitCount <= 0)
            return offsets;

        // 1. Obtener todos los píxeles válidos
        List<Vector2> validPixels = new List<Vector2>();

        for (int y = 0; y < shapeTexture.height; y++)
        {
            for (int x = 0; x < shapeTexture.width; x++)
            {
                Color pixel = shapeTexture.GetPixel(x, y);

                // Negro = zona válida
                if (pixel.r < 0.5f)
                {
                    validPixels.Add(new Vector2(x, y));
                }
            }
        }

        if (validPixels.Count == 0)
            return offsets;

        // 2. Elegir primer punto
        Vector2 first =
            validPixels[Random.Range(0, validPixels.Count)];

        List<Vector2> selected = new List<Vector2>();
        selected.Add(first);

        // 3. Seleccionar puntos lo más separados posible
        while (selected.Count < unitCount &&
               selected.Count < validPixels.Count)
        {
            Vector2 bestPoint = Vector2.zero;
            float bestDistance = -1f;

            foreach (Vector2 candidate in validPixels)
            {
                float minDistance = float.MaxValue;

                foreach (Vector2 point in selected)
                {
                    float distance =
                        (candidate - point).sqrMagnitude;

                    if (distance < minDistance)
                        minDistance = distance;
                }

                if (minDistance > bestDistance)
                {
                    bestDistance = minDistance;
                    bestPoint = candidate;
                }
            }

            selected.Add(bestPoint);
        }

        // 4. Rotación de la formación
        Quaternion rotation =
            Quaternion.Euler(0f, rotationAngle, 0f);

        // 5. Convertir píxeles -> offsets
        foreach (Vector2 pixel in selected)
        {
            float normalizedX =
                (pixel.x / (shapeTexture.width - 1)) - 0.5f;

            float normalizedY =
                (pixel.y / (shapeTexture.height - 1)) - 0.5f;

            Vector3 offset = new Vector3(
                normalizedX * formationWidth,
                0f,
                normalizedY * formationDepth
            );

            // Aplicar rotación alrededor del centro
            offset = rotation * offset;

            offsets.Add(offset);
        }

        return offsets;
    }
}