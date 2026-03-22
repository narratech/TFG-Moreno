using UnityEngine;
using UnityEngine.AI;

public class FlowFieldAgent : MonoBehaviour
{
    public FlowFieldGrid grid;

    private NavMeshAgent navMeshAgent;

    private Vector3 offset;
    private Vector3 observedOffest;
    private int maxSteps;
    private int acumalteSteps;

    private FlowFieldCell lookingCell;

    private void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
        {
            Debug.LogError("NavMeshAgent component not found on FlowFieldAgent");
        }

        navMeshAgent.updatePosition = true;
        navMeshAgent.updateRotation = false; // tú controlas la rotación
        navMeshAgent.acceleration = 50f;     // importante para suavidad
        navMeshAgent.autoBraking = false;
    }

    void Update()
    {
        if (grid == null) return;

        ManageMovement();

    }

    private void ManageMovement()
    {
        // --- POSICIÓN ACTUAL ---
        Vector3 worldPos = transform.position + observedOffest;

        FlowFieldCell cell = grid.GetCellAtWorldPos(worldPos);

        if (cell == null) return;

        if (lookingCell == null || cell != lookingCell)
        {
            lookingCell = cell;
            observedOffest = Vector3.zero;

            float distances = grid.cellSize * 0.5f;
            Vector3 dir = offset.normalized;
            int steps = 0;
            
            while (offset.magnitude > distances * steps && steps < acumalteSteps)
            {
                observedOffest += dir * distances;
                steps++;
                FlowFieldCell checkCell = grid.GetCellAtWorldPos(worldPos + observedOffest);
                if (checkCell == null || checkCell.isObstacle)
                {
                    // Si encontramos un obstáculo, retrocedemos un paso y salimos
                    observedOffest -= dir * distances;
                    break;
                }
            }

            if (steps >= acumalteSteps)
            {
                acumalteSteps++;
                if (acumalteSteps > maxSteps)
                {
                    acumalteSteps = maxSteps;
                }
            }
            else
            {
                acumalteSteps = steps;
            }

            worldPos = transform.position + observedOffest;

        }

        Vector3 desiredDir = new Vector3(lookingCell.direction.x, 0, lookingCell.direction.y);
        if (grid.IsDestination(lookingCell))
        {
            // Si llegamos a la celda destino, ir hacia el centro de la celda
            desiredDir = (grid.DestinationWorldCentre() - worldPos);
        }

        if (desiredDir.sqrMagnitude < 0.2f)
        {
            // Sin dirección deseada, detenerse suavemente
            navMeshAgent.velocity = Vector3.Lerp(
                navMeshAgent.velocity,
                Vector3.zero,
                Time.deltaTime * navMeshAgent.acceleration
            );
            return;
        }

        desiredDir.Normalize();

        // --- VELOCIDAD ACTUAL ---
        Vector3 currentVelocity = navMeshAgent.velocity;
        Vector3 currentDir = currentVelocity.sqrMagnitude > 0.01f
            ? currentVelocity.normalized
            : transform.forward;

        // --- VELOCIDAD DESEADA ---
        Vector3 desiredVelocity = desiredDir * navMeshAgent.speed;

        // --- ÁNGULO ENTRE VELOCIDADES ---
        float angle = Vector3.Angle(currentDir, desiredDir); // 0..180

        float rotationFactor = Mathf.InverseLerp(0f, 180f, angle);

        // --- ROTACIÓN DINÁMICA ---
        float dynamicAngularSpeed = Mathf.Lerp(
            0, // mínimo
            navMeshAgent.angularSpeed,    // máximo
            rotationFactor
        );

        Quaternion targetRotation = Quaternion.LookRotation(desiredDir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            dynamicAngularSpeed * Time.deltaTime
        );

        // --- ALIGNMENT PARA AVANCE ---
        float alignment = Mathf.Clamp01(Vector3.Dot(transform.forward, desiredDir));

        // --- AVANCE SOLO HACIA ADELANTE ---
        Vector3 forwardVelocity = transform.forward * navMeshAgent.speed * alignment;

        // --- SUAVIZADO DE VELOCIDAD (INERCIA) ---
        navMeshAgent.velocity = Vector3.Lerp(
            navMeshAgent.velocity,
            forwardVelocity,
            Time.deltaTime * navMeshAgent.acceleration
        );
    }

    public void setOffest(Vector3 vec)
    {
        offset = vec;
        observedOffest = vec;
        maxSteps = Mathf.CeilToInt(vec.magnitude / (grid.cellSize * 0.5f));
        acumalteSteps = maxSteps;
    }

}
