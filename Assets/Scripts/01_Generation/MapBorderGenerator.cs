using UnityEngine;
using System.Collections.Generic;

public class MapBorderGenerator : MonoBehaviour
{
    [Header("Map Dimensions")]
    public float mapWidth = 40f;
    public float mapDepth = 30f;
    public float wallHeight = 4f;
    public float wallThickness = 0.3f;

    [Header("Visual Settings")]
    public Material borderWallMaterial;
    public Material floorMaterial;

    [Header("System Settings")]
    [Tooltip("The layer assigned to border walls for raycast detection.")]
    public string environmentLayer = "Default";

    private List<GameObject> generatedObjects = new List<GameObject>();

    #region Main Generation Logic
    [ContextMenu("Generate Map Border")]
    public void GenerateMapBorder()
    {
        ClearPrevious();
        CreateFloor();
        CreateBorderWalls();
        
        Debug.Log($"[MapBorder] Generated: {mapWidth}x{mapDepth}m | {generatedObjects.Count} objects");
    }

    private void CreateFloor()
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(mapWidth / 10f, 1f, mapDepth / 10f);
        floor.name = "Map_Floor";
        
        floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
        floor.isStatic = true;
        
        generatedObjects.Add(floor);
    }

    private void CreateBorderWalls()
    {
        float hW = mapWidth / 2f;
        float hD = mapDepth / 2f;
        float hH = wallHeight / 2f;

        // North & South
        CreateBorderWall(new Vector3(0, hH, hD),  new Vector3(mapWidth, wallHeight, wallThickness), "North_Border");
        CreateBorderWall(new Vector3(0, hH, -hD), new Vector3(mapWidth, wallHeight, wallThickness), "South_Border");

        // East & West
        CreateBorderWall(new Vector3(hW, hH, 0),  new Vector3(wallThickness, wallHeight, mapDepth), "East_Border");
        CreateBorderWall(new Vector3(-hW, hH, 0), new Vector3(wallThickness, wallHeight, mapDepth), "West_Border");
    }

    private void CreateBorderWall(Vector3 position, Vector3 scale, string name)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = position;
        wall.transform.localScale = scale;
        wall.name = name;
        
        // Assign Layer
        wall.layer = LayerMask.NameToLayer(environmentLayer);
        
        if (borderWallMaterial != null) wall.GetComponent<Renderer>().sharedMaterial = borderWallMaterial;
        wall.isStatic = true;
        
        generatedObjects.Add(wall);
    }
    #endregion

    #region Utility & Bounds
    public Bounds GetPlayableBounds()
    {
        float pW = mapWidth - (wallThickness * 2f);
        float pD = mapDepth - (wallThickness * 2f);
        return new Bounds(Vector3.zero, new Vector3(pW, wallHeight, pD));
    }

    private void ClearPrevious()
    {
        foreach (GameObject obj in generatedObjects)
        {
            if (obj != null) DestroyImmediate(obj);
        }
        generatedObjects.Clear();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Bounds bounds = GetPlayableBounds();
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
    #endregion
}