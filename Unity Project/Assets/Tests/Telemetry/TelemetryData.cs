using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TelemetryFrame
{
    public float timeStamp;
    public float fps;
    public List<AgentTelemetryData> standardAgents;
    public List<ECSAgentTelemetryData> ecsAgents;
}

[Serializable]
public class AgentTelemetryData
{
    public string instanceName;
    public Vector3 position;
    public Vector3 velocity;
    public int currentNode;
    public int targetNode;
    public int graphId;
    public int currentSteps;
    public float maxSteps;
    public float offsetPercentage;
}

[Serializable]
public class ECSAgentTelemetryData
{
    public int entityIndex;
    public Vector3 position;
    public Vector3 velocity;
    public int currentNode;
    public int targetNode; // Mapeado desde routeId
    public int graphId;
    public int currentSteps;
    public float maxSteps;
    public float offsetPercentage;
}