using UnityEngine;

[RequireComponent(typeof(NavAgent))]
public class NavAgentDebugger : MonoBehaviour
{
    [Header("Gizmos Toggle")]
    [SerializeField] private bool _drawGizmos = true;
    [SerializeField] private bool _drawVectors = true;
    [SerializeField] private bool _drawNodes = true;

    [Header("Vector Settings")]
    [SerializeField] private float _vectorScale = 1.0f;
    [SerializeField] private Color _velocityColor = Color.green;
    [SerializeField] private Color _steeringColor = Color.cyan;

    [Header("Node Settings")]
    [SerializeField] private Color _currentNodeColor = Color.yellow;
    [SerializeField] private Color _targetNodeColor = Color.magenta;
    [SerializeField] private float _nodeRadius = 0.2f;

    private NavAgent _agent;

    private void Awake()
    {
        _agent = GetComponent<NavAgent>();
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos) return;
        if (_agent == null) _agent = GetComponent<NavAgent>();
        if (_agent == null) return;

        Vector3 pos = transform.position;

        // 1. Dibujar Vectores (Fuerza y Velocidad)
        if (_drawVectors)
        {
            if (_agent.Velocity.sqrMagnitude > 0.001f)
            {
                Gizmos.color = _velocityColor;
                Gizmos.DrawRay(pos, _agent.Velocity * _vectorScale);
            }

            if (_agent.SteeringForce.sqrMagnitude > 0.001f)
            {
                Gizmos.color = _steeringColor;
                Gizmos.DrawRay(pos, _agent.SteeringForce * _vectorScale);
            }
        }

        // 2. Dibujar Nodos del Grafo (Actual y Target)
        if (_drawNodes && _agent.Graph != null)
        {
            if (_agent.CurrentNode >= 0)
            {
                Gizmos.color = _currentNodeColor;
                Vector3 currentPos = _agent.Graph.GetNodePosition(_agent.CurrentNode);
                Gizmos.DrawWireSphere(currentPos, _nodeRadius);
                Gizmos.DrawLine(pos, currentPos);
            }

            if (_agent.TargetNode >= 0)
            {
                Gizmos.color = _targetNodeColor;
                Vector3 targetPos = _agent.Graph.GetNodePosition(_agent.TargetNode);
                Gizmos.DrawSphere(targetPos, _nodeRadius);
            }
        }
    }
}