using UnityEngine;

public class SingleRoomGenerator : MonoBehaviour
{
    [Header("Room Dimensions")]
    public float roomWidth = 10f;
    public float roomDepth = 8f;
    public float roomHeight = 3f;
    public float wallThickness = 0.2f;

    [Header("Cover Constraints")]
    public int coverCount = 4;
    public LayerMask collisionMask;
    
    [Header("Visual Settings")]
    public Material fullCoverMaterial;
    public Material partialCoverMaterial;
    public Material wallMaterial;
    public Material floorMaterial;

    [Header("References")]
    public MapBorderGenerator mapBorder;
    public Vector2 roomPosition;
    private Transform roomRoot;

    [Header("Door Configuration")]
    public float doorWidth = 2f;

    public float doorExclusionRadius = 2.0f;

    [SerializeField, HideInInspector] private bool _northDoor = true;
    public bool northDoor { get => _northDoor; set => _northDoor = value; }
    [SerializeField, HideInInspector] private bool _eastDoor = true;
    public bool eastDoor { get => _eastDoor; set => _eastDoor = value; }
    [SerializeField, HideInInspector] private bool _southDoor = false;
    public bool southDoor { get => _southDoor; set => _southDoor = value; }
    [SerializeField, HideInInspector] private bool _westDoor = false;
    public bool westDoor { get => _westDoor; set => _westDoor = value; }

    #region Main Pipeline
    public void GenerateRoom()
    {
        ClearRoom();
        ConstrainToMapBounds();
        CreateFloor();
        CreateWallsWithDoors();
        PlaceCover();
    }

    public void ConfigureDoors(bool north, bool east, bool south, bool west)
    {
        _northDoor = north;
        _eastDoor = east;
        _southDoor = south;
        _westDoor = west;
    }

    public void SetRoomPosition(Vector2 newPos)
    {
        roomPosition = newPos;
        GenerateRoom();
    }

    private void ConstrainToMapBounds()
    {
        if (mapBorder == null) return;
        Bounds bounds = mapBorder.GetPlayableBounds();
        roomWidth = Mathf.Min(roomWidth, bounds.size.x - 2f);
        roomDepth = Mathf.Min(roomDepth, bounds.size.z - 2f);
        
        float maxX = bounds.max.x - roomWidth / 2;
        float minX = bounds.min.x + roomWidth / 2;
        float maxZ = bounds.max.z - roomDepth / 2;
        float minZ = bounds.min.z + roomDepth / 2;
        
        roomPosition.x = Mathf.Clamp(roomPosition.x, minX, maxX);
        roomPosition.y = Mathf.Clamp(roomPosition.y, minZ, maxZ);
    }
    #endregion

    #region Geometry Generation
    private void CreateFloor()
    {
        EnsureRoomRoot();
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.transform.SetParent(roomRoot, true);
        floor.transform.position = new Vector3(roomPosition.x, 0f, roomPosition.y);
        floor.transform.localScale = new Vector3(roomWidth / 10f, 1f, roomDepth / 10f);
        floor.name = "Floor";
        floor.tag = "Generated";
        if (floorMaterial != null) floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
        floor.isStatic = true;
    }

    private void CreateWallsWithDoors()
    {
        EnsureRoomRoot();
        float hW = roomWidth / 2f;
        float hD = roomDepth / 2f;
        float hH = roomHeight / 2f;
        Vector3 ctr = new Vector3(roomPosition.x, 0, roomPosition.y);

        CreateWallSection(_northDoor, ctr + new Vector3(0, hH, hD), new Vector3(roomWidth, roomHeight, wallThickness), "North");
        CreateWallSection(_southDoor, ctr + new Vector3(0, hH, -hD), new Vector3(roomWidth, roomHeight, wallThickness), "South");
        CreateWallSection(_eastDoor, ctr + new Vector3(hW, hH, 0), new Vector3(wallThickness, roomHeight, roomDepth), "East");
        CreateWallSection(_westDoor, ctr + new Vector3(-hW, hH, 0), new Vector3(wallThickness, roomHeight, roomDepth), "West");
    }

