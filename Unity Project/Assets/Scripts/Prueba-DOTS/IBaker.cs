using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public class AgentBaker : Baker<AgentAuthoring>
{
    public override void Bake(AgentAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);

        UnityEngine.Debug.Log($"Baking entity: {entity}");

        AddComponent(entity, new MoveSpeed
        {
            Value = authoring.speed 
        });
    }
}