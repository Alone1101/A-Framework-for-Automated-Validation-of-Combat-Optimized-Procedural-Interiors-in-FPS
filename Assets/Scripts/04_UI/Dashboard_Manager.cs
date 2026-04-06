using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DashboardManager : MonoBehaviour
{
    #region UI Structures & References
    [Header("Global Status")]
    public TextMeshProUGUI statusText;

    [Header("UI Toggles")]
    public Toggle toggleCover;
    public Toggle toggleExclusion;
    public Toggle toggleHeatmap;

    [System.Serializable]
    public struct StatUI
    {
        public Image bgBox;
        public TextMeshProUGUI valueText;
    }

    [Header("Cover Metrics UI")]
    public StatUI uiCoverDensity;
    public StatUI uiCoverEffectiveness;
    public StatUI uiCoverEvenness;
    public StatUI uiCoverClustering;
    public StatUI uiCoverTypeRatio;

    [Header("Visibility Metrics UI")]
    public StatUI uiSightlineExposure;
    public StatUI uiVisibilityBalance;

    [Header("Navigation Metrics UI")]
    public StatUI uiNavigableArea;
    public StatUI uiChokepointCentrality;
    #endregion

    #region Internal State
    private bool _coverPassed = false;
    private bool _visibilityPassed = false;
    private bool _navigationPassed = false;
    #endregion

    #region Initialization
    void Start()
    {
        // Bind UI toggles to their respective visualization methods
        if (toggleCover != null) toggleCover.onValueChanged.AddListener(UpdateCoverVisuals);
        if (toggleExclusion != null) toggleExclusion.onValueChanged.AddListener(UpdateExclusionVisuals);
        if (toggleHeatmap != null) toggleHeatmap.onValueChanged.AddListener(UpdateHeatmapVisuals);
    }
    #endregion

    #region Data Refresh Methods
    public void RefreshCoverStats(CoverAnalyzer analyzer)
    {
        var metrics = analyzer.LastComputedMetrics;

        // 1. Cover Density
        bool densityPass = metrics.CoverDensity >= analyzer.minCoverDensity && metrics.CoverDensity <= analyzer.maxCoverDensity;
        ApplyColorLogic(uiCoverDensity, metrics.CoverDensity.ToString("F2"), densityPass);

        // 2. Cover Effectiveness
        bool effPass = metrics.AvgCoverEffectiveness >= analyzer.minCoverEffectiveness;
        ApplyColorLogic(uiCoverEffectiveness, metrics.AvgCoverEffectiveness.ToString("F2"), effPass);

        // 3. Distribution Evenness
        bool evenPass = metrics.DistributionEvenness >= analyzer.minDistributionEvenness;
        ApplyColorLogic(uiCoverEvenness, metrics.DistributionEvenness.ToString("F2"), evenPass);

        // 4. Clustering Score
        bool clusterPass = metrics.ClusteringScore >= analyzer.minClusteringScore && metrics.ClusteringScore <= analyzer.maxClusteringScore;
        ApplyColorLogic(uiCoverClustering, metrics.ClusteringScore.ToString("F2"), clusterPass);

        // 5. Type Ratio
        string ratioString = $"{metrics.TypeRatio.FullCoverRatio:F2} / {metrics.TypeRatio.PartialCoverRatio:F2}";
        ApplyColorLogic(uiCoverTypeRatio, ratioString, true); 

        _coverPassed = densityPass && effPass && evenPass && clusterPass;
        EvaluateGlobalStatus();
    }

    public void RefreshVisibilityStats(float exposureVal, bool exposurePasses, float symmetryVal, bool symmetryPasses)
    {
        ApplyColorLogic(uiSightlineExposure, exposureVal.ToString("F2"), exposurePasses);
        ApplyColorLogic(uiVisibilityBalance, symmetryVal.ToString("F1") + "%", symmetryPasses);

        _visibilityPassed = exposurePasses && symmetryPasses;
        EvaluateGlobalStatus();
    }

    public void RefreshNavigationStats(float navAreaVal, bool navPasses, float chokeVal, bool chokePasses)
    {
        ApplyColorLogic(uiNavigableArea, (navAreaVal * 100f).ToString("F0") + "%", navPasses); 
        ApplyColorLogic(uiChokepointCentrality, chokeVal.ToString("F2"), chokePasses);

        _navigationPassed = navPasses && chokePasses;
        EvaluateGlobalStatus();
    }
    #endregion

    #region UI Visual Logic
    private void EvaluateGlobalStatus()
    {
        if (statusText == null) return;

        if (_coverPassed && _visibilityPassed && _navigationPassed)
        {
            statusText.text = "Status: PASSED";
            statusText.color = Color.green;
        }
        else
        {
            statusText.text = "Status: REJECTED";
            statusText.color = Color.red;
        }
    }

    private void ApplyColorLogic(StatUI stat, string valueString, bool passed)
    {
        Color mainColor = passed ? Color.green : Color.red;

        if (stat.valueText != null)
        {
            stat.valueText.text = valueString;
            stat.valueText.color = mainColor;
        }

        // Update the Box Background (20% Opacity)
        if (stat.bgBox != null)
        {
            Color boxColor = mainColor;
            boxColor.a = 0.2f; 
            stat.bgBox.color = boxColor;
        }
    }
    #endregion

    #region Visual Debug Toggles
    void UpdateCoverVisuals(bool isOn)
    {
        if (ValidationManager.Instance != null) ValidationManager.Instance.showCoverLines = isOn;
        ForceSceneUpdate();
    }

    void UpdateExclusionVisuals(bool isOn)
    {
        if (ValidationManager.Instance != null) ValidationManager.Instance.showExclusionZones = isOn;
        ForceSceneUpdate();
    }

    void UpdateHeatmapVisuals(bool isOn)
    {
        if (ValidationManager.Instance != null) ValidationManager.Instance.showHeatmap = isOn;
        ForceSceneUpdate();
    }

    void ForceSceneUpdate()
    {
#if UNITY_EDITOR
        UnityEditor.SceneView.RepaintAll();
#endif
    }
    #endregion
}