    private void CreateWallSection(bool hasDoor, Vector3 pos, Vector3 scale, string label)
    {
        if (hasDoor) CreateWallWithDoorway(pos, scale, label);
        else CreateSolidWall(pos, scale, label + " Wall");
    }

    private void CreateWallWithDoorway(Vector3 pos, Vector3 scale, string label)
    {
        float wallLen = (scale.x > scale.z) ? scale.x : scale.z;
        float segLen = (wallLen - doorWidth) / 2f;
        if (segLen <= 0.5f) return;

        bool isHoriz = (label == "North" || label == "South");
        Vector3 offset = isHoriz ? new Vector3(doorWidth / 2 + segLen / 2, 0, 0) : new Vector3(0, 0, doorWidth / 2 + segLen / 2);
        Vector3 segScale = isHoriz ? new Vector3(segLen, scale.y, scale.z) : new Vector3(scale.x, scale.y, segLen);

        CreateSolidWall(pos - offset, segScale, label + "_Left");
        CreateSolidWall(pos + offset, segScale, label + "_Right");
    }

    private void CreateSolidWall(Vector3 pos, Vector3 scale, string name)
    {
        EnsureRoomRoot();
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.SetParent(roomRoot, true);
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        wall.name = name;
        wall.tag = "Generated";
        wall.layer = LayerMask.NameToLayer("Environment");
        if (wallMaterial != null) wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
        wall.isStatic = true;
    }
    #endregion

    #region Cover Placement
    private void PlaceCover()
    {
        MultiRoomManager manager = Object.FindFirstObjectByType<MultiRoomManager>();
        int maxAttempts = coverCount * 15;
        int currentAttempts = 0;

        for (int i = 0; i < coverCount && currentAttempts < maxAttempts; i++)
        {
            currentAttempts++;
            float x = roomPosition.x + Random.Range(-roomWidth / 2 + 1f, roomWidth / 2 - 1f);
            float z = roomPosition.y + Random.Range(-roomDepth / 2 + 1f, roomDepth / 2 - 1f);

            CoverArchetype archetype = (CoverArchetype)Random.Range(0, 3);
            Quaternion rotation = Quaternion.Euler(0, Random.Range(0, 4) * 90f, 0);
            Vector3 localScale = GetArchetypeScale(archetype);

            if (IsClippingWalls(x, z, localScale)) continue;

            Vector3 checkPos = new Vector3(x, localScale.y / 2f, z);
            Physics.SyncTransforms();

            if (manager != null && IsPointInSpawnZone(checkPos, manager)) { i--; continue; }
            if (IsBlockingDoorway(checkPos, localScale)) { i--; continue; }
            if (Physics.CheckBox(checkPos, (localScale / 2f) * 1.15f, rotation, collisionMask)) { i--; continue; }

            CreateCoverObject(new Vector3(x, 0, z), i, archetype, rotation, localScale);
        }
    }

    private Vector3 GetArchetypeScale(CoverArchetype type)
    {
        return type switch {
            CoverArchetype.Pillar => new Vector3(0.8f, 2.2f, 0.8f),
            CoverArchetype.Barrier => new Vector3(2.2f, 1.1f, 0.6f),
            CoverArchetype.LBlock => new Vector3(1.8f, 2.2f, 1.8f),
            _ => Vector3.one
        };
    }

    private void CreateCoverObject(Vector3 pos, int idx, CoverArchetype arc, Quaternion rot, Vector3 scale)
    {
        EnsureRoomRoot();
        GameObject cover = new GameObject($"Cover_{idx}_{arc}");
        cover.transform.SetParent(roomRoot, true);
        cover.transform.SetPositionAndRotation(pos, rot);
        cover.layer = LayerMask.NameToLayer("Cover");

        if (arc == CoverArchetype.LBlock) {
            CreatePart(cover.transform, new Vector3(0, 1.1f, 0), new Vector3(1.2f, 2.2f, 1.2f), fullCoverMaterial);
            CreatePart(cover.transform, new Vector3(0.6f, 1.1f, 0.6f), new Vector3(1.2f, 2.2f, 1.2f), fullCoverMaterial);
        } else {
            CreatePart(cover.transform, new Vector3(0, scale.y / 2f, 0), scale, arc == CoverArchetype.Barrier ? partialCoverMaterial : fullCoverMaterial);
        }

        cover.AddComponent<CoverObject>().CoverType = arc == CoverArchetype.Barrier ? CoverType.Partial : CoverType.Full;
        cover.tag = "Generated";
        cover.isStatic = true;
    }

