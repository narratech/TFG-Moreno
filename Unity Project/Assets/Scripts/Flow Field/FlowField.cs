using UnityEngine;

public class FlowField
{
    public readonly float[] IntegrationField; // Coste acumulado al destino
    public readonly Vector3[] FlowDirections; // Vectores de dirección finales
    public readonly int RegionId; // Región a la que pertenece este campo de flujo

    public FlowField(int nodeCount, int regionId)
    {
        IntegrationField = new float[nodeCount];
        FlowDirections = new Vector3[nodeCount];
        RegionId = regionId;

        // Inicialización: Infinito para costes, cero para direcciones
        for (int i = 0; i < nodeCount; i++)
        {
            IntegrationField[i] = float.MaxValue;
            FlowDirections[i] = Vector3.zero;
        }
    }
}