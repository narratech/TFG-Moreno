using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;

[BurstCompile]
public partial struct MovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        FlowFieldGrid grid = FlowFieldGrid.Instance;
        NativeArray<FlowFieldCellData> nativeCells = grid.GetNativeCells(Allocator.TempJob);

        var job = new MovementJob
        {
            cellSize = grid.cellSize,
            width = grid.width,
            deltaTime = SystemAPI.Time.DeltaTime,
            cells = nativeCells
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);

        nativeCells.Dispose(state.Dependency);
    }

    [BurstCompile]
    public partial struct MovementJob : IJobEntity
    {
        [ReadOnly] public NativeArray<FlowFieldCellData> cells;

        public float cellSize;
        public int width;
        public float deltaTime;

        public void Execute(ref LocalTransform transform,
                            ref PhysicsVelocity velocity,
                            in MoveSpeed speed)
        {
            int x = (int)(transform.Position.x / cellSize);
            int y = (int)(transform.Position.z / cellSize);
            int index = x + y * width;

            // (Opcional pero recomendable)
            if (x < 0 || y < 0 || index >= cells.Length)
                return;

            var cell = cells[index];

            if (cell.direction.x != 0 || cell.direction.y != 0)
            {
                float3 dir = new float3(cell.direction.x, 0, cell.direction.y);
                dir = math.normalize(dir);

                // En vez de mover posición directamente, asignamos velocidad
                velocity.Linear = dir * speed.Value;
            }
            else
            {
                // Si no hay dirección, paramos
                velocity.Linear = float3.zero;
            }
        }
    }
}