using NUnit.Framework;
using System.Linq;
using UnityEngine;

[TestFixture]
public class Grid3DNavigationTests
{
    private Grid3DNavGraph _grid3D;

    [SetUp]
    public void SetUp()
    {
        // Grid 3D de 5x5x5 nodos (125 nodos), cellSize 1.0f, regiones 5x5x5 en el origen (0,0,0)
        _grid3D = new Grid3DNavGraph(width: 5, height: 5, depth: 5, cellSize: 1.0f, 
            regionWidth: 5,
            regionHeight: 5,
            regionDepth: 5, origin: Vector3.zero);
        FlowFieldManager.Instance.RegisterContext(_grid3D);
    }

    // ==========================================
    // NAV GRAPH TESTS (3D)
    // ==========================================

    [Test]
    public void Grid3D_CreatesCorrectNumberOfNodes()
    {
        // 5 x 5 x 5 = 125 nodos
        Assert.AreEqual(125, _grid3D.NodeCount);
    }

    [Test]
    public void Grid3D_NodePositionsAreCorrect()
    {
        int index = _grid3D.GetClosestNode(new Vector3(2f, 1f, 3f));
        Vector3 position = _grid3D.GetNodePosition(index);

        Assert.AreEqual(new Vector3(2f, 1f, 3f), position);
    }

    [Test]
    public void Grid3D_CenterNodeHasSixNeighbours()
    {
        // En 3D un nodo central (2,2,2) en adyacencia ortogonal de 6 caras debe devolver 6 vecinos
        int centerNode = _grid3D.GetClosestNode(new Vector3(2f, 2f, 2f));

        int count = _grid3D.GetNeighbors(centerNode).Count();

        Assert.AreEqual(6, count);
    }

    [Test]
    public void Grid3D_ObstacleIsNotWalkable()
    {
        int obstacleNode = _grid3D.GetClosestNode(new Vector3(2f, 2f, 2f));
        _grid3D.SetWalkable(obstacleNode, false);

        Assert.IsFalse(_grid3D.IsWalkable(obstacleNode));
    }

    // ==========================================
    // INTEGRATION FIELD TESTS (3D)
    // ==========================================

    [Test]
    public void IntegrationField3D_GoalHasZeroCost()
    {
        int goalNode = _grid3D.GetClosestNode(new Vector3(2f, 2f, 2f));
        int region = _grid3D.GetRegionId(goalNode);

        FlowField flowField = FlowFieldEngine.GenerateFlowPath(_grid3D, goalNode, region);

        int localGoalIndex = _grid3D.GetLocalNode(goalNode);
        Assert.AreEqual(0, flowField.IntegrationField[localGoalIndex]);
    }

    [Test]
    public void IntegrationField3D_CostIncreasesWithDistance()
    {
        int goalNode = _grid3D.GetClosestNode(new Vector3(0f, 0f, 0f));
        int nearNode = _grid3D.GetClosestNode(new Vector3(1f, 0f, 0f));
        int farNode = _grid3D.GetClosestNode(new Vector3(3f, 0f, 0f));
        int region = _grid3D.GetRegionId(goalNode);

        FlowField flowField = FlowFieldEngine.GenerateFlowPath(_grid3D, goalNode, region);

        float nearCost = flowField.IntegrationField[_grid3D.GetLocalNode(nearNode)];
        float farCost = flowField.IntegrationField[_grid3D.GetLocalNode(farNode)];

        Assert.IsTrue(farCost > nearCost);
        Assert.IsTrue(nearCost > 0);
    }

    [Test]
    public void IntegrationField3D_FindsPathAroundObstacle()
    {
        int goalNode = _grid3D.GetClosestNode(new Vector3(0f, 0f, 0f));
        int obstacleNode = _grid3D.GetClosestNode(new Vector3(1f, 0f, 0f));
        int behindObstacleNode = _grid3D.GetClosestNode(new Vector3(2f, 0f, 0f));
        int region = _grid3D.GetRegionId(goalNode);

        _grid3D.SetWalkable(obstacleNode, false);

        FlowField flowField = FlowFieldEngine.GenerateFlowPath(_grid3D, goalNode, region);

        float obstacleCost = flowField.IntegrationField[_grid3D.GetLocalNode(obstacleNode)];
        float pathCost = flowField.IntegrationField[_grid3D.GetLocalNode(behindObstacleNode)];

        Assert.AreEqual(float.MaxValue, obstacleCost);
        Assert.IsTrue(pathCost < float.MaxValue);
    }

