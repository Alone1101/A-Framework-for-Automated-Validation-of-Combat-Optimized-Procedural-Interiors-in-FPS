using UnityEngine;
using System.Collections.Generic;

public class MultiRoomManager : MonoBehaviour
{
    [Header("Room Generation")]
    public MapBorderGenerator mapBorder;
    public Vector2Int roomCountRange = new Vector2Int(1, 5);
    [Min(1)] public int placementAttempts = 25;
    public float roomPadding = 1f;
    
    [Header("Room Variety")]
    public Vector2 roomSizeRange = new Vector2(6, 12);
    public Vector2Int coverCountRange = new Vector2Int(2, 6);

    [Header ("Spawn Settings")]
    public Vector3 teamASpawn;
    public Vector3 teamBSpawn;
    public float spawnSafetyRadius = 3f;
    
    [Header("Connectivity Check")]
    public ConnectivityChecker connectivityChecker;
    public bool checkConnectivityAfterGeneration = true;

    [Header("Global Constraints")]
    public LayerMask roomCollisionMask;

    [Header("Visual Settings")]
    public Material roomWallMaterial;
    public Material roomFloorMaterial;
    public Material roomFullCoverMaterial;
    public Material roomPartialCoverMaterial;

    private List<SingleRoomGenerator> activeRooms = new List<SingleRoomGenerator>();
    private readonly List<Rect> occupiedSpaces = new List<Rect>();
    private static readonly DoorDirection[] doorDirections = { DoorDirection.North, DoorDirection.East, DoorDirection.South, DoorDirection.West };

    #region Main Generation Pipeline
    public void GenerateMultipleRooms()
    {
        ClearRooms();
        occupiedSpaces.Clear();
        
        int minRooms = Mathf.Max(1, roomCountRange.x);
        int maxRooms = Mathf.Max(minRooms, roomCountRange.y);
        int targetRoomCount = Random.Range(minRooms, maxRooms + 1);
        
        for (int i = 0; i < targetRoomCount; i++)
        {
            CreateRandomRoom(i);
        }
    }

    private void CreateRandomRoom(int index)
    {
        float width = Random.Range(roomSizeRange.x, roomSizeRange.y);
        float depth = Random.Range(roomSizeRange.x, roomSizeRange.y);
        int coverTotal = Random.Range(coverCountRange.x, coverCountRange.y + 1);

        if (!TryFindPlacement(width, depth, out Vector2 position))
        {
            Debug.LogWarning($"MultiRoomManager: Failed to place room {index} after {placementAttempts} attempts.");
            return;
        }

        GameObject roomObj = new GameObject("Room_" + index);
        roomObj.transform.SetParent(transform);
        SingleRoomGenerator room = roomObj.AddComponent<SingleRoomGenerator>();
        
        room.mapBorder = mapBorder;
        room.roomWidth = width;
        room.roomDepth = depth;
        room.coverCount = coverTotal;
        room.collisionMask = this.roomCollisionMask;
        room.wallMaterial = this.roomWallMaterial;
        room.floorMaterial = this.roomFloorMaterial;
        room.fullCoverMaterial = this.roomFullCoverMaterial;
        room.partialCoverMaterial = this.roomPartialCoverMaterial;

        AssignRandomDoors(room);
        room.SetRoomPosition(position);
        room.GenerateRoom();
        activeRooms.Add(room);
    }
    #endregion

    #region Door Logic
    private void AssignRandomDoors(SingleRoomGenerator room)
    {
        List<DoorDirection> pool = new List<DoorDirection>(doorDirections);
        DoorDirection first = pool[Random.Range(0, pool.Count)];
        pool.Remove(first);
        DoorDirection second = pool[Random.Range(0, pool.Count)];

        bool north = false, east = false, south = false, west = false;
        SetDoorFlag(first, ref north, ref east, ref south, ref west);
        SetDoorFlag(second, ref north, ref east, ref south, ref west);

        room.ConfigureDoors(north, east, south, west);
    }

    private void SetDoorFlag(DoorDirection direction, ref bool north, ref bool east, ref bool south, ref bool west)
    {
        switch (direction)
        {
            case DoorDirection.North: north = true; break;
            case DoorDirection.East:  east = true;  break;
            case DoorDirection.South: south = true; break;
            case DoorDirection.West:  west = true;  break;
        }
    }
    #endregion

    #region Spatial Constraints & Placement
    private bool TryFindPlacement(float width, float depth, out Vector2 position)
    {
        position = Vector2.zero;
        if (mapBorder == null) return false;

        Bounds bounds = mapBorder.GetPlayableBounds();
        float halfW = width / 2f;
        float halfD = depth / 2f;

        if (halfW <= 0 || halfD <= 0) return false;

        for (int attempt = 0; attempt < placementAttempts; attempt++)
        {
            float x = Random.Range(bounds.min.x + halfW, bounds.max.x - halfW);
            float z = Random.Range(bounds.min.z + halfD, bounds.max.z - halfD);

            Rect candidate = new Rect(
                x - halfW - roomPadding * 0.5f,
                z - halfD - roomPadding * 0.5f,
                width + roomPadding,
                depth + roomPadding
            );

            bool overlaps = false;
            foreach (Rect occupied in occupiedSpaces)
            {
                if (occupied.Overlaps(candidate))
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps && !IsInSpawnZone(candidate))
            {
                position = new Vector2(x, z);
                occupiedSpaces.Add(candidate);
                return true;
            }
        }
        return false;
    }

    private bool IsInSpawnZone(Rect candidate)
    {
        Rect spawnARect = new Rect(teamASpawn.x - spawnSafetyRadius, teamASpawn.z - spawnSafetyRadius, spawnSafetyRadius * 2, spawnSafetyRadius * 2);
        Rect spawnBRect = new Rect(teamBSpawn.x - spawnSafetyRadius, teamBSpawn.z - spawnSafetyRadius, spawnSafetyRadius * 2, spawnSafetyRadius * 2);

        return candidate.Overlaps(spawnARect) || candidate.Overlaps(spawnBRect);
    }
    #endregion

    #region Cleanup & Utility
    public void ClearAllRooms(Transform preserveParent = null)
    {
        foreach (SingleRoomGenerator room in activeRooms)
        {
            if (room == null) continue;
            GameObject go = room.gameObject;
            bool hasAnalyzer = go.GetComponent<SightlineAnalyzer>() != null || go.GetComponent<CoverAnalyzer>() != null;
            if (preserveParent != null && hasAnalyzer)
            {
                go.transform.SetParent(preserveParent, true);
                for (int i = go.transform.childCount - 1; i >= 0; i--)
                    DestroyImmediate(go.transform.GetChild(i).gameObject);
            }
            else
                DestroyImmediate(go);
        }
        activeRooms.Clear();
        occupiedSpaces.Clear();
    }

    private void ClearRooms()
    {
        foreach (SingleRoomGenerator room in activeRooms)
        {
            if (room != null) DestroyImmediate(room.gameObject);
        }
        activeRooms.Clear();
    }

    void OnDrawGizmos()
    {
        if (ValidationManager.Instance != null && ValidationManager.Instance.showExclusionZones)
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawCube(teamASpawn, new Vector3(spawnSafetyRadius * 2, 0.1f, spawnSafetyRadius * 2));
            
            Gizmos.color = new Color(0, 0, 1, 0.3f);
            Gizmos.DrawCube(teamBSpawn, new Vector3(spawnSafetyRadius * 2, 0.1f, spawnSafetyRadius * 2));
        }
    }
    #endregion
}

public enum DoorDirection { North, East, South, West }