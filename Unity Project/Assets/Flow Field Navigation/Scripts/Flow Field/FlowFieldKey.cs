using System;

/// <summary>
/// Identifica un Flow Field concreto dentro del sistema de navegación.
/// </summary>
public readonly struct FlowFieldKey : IEquatable<FlowFieldKey>
{
    public readonly int GraphId;
    public readonly int RouteId;
    public readonly int RegionId;

    public FlowFieldKey(int graphId, int routeId, int regionId)
    {
        GraphId = graphId;
        RouteId = routeId;
        RegionId = regionId;
    }

    public bool Equals(FlowFieldKey other)
    {
        return GraphId == other.GraphId &&
               RouteId == other.RouteId &&
               RegionId == other.RegionId;
    }

    public override bool Equals(object obj)
    {
        return obj is FlowFieldKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(GraphId, RouteId, RegionId);
    }
}