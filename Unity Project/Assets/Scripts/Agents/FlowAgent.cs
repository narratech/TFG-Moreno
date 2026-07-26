using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class FlowFieldAgent : MonoBehaviour
{
    [SerializeField] public MonoBehaviour provider;
    [SerializeField] public float MaxSpeed = 5f;
    [SerializeField] public float MaxForce = 20f;
    [Range(0, 1)][SerializeField] private float FrictionForce = 0.1f;

    private IAgentSteering[] _steerings;

    public INavGraph Graph { get; private set; }

    public int TargetNode { get; private set; } = -1;

    public int CurrentNode { get; private set; }

    public int CurrentRegion { get; private set; }

    public Vector3 Velocity { get; private set; } = Vector3.zero;

    public Vector3 SteeringForce { get; private set; } = Vector3.zero;

    [SerializeField] private float MinForce = 0.1f;
    [SerializeField] private float MinSpeed = 0.1f;

    private void Awake()
    {
        AssignGraph();
        _steerings = GetComponents<IAgentSteering>();
    }

    private void Start()
    {
        if (Graph == null)
        {
            AssignGraph();
        }
        FlowFieldAgentManager.Instance?.Subscribe(this);
    }

    private void AssignGraph()
    {
        switch (provider)
        {
            case Grid2DProvider g:
                Graph = g.Graph;
                break;
            case Grid3DProvider g:
                Graph = g.Graph;
                break;
            case QuadSphereProvider g:
                Graph = g.Graph;
                break;
        }
    }

    private void OnDestroy()
    {
        if (FlowFieldAgentManager.Instance != null)
            FlowFieldAgentManager.Instance.Unsubscribe(this);
    }

    private void Update()
    {
        if (Graph == null)
            return;

        // 1. Obtener estado seguro inicial
        CurrentNode = Graph.GetClosestNode(transform.position);
        CurrentRegion = Graph.GetRegionId(CurrentNode);

        // 2. Calcular steering
        SteeringForce = ComputeSteering();

        // 3. Aplicar físicas
        if (SteeringForce.sqrMagnitude > MinForce * MinForce)
        {
            Velocity += SteeringForce * Time.deltaTime;
            Velocity = Vector3.ClampMagnitude(Velocity, MaxSpeed);
        }
        else
        {
            Velocity = Vector3.MoveTowards(
                Velocity,
                Vector3.zero,
                MaxSpeed * FrictionForce * Time.deltaTime);
        }

        if (Velocity.sqrMagnitude < MinSpeed * MinSpeed)
            Velocity = Vector3.zero;

        // 4. Intentar movimiento con deslizamiento corregido
        TryMove();

        // 5. Ajustar restricciones geométricas (Grafo / Malla)
        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;
        Vector3 velocity = Velocity;

        Graph.ConstrainPositionAndRotation(
            ref position,
            ref velocity,
            ref rotation);

        transform.SetPositionAndRotation(position, rotation);
        Velocity = velocity;

        // 6. Estado final sincronizado
        CurrentNode = Graph.GetClosestNode(transform.position);
        CurrentRegion = Graph.GetRegionId(CurrentNode);
    }

    private void TryMove()
    {
        if (Velocity.sqrMagnitude < 0.0001f)
            return;

        Vector3 currentPosition = transform.position;
        Vector3 desiredPosition = currentPosition + Velocity * Time.deltaTime;

        int targetNode = Graph.GetClosestNode(desiredPosition);

        // CASO 1: Destino libre y caminable
        if (Graph.IsWalkable(targetNode))
        {
            transform.position = desiredPosition;
            return;
        }

        // CASO 2: Intentar deslizamiento agnóstico
        Vector3 obstaclePos = Graph.GetNodePosition(targetNode);
        Vector3 safeAgentPos = Graph.GetNodePosition(CurrentNode);
        Vector3 surfaceNormal = Graph.GetNodeNormal(CurrentNode);

        // Dirección desde el obstáculo hacia el agente
        Vector3 toAgent = safeAgentPos - obstaclePos;

        // Si coincide el centro del nodo obstáculo y el actual, usamos la velocidad inversa
        if (toAgent.sqrMagnitude < 0.0001f)
            toAgent = -Velocity;

        // Proyectamos la dirección de choque sobre la superficie local (QuadSphere/Plano)
        // para obtener una normal de pared tangente a la superficie
        Vector3 wallNormal = Vector3.ProjectOnPlane(toAgent, surfaceNormal).normalized;

        if (wallNormal.sqrMagnitude < 0.0001f)
            wallNormal = -Velocity.normalized;

        // Deslizar la velocidad sobre la normal de la pared
        Vector3 slideVelocity = Vector3.ProjectOnPlane(Velocity, wallNormal);

        if (slideVelocity.sqrMagnitude > MinSpeed * MinSpeed)
        {
            Vector3 slidePosition = currentPosition + slideVelocity * Time.deltaTime;
            int slideNode = Graph.GetClosestNode(slidePosition);

            if (Graph.IsWalkable(slideNode))
            {
                Velocity = slideVelocity;
                transform.position = slidePosition;
                return;
            }
        }

        // CASO 3: Si no se puede deslizar, NO reseteamos Velocity a Vector3.zero.
        // Simplemente no actualizamos transform.position este frame.
        // Esto permite que el steering mantenga su inercia/fuerza y gire libremente en el siguiente frame.
    }

    private Vector3 ComputeSteering()
    {
        Vector3 force = Vector3.zero;

        foreach (IAgentSteering steering in _steerings)
        {
            if (steering == null || !steering.enabled)
                continue;

            Vector3 dir = steering.GetDirection(this);
            if (dir.sqrMagnitude > MinForce * MinForce)
            {
                force += dir * steering.Weight;
            }
        }

        return Vector3.ClampMagnitude(force, MaxForce);
    }

    public void SetDestination(int targetNode)
    {
        if (TargetNode == targetNode)
            return;

        TargetNode = targetNode;
        FlowFieldAgentManager.Instance?.Subscribe(this);
    }
}