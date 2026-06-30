using Unity.Entities;
using Unity.Mathematics;

namespace DOTSFlowField
{
    /// <summary>
    /// Almacena el coste acumulado desde la meta para cada nodo.
    /// </summary>
    public struct IntegrationFieldBuffer : IBufferElementData
    {
        public float Value;

        public static implicit operator float(IntegrationFieldBuffer element) => element.Value;
        public static implicit operator IntegrationFieldBuffer(float value) => new IntegrationFieldBuffer { Value = value };
    }

    /// <summary>
    /// Almacena la dirección de movimiento asociada a cada nodo.
    /// </summary>
    public struct FlowDirectionBuffer : IBufferElementData
    {
        public float3 Value;

        public static implicit operator float3(FlowDirectionBuffer element) => element.Value;
        public static implicit operator FlowDirectionBuffer(float3 value) => new FlowDirectionBuffer { Value = value };
    }

    /// <summary>
    /// Contiene la posición en el mundo de cada nodo de la región.
    /// </summary>
    public struct NodeDataBuffer : IBufferElementData
    {
        public float3 Position;
    }
}