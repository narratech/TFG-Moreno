using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[TestFixture]
public class QuadSphereNavigationTests
{
    private QuadSphereNavGraph _sphereGraph;
    private const float Radius = 10.0f;

    [SetUp]
    public void SetUp()
    {
        // Instanciamos un grafo geodésico de radio 10 unidades centrada en Vector3.zero
        _sphereGraph = new QuadSphereNavGraph(center: Vector3.zero, radius: Radius, rotation: Quaternion.identity, resolution: 8, regionsPerAxis:2);
        FlowFieldManager.Instance.RegisterContext(_sphereGraph);
    }

    // ==========================================
    // NAV GRAPH TESTS (QUADSPHERE)
    // ==========================================

    [Test]
    public void QuadSphere_NodePositionsAreOnSphereSurface()
    {
        int sampleNode = _sphereGraph.GetClosestNode(new Vector3(0f, Radius, 0f)); // Polo Norte
        Vector3 position = _sphereGraph.GetNodePosition(sampleNode);

        // La distancia desde el origen debe ser igual al radio de la esfera
        Assert.AreEqual(Radius, position.magnitude, 0.05f);
    }

    [Test]
    public void QuadSphere_NodeHasFourNeighbours()
    {
        // En una QuadSphere la mayoría de los nodos de la malla subdividida tienen 4 vecinos
        int nodeOnEquator = _sphereGraph.GetClosestNode(new Vector3(Radius, 0f, 0f));
        int count = _sphereGraph.GetNeighbors(nodeOnEquator).Count();

        Assert.AreEqual(4, count);
    }

    [Test]
    public void QuadSphere_ObstacleIsNotWalkable()
    {
        int obstacleNode = _sphereGraph.GetClosestNode(new Vector3(0f, Radius, 0f));
        _sphereGraph.SetWalkable(obstacleNode, false);

        Assert.IsFalse(_sphereGraph.IsWalkable(obstacleNode));
    }

    // ==========================================
    // INTEGRATION FIELD TESTS (QUADSPHERE)
    // ==========================================

    [Test]
    public void QuadSphere_IntegrationField_GoalHasZeroCost()
    {
        int goalNode = _sphereGraph.GetClosestNode(new Vector3(0f, Radius, 0f)); // Polo Norte
        int region = _sphereGraph.GetRegionId(goalNode);

        FlowField flowField = FlowFieldEngine.GenerateFlowPath(_sphereGraph, goalNode, region);

        int localGoalIndex = _sphereGraph.GetLocalNode(goalNode);
        Assert.AreEqual(0, flowField.IntegrationField[localGoalIndex]);
    }

    [Test]
    public void QuadSphere_IntegrationField_CostIncreasesWithSurfaceDistance()
    {
        // Metas y nodos evaluados siguiendo la curvatura de la esfera
        Vector3 goalPoint = new Vector3(0f, Radius, 0f);
        Vector3 nearPoint = Quaternion.AngleAxis(15, Vector3.right) * goalPoint;
        Vector3 farPoint = Quaternion.AngleAxis(45, Vector3.right) * goalPoint;

        int goalNode = _sphereGraph.GetClosestNode(goalPoint);
        int nearNode = _sphereGraph.GetClosestNode(nearPoint);
        int farNode = _sphereGraph.GetClosestNode(farPoint);
        int region = _sphereGraph.GetRegionId(goalNode);

        FlowField flowField = FlowFieldEngine.GenerateFlowPath(_sphereGraph, goalNode, region);

        float nearCost = flowField.IntegrationField[_sphereGraph.GetLocalNode(nearNode)];
        float farCost = flowField.IntegrationField[_sphereGraph.GetLocalNode(farNode)];

        Assert.IsTrue(farCost > nearCost);
        Assert.IsTrue(nearCost > 0);
    }

    // ==========================================
    // FLOW FIELD TESTS (QUADSPHERE)
    // ==========================================

    [Test]
    public void QuadSphere_FlowField_DirectionIsTangentToSphere()
    {
        Vector3 goalPoint = new Vector3(0f, Radius, 0f);
        Vector3 startPoint = Quaternion.AngleAxis(30, Vector3.right) * goalPoint;

        int goalNode = _sphereGraph.GetClosestNode(goalPoint);
        int startNode = _sphereGraph.GetClosestNode(startPoint);
        int region = _sphereGraph.GetRegionId(goalNode);

        FlowField flowField = FlowFieldEngine.GenerateFlowPath(_sphereGraph, goalNode, region);
        Assert.IsNotNull(flowField);

        Vector3 direction = flowField.FlowDirections[_sphereGraph.GetLocalNode(startNode)];
        Vector3 nodePosition = _sphereGraph.GetNodePosition(startNode);

        // El vector dirección del flujo debe ser tangente a la superficie esférica (producto escalar con el vector normal posicional apróx 0)
        float dotProduct = Vector3.Dot(direction.normalized, nodePosition.normalized);

        Assert.AreEqual(0f, dotProduct, 1.0f);
    }

    // ==========================================
    // AGENT INTEGRATION TESTS (QUADSPHERE)
    // ==========================================

    [Test]
    public void QuadSphere_Agent_MovesOnSphereSurface()
    {
        GameObject agentGO = new GameObject("TestSphereAgent");
        NavAgent agent = agentGO.AddComponent<NavAgent>();
        FlowFieldSteering steering = agentGO.AddComponent<FlowFieldSteering>();

        agent.AssignGraph(_sphereGraph);

        Vector3 startPos = (new Vector3(0f, 0f, Radius)).normalized * Radius;
        agent.transform.position = startPos;

        int goalNode = _sphereGraph.GetClosestNode(new Vector3(0f, Radius, 0f));
        agent.SetDestination(goalNode);

        Assert.AreEqual(goalNode, agent.TargetNode);

        // La distancia desde la posición inicial del agente al origen debe coincidir con el Radio
        Assert.AreEqual(Radius, agent.transform.position.magnitude, 0.05f);

        Object.DestroyImmediate(agentGO);
    }
}