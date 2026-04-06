using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class ConnectivityChecker : MonoBehaviour
{
    [Header("NavMesh Settings")]
    public NavMeshSurface navMeshSurface;

    [Header("Validation Settings")]
    [Tooltip("How far from the room center to look for a valid NavMesh point.")]
    public float sampleRadius = 5f;

    [Header("Traffic Analysis Settings")]
    [Tooltip("How many random paths to simulate to find the highest traffic chokepoint.")]
    public int centralitySimulationPaths = 100;

    public float NavigableAreaRatio { get; private set; }
    public float MaxCentrality { get; private set; }
    public bool IsBaking { get; private set; }

    private Coroutine _bakeRoutine;

    #region Primary Execution
    public void StopAndReset()
    {
        if (_bakeRoutine != null)
        {
            StopCoroutine(_bakeRoutine);
            _bakeRoutine = null;
        }
        IsBaking = false;
    }

    public void ClearNavMeshData()
    {
        if (navMeshSurface != null)
            navMeshSurface.RemoveData();
        else
            NavMesh.RemoveAllNavMeshData();
    }

    public void CheckNavMeshConnectivity()
    {
        StopAndReset();
        IsBaking = true;

        _bakeRoutine = StartCoroutine(CheckAfterBake());
    }

    private IEnumerator CheckAfterBake()
    {
        // 1. Clear existing data
        if (navMeshSurface != null)
            navMeshSurface.RemoveData();
        else
            NavMesh.RemoveAllNavMeshData();
        
        yield return new WaitForEndOfFrame();
        
        // Wait for geometry to fully settle before baking
        yield return new WaitForSeconds(0.2f);
        yield return new WaitForFixedUpdate();
        
        // 2. Re-bake NavMesh
        if (navMeshSurface != null)
        {
            Debug.Log("<color=magenta>[ConnectivityChecker]</color> About to build NavMesh...");
            navMeshSurface.BuildNavMesh();
            Debug.Log("<color=magenta>[ConnectivityChecker]</color> NavMesh build complete.");
        }
        else
        {
            Debug.LogWarning("<color=magenta>[ConnectivityChecker]</color> NavMeshSurface not assigned! Connectivity check aborted.");
            IsBaking = false;
            yield break;
        }
        
        // Wait for physics and bake to finalize
        yield return new WaitForEndOfFrame();
        yield return new WaitForFixedUpdate();
        
        // 3. Connectivity Analysis
        if (IsNavMeshFullyConnected())
        {
            Debug.Log("<color=magenta>[ConnectivityChecker]</color> Success: All rooms are connected!</color>");
        }
        else
        {
            Debug.LogWarning("<color=magenta>[ConnectivityChecker]</color> Failure: Isolated rooms detected! Regenerate map suggested.");
        }

        CalculateNavigationMetrics();

        IsBaking = false;
        _bakeRoutine = null;
    }
    #endregion

    #region Core Logic
    private bool IsNavMeshFullyConnected()
    {
        List<Vector3> roomCenters = GetAllRoomCenters();
        
        if (roomCenters.Count < 2)
        {
            Debug.Log("<color=magenta>[ConnectivityChecker]</color> Less than 2 rooms; map is implicitly connected.");
            return true;
        }
        
        // Loop through all room pairs to ensure a complete graph
        for (int i = 0; i < roomCenters.Count; i++)
        {
            if (!NavMesh.SamplePosition(roomCenters[i], out NavMeshHit startHit, sampleRadius, NavMesh.AllAreas))
            {
                Debug.LogWarning($"<color=magenta>[ConnectivityChecker]</color> Room {i} center is off-NavMesh.");
                return false;
            }
            
            for (int j = i + 1; j < roomCenters.Count; j++)
            {
                if (!NavMesh.SamplePosition(roomCenters[j], out NavMeshHit endHit, sampleRadius, NavMesh.AllAreas))
                {
                    Debug.LogWarning($"<color=magenta>[ConnectivityChecker]</color> Room {j} center is off-NavMesh.");
                    return false;
                }
                
                NavMeshPath path = new NavMeshPath();
                bool pathFound = NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, path);
                
                if (!pathFound || path.status != NavMeshPathStatus.PathComplete)
                {
                    Debug.LogWarning($"<color=magenta>[ConnectivityChecker]</color> Path blocked between Room {i} and Room {j}!");
                    return false;
                }
            }
        }
        
        return true;
    }
    #endregion

    #region Metric Calculations
    private void CalculateNavigationMetrics()
    {

        // Fetch the Map Border Generator to get the total area
        MapBorderGenerator border = Object.FindFirstObjectByType<MapBorderGenerator>();
        float totalMapArea = (border != null) ? border.mapWidth * border.mapDepth : 1.0f;

        // 1. Calculate Navigable Area
        float navArea = CalculateTotalNavMeshArea();
        NavigableAreaRatio = totalMapArea > 0 ? (navArea / totalMapArea) : 0f;

        // 2. Calculate Traffic/Chokepoint Centrality
        MaxCentrality = CalculateTrafficCentrality();

        Debug.Log($"<color=cyan>[ConnectivityChecker]</color> Nav Area: {NavigableAreaRatio:F2}, Max Chokepoint: {MaxCentrality:F2}");
    }

    private float CalculateTotalNavMeshArea()
    {
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        float totalArea = 0f;

        for (int i = 0; i < triangulation.indices.Length; i += 3)
        {
            Vector3 p1 = triangulation.vertices[triangulation.indices[i]];
            Vector3 p2 = triangulation.vertices[triangulation.indices[i + 1]];
            Vector3 p3 = triangulation.vertices[triangulation.indices[i + 2]];

            totalArea += Vector3.Cross(p2 - p1, p3 - p1).magnitude / 2f;
        }
        return totalArea;
    }

    private float CalculateTrafficCentrality()
    {
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        if (triangulation.vertices.Length == 0) return 0f;

        Dictionary<Vector2Int, int> trafficGrid = new Dictionary<Vector2Int, int>();
        int highestTraffic = 0;
        int successfulPaths = 0;

        for (int i = 0; i < centralitySimulationPaths; i++)
        {
            Vector3 start = triangulation.vertices[Random.Range(0, triangulation.vertices.Length)];
            Vector3 end = triangulation.vertices[Random.Range(0, triangulation.vertices.Length)];

            NavMeshPath path = new NavMeshPath();
            if (NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
            {
                successfulPaths++;
                
                // Discretize the path into a 2x2 meter grid to count traffic
                foreach (Vector3 corner in path.corners)
                {
                    Vector2Int gridCell = new Vector2Int(Mathf.RoundToInt(corner.x / 2f), Mathf.RoundToInt(corner.z / 2f));
                    
                    if (!trafficGrid.ContainsKey(gridCell)) trafficGrid[gridCell] = 0;
                    trafficGrid[gridCell]++;

                    if (trafficGrid[gridCell] > highestTraffic) highestTraffic = trafficGrid[gridCell];
                }
            }
        }

        // Return a normalized score whereby the highest traffic is divided by total successful paths
        return successfulPaths > 0 ? (float)highestTraffic / successfulPaths : 0f;
    }
    #endregion

    #region Utility
    private List<Vector3> GetAllRoomCenters()
    {
        List<Vector3> centers = new List<Vector3>();
        SingleRoomGenerator[] rooms = Object.FindObjectsByType<SingleRoomGenerator>(FindObjectsSortMode.None);
        
        foreach (var room in rooms)
        {
            if (room != null)
            {
                // Sampling slightly above floor (0.5m) to ensure the NavMesh agent can "stand" there
                centers.Add(new Vector3(room.roomPosition.x, 0.5f, room.roomPosition.y));
            }
        }
        
        return centers;
    }

    // For unstructured map border
    private float FloodFillAreaCheck()
    {
        float stepSize = 0.5f;
        float areaPerStep = stepSize * stepSize;
        float calculatedArea = 0f;

        // Initialize at the center of the map
        Vector3 startPosition = new Vector3(-132.5f, 0.6f, -63.5f);

        Queue<Vector3> pointsToWalk = new Queue<Vector3>();
        HashSet<Vector2Int> visitedPoints = new HashSet<Vector2Int>();

        pointsToWalk.Enqueue(startPosition);
        visitedPoints.Add(new Vector2Int(0, 0));

        int fenceLayerMask = LayerMask.GetMask("Default"); 

        while (pointsToWalk.Count > 0)
        {
            Vector3 currentPoint = pointsToWalk.Dequeue(); 
            calculatedArea += areaPerStep;
            Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            
            foreach (Vector3 dir in directions)
            {
                Vector3 nextStep = currentPoint + (dir * stepSize);
                
                // Convert to grid coordinates for memory
                Vector2Int gridPos = new Vector2Int(
                    Mathf.RoundToInt(nextStep.x / stepSize), 
                    Mathf.RoundToInt(nextStep.z / stepSize)
                );

                if (!visitedPoints.Contains(gridPos))
                {
                    visitedPoints.Add(gridPos);
                    
                    // Physics check against fence
                    bool hitFence = Physics.CheckSphere(nextStep, stepSize / 2.1f, fenceLayerMask);

                    if (!hitFence)
                    {
                        pointsToWalk.Enqueue(nextStep);
                    }
                }
            }
        }

        Debug.Log($"<color=cyan>[Flood Fill Scanner]</color> Finished walking the map! True Playable Area: {calculatedArea:F2}");
        return calculatedArea;
    }
    #endregion
}