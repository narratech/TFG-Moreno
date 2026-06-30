using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using BurstCompile = Unity.Burst.BurstCompileAttribute;

namespace DOTSFlowField
{
    /// <summary>
    /// Construye el campo de integración por región usando propagación tipo Dijkstra.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RegionLifecycleSystem))]
    public partial struct IntegrationFieldSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var bridge = FlowFieldBridge.Instance;
            if (bridge == null || !bridge.GlobalPortalDistances.IsCreated)
                return;

            var integrationJob = new IntegrationFieldsParallelJob
            {
                PortalDistances = bridge.GlobalPortalDistances,
                WindowSize = bridge.NumRegionLevelsWindow
            };

            state.Dependency = integrationJob.ScheduleParallel(state.Dependency);
        }
    }

    /// <summary>
    /// Genera el integration field por región en paralelo.
    /// </summary>
    [BurstCompile]
    public partial struct IntegrationFieldsParallelJob : IJobEntity
    {
        [ReadOnly] public NativeParallelHashMap<int, float> PortalDistances;
        public int WindowSize;

        public void Execute(
            ref RegionRouteConfig config,
            ref DynamicBuffer<IntegrationFieldBuffer> integrationBuffer,
            [ReadOnly] in DynamicBuffer<NodeDataBuffer> nodesBuffer)
        {
            if (config.State != RegionState.Required || !config.IsDirty)
                return;

            for (int i = 0; i < integrationBuffer.Length; i++)
                integrationBuffer[i] = float.MaxValue;

            var openSet = new NativeQueue<int>(Allocator.Temp);

            for (int i = 0; i < nodesBuffer.Length; i++)
            {
                int globalNodeId = i; // TODO: mapping real

                if (PortalDistances.TryGetValue(globalNodeId, out float portalCost))
                {
                    integrationBuffer[i] = portalCost;
                    openSet.Enqueue(i);
                }
            }

            int regionWidth = 32; // TODO: sacar de configuración real

            while (openSet.Count > 0)
            {
                int currentNode = openSet.Dequeue();
                float currentCost = integrationBuffer[currentNode].Value;

                int right = currentNode + 1;
                if (right >= 0 && right < integrationBuffer.Length)
                {
                    float newCost = currentCost + 1f;
                    if (newCost < integrationBuffer[right].Value)
                    {
                        integrationBuffer[right] = newCost;
                        openSet.Enqueue(right);
                    }
                }

                int left = currentNode - 1;
                if (left >= 0 && left < integrationBuffer.Length)
                {
                    float newCost = currentCost + 1f;
                    if (newCost < integrationBuffer[left].Value)
                    {
                        integrationBuffer[left] = newCost;
                        openSet.Enqueue(left);
                    }
                }

                int up = currentNode + regionWidth;
                if (up >= 0 && up < integrationBuffer.Length)
                {
                    float newCost = currentCost + 1f;
                    if (newCost < integrationBuffer[up].Value)
                    {
                        integrationBuffer[up] = newCost;
                        openSet.Enqueue(up);
                    }
                }

                int down = currentNode - regionWidth;
                if (down >= 0 && down < integrationBuffer.Length)
                {
                    float newCost = currentCost + 1f;
                    if (newCost < integrationBuffer[down].Value)
                    {
                        integrationBuffer[down] = newCost;
                        openSet.Enqueue(down);
                    }
                }
            }

            openSet.Dispose();

            config.State = RegionState.Generated;
            config.IsDirty = false;
        }
    }
}