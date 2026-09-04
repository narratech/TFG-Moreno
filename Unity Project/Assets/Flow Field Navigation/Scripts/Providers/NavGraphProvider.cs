using UnityEngine;

[DefaultExecutionOrder(-200)]
public abstract class NavGraphProvider : MonoBehaviour
{
    [Header("Obstacles Base")]
    [SerializeField] protected LayerMask _obstacleMask;

    // Referencia polimórfica al grafo (todos tus grafos implementan INavGraph)
    public INavGraph Graph { get; protected set; }

    protected virtual void Awake()
    {
        InitializeGraph();
    }

    public void InitializeGraph()
    {
        if (Graph != null) return;

        Graph = CreateGraph();
        ScanObstacles();
        RegisterToFlowFieldManager();
    }

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
}