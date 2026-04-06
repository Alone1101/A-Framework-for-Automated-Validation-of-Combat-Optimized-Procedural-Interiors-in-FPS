using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CoverFieldGenerator : MonoBehaviour
{
    #region Configuration
    [Header("References")]
    public MapBorderGenerator mapBorder;
    public GameObject coverPrefab;

    [Header("Generation Settings")]
    public LayerMask collisionMask;
    public bool autoGenerateOnStart = true;
    public int coverCount = 40;
    public float spawnMargin = 0.5f;

    [Header("Visuals")]
    public Material fullCoverMaterial;
    public Material partialCoverMaterial;

    private readonly List<GameObject> spawnedCovers = new();
    private Transform coverRoot;
    #endregion

    #region Main Generation Logic
    public void ClearGeneratedCover()
    {
        EnsureCoverRoot();
        ClearCovers(immediate: true);
    }

    [ContextMenu("Generate Cover Field")]
    public void GenerateCoverField()
    {
        if (mapBorder == null) return;

        MultiRoomManager manager = Object.FindFirstObjectByType<MultiRoomManager>();
        EnsureCoverRoot();
        ClearCovers(immediate: true);

        Bounds playableBounds = mapBorder.GetPlayableBounds();
        int attempts = 0;
        int maxAttempts = coverCount * 15;

        for (int i = 0; i < coverCount && attempts < maxAttempts; i++)
        {
            attempts++;

            // 1. Determine archetype and rotation
            CoverArchetype archetype = (CoverArchetype)Random.Range(0, 3);
            Quaternion spawnRotation = Quaternion.Euler(0, Random.Range(0, 4) * 90f, 0);

            // 2. Define dimensions based on archetype
            Vector3 localScale = GetArchetypeScale(archetype);
            float checkHeight = (archetype == CoverArchetype.Pillar) ? 2.5f : 1.1f;

            // 3. Calculate Maximum Extent (Diagonal radius) to prevent border/wall clipping
            float maxExtent = Mathf.Sqrt(Mathf.Pow(localScale.x / 2f, 2) + Mathf.Pow(localScale.z / 2f, 2));

            // 4. Sample Position with the calculated extent buffer
            Vector3 position = SamplePosition(playableBounds, maxExtent); 
            Vector3 physicsCheckPos = new Vector3(position.x, checkHeight / 2f, position.z);

            Physics.SyncTransforms();

            // 5. Constraint Pipeline (Exclusion checks)
            if (IsPointInsideAnyRoom(position, maxExtent, manager)) { i--; continue; }
            if (IsNearAnyRoomDoor(position)) { i--; continue; }
            if (manager != null && IsPointInSpawnZone(physicsCheckPos, manager)) { i--; continue; }

            // 6. Oriented bounding box collision check
            Vector3 halfExtents = (localScale / 2f) * 1.15f;
            if (Physics.CheckBox(physicsCheckPos, halfExtents, spawnRotation, collisionMask))
            {
                i--;
                continue;
            }

            // 7. Spawn
            GameObject cover = CreateCover(i, archetype, position, spawnRotation, localScale);
            spawnedCovers.Add(cover);
        }
    }
    #endregion

    #region Object Creation Helpers
    private Vector3 GetArchetypeScale(CoverArchetype type)
    {
        return type switch
        {
            CoverArchetype.Pillar => new Vector3(0.8f, 2.2f, 0.8f),
            CoverArchetype.Barrier => new Vector3(2.2f, 1.1f, 0.6f),
            CoverArchetype.LBlock => new Vector3(1.8f, 2.2f, 1.8f),
            _ => Vector3.one
        };
    }

    private GameObject CreateCover(int index, CoverArchetype archetype, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        GameObject cover = new GameObject($"FieldCover_{index}_{archetype}");
        cover.transform.SetParent(coverRoot, true);
        cover.transform.position = pos;
        cover.transform.rotation = rot;
        cover.layer = LayerMask.NameToLayer("Cover");

        if (archetype == CoverArchetype.LBlock)
        {
            CreatePart(cover.transform, new Vector3(0, 1.1f, 0), new Vector3(1.2f, 2.2f, 1.2f), fullCoverMaterial);
            CreatePart(cover.transform, new Vector3(0.6f, 1.1f, 0.6f), new Vector3(1.2f, 2.2f, 1.2f), fullCoverMaterial);
        }
        else
        {
            float yOffset = (archetype == CoverArchetype.Pillar) ? 1.1f : 0.55f;
            CreatePart(cover.transform, new Vector3(0, yOffset, 0), scale, (archetype == CoverArchetype.Barrier) ? partialCoverMaterial : fullCoverMaterial);
        }

        cover.AddComponent<CoverObject>().CoverType = (archetype == CoverArchetype.Barrier) ? CoverType.Partial : CoverType.Full;
        cover.tag = "Generated";
        cover.isStatic = true;
        return cover;
    }

    private void CreatePart(Transform parent, Vector3 localPos, Vector3 partScale, Material mat)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPos;
        part.transform.localScale = partScale;
        if (mat != null) part.GetComponent<Renderer>().sharedMaterial = mat; 
        part.layer = parent.gameObject.layer;
    }
    #endregion

    #region Spatial Constraints & Validation
    private Vector3 SamplePosition(Bounds bounds, float extent)
    {
        // Add the physical radius of the object to the margin to prevent border overlap
        float totalMargin = spawnMargin + extent;
        float x = Random.Range(bounds.min.x + totalMargin, bounds.max.x - totalMargin);
        float z = Random.Range(bounds.min.z + totalMargin, bounds.max.z - totalMargin);
        return new Vector3(x, 0f, z);
    }

    private bool IsPointInsideAnyRoom(Vector3 point, float extent, MultiRoomManager manager)
    {
        if (manager == null) return false;
        SingleRoomGenerator[] rooms = Object.FindObjectsByType<SingleRoomGenerator>(FindObjectsSortMode.None);
        foreach (var room in rooms)
        {
            float buffer = 0.5f; 
            // Account for the object's width (extent) when checking room boundaries
            float halfW = (room.roomWidth / 2f) + buffer + extent;
            float halfD = (room.roomDepth / 2f) + buffer + extent;

            if (Mathf.Abs(point.x - room.roomPosition.x) < halfW && 
                Mathf.Abs(point.z - room.roomPosition.y) < halfD)
                return true;
        }
        return false;
    }

    private bool IsNearAnyRoomDoor(Vector3 point)
    {
        float doorBuffer = 1.5f;
        SingleRoomGenerator[] rooms = Object.FindObjectsByType<SingleRoomGenerator>(FindObjectsSortMode.None);
        foreach (var room in rooms)
        {
            float halfW = room.roomWidth / 2f;
            float halfD = room.roomDepth / 2f;
            Vector3 roomCenter = new Vector3(room.roomPosition.x, 0, room.roomPosition.y);

            if (CheckDoor(point, roomCenter + new Vector3(0, 0, halfD), room.northDoor, doorBuffer)) return true;
            if (CheckDoor(point, roomCenter + new Vector3(0, 0, -halfD), room.southDoor, doorBuffer)) return true;
            if (CheckDoor(point, roomCenter + new Vector3(halfW, 0, 0), room.eastDoor, doorBuffer)) return true;
            if (CheckDoor(point, roomCenter + new Vector3(-halfW, 0, 0), room.westDoor, doorBuffer)) return true;
        }
        return false;
    }

    private bool CheckDoor(Vector3 point, Vector3 doorPos, bool exists, float buffer)
    {
        return exists && Vector3.Distance(new Vector3(point.x, 0, point.z), doorPos) < buffer;
    }

    private bool IsPointInSpawnZone(Vector3 point, MultiRoomManager manager)
    {
        float halfSize = manager.spawnSafetyRadius;
        bool inWest = Mathf.Abs(point.x - manager.teamASpawn.x) < halfSize && Mathf.Abs(point.z - manager.teamASpawn.z) < halfSize;
        bool inEast = Mathf.Abs(point.x - manager.teamBSpawn.x) < halfSize && Mathf.Abs(point.z - manager.teamBSpawn.z) < halfSize;
        return inWest || inEast;
    }
    #endregion

    #region Management & Cleanup
    private void ClearCovers(bool immediate = false)
    {
        if (coverRoot == null)
        {
            GameObject existingRoot = GameObject.Find("GeneratedCoverField");
            if (existingRoot != null) coverRoot = existingRoot.transform;
        }

        if (coverRoot != null)
        {
            for (int i = coverRoot.childCount - 1; i >= 0; i--)
            {
                GameObject toDestroy = coverRoot.GetChild(i).gameObject;
                if (immediate || !Application.isPlaying)
                    DestroyImmediate(toDestroy);
                else
                    Destroy(toDestroy);
            }
        }
        spawnedCovers.Clear();
    }

    private void EnsureCoverRoot()
    {
        if (coverRoot == null)
        {
            GameObject existingRoot = GameObject.Find("GeneratedCoverField");
            if (existingRoot != null) coverRoot = existingRoot.transform;
            else
            {
                coverRoot = new GameObject("GeneratedCoverField").transform;
                coverRoot.SetParent(transform, false);
            }
        }
    }
    #endregion
}