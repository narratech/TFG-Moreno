using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class FormationRecorder : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float _recordingDelay = 300f;
    [SerializeField] private string _fileName = "formation_data.json";

    private float _timer = 0f;
    private bool _hasRecorded = false;

    [Serializable]
    public struct Vector3Data
    {
        public float x, y, z;
        public Vector3Data(Vector3 v) { x = v.x; y = v.y; z = v.z; }
    }

    [Serializable]
    public class AgentRecord
    {
        public string agentName;
        public Vector3Data agentPosition;
        public Vector3Data samplePosition;
        public Vector3Data targetNodePosition;
        public Vector3Data formationOffset;
        public int currentSteps;
        public float maxSteps;
        public float offsetPercentage;
    }

    [Serializable]
    public class AccuracyDataset
    {
        public List<AgentRecord> agents = new List<AgentRecord>();
    }

    private void Update()
    {
        if (!_hasRecorded)
        {
            _timer += Time.deltaTime;
            if (_timer >= _recordingDelay)
            {
                RecordAndSaveData();
                _hasRecorded = true;
            }
        }
    }

    [ContextMenu("Save Data Now")]
    public void RecordAndSaveData()
    {
        AccuracyDataset dataset = new AccuracyDataset();
        // Sustituir la línea obsoleta:
        NavAgent[] navAgents = FindObjectsByType<NavAgent>(FindObjectsSortMode.None);

        foreach (var agent in navAgents)
        {
            if (agent == null) continue;

            FlowFieldSteering steering = agent.GetComponent<FlowFieldSteering>();
            if (steering == null) continue;

            Vector3 targetPos = (agent.TargetNode >= 0 && agent.Graph != null)
                ? agent.Graph.GetNodePosition(agent.TargetNode)
                : Vector3.zero;

            float maxSteps = steering.GetAbsoluteMaxSteps();
            int currentSteps = steering.CurrentSteps;
            float offsetPercentage = maxSteps > 0 ? (float)currentSteps / maxSteps : 0f;

            AgentRecord record = new AgentRecord
            {
                agentName = agent.gameObject.name,
                agentPosition = new Vector3Data(agent.transform.position),
                samplePosition = new Vector3Data(steering.GetConstrainedSamplePosition()),
                targetNodePosition = new Vector3Data(targetPos),
                formationOffset = new Vector3Data(steering.FormationOffset),
                currentSteps = currentSteps,
                maxSteps = maxSteps,
                offsetPercentage = offsetPercentage
            };

            dataset.agents.Add(record);
        }

        string jsonString = JsonUtility.ToJson(dataset, true);
        string filePath = Path.Combine(Application.persistentDataPath, _fileName);
        File.WriteAllText(filePath, jsonString);

        Debug.Log($"[AgentAccuracyRecorder] Datos de precisión guardados en: {filePath}");
    }
}