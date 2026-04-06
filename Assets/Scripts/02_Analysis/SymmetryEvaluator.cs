using UnityEngine;
using System.Collections.Generic;

public class SymmetryEvaluator : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The maximum allowed percentage difference to be considered 'Balanced'")]
    public float balanceThresholdPercent = 15f; 
    
    [Tooltip("True = Split left/right (X-axis). False = Split top/bottom (Z-axis).")]
    public bool splitOnXAxis = true;  

    public float SymmetryDelta { get; private set; }

    #region Core Evaluation Logic
    /// <param name="nodes">The list of evaluated nodes representing map visibility.</param>
    public void EvaluateMapSymmetry(List<SightlineAnalyzer.SightlineNode> nodes)
    {
        if (nodes == null || nodes.Count == 0)
        {
            Debug.LogWarning("<color=purple>[Symmetry]</color> No nodes provided to evaluate.");
            return;
        }

        float teamAScore = 0f;
        float teamBScore = 0f;
        int teamACount = 0;
        int teamBCount = 0;

        // 1. Sort and aggregate data based on the chosen axis
        foreach (var node in nodes)
        {
            float axisValue = splitOnXAxis ? node.position.x : node.position.z;

            if (axisValue < 0)
            {
                teamAScore += node.score;
                teamACount++;
            }
            else if (axisValue > 0)
            {
                teamBScore += node.score;
                teamBCount++;
            }
        }

        // Safety check to prevent divide-by-zero
        if (teamACount == 0 || teamBCount == 0)
        {
            Debug.LogWarning("<color=purple>[Symmetry]</color> One side of the map has 0 nodes. Map is completely broken.");
            SymmetryDelta = 100f; // Assign maximum unfairness
            return;
        }

        // 2. Calculate averages
        float teamA_Avg = teamAScore / teamACount;
        float teamB_Avg = teamBScore / teamBCount;

        // 3. Compute fairness delta
        float difference = Mathf.Abs(teamA_Avg - teamB_Avg);
        float maxScore = Mathf.Max(teamA_Avg, teamB_Avg);
        
        float deltaPercent = (difference / maxScore) * 100f;
        float fairnessScore = 100f - deltaPercent;

        SymmetryDelta = deltaPercent;

        LogResults(teamA_Avg, teamACount, teamB_Avg, teamBCount, fairnessScore, deltaPercent);
    }
    #endregion

    #region Output & Logging
    private void LogResults(float teamA_Avg, int teamACount, float teamB_Avg, int teamBCount, float fairnessScore, float deltaPercent)
    {
        Debug.Log("<b><color=purple>--- [Tactical Symmetry Report] ---</color></b>");
        Debug.Log($"Team A (West/South) Avg Score: <b>{teamA_Avg:F2}</b> | Nodes: {teamACount}");
        Debug.Log($"Team B (East/North) Avg Score: <b>{teamB_Avg:F2}</b> | Nodes: {teamBCount}");

        if (deltaPercent <= balanceThresholdPercent)
        {
            Debug.Log($"<color=purple>[BALANCED]</color> Map Fairness: <b>{fairnessScore:F1}%</b> (Delta: {deltaPercent:F1}%)");
        }
        else
        {
            string favored = teamA_Avg > teamB_Avg ? "Team A" : "Team B";
            Debug.Log($"<color=purple>[UNBALANCED]</color> Map favors {favored}. Fairness: <b>{fairnessScore:F1}%</b> (Delta: {deltaPercent:F1}%)");
        }
    }
    #endregion
}