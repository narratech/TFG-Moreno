using System;
using Unity.Transforms;
using UnityEngine;

public class FlowFieldSteering : IAgentSteering
{
    [Header("Settings")]
    [SerializeField]
    private FlowFieldSteeringSettings settings;
    private float StepSize => settings != null ? settings.StepSize : _stepSize;
    private float TimeStamp => settings != null ? settings.TimeStamp : _timeStamp;

    [Header("Manual Settings")]
    [SerializeField]
    private float _stepSize = 1f;

    [SerializeField]
    private float _stopRadius = 1.0f;

    [SerializeField]
    private float _timeStamp = 0.1f;

    [SerializeField]
    private Vector3 _formationOffset;

    private readonly int[] _nodes = new int[8];

    private float _time = 0f;
    private int _currentSteps = 0;

    public override Vector3 GetForce()
    {
        if (Agent == null || Agent.Graph == null)
            return Vector3.zero;

        bool hasTarget = Agent.TargetNode >= 0;

        // 1. Muestreamos el FlowField o las fuerzas de colisión
        Vector3 desiredOffset = GetRealOffset(_formationOffset);
        Vector3 desiredOffsetDir = desiredOffset.normalized;

        if (hasTarget && Time.time - _time > TimeStamp)
        {
            _currentSteps = UpdateSteps(_currentSteps, desiredOffsetDir);
            _time = Time.time;
        }
        else if (!hasTarget)
        {
            _currentSteps = 0;
        }

        Vector3 samplePosition = Agent.transform.position + desiredOffsetDir * (_currentSteps * StepSize);
        Vector3 flowDirection = SampleFlowField(samplePosition);

        // 2. Si hay destino, verificar radio de parada
        if (hasTarget)
        {
            Vector3 targetPosition = Agent.Graph.GetNodePosition(Agent.TargetNode);
            if (Vector3.SqrMagnitude(samplePosition - targetPosition) < _stopRadius * _stopRadius)
                return -Agent.Velocity;
        }
        else if (flowDirection.sqrMagnitude < 0.0001f)
        {
            // Si no hay objetivo Y tampoco hay colisión/repulsión cercana, no hay fuerza
            return Vector3.zero;
        }

        // 3. Calculamos la velocidad deseada y devolvemos la fuerza de dirección
        Vector3 desiredVelocity = Vector3.ClampMagnitude(flowDirection * 0.5f, Agent.MaxSpeed);
        return desiredVelocity - Agent.Velocity;
    }

    private int UpdateSteps(int currentSteps, Vector3 offsetDir)
    {
        Vector3 desiredOffset = GetRealOffset(_formationOffset);
        float offsetLen = desiredOffset.magnitude;
        float stepSize = StepSize > 0f ? StepSize : 1f;

        if (offsetLen < 0.001f)
        {
            return 0;
        }

        int absoluteMaxSteps = Mathf.CeilToInt(offsetLen / stepSize);

        if (absoluteMaxSteps <= 0)
        {
            return 0;
        }

        Vector3 currentPos = Agent.transform.position;

        // 1. Buscamos hasta qué paso incremental es caminable el suelo (CurrentSteps + 1)
        int targetCheckStep = Mathf.Min(currentSteps + 1, absoluteMaxSteps);
        int maxWalkableStep = 0;

        for (int step = 1; step <= targetCheckStep; step++)
        {
            Vector3 checkPos = currentPos + offsetDir * (step * stepSize);
            int node = Agent.Graph.GetClosestNode(checkPos);

            if (node >= 0 && Agent.Graph.IsWalkable(node))
            {
                maxWalkableStep = step;
            }
            else
            {
                break;
            }
        }

        if (maxWalkableStep == 0)
        {
            return 0;
        }

        // 2. Evaluamos desde el paso caminable más alto hacia abajo la validez del flujo
        for (int step = maxWalkableStep; step >= 1; step--)
        {
            Vector3 samplePos = currentPos + offsetDir * (step * stepSize);
            Vector3 sampleFlow = SampleFlowField(samplePos);

            if (sampleFlow.sqrMagnitude < 0.0001f)
            {
                return step;
            }

            Vector3 flowDir = sampleFlow.normalized;
            bool pathBlocked = false;

            for (int flowStep = 1; flowStep <= step; flowStep++)
            {
                Vector3 agentProjectionPos = currentPos + flowDir * (flowStep * stepSize * 0.5f);
                int projNode = Agent.Graph.GetClosestNode(agentProjectionPos);

                if (projNode < 0 || !Agent.Graph.IsWalkable(projNode))
                {
                    pathBlocked = true;
                    break;
                }
            }

            if (!pathBlocked)
            {
                return step;
            }
        }

        return 0;
    }

