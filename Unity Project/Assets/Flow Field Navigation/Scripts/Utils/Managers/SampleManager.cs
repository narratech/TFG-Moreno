using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class SampleManager : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private RectTransform selectionBox;

    [Header("Configuración del Grafo")]
    public NavGraphProvider graphProvider;
    public int targetNode = -1;

    [Header("Configuración de Formaciones")]
    [SerializeField] private FormationType formationType;
    [SerializeField] private float formationSpacing = 10f;
    [SerializeField] private Texture2D shapeTexture = null;

    private InputManager input;
    private Camera mainCamera;
    private readonly List<Selectable> selectedUnits = new List<Selectable>();

    public static SampleManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        input = InputManager.Instance;
        mainCamera = Camera.main;

        if (selectionBox != null)
            selectionBox.gameObject.SetActive(false);
    }

    private void Update()
    {
        HandleSelectionInput();

        if (input.IsCommanding)
        {
            HandleCommand();
        }
    }

    // --- LÓGICA DE SELECCIÓN ---

    private void HandleSelectionInput()
    {
        if (Mouse.current == null) return;

        // 1. Si el jugador está arrastrando la caja de selección
        if (input.IsDragging)
        {
            UpdateSelectionBoxUI();

            // Si suelta el clic MIENTRAS estaba arrastrando, procesamos la selección
            // (Sin importar si el ratón terminó encima de un elemento de la UI o no)
            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                EndSelection();
            }
        }

        // 2. Si NO está arrastrando y el ratón está sobre la UI, ignoramos clics nuevos
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // 3. Si no hay UI de por medio y se suelta el clic (clic rápido / simple)
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            EndSelection();
        }
    }

private void EndSelection()
{
    if (selectionBox != null)
        selectionBox.gameObject.SetActive(false);

    SelectUnitsInBox();
}

    private void UpdateSelectionBoxUI()
    {
        if (selectionBox == null) return;

        Vector2 startPos = input.DragStartPosition;
        Vector2 currentPos = input.MouseScreenPosition;

        // Umbral mínimo de movimiento para mostrar la caja
        if ((currentPos - startPos).sqrMagnitude < 50f)
            return;

        if (!selectionBox.gameObject.activeSelf)
            selectionBox.gameObject.SetActive(true);

        Vector2 min = Vector2.Min(startPos, currentPos);
        Vector2 max = Vector2.Max(startPos, currentPos);

        selectionBox.position = min;
        selectionBox.sizeDelta = max - min;
    }

    private void SelectUnitsInBox()
    {
        Vector2 startPos = input.DragStartPosition;
        Vector2 endPos = input.MouseScreenPosition;

        // Si fue un click rápido (sin arrastre relevante), hacemos selección por Raycast
        if ((endPos - startPos).sqrMagnitude < 100f)
        {
            HandleSingleClickSelection();
            return;
        }

        if (!input.IsMultiSelect)
        {
            DeselectAll();
        }

        Rect selectionRect = GetScreenRect(startPos, endPos);
        Selectable[] allSelectables = Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None);

        foreach (Selectable unit in allSelectables)
        {
            Vector3 screenPos = mainCamera.WorldToScreenPoint(unit.transform.position);

            // Verificar si la unidad está en pantalla y dentro del rectángulo
            if (screenPos.z > 0 && selectionRect.Contains(screenPos))
            {
                SelectUnit(unit);
            }
        }
    }

    private void HandleSingleClickSelection()
    {
        Ray ray = mainCamera.ScreenPointToRay(input.MouseScreenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            Selectable selectable = hit.collider.GetComponentInParent<Selectable>();

            if (!input.IsMultiSelect) DeselectAll();

            if (selectable != null)
            {
                SelectUnit(selectable);
            }
        }
        else if (!input.IsMultiSelect)
        {
            DeselectAll();
        }
    }

    // --- LÓGICA DE NAVEGACIÓN Y COMANDOS ---

    private void HandleCommand()
    {
        //if (selectedUnits.Count == 0) return; // <---------------------------------IMPORTANTE DESCOMENTAR

        Ray ray = mainCamera.ScreenPointToRay(input.MouseScreenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            OnClickGround(hit.point);
        }
    }

    private void OnClickGround(Vector3 pos)
    {
        int destination = graphProvider.Graph.GetClosestNode(pos);
        if (destination == -1 || !graphProvider.Graph.IsWalkable(destination))
        {
            targetNode = -1;
            return;
        }

        if (!FlowFieldManager.Instance.TryGetRoute(graphProvider.Graph, destination))
        {
            FlowFieldManager.Instance.RegisterRoute(graphProvider.Graph, destination);
        }
        targetNode = destination;

        NavAgent[] activeAgents = new NavAgent[selectedUnits.Count];

        for (int i = 0; i < selectedUnits.Count; i++)
        {
            NavAgent agent = selectedUnits[i].Agent;
            if (agent != null)
            {
                agent.SetDestination(targetNode);
                activeAgents[i] = agent;
            }
        }
        Vector3 position = graphProvider.Graph.GetNodePosition(destination);

        FormationGenerator.GenerateAndApply(formationType, position, formationSpacing, activeAgents, shapeTexture, graphProvider.Graph);

        ProcessECSAgents(targetNode);
    }

    public void ProcessECSAgents(int destinationNode)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<AgentComponent>());

        using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

        foreach (var entity in entities)
        {
            AgentComponent agent = entityManager.GetComponentData<AgentComponent>(entity);

            agent.NextRouteId = destinationNode;
            entityManager.SetComponentData(entity, agent);
        }

        Vector3 position = graphProvider.Graph.GetNodePosition(destinationNode);

        FormationGenerator.GenerateAndApply(formationType, position, formationSpacing, query, entityManager, shapeTexture, graphProvider.Graph);
    }

    // --- GESTIÓN DE SELECCIÓN DE UNIDADES ---

    public void SelectUnit(Selectable unit)
    {
        if (!selectedUnits.Contains(unit))
        {
            selectedUnits.Add(unit);
            unit.SetSelected(true);
        }
    }

    public void DeselectAll()
    {
        for (int i = 0; i < selectedUnits.Count; i++)
        {
            if (selectedUnits[i] != null)
                selectedUnits[i].SetSelected(false);
        }
        selectedUnits.Clear();
    }

    private Rect GetScreenRect(Vector2 screenPos1, Vector2 screenPos2)
    {
        Vector2 min = Vector2.Min(screenPos1, screenPos2);
        Vector2 max = Vector2.Max(screenPos1, screenPos2);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}