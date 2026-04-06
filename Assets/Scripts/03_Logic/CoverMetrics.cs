using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CoverMetrics
{
    [Tooltip("Percentage of the total physical spatial footprint of covers against the total navigable combat space.")]
    public float CoverDensity;

    [Tooltip("Percentage of covers that are full vs partial height.")]
    public CoverTypeRatio TypeRatio;

    [Tooltip("Ratio of covers that belong to clusters (0-1).")]
    public float ClusteringScore;

    [Tooltip("Average fraction of directions that a cover blocks within the specified radius.")]
    public float AvgCoverEffectiveness;

    [Tooltip("Normalized measure of how evenly the cover is distributed over the combat space.")]
    public float DistributionEvenness;

    [Tooltip("Area covered by the NavMesh triangles used in the analysis.")]
    public float NavMeshArea;

    [Tooltip("True when NavMesh data was available during the last analysis.")]
    public bool HasNavMesh;

    public bool IsValid(float minDensity = 0.04f, float maxDensity = 0.15f,
                        float minCluster = 0.3f, float maxCluster = 0.85f,
                        float minEffectiveness = 0.25f)
    {
        return Mathf.Clamp(CoverDensity, minDensity, maxDensity) == CoverDensity &&
               Mathf.Clamp(ClusteringScore, minCluster, maxCluster) == ClusteringScore &&
               AvgCoverEffectiveness >= minEffectiveness;
    }
}

[System.Serializable]
public struct CoverTypeRatio
{
    [Range(0f, 1f)]
    public float FullCoverRatio;
    [Range(0f, 1f)]
    public float PartialCoverRatio;
}

public class CoverCluster
{
    public readonly List<CoverObject> Members = new List<CoverObject>();
}