//using Unity.Collections;
//using Unity.Entities;
//using Unity.Mathematics;
//using BurstCompile = Unity.Burst.BurstCompileAttribute;

//namespace DOTSFlowField
//{
//    [UpdateInGroup(typeof(SimulationSystemGroup))]
//    [UpdateAfter(typeof(RouteSystem))]
//    public partial struct IntegrationFieldSystem : ISystem
//    {
//        public void OnCreate(ref SystemState state) => state.RequireForUpdate<NavGraphData>();

//        public void OnUpdate(ref SystemState state)
//        {
//            var bridge = FlowFieldBridge.Instance;
//            if (bridge == null || !bridge.ActiveRegionsLookup.IsCreated) return;

//            var navGraph = SystemAPI.GetSingleton<NavGraphData>();

//            var job = new ParallelEikonalInundationJob
//            {
//                NavGraph = navGraph,
//                ActiveRegionsLookup = bridge.ActiveRegionsLookup,
//                IntegrationFieldsLookup = SystemAPI.GetBufferLookup<IntegrationFieldBuffer>(false),
//                RegionRouteConfigLookup = SystemAPI.GetComponentLookup<RegionRouteConfig>(true)
//            };

//            state.Dependency = job.Schedule(state.Dependency);
//        }
//    }

//    public struct NeighborDataBurst
//    {
//        public float3 Pos;
//        public float T;
//        public float Cost;
//    }

//    [BurstCompile]
//    public partial struct ParallelEikonalInundationJob : IJobEntity
//    {
//        [ReadOnly] public NavGraphData NavGraph;
//        [ReadOnly] public NativeParallelHashMap<int2, Entity> ActiveRegionsLookup;

//        public BufferLookup<IntegrationFieldBuffer> IntegrationFieldsLookup;
//        [ReadOnly] public ComponentLookup<RegionRouteConfig> RegionRouteConfigLookup;

//        public void Execute(ref RouteComponent route)
//        {
//            if (!route.IsDirty) return;

//            var allowedRegions = new NativeHashSet<int>(16, Allocator.Temp);
//            var pq = new NativeList<int>(1024, Allocator.Temp);

//            for (int r = 0; r < NavGraph.RegionCount; r++)
//            {
//                int2 key = new int2(route.RouteIndex, r);
//                if (ActiveRegionsLookup.TryGetValue(key, out Entity regEntity))
//                {
//                    allowedRegions.Add(r);

//                    var buffer = IntegrationFieldsLookup[regEntity];
//                    for (int i = 0; i < buffer.Length; i++)
//                    {
//                        if (buffer[i].Value < float.MaxValue)
//                        {
//                            int2 localKey = new int2(i, r);
//                            if (NavGraph.LocalToGlobalMap.TryGetValue(localKey, out int gNode))
//                            {
//                                PushHeap(ref pq, gNode, ref route);
//                            }
//                        }
//                    }
//                }
//            }

//            while (pq.Length > 0)
//            {
//                int currGlobal = PopHeap(ref pq, ref route);

//                int2 offsetData = NavGraph.NodeNeighborsOffsets[currGlobal];
//                for (int i = 0; i < offsetData.y; i++)
//                {
//                    int neighborGlobal = NavGraph.NeighborsBuffer[offsetData.x + i];
//                    int nRegion = NavGraph.NodeRegionIds[neighborGlobal];

//                    if (!allowedRegions.Contains(nRegion) || !NavGraph.IsWalkableFlags[neighborGlobal]) continue;

//                    var acceptedNeighbors = new NativeList<NeighborDataBurst>(8, Allocator.Temp);
//                    int2 nOffsetData = NavGraph.NodeNeighborsOffsets[neighborGlobal];

//                    for (int j = 0; j < nOffsetData.y; j++)
//                    {
//                        int nOfN = NavGraph.NeighborsBuffer[nOffsetData.x + j];
//                        int nnRegion = NavGraph.NodeRegionIds[nOfN];

//                        if (!allowedRegions.Contains(nnRegion) || !NavGraph.IsWalkableFlags[nOfN]) continue;

//                        int2 nnKey = new int2(route.RouteIndex, nnRegion);
//                        if (ActiveRegionsLookup.TryGetValue(nnKey, out Entity nnEntity))
//                        {
//                            int nnLocal = NavGraph.GlobalToLocalMap[nOfN];
//                            float val = IntegrationFieldsLookup[nnEntity][nnLocal].Value;

//                            if (val < float.MaxValue)
//                            {
//                                acceptedNeighbors.Add(new NeighborDataBurst { Pos = NavGraph.NodePositions[nOfN], T = val, Cost = NavGraph.NodeCosts[nOfN] });
//                            }
//                        }
//                    }

//                    if (acceptedNeighbors.Length == 0) { acceptedNeighbors.Dispose(); continue; }

//                    float nodeCost = NavGraph.NodeCosts[neighborGlobal];
//                    float3 targetPos = NavGraph.NodePositions[neighborGlobal];
//                    float newDist = CalculateEikonalCostBurst(targetPos, ref acceptedNeighbors, nodeCost);
//                    acceptedNeighbors.Dispose();

