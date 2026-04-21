using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Diagnostics;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class LevelOrchestrator : MonoBehaviour
{
    #region Serialized Fields
    [Header("Generators")]
    public MapBorderGenerator borderGen;
    public CoverFieldGenerator coverGen;
    public MultiRoomManager roomManager;

    [Header("Navigation & Validation")]
    public ConnectivityChecker navChecker;

    [Header("Tactical Analyzers")]
    public SightlineAnalyzer sightlineAnlz;
    public CoverAnalyzer coverAnlz;
    public SymmetryEvaluator symmetryEval;

    [Header("UI Dashboard")]
    public DashboardManager dashboardUI;

    [Header("Settings")]
    public bool autoAnalyzeAfterGenerate = true;

    [Header("Batch Processing")]
    public int batchRunTarget = 1000; 
    public bool isBatchRunning = false;
    #endregion

    #region Private Variables
    private int _pipelineRunId;
    private bool _regenerateNextFrame;
    private Coroutine _currentPipeline;
    private List<GameObject> _spawnedObjects = new List<GameObject>();

    private long _totalComputeTimeMs = 0;
    private int _passCount = 0;
    private int _failNav = 0;
    private int _failCover = 0;
    private int _failFlow = 0;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        UnityEngine.Debug.Log("<b><color=yellow>[Orchestrator]</color></b> Initializing Scene...");
        _currentPipeline = StartCoroutine(GenerationPipeline());
    }

    private void Update()
    {
        // Deferred regeneration to prevent overlapping with previous active routines
        if (_regenerateNextFrame)
        {
            _regenerateNextFrame = false;
            if (_currentPipeline != null)
            {
                StopCoroutine(_currentPipeline);
            }
            
            if (navChecker != null) navChecker.StopAndReset();
            
            _currentPipeline = StartCoroutine(GenerationPipeline());
            return;
        }

        // Press R for re-generation (defer to next frame)
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            _regenerateNextFrame = true;
            return;
        }

        // Press V for quick re-validation of existing metrics
        if (Keyboard.current.vKey.wasPressedThisFrame)
        {
            StartCoroutine(RunAnalysisSuite());
        }

        // Press T to toggle the sightline heatmap visibility
        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            if (sightlineAnlz != null)
            {
                sightlineAnlz.showHeatmap = !sightlineAnlz.showHeatmap;
                UnityEngine.Debug.Log($"<color=white>[Orchestrator]</color> Heatmap Toggled: {sightlineAnlz.showHeatmap}");
            }
        }

        // Press B to start batch automation
        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            if (_currentPipeline != null) StopCoroutine(_currentPipeline);
            if (navChecker != null) navChecker.StopAndReset();
            StartCoroutine(BatchRunRoutine());
        }

        // Press E to force export the current map
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            UnityEngine.Debug.Log("<color=white>[Orchestrator]</color> Force Exporting current view...");
            ExportCurrentMap(_pipelineRunId);
        }
    }
    #endregion

    #region Core Pipelines
    public IEnumerator BatchRunRoutine()
    {
        isBatchRunning = true;

        _totalComputeTimeMs = 0;
        _passCount = 0;
        _failNav = 0;
        _failCover = 0;
        _failFlow = 0;

        UnityEngine.Debug.Log($"<color=yellow>[Batch Runner]</color> Starting batch of {batchRunTarget} runs...");

        string filePath = Application.dataPath + "/ProceduralGeneration_BatchResults.csv";
        if (File.Exists(filePath)) File.Delete(filePath);

        for (int i = 1; i <= batchRunTarget; i++)
        {
            // 1. Normal pipeline run
            yield return StartCoroutine(GenerationPipeline());

            // 2. Extract the math from analyzers
            CoverMetrics coverMetrics = coverAnlz != null ? coverAnlz.LastComputedMetrics : new CoverMetrics();
            float exposure = sightlineAnlz != null ? sightlineAnlz.ExposureScore : 0f;
            float symmetry = symmetryEval != null ? symmetryEval.SymmetryDelta : 0f;
            float navArea = navChecker != null ? navChecker.NavigableAreaRatio : 0f;
            float chokepoint = navChecker != null ? navChecker.MaxCentrality : 0f;

            // 3. Evaluate (Updated with Tactical Thresholds)
            bool covPass = coverAnlz != null && coverAnlz.ValidateCoverSetup(false); 
            bool expPass = exposure >= 0.30f && exposure <= 0.70f;
            bool symPass = symmetry <= 15.0f;
            bool navPass = navArea >= 0.55f && navArea <= 0.85f;
            bool chokePass = chokepoint >= 0.15f;
            bool mapPassed = covPass && expPass && symPass && navPass && chokePass;

            if (mapPassed)
            {
                _passCount++;
                // 4. Export Passed Maps
                ExportCurrentMap(i);
            }
            else
            {
                if (!navPass) _failNav++;
                else if (!covPass) _failCover++;
                else _failFlow++; // Catches Exposure, Symmetry, or Chokepoint failures
            }

            // 5. Insert into CSV
            LogRunToCSV(i, mapPassed, coverMetrics, navArea, chokepoint, symmetry, exposure);

            yield return null;
        }

        isBatchRunning = false;

        float avgTime = (float)_totalComputeTimeMs / batchRunTarget;
        float yieldRate = ((float)_passCount / batchRunTarget) * 100f;

        UnityEngine.Debug.Log($"<color=green>[Batch Runner]</color> Finished {batchRunTarget} runs!");
        UnityEngine.Debug.Log($"<color=white>[Time Complexity]</color> Average pure CPU execution time per map: <b>{avgTime:F2} ms</b>");
        UnityEngine.Debug.Log($"<color=white>[Yield Stats]</color> Passed: {_passCount} ({yieldRate:F1}%) | Failed: {batchRunTarget - _passCount}");
        UnityEngine.Debug.Log($"<color=white>[Failure Breakdown]</color> NavMesh/Connectivity: {_failNav} | Cover Constraints: {_failCover} | Flow/Sightlines: {_failFlow}");
    }

    public IEnumerator GenerationPipeline()
    {
        _pipelineRunId++;
        int runId = _pipelineRunId;

        // Clean up
        ClearAllGeneratedContent();
        yield return new WaitForEndOfFrame();
        yield return null;

        Stopwatch perfTimer = new Stopwatch();
        perfTimer.Start();
        
        // Geometry
        if(!isBatchRunning) UnityEngine.Debug.Log($"<color=yellow>[Orchestrator]</color> Phase 1: Generating Geometry... (run {runId})");
        if (borderGen != null) borderGen.GenerateMapBorder();
        if (roomManager != null) roomManager.GenerateMultipleRooms();
        if (coverGen != null) coverGen.GenerateCoverField();

        // Capture the objects for tracking/export
        CaptureActiveGeometry();

        yield return new WaitForFixedUpdate();
        
        // Navigation
        if(!isBatchRunning) UnityEngine.Debug.Log($"<color=yellow>[Orchestrator]</color> Phase 2: Baking NavMesh... (run {runId})");
        if (navChecker != null) navChecker.CheckNavMeshConnectivity();

        perfTimer.Stop();
        _totalComputeTimeMs += perfTimer.ElapsedMilliseconds;

        yield return new WaitForSeconds(0.1f);

        // Wait for bake with safety timeout
        float timeout = 5f;
        float elapsed = 0f;
        while (navChecker != null && navChecker.IsBaking && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
            if (runId != _pipelineRunId) yield break; // Cancel if R is pressed again
        }

        if (elapsed >= timeout) UnityEngine.Debug.LogWarning($"<color=red>[Orchestrator]</color> NavMesh bake timeout! (Run {runId})");

        yield return new WaitForSeconds(0.3f);
        Physics.SyncTransforms();
        yield return null;

        // Analysis
        if (autoAnalyzeAfterGenerate)
        {
            yield return StartCoroutine(RunAnalysisSuite(runId));
        }
    }

    public IEnumerator RunAnalysisSuite(int runId = -1)
    {
        if (runId >= 0 && runId != _pipelineRunId) yield break;
        
        if(!isBatchRunning) UnityEngine.Debug.Log($"<b><color=yellow>[Orchestrator]</color></b> Phase 3: Tactical Analysis Suite... (Run {runId})");

        // Safety fallback by finding missing references
        if (!sightlineAnlz) sightlineAnlz = Object.FindFirstObjectByType<SightlineAnalyzer>();
        if (!coverAnlz) coverAnlz = Object.FindFirstObjectByType<CoverAnalyzer>();

        Stopwatch mathTimer = new Stopwatch();
        mathTimer.Start();

        if (sightlineAnlz)
        {
            bool success = false;
            try
            {
                sightlineAnlz.AnalyzeMap();
                success = true;
            }
            catch (System.Exception e) 
            { 
                UnityEngine.Debug.LogError($"<color=red>[Orchestrator]</color> SightlineAnalyzer CRASHED: {e.Message}"); 
            }

            if (success)
            {
                yield return null; // Yield for a frame after math analysis

                if (symmetryEval != null)
                {
                    symmetryEval.EvaluateMapSymmetry(sightlineAnlz.nodes);
                }
            }
        }

        if (coverAnlz)
        {
            try 
            { 
                coverAnlz.ValidateCoverSetup();

                if (dashboardUI != null)
                {
                    dashboardUI.RefreshCoverStats(coverAnlz);
                }
            }
            catch (System.Exception e) { UnityEngine.Debug.LogError($"<color=red>[Orchestrator]</color> CoverAnalyzer CRASHED: {e.Message}"); }
        }

        mathTimer.Stop();
        _totalComputeTimeMs += mathTimer.ElapsedMilliseconds;

        if (dashboardUI != null)
        {
            // Calculate Pass/Fail status
            float exposure = sightlineAnlz.ExposureScore; 
            bool expPass = exposure >= 0.30f && exposure <= 0.70f;

            float symmetry = symmetryEval.SymmetryDelta; 
            bool symPass = symmetry <= 15.0f;

            float navArea = navChecker.NavigableAreaRatio;
            bool navPass = navArea >= 0.55f && navArea <= 0.85f;

            float chokePoint = navChecker.MaxCentrality;
            bool chokePass = chokePoint >= 0.15f;

            // Update UI
            dashboardUI.RefreshVisibilityStats(exposure, expPass, symmetry, symPass);
            dashboardUI.RefreshNavigationStats(navArea, navPass, chokePoint, chokePass);

            // Handle non batch run
            if (!isBatchRunning)
            {
                bool covPass = coverAnlz != null && coverAnlz.ValidateCoverSetup(false);
                bool mapPassed = covPass && expPass && symPass && navPass && chokePass;

                if (mapPassed)
                {
                    UnityEngine.Debug.Log("<color=yellow>[Orchestrator]</color> Manual Run PASSED. Exporting...");
                    ExportCurrentMap(_pipelineRunId);
                }
            }
        }

        if(!isBatchRunning) UnityEngine.Debug.Log($"<b><color=green>[Orchestrator]</color></b> Pipeline Complete. (Run {runId})");
    }
    #endregion

    #region Helper Methods
    private void CaptureActiveGeometry()
    {
        _spawnedObjects.Clear();

        // Get all root objects in the scene
        GameObject[] allObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (GameObject go in allObjects)
        {
            string n = go.name;

            if (n.StartsWith("Map_") || 
                n.Contains("_Border") || 
                n.StartsWith("Room_") || 
                n.StartsWith("FieldCover_"))
            {
                _spawnedObjects.Add(go);
            }
        }

        // 2. Capture anything specifically parented for safety
        if (coverGen != null) AddUniqueChildrenToList(coverGen.transform);
        if (roomManager != null) AddUniqueChildrenToList(roomManager.transform);
        if (borderGen != null) AddUniqueChildrenToList(borderGen.transform);

        UnityEngine.Debug.Log($"<color=yellow>[Orchestrator]</color> Captured {_spawnedObjects.Count} tactical components (Rooms, Borders, Covers, Floor).");
    }

    private void AddUniqueChildrenToList(Transform parent)
    {
        foreach (Transform child in parent)
        {
            if (!_spawnedObjects.Contains(child.gameObject))
            {
                _spawnedObjects.Add(child.gameObject);
            }
        }
    }

    private void ClearAllGeneratedContent()
    {
        if (navChecker != null) navChecker.ClearNavMeshData();
        if (coverGen != null) coverGen.ClearGeneratedCover();
        if (roomManager != null) roomManager.ClearAllRooms(transform);

        // Safety cleanup for tracked objects
        foreach (GameObject obj in _spawnedObjects) { if (obj != null) Destroy(obj); }
        _spawnedObjects.Clear();
    }

    private void LogRunToCSV(int runID, bool passedMap, CoverMetrics coverMetrics, float navArea, float chokepoint, float symmetry, float exposure)
    {
        string filePath = Application.dataPath + "/ProceduralGeneration_BatchResults.csv";

        if (!File.Exists(filePath))
        {
            string header = "Run_ID,Status,Cover_Density,Avg_Effectiveness,Distribution_Evenness,Clustering_Score,Type_Ratio,Sightline_Exposure,Symmetry_Delta,Navigable_Area,Chokepoint_Centrality\n";
            File.WriteAllText(filePath, header);
        }

        string statusText = passedMap ? "PASSED" : "REJECTED";
        
        string dataRow =
            $"{runID},{statusText}," +
            $"{coverMetrics.CoverDensity:F3}," +
            $"{coverMetrics.AvgCoverEffectiveness:F3}," +
            $"{coverMetrics.DistributionEvenness:F3}," +
            $"{coverMetrics.ClusteringScore:F3}," +
            $"{coverMetrics.TypeRatio.FullCoverRatio:F3}," + 
            $"{exposure:F3}," +
            $"{symmetry:F3}," +
            $"{navArea:F3}," +
            $"{chokepoint:F3}\n";

        File.AppendAllText(filePath, dataRow);
    }
    #endregion

    #region Export Logic
    public void ExportCurrentMap(int seed)
    {
        // 1. Create container for the passed Map
        GameObject mapRoot = new GameObject($"ValidatedMap_Seed_{seed}");
        mapRoot.transform.position = transform.position;

        // 2. Clone current geometry into the root
        foreach (GameObject obj in _spawnedObjects)
        {
            if (obj != null) 
            {
                GameObject clone = Instantiate(obj, mapRoot.transform);
                clone.transform.localPosition = obj.transform.position - transform.position;
            }
        }

        // 3. Save as a Prefab
        #if UNITY_EDITOR
        string folderPath = "Assets/Exports/PassedMaps";
        
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        string localPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{mapRoot.name}.prefab");
        PrefabUtility.SaveAsPrefabAsset(mapRoot, localPath);
        
        UnityEngine.Debug.Log($"<color=white>[Exporter]</color> Successfully exported Seed {seed} to {localPath}");
        #endif

        // Clean up the temporary root
        Destroy(mapRoot);
    }
    #endregion
}