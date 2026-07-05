using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace DOTSFlowField
{
    public class FlowFieldBridge
    {
        private static FlowFieldBridge _instance;
        public static FlowFieldBridge Instance => _instance ??= new FlowFieldBridge();

        public INavGraph gridNavGraph;
        public int NumRegionLevelsWindow = 1;

        // Diccionario nativo persistente: int2(RouteIndex, RegionId) -> Entity (Contenedora del Buffer)
        public NativeParallelHashMap<int2, Entity> ActiveRegionsLookup;

        // Mapa global de distancias a portales generado por tu sistema clásico
        public NativeParallelHashMap<int, float> GlobalPortalDistances;
    }

    public struct RouteComponent : IComponentData
    {
        public int RouteIndex;
        public int TargetNodeGlobal;
        public int InitialNodeGlobal;
        public bool IsDirty;
    }

    public struct RegionRouteConfig : IComponentData
    {
        public int RegionId;
        public int RouteIndex;
        public bool IsInsideWindow; // true = insideRegion, false = frontierRegion (sumidero)
    }

    public struct IntegrationFieldBuffer : IBufferElementData
    {
        public float Value;
        public static implicit operator float(IntegrationFieldBuffer e) => e.Value;
        public static implicit operator IntegrationFieldBuffer(float v) => new IntegrationFieldBuffer { Value = v };
    }

    public struct AgentComponent : IComponentData
    {
        public float Speed;
        public float3 Velocity;
        public int RouteId;
    }
}