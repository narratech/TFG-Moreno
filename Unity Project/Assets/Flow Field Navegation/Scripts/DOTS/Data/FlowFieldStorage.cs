using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Almacena las representaciones nativas de los Flow Fields
/// y de los grafos de navegación disponibles para los sistemas DOTS.
///
/// El Storage es propietario de toda la memoria nativa que contiene.
/// </summary>
public class FlowFieldStorage : IDisposable
{
    private static FlowFieldStorage _instance;

    public static FlowFieldStorage Instance =>
        _instance ??= new FlowFieldStorage();

    private NativeParallelHashMap<FlowFieldKey, NativeFlowFieldInfo> _fieldMap;

    private NativeList<float3> _directions;

    private NativeList<NavGraphData> _navGraphData;

    public NativeList<float> StaticCosts;
    public NativeList<bool> Walkability;

    public NativeParallelHashMap< FlowFieldKey, NativeFlowFieldInfo>.ReadOnly FieldMap =>
        _fieldMap.AsReadOnly();

    public NativeList<float3> Directions =>
        _directions;

    public NativeList<NavGraphData> NavGraphs =>
        _navGraphData;

    public FlowFieldStorage(int initialCapacity = 24)
    {
        _fieldMap = new NativeParallelHashMap<
            FlowFieldKey,
            NativeFlowFieldInfo>(
                initialCapacity,
                Allocator.Persistent);

        _directions = new NativeList<float3>(
            Allocator.Persistent);

        _navGraphData = new NativeList<NavGraphData>(
            initialCapacity,
            Allocator.Persistent);

        StaticCosts = new NativeList<float>(
            Allocator.Persistent);
        Walkability = new NativeList<bool>(
            Allocator.Persistent);
    }

    /// <summary>
    /// Registra un Flow Field en el almacenamiento nativo.
    ///
    /// Las direcciones se añaden al bloque contiguo _directions.
    /// NativeFlowFieldInfo guarda el rango correspondiente.
    /// </summary>
    public void Register(
    FlowFieldKey key,
    FlowField flowField)
    {
        if (flowField == null)
            throw new ArgumentNullException(nameof(flowField));

        if (_fieldMap.ContainsKey(key))
            return;

        int startIndex = _directions.Length;

        for (int i = 0; i < flowField.FlowDirections.Length; i++)
        {
            Vector3 direction = flowField.FlowDirections[i];

            _directions.Add(new float3(
                direction.x,
                direction.y,
                direction.z));
        }

        var info = new NativeFlowFieldInfo
        {
            GraphId = key.GraphId,
            RouteId = key.RouteId,
            RegionId = key.RegionId,

            StartIndex = startIndex,
            Length = flowField.FlowDirections.Length
        };

        _fieldMap.TryAdd(key, info);
    }

    /// <summary>
    /// Registra un NavGraphData para su utilización desde DOTS.
    ///
    /// GraphId debe coincidir con el índice que ocupará dentro
    /// de _navGraphData.
    /// </summary>
    public void RegisterNavGraphData(NavGraphData graph)
    {
        if (graph.GraphId != _navGraphData.Length)
        {
            throw new InvalidOperationException(
                $"El GraphId {graph.GraphId} " +
                $"no coincide con el índice esperado " +
                $"{_navGraphData.Length}.");
        }

        graph.GraphId = NavGraphs.Length;

        graph.NodeOffset = Walkability.Length;

        NavGraphs.Add(graph);

        int nodeCount = graph.NodeCount;

        for (int i = 0; i < nodeCount; i++)
        {
            Walkability.Add(true);
            StaticCosts.Add(1f);
        }
    }

    /// <summary>
    /// Comprueba si existe un Flow Field para la clave indicada.
    /// </summary>
    public bool ContainsFlowField(
        FlowFieldKey key)
    {
        return _fieldMap.ContainsKey(key);
    }


    /// <summary>
    /// Intenta obtener la información de un Flow Field.
    /// </summary>
    public bool TryGetFlowFieldInfo(
        FlowFieldKey key,
        out NativeFlowFieldInfo info)
    {
        return _fieldMap.TryGetValue(
            key,
            out info);
    }

    /// <summary>
    /// Elimina un Flow Field.
    ///
    /// Actualmente no se compacta _directions, ya que los índices
    /// almacenados en NativeFlowFieldInfo deben permanecer estables.
    /// </summary>
    public bool Remove(
        FlowFieldKey key)
    {
        return _fieldMap.Remove(key);
    }

    /// <summary>
    /// Libera toda la memoria nativa propiedad del Storage.
    /// </summary>
    public void Dispose()
    {
        if (_fieldMap.IsCreated)
            _fieldMap.Dispose();

        if (_directions.IsCreated)
            _directions.Dispose();

        if (_navGraphData.IsCreated)
        {
            _navGraphData.Dispose();
        }
    }
}

public struct NativeFlowFieldInfo
{
    public int GraphId;
    public int RouteId;
    public int RegionId;

    public int StartIndex;
    public int Length;
}