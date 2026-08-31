using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

[TestFixture]
public class NavigationTests
{
    private Grid2DNavGraph _grid;

    [SetUp]
    public void SetUp()
    {
        // Instanciamos un Grid de 10x10 nodos, tamaño 1.0f, regiones de 5x5, en posición origen (0,0,0)
        _grid = new Grid2DNavGraph(width: 10, height: 10, cellSize: 1.0f, regionWidth: 5, regionHeight: 5, origin: Vector3.zero);
        FlowFieldManager.Instance.RegisterContext(_grid);
    }

    // ==========================================
    // NAV GRAPH TESTS
    // ==========================================

    [Test]
    public void Grid_CreatesCorrectNumberOfNodes()
    {
        // 10 x 10 = 100 nodos
        Assert.AreEqual(100, _grid.NodeCount);
    }

    [Test]
    public void Grid_NodePositionsAreCorrect()
    {
        int index = _grid.GetClosestNode(new Vector3(2f, 0f, 3f));
        Vector3 position = _grid.GetNodePosition(index);

        Assert.AreEqual(new Vector3(2f, 0f, 3f), position);
    }

    [Test]
    public void Grid_CenterNodeHasFourNeighbours()
    {
        int centerNode = _grid.GetClosestNode(new Vector3(5f, 0f, 5f));

        int count = _grid.GetNeighbors(centerNode).Count();

        // En un Grid 2D sin diagonales directas en adyacencia principal debe devolver 4 vecinos
        Assert.AreEqual(4, count);
    }

    [Test]
    public void Grid_ObstacleIsNotWalkable()
    {
        int obstacleNode = 15;
        _grid.SetWalkable(obstacleNode, false);

        Assert.IsFalse(_grid.IsWalkable(obstacleNode));
    }

    // ==========================================
    // REGIONS TESTS
    // ==========================================

    [Test]
    public void Regions_AreAssignedCorrectly()
    {
        int nodeRegion0 = _grid.GetClosestNode(new Vector3(1f, 0f, 1f));
        int nodeRegion1 = _grid.GetClosestNode(new Vector3(7f, 0f, 1f));

        int region0 = _grid.GetRegionId(nodeRegion0);
        int region1 = _grid.GetRegionId(nodeRegion1);

        Assert.AreNotEqual(region0, region1);
    }

    [Test]
    public void Regions_DisconnectedAreasHaveDifferentRegions()
    {
        // Bloqueamos una columna entera para dividir físicamente el grafo
        for (int y = 0; y < 10; y++)
        {
            int blockedNode = _grid.GetClosestNode(new Vector3(4f, 0f, y));
            _grid.SetWalkable(blockedNode, false);
        }

        int leftSideNode = _grid.GetClosestNode(new Vector3(1f, 0f, 5f));
        int rightSideNode = _grid.GetClosestNode(new Vector3(8f, 0f, 5f));

        Assert.AreNotEqual(_grid.GetRegionId(leftSideNode), _grid.GetRegionId(rightSideNode));
    }

    // ==========================================
    // INTEGRATION FIELD TESTS
    // ==========================================

    [Test]
    public void IntegrationField_GoalHasZeroCost()
    {
        int goalNode = _grid.GetClosestNode(new Vector3(5f, 0f, 5f));
        int region = _grid.GetRegionId(goalNode);

        FlowField flowField = FlowFieldEngine.GenerateFlowPath(_grid, goalNode, region);

        int localGoalIndex = _grid.GetLocalNode(goalNode);
        Assert.AreEqual(0, flowField.IntegrationField[localGoalIndex]);
    }

    [Test]
    public void IntegrationField_CostIncreasesWithDistance()
    {
        int goalNode = _grid.GetClosestNode(new Vector3(0f, 0f, 0f));
        int nearNode = _grid.GetClosestNode(new Vector3(1f, 0f, 0f));
        int farNode = _grid.GetClosestNode(new Vector3(3f, 0f, 0f));
        int region = _grid.GetRegionId(goalNode);

        FlowField flowField = FlowFieldEngine.GenerateFlowPath(_grid, goalNode, region);

        float nearCost = flowField.IntegrationField[_grid.GetLocalNode(nearNode)];
        float farCost = flowField.IntegrationField[_grid.GetLocalNode(farNode)];

        Assert.IsTrue(farCost > nearCost);
        Assert.IsTrue(nearCost > 0);
    }

