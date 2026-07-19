using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PlanetGenerator : MonoBehaviour
{
    [Header("Sphere")]
    [Min(2)]
    public int resolution = 64;

    [Min(0.1f)]
    public float radius = 100f;

    [Header("Terrain")]

    public int seed = 1337;

    public float mountainNoiseScale = 4f;
    public float mountainHeight = 25f;

    [Range(0, 1)]
    public float mountainThreshold = 0.82f;

    [Range(1, 10)]
    public float mountainSharpness = 4f;

    [Header("Fractal")]

    [Range(1, 8)]
    public int octaves = 5;

    public float persistence = 0.5f;
    public float lacunarity = 2f;

    FastNoiseLite mountainNoise;

    [ContextMenu("Generate Planet")]
    public void Generate()
    {
        mountainNoise = new FastNoiseLite(seed + 1000);

        mountainNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);

        mountainNoise.SetFractalType(FastNoiseLite.FractalType.FBm);

        mountainNoise.SetFractalOctaves(octaves);

        mountainNoise.SetFractalGain(persistence);

        mountainNoise.SetFractalLacunarity(lacunarity);

        Mesh mesh = BuildQuadSphere();

        mesh.name = $"Planet_{resolution}";

        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    Mesh BuildQuadSphere()
    {
        Mesh mesh = new Mesh();

        int vertsPerFace = (resolution + 1) * (resolution + 1);
        int trisPerFace = resolution * resolution * 6;

        Vector3[] vertices = new Vector3[vertsPerFace * 6];
        Vector3[] normals = new Vector3[vertices.Length];
        int[] triangles = new int[trisPerFace * 6];

        int v = 0;
        int t = 0;

        BuildFace(Vector3.forward, Vector3.right, Vector3.up);
        BuildFace(Vector3.back, Vector3.left, Vector3.up);
        BuildFace(Vector3.right, Vector3.back, Vector3.up);
        BuildFace(Vector3.left, Vector3.forward, Vector3.up);
        BuildFace(Vector3.up, Vector3.right, Vector3.back);
        BuildFace(Vector3.down, Vector3.right, Vector3.forward);

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;

        void BuildFace(Vector3 localUp, Vector3 axisA, Vector3 axisB)
        {
            int row = resolution + 1;

            for (int y = 0; y <= resolution; y++)
            {
                for (int x = 0; x <= resolution; x++)
                {
                    Vector2 percent = new Vector2(x, y) / resolution;

                    Vector3 pointOnCube =
                        localUp +
                        (percent.x - 0.5f) * 2f * axisA +
                        (percent.y - 0.5f) * 2f * axisB;

                    Vector3 dir = pointOnCube.normalized;

                    float h = GetHeight(dir);

                    vertices[v] = dir * h;
                    normals[v] = dir;

                    if (x < resolution && y < resolution)
                    {
                        triangles[t++] = v;
                        triangles[t++] = v + row + 1;
                        triangles[t++] = v + row;

                        triangles[t++] = v;
                        triangles[t++] = v + 1;
                        triangles[t++] = v + row + 1;
                    }

                    v++;
                }
            }
        }

        float GetHeight(Vector3 dir)
        {
            // Ruido de montaña [-1, 1]
            float noise = mountainNoise.GetNoise(
                dir.x * mountainNoiseScale,
                dir.y * mountainNoiseScale,
                dir.z * mountainNoiseScale);

            // Convertimos a [0,1]
            noise = (noise + 1f) * 0.5f;

            // Eliminamos todo lo que esté por debajo del umbral
            if (noise <= mountainThreshold) 
                return radius;

            // Reescalamos el resto para que vuelva a ocupar [0,1]
            noise = (noise - mountainThreshold) / (1f - mountainThreshold);

            // Hace las montañas más abruptas
            noise = 1f - Mathf.Pow(1f - noise, mountainSharpness);

            return radius + noise * mountainHeight;
        }
    }
}