    // ==========================================
    // FLOW FIELD TESTS (3D)
    // ==========================================

    [Test]
    public void FlowField3D_DirectionPointsTowardsGoal()
    {
        int goalNode = _grid3D.GetClosestNode(new Vector3(1f, 1f, 1f));
        int startNode = _grid3D.GetClosestNode(new Vector3(3f, 1f, 1f));
        int region = _grid3D.GetRegionId(goalNode);

        FlowField flowField = FlowFieldEngine.GenerateFlowPath(_grid3D, goalNode, region);
        Assert.IsNotNull(flowField);

        Vector3 direction = flowField.FlowDirections[_grid3D.GetLocalNode(startNode)];

        // La dirección desde (3,1,1) hacia (1,1,1) apunta en el eje -X
        Assert.AreEqual(-1f, direction.x, 0.01f);
        Assert.AreEqual(0f, direction.y, 0.01f);
        Assert.AreEqual(0f, direction.z, 0.01f);
    }

    [Test]
    public void FlowField3D_DoesNotGenerateDirectionForObstacle()
    {
        int goalNode = _grid3D.GetClosestNode(new Vector3(0f, 0f, 0f));
        int obstacleNode = _grid3D.GetClosestNode(new Vector3(2f, 2f, 2f));
        int region = _grid3D.GetRegionId(goalNode);

        _grid3D.SetWalkable(obstacleNode, false);

        FlowField flowField = FlowFieldEngine.GenerateFlowPath(_grid3D, goalNode, region);
        Vector3 direction = flowField.FlowDirections[_grid3D.GetLocalNode(obstacleNode)];

        Assert.AreEqual(Vector3.zero, direction);
    }

    // ==========================================
    // AGENT INTEGRATION TESTS (3D)
    // ==========================================

    [Test]
    public void Agent3D_MovesTowardsGoal()
    {
        GameObject agentGO = new GameObject("TestAgent3D");
        NavAgent agent = agentGO.AddComponent<NavAgent>();
        FlowFieldSteering steering = agentGO.AddComponent<FlowFieldSteering>();

        agent.AssignGraph(_grid3D);
        agent.transform.position = new Vector3(3f, 1f, 1f);

        int goalNode = _grid3D.GetClosestNode(new Vector3(1f, 1f, 1f));
        agent.SetDestination(goalNode);

        Assert.AreEqual(goalNode, agent.TargetNode);

        Object.DestroyImmediate(agentGO);
    }

    [Test]
    public void Agent3D_DoesNotMoveThroughObstacle()
    {
        int obstacleNode = _grid3D.GetClosestNode(new Vector3(2f, 1f, 1f));
        _grid3D.SetWalkable(obstacleNode, false);

        GameObject agentGO = new GameObject("TestAgent3D");
        NavAgent agent = agentGO.AddComponent<NavAgent>();
        agent.AssignGraph(_grid3D);

        Vector3 initialPosition = new Vector3(3f, 1f, 1f);
        agent.transform.position = initialPosition;

        Vector3 obstaclePosition = _grid3D.GetNodePosition(obstacleNode);
        Vector3 directionToObstacle = (obstaclePosition - initialPosition).normalized;

        int targetNode = _grid3D.GetClosestNode(initialPosition + directionToObstacle * 1.0f);

        Assert.IsFalse(_grid3D.IsWalkable(targetNode), "El voxel objetivo debería ser no caminable.");

        float distanceToObstacleCenter = Vector3.Distance(agent.transform.position, obstaclePosition);
        Assert.IsTrue(distanceToObstacleCenter >= 0.5f);

        Object.DestroyImmediate(agentGO);
    }
}