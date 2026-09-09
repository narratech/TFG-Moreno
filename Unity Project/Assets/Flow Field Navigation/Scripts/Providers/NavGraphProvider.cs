using UnityEngine;

[DefaultExecutionOrder(-200)]
public abstract class NavGraphProvider : MonoBehaviour
{
    [Header("Scan Options")]
    [SerializeField] protected LayerMask _obstacleMask = -1;
    [Range(0, 1)]
    [SerializeField] protected float _scanFactor = 1.0f;

    [Header("Debug Settings")]
    [SerializeField] protected bool _drawWalkability = false;
    [SerializeField] protected bool _drawNodeIndices = false;
    [SerializeField] protected bool _drawCoordinates = false;

    // Referencia polimórfica al grafo (todos tus grafos implementan INavGraph)
    public INavGraph Graph { get; protected set; }

    protected virtual void Awake()
    {
        InitializeGraph();
    }

    public void InitializeGraph()
    {
        if (!ValidateConfiguration())
        {
            Debug.LogWarning($"[{gameObject.name}] Configuración inválida (valores <= 0). Se abortó la creación del grafo.", this);
            return;
        }

        if (Graph != null) return;

        Graph = CreateGraph();
        ScanObstacles();
        RegisterToFlowFieldManager();
    }

    /// <summary>
    /// Valida que las dimensiones numéricas sean aptas (> 0) antes de ejecutar.
    /// </summary>
    protected abstract bool ValidateConfiguration();

    /// <summary>
    /// Instancia la lógica del grafo específico (Grid2DNavGraph, Grid3DNavGraph, QuadSphereNavGraph).
    /// </summary>
    protected abstract INavGraph CreateGraph();

    /// <summary>
    /// Marca los nodos no caminables según los obstáculos detectados en la escena.
    /// </summary>
    protected abstract void ScanObstacles();

    protected virtual void RegisterToFlowFieldManager()
    {
        if (Graph != null && FlowFieldManager.Instance != null)
        {
            FlowFieldManager.Instance.RegisterContext(Graph);
        }
    }

    protected virtual void OnValidate()
    {
        // Método base disponible para extensiones en el editor
    }
}