    private Vector3 GetRealOffset(Vector3 formationOffset)
    {
        Vector3 offset = formationOffset;

        if (Agent.Graph == null)
            return offset;

        Vector3 normal = Agent.Graph.GetNodeNormal(Agent.CurrentNode);

        if (normal != Vector3.zero)
        {
            float dot = Vector3.Dot(normal, Vector3.up);

            if (dot < 0.9999f)
            {
                Quaternion surfaceRotation = Quaternion.FromToRotation(Vector3.up, normal);
                offset = surfaceRotation * offset;
            }
        }

        return offset;
    }

    private Vector3 SampleFlowField(Vector3 samplePosition)
    {
        int count = Agent.Graph.GetInterpolationNodes(samplePosition, _nodes);

        Vector3 direction = Vector3.zero;
        float totalWeight = 0f;

        int targetNode = Agent.TargetNode;
        FlowFieldManager fieldManager = FlowFieldManager.Instance;

        for (int i = 0; i < count; i++)
        {
            int node = _nodes[i];
            Vector3 nodePosition = Agent.Graph.GetNodePosition(node);

            float dx = samplePosition.x - nodePosition.x;
            float dy = samplePosition.y - nodePosition.y;
            float dz = samplePosition.z - nodePosition.z;
            float sqrDistance = dx * dx + dy * dy + dz * dz;

            float weight = 1f / (sqrDistance + 0.0001f);

            // A) Si el nodo NO es transitable, siempre genera vector de repulsión
            if (!Agent.Graph.IsWalkable(node))
            {
                if (sqrDistance > 0.0001f)
                {
                    float invDist = 1f / Mathf.Sqrt(sqrDistance);
                    Vector3 deltaNorm = new Vector3(dx * invDist, dy * invDist, dz * invDist);
                    direction += deltaNorm * weight;
                    totalWeight += weight;
                }
                continue;
            }

            // B) Si el nodo es caminable pero NO tenemos un objetivo válido, omitimos la búsqueda de FlowField
            if (targetNode < 0)
                continue;

            int region = Agent.Graph.GetRegionId(node);
            FlowField field = fieldManager.GetFlowField(Agent.Graph, region, targetNode);

            if (field == null)
            {
                field = FlowFieldEngine.GenerateFlowPath(Agent.Graph, targetNode, region);
            }

            if (field == null)
                continue;

            int localNode = Agent.Graph.GetLocalNode(node);
            direction += field.FlowDirections[localNode] * weight * field.IntegrationField[localNode];
            totalWeight += weight;
        }

        if (totalWeight > 0f)
            direction /= totalWeight;

        return direction;
    }

    public void SetFormationOffset(Vector3 vector3)
    {
        _formationOffset = vector3;
    }

    private void OnDrawGizmosSelected()
    {
        if (Agent == null || Agent.Graph == null)
            return;

        Vector3 currentPos = Agent.transform.position;
        Vector3 desiredOffset = GetRealOffset(_formationOffset);
        float offsetLen = desiredOffset.magnitude;

        if (offsetLen < 0.001f)
            return;

        Vector3 offsetDir = desiredOffset / offsetLen;
        float stepSize = StepSize > 0f ? StepSize : 1f;
        int absoluteMaxSteps = Mathf.CeilToInt(offsetLen / stepSize);

        Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
        Gizmos.DrawLine(currentPos, currentPos + desiredOffset);

        for (int i = 1; i <= absoluteMaxSteps; i++)
        {
            Vector3 stepPos = currentPos + offsetDir * (i * stepSize);

            if (i <= _currentSteps)
            {
                bool isTargetStep = (i == _currentSteps);
                Gizmos.color = isTargetStep ? Color.green : Color.yellow;
                Gizmos.DrawSphere(stepPos, isTargetStep ? 0.22f : 0.12f);
            }
            else
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
                Gizmos.DrawWireSphere(stepPos, 0.1f);
            }
        }

        if (_currentSteps > 0)
        {
            Vector3 samplePos = currentPos + offsetDir * (_currentSteps * stepSize);
            Vector3 sampleFlow = SampleFlowField(samplePos);

            if (sampleFlow.sqrMagnitude > 0.0001f)
            {
                Vector3 flowDir = sampleFlow.normalized;

                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(samplePos, flowDir * 1.2f);

                Vector3 prevProjPos = currentPos;

                for (int flowStep = 1; flowStep <= _currentSteps; flowStep++)
                {
                    Vector3 agentProjectionPos = currentPos + flowDir * (flowStep * stepSize * 0.5f);
                    int projNode = Agent.Graph.GetClosestNode(agentProjectionPos);

                    bool isStepWalkable = (projNode >= 0 && Agent.Graph.IsWalkable(projNode));

                    Gizmos.color = isStepWalkable ? Color.magenta : Color.red;
                    Gizmos.DrawLine(prevProjPos, agentProjectionPos);
                    Gizmos.DrawSphere(agentProjectionPos, isStepWalkable ? 0.08f : 0.18f);

                    prevProjPos = agentProjectionPos;

                    if (!isStepWalkable)
                        break;
                }
            }
        }
    }
}