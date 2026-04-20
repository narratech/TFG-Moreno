using UnityEngine;

public class Grid2DProvider : MonoBehaviour
{
    [Header("Configuración del Grid")]
    [SerializeField] private int _width = 50;
    [SerializeField] private int _height = 50;
    [SerializeField] private int _regionWidth = 5;
    [SerializeField] private int _regionHeight = 5;
    [SerializeField] private float _cellSize = 1.0f;
    [SerializeField] private LayerMask _obstacleMask;

    // El grafo real (lógica pura)
    public Grid2DNavGraph Graph { get; private set; }

    private void Awake()
    {
        InitializeGraph();
    }

    public void InitializeGraph()
    {
        if (Graph != null) return;

        // Creamos la instancia de la lógica que ya tenemos
        Graph = new Grid2DNavGraph(_width, _height, _cellSize, _regionWidth, _regionHeight, transform.position);

        // Escaneamos el mundo para marcar obstáculos
        ScanObstacles();
    }

    private void ScanObstacles()
    {
        for (int i = 0; i < Graph.NodeCount; i++)
        {
            Vector3 worldPos = Graph.GetNodePosition(i);

            // Usamos OverlapSphere o CheckSphere para ver si hay algo en la máscara de obstáculos
            // El radio es ligeramente menor a la mitad de la celda para evitar falsos positivos
            bool isObstacle = Physics.CheckSphere(worldPos, _cellSize * 0.45f, _obstacleMask);

            if (isObstacle)
            {
                Graph.SetWalkable(i, false);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Dibujo previo si el juego no está ejecutándose
        Gizmos.color = Color.white;
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                Vector3 pos = transform.position + new Vector3(x * _cellSize, 0, y * _cellSize);
                Gizmos.DrawWireCube(pos, new Vector3(_cellSize, 0.1f, _cellSize));
            }
        }

        // Dibujamos las regiones
        Gizmos.color = Color.blue;
        for (int x = 0; x < _width; x += _regionWidth)
        {
            for (int y = 0; y < _height; y += _regionHeight)
            {
                Vector3 pos = transform.position + new Vector3(x * _cellSize, 0, y * _cellSize);
                Vector3 size = new Vector3(_regionWidth * _cellSize, 0.1f, _regionHeight * _cellSize);
                Gizmos.DrawWireCube(pos + size / 2 - new Vector3(_cellSize / 2, 0, _cellSize / 2), size);
            }
        }

        if (Graph == null) return;
        // Dibujo dinámico cuando el juego corre
        for (int i = 0; i < Graph.NodeCount; i++)
        {
            Gizmos.color = Graph.IsWalkable(i) ? Color.cyan * 0.3f : Color.red * 0.5f;
            Gizmos.DrawCube(Graph.GetNodePosition(i), new Vector3(_cellSize * 0.9f, 0.1f, _cellSize * 0.9f));
        }
    }
}