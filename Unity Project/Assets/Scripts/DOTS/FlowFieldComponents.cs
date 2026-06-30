using Unity.Entities;
using Unity.Mathematics;

namespace DOTSFlowField
{
    /// <summary>
    /// Estados del ciclo de vida de una región dentro de una ruta.
    /// </summary>
    public enum RegionState
    {
        Required,
        Generated,
        ToEliminate
    }

    /// <summary>
    /// Configuración de una región asociada a una ruta de navegación.
    /// </summary>
    public struct RegionRouteConfig : IComponentData
    {
        public int RegionId;
        public Entity RouteEntity;
        public int TargetNodeGlobal;
        public int RouteLevel;
        public RegionState State;
        public bool IsDirty;
    }

    /// <summary>
    /// Datos de movimiento utilizados por los agentes.
    /// </summary>
    public struct AgentMovementData : IComponentData
    {
        public float Speed;
        public float3 Velocity;
        public int RouteId;
    }
}