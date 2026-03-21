using UnityEngine;

public class FlowFieldGroup
{
    public void MakeFormation(FlowFieldAgent[] agents, Vector3 center)
    {
        int numAgents = agents.Length;

        int agentsPerSide = Mathf.CeilToInt(Mathf.Sqrt(numAgents));
        float offsetDistance = 10f;

        int agentIndex = 0;

        // Para centrar el grid
        float halfSize = (agentsPerSide - 1) * offsetDistance / 2f;

        for (int x = 0; x < agentsPerSide; x++)
        {
            for (int z = 0; z < agentsPerSide; z++)
            {
                if (agentIndex >= numAgents)
                    return;

                float xPos = x * offsetDistance - halfSize;
                float zPos = z * offsetDistance - halfSize;

                Vector3 offset = new Vector3(xPos, 0, zPos);

                agents[agentIndex].offset = offset;

                agentIndex++;
            }
        }
    }

}