//                    int2 nKey = new int2(route.RouteIndex, nRegion);
//                    if (ActiveRegionsLookup.TryGetValue(nKey, out Entity nEntity))
//                    {
//                        var nBuffer = IntegrationFieldsLookup[nEntity];
//                        int nLocal = NavGraph.GlobalToLocalMap[neighborGlobal];

//                        if (newDist < nBuffer[nLocal].Value)
//                        {
//                            nBuffer[nLocal] = newDist;
//                            PushHeap(ref pq, neighborGlobal, ref route);
//                        }
//                    }
//                }
//            }

//            // 3. Fase 4: Persistencia Filtrada (Limpieza de fronteras que no sean target)
//            using (var e = allowedRegions.GetEnumerator())
//            {
//                int targetRegion = NavGraph.NodeRegionIds[route.TargetNodeGlobal];
//                while (e.MoveNext())
//                {
//                    int rId = e.Current;
//                    int2 key = new int2(route.RouteIndex, rId);
//                    if (ActiveRegionsLookup.TryGetValue(key, out Entity regEntity))
//                    {
//                        var config = RegionRouteConfigLookup[regEntity];
//                        if (!config.IsInsideWindow && rId != targetRegion)
//                        {
//                            var buffer = IntegrationFieldsLookup[regEntity];
//                            for (int i = 0; i < buffer.Length; i++) buffer[i] = float.MaxValue;
//                        }
//                    }
//                }
//            }

//            allowedRegions.Dispose();
//            pq.Dispose();
//            route.IsDirty = false;
//        }

//        // --- MATEMÁTICA EIKONAL (KIMMEL) Y OPERACIONES HEAP ---
//        private float CalculateEikonalCostBurst(float3 targetPos, ref NativeList<NeighborDataBurst> neighbors, float localCost)
//        {
//            for (int i = 1; i < neighbors.Length; i++)
//            {
//                var key = neighbors[i]; int j = i - 1;
//                while (j >= 0 && neighbors[j].T > key.T) { neighbors[j + 1] = neighbors[j]; j--; }
//                neighbors[j + 1] = key;
//            }
//            if (neighbors.Length >= 3)
//            {
//                float t = SolveQuadraticNDBurst(targetPos, ref neighbors, 3, localCost);
//                if (!float.IsNaN(t) && t > neighbors[0].T && t > neighbors[1].T && t > neighbors[2].T) return t;
//            }
//            if (neighbors.Length >= 2)
//            {
//                float t = SolveQuadraticNDBurst(targetPos, ref neighbors, 2, localCost);
//                if (!float.IsNaN(t) && t > neighbors[0].T && t > neighbors[1].T) return t;
//            }
//            return neighbors[0].T + (math.distance(targetPos, neighbors[0].Pos) * localCost);
//        }

//        private float SolveQuadraticNDBurst(float3 pC, ref NativeList<NeighborDataBurst> pts, int count, float f)
//        {
//            float a = 0, b = 0, c = -f * f;
//            for (int i = 0; i < count; i++)
//            {
//                float3 v = pts[i].Pos - pC; float d = math.max(0.001f, math.length(v));
//                float dSq = d * d; a += 1f / dSq; b -= 2f * pts[i].T / dSq; c += (pts[i].T * pts[i].T) / dSq;
//            }
//            float disc = (b * b) - (4f * a * c);
//            if (disc < 0) return float.NaN;
//            return (-b + math.sqrt(disc)) / (2f * a);
//        }

//        private void PushHeap(ref NativeList<int> heap, int globalNode, ref RouteComponent route)
//        {
//            heap.Add(globalNode); int i = heap.Length - 1;
//            while (i > 0)
//            {
//                int p = (i - 1) / 2;
//                if (GetNodeCost(heap[i], ref route) >= GetNodeCost(heap[p], ref route)) break;
//                int temp = heap[i]; heap[i] = heap[p]; heap[p] = temp; i = p;
//            }
//        }

//        private int PopHeap(ref NativeList<int> heap, ref RouteComponent route)
//        {
//            int top = heap[0]; heap[0] = heap[heap.Length - 1]; heap.RemoveAt(heap.Length - 1); int i = 0;
//            while (i * 2 + 1 < heap.Length)
//            {
//                int left = i * 2 + 1; int right = left + 1; int target = left;
//                if (right < heap.Length && GetNodeCost(heap[right], ref route) < GetNodeCost(heap[left], ref route)) target = right;
//                if (GetNodeCost(heap[i], ref route) <= GetNodeCost(heap[target], ref route)) break;
//                int temp = heap[i]; heap[i] = heap[target]; heap[target] = temp; i = target;
//            }
//            return top;
//        }

//        private float GetNodeCost(int globalNode, ref RouteComponent route)
//        {
//            int rId = NavGraph.NodeRegionIds[globalNode];
//            int2 key = new int2(route.RouteIndex, rId);
//            if (ActiveRegionsLookup.TryGetValue(key, out Entity entity))
//            {
//                int local = NavGraph.GlobalToLocalMap[globalNode];
//                return IntegrationFieldsLookup[entity][local].Value;
//            }
//            return float.MaxValue;
//        }
//    }
//}