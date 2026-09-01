using UnityEngine;

/// <summary>
/// Agente de navegación que gestiona el movimiento, dinámica angular y restricciones sobre el grafo.
/// </summary>
public class NavAgent : MonoBehaviour
{
    [Header("Provider")]
    [SerializeField] private NavGraphProvider _provider;

    [Header("Movement Settings")]
    [SerializeField] public float MaxSpeed = 5f;
    [SerializeField] public float MaxForce = 20f;

    [Header("Angular Dynamics (Giro Físico)")]
    [Tooltip("Velocidad angular máxima en grados/segundo")]
    [SerializeField] public float MaxAngularSpeed = 240f;

    [Tooltip("Frecuencia de respuesta del giro. Mayor número = gira más rápido.")]
    [SerializeField] public float AngularFrequency = 12f;

    [Tooltip("Amortiguación del giro. 1 = amortiguación crítica.")]
    [SerializeField] public float AngularDamping = 1.0f;

    [Tooltip("Sensibilidad de frenado en giros. Mayor número = reduce más la velocidad.")]
    [Range(1f, 4f)]
    [SerializeField] public float TurnTightness = 2f;

    private IAgentSteering[] _steerings;
    private float _currentAngularSpeed = 0f;

    /// <summary> Grafo de navegación asignado al agente. </summary>
    public INavGraph Graph { get; private set; }

    /// <summary> Nodo de destino actual. </summary>
    public int TargetNode { get; private set; } = -1;

    /// <summary> Nodo en el que se encuentra el agente. </summary>
    public int CurrentNode { get; private set; }

    /// <summary> Región actual del grafo. </summary>
    public int CurrentRegion { get; private set; }

    /// <summary> Velocidad lineal actual. </summary>
    public Vector3 Velocity { get; private set; } = Vector3.zero;

    /// <summary> Fuerza total resultante de los steerings. </summary>
    public Vector3 SteeringForce { get; private set; } = Vector3.zero;

    private void Awake()
    {
        if (Graph == null && _provider != null) AssignGraph(_provider.Graph);
        _steerings = GetComponents<IAgentSteering>();
    }

    private void Start()
    {
        if (Graph == null && _provider != null) AssignGraph(_provider.Graph);
        if (TargetNode >= 0) AgentManager.Instance?.Subscribe(this);
    }

    /// <summary> Asigna el grafo de navegación. </summary>
    public void AssignGraph(INavGraph graph) => Graph = graph;

    private void OnDestroy()
    {
        if (AgentManager.Instance != null) AgentManager.Instance.Unsubscribe(this);
    }

    private void Update()
    {
        if (Graph == null) return;

        CurrentNode = Graph.GetClosestNode(transform.position);
        CurrentRegion = Graph.GetRegionId(CurrentNode);

        SteeringForce = ComputeSteering();

        Vector3 surfaceNormal = Graph.GetNodeNormal(CurrentNode);
        if (surfaceNormal.sqrMagnitude < 0.0001f) surfaceNormal = Vector3.up;
        else surfaceNormal.Normalize();

        Vector3 desiredDirection = Vector3.ProjectOnPlane(SteeringForce, surfaceNormal);

        if (desiredDirection.sqrMagnitude > 0.0001f)
        {
            desiredDirection.Normalize();

            Vector3 currentForward = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
            if (currentForward.sqrMagnitude < 0.0001f) currentForward = desiredDirection;
            else currentForward.Normalize();

            float angleDifference = Vector3.SignedAngle(currentForward, desiredDirection, surfaceNormal);

            float omega = AngularFrequency;
            float angularAcceleration = omega * omega * angleDifference - 2f * AngularDamping * omega * _currentAngularSpeed;

            _currentAngularSpeed += angularAcceleration * Time.deltaTime;
            _currentAngularSpeed = Mathf.Clamp(_currentAngularSpeed, -MaxAngularSpeed, MaxAngularSpeed);

            transform.Rotate(surfaceNormal, _currentAngularSpeed * Time.deltaTime, Space.World);

            float angle = Mathf.Abs(angleDifference);
            float speedFactor = Mathf.Clamp01(1f - angle / 120f);
            speedFactor = Mathf.Pow(speedFactor, TurnTightness);

            Vector3 movementForward = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
            if (movementForward.sqrMagnitude > 0.0001f) movementForward.Normalize();
            else movementForward = desiredDirection;

            Vector3 targetVelocity = movementForward * (MaxSpeed * speedFactor);
            Velocity = Vector3.MoveTowards(Velocity, targetVelocity, MaxForce * Time.deltaTime);
        }
        else
        {
            Velocity = Vector3.MoveTowards(Velocity, Vector3.zero, MaxForce * Time.deltaTime);
            _currentAngularSpeed = Mathf.MoveTowards(_currentAngularSpeed, 0f, MaxAngularSpeed * Time.deltaTime);
        }

        if (Velocity.sqrMagnitude > 0.0001f) transform.position += Velocity * Time.deltaTime;

        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;
        Vector3 velocity = Velocity;

        Graph.ConstrainPositionAndRotation(ref position, ref velocity, ref rotation);

        transform.SetPositionAndRotation(position, rotation);
        Velocity = velocity;
    }

    /// <summary> Calcula la fuerza total acumulada de todos los comportamientos de steering. </summary>
    private Vector3 ComputeSteering()
    {
        Vector3 force = Vector3.zero;
        foreach (IAgentSteering steering in _steerings)
        {
            if (steering == null || !steering.enabled) continue;
            force += steering.GetForce() * steering.Weight;
        }
        return Vector3.ClampMagnitude(force, MaxForce);
    }

    /// <summary> Establece un nuevo nodo objetivo para el agente. </summary>
    public void SetDestination(int targetNode)
    {
        if (TargetNode == targetNode) return;
        TargetNode = targetNode;
        AgentManager.Instance?.Subscribe(this);
    }

    /// <summary> Asigna el proveedor del grafo de navegación. </summary>
    public void SetProvider(NavGraphProvider provider)
    {
        _provider = provider;
        if (provider != null) AssignGraph(provider.Graph);
    }
}