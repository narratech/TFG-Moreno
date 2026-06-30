using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DOTSFlowField
{
    /// <summary>
    /// Gestiona el ciclo de vida de regiones dinámicas en función de los agentes activos.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(IntegrationFieldSystem))]
    public partial struct RegionLifecycleSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AgentMovementData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;

            var navGraph = FlowFieldBridge.Instance.gridNavGraph;
            if (navGraph == null) return;

            var regionQuery = em.CreateEntityQuery(typeof(RegionRouteConfig));
            var regionEntities = regionQuery.ToEntityArray(Allocator.Temp);
            var regionConfigs = regionQuery.ToComponentDataArray<RegionRouteConfig>(Allocator.Temp);

            var existingRegionsMap = new NativeParallelHashMap<int2, Entity>(
                regionEntities.Length, Allocator.Temp);

            for (int i = 0; i < regionEntities.Length; i++)
            {
                var config = regionConfigs[i];

                if (config.RouteEntity == Entity.Null)
                    continue;

                if (config.State == RegionState.Generated || config.State == RegionState.Required)
                {
                    config.State = RegionState.ToEliminate;
                    em.SetComponentData(regionEntities[i], config);
                }

                existingRegionsMap.TryAdd(
                    new int2(config.RouteEntity.Index, config.RegionId),
                    regionEntities[i]);
            }

            var agentQuery = em.CreateEntityQuery(typeof(AgentMovementData), typeof(LocalTransform));
            var agentTransforms = agentQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var agentDatas = agentQuery.ToComponentDataArray<AgentMovementData>(Allocator.Temp);

            var currentRouteEntity = FlowFieldBridge.Instance.CurrentActiveRouteEntity;

            var neededRegions = new NativeParallelHashSet<int>(
                agentTransforms.Length, Allocator.Temp);

            for (int i = 0; i < agentTransforms.Length; i++)
            {
                var agent = agentDatas[i];

                if (agent.RouteId == -1)
                    continue;

                int regionId = navGraph.GetRegionId(
                    navGraph.GetClosestNode(agentTransforms[i].Position));

                neededRegions.Add(regionId);
            }

            var baseQuery = em.CreateEntityQuery(typeof(RegionRouteConfig));

            var baseEntities = baseQuery.ToEntityArray(Allocator.Temp);
            var baseConfigs = baseQuery.ToComponentDataArray<RegionRouteConfig>(Allocator.Temp);

            foreach (var regionId in neededRegions)
            {
                var key = new int2(currentRouteEntity.Index, regionId);

                if (existingRegionsMap.TryGetValue(key, out var existing))
                {
                    var config = em.GetComponentData<RegionRouteConfig>(existing);
                    config.State = RegionState.Required;
                    em.SetComponentData(existing, config);
                    continue;
                }

                var entity = em.CreateEntity();

                em.AddComponentData(entity, new RegionRouteConfig
                {
                    RegionId = regionId,
                    RouteEntity = currentRouteEntity,
                    TargetNodeGlobal = -1,
                    State = RegionState.Required,
                    IsDirty = true,
                    RouteLevel = 0
                });

                em.AddBuffer<NodeDataBuffer>(entity);
                em.AddBuffer<IntegrationFieldBuffer>(entity);
                em.AddBuffer<FlowDirectionBuffer>(entity);

                int nodeCount = navGraph.GetRegionSize(regionId);

                em.GetBuffer<NodeDataBuffer>(entity);
                em.GetBuffer<IntegrationFieldBuffer>(entity).ResizeUninitialized(nodeCount);
                em.GetBuffer<FlowDirectionBuffer>(entity).ResizeUninitialized(nodeCount);

#if UNITY_EDITOR
                em.SetName(entity, $"DynamicRegion_{regionId}_Route_{currentRouteEntity.Index}");
#endif
            }

            for (int i = 0; i < regionEntities.Length; i++)
            {
                if (regionConfigs[i].RouteEntity == Entity.Null)
                    continue;

                var config = em.GetComponentData<RegionRouteConfig>(regionEntities[i]);

                if (config.State == RegionState.ToEliminate)
                {
                    em.DestroyEntity(regionEntities[i]);
                }
            }

            regionEntities.Dispose();
            regionConfigs.Dispose();
            existingRegionsMap.Dispose();
            agentTransforms.Dispose();
            agentDatas.Dispose();
            baseEntities.Dispose();
            baseConfigs.Dispose();
            neededRegions.Dispose();
        }
    }
}