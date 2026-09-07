using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;

public class TelemetryTracker : MonoBehaviour
{
    [Header("File Settings")]
    public string fileName = "TelemetryData.json";

    [Header("Configuration")]
    [Tooltip("Tiempo en segundos entre cada captura")]
    public float captureInterval = 1.0f;

    public bool recordFps = true;
    public bool recordAgentData = true;
    public bool recordECSAgentData = true;

    private TelemetryWriter _writer;
    private float _timer = 0f;

    private void Awake()
    {  
        _writer = new TelemetryWriter(fileName);
    }

    private void Update()
    {
        _timer += Time.deltaTime;

        if (_timer >= captureInterval)
        {
            _timer = 0f;
            CaptureData();
        }
    }

    private void CaptureData()
    {
        TelemetryFrame frame = new TelemetryFrame
        {
            timeStamp = Time.time,
            fps = recordFps ? (1f / Time.unscaledDeltaTime) : 0f,
            standardAgents = new List<AgentTelemetryData>(),
            ecsAgents = new List<ECSAgentTelemetryData>()
        };

        // 1. Capturar NavAgent (Standard / GameObjects)
        if (recordAgentData)
        {
            NavAgent[] agents = FindObjectsByType<NavAgent>(FindObjectsSortMode.None);
            foreach (var agent in agents)
            {
                if (agent == null) continue;

                FlowFieldSteering steering = agent.GetComponent<FlowFieldSteering>();

                int currentSteps = steering != null ? steering.CurrentSteps : 0;
                float maxSteps = steering != null ? steering.GetAbsoluteMaxSteps() : 0f;
                float offsetPercentage = maxSteps > 0f ? (float)currentSteps / maxSteps : 0f;

                frame.standardAgents.Add(new AgentTelemetryData
                {
                    instanceName = agent.gameObject.name,
                    position = agent.transform.position,
                    velocity = agent.Velocity,
                    currentNode = agent.CurrentNode,
                    targetNode = agent.TargetNode,
                    graphId = agent.Graph != null ? agent.Graph.GraphId : -1,
                    currentSteps = currentSteps,
                    maxSteps = maxSteps,
                    offsetPercentage = offsetPercentage
                });
            }
        }

        // 2. Capturar AgentComponent (DOTS / ECS)
        if (recordECSAgentData && World.DefaultGameObjectInjectionWorld != null)
        {
            EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery query = em.CreateEntityQuery(typeof(AgentComponent), typeof(LocalTransform));

            using (var ecsAgents = query.ToComponentDataArray<AgentComponent>(Allocator.TempJob))
            using (var ecsTransforms = query.ToComponentDataArray<LocalTransform>(Allocator.TempJob))
            using (var entities = query.ToEntityArray(Allocator.TempJob))
            {
                for (int i = 0; i < ecsAgents.Length; i++)
                {
                    var agentComp = ecsAgents[i];

                    float maxSteps = agentComp.MaxSteps;
                    int currentSteps = agentComp.CurrentSteps;
                    float offsetPercentage = maxSteps > 0f ? (float)currentSteps / maxSteps : 0f;

                    frame.ecsAgents.Add(new ECSAgentTelemetryData
                    {
                        entityIndex = entities[i].Index,
                        position = ecsTransforms[i].Position,
                        velocity = agentComp.Velocity,
                        currentNode = agentComp.CurrentNode,
                        targetNode = agentComp.RouteId, // RouteId asignado como targetNode
                        graphId = agentComp.GraphId,
                        currentSteps = currentSteps,
                        maxSteps = maxSteps,
                        offsetPercentage = offsetPercentage
                    });
                }
            }
        }

        // 3. Enviar a la cola del hilo secundario
        _writer.EnqueueFrame(frame);
    }

    private void OnDestroy()
    {
        if (_writer != null)
        {
            _writer.Dispose();
        }
    }
}