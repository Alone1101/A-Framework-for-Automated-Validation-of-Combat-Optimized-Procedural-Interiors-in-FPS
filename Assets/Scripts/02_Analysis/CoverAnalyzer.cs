using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class CoverAnalyzer : MonoBehaviour
{
    [Header("Lifecycle")]
    public bool validateOnStart = false;

    [Header("Analysis Parameters")]
    public float coverClusterRadius = 3.5f;
    public int minClusterSize = 3;
    public int raysPerCover = 36;
    public float maxProtectionDistance = 2f;
    public float raycastOriginHeight = 1.2f;
    public LayerMask coverBlockingLayers;

    [Header("Validation Thresholds")]
    public float minCoverDensity = 0.04f;
    public float maxCoverDensity = 0.15f;
    public float minClusteringScore = 0.3f;
    public float maxClusteringScore = 0.85f;
    public float minCoverEffectiveness = 0.25f;
    [Range(0f, 1f)] public float minDistributionEvenness = 0.4f;
    public bool logValidationMessages = true;

    public CoverMetrics LastComputedMetrics { get; private set; } = new CoverMetrics();

    //Cache for UI visualizations
    private struct CoverRayData
    {
        public Vector3 start;
        public Vector3 end;
        public Color color;
    }
    private List<CoverRayData> debugRays = new List<CoverRayData>();
    private List<Vector3[]> clusterDebugLines = new List<Vector3[]>();

    #region Main Analysis Logic
    public CoverMetrics AnalyzeCoverMetrics()
    {
        // Clear old visual lines before running new math
        debugRays.Clear();
        clusterDebugLines.Clear();

        CoverMetrics metrics = new CoverMetrics();
        CoverObject[] allCovers = FindObjectsByType<CoverObject>(FindObjectsSortMode.None);
        
        if (allCovers.Length == 0)
        {
            LastComputedMetrics = metrics;
            return metrics;
        }

        // Calculate global map area from MapBorderGenerator
        MapBorderGenerator border = Object.FindFirstObjectByType<MapBorderGenerator>();
        float area = (border != null) ? border.mapWidth * border.mapDepth : 1.0f;
        
        metrics.NavMeshArea = area;
        List<CoverObject> coverList = allCovers.ToList();

        metrics.CoverDensity = CalculateCoverDensity(coverList, area);
        metrics.TypeRatio = CalculateCoverTypeDistribution(coverList);
        metrics.ClusteringScore = CalculateCoverClustering(coverList);
        metrics.AvgCoverEffectiveness = CalculateAverageCoverEffectiveness(coverList);
        metrics.DistributionEvenness = CalculateCoverDistributionEvenness(coverList, border);

        LastComputedMetrics = metrics;
        return metrics;
    }

    public bool ValidateCoverSetup(bool recomputeMetrics = true, List<string> issues = null)
    {
        List<string> issueSink = issues ?? new List<string>();
        CoverMetrics metrics = recomputeMetrics ? AnalyzeCoverMetrics() : LastComputedMetrics;
        bool isValid = EvaluateMetrics(metrics, issueSink);

        if (logValidationMessages)
        {
            string summary = FormatMetricSummary(metrics);
            if (isValid) Debug.Log($"<color=green>[CoverAnalyzer]</color> Validation succeeded. {summary}");
            else Debug.LogWarning($"<color=orange>[CoverAnalyzer]</color> Validation failed: {string.Join("; ", issueSink)}. {summary}");
        }

        return isValid;
    }
    #endregion

    #region Tactical Calculations
    private float CalculateAverageCoverEffectiveness(List<CoverObject> covers)
    {
        if (covers.Count == 0) return 0f;
        float total = covers.Sum(CalculateSingleCoverEffectiveness);
        return total / covers.Count;
    }

    private float CalculateSingleCoverEffectiveness(CoverObject cover)
    {
        if (cover == null || raysPerCover <= 0) return 0f;

        int blockedRays = 0;
        Vector3 coverPos = cover.transform.position;
        float[] heights = { 0.6f, raycastOriginHeight };
        int totalPossibleHits = raysPerCover * heights.Length;

        foreach (float h in heights)
        {
            Vector3 origin = new Vector3(coverPos.x, h, coverPos.z);
            for (int i = 0; i < raysPerCover; i++)
            {
                float angle = i * (360f / raysPerCover);
                Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                Vector3 start = origin + (dir * 3.0f);

                if (Physics.Raycast(start, -dir, out RaycastHit hit, 3.5f, coverBlockingLayers))
                {
                    // Green: Hits the target cover
                    if (hit.collider.gameObject == cover.gameObject || hit.collider.transform.IsChildOf(cover.transform))
                    {
                        blockedRays++;
                        debugRays.Add(new CoverRayData { start = start, end = hit.point, color = Color.green });
                    }
                    // Yellow: Hits environment (walls/border) or another cover
                    else 
                    {
                        debugRays.Add(new CoverRayData { start = start, end = hit.point, color = Color.yellow });
                    }
                }
                // Red: Total Exposure
                else 
                {
                    debugRays.Add(new CoverRayData { start = start, end = start + (-dir * 3.5f), color = Color.red });
                }
            }
        }
        return (float)blockedRays / totalPossibleHits;
    }
    #endregion

    #region Helper & Metrics Methods
    private float CalculateCoverDensity(List<CoverObject> covers, float area)
    {
        float totalCoverArea = 0f;
        foreach (var cover in covers)
        {
            // Try to get a collider on the parent first
            Collider col = cover.GetComponent<Collider>();
            
            if (col != null) 
            {
                // If the parent has a collider then measure it
                Vector3 size = col.bounds.size;
                totalCoverArea += (size.x * size.z);
            }
            else
            {
                // If the parent is empty then check its children
                Collider[] childColliders = cover.GetComponentsInChildren<Collider>();
                
                if (childColliders.Length > 0)
                {
                    // Create an empty bounding box
                    Bounds combinedBounds = childColliders[0].bounds;
                    
                    // Expand the box to fit all the children pieces
                    foreach (Collider childCol in childColliders)
                    {
                        combinedBounds.Encapsulate(childCol.bounds);
                    }
                    
                    // Measure the total combined physical footprint
                    Vector3 size = combinedBounds.size;
                    totalCoverArea += (size.x * size.z);
                }
                else
                {
                    Debug.LogWarning($"<color=yellow>[CoverAnalyzer]</color> Warning: {cover.name} has no colliders on itself or its children! Skipping.");
                }
            }
        }
        return totalCoverArea / Mathf.Max(area, 0.01f);
    }

    private CoverTypeRatio CalculateCoverTypeDistribution(List<CoverObject> covers)
    {
        int total = covers.Count;
        if (total == 0) return new CoverTypeRatio();
        int full = covers.Count(c => c.CoverType == CoverType.Full);
        return new CoverTypeRatio { FullCoverRatio = (float)full / total, PartialCoverRatio = (float)(total - full) / total };
    }

    private float CalculateCoverDistributionEvenness(List<CoverObject> covers, MapBorderGenerator border)
    {
        if (covers.Count == 0 || border == null) return 0f;
        Bounds bounds = border.GetPlayableBounds();

        int gridSize = 4;
        int[,] grid = new int[gridSize, gridSize];

        foreach (var cover in covers)
        {
            Vector3 rel = cover.transform.position - bounds.min;
            int x = Mathf.Clamp(Mathf.FloorToInt(rel.x / (bounds.size.x / gridSize)), 0, gridSize - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(rel.z / (bounds.size.z / gridSize)), 0, gridSize - 1);
            grid[x, z]++;
        }

        float mean = (float)covers.Count / (gridSize * gridSize);
        float variance = 0;
        for (int x = 0; x < gridSize; x++)
            for (int z = 0; z < gridSize; z++)
                variance += Mathf.Pow(grid[x, z] - mean, 2);
        
        float stdDev = Mathf.Sqrt(variance / (gridSize * gridSize));
        return 1f / (1f + (stdDev / (mean + 0.001f)));
    }

    private bool EvaluateMetrics(CoverMetrics metrics, List<string> issues)
    {
        bool valid = true;
        if (metrics.CoverDensity < minCoverDensity){ issues.Add("Low Density"); valid = false; }
        if (metrics.CoverDensity > maxCoverDensity){ issues.Add("High Density"); valid = false; }
        if (metrics.ClusteringScore < minClusteringScore){ issues.Add("Low Clustering"); valid = false; }
        if (metrics.ClusteringScore > maxClusteringScore){ issues.Add("High Clustering"); valid = false; }
        if (metrics.AvgCoverEffectiveness < minCoverEffectiveness){ issues.Add("Low Effectiveness"); valid = false; }
        if (metrics.DistributionEvenness < minDistributionEvenness){ issues.Add("Low Evenness"); valid = false; }
        return valid;
    }

    private string FormatMetricSummary(CoverMetrics metrics) => 
        $"Density: {metrics.CoverDensity:F2}, " +
        $"Clustering: {metrics.ClusteringScore:F2}, " +
        $"Effectiveness: {metrics.AvgCoverEffectiveness:F2}, " +
        $"Evenness: {metrics.DistributionEvenness:F2}";

    private float CalculateCoverClustering(List<CoverObject> covers)
    {
        if (covers.Count < minClusterSize) return 0f;
        int clustered = 0;
        bool[] visited = new bool[covers.Count];
        for (int i = 0; i < covers.Count; i++) {
            if (visited[i]) continue;
            List<int> currentCluster = new List<int>();
            FindClusterRecursive(covers, i, visited, currentCluster);
            if (currentCluster.Count >= minClusterSize) clustered += currentCluster.Count;
        }
        return (float)clustered / covers.Count;
    }

    private void FindClusterRecursive(List<CoverObject> covers, int idx, bool[] visited, List<int> cluster) {
        visited[idx] = true; cluster.Add(idx);
        // Get starting position and set y = 0
        Vector3 posA = covers[idx].transform.position;
        posA.y = 0;
        for (int i = 0; i < covers.Count; i++)
        {
            if (!visited[i])
            {
                Vector3 posB = covers[i].transform.position;
                posB.y = 0;
                if (Vector3.Distance(posA, posB) <= coverClusterRadius)
                {
                    clusterDebugLines.Add(new Vector3[] { covers[idx].transform.position, covers[i].transform.position });
                    FindClusterRecursive(covers, i, visited, cluster);
                }
            }
        }
    }
    #endregion

    void OnDrawGizmos()
    {
        if (ValidationManager.Instance != null && ValidationManager.Instance.showCoverLines)
        {
            foreach (var ray in debugRays)
            {
                Gizmos.color = ray.color;
                Gizmos.DrawLine(ray.start, ray.end);
            }

            Gizmos.color = Color.cyan;
            foreach (var line in clusterDebugLines)
            {
                Gizmos.DrawLine(line[0], line[1]);
                Gizmos.DrawWireSphere(line[0], 0.5f);
            }
        }
    }
}