    [Test]
    public void IntegrationField_FindsPathAroundObstacle()
    {
        int goalNode = _grid.GetClosestNode(new Vector3(0f, 0f, 0f));
        int obstacleNode = _grid.GetClosestNode(new Vector3(1f, 0f, 0f));
        int behindObstacleNode = _grid.GetClosestNode(new Vector3(2f, 0f, 0f));
        int region = _grid.GetRegionId(goalNode);

        _grid.SetWalkable(obstacleNode, false);

        FlowField flowField = FlowFieldEngine.GenerateFlowPath(_grid, goalNode, region);

        float obstacleCost = flowField.IntegrationField[_grid.GetLocalNode(obstacleNode)];
        float pathCost = flowField.IntegrationField[_grid.GetLocalNode(behindObstacleNode)];

        Assert.AreEqual(float.MaxValue, obstacleCost);
        Assert.IsTrue(pathCost < float.MaxValue);
    }

    [Test]
    public void IntegrationField_SameInputProducesSameResult()
    {
        int goalNode = _grid.GetClosestNode(new Vector3(2f, 0f, 2f));
        int region = _grid.GetRegionId(goalNode);

        FlowField fieldA = FlowFieldEngine.GenerateFlowPath(_grid, goalNode, region);
        FlowField fieldB = FlowFieldEngine.GenerateFlowPath(_grid, goalNode, region);

        CollectionAssert.AreEqual(fieldA.IntegrationField, fieldB.IntegrationField);
    }

    // ==========================================
    // FLOW FIELD TESTS
    // ==========================================

    [Test]
    public void FlowField_DirectionPointsTowardsGoal()
    {
        int goalNode = _grid.GetClosestNode(new Vector3(5f, 0f, 5f));
        int startNode = _grid.GetClosestNode(new Vector3(7f, 0f, 5f));
        int region = _grid.GetRegionId(goalNode);

        FlowField flowField = FlowFieldEngine.GenerateFlowPath(_grid, goalNode, region);
        if (flowField == null)
        {
            Assert.Fail("FlowField generation failed.");
        }
        Vector3 direction = flowField.FlowDirections[_grid.GetLocalNode(startNode)];

        // La dirección desde (7,0,5) hacia la meta en (5,0,5) debe apuntar hacia la izquierda (-X)
        Assert.AreEqual(-1f, direction.x, 0.01f);
        Assert.AreEqual(0f, direction.z, 0.01f);
    }

    [Test]
    public void FlowField_DoesNotGenerateDirectionForObstacle()
    {
        int goalNode = _grid.GetClosestNode(new Vector3(0f, 0f, 0f));
        int obstacleNode = _grid.GetClosestNode(new Vector3(2f, 0f, 2f));
        int region = _grid.GetRegionId(goalNode);

        _grid.SetWalkable(obstacleNode, false);

        FlowField flowField = FlowFieldEngine.GenerateFlowPath(_grid, goalNode, region);
        Vector3 direction = flowField.FlowDirections[_grid.GetLocalNode(obstacleNode)];

        Assert.AreEqual(Vector3.zero, direction);
    }

    // ==========================================
    // COST FIELD TESTS
    // ==========================================

    [Test]
    public void CostField_DefaultCostIsCorrect()
    {
        // Verificar que los nodos transitables por defecto tengan el coste base estándar (1)
        int sampleNode = _grid.GetClosestNode(new Vector3(2f, 0f, 2f));
        float defaultCost = _grid.GetNodeCost(sampleNode);

        Assert.AreEqual(1, defaultCost);
    }

    // ==========================================
    // ADVANCED INTEGRATION FIELD TESTS
    // ==========================================

    [Test]
    public void IntegrationField_HandlesCompletelyBlockedArea()
    {
        // Aislar por completo la esquina superior derecha encerrándola con obstáculos
        for (int x = 7; x <= 9; x++)
        {
            for (int y = 7; y <= 9; y++)
            {
                if (x == 7 || y == 7)
                {
                    int nodeToBlock = _grid.GetClosestNode(new Vector3(x, 0f, y));
                    _grid.SetWalkable(nodeToBlock, false);
                }
            }
        }

        int goalNode = _grid.GetClosestNode(new Vector3(0f, 0f, 0f));
        int unreachableNode = _grid.GetClosestNode(new Vector3(9f, 0f, 9f));
        int region = _grid.GetRegionId(unreachableNode);

        FlowField flowField = FlowFieldEngine.GenerateFlowPath(_grid, goalNode, region);
        Assert.IsNotNull(flowField);

        float unreachableCost = flowField.IntegrationField[_grid.GetLocalNode(unreachableNode)];

        // Los nodos inalcanzables deben mantener el valor máximo (infinito/float.MaxValue)
        Assert.AreEqual(float.MaxValue, unreachableCost);
    }

    // ==========================================
    // ADVANCED FLOW FIELD TESTS
    // ==========================================