    private void CreatePart(Transform parent, Vector3 localPos, Vector3 scale, Material mat)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPos;
        part.transform.localScale = scale;
        if (mat != null) part.GetComponent<Renderer>().sharedMaterial = mat;
        part.layer = parent.gameObject.layer;
    }
    #endregion

    #region Constraints & Utility
    private bool IsClippingWalls(float x, float z, Vector3 scale)
    {
        float extent = Mathf.Max(scale.x, scale.z) / 2f;
        float limitX = (roomWidth / 2f) - wallThickness;
        float limitZ = (roomDepth / 2f) - wallThickness;
        return (Mathf.Abs(x - roomPosition.x) + extent > limitX || Mathf.Abs(z - roomPosition.y) + extent > limitZ);
    }

    private bool IsBlockingDoorway(Vector3 pt, Vector3 scale)
    {
        // Maximum extendable distance from cover's center pivot
        float coverRadius = Mathf.Max(scale.x, scale.z) / 2f;

        // Safe distance = door radius + cover's physical size
        float safeDistance = doorExclusionRadius + coverRadius;

        Vector3 ctr = new Vector3(roomPosition.x, 0, roomPosition.y);
        Vector3 ptFlat = new Vector3(pt.x, 0, pt.z);

        if (_northDoor && Vector3.Distance(ptFlat, ctr + new Vector3(0, 0, roomDepth / 2f)) < safeDistance) return true;
        if (_southDoor && Vector3.Distance(ptFlat, ctr + new Vector3(0, 0, -roomDepth / 2f)) < safeDistance) return true;
        if (_eastDoor && Vector3.Distance(ptFlat, ctr + new Vector3(roomWidth / 2f, 0, 0)) < safeDistance) return true;
        if (_westDoor && Vector3.Distance(ptFlat, ctr + new Vector3(-roomWidth / 2f, 0, 0)) < safeDistance) return true;

        return false;
    }

    private bool IsPointInSpawnZone(Vector3 pt, MultiRoomManager mgr)
    {
        float s = mgr.spawnSafetyRadius;
        bool inA = Mathf.Abs(pt.x - mgr.teamASpawn.x) < s && Mathf.Abs(pt.z - mgr.teamASpawn.z) < s;
        bool inB = Mathf.Abs(pt.x - mgr.teamBSpawn.x) < s && Mathf.Abs(pt.z - mgr.teamBSpawn.z) < s;
        return inA || inB;
    }

    private void ClearRoom()
    {
        if (roomRoot != null) {
            for (int i = roomRoot.childCount - 1; i >= 0; i--) DestroyImmediate(roomRoot.GetChild(i).gameObject);
        }
    }

    private void EnsureRoomRoot()
    {
        if (roomRoot == null) {
            roomRoot = new GameObject("RoomGeometry").transform;
            roomRoot.SetParent(transform, false);
        }
    }

    private void OnDrawGizmos()
    {
        if (ValidationManager.Instance != null && ValidationManager.Instance.showExclusionZones)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(new Vector3(roomPosition.x, roomHeight / 2f, roomPosition.y), new Vector3(roomWidth, roomHeight, roomDepth));
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Vector3 ctr = new Vector3(roomPosition.x, 0.1f, roomPosition.y);
            if (_northDoor) Gizmos.DrawSphere(ctr + new Vector3(0, 0, roomDepth / 2f), doorExclusionRadius);
            if (_southDoor) Gizmos.DrawSphere(ctr + new Vector3(0, 0, -roomDepth / 2f), doorExclusionRadius);
            if (_eastDoor) Gizmos.DrawSphere(ctr + new Vector3(roomWidth / 2f, 0, 0), doorExclusionRadius);
            if (_westDoor) Gizmos.DrawSphere(ctr + new Vector3(-roomWidth / 2f, 0, 0), doorExclusionRadius);
        }
    }
    #endregion
}