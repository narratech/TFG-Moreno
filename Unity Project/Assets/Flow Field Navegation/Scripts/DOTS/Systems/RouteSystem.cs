using DOTSFlowField;
using System.Diagnostics;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
// [UpdateBefore(typeof(MovementSystem))]
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
        // Actualiza los agentes usando SystemAPI.Query en vez de Entities.ForEach
        foreach (var agentRef in SystemAPI.Query<RefRW<AgentComponent>>())
        {
            ref AgentComponent agent = ref agentRef.ValueRW;

            if (agent.NextRouteId < 0)
                continue;

            if (agent.RouteId == agent.NextRouteId)
            {
                agent.NextRouteId = -1;
                continue;
            }

            agent.RouteId = agent.NextRouteId;
            agent.NextRouteId = -1;

            UnityEngine.Debug.Log($"Agent updated RouteId to {agent.RouteId}");
        }

        var storage = FlowFieldStorage.Instance;

        var availableFields = storage.FieldMap;
        var navGraphs = storage.NavGraphs;

        // Peticiones del frame actual.
        _requests.Clear();

        var requests = _requests.AsParallelWriter();

        new DetectMissingFlowFieldsJob
        {
            AvailableFields = availableFields,
            NavGraphs = navGraphs,
            Requests = requests
        }.Run();

        // Las peticiones se procesan desde C# normal.
        Dependency.Complete();

        UnityEngine.Debug.Log($"Total requests: {_requests.Count()}");

        foreach (var key in _requests)
        {
            RequestFlowField(key);
        }
    }

    private void RequestFlowField(FlowFieldKey key)
    {
        FlowFieldManager.Instance.RequestFlowField(
            key.GraphId,
            key.RouteId,
            key.RegionId);
    }

    //[BurstCompile]
    private partial struct DetectMissingFlowFieldsJob : IJobEntity
    {
        [ReadOnly]
        public NativeParallelHashMap<
            FlowFieldKey,
            NativeFlowFieldInfo>.ReadOnly AvailableFields;

        [ReadOnly]
        public NativeList<NavGraphData> NavGraphs;

        public NativeParallelHashSet<FlowFieldKey>.ParallelWriter Requests;

        private void Execute(
            in AgentComponent agent,
            in LocalTransform transform)
        {
            UnityEngine.Debug.Log(
                $"EXECUTE -> GraphId: {agent.GraphId}, RouteId: {agent.RouteId}");

            if (agent.RouteId < 0)
                return;

            UnityEngine.Debug.Log("RouteId válido");

            NavGraphData graph = NavGraphs[agent.GraphId];

            int nodeId = NavGraphAPI.GetClosestNode(
                graph,
                transform.Position);

            int regionId = NavGraphAPI.GetRegionId(
                graph,
                nodeId);

            UnityEngine.Debug.Log(
                $"Node: {nodeId}, Region: {regionId}");

            var key = new FlowFieldKey(
                agent.GraphId,
                agent.RouteId,
                regionId);

            bool exists = AvailableFields.ContainsKey(key);

            UnityEngine.Debug.Log(
                $"FlowField exists: {exists}");

            if (!exists)
            {
                bool added = Requests.Add(key);

                UnityEngine.Debug.Log(
                    $"Request added: {added}");
            }
        }
    }
}