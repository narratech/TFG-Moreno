using UnityEngine;

public class FlowField
{
    public readonly float[] IntegrationField; // Coste acumulado al destino
    public readonly Vector3[] FlowDirections; // Vectores de dirección finales
    public readonly int DestinationNode;      // El objetivo de este campo

    public FlowField(int nodeCount, int destinationNode)
    {
        DestinationNode = destinationNode;
        IntegrationField = new float[nodeCount];
        FlowDirections = new Vector3[nodeCount];

        // Inicialización: Infinito para costes, cero para direcciones
        for (int i = 0; i < nodeCount; i++)
        {
            IntegrationField[i] = float.MaxValue;
            FlowDirections[i] = Vector3.zero;
        }
    }
}