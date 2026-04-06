using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class SightlineAnalyzer : MonoBehaviour
{
    [Header("References")]
    public MapBorderGenerator mapBorder;

    [Header("Sampling Settings")]
    public float gridSpacing = 1.5f;
    public float eyeHeight = 1.2f;
    public int raysPerPoint = 36;
    public float maxViewDistance = 18f;
    public LayerMask obstructionMask;

    [Header("Visualization")]
    public bool showHeatmap = true;
    public float sphereSize = 0.5f;
    [GradientUsage(true)] public Gradient heatmapGradient;

    private readonly List<SightlineNode> _nodes = new();

    public List<SightlineNode> nodes => _nodes;

    public float ExposureScore { get; private set; }

    public struct SightlineNode
    {
        public Vector3 position;
        public float score; 
    }

    #region Analysis Logic
    [ContextMenu("Analyze Sightlines")]
    public void AnalyzeMap()
    {
        if (mapBorder == null)
        {
            Debug.LogWarning("<color=cyan>[SightlineAnalyzer]</color> mapBorder is null; skipping.");
            return;
        }
        nodes.Clear();

        Bounds bounds = mapBorder.GetPlayableBounds();

        float margin = 1.0f;

        for (float x = bounds.min.x + margin; x <= bounds.max.x - margin; x += gridSpacing)
        {
            for (float z = bounds.min.z + margin; z <= bounds.max.z - margin; z += gridSpacing)
            {
                // 1. NavMesh check at Y = 0
                Vector3 floorLevelPos = new Vector3(x, 0f, z);
                
                // 2. Increase the 'maxDistance' (2.0f) for safety bound
                if (NavMesh.SamplePosition(floorLevelPos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                {
                    // 3. Move the analysis point back up to eye height
                    Vector3 analyzePos = hit.position + Vector3.up * eyeHeight;
                    
                    float score = CalculateVisibilityScore(analyzePos);
                    nodes.Add(new SightlineNode { position = analyzePos, score = score });
                }
            }
        }

        float totalExposure = 0f;
        foreach (var node in nodes)
        {
            totalExposure += node.score;
        }
        ExposureScore = nodes.Count > 0 ? (totalExposure / nodes.Count) : 0f;

        Debug.Log($"<color=cyan>[SightlineAnalyzer]</color> Successfully found and analyzed {nodes.Count} walkable nodes. Avg Exposure: {ExposureScore:F2}");
        if (nodes.Count == 0)
            Debug.LogWarning("<color=cyan>[SightlineAnalyzer]</color> No walkable nodes found; check NavMesh bake and bounds.");
    }

    private float CalculateVisibilityScore(Vector3 origin)
    {
        float totalDist = 0f;
        for (int i = 0; i < raysPerPoint; i++)
        {
            float angle = i * (360f / raysPerPoint);
            Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, maxViewDistance, obstructionMask))
                totalDist += hit.distance;
            else
                totalDist += maxViewDistance;
        }

        float linearScore = totalDist / (raysPerPoint * maxViewDistance);
        return Mathf.Pow(linearScore, 1.5f); // Contrast boost
    }
    #endregion

    #region Visualization
    private void OnDrawGizmos()
    {
        if (!showHeatmap || nodes.Count == 0) return;

        if (ValidationManager.Instance != null && ValidationManager.Instance.showHeatmap)
        {
            foreach (var node in nodes)
            {
                Color c = heatmapGradient.Evaluate(node.score);
                Gizmos.color = c;

                // Draw sphere at eye height
                Gizmos.DrawSphere(node.position, sphereSize);
            }
        }
    }
    #endregion
}