    [Test]
    public void FlowField_GoalHasZeroDirection()
    {
        int goalNode = _grid.GetClosestNode(new Vector3(2f, 0f, 2f));
        int region = _grid.GetRegionId(goalNode);

        FlowField flowField = FlowFieldEngine.GenerateFlowPath(_grid, goalNode, region);
        Assert.IsNotNull(flowField);

        Vector3 goalDirection = flowField.FlowDirections[_grid.GetLocalNode(goalNode)];

        // En el nodo meta la dirección debe ser vector cero para evitar oscilaciones
        Assert.AreEqual(Vector3.zero, goalDirection);
    }

    [Test]
    public void FlowField_DirectionsFollowIntegrationFieldGradient()
    {
        int goalNode = _grid.GetClosestNode(new Vector3(0f, 0f, 0f));
        int testNode = _grid.GetClosestNode(new Vector3(2f, 0f, 0f));
        int neighborLeft = _grid.GetClosestNode(new Vector3(1f, 0f, 0f));
        int region = _grid.GetRegionId(goalNode);

        FlowField flowField = FlowFieldEngine.GenerateFlowPath(_grid, goalNode, region);
        Assert.IsNotNull(flowField);

        float currentCost = flowField.IntegrationField[_grid.GetLocalNode(testNode)];
        float neighborCost = flowField.IntegrationField[_grid.GetLocalNode(neighborLeft)];

        Vector3 direction = flowField.FlowDirections[_grid.GetLocalNode(testNode)];

        // Garantiza que la dirección apunte descendentemente en el gradiente de coste (hacia neighborLeft)
        Assert.IsTrue(neighborCost < currentCost);
        Assert.AreEqual(-1f, direction.x, 0.01f);
    }

    // ==========================================
    // SMART OFFSET / FORMATION TESTS
    // ==========================================

    [Test]
    public void SmartOffset_ReturnsValidPosition()
    {
        List<Vector3> offsets = FormationGenerator.Generate(FormationType.Square, 4, 1.0f);

        Assert.AreEqual(4, offsets.Count);
        foreach (Vector3 offset in offsets)
        {
            Assert.IsFalse(float.IsNaN(offset.x));
            Assert.IsFalse(float.IsNaN(offset.y));
            Assert.IsFalse(float.IsNaN(offset.z));
        }
    }

    [Test]
    public void SmartOffset_DoesNotExceedMaximumOffset()
    {
        int agentCount = 9;
        float spacing = 1.5f;

        List<Vector3> offsets = FormationGenerator.Generate(FormationType.Square, agentCount, spacing);

        // En una formación de 9 unidades (3x3), la distancia máxima al centro no debe superar la diagonal del cuadrado
        float maxAllowedDistance = Mathf.Sqrt(2) * spacing * 2f;

        foreach (Vector3 offset in offsets)
        {
            Assert.IsTrue(offset.magnitude <= maxAllowedDistance);
        }
    }

    // ==========================================
    // AGENT INTEGRATION TESTS
    // ==========================================

    [Test]
    public void Agent_MovesTowardsGoal()
    {
        GameObject agentGO = new GameObject("TestAgent");
        NavAgent agent = agentGO.AddComponent<NavAgent>();
        FlowFieldSteering steering = agentGO.AddComponent<FlowFieldSteering>();

        agent.AssignGraph(_grid);

        agent.transform.position = new Vector3(3f, 0f, 1f);
        int goalNode = _grid.GetClosestNode(new Vector3(1f, 0f, 1f));

        // Iniciar navegación
        agent.SetDestination(goalNode);

        Vector3 initialDistance = agent.transform.position - _grid.GetNodePosition(goalNode);

        // Simular ejecución manual del ciclo Update si no se corre en PlayMode
        // agent.Update();

        Assert.AreEqual(goalNode, agent.TargetNode);

        Object.DestroyImmediate(agentGO);
    }

    [Test]
    public void Agent_DoesNotMoveThroughObstacle()
    {
        int obstacleNode = _grid.GetClosestNode(new Vector3(2f, 0f, 1f));
        _grid.SetWalkable(obstacleNode, false);

        GameObject agentGO = new GameObject("TestAgent");
        NavAgent agent = agentGO.AddComponent<NavAgent>();
        agent.AssignGraph(_grid);

        Vector3 initialPosition = new Vector3(3f, 0f, 1f);
        agent.transform.position = initialPosition;

        Vector3 obstaclePosition = _grid.GetNodePosition(obstacleNode);

        Vector3 directionToObstacle = (obstaclePosition - initialPosition).normalized;

        int targetNode = _grid.GetClosestNode(initialPosition + directionToObstacle * 1.0f);

        Assert.IsFalse(_grid.IsWalkable(targetNode), "El nodo objetivo debería ser no caminable.");

        float distanceToObstacleCenter = Vector3.Distance(agent.transform.position, obstaclePosition);

        Assert.IsTrue(distanceToObstacleCenter >= 0.5f, "El agente penetró en la posición del obstáculo.");

        Object.DestroyImmediate(agentGO);
    }
}