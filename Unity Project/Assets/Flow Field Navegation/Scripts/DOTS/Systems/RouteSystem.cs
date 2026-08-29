using DOTSFlowField;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class RouteSystem : SystemBase
{
    private NativeParallelHashSet<FlowFieldKey> _requests;

    protected override void OnCreate()
    {
        base.OnCreate();

        _requests = new NativeParallelHashSet<FlowFieldKey>(
            128,
            Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        if (_requests.IsCreated)
            _requests.Dispose();

        base.OnDestroy();
    }

    protected override void OnUpdate()
    {
        ApplyPendingRoutes();
        DetectMissingFlowFields();
        ProcessRequests();
    }

    private void ApplyPendingRoutes()
    {
        foreach (var agentRef in SystemAPI.Query<RefRW<AgentComponent>>())
        {
            ref AgentComponent agent = ref agentRef.ValueRW;

            // No hay nueva ruta pendiente.
            if (agent.NextRouteId < 0)
                continue;

            // Ya estamos en esa ruta.
            if (agent.RouteId == agent.NextRouteId)
            {
                agent.NextRouteId = -1; 
                continue;
            }

            // Aplicar nueva ruta.
            agent.RouteId = agent.NextRouteId;

            // Consumir la petición.
            agent.NextRouteId = -1;
        }
    }

    private void DetectMissingFlowFields()
    {
        var storage = FlowFieldStorage.Instance;

        _requests.Clear();

        Dependency = new DetectMissingFlowFieldsJob
        {
            AvailableFields = storage.FieldMap,
            NavGraphs = storage.NavGraphs,
            Requests = _requests.AsParallelWriter()
        }.ScheduleParallel(Dependency);

        Dependency.Complete();

        ProcessRequests();
    }

    private void ProcessRequests()
    {
        foreach (var key in _requests)
        {
            FlowFieldManager.Instance.RequestFlowField(
                key.GraphId,
                key.RouteId,
                key.RegionId);
        }
    }

    private partial struct DetectMissingFlowFieldsJob : IJobEntity
    {
        [ReadOnly]
        public NativeParallelHashMap<
            FlowFieldKey,
            NativeFlowFieldInfo>.ReadOnly AvailableFields;

        [ReadOnly]
        public NativeList<NavGraphData> NavGraphs;

        public NativeParallelHashSet<
            FlowFieldKey>.ParallelWriter Requests;

        private void Execute(
            in AgentComponent agent,
            in LocalTransform transform)
        {
            // Sin ruta no hay nada que solicitar.
            if (agent.RouteId < 0)
                return;

            // Obtener el grafo del agente.
            NavGraphData graph = NavGraphs[agent.GraphId];

            // Nodo actual del agente.
            int nodeId = NavGraphAPI.GetClosestNode(
                graph,
                transform.Position);

            if (nodeId < 0)
                return;

            // Región actual.
            int regionId = NavGraphAPI.GetRegionId(
                graph,
                nodeId);

            if (regionId < 0)
                return;

            // Identificador único del FlowField.
            var key = new FlowFieldKey(
                agent.GraphId,
                agent.RouteId,
                regionId);

            // Ya existe.
            if (AvailableFields.ContainsKey(key))
                return;

            // No existe → solicitarlo.
            Requests.Add(key);
        }
